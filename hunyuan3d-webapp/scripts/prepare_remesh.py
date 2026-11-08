"""Pinned TRELLIS.2 dual contouring from a preserved 1536 raw checkpoint."""
import argparse
import json
import os
from pathlib import Path
import resource
import shutil
import time

import cumesh
import torch


def save_atomic(data, target: Path) -> None:
    temporary = target.with_suffix(target.suffix + ".partial")
    torch.save(data, temporary)
    os.replace(temporary, target)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("raw", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--pre-simplified", type=Path,
                        help="Experimental geometry source; deviates from the pinned direct remesh path")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if shutil.disk_usage(args.output).free < 30 * 1024**3:
        raise RuntimeError("Less than 30 GiB free; refusing remesh checkpoints")
    started = time.monotonic()
    raw = torch.load(args.raw, map_location="cpu", weights_only=False)
    actual_resolution = int(raw["actual_resolution"])
    if actual_resolution != 1536:
        raise ValueError(f"Expected actual 1536 resolution; got {actual_resolution}")
    free, total = torch.cuda.mem_get_info()
    if free < 18 * 1024**3:
        raise RuntimeError("Insufficient GPU memory for remeshing")
    reserve_mib = 2048 if total - free < 1024 * 1048576 else 4096
    fraction = min(0.95, (free - reserve_mib * 1048576) / total)
    torch.cuda.set_per_process_memory_fraction(fraction)
    torch.cuda.reset_peak_memory_stats()
    source = torch.load(args.pre_simplified, map_location="cpu", weights_only=False) if args.pre_simplified else raw
    vertices = source["vertices"].cuda()
    faces = source["faces"].cuda()
    mesh = cumesh.CuMesh()
    mesh.init(vertices, faces)
    mesh.fill_holes(max_hole_perimeter=3e-2)
    vertices, faces = mesh.read()
    save_atomic({"vertices": vertices.cpu(), "faces": faces.cpu()},
                args.output / "cleaned_surface.pt")
    del mesh
    aabb = torch.tensor([[-0.5] * 3, [0.5] * 3], device="cuda", dtype=torch.float32)
    center = aabb.mean(dim=0)
    scale = (actual_resolution + 3) / actual_resolution * 1.0
    out_vertices, out_faces = cumesh.remeshing.remesh_narrow_band_dc(
        vertices, faces, center=center, scale=scale, resolution=actual_resolution,
        band=1, project_back=0, verbose=True)
    save_atomic({"vertices": out_vertices.cpu(), "faces": out_faces.cpu()},
                args.output / "remeshed_geometry.pt")
    report = {"actual_resolution": actual_resolution,
              "source": "experimental_pre_simplified" if args.pre_simplified else "pinned_direct",
              "input_faces": len(source["faces"]), "cleaned_faces": len(faces),
              "remeshed_faces": len(out_faces),
              "physical_gpu": int(os.environ["CUDA_VISIBLE_DEVICES"]),
              "free_mib_at_start": round(free / 1048576, 1),
              "reserved_for_others_mib": reserve_mib,
              "memory_fraction": round(fraction, 4),
              "gpu_peak_allocated_mib": round(torch.cuda.max_memory_allocated() / 1048576, 1),
              "gpu_peak_reserved_mib": round(torch.cuda.max_memory_reserved() / 1048576, 1),
              "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1),
              "elapsed_seconds": round(time.monotonic() - started, 2)}
    (args.output / "remesh_report.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
