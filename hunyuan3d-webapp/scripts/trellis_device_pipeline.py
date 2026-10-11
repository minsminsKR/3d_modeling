"""A100 single/two-GPU placement with bounded auxiliary use and clean caches.

Shape sampling/decoding/mesh processing uses logical CUDA 0 (physical GPU 1).
Material sampling/decoding uses logical CUDA 1 (physical GPU 0) in split mode.
Only independent mesh repair and material decoding overlap. Parameters and
native PBR export remain unchanged; this is not a pooled 80GB CUDA allocator.
"""
from __future__ import annotations

import contextlib
import gc
import time
import os
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor

import torch
from trellis2.modules.sparse import SparseTensor
from trellis2.pipelines import Trellis2ImageTo3DPipeline
from trellis2.representations import MeshWithVoxel


def safe_to(value: torch.Tensor, device) -> torch.Tensor:
    target = torch.device(device)
    # These A100s report peer support, but direct cross-GPU .to() reproduced
    # zero/corrupt values on this host. Use synchronous CPU staging. Never let
    # incorrect peer copies silently change the generated geometry/material.
    if value.is_cuda and target.type == 'cuda' and value.device != target:
        return value.cpu().to(target)
    return value.to(target)


def fresh_sparse(value: SparseTensor, device) -> SparseTensor:
    # Upstream .to() shares _spatial_cache, including CUDA index tables. Those
    # tables cannot be reused on another GPU and can retain large old buffers.
    return SparseTensor(safe_to(value.feats, device), safe_to(value.coords, device),
                        shape=value.shape, scale=value._scale)


def drop_sparse_caches(value):
    if isinstance(value, SparseTensor):
        value._spatial_cache.clear()
    elif isinstance(value, (list, tuple)):
        for item in value:
            drop_sparse_caches(item)


def move_topology_cache(cache, device):
    """Keep subdivision/index ordering while discarding large conv scratch."""
    def move(value):
        if isinstance(value, torch.Tensor):
            return safe_to(value, device)
        if isinstance(value, torch.Size):
            return value
        if isinstance(value, tuple):
            return tuple(move(item) for item in value)
        if isinstance(value, list):
            return [move(item) for item in value]
        if isinstance(value, dict):
            return {key: move(item) for key, item in value.items()}
        return value
    keep = {"shape", "layout", "subdivision"}
    prefixes = ("upsample_", "downsample_", "channel2spatial_", "spatial2channel_")
    return {scale: {key: move(value) for key, value in entries.items()
                    if key in keep or str(key).startswith(prefixes)}
            for scale, entries in cache.items()}


