"""Audit CuMesh UV output against the pre-UV mesh without changing either."""
import json
import os
from pathlib import Path
import sys

import numpy as np
import torch


def keys(faces: np.ndarray) -> np.ndarray:
    f = np.sort(faces.astype(np.int64), axis=1)
    if f.max() >= 2**21:
        raise ValueError("Vertex ID exceeds 21-bit key range")
    return (f[:, 0] << 42) | (f[:, 1] << 21) | f[:, 2]


def main() -> None:
    source_path, uv_path, out_dir = map(Path, sys.argv[1:4])
    ordered_path = Path(sys.argv[4]) if len(sys.argv) > 4 else None
    out_dir.mkdir(parents=True, exist_ok=True)
    source = torch.load(source_path, map_location="cpu", weights_only=False)
    uv = torch.load(uv_path, map_location="cpu", weights_only=False)
    original_vertices = source["vertices"].numpy()
    original_faces = source["faces"].numpy().astype(np.int64)
    tri = original_vertices[original_faces]
    e0 = tri[:, 1] - tri[:, 0]
    e1 = tri[:, 2] - tri[:, 1]
    e2 = tri[:, 0] - tri[:, 2]
    length = np.maximum.reduce([np.linalg.norm(e0, axis=1),
                                np.linalg.norm(e1, axis=1),
                                np.linalg.norm(e2, axis=1)])
    area = np.linalg.norm(np.cross(e0, e1), axis=1) / 2
    removed = ((original_faces[:, 0] == original_faces[:, 1]) |
               (original_faces[:, 1] == original_faces[:, 2]) |
               (original_faces[:, 2] == original_faces[:, 0]) |
               (area < np.minimum(1e-12 * length * length, 1e-24)))
    surviving = np.flatnonzero(~removed)
    used_vertices = np.unique(original_faces[~removed])
    compact_vertices = uv["source_vertices"].numpy()
    compact_faces = uv["source_faces"].numpy().astype(np.int64)
    expected_faces = np.searchsorted(used_vertices, original_faces[~removed])
    vertices_preserved = np.array_equal(compact_vertices, original_vertices[used_vertices])
    source_faces_preserved = np.array_equal(compact_faces, expected_faces)

    uv_faces = uv["vmaps"][uv["faces"]].numpy().astype(np.int64)
    compact_keys = keys(compact_faces)
    uv_keys = keys(uv_faces)
    sorted_indices = np.argsort(compact_keys)
    sorted_keys = compact_keys[sorted_indices]
    locations = np.searchsorted(sorted_keys, uv_keys)
    matching = (locations < len(sorted_keys)) & (sorted_keys[np.minimum(locations, len(sorted_keys) - 1)] == uv_keys)
    duplicates = len(np.unique(compact_keys)) != len(compact_keys)
    if not matching.all() or duplicates or len(uv_faces) != len(compact_faces):
        raise RuntimeError(f"UV face bijection failed: matching={int(matching.sum())}/{len(matching)}, duplicates={duplicates}")
    compact_face_ids = sorted_indices[locations]
    corresponding = compact_faces[compact_face_ids]
    same = np.zeros(len(uv_faces), dtype=bool)
    for shift in range(3):
        same |= np.all(np.roll(uv_faces, shift, axis=1) == corresponding, axis=1)
    print({"cpu_removed": int(removed.sum()), "uv_removed": len(original_faces) - len(compact_faces),
           "vertices_preserved": vertices_preserved, "source_faces_preserved": source_faces_preserved,
           "uv_bijection": bool(matching.all()), "uv_duplicates": duplicates,
           "orientation_preserved": bool(same.all())}, flush=True)
    original_vertex_keys = np.ascontiguousarray(original_vertices).view("V12").reshape(-1)
    compact_vertex_keys = np.ascontiguousarray(compact_vertices).view("V12").reshape(-1)
    sorted_original = np.sort(original_vertex_keys)
    vertex_locations = np.searchsorted(sorted_original, compact_vertex_keys)
    vertex_matches = (vertex_locations < len(sorted_original)) & (
        sorted_original[np.minimum(vertex_locations, len(sorted_original) - 1)] == compact_vertex_keys)
    print({"source_coordinate_match_count": int(vertex_matches.sum()),
           "source_vertices": len(compact_vertices),
           "original_duplicate_coordinates": len(original_vertex_keys) - len(np.unique(original_vertex_keys)),
           "source_duplicate_coordinates": len(compact_vertex_keys) - len(np.unique(compact_vertex_keys))}, flush=True)
    # CuMesh uses CUDA float arithmetic for its zero-area predicate. Match
    # surviving vertices by their original ordered stream instead of trying to
    # reproduce that predicate with CPU floating-point reductions.
    vertex_map = np.empty(len(compact_vertex_keys), dtype=np.int64)
    oi = 0
    for si, value in enumerate(compact_vertex_keys):
        while oi < len(original_vertex_keys) and original_vertex_keys[oi] != value:
            oi += 1
        if oi == len(original_vertex_keys):
            raise RuntimeError(f"Source vertex {si} is not in original order")
        vertex_map[si] = oi
        oi += 1
    mapped_source_faces = vertex_map[compact_faces]
    original_keys = keys(original_faces)
    source_keys = keys(mapped_source_faces)
    original_order = np.argsort(original_keys)
    original_sorted = original_keys[original_order]
    source_locations = np.searchsorted(original_sorted, source_keys)
    source_matching = (source_locations < len(original_sorted)) & (
        original_sorted[np.minimum(source_locations, len(original_sorted) - 1)] == source_keys)
    print({"source_face_geometry_match": int(source_matching.sum()),
           "source_face_count": len(source_keys),
           "original_face_key_duplicates": len(original_keys) - len(np.unique(original_keys))}, flush=True)
    if not source_matching.all() or not same.all():
        raise RuntimeError("UV faces are not an oriented bijection with the source")
    compact_to_original = original_order[source_locations]
    original_face_ids = compact_to_original[compact_face_ids]
    removed_face_ids = np.setdiff1d(np.arange(len(original_faces)), compact_to_original)
    oriented_source = original_faces[compact_to_original]
    oriented = np.zeros(len(mapped_source_faces), dtype=bool)
    for shift in range(3):
        oriented |= np.all(np.roll(mapped_source_faces, shift, axis=1) == oriented_source, axis=1)
    if not oriented.all():
        raise RuntimeError("Original face orientation changed")
    if ordered_path is not None:
        order = np.argsort(original_face_ids, kind="stable")
        ordered_ids = original_face_ids[order]
        if not np.all(np.diff(ordered_ids) > 0):
            raise RuntimeError("Cannot restore a unique original face order")
        uv["faces"] = uv["faces"][torch.from_numpy(order.copy())]
        uv["original_face_ids"] = torch.from_numpy(ordered_ids.copy())
        temporary = ordered_path.with_suffix(ordered_path.suffix + ".partial")
        torch.save(uv, temporary)
        os.replace(temporary, ordered_path)
    np.save(out_dir / "uv_original_face_ids.npy", original_face_ids)
    report = {
        "source_faces": len(original_faces),
        "removed_faces": len(original_faces) - len(compact_faces),
        "removed_zero_area_by_cpu": int(np.count_nonzero(area[removed_face_ids] == 0)),
        "removed_max_area_by_cpu": float(area[removed_face_ids].max()) if len(removed_face_ids) else 0.0,
        "source_vertices_compacted": len(compact_vertices),
        "source_vertices_exact_coordinates": bool(vertex_matches.all()),
        "source_vertices_original_order": True,
        "source_faces_original_order": bool(np.all(np.diff(compact_to_original) > 0)),
        "uv_faces": len(uv_faces),
        "uv_face_bijection": bool(matching.all() and not duplicates),
        "uv_face_orientation_preserved": bool(same.all()),
        "uv_face_rows_reordered": int(np.count_nonzero(compact_face_ids != np.arange(len(compact_face_ids)))),
        "global_face_id_min": int(original_face_ids.min()),
        "global_face_id_max": int(original_face_ids.max()),
        "ordered_checkpoint": str(ordered_path) if ordered_path else None,
    }
    (out_dir / "uv_face_audit.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
