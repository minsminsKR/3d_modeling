"""Validate agent discovery, structured errors and resumable batch collection.

Uses an isolated store and tiny deterministic fixture assets, never GPU inference.
Run beside the original compatibility suite: python scripts/validate_cli_agent.py
"""
from __future__ import annotations

import argparse
import io
import json
import sys
import threading
import time
import unittest
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from validate_cli_api import BASE, MODEL, PNG, CliApiTests


class CliAgentTests(unittest.TestCase):
    # Reuse the real Flask/HTTP/subprocess fixture without running its legacy
    # compatibility cases a second time in this focused suite.
    setUp = CliApiTests.setUp
    post = CliApiTests.post
    create = CliApiTests.create
    finish = CliApiTests.finish
    stored = CliApiTests.stored
    rewrite_and_restart = CliApiTests.rewrite_and_restart
    start_http = CliApiTests.start_http
    cli = CliApiTests.cli

    def assert_error(self, payload, code=None, retryable=None):
        self.assertIsInstance(payload, dict)
        self.assertEqual(payload.get("schema_version"), 1, payload)
        self.assertIs(payload.get("ok"), False, payload)
        for key in ("error", "message", "code", "next_action"):
            self.assertIsInstance(payload.get(key), str, (key, payload))
            self.assertTrue(payload[key].strip(), (key, payload))
        self.assertIsInstance(payload.get("retryable"), bool, payload)
        if code is not None:
            self.assertEqual(payload["code"], code, payload)
        if retryable is not None:
            self.assertIs(payload["retryable"], retryable, payload)

    def assert_public(self, payload):
        text = json.dumps(payload)
        self.assertNotIn(str(self.directory), text)
        self.assertNotIn("fixture-private-secret", text)

        def visit(value):
            if isinstance(value, dict):
                for key, item in value.items():
                    self.assertNotIn(key.lower(), {"password", "cookie", "secret", "api_key", "authorization"})
                    self.assertFalse(key.endswith(("_path", "_paths")), key)
                    visit(item)
            elif isinstance(value, list):
                for item in value:
                    visit(item)
        visit(payload)

    def batch(self, count=3):
        request_id = str(uuid.uuid4())
        result = self.create(key=request_id, images=[(PNG, f"input-{index}.png") for index in range(count)])
        return request_id, [job["id"] for job in result["jobs"]]

    def fail_job(self, index):
        original = self.module.run_isolated_generation

        def controlled_failure(*args, **kwargs):
            raise RuntimeError("Controlled fixture failure")

        try:
            self.module.run_isolated_generation = controlled_failure
            return self.finish(index)
        finally:
            self.module.run_isolated_generation = original

    def background_finish(self, expected, first_failed=True, delay=1.1):
        errors = []
        stop = threading.Event()

        def run():
            try:
                deadline = time.monotonic() + 10
                while len(self.submitted) < expected:
                    if stop.wait(0.01) or time.monotonic() >= deadline:
                        raise RuntimeError("CLI never submitted expected fixture jobs")
                if first_failed:
                    self.fail_job(0)
                else:
                    self.finish(0)
                if not stop.wait(delay):
                    self.finish(1)
            except BaseException as error:
                errors.append(str(error))

        worker = threading.Thread(target=run, daemon=True)
        worker.start()

        def cleanup():
            stop.set()
            worker.join(5)

        self.addCleanup(cleanup)
        return worker, errors

    def assert_collected(self, payload, total, completed, failed, pending, downloaded):
        self.assertEqual(payload.get("schema_version"), 1)
        summary = payload.get("summary", {})
        for key, count in (("total", total), ("completed", completed), ("failed", failed),
                           ("pending", pending), ("downloaded", downloaded)):
            self.assertEqual(summary.get(key), count, (key, payload))
        self.assertEqual(len(payload["jobs"]), total)
        self.assertEqual(len(payload["failed_jobs"]), failed)
        self.assertEqual(len(payload["pending_jobs"]), pending)
        for download in payload["downloads"]:
            self.assertTrue(Path(download["archive"]).is_file())
            self.assertTrue(Path(download["receipt"]).is_file())
        self.assertFalse(list(self.directory.rglob("*.part")))

    def test_capabilities_report_live_defaults_and_parameter_bounds_without_private_paths(self):
        response = self.client.get("/api/capabilities")
        self.assertEqual(response.status_code, 200)
        payload = response.json
        self.assertEqual(payload["schema_version"], 1)
        self.assertEqual(payload["api_version"], "1.1")
        self.assertTrue(payload["ok"])
        self.assert_public(payload)
        self.assertEqual(payload["defaults"]["model"], "trellis2")
        self.assertEqual(payload["defaults"]["seed"], 1234)
        self.assertEqual(payload["defaults"]["trellis_steps"], 12)
        self.assertEqual(payload["defaults"]["trellis_texture_size"], 8192)
        self.assertEqual(payload["parameters"]["seed"]["minimum"], 0)
        self.assertEqual(payload["parameters"]["seed"]["maximum"], 2**32 - 1)
        self.assertEqual(payload["parameters"]["trellis_steps"]["maximum"], 50)
        self.assertTrue(payload["models"])
        self.assertTrue(payload["features"])
        self.assertEqual(self.submitted, [])

    def test_cli_capabilities_is_machine_readable_and_has_no_generation_side_effect(self):
        payload = self.cli(self.start_http(), "capabilities")
        self.assertEqual(payload["schema_version"], 1)
        self.assertEqual(payload["api_version"], "1.1")
        self.assertTrue(payload["ok"])
        self.assert_public(payload)
        self.assertEqual(self.submitted, [])

    def test_doctor_reports_runtime_and_private_storage_without_starting_workers(self):
        response = self.client.get("/api/doctor")
        self.assertEqual(response.status_code, 200)
        payload = response.json
        self.assertEqual(payload["schema_version"], 1)
        self.assertIsInstance(payload["ok"], bool)
        self.assertIsInstance(payload["status"], str)
        self.assertTrue(payload["checks"])
        self.assert_public(payload)
        self.assertEqual(self.submitted, [])
        cli_result = self.cli(self.start_http(), "doctor", expected=0 if payload["ok"] else 1)
        self.assertEqual(cli_result["status"], payload["status"])
        self.assertEqual(cli_result["ok"], payload["ok"])
        self.assertEqual(self.submitted, [])

    def test_doctor_and_capabilities_reflect_actual_cuda_model_and_queue_readiness(self):
        # Keep this readiness transition on the same selected model even when
        # the host also has another engine installed and available as fallback.
        self.module.default_generation_model = lambda: "trellis2"
        unavailable = self.client.get("/api/capabilities").json
        self.assertEqual(unavailable["gpu_ids"], [])
        self.assertFalse(unavailable["models"]["trellis2"]["available"])
        self.module.runtime_diagnostics = lambda: {"cuda_available": True, "cuda_device_count": 2}
        ready = self.client.get("/api/capabilities").json
        self.assertEqual(ready["gpu_ids"], [0, 1])
        self.assertTrue(ready["models"]["trellis2"]["available"])
        self.batch(1)
        doctor = self.client.get("/api/doctor").json
        self.assertTrue(doctor["ok"], doctor)
        self.assertEqual(doctor["status"], "ready")
        self.assertEqual(doctor["runtime"]["gpu_queue_load"], {"0": 1, "1": 1})
        self.assertEqual(doctor["runtime"]["jobs"], 1)
        self.assertFalse(doctor["inference_checked"])
        self.assertFalse(doctor["models_loaded_by_check"])
        self.module.trellis_ready = lambda: False
        degraded = self.client.get("/api/doctor").json
        self.assert_error(degraded, code="SERVER_NOT_READY", retryable=True)
        self.assertFalse(degraded["checks"]["default_model"]["ok"])
        self.assertEqual(degraded["runtime"]["gpu_queue_load"], doctor["runtime"]["gpu_queue_load"])
        self.assertEqual(len(self.submitted), 1)

    def test_single_gpu_invalid_split_defaults_degrade_doctor_even_with_ready_model_files(self):
        self.module.runtime_diagnostics = lambda: {"cuda_available": True, "cuda_device_count": 1}
        self.module.available_gpu_ids = lambda: [0]
        payload = self.client.get("/api/capabilities").json
        self.assertEqual(payload["gpu_ids"], [0])
        model = payload["models"]["trellis2"]
        self.assertTrue(model["configured"])
        configuration = model["default_configuration"]
        self.assertFalse(configuration["valid"])
        self.assertIn("required_physical_gpu_unavailable", configuration["unavailable_reasons"])
        self.assertIn(1, configuration["required_gpu_ids"])
        doctor = self.client.get("/api/doctor").json
        self.assert_error(doctor, code="SERVER_NOT_READY", retryable=True)
        self.assertFalse(doctor["checks"]["default_model"]["ok"])
        self.assertEqual(self.submitted, [])

    def test_api_http_errors_have_stable_machine_contract(self):
        from flask import abort, request

        @self.app.get("/api/fixture-error/<int:status>")
        def fixture_error(status):
            abort(status, "Fixture client-visible failure")

        @self.app.after_request
        def retry_hint(response):
            if request.path == "/api/fixture-error/429":
                response.headers["Retry-After"] = "7"
            return response

        codes = {}
        for status in (400, 401, 403, 404, 409, 429, 502, 503):
            with self.subTest(status=status):
                response = self.client.get(f"/api/fixture-error/{status}")
                self.assertEqual(response.status_code, status)
                self.assert_error(response.json, retryable=status in {429, 502, 503})
                self.assert_public(response.json)
                codes[status] = response.json["code"]
        self.assertNotEqual(codes[400], codes[401])
        self.assertNotEqual(codes[401], codes[403])

    def test_invalid_submission_and_not_ready_archive_are_structured(self):
        invalid = self.post(key="bad")
        self.assertEqual(invalid.status_code, 400)
        self.assert_error(invalid.json, retryable=False)
        request_id, ids = self.batch(1)
        unfinished = self.client.get(f"/api/jobs/{ids[0]}/archive")
        self.assertEqual(unfinished.status_code, 409)
        self.assert_error(unfinished.json)
        self.assertEqual(len(self.submitted), 1)

    def test_api_rejects_invalid_numbers_and_bounds_without_scheduling_gpu_work(self):
        for fields in ({"seed": "-1"}, {"seed": str(2**32)}, {"trellis_steps": "51"},
                       {"trellis_texture_size": "5000"}, {"guidance_scale": "NaN"},
                       {"num_chunks": "not-a-number"}, {"gpu_id": "99"}):
            with self.subTest(fields=fields):
                response = self.post(fields=fields)
                self.assertEqual(response.status_code, 400)
                self.assert_error(response.json, code="INVALID_INPUT", retryable=False)
                self.assert_public(response.json)
        self.assertEqual(self.submitted, [])

    def test_unexpected_api_failure_is_structured_and_strips_internal_exception_details(self):
        @self.app.get("/api/fixture-unexpected-failure")
        def unexpected_failure():
            raise RuntimeError(f"fixture-private-secret {self.directory / 'private-state.json'}")

        self.app.testing = False
        with self.assertLogs(self.app.logger, level="ERROR"):
            response = self.client.get("/api/fixture-unexpected-failure")
        self.assertEqual(response.status_code, 500)
        self.assert_error(response.json, code="INTERNAL_ERROR", retryable=False)
        self.assert_public(response.json)

    def test_cli_parser_errors_remain_one_json_value_on_stdout(self):
        server = self.start_http()
        for arguments in (("unknown-command",), ("wait", "job", "--timeout", "NaN"),
                          ("generate", "--model", "not-a-model"), ("status",)):
            with self.subTest(arguments=arguments):
                payload = self.cli(server, *arguments, expected=2)
                self.assert_error(payload, code="INVALID_INPUT", retryable=False)
        self.assertEqual(self.submitted, [])

    def test_cli_preserves_server_retry_and_recovery_fields(self):
        from flask import jsonify

        def unavailable(job_id):
            response = jsonify(schema_version=1, ok=False, error="Fixture maintenance", message="Fixture maintenance",
                               code="SERVER_UNAVAILABLE", retryable=True,
                               next_action="Wait seven seconds and retry the same job ID.", retry_after=7)
            response.status_code = 503
            response.headers["Retry-After"] = "7"
            return response

        self.app.view_functions["job_status"] = unavailable
        payload = self.cli(self.start_http(), "status", "fixture-job", expected=1)
        self.assert_error(payload, code="SERVER_UNAVAILABLE", retryable=True)
        self.assertIn("same job", payload["next_action"])
        self.assertEqual(self.submitted, [])

    def test_cli_exposes_retry_after_header_for_legacy_rate_limit_response(self):
        from flask import jsonify

        def rate_limited(job_id):
            response = jsonify(error="Fixture rate limit")
            response.status_code = 429
            response.headers["Retry-After"] = "7"
            return response

        self.app.view_functions["job_status"] = rate_limited
        payload = self.cli(self.start_http(), "status", "fixture-job", expected=1)
        self.assert_error(payload, code="RATE_LIMITED", retryable=True)
        self.assertEqual(payload["details"]["retry_after_seconds"], 7)
        self.assertEqual(self.submitted, [])

    def test_missing_capabilities_on_old_server_returns_structured_compatibility_error(self):
        from flask import abort

        def old_server():
            abort(404)

        self.app.view_functions["api_capabilities"] = old_server
        payload = self.cli(self.start_http(), "capabilities", expected=1)
        self.assert_error(payload, retryable=False)
        self.assertTrue(any(word in (payload["code"] + payload["next_action"]).lower()
                            for word in ("api", "server", "upgrade")), payload)

    def test_collect_retains_completed_download_while_reporting_failed_and_pending(self):
        request_id, ids = self.batch()
        self.finish(0)
        self.fail_job(1)
        payload = self.cli(self.start_http(), "collect", request_id, "--output", str(self.directory / "partial"), expected=2)
        self.assert_collected(payload, 3, 1, 1, 1, 1)
        self.assertEqual(payload["request_id"], request_id)
        self.assertEqual(payload["downloads"][0]["job"]["id"], ids[0])
        self.assertEqual(len(self.submitted), 3)

    def test_collect_pending_only_returns_immediately_with_explicit_pending_summary(self):
        request_id, _ = self.batch(2)
        before = time.monotonic()
        payload = self.cli(self.start_http(), "collect", request_id, "--output", str(self.directory / "pending"))
        self.assert_collected(payload, 2, 0, 0, 2, 0)
        self.assertLess(time.monotonic() - before, 15)

    def test_resume_waits_for_other_jobs_after_first_failure_and_downloads_success(self):
        request_id, ids = self.batch(2)
        worker, errors = self.background_finish(2)
        payload = self.cli(self.start_http(), "resume", request_id, "--output", str(self.directory / "resume"),
                           "--timeout", "8", "--poll-interval", "1", expected=2)
        worker.join(3)
        self.assertEqual(errors, [])
        self.assert_collected(payload, 2, 1, 1, 0, 1)
        self.assertEqual(payload["downloads"][0]["job"]["id"], ids[1])
        self.assertEqual(len(self.submitted), 2)

    def test_generate_download_waits_for_all_jobs_even_when_first_job_fails(self):
        images = []
        for index in range(2):
            path = self.directory / f"batch-{index}.png"
            path.write_bytes(PNG)
            images.append(str(path))
        request_id = str(uuid.uuid4())
        worker, errors = self.background_finish(2)
        payload = self.cli(self.start_http(), "generate", *images, "--request-id", request_id,
                           "--download", str(self.directory / "generated"), "--timeout", "8", "--poll-interval", "1", expected=2)
        worker.join(3)
        self.assertEqual(errors, [])
        self.assert_collected(payload, 2, 1, 1, 0, 1)
        self.assertEqual(payload["request_id"], request_id)
        self.assertEqual(len(self.submitted), 2)

    def test_resume_timeout_keeps_completed_assets_and_pending_ids(self):
        request_id, ids = self.batch(2)
        self.finish(0)
        payload = self.cli(self.start_http(), "resume", request_id, "--output", str(self.directory / "timeout"),
                           "--timeout", "0.5", "--poll-interval", "1", expected=3)
        self.assert_collected(payload, 2, 1, 0, 1, 1)
        self.assertEqual(payload["pending_jobs"][0]["id"], ids[1])
        self.assert_error(payload, retryable=True)
        self.assertEqual(len(self.submitted), 2)

    def test_one_bad_archive_does_not_block_other_completed_downloads(self):
        from flask import Response
        request_id, ids = self.batch(2)
        self.finish(0)
        self.finish(1)
        original = self.app.view_functions["job_archive"]

        def one_bad_archive(job_id):
            return Response("<html>fixture failure</html>", mimetype="text/html") if job_id == ids[0] else original(job_id)

        self.app.view_functions["job_archive"] = one_bad_archive
        payload = self.cli(self.start_http(), "collect", request_id, "--output", str(self.directory / "bad-one"), expected=2)
        self.assert_collected(payload, 2, 2, 0, 0, 1)
        self.assertEqual(len(payload["download_errors"]), 1)
        self.assertEqual(payload["download_errors"][0]["job_id"], ids[0])
        self.assert_error(payload["download_errors"][0])
        self.assertEqual(payload["downloads"][0]["job"]["id"], ids[1])

    def test_collect_skips_verified_existing_archive_and_repairs_modified_archive(self):
        request_id, ids = self.batch(1)
        self.finish(0)
        requests = []
        original = self.app.view_functions["job_archive"]

        def counted_archive(job_id):
            requests.append(job_id)
            return original(job_id)

        self.app.view_functions["job_archive"] = counted_archive
        server = self.start_http()
        output = str(self.directory / "repeat")
        first = self.cli(server, "collect", request_id, "--output", output)
        self.assert_collected(first, 1, 1, 0, 0, 1)
        second = self.cli(server, "collect", request_id, "--output", output)
        self.assertTrue(second["downloads"][0]["skipped"])
        self.assertEqual(requests, ids)
        Path(second["downloads"][0]["archive"]).write_bytes(b"modified-after-download")
        repaired = self.cli(server, "collect", request_id, "--output", output)
        self.assertFalse(repaired["downloads"][0]["skipped"])
        self.assertEqual(requests, ids + ids)
        self.assertEqual(len(self.submitted), 1)

    def test_corrupted_receipt_shapes_are_cache_misses_and_redownload_same_job(self):
        request_id, ids = self.batch(1)
        self.finish(0)
        requests = []
        original = self.app.view_functions["job_archive"]

        def counted_archive(job_id):
            requests.append(job_id)
            return original(job_id)

        self.app.view_functions["job_archive"] = counted_archive
        server = self.start_http()
        output = str(self.directory / "receipt-repair")
        first = self.cli(server, "collect", request_id, "--output", output)
        receipt_path = Path(first["downloads"][0]["receipt"])
        valid = json.loads(receipt_path.read_text(encoding="utf-8"))
        for name, damaged in (("array receipt", []), ("array generation record", {**valid, "generation_record": []}),
                              ("null archive", {**valid, "archive": None})):
            with self.subTest(name=name):
                receipt_path.write_text(json.dumps(damaged), encoding="utf-8")
                recovered = self.cli(server, "collect", request_id, "--output", output)
                self.assert_collected(recovered, 1, 1, 0, 0, 1)
                self.assertFalse(recovered["downloads"][0]["skipped"])
                self.assertIsInstance(json.loads(receipt_path.read_text(encoding="utf-8")), dict)
        self.assertEqual(requests, ids * 4)
        self.assertEqual(len(self.submitted), 1)

    def test_request_recovery_does_not_require_local_manifest(self):
        request_id, ids = self.batch(1)
        self.finish(0)
        self.assertFalse((self.directory / "cli-state").exists())
        payload = self.cli(self.start_http(), "resume", request_id, "--output", str(self.directory / "recovered"),
                           "--timeout", "3", "--poll-interval", "1")
        self.assert_collected(payload, 1, 1, 0, 0, 1)
        self.assertEqual(payload["jobs"][0]["id"], ids[0])
        self.assertEqual(len(self.submitted), 1)

    def test_remote_request_lookup_fetches_all_pages(self):
        from flask import jsonify, request
        request_id = str(uuid.uuid4())
        jobs = [{"id": f"page-job-{index}", "status": "failed", "request_id": request_id,
                 "source": "cli", "message": "Controlled fixture failure"} for index in range(201)]
        offsets = []

        def paginated_history():
            self.assertEqual(request.args.get("request_id"), request_id)
            offset, limit = int(request.args.get("offset", 0)), int(request.args.get("limit", 50))
            offsets.append(offset)
            return jsonify(jobs=jobs[offset:offset + limit], total=len(jobs), offset=offset, limit=limit)

        def paginated_status(job_id):
            return jsonify(next(job for job in jobs if job["id"] == job_id))

        self.app.view_functions["job_history"] = paginated_history
        self.app.view_functions["job_status"] = paginated_status
        payload = self.cli(self.start_http(), "collect", request_id, "--output", str(self.directory / "pages"), expected=2)
        self.assert_collected(payload, 201, 0, 201, 0, 0)
        self.assertIn(0, offsets)
        self.assertIn(200, offsets)
        self.assertEqual(self.submitted, [])

    def test_unknown_request_resume_does_not_create_any_generation(self):
        request_id = str(uuid.uuid4())
        payload = self.cli(self.start_http(), "resume", request_id, "--output", str(self.directory / "unknown"), expected=1)
        self.assert_error(payload, retryable=False)
        self.assertEqual(payload.get("request_id"), request_id)
        self.assertEqual(self.submitted, [])

    def test_resume_uncertain_unaccepted_submission_reuses_original_uuid_and_input(self):
        from flask import abort, request
        original = self.app.view_functions["create_job"]
        blocked = [True]
        posted_ids = []

        def uncertain_submission():
            posted_ids.append(request.headers.get("Idempotency-Key"))
            if blocked[0]:
                abort(503, "Fixture outage before accepting submission")
            result = original()
            self.finish(0)
            return result

        self.app.view_functions["create_job"] = uncertain_submission
        server = self.start_http()
        source = self.directory / "preserved.png"
        source.write_bytes(PNG)
        request_id = str(uuid.uuid4())
        rejected = self.cli(server, "generate", str(source), "--request-id", request_id, expected=1)
        self.assert_error(rejected, retryable=True)
        self.assertEqual(rejected["request_id"], request_id)
        self.assertEqual(self.submitted, [])
        source.unlink()
        blocked[0] = False
        recovered = self.cli(server, "resume", request_id, "--output", str(self.directory / "preserved"),
                             "--timeout", "3", "--poll-interval", "1")
        self.assert_collected(recovered, 1, 1, 0, 0, 1)
        self.assertEqual(posted_ids, [request_id, request_id])
        self.assertEqual(len(self.submitted), 1)

    def test_resume_previously_submitted_but_absent_request_never_regenerates(self):
        from flask import jsonify
        source = self.directory / "accepted.png"
        source.write_bytes(PNG)
        server = self.start_http()
        request_id = str(uuid.uuid4())
        self.cli(server, "generate", str(source), "--request-id", request_id)
        self.assertEqual(len(self.submitted), 1)

        def missing_remote_history():
            return jsonify(jobs=[], total=0, limit=200, offset=0)

        self.app.view_functions["job_history"] = missing_remote_history
        rejected = self.cli(server, "resume", request_id, "--output", str(self.directory / "never-regenerate"), expected=1)
        self.assert_error(rejected, retryable=False)
        self.assertEqual(rejected["request_id"], request_id)
        self.assertEqual(len(self.submitted), 1)

    def test_one_status_error_does_not_block_other_completed_download(self):
        from flask import abort, jsonify
        request_id, ids = self.batch(2)
        self.finish(0)
        self.finish(1)
        original = self.app.view_functions["job_status"]
        original_history = self.app.view_functions["job_history"]

        def initially_pending_history():
            payload = original_history().get_json()
            for job in payload["jobs"]:
                if job["id"] == ids[0]:
                    job["status"] = "running"
            return jsonify(payload)

        def one_status_failure(job_id):
            if job_id == ids[0]:
                abort(503, "Controlled status outage")
            return original(job_id)

        self.app.view_functions["job_status"] = one_status_failure
        self.app.view_functions["job_history"] = initially_pending_history
        payload = self.cli(self.start_http(), "collect", request_id, "--output", str(self.directory / "status-error"), expected=2)
        self.assertEqual(len(payload["downloads"]), 1)
        self.assertEqual(payload["downloads"][0]["job"]["id"], ids[1])
        self.assertEqual(len(payload["status_errors"]), 1)
        self.assert_error(payload["status_errors"][0], retryable=True)
        self.assertEqual(len(self.submitted), 2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()
    output = io.StringIO()
    result = unittest.TextTestRunner(stream=output, verbosity=2 if args.verbose else 1).run(
        unittest.defaultTestLoader.loadTestsFromTestCase(CliAgentTests))
    if args.verbose or not result.wasSuccessful():
        print(output.getvalue(), end="")
    if result.wasSuccessful():
        print(f"CLI agent validation: {result.testsRun} tests passed, {len(result.skipped)} skipped (no GPU inference).")
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())
