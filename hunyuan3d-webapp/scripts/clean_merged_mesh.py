"""Experimental whole-mesh cleanup after exact dual-GPU seam welding."""
import argparse
import json
import os
from pathlib import Path
import resource
import time

import cumesh
import torch


def save(mesh, target: Path) -> tuple[int, int]:
    vertices, faces = mesh.read()
    temporary = target.with_suffix(target.suffix + ".partial")
    torch.save({"vertices": vertices.cpu(), "faces": faces.cpu()}, temporary)
    os.replace(temporary, target)
    return len(vertices), len(faces)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    source = torch.load(args.input, map_location="cpu", weights_only=False)
    free, total = torch.cuda.mem_get_info()
    if free < 18 * 1024**3:
        raise RuntimeError("Insufficient GPU memory for cleanup")
    reserve_mib = 2048 if total - free < 1024 * 1048576 else 4096
    fraction = min(.95, (free - reserve_mib * 1048576) / total)
    torch.cuda.set_per_process_memory_fraction(fraction)
    torch.cuda.reset_peak_memory_stats()
    mesh = cumesh.CuMesh()
    mesh.init(source["vertices"].cuda(), source["faces"].cuda())
    report = {"source": str(args.input), "physical_gpu": int(os.environ["CUDA_VISIBLE_DEVICES"]),
              "free_mib_at_start": round(free / 1048576, 1),
              "reserve_mib": reserve_mib, "memory_fraction": round(fraction, 4),
              "stages": []}
    for name, action in (
            ("deduplicate", mesh.remove_duplicate_faces),
            ("repair_nonmanifold", mesh.repair_non_manifold_edges),
            ("fill_holes", lambda: mesh.fill_holes(max_hole_perimeter=3e-2)),
            ("orient", mesh.unify_face_orientations)):
        start = time.monotonic()
        action()
        checkpoint = args.output / f"{name}.pt"
        vertices, faces = save(mesh, checkpoint)
        stage = {"name": name, "vertices": vertices, "faces": faces,
                 "checkpoint": str(checkpoint), "elapsed_seconds": round(time.monotonic() - start, 2)}
        report["stages"].append(stage)
        (args.output / "cleanup_report.json").write_text(json.dumps(report, indent=2))
        print(json.dumps(stage), flush=True)
    report["gpu_peak_allocated_mib"] = round(torch.cuda.max_memory_allocated() / 1048576, 1)
    report["gpu_peak_reserved_mib"] = round(torch.cuda.max_memory_reserved() / 1048576, 1)
    report["host_peak_rss_mib"] = round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1)
    (args.output / "cleanup_report.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