class DevicePipeline(Trellis2ImageTo3DPipeline):
    def configure_devices(self, split: bool, progress=None, shape_physical=1, material_physical=0):
        self.shape_device = torch.device("cuda:0")
        self.material_device = torch.device("cuda:1" if split else "cuda:0")
        self.split_devices = split
        self.shape_physical = shape_physical
        self.material_physical = material_physical if split else shape_physical
        self.stage_timings = {}
        self.memory_snapshots = []
        self.material_decode_fallback = False
        self._progress = progress or (lambda message: None)
        self.low_vram = True
        self.to(self.shape_device)

    @contextlib.contextmanager
    def stage(self, name: str, device):
        previous = self._device
        self._device = device
        started = time.monotonic()
        self._progress(name)
        try:
            with torch.cuda.device(device):
                yield
                torch.cuda.synchronize(device)
        finally:
            self.stage_timings[name] = self.stage_timings.get(name, 0) + time.monotonic() - started
            self._device = previous

    def release_cache(self, *values):
        for value in values:
            drop_sparse_caches(value)
        gc.collect()
        devices = {self.shape_device, self.material_device}
        for device in devices:
            with torch.cuda.device(device):
                torch.cuda.synchronize(device)
                torch.cuda.empty_cache()
                free, total = torch.cuda.mem_get_info(device)
                self.memory_snapshots.append({"logical_gpu": device.index, "free_mib": free // 1048576,
                                              "allocated_mib": torch.cuda.memory_allocated(device) // 1048576,
                                              "reserved_mib": torch.cuda.memory_reserved(device) // 1048576})

    def sample_tex_slat(self, cond, flow_model, shape_slat, sampler_params=None):
        with self.stage(f"TRELLIS.2 재질 생성 · GPU {self.material_physical}", self.material_device):
            moved_cond = {name: safe_to(tensor, self.material_device) for name, tensor in cond.items()}
            moved_shape = fresh_sparse(shape_slat, self.material_device) if self.split_devices else shape_slat
            result = super().sample_tex_slat(moved_cond, flow_model, moved_shape, sampler_params or {})
        self.release_cache(result)
        return result

    @torch.no_grad()
    def decode_latent(self, shape_slat, tex_slat, resolution):
        with self.stage(f"TRELLIS.2 형상 복원 · GPU {self.shape_physical}", self.shape_device):
            meshes, subs = self.decode_shape_slat(shape_slat, resolution)
        # Guide features/coordinates must survive transfer exactly. Retain any
        # topology maps, while discarding large convolution/attention scratch.
        topology = move_topology_cache(shape_slat._spatial_cache, self.material_device)
        for sub in subs:
            extra = move_topology_cache(sub._spatial_cache, self.material_device)
            for scale, entries in extra.items():
                topology.setdefault(scale, {}).update(entries)
        moved_subs = [fresh_sparse(sub, self.material_device) for sub in subs]
        for sub in moved_subs:
            sub._spatial_cache = topology
        original_tex_cache = tex_slat._spatial_cache
        tex_slat._spatial_cache = topology
        original_tex_cache.clear()
        if os.getenv("TRELLIS_SAVE_DECODER_CHECKPOINT") == "1":
            destination = Path(os.environ["TRELLIS_DECODER_CHECKPOINT_DIR"])
            destination.mkdir(parents=True, exist_ok=True)
            torch.save({"shape": (shape_slat.feats.cpu(), shape_slat.coords.cpu(), shape_slat._scale),
                        "texture": (tex_slat.feats.cpu(), tex_slat.coords.cpu(), tex_slat._scale),
                        "subs": [(sub.feats.cpu(), sub.coords.cpu(), sub._scale) for sub in moved_subs],
                        "topology": move_topology_cache(topology, "cpu"), "resolution": resolution}, destination / "decoder-checkpoint.pt")
        drop_sparse_caches(subs)
        subs.clear()
        self.release_cache(shape_slat)

        def repair():
            # Thread-local CUDA device must be explicit: upstream Mesh.cuda()
            # otherwise selects the worker thread's default physical GPU.
            started = time.monotonic()
            with torch.no_grad(), torch.cuda.device(self.shape_device):
                for mesh in meshes:
                    mesh.fill_holes()
                torch.cuda.synchronize(self.shape_device)
            self.stage_timings["mesh_repair"] = time.monotonic() - started

        # In single-GPU mode the decoders are completed first. In split mode
        # only the independent native mesh pass overlaps material decoding.
        executor = ThreadPoolExecutor(max_workers=1) if self.split_devices else None
        repair_future = executor.submit(repair) if executor else None
        try:
            label = f"TRELLIS.2 재질 복원 · GPU {self.material_physical}"
            if self.split_devices:
                label += f" / GPU {self.shape_physical} 메시 보정 병행"
            try:
                with self.stage(label, self.material_device):
                    tex_voxels = self.decode_tex_slat(tex_slat, moved_subs)
            except (torch.OutOfMemoryError, RuntimeError) as error:
                if not self.split_devices or "out of memory" not in str(error).lower():
                    raise
                # Do not raise the helper's memory budget. Reuse the existing
                # sampled codes on the full 40GB shape GPU after mesh repair.
                if repair_future:
                    repair_future.result()
                error.__traceback__ = None
                self.models['tex_slat_decoder'].cpu()
                moved_subs = [fresh_sparse(sub, self.shape_device) for sub in moved_subs]
                tex_primary = fresh_sparse(tex_slat, self.shape_device)
                primary_topology = move_topology_cache(topology, self.shape_device)
                for sub in moved_subs:
                    sub._spatial_cache = primary_topology
                tex_primary._spatial_cache = primary_topology
                topology.clear()
                self.release_cache(tex_slat)
                with self.stage(f"TRELLIS.2 재질 복원 · GPU {self.shape_physical} 메모리 대체", self.shape_device):
                    tex_voxels = self.decode_tex_slat(tex_primary, moved_subs)
                del tex_primary
                self.material_decode_fallback = True
            voxel_shapes = [torch.Size([*voxels.shape, *voxels.spatial_shape]) for voxels in tex_voxels]
            moved_subs.clear()
            self.release_cache(tex_voxels, shape_slat, tex_slat)
            if repair_future:
                repair_future.result()
            else:
                repair()
            out_mesh = []
            with torch.cuda.device(self.shape_device):
                for mesh, voxels, voxel_shape in zip(meshes, tex_voxels, voxel_shapes):
                    out_mesh.append(MeshWithVoxel(
                        mesh.vertices, mesh.faces, origin=[-0.5] * 3,
                        voxel_size=1 / resolution, coords=safe_to(voxels.coords[:, 1:], self.shape_device),
                        attrs=safe_to(voxels.feats, self.shape_device),
                        voxel_shape=voxel_shape,
                        layout=self.pbr_attr_layout))
            del tex_voxels
            self.release_cache(shape_slat, tex_slat)
            return out_mesh
        finally:
            if executor:
                executor.shutdown(wait=True)
