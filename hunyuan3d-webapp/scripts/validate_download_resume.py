"""Exercise archive recovery over real TCP and new CLI processes, without GPU.

python scripts/validate_download_resume.py
python scripts/validate_download_resume.py --benchmark-json report.json

Fixtures are deliberately uncompressed, so transferred bytes measure recovery
rather than compression. No production jobs, credentials or data are touched.
"""
from __future__ import annotations

import argparse
import hashlib
import http.server
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import uuid
import zipfile
from unittest.mock import patch

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))
import modeling_cli as cli
import modeling_download as download

MIB = 1024 * 1024
JOB = "fixture-resumable-job"
REQUEST = str(uuid.uuid4())
BENCHMARK_REPORT = None


def fixture_archive(directory, name="fixture.zip", size=8 * MIB, marker=b"M", request_id=REQUEST):
    path = directory / name
    digest = hashlib.sha256()
    block = marker * MIB
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_STORED) as archive:
        with archive.open("model.glb", "w") as model:
            remaining = size
            while remaining:
                chunk = block[:min(remaining, len(block))]
                model.write(chunk)
                digest.update(chunk)
                remaining -= len(chunk)
        image = b"fixture-input-image"
        archive.writestr("inputs/reference.png", image)
        record = {"id": JOB, "request_id": request_id,
                  "inputs": [{"archive_name": "inputs/reference.png", "size": len(image),
                              "sha256": hashlib.sha256(image).hexdigest()}],
                  "artifacts": [{"name": "model.glb", "size": size, "sha256": digest.hexdigest()}]}
        archive.writestr("generation-record.json", json.dumps(record))
    sha = cli.sha256_file(path)
    return path, {**record, "archive": {"etag": f'"{sha}"', "sha256": sha,
                                        "size": path.stat().st_size, "version": sha}}


