#!/usr/bin/env python3
"""Measure CLI state/cookie filesystem work against an immutable old checkout.

Uses synthetic private files in TemporaryDirectory, never production state or a
GPU. Fixture creation and correctness assertions are outside the measured region.
I/O counts observe actual Python file opens, os.replace and os.fsync calls rather
than infer them from the number of status responses. Run on the same filesystem
for comparable wall times; the report records all repetitions and their medians.
"""
from __future__ import annotations

import argparse
import builtins
import contextlib
import copy
import http.cookiejar
import importlib.util
import io
import json
import os
from pathlib import Path
import platform
import statistics
import sys
import tempfile
import time
from unittest.mock import patch
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import modeling_state as optimized


def positive_integer(value):
    parsed = int(value)
    if parsed < 1:
        raise argparse.ArgumentTypeError("must be at least one")
    return parsed


class IOCounter:
    def __init__(self):
        self.counts = {"file_reads": 0, "file_write_opens": 0, "writes": 0, "fsyncs": 0}
        self.stack = contextlib.ExitStack()

    def __enter__(self):
        real_io_open, real_open = io.open, builtins.open
        real_path_open = Path.open
        real_replace, real_fsync = os.replace, os.fsync
        open_depth = 0

        def observed_open(function, file, mode="r", *args, **kwargs):
            nonlocal open_depth
            if not open_depth and isinstance(mode, str):
                if "r" in mode or "+" in mode:
                    self.counts["file_reads"] += 1
                if any(character in mode for character in "wax+"):
                    self.counts["file_write_opens"] += 1
            open_depth += 1
            try:
                return function(file, mode, *args, **kwargs)
            finally:
                open_depth -= 1

        def replaced(*args, **kwargs):
            result = real_replace(*args, **kwargs)
            self.counts["writes"] += 1
            return result

        def synced(*args, **kwargs):
            result = real_fsync(*args, **kwargs)
            self.counts["fsyncs"] += 1
            return result

        self.stack.enter_context(patch.object(io, "open", lambda *a, **k: observed_open(real_io_open, *a, **k)))
        self.stack.enter_context(patch.object(builtins, "open", lambda *a, **k: observed_open(real_open, *a, **k)))
        # Python 3.10 pathlib retains a reference to io.open in its accessor;
        # patching io.open alone therefore misses actual Path.read_text reads.
        self.stack.enter_context(patch.object(Path, "open", lambda *a, **k: observed_open(real_path_open, *a, **k)))
        self.stack.enter_context(patch.object(os, "replace", replaced))
        self.stack.enter_context(patch.object(os, "fsync", synced))
        return self

    def __exit__(self, *args):
        return self.stack.__exit__(*args)


