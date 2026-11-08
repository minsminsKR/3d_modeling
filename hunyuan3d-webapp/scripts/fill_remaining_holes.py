"""Create a separate candidate with larger CuMesh hole perimeter."""
import argparse
import json
import os
from pathlib import Path
import resource
import time

import cumesh
import torch


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--max-perimeter", type=float, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    source = torch.load(args.source, map_location="cpu", weights_only=False)
    free, total = torch.cuda.mem_get_info()
    if free < 18 * 1024**3:
        raise RuntimeError("Insufficient GPU memory")
    reserve_mib = 2048 if total - free < 1024 * 1048576 else 4096
    fraction = min(.95, (free - reserve_mib * 1048576) / total)
    torch.cuda.set_per_process_memory_fraction(fraction)
    mesh = cumesh.CuMesh()
    mesh.init(source["vertices"].cuda(), source["faces"].cuda())
    start = time.monotonic()
    mesh.fill_holes(max_hole_perimeter=args.max_perimeter)
    mesh.unify_face_orientations()
    vertices, faces = mesh.read()
    temporary = args.output.with_suffix(args.output.suffix + ".partial")
    torch.save({"vertices": vertices.cpu(), "faces": faces.cpu()}, temporary)
    os.replace(temporary, args.output)
    report = {"source": str(args.source), "output": str(args.output),
              "max_perimeter": args.max_perimeter,
              "source_vertices": len(source["vertices"]), "source_faces": len(source["faces"]),
              "output_vertices": len(vertices), "output_faces": len(faces),
              "physical_gpu": int(os.environ["CUDA_VISIBLE_DEVICES"]),
              "free_mib_at_start": round(free / 1048576, 1),
              "reserve_mib": reserve_mib, "memory_fraction": round(fraction, 4),
              "gpu_peak_allocated_mib": round(torch.cuda.max_memory_allocated() / 1048576, 1),
              "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1),
              "elapsed_seconds": round(time.monotonic() - start, 2)}
    args.output.with_suffix(".json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
