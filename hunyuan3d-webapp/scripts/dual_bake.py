"""Experimental TRELLIS.2 UV and 8K PBR bake split by texture rows.

Each tile runs in a separate CUDA_VISIBLE_DEVICES process. All transfers use
CPU checkpoints. Face IDs stay global; inpainting happens after tile assembly.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import resource
import time

import cv2
import numpy as np
from PIL import Image
import torch
import trimesh
import trimesh.visual
import cumesh
import nvdiffrast.torch as dr


def _atomic_save(data, path: Path) -> None:
    temporary = path.with_suffix(path.suffix + ".partial")
    torch.save(data, temporary)
    os.replace(temporary, path)


def configure_cuda() -> dict:
    free, total = torch.cuda.mem_get_info(0)
    if free < 18 * 1024**3:
        raise RuntimeError(f"GPU {os.environ['CUDA_VISIBLE_DEVICES']} has insufficient free memory")
    reserve_mib = 4096
    fraction = min(0.95, (free - reserve_mib * 1048576) / total)
    torch.cuda.set_per_process_memory_fraction(fraction, 0)
    torch.cuda.reset_peak_memory_stats(0)
    return {"physical_gpu": int(os.environ["CUDA_VISIBLE_DEVICES"]),
            "free_mib_at_start": round(free / 1048576, 1),
            "reserved_for_others_mib": reserve_mib, "memory_fraction": round(fraction, 4)}


def prepare_uv(simplified: Path, output: Path) -> dict:
    started = time.monotonic()
    budget = configure_cuda()
    data = torch.load(simplified, map_location="cpu", weights_only=False)
    mesh = cumesh.CuMesh()
    mesh.init(data["vertices"].cuda(), data["faces"].cuda())
    vertices, faces, uvs, vmaps = mesh.uv_unwrap(
        compute_charts_kwargs={"threshold_cone_half_angle_rad": np.radians(90.0),
                               "refine_iterations": 0, "global_iterations": 1,
                               "smooth_strength": 1}, return_vmaps=True, verbose=True)
    mesh.compute_vertex_normals()
    normals = mesh.read_vertex_normals()[vmaps]
    source_vertices, source_faces = mesh.read()
    _atomic_save({"vertices": vertices.cpu(), "faces": faces.cpu(),
                  "uvs": uvs.cpu(), "normals": normals.cpu(),
                  "vmaps": vmaps.cpu(), "source_vertices": source_vertices.cpu(),
                  "source_faces": source_faces.cpu()}, output)
    return {**budget, "uv_vertices": len(vertices), "uv_faces": len(faces),
            "gpu_peak_allocated_mib": round(torch.cuda.max_memory_allocated() / 1048576, 1),
            "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1),
            "elapsed_seconds": round(time.monotonic() - started, 2)}


def rasterize_tile(uvs: torch.Tensor, faces: torch.Tensor, size: int,
                   row_start: int, row_end: int, chunk_size: int = 100000) -> torch.Tensor:
    height = row_end - row_start
    if height <= 0 or row_start < 0 or row_end > size:
        raise ValueError("Invalid tile range")
    # Rasterize against the original full image on each GPU. Cropping afterward
    # preserves the vendor pixel centers and face IDs exactly at tile boundaries.
    full_ndc = torch.cat([uvs * 2 - 1, torch.zeros_like(uvs[:, :1]),
                          torch.ones_like(uvs[:, :1])], dim=-1)[None]
    ctx = dr.RasterizeCudaContext()
    rast = torch.zeros((1, size, size, 4), device=uvs.device, dtype=torch.float32)
    for start in range(0, len(faces), chunk_size):
        chunk, _ = dr.rasterize(ctx, full_ndc, faces[start:start + chunk_size],
                                resolution=[size, size])
        mask = chunk[..., 3:4] > 0
        chunk[..., 3:4] += start
        rast = torch.where(mask, chunk, rast)
    return rast[:, row_start:row_end].contiguous()


def bake_tile(raw_path: Path, surface_path: Path, uv_path: Path, size: int, row_start: int,
              row_end: int, output: Path) -> dict:
    from flex_gemm.ops.grid_sample import grid_sample_3d
    started = time.monotonic()
    budget = configure_cuda()
    raw = torch.load(raw_path, map_location="cpu", weights_only=False)
    surface = torch.load(surface_path, map_location="cpu", weights_only=False)
    uv = torch.load(uv_path, map_location="cpu", weights_only=False)
    aabb = torch.tensor([[-0.5] * 3, [0.5] * 3], device="cuda", dtype=torch.float32)
    voxel_size = torch.as_tensor(raw["voxel_size"], dtype=torch.float32, device="cuda")
    if voxel_size.ndim == 0:
        voxel_size = voxel_size.repeat(3)
    grid_size = ((aabb[1] - aabb[0]) / voxel_size).round().int()
    original_vertices = surface["vertices"].cuda()
    original_faces = surface["faces"].cuda()
    bvh = cumesh.cuBVH(original_vertices, original_faces)
    attr_volume = raw["attrs"].cuda()
    coords = raw["coords"].cuda()
    vertices = uv["vertices"].cuda()
    faces = uv["faces"].cuda()
    uvs = uv["uvs"].cuda()
    rast = rasterize_tile(uvs, faces, size, row_start, row_end)
    mask = rast[0, ..., 3] > 0
    pos = dr.interpolate(vertices[None], rast, faces)[0][0].reshape(-1, 3)
    valid = torch.nonzero(mask.reshape(-1), as_tuple=False).flatten()
    channels = attr_volume.shape[1]
    attrs = torch.zeros((row_end - row_start) * size, channels, device="cuda", dtype=torch.float32)
    sparse_coords = torch.cat([torch.zeros_like(coords[:, :1]), coords], dim=-1)
    for indices in valid.split(500000):
        _, face_id, uvw = bvh.unsigned_distance(pos[indices], return_uvw=True)
        tri = original_vertices[original_faces[face_id.long()]]
        projected = (tri * uvw.unsqueeze(-1)).sum(dim=1)
        sampled = grid_sample_3d(attr_volume, sparse_coords,
                                 shape=torch.Size([1, channels, *grid_size.tolist()]),
                                 grid=((projected - aabb[0]) / voxel_size).reshape(1, -1, 3),
                                 mode="trilinear")
        attrs[indices] = sampled
    _atomic_save({"attrs": attrs.reshape(row_end - row_start, size, channels).cpu(),
                  "mask": mask.cpu(), "row_start": row_start, "row_end": row_end,
                  "size": size}, output)
    return {**budget,
            "row_start": row_start, "row_end": row_end,
            "valid_pixels": len(valid),
            "gpu_peak_allocated_mib": round(torch.cuda.max_memory_allocated() / 1048576, 1),
            "gpu_peak_reserved_mib": round(torch.cuda.max_memory_reserved() / 1048576, 1),
            "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1),
            "elapsed_seconds": round(time.monotonic() - started, 2)}


def assemble(raw_path: Path, uv_path: Path, tile_paths: list[Path], output: Path) -> dict:
    raw = torch.load(raw_path, map_location="cpu", weights_only=False)
    uv = torch.load(uv_path, map_location="cpu", weights_only=False)
    tiles = [torch.load(path, map_location="cpu", weights_only=False) for path in tile_paths]
    tiles.sort(key=lambda x: x["row_start"])
    size = tiles[0]["size"]
    if tiles[0]["row_start"] != 0 or tiles[-1]["row_end"] != size or any(
            left["row_end"] != right["row_start"] for left, right in zip(tiles, tiles[1:])):
        raise ValueError("Tiles do not cover the complete texture")
    attrs = torch.cat([tile["attrs"] for tile in tiles], dim=0).numpy()
    mask = torch.cat([tile["mask"] for tile in tiles], dim=0).numpy()
    layout = raw["layout"]
    channels = {}
    for name in ("base_color", "metallic", "roughness", "alpha"):
        channels[name] = np.clip(attrs[..., layout[name]] * 255, 0, 255).astype(np.uint8)
    invalid = (~mask).astype(np.uint8)
    base_color = cv2.inpaint(channels["base_color"], invalid, 3, cv2.INPAINT_TELEA)
    metallic = cv2.inpaint(channels["metallic"], invalid, 1, cv2.INPAINT_TELEA)[..., None]
    roughness = cv2.inpaint(channels["roughness"], invalid, 1, cv2.INPAINT_TELEA)[..., None]
    alpha = cv2.inpaint(channels["alpha"], invalid, 1, cv2.INPAINT_TELEA)[..., None]
    material = trimesh.visual.material.PBRMaterial(
        baseColorTexture=Image.fromarray(np.concatenate([base_color, alpha], axis=-1)),
        baseColorFactor=np.array([255, 255, 255, 255], dtype=np.uint8),
        metallicRoughnessTexture=Image.fromarray(np.concatenate([
            np.zeros_like(metallic), roughness, metallic], axis=-1)),
        metallicFactor=1.0, roughnessFactor=1.0, alphaMode="OPAQUE", doubleSided=False)
    vertices = uv["vertices"].numpy().copy()
    faces = uv["faces"].numpy().copy()
    uvs = uv["uvs"].numpy().copy()
    normals = uv["normals"].numpy().copy()
    vertices[:, 1], vertices[:, 2] = vertices[:, 2].copy(), -vertices[:, 1].copy()
    normals[:, 1], normals[:, 2] = normals[:, 2].copy(), -normals[:, 1].copy()
    uvs[:, 1] = 1 - uvs[:, 1]
    mesh = trimesh.Trimesh(vertices=vertices, faces=faces, vertex_normals=normals,
                           process=False, visual=trimesh.visual.TextureVisuals(uv=uvs, material=material))
    temporary = output.with_suffix(".partial.glb")
    mesh.export(str(temporary), extension_webp=False)
    os.replace(temporary, output)
    scene = trimesh.load(output, force="scene")
    if not scene.geometry:
        raise RuntimeError("Exported GLB has no geometry")
    return {"model_path": str(output), "texture_size": size,
            "faces": len(faces), "valid_pixels": int(mask.sum()),
            "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1)}


def main() -> None:
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="mode", required=True)
    uv = sub.add_parser("uv")
    uv.add_argument("simplified", type=Path)
    uv.add_argument("output", type=Path)
    tile = sub.add_parser("tile")
    tile.add_argument("raw", type=Path)
    tile.add_argument("surface", type=Path)
    tile.add_argument("uv", type=Path)
    tile.add_argument("size", type=int)
    tile.add_argument("row_start", type=int)
    tile.add_argument("row_end", type=int)
    tile.add_argument("output", type=Path)
    final = sub.add_parser("assemble")
    final.add_argument("raw", type=Path)
    final.add_argument("uv", type=Path)
    final.add_argument("output", type=Path)
    final.add_argument("tiles", type=Path, nargs="+")
    args = parser.parse_args()
    if args.mode == "uv":
        result = prepare_uv(args.simplified, args.output)
    elif args.mode == "tile":
        result = bake_tile(args.raw, args.surface, args.uv, args.size, args.row_start, args.row_end, args.output)
    else:
        result = assemble(args.raw, args.uv, args.tiles, args.output)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