def baseline_module(directory):
    path = directory / "modeling_cli.py"
    if not path.is_file():
        raise SystemExit("--baseline-dir must contain the original modeling_cli.py")
    spec = importlib.util.spec_from_file_location("modeling_cli_state_baseline", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def fixture(module, root, count):
    state = module.State(root, "https://benchmark.invalid")
    for index in range(count):
        request_id = str(uuid.UUID(int=index + 1))
        job = {"id": f"job-{index}", "request_id": request_id, "status": "running",
               "message": "Generating", "elapsed_seconds": 0, "updated_at": "stable"}
        state.write_manifest({"schema_version": 1, "server": state.server,
                              "request_id": request_id, "created_at": "fixture",
                              "state": "submitted", "jobs": [job], "downloads": [],
                              "inputs": [{"filename": "reference.png", "sha256": "fixture",
                                          "size": 10, "local_path": "inputs/000.png"}],
                              "submitted_options": {"model": "trellis2"}})
    return module.State(root, state.server)


def measured(action):
    with IOCounter() as counter:
        started = time.perf_counter()
        action()
        elapsed = time.perf_counter() - started
    return {"wall_seconds": round(elapsed, 6), **counter.counts}


def poll_case(state, iterations):
    job = {"id": "job-0", "request_id": str(uuid.UUID(int=1)), "status": "running",
           "message": "Generating", "elapsed_seconds": 0, "updated_at": "stable"}

    def poll():
        for index in range(iterations):
            job["elapsed_seconds"] = index + 1
            if index >= iterations // 2:
                job["message"] = "Texturing"
            state.remember_job(copy.deepcopy(job))

    result = measured(poll)
    saved = json.loads(state.manifest_path(job["request_id"]).read_text(encoding="utf-8"))
    assert saved["jobs"][0]["message"] == "Texturing", "changed status was not persisted"
    assert saved["inputs"] and saved["submitted_options"], "provenance was not retained"
    result["correctness"] = {"changed_message_saved": True, "provenance_preserved": True}
    return result


def history_case(state, iterations, requests):
    limit, offset = min(50, requests), 0
    expected_ids = [str(uuid.UUID(int=index + 1)) for index in range(limit)]

    def history():
        for _ in range(iterations):
            if hasattr(state, "manifest_page"):
                page = state.manifest_page(limit, offset)
                assert page["total"] == requests
                documents = page["requests"]
            else:
                documents = state.manifests()
                assert len(documents) == requests
                documents = documents[offset:offset + limit]
            assert [item["request_id"] for item in documents] == expected_ids

    result = measured(history)
    result["correctness"] = {"total_requests_correct": True, "identical_ordered_page": True,
                             "page_limit": limit, "page_offset": offset}
    return result


def example_cookie(value):
    return http.cookiejar.Cookie(0, "benchmark-session", value, None, False,
                                 "benchmark.invalid", True, False, "/", True, True,
                                 None, True, None, None, {"HttpOnly": None}, False)


def cookie_case(module, root, responses, use_optimized):
    state = module.State(root, "https://benchmark.invalid")
    jar = http.cookiejar.LWPCookieJar(str(state.cookies))
    jar.set_cookie(example_cookie("synthetic-fixture"))
    if use_optimized:
        signature = optimized.save_cookie_jar(jar, state.cookies, None)

        def save():
            nonlocal signature
            signature = optimized.save_cookie_jar(jar, state.cookies, signature)
    else:
        client = module.Client.__new__(module.Client)
        client.state, client.jar = state, jar
        save = client.save_cookies
        save()

    result = measured(lambda: [save() for _ in range(responses)])
    jar.set_cookie(example_cookie("synthetic-updated-fixture"))
    with IOCounter() as changed_counter:
        save()
    loaded = http.cookiejar.LWPCookieJar(str(state.cookies))
    loaded.load(ignore_discard=True, ignore_expires=False)
    assert next(iter(loaded)).value == "synthetic-updated-fixture", "changed cookie was not persisted"
    result["correctness"] = {"changed_cookie_saved": True,
                             "changed_cookie_writes": changed_counter.counts["writes"],
                             "changed_cookie_fsyncs": changed_counter.counts["fsyncs"]}
    return result


def aggregate(repetitions):
    result = {key: statistics.median(item[key] for item in repetitions)
              for key in ("wall_seconds", "file_reads", "file_write_opens", "writes", "fsyncs")}
    result["correctness"] = repetitions[0].get("correctness", {})
    result["repetitions"] = repetitions
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--requests", type=positive_integer, default=500)
    parser.add_argument("--iterations", type=positive_integer, default=50)
    parser.add_argument("--repetitions", type=positive_integer, default=3)
    parser.add_argument("--cookie-responses", type=positive_integer, default=100)
    args = parser.parse_args()
    baseline = baseline_module(args.baseline_dir.resolve())
    observations = {name: {"before": [], "after": []}
                    for name in ("status_polls", "history", "cookie_responses")}
    with tempfile.TemporaryDirectory(prefix="modeling-cli-benchmark-") as temporary:
        for repetition in range(args.repetitions):
            for label, module, optimized_cookie in (("before", baseline, False), ("after", optimized, True)):
                root = Path(temporary) / f"{repetition}-{label}"
                state = fixture(module, root / "poll", args.requests)
                observations["status_polls"][label].append(poll_case(state, args.iterations))
                state = module.State(root / "poll", state.server)
                observations["history"][label].append(history_case(state, args.iterations, args.requests))
                observations["cookie_responses"][label].append(
                    cookie_case(module, root / "cookies", args.cookie_responses, optimized_cookie))
    results = {name: {label: aggregate(values) for label, values in pair.items()}
               for name, pair in observations.items()}
    for result in results.values():
        before, after = result["before"], result["after"]
        result["speedup"] = round(before["wall_seconds"] / max(after["wall_seconds"], 1e-9), 2)
    report = {"schema_version": 1, "generated_at": optimized.now(),
              "environment": {"platform": platform.platform(), "python": platform.python_version()},
              "fixture": {"requests": args.requests, "status_polls": args.iterations,
                          "history_queries": args.iterations, "cookie_responses": args.cookie_responses,
                          "history_limit": min(50, args.requests), "history_offset": 0,
                          "repetitions": args.repetitions, "inference_or_network": False},
              "method": {"summary": "median of all repetitions; fixture setup and correctness reads excluded",
                         "file_reads": "actual Path.open/io.open/builtins.open read-mode calls, nested opens counted once",
                         "writes": "successful actual os.replace calls (atomic completed writes)",
                         "fsyncs": "successful actual os.fsync calls",
                         "status_change": "one message change, elapsed_seconds changes every response"},
              "results": results}
    encoded = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    print(encoded, end="")


if __name__ == "__main__":
    main()
