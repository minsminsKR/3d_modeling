"""Offline, optional two-GPU mesh simplification from a preserved remesh."""
import argparse
import json
import math
import os
from pathlib import Path
import resource
import shutil
import subprocess
import sys
import time

import numpy as np
import torch

from dual_mesh import merge, partition


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("checkpoint", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--cumesh-root", type=Path, required=True)
    parser.add_argument("--target-faces", type=int, default=1_000_000)
    parser.add_argument("--max-faces-per-shard", type=int, default=12_000_000)
    parser.add_argument("--intermediate-nonmanifold", action="store_true",
                        help="Only for original pre-remesh meshes; exact seam checks still apply")
    parser.add_argument("--resume-existing", action="store_true",
                        help="Validate and merge preserved shard outputs without GPU work")
    parser.add_argument("--reuse-partitions", action="store_true",
                        help="Reuse saved partition inputs and rerun GPU shards with a new target")
    args = parser.parse_args()
    if args.resume_existing and args.reuse_partitions:
        parser.error("Choose either --resume-existing or --reuse-partitions")
    args.output.mkdir(parents=True, exist_ok=True)
    if shutil.disk_usage(args.output).free < 30 * 1024**3:
        raise RuntimeError("Less than 30 GiB free; refusing large mesh checkpoints")
    started = time.monotonic()
    directory = args.output / "partitions"
    if args.resume_existing:
        manifest = json.loads((directory / "manifest.json").read_text())
        count = manifest["shards"]
        results = [json.loads((directory / f"result_{i}.json").read_text()) for i in range(count)]
        for i in range(count):
            for prefix in ("out_vertices", "out_faces", "out_seam_ids"):
                if not (directory / f"{prefix}_{i}.npy").is_file():
                    raise RuntimeError(f"Incomplete saved shard {i}")
    else:
        if args.reuse_partitions:
            manifest = json.loads((directory / "manifest.json").read_text())
            count = manifest["shards"]
        else:
            data = torch.load(args.checkpoint, map_location="cpu", weights_only=False)
            vertices = np.asarray(data["vertices"], dtype=np.float32)
            faces = np.asarray(data["faces"], dtype=np.int32)
            count = max(2, math.ceil(len(faces) / args.max_faces_per_shard))
            count += count % 2
            manifest = partition(vertices, faces, directory, count)
            del data, vertices, faces
        shard_face_counts = [len(np.load(directory / f"faces_{i}.npy", mmap_mode="r")) for i in range(count)]
        total_faces = sum(shard_face_counts)
        results = []
        for first in range(0, count, 2):
            running = []
            for shard, gpu in ((first, 0), (first + 1, 1)):
                target = max(1, math.ceil(args.target_faces * shard_face_counts[shard] / total_faces))
                env = os.environ.copy()
                env["CUDA_VISIBLE_DEVICES"] = str(gpu)
                env["CUMESH_BOUNDARY_PATCH"] = "1"
                env["CUMESH_PATCH_ROOT"] = str(args.cumesh_root.resolve())
                env["PYTHONPATH"] = str(args.cumesh_root) + os.pathsep + env.get("PYTHONPATH", "")
                log = (args.output / f"shard_{shard}.log").open("w")
                process = subprocess.Popen([sys.executable, str(Path(__file__).with_name("dual_mesh.py")),
                                            str(directory), str(shard), str(target)],
                                           env=env, stdout=log, stderr=subprocess.STDOUT)
                running.append((shard, process, log))
            failures = []
            for shard, process, log in running:
                code = process.wait()
                log.close()
                if code:
                    failures.append((shard, code))
                else:
                    results.append(json.loads((directory / f"result_{shard}.json").read_text()))
            if failures:
                raise RuntimeError(f"Shards failed: {failures}; see logs in {args.output}")
    out_vertices, out_faces = merge(directory, require_closed=not args.intermediate_nonmanifold)
    temporary = args.output / "simplified_mesh.pt.partial"
    final = args.output / "simplified_mesh.pt"
    torch.save({"vertices": torch.from_numpy(out_vertices), "faces": torch.from_numpy(out_faces)}, temporary)
    os.replace(temporary, final)
    summary = {**manifest, "output_vertices": len(out_vertices), "output_faces": len(out_faces),
               "output": str(final), "per_shard": results,
               "seam_exact": True, "closed_topology_verified": not args.intermediate_nonmanifold,
               "resumed_saved_shards": args.resume_existing,
               "reused_partitions": args.reuse_partitions,
               "gpu_face_totals": {str(gpu): sum(x["input_faces"] for x in results if x["physical_gpu"] == gpu)
                                   for gpu in (0, 1)},
               "host_peak_rss_mib": round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024, 1),
               "elapsed_seconds": round(time.monotonic() - started, 2)}
    (args.output / "summary.json").write_text(json.dumps(summary, indent=2))
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