class Fixture:
    def __init__(self, directory, size=8 * MIB):
        self.directory = directory
        self.path, self.record = fixture_archive(directory, size=size)
        self.mode = "normal"
        self.cut = 3 * MIB
        self.etags = True
        self.logs = []
        self.sent = 0
        self.delay = 0
        self.record_switch = None
        self.posts = 0
        owner = self

        class Handler(http.server.BaseHTTPRequestHandler):
            # One connection/request keeps intentionally aborted transfers from
            # emitting irrelevant keep-alive reset tracebacks in test output.
            protocol_version = "HTTP/1.0"

            def log_message(self, *args):
                pass

            def json(self, value):
                payload = json.dumps(value).encode()
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

            def do_GET(self):
                if self.path.endswith("/record"):
                    record = dict(owner.record)
                    if not owner.etags:
                        record.pop("archive", None)
                    if owner.record_switch:
                        owner.path, owner.record = owner.record_switch
                        owner.record_switch = None
                    return self.json(record)
                if self.path.startswith("/api/jobs?"):
                    return self.json({"jobs": [{"id": JOB, "request_id": REQUEST,
                                                 "status": "completed", "web_url": f"/jobs/{JOB}"}], "total": 1})
                if self.path == f"/api/jobs/{JOB}":
                    return self.json({"job": {"id": JOB, "request_id": owner.record.get("request_id"), "status": "completed",
                                               "web_url": f"/jobs/{JOB}"}})
                if not self.path.endswith("/archive"):
                    self.send_error(404)
                    return
                request_range, if_range = self.headers.get("Range"), self.headers.get("If-Range")
                owner.logs.append({"range": request_range, "if_range": if_range,
                                   "accept_encoding": self.headers.get("Accept-Encoding")})
                if owner.mode in {"auth", "forbidden"}:
                    status = 401 if owner.mode == "auth" else 403
                    self.send_response(status)
                    self.send_header("Content-Length", "0")
                    self.end_headers()
                    return
                if owner.mode == "redirect":
                    self.send_response(302)
                    self.send_header("Location", "http://foreign.invalid/archive")
                    self.send_header("Content-Length", "0")
                    self.end_headers()
                    return
                total = owner.path.stat().st_size
                etag = owner.record["archive"]["etag"]
                start, status = 0, 200
                if request_range and if_range == etag and owner.mode != "ignore":
                    start = int(request_range.split("=")[1].split("-")[0])
                    status = 206
                if owner.mode == "force416" or start >= total:
                    self.send_response(416)
                    self.send_header("Content-Length", "0")
                    self.send_header("Content-Range", f"bytes */{total}")
                    if owner.etags:
                        self.send_header("ETag", etag)
                    self.end_headers()
                    return
                self.send_response(status)
                self.send_header("Content-Type", "text/html" if owner.mode == "html" else "application/zip")
                if owner.etags:
                    self.send_header("ETag", '"wrong-tag"' if owner.mode == "wrong_etag" and status == 206 else etag)
                self.send_header("Accept-Ranges", "bytes")
                if owner.mode == "gzip":
                    self.send_header("Content-Encoding", "gzip")
                if status == 206:
                    range_start = start + 1 if owner.mode == "wrong_range" else start
                    self.send_header("Content-Range", f"bytes {range_start}-{total - 1}/{total}")
                length = total - start
                self.send_header("Content-Length", str(length + 1 if owner.mode == "wrong_size" else length))
                self.end_headers()
                stop = min(total, start + owner.cut) if owner.mode == "interrupt" else total
                try:
                    with owner.path.open("rb") as source:
                        source.seek(start)
                        remaining = stop - start
                        while remaining:
                            chunk = source.read(min(64 * 1024, remaining))
                            if not chunk:
                                break
                            self.wfile.write(chunk)
                            owner.sent += len(chunk)
                            remaining -= len(chunk)
                            if owner.delay:
                                time.sleep(owner.delay)
                    self.wfile.flush()
                    if stop != total:
                        self.close_connection = True
                        self.connection.shutdown(socket.SHUT_RDWR)
                except (BrokenPipeError, ConnectionError, OSError):
                    pass

            def do_POST(self):
                owner.posts += 1
                self.send_error(501, "Fixture must never generate")

        self.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.server.daemon_threads = True
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.origin = f"http://127.0.0.1:{self.server.server_port}"

    def close(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(5)


class DownloadResumeTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix=".download-test-", dir=BASE)
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.fixture = Fixture(self.directory)
        self.addCleanup(self.fixture.close)
        self.output = self.directory / "results"
        self.state = self.directory / "state"
        self.part = self.output / f".{JOB}.zip.part"
        self.sidecar = self.output / f".{JOB}.zip.part.json"

    def command(self, *, expected=0, output=None, timeout=15):
        completed = subprocess.run([sys.executable, str(BASE / "modeling_cli.py"),
                                    "--server", self.fixture.origin, "--state-dir", str(self.state),
                                    "download", JOB, "--output", str(output or self.output),
                                    "--request-timeout", "5", "--json"], capture_output=True,
                                   text=True, timeout=timeout)
        self.assertEqual(completed.returncode, expected, completed.stdout + completed.stderr)
        return json.loads(completed.stdout)

    def partial(self):
        self.fixture.mode = "interrupt"
        result = self.command(expected=1)
        self.assertEqual(result["code"], "NETWORK_ERROR", result)
        self.assertTrue(result["details"]["resume_available"])
        self.assertEqual(result["details"]["partial_bytes"], self.fixture.cut)
        self.assertEqual(self.part.stat().st_size, self.fixture.cut)
        self.fixture.mode = "normal"
        return result

    def assert_final(self, result):
        self.assertEqual(result["archive_sha256"], self.fixture.record["archive"]["sha256"])
        self.assertEqual(result["archive_bytes"], self.fixture.path.stat().st_size)
        self.assertFalse(self.part.exists())
        self.assertFalse(self.sidecar.exists())
        receipt = json.loads(Path(result["receipt"]).read_text())
        self.assertEqual(receipt["resumed_bytes"], result["resumed_bytes"])
        self.assertEqual(receipt["received_bytes"], result["received_bytes"])

    def test_network_interruption_resumes_in_new_process_with_remaining_bytes_only(self):
        self.partial()
        before = self.fixture.sent
        result = self.command()
        self.assert_final(result)
        remaining = self.fixture.path.stat().st_size - self.fixture.cut
        self.assertEqual(result["resumed_bytes"], self.fixture.cut)
        self.assertEqual(result["received_bytes"], remaining)
        self.assertEqual(self.fixture.sent - before, remaining)
        self.assertEqual(self.fixture.logs[-1]["range"], f"bytes={self.fixture.cut}-")
        self.assertEqual(self.fixture.logs[-1]["if_range"], self.fixture.record["archive"]["etag"])
        self.assertEqual(self.fixture.logs[-1]["accept_encoding"], "identity")

    def test_legacy_api_job_with_non_uuid_request_id_still_downloads(self):
        self.fixture.path, self.fixture.record = fixture_archive(self.directory, "legacy.zip", request_id="request-00001")
        result = self.command()
        self.assert_final(result)
        receipt = json.loads(Path(result["receipt"]).read_text())
        self.assertEqual(receipt["generation_record"]["request_id"], "request-00001")

    def test_snapshot_change_discards_old_prefix_and_fetches_latest(self):
        self.partial()
        self.fixture.path, self.fixture.record = fixture_archive(self.directory, "new.zip", marker=b"N")
        result = self.command()
        self.assert_final(result)
        self.assertEqual(result["resumed_bytes"], 0)
        self.assertIsNone(self.fixture.logs[-1]["range"])

    def test_if_range_200_never_appends_full_response_to_old_bytes(self):
        self.partial()
        self.fixture.mode = "ignore"
        result = self.command()
        self.assert_final(result)
        self.assertEqual(result["resumed_bytes"], 0)
        self.assertEqual(result["received_bytes"], self.fixture.path.stat().st_size)

    def test_snapshot_changes_between_record_and_range_get(self):
        self.partial()
        previous_etag = self.fixture.record["archive"]["etag"]
        self.fixture.record_switch = fixture_archive(self.directory, "raced.zip", marker=b"R")
        result = self.command()
        self.assert_final(result)
        self.assertEqual(self.fixture.logs[-1]["if_range"], previous_etag)
        self.assertEqual(result["resumed_bytes"], 0)
        self.assertEqual(result["received_bytes"], self.fixture.path.stat().st_size)

    def test_collect_failure_resumes_same_uuid_then_reuses_verified_receipt(self):
        common = [sys.executable, str(BASE / "modeling_cli.py"), "--server", self.fixture.origin,
                  "--state-dir", str(self.state)]

        def collect(command, expected):
            result = subprocess.run([*common, command, REQUEST, "--output", str(self.output), "--json"],
                                    capture_output=True, text=True, timeout=15)
            self.assertEqual(result.returncode, expected, result.stdout + result.stderr)
            return json.loads(result.stdout)

        self.fixture.mode = "interrupt"
        interrupted = collect("collect", 2)
        self.assertEqual(interrupted["download_errors"][0]["code"], "NETWORK_ERROR")
        self.assertTrue(interrupted["download_errors"][0]["details"]["resume_available"])
        self.fixture.mode = "normal"
        resumed = collect("resume", 0)
        self.assert_final(resumed["downloads"][0])
        self.assertEqual(resumed["downloads"][0]["resumed_bytes"], self.fixture.cut)
        transfers = len(self.fixture.logs)
        cached = collect("collect", 0)
        self.assertTrue(cached["downloads"][0]["skipped"])
        self.assertEqual(cached["downloads"][0]["received_bytes"], 0)
        self.assertEqual(len(self.fixture.logs), transfers)
        self.assertEqual(self.fixture.posts, 0)

    def test_wrong_206_range_etag_size_and_encoded_responses_preserve_prefix(self):
        self.partial()
        prefix = cli.sha256_file(self.part)
        for mode in ("wrong_range", "wrong_etag", "wrong_size", "gzip"):
            with self.subTest(mode=mode):
                self.fixture.mode = mode
                result = self.command(expected=1)
                self.assertEqual(result["code"], "PROTOCOL_ERROR", result)
                self.assertEqual(cli.sha256_file(self.part), prefix)
        self.fixture.mode = "normal"
        self.assert_final(self.command())

    def test_complete_partial_416_is_verified_without_retransmission(self):
        self.output.mkdir()
        payload = self.fixture.path.read_bytes()
        self.part.write_bytes(payload)
        sha = hashlib.sha256(payload).hexdigest()
        cli.atomic_json(self.sidecar, {"schema_version": 1, "server": self.fixture.origin, "job_id": JOB,
                                      "url": f"{self.fixture.origin}/api/jobs/{JOB}/archive",
                                      "etag": self.fixture.record["archive"]["etag"], "size": len(payload),
                                      "sha256": sha, "checkpoint_bytes": len(payload), "prefix_sha256": sha})
        result = self.command()
        self.assert_final(result)
        self.assertEqual(result["received_bytes"], 0)
        self.assertEqual(result["resumed_bytes"], len(payload))

    def test_incorrect_416_has_one_bounded_full_restart(self):
        self.partial()
        self.fixture.mode = "force416"
        start = len(self.fixture.logs)
        result = self.command(expected=1)
        self.assertEqual(result["code"], "PROTOCOL_ERROR")
        self.assertEqual(len(self.fixture.logs) - start, 2)
        self.assertIsNone(self.fixture.logs[-1]["range"])

    def test_modified_prefix_or_sidecar_forces_full_safe_restart(self):
        for mode in ("bytes", "json", "foreign", "oversize"):
            with self.subTest(mode=mode):
                self.partial()
                if mode == "bytes":
                    with self.part.open("r+b") as handle:
                        handle.write(b"modified")
                elif mode == "json":
                    self.sidecar.write_text("invalid-json")
                elif mode == "oversize":
                    self.sidecar.write_bytes(b"x" * 17000)
                else:
                    data = json.loads(self.sidecar.read_text())
                    data["server"] = "https://foreign.invalid"
                    cli.atomic_json(self.sidecar, data)
                result = self.command()
                self.assert_final(result)
                self.assertEqual(result["resumed_bytes"], 0)
                self.assertIsNone(self.fixture.logs[-1]["range"])

    def test_crash_tail_truncated_to_last_durable_prefix(self):
        self.partial()
        with self.part.open("ab") as handle:
            handle.write(b"UNCOMMITTED CRASH TAIL")
        result = self.command()
        self.assert_final(result)
        self.assertEqual(result["resumed_bytes"], self.fixture.cut)

    def test_hard_kill_during_large_transfer_recovers_durable_checkpoint(self):
        self.fixture.path, self.fixture.record = fixture_archive(self.directory, "kill.zip", size=64 * MIB)
        self.fixture.delay = 0.003
        command = [sys.executable, str(BASE / "modeling_cli.py"), "--server", self.fixture.origin,
                   "--state-dir", str(self.state), "download", JOB,
                   "--output", str(self.output), "--json"]
        process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline, durable = time.monotonic() + 15, 0
            while time.monotonic() < deadline and process.poll() is None:
                try:
                    durable = json.loads(self.sidecar.read_text())["checkpoint_bytes"]
                    if durable >= download.CHECKPOINT_BYTES:
                        break
                except (OSError, ValueError, KeyError):
                    pass
                time.sleep(0.01)
            self.assertGreaterEqual(durable, download.CHECKPOINT_BYTES)
            process.kill()
            process.communicate(timeout=10)
        finally:
            if process.poll() is None:
                process.kill()
                process.communicate(timeout=10)
        committed = json.loads(self.sidecar.read_text())["checkpoint_bytes"]
        self.fixture.delay = 0
        result = self.command(timeout=30)
        self.assert_final(result)
        self.assertEqual(result["resumed_bytes"], committed)
        self.assertEqual(result["received_bytes"], self.fixture.path.stat().st_size - committed)

    def test_ctrl_c_checkpoints_bytes_and_returns_machine_readable_recovery(self):
        client = cli.Client(cli.State(self.state, self.fixture.origin), 5)
        original = client.session.request

        def interrupting_response(*args, **kwargs):
            response = original(*args, **kwargs)
            if str(args[1]).endswith("/archive"):
                iterator = response.iter_content

                def interrupted(chunk_size):
                    for index, chunk in enumerate(iterator(chunk_size)):
                        if index == 2:
                            raise KeyboardInterrupt()
                        yield chunk
                response.iter_content = interrupted
            return response

        with patch.object(client.session, "request", side_effect=interrupting_response):
            with self.assertRaises(cli.ClientError) as raised:
                client.download(JOB, self.output)
        self.assertEqual(raised.exception.code, "INTERRUPTED")
        self.assertEqual(raised.exception.details["partial_bytes"], 2 * MIB)
        self.assertTrue(raised.exception.details["resume_available"])
        result = self.command()
        self.assert_final(result)
        self.assertEqual(result["resumed_bytes"], 2 * MIB)

    def test_two_processes_cannot_mutate_same_job_output_concurrently(self):
        self.fixture.delay = 0.04
        command = [sys.executable, str(BASE / "modeling_cli.py"), "--server", self.fixture.origin,
                   "--state-dir", str(self.state), "download", JOB,
                   "--output", str(self.output), "--json"]
        process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline = time.monotonic() + 10
            while not self.sidecar.exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(0.01)
            self.assertTrue(self.sidecar.exists())
            second = subprocess.run([*command, "--request-timeout", "1"],
                                    capture_output=True, text=True, timeout=5)
            self.assertEqual(second.returncode, 1, second.stdout + second.stderr)
            payload = json.loads(second.stdout)
            self.assertEqual(payload["code"], "LOCAL_IO_ERROR")
            self.assertTrue(payload["retryable"])
            stdout, stderr = process.communicate(timeout=15)
            self.assertEqual(process.returncode, 0, stdout + stderr)
            self.assert_final(json.loads(stdout))
        finally:
            if process.poll() is None:
                process.kill()
                process.communicate(timeout=10)

    def test_old_server_without_etag_restarts_full_instead_of_reusing_bytes(self):
        self.fixture.etags = False
        self.fixture.mode = "interrupt"
        first = self.command(expected=1)
        self.assertFalse(first["details"]["resume_available"])
        self.fixture.mode = "normal"
        result = self.command()
        self.assert_final(result)
        self.assertIsNone(self.fixture.logs[-1]["range"])
        self.assertEqual(result["resumed_bytes"], 0)

    def test_authentication_html_and_cross_origin_errors_preserve_valid_partial(self):
        self.partial()
        prefix = cli.sha256_file(self.part)
        for mode, code in (("auth", "AUTH_REQUIRED"), ("forbidden", "AUTH_REJECTED"),
                           ("html", "AUTH_REQUIRED"), ("redirect", "PROTOCOL_ERROR")):
            with self.subTest(mode=mode):
                self.fixture.mode = mode
                result = self.command(expected=1)
                self.assertEqual(result["code"], code, result)
                self.assertTrue(result["details"]["resume_available"])
                self.assertEqual(cli.sha256_file(self.part), prefix)

    def test_corrupt_replacement_preserves_last_verified_archive_and_receipt(self):
        good = self.command()
        old_zip = Path(good["archive"]).read_bytes()
        old_receipt = Path(good["receipt"]).read_bytes()
        # Hash metadata agrees with corrupt bytes; ZIP/provenance verification
        # must still independently reject the new replacement.
        self.fixture.path = self.directory / "corrupt.zip"
        self.fixture.path.write_bytes(b"corrupt snapshot")
        sha = cli.sha256_file(self.fixture.path)
        self.fixture.record["archive"] = {"etag": f'"{sha}"', "sha256": sha,
                                            "size": self.fixture.path.stat().st_size, "version": sha}
        result = self.command(expected=1)
        self.assertEqual(result["code"], "INTEGRITY_ERROR")
        self.assertEqual(Path(good["archive"]).read_bytes(), old_zip)
        self.assertEqual(Path(good["receipt"]).read_bytes(), old_receipt)
        self.assertFalse(self.part.exists())

    def test_symlink_partial_sidecar_final_receipt_and_lock_rejected(self):
        self.output.mkdir()
        target = self.directory / "private-file"
        target.write_text("untouched")
        for path in (self.part, self.sidecar, self.output / f"{JOB}.zip",
                     self.output / f"{JOB}.download.json", self.output / f".{JOB}.download.lock"):
            with self.subTest(path=path.name):
                # A lock file left by a prior subcase is safe to replace here.
                if path.exists():
                    path.unlink()
                try:
                    path.symlink_to(target)
                except OSError:
                    self.skipTest("This Windows account cannot create symlinks")
                result = self.command(expected=1)
                self.assertEqual(result["code"], "LOCAL_IO_ERROR")
                self.assertEqual(target.read_text(), "untouched")
                path.unlink()

    def test_64mib_resume_benchmark_records_transfer_saving(self):
        self.fixture.path, self.fixture.record = fixture_archive(self.directory, "large.zip", size=64 * MIB)
        self.fixture.cut = 24 * MIB
        start = time.perf_counter()
        baseline = self.command(output=self.directory / "baseline", timeout=30)
        baseline_seconds = time.perf_counter() - start
        self.partial()
        before = self.fixture.sent
        start = time.perf_counter()
        resumed = self.command(timeout=30)
        resumed_seconds = time.perf_counter() - start
        self.assert_final(resumed)
        total = self.fixture.path.stat().st_size
        self.assertEqual(resumed["received_bytes"], total - self.fixture.cut)
        self.assertEqual(self.fixture.sent - before, total - self.fixture.cut)
        report = {"fixture_model_bytes": 64 * MIB, "archive_bytes": total,
                  "baseline_received_bytes": baseline["received_bytes"],
                  "resume_received_bytes": resumed["received_bytes"], "reused_bytes": resumed["resumed_bytes"],
                  "retransmission_saved_percent": round(100 * self.fixture.cut / total, 3),
                  "baseline_seconds": round(baseline_seconds, 4), "resume_seconds": round(resumed_seconds, 4),
                  "stream_chunk_bytes": download.CHUNK_BYTES, "checkpoint_bytes": download.CHECKPOINT_BYTES}
        if BENCHMARK_REPORT:
            Path(BENCHMARK_REPORT).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print("DOWNLOAD_BENCHMARK " + json.dumps(report), file=sys.stderr)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--benchmark-json")
    options, remainder = parser.parse_known_args()
    BENCHMARK_REPORT = options.benchmark_json
    unittest.main(argv=[sys.argv[0], *remainder], verbosity=2)
