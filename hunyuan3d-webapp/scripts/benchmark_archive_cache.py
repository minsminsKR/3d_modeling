"""Reproducible, bounded-memory archive benchmark; no GPU or production data.

python scripts/benchmark_archive_cache.py --size-mib 64 --iterations 5
Optional --baseline /path/to/previous/generation_api.py times the previous code
against exactly the same incompressible source files and request contexts.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import random
import statistics
import sys
import tempfile
import time

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))
from flask import Flask
import generation_api
import modeling_archives as cache


def timed(function):
    start = time.perf_counter()
    value = function()
    return time.perf_counter() - start, value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--size-mib", type=int, default=64)
    parser.add_argument("--iterations", type=int, default=5)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--json-output", type=Path)
    args = parser.parse_args()
    if not (1 <= args.size_mib <= 4096 and 1 <= args.iterations <= 100):
        parser.error("size-mib must be 1..4096 and iterations 1..100")
    app = Flask(__name__)
    with tempfile.TemporaryDirectory(prefix=".zipbench-", dir=BASE) as temporary:
        directory = Path(temporary)
        uploads, outputs = directory / "uploads", directory / "outputs"
        job_id = "benchmark-job"
        for path in (uploads / job_id, outputs / job_id):
            path.mkdir(parents=True)
        model = outputs / job_id / "model.glb"
        generator = random.Random(20261023)
        expected_source = hashlib.sha256()
        with model.open("wb") as stream:
            for _ in range(args.size_mib):
                block = generator.randbytes(1024 * 1024)
                expected_source.update(block)
                stream.write(block)
        image = uploads / job_id / "reference.png"
        image.write_bytes(generator.randbytes(8192))
        job = {"id": job_id, "status": "completed", "model_path": str(model), "image_paths": [str(image)],
               "inputs": [{"name": "reference.png"}], "request_id": "benchmark-request"}
        public = {"id": job_id, "status": "completed", "request_id": "benchmark-request", "options": {"seed": 12}}

        def record(module):
            return module.record_for_job(job, public, uploads, outputs)

        def download(module):
            # Stream the response, as a WSGI server would, without loading the
            # full ZIP in RAM. Source generation also uses only 1 MiB blocks.
            with app.test_request_context("/archive"):
                response = module.archive_for_job(job, public, uploads, outputs)
                digest = hashlib.sha256()
                size = 0
                try:
                    for block in response.response:
                        digest.update(block)
                        size += len(block)
                    return {"size": size, "sha256": digest.hexdigest()}
                finally:
                    response.close()

        results = {"schema_version": 1, "fixture": {"size_mib": args.size_mib, "iterations": args.iterations,
                    "source_sha256": expected_source.hexdigest(), "source_bytes": model.stat().st_size + image.stat().st_size,
                    "seed": 20261023}, "runtime": {"python": platform.python_version(), "platform": platform.system()}}
        if args.baseline:
            specification = importlib.util.spec_from_file_location("archive_benchmark_baseline", args.baseline)
            previous = importlib.util.module_from_spec(specification)
            specification.loader.exec_module(previous)
            records, downloads = [], []
            for _ in range(args.iterations):
                elapsed, _ = timed(lambda: record(previous))
                records.append(elapsed)
                elapsed, _ = timed(lambda: download(previous))
                downloads.append(elapsed)
            results["baseline"] = {"record_seconds": records, "record_median_seconds": statistics.median(records),
                                   "archive_seconds": downloads, "archive_median_seconds": statistics.median(downloads),
                                   "source_bytes_read_per_record": results["fixture"]["source_bytes"],
                                   "source_bytes_read_per_archive": results["fixture"]["source_bytes"]}
        cache.METRICS.update(builds=0, cache_hits=0, source_bytes_read=0)
        elapsed, first = timed(lambda: record(generation_api))
        cold_metrics = dict(cache.METRICS)
        results["cold"] = {"record_build_seconds": elapsed, "metrics": cold_metrics,
                           "archive_size": first["archive"]["size"]}
        records, downloads = [], []
        for _ in range(args.iterations):
            elapsed, current = timed(lambda: record(generation_api))
            records.append(elapsed)
            assert current["archive"] == first["archive"]
            elapsed, downloaded = timed(lambda: download(generation_api))
            downloads.append(elapsed)
            assert downloaded["sha256"] == first["archive"]["sha256"]
            assert downloaded["size"] == first["archive"]["size"]
        warm_metrics = {key: cache.METRICS[key] - cold_metrics[key] for key in cache.METRICS}
        assert warm_metrics["builds"] == 0 and warm_metrics["source_bytes_read"] == 0
        results["warm"] = {"record_seconds": records, "record_median_seconds": statistics.median(records),
                           "archive_seconds": downloads, "archive_median_seconds": statistics.median(downloads),
                           "metrics": warm_metrics}
        if "baseline" in results:
            results["speedup"] = {"record": results["baseline"]["record_median_seconds"] / statistics.median(records),
                                  "archive": results["baseline"]["archive_median_seconds"] / statistics.median(downloads)}
        rendered = json.dumps(results, ensure_ascii=False, indent=2)
        if args.json_output:
            args.json_output.write_text(rendered + "\n", encoding="utf-8")
        print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
