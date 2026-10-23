"""Measure identical history selection work with setup excluded (no inference)."""
from __future__ import annotations

import argparse
import json
import platform
import statistics
import time
from pathlib import Path

from validate_history_index import JobHistory, baseline, fixture


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--jobs", type=int, default=5000)
    parser.add_argument("--iterations", type=int, default=300)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.jobs < 10 or args.iterations < 1:
        parser.error("jobs must be >=10 and iterations >=1")
    jobs = fixture(args.jobs)
    history = JobHistory(jobs)
    queries = {
        "recent_50": dict(limit=50),
        "offset_page": dict(limit=50, offset=args.jobs // 2),
        "source_cli": dict(source="cli", limit=50),
        "exact_request": dict(request_id=f"request-{args.jobs // 14}", limit=200),
    }
    report = {"fixture_jobs": args.jobs, "iterations": args.iterations,
              "python": platform.python_version(), "platform": platform.platform(),
              "scope": "in-memory API history selection; excludes setup, JSON serialization, HTTP and GPU",
              "queries": {}}
    for name, options in queries.items():
        assert history.page(jobs, **options) == baseline(jobs, **options)
        timings = {}
        for implementation, call in (("before", lambda: baseline(jobs, **options)),
                                     ("after", lambda: history.page(jobs, **options))):
            samples = []
            for _ in range(args.iterations):
                start = time.perf_counter_ns()
                call()
                samples.append((time.perf_counter_ns() - start) / 1e6)
            timings[implementation] = {"median_ms": statistics.median(samples),
                                       "p95_ms": sorted(samples)[int((len(samples)-1) * .95)]}
        timings["speedup"] = timings["before"]["median_ms"] / max(timings["after"]["median_ms"], 1e-9)
        report["queries"][name] = timings
    content = json.dumps(report, ensure_ascii=False, indent=2)
    if args.output:
        args.output.write_text(content + "\n", encoding="utf-8")
    print(content)


if __name__ == "__main__":
    main()
