"""Experimental seam-safe mesh partitioning for the pinned CuMesh build.

All transport between GPUs goes through CPU arrays.  The pinned CuMesh CUDA
kernel must carry cumesh-boundary.patch; otherwise this module refuses to run.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import resource
import subprocess
import sys

import numpy as np


def _row_keys(values: np.ndarray) -> np.ndarray:
    contiguous = np.ascontiguousarray(values)
    return contiguous.view(np.dtype((np.void, contiguous.dtype.itemsize * contiguous.shape[1]))).reshape(-1)


def partition(vertices: np.ndarray, faces: np.ndarray, directory: Path, count: int) -> dict:
    if vertices.dtype != np.float32 or faces.dtype != np.int32:
        raise ValueError("Expected float32 vertices and int32 faces")
    if count < 2 or len(faces) < count:
        raise ValueError("Invalid partition count")
    directory.mkdir(parents=True, exist_ok=False)
    axis = int(np.argmax(np.ptp(vertices, axis=0)))
    centers = vertices[faces[:, 0], axis] + vertices[faces[:, 1], axis] + vertices[faces[:, 2], axis]
    edges = np.quantile(centers, np.arange(1, count) / count)
    shard_ids = np.searchsorted(edges, centers, side="right")
    del centers
    shard_vertices = []
    for shard in range(count):
        selected = faces[shard_ids == shard]
        if not len(selected):
            raise ValueError(f"Empty shard {shard}")
        ids, inverse = np.unique(selected, return_inverse=True)
        local_faces = inverse.reshape(-1, 3).astype(np.int32)
        np.save(directory / f"vertices_{shard}.npy", vertices[ids])
        np.save(directory / f"faces_{shard}.npy", local_faces)
        np.save(directory / f"ids_{shard}.npy", ids.astype(np.int32))
        shard_vertices.append(ids)
    counts = np.zeros(len(vertices), dtype=np.uint8)
    for ids in shard_vertices:
        counts[ids] += 1
    seam_ids = np.flatnonzero(counts > 1).astype(np.int32)
    np.save(directory / "seam_ids.npy", seam_ids)
    np.save(directory / "seam_vertices.npy", vertices[seam_ids])
    manifest = {"axis": axis, "shards": count, "input_vertices": len(vertices),
                "input_faces": len(faces), "seam_vertices": len(seam_ids)}
    (directory / "manifest.json").write_text(json.dumps(manifest, indent=2))
    return manifest


def simplify_shard(directory: Path, shard: int, target: int) -> dict:
    import torch
    import cumesh

    if os.getenv("CUMESH_BOUNDARY_PATCH") != "1":
        raise RuntimeError("Patched CuMesh is required to preserve partition seams")
    patch_root = Path(os.environ["CUMESH_PATCH_ROOT"]).resolve()
    if not Path(cumesh._C.__file__).resolve().is_relative_to(patch_root):
        raise RuntimeError("Patched CuMesh extension was not loaded")
    free, total = torch.cuda.mem_get_info(0)
    if free < 18 * 1024**3:
        raise RuntimeError(f"GPU {os.environ['CUDA_VISIBLE_DEVICES']} has insufficient free memory")
    reserve = 4 * 1024**3
    fraction = min(0.95, (free - reserve) / total)
    torch.cuda.set_per_process_memory_fraction(fraction, 0)
    torch.cuda.reset_peak_memory_stats(0)
    vertices = np.load(directory / f"vertices_{shard}.npy")
    faces = np.load(directory / f"faces_{shard}.npy")
    mesh = cumesh.CuMesh()
    mesh.init(torch.from_numpy(vertices).cuda(), torch.from_numpy(faces).cuda())
    ids = np.load(directory / f"ids_{shard}.npy")
    seam_ids = np.load(directory / "seam_ids.npy")
    protected = np.isin(ids, seam_ids).astype(np.uint8)
    if not hasattr(mesh.cu_mesh, "set_protected_vertices") or not hasattr(mesh.cu_mesh, "set_seam_vertex_ids"):
        raise RuntimeError("CuMesh extension lacks protected seam ID tracking")
    mesh.cu_mesh.set_protected_vertices(torch.from_numpy(protected).cuda())
    marked_ids = np.where(protected != 0, ids, -1).astype(np.int32)
    mesh.cu_mesh.set_seam_vertex_ids(torch.from_numpy(marked_ids).cuda())
    initial = mesh.num_faces
    threshold = 1e-8
    prior = initial
    stalls = 0
    for _ in range(80):
        if prior <= target:
            break
        _, current = mesh.cu_mesh.simplify_step(1e-2, 1e-3, threshold, False)
        if current == prior:
            stalls += 1
        else:
            stalls = 0
        if stalls >= 3 or (prior - current) / max(prior, 1) < 0.01:
            threshold *= 10
        prior = current
        if stalls > 12:
            raise RuntimeError(f"Shard {shard} stalled at {current} faces")
    if prior > target:
        raise RuntimeError(f"Shard {shard} has {prior} faces, above target {target}")
    out_v, out_f = mesh.read()
    out_ids = mesh.cu_mesh.read_seam_vertex_ids()
    np.save(directory / f"out_vertices_{shard}.npy", out_v.cpu().numpy())
    np.save(directory / f"out_faces_{shard}.npy", out_f.cpu().numpy())
    np.save(directory / f"out_seam_ids_{shard}.npy", out_ids.cpu().numpy())
    result = {"shard": shard, "physical_gpu": int(os.environ['CUDA_VISIBLE_DEVICES']),
              "input_faces": initial, "output_faces": prior,
              "protected_vertices": int(protected.sum()),
              "free_mib_at_start": round(free / 1048576, 1),
              "memory_fraction": round(fraction, 4),
              "cuda_peak_allocated_mib": round(torch.cuda.max_memory_allocated() / 1048576, 1),
              "cuda_peak_reserved_mib": round(torch.cuda.max_memory_reserved() / 1048576, 1),
              "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1)}
    (directory / f"result_{shard}.json").write_text(json.dumps(result, indent=2))
    return result


def _seam_directed_edges(faces: np.ndarray, seam_count: int) -> np.ndarray:
    parts = []
    for a, b in ((0, 1), (1, 2), (2, 0)):
        selected = (faces[:, a] < seam_count) & (faces[:, b] < seam_count)
        parts.append(faces[selected][:, [a, b]])
    return np.concatenate(parts)


def _edge_signature(directed: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    first = directed[:, 0].astype(np.uint64)
    second = directed[:, 1].astype(np.uint64)
    keys = (np.minimum(first, second) << 32) | np.maximum(first, second)
    unique, inverse, count = np.unique(keys, return_inverse=True, return_counts=True)
    orientation = np.bincount(inverse, weights=np.where(first < second, 1, -1),
                              minlength=len(unique)).astype(np.int64)
    return unique, count, orientation


def merge(directory: Path, require_closed: bool = True) -> tuple[np.ndarray, np.ndarray]:
    manifest = json.loads((directory / "manifest.json").read_text())
    seam = np.load(directory / "seam_vertices.npy")
    merged_vertices = [seam]
    merged_faces = []
    input_seam_edges = []
    output_seam_edges = []
    seam_ids = np.load(directory / "seam_ids.npy")
    next_vertex = len(seam)
    for shard in range(manifest["shards"]):
        vertices = np.load(directory / f"out_vertices_{shard}.npy")
        faces = np.load(directory / f"out_faces_{shard}.npy")
        output_ids = np.load(directory / f"out_seam_ids_{shard}.npy")
        if len(output_ids) != len(vertices):
            raise ValueError(f"Shard {shard} seam ID count differs from vertices")
        local_to_global = np.empty(len(vertices), dtype=np.int32)
        protected_local = np.flatnonzero(output_ids >= 0)
        interior = np.flatnonzero(output_ids < 0)
        protected_ids = output_ids[protected_local]
        seam_positions = np.searchsorted(seam_ids, protected_ids)
        if np.any(seam_positions >= len(seam_ids)) or np.any(seam_ids[seam_positions] != protected_ids):
            raise ValueError(f"Shard {shard} has unknown seam IDs")
        if len(np.unique(protected_ids)) != len(protected_ids):
            raise ValueError(f"Duplicate seam vertex ID in shard {shard}")
        if not np.array_equal(vertices[protected_local], seam[seam_positions]):
            raise ValueError(f"Shard {shard} moved seam vertices")
        local_to_global[protected_local] = seam_positions
        local_to_global[interior] = next_vertex + np.arange(len(interior), dtype=np.int32)
        ids = np.load(directory / f"ids_{shard}.npy")
        expected = set(np.searchsorted(seam_ids, np.intersect1d(ids, seam_ids)))
        if set(seam_positions) != expected:
            raise ValueError(f"Shard {shard} lost or moved seam vertices")
        merged_vertices.append(vertices[interior])
        next_vertex += len(interior)
        converted = local_to_global[faces]
        merged_faces.append(converted)
        output_seam_edges.append(_seam_directed_edges(converted, len(seam)))
        input_faces = np.load(directory / f"faces_{shard}.npy")
        positions = np.searchsorted(seam_ids, ids)
        on_seam = np.isin(ids, seam_ids)
        input_local_to_seam = np.where(on_seam, positions, len(seam)).astype(np.int32)
        input_seam_edges.append(_seam_directed_edges(input_local_to_seam[input_faces], len(seam)))
    if not all(np.array_equal(a, b) for a, b in zip(
            _edge_signature(np.concatenate(input_seam_edges)),
            _edge_signature(np.concatenate(output_seam_edges)))):
        raise ValueError("Shared seam edge counts or orientations changed")
    vertices = np.concatenate(merged_vertices)
    faces = np.concatenate(merged_faces)
    if np.any(np.diff(np.sort(faces, axis=1), axis=1) == 0):
        raise ValueError("Degenerate merged faces")
    if not require_closed:
        return vertices, faces
    # The original remesh is closed; every edge in a valid welded result must
    # have two incident faces with opposite orientation.
    directed = np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]])
    ordered = np.sort(directed, axis=1)
    unique_edges, inverse, incidence = np.unique(ordered, axis=0, return_inverse=True, return_counts=True)
    if np.any(incidence != 2):
        raise ValueError(f"Merged mesh has {np.count_nonzero(incidence != 2)} non-manifold/boundary edges")
    sign = np.where(directed[:, 0] < directed[:, 1], 1, -1)
    balance = np.bincount(inverse, weights=sign, minlength=len(unique_edges))
    if np.any(balance != 0):
        raise ValueError("Merged face orientations disagree across edges")
    return vertices, faces


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("shard", type=int)
    parser.add_argument("target", type=int)
    args = parser.parse_args()
    print(json.dumps(simplify_shard(args.directory, args.shard, args.target)))


if __name__ == "__main__":
    main()
