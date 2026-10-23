"""Exercise the shared CLI/web API in an isolated store without GPU inference.

Run with the web application's Python environment:
    python scripts/validate_cli_api.py

The module-level app factory call is disabled before loading app.py so this
validation never opens or rewrites the production job store.
"""
from __future__ import annotations

import argparse
import base64
import concurrent.futures
import hashlib
import importlib.util
import io
import json
import os
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import uuid
import zipfile
from pathlib import Path
from unittest.mock import patch


BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))
PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6"
    "nXkAAAAASUVORK5CYII="
)
MODEL = b"glTF\x02\x00\x00\x00\x0c\x00\x00\x00"


def load_isolated_module():
    source = BASE / "app.py"
    name = "cli_api_validation_" + uuid.uuid4().hex
    spec = importlib.util.spec_from_file_location(name, source)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    source_text = source.read_text(encoding="utf-8")
    if "app = create_app()" not in source_text:
        raise RuntimeError("Refusing to import app.py without disabling its production app factory")
    exec(compile(source_text.replace("app = create_app()", "app = None"), str(source), "exec"), module.__dict__)
    return module


class CliApiTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix=".cli-api-test-", dir=BASE)
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        environment = {
            "APP_DEFAULT_MODEL": "trellis2",
            "TRELLIS_DEFAULT_GPU_MODE": "split",
            "TRELLIS_DEFAULT_PIPELINE": "1536_cascade",
            "TRELLIS_DEFAULT_TEXTURE_SIZE": "8192",
            "TRELLIS_DEFAULT_STEPS": "12",
            "TRELLIS_SPLIT_MAX_NUM_TOKENS": "65536",
            "APP_DEFAULT_REMOVE_BACKGROUND": "true",
            "APP_DEFAULT_SEED": "1234",
            "HUNYUAN_SHAPE_GPU": "0",
            "HUNYUAN_TEXTURE_GPU": "1",
            "HUNYUAN_UNSAVED_JOB_RETENTION_DAYS": "0",
            "APP_INSTANCE_DIR": str(self.directory / "instance"),
        }
        self.environment = patch.dict(os.environ, environment)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.module = load_isolated_module()
        # A production .env is loaded with override=True by app.py. Restore only
        # this fixture's settings after import, then bind its private store.
        os.environ.update(environment)
        self.addCleanup(sys.modules.pop, self.module.__name__, None)
        self.module.INSTANCE_DIR = self.directory / "instance"
        self.module.UPLOAD_DIR = self.module.INSTANCE_DIR / "uploads"
        self.module.OUTPUT_DIR = self.module.INSTANCE_DIR / "outputs"
        self.module.JOBS_FILE = self.module.INSTANCE_DIR / "jobs.json"
        self.submitted = []
        submitted = self.submitted

        class CaptureExecutor:
            def __init__(self, **kwargs):
                pass

            def submit(self, *args):
                submitted.append(args)

        self.module.ThreadPoolExecutor = CaptureExecutor
        self.module.available_gpu_ids = lambda: [0, 1]
        self.module.trellis_ready = lambda: True
        self.module.trellis_dual_ready = lambda: True
        self.module.omni_dependency_issues = lambda: []
        self.module.texture_dependency_issues = lambda: []
        self.module.texture_dependencies_ready = lambda: True
        self.module.omni_ema_ready = lambda: True
        self.module.rigging_ready = lambda: False
        self.module.gpu_stats = lambda: []
        self.module.runtime_diagnostics = lambda: {}

        def fixture_generation(image_paths, output, options, progress):
            from hunyuan_service import GeneratedAsset
            output.mkdir(parents=True, exist_ok=True)
            progress("Fixture generation")
            model = output / "model.glb"
            model.write_bytes(MODEL)
            metadata = output / "metadata.json"
            metadata.write_text(json.dumps({"options": options.__dict__, "fixture": True}), encoding="utf-8")
            processed = output / "processed_inputs"
            processed.mkdir(exist_ok=True)
            (processed / "01.png").write_bytes(PNG)
            return GeneratedAsset(model, metadata, True, ())

        self.module.run_isolated_generation = fixture_generation
        self.app = self.module.create_app()
        self.app.testing = True
        self.client = self.app.test_client()

    def post(self, key="request-00000001", images=None, fields=None, cli=True, client=None):
        images = images if images is not None else [(PNG, "캐릭터.png")]
        data = {"images": [(io.BytesIO(content), name) for content, name in images]}
        data.update(fields or {})
        headers = {"Idempotency-Key": key}
        if cli:
            headers["X-Client-Type"] = "cli"
        return (client or self.client).post("/api/jobs", data=data, headers=headers, content_type="multipart/form-data")

    def create(self, **kwargs):
        response = self.post(**kwargs)
        self.assertEqual(response.status_code, 201, response.get_data(as_text=True))
        self.assertTrue(response.is_json)
        result = response.get_json()
        self.assertTrue(result["request_id"])
        self.assertFalse(result["replayed"])
        self.assertTrue(result["jobs"])
        return result

    def finish(self, index=-1):
        function, job_id, image_paths, options = self.submitted[index]
        function(job_id, image_paths, options)
        return job_id

    def stored(self):
        return json.loads(self.module.JOBS_FILE.read_text(encoding="utf-8"))

    def rewrite_and_restart(self, records):
        self.module.JOBS_FILE.write_text(json.dumps(records), encoding="utf-8")
        self.app = self.module.create_app()
        self.app.testing = True
        self.client = self.app.test_client()

    def assert_safe(self, record):
        sensitive = {"password", "cookie", "secret", "authorization", "api_key", "token", "idempotency_key", "request_fingerprint"}
        self.assertFalse(sensitive.intersection(record))
        self.assertFalse([key for key in record if key.endswith(("_path", "_paths"))])
        self.assertNotIn(str(self.directory), json.dumps(record))

    def start_http(self):
        from werkzeug.serving import WSGIRequestHandler, make_server

        class QuietHandler(WSGIRequestHandler):
            def log(self, *args, **kwargs):
                pass

        server = make_server("127.0.0.1", 0, self.app, threaded=True, request_handler=QuietHandler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()

        def stop():
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)

        self.addCleanup(stop)
        return f"http://127.0.0.1:{server.server_port}"

    def cli(self, server, *arguments, expected=0):
        source = BASE / "modeling_cli.py"
        if not source.is_file():
            self.skipTest("modeling_cli.py is not available")
        environment = dict(os.environ, PYTHONIOENCODING="utf-8", PYTHONUTF8="1")
        result = subprocess.run(
            [sys.executable, str(source), "--server", server, "--state-dir", str(self.directory / "cli-state"), "--json", *arguments],
            cwd=BASE, env=environment, text=True, encoding="utf-8", capture_output=True, timeout=40,
        )
        self.assertEqual(result.returncode, expected, f"stdout={result.stdout}\nstderr={result.stderr}")
        try:
            payload = json.loads(result.stdout)
        except ValueError as error:
            self.fail(f"CLI stdout must be one JSON value: {result.stdout!r}; stderr={result.stderr!r}; {error}")
        self.assertIsInstance(payload, dict)
        return payload

    def test_web_form_redirect_and_existing_status_contract(self):
        response = self.client.post("/jobs", data={"images": (io.BytesIO(PNG), "web.png")}, content_type="multipart/form-data")
        self.assertEqual(response.status_code, 302)
        self.assertTrue(response.headers["Location"].startswith("/jobs/"))
        job_id = response.headers["Location"].rsplit("/", 1)[-1]
        self.assertEqual(self.client.get("/api/jobs/" + job_id).json["status"], "queued")
        self.finish()
        self.assertEqual(self.client.get(response.headers["Location"]).status_code, 200)

    def test_cli_create_persists_and_uses_web_views_and_saved_list(self):
        result = self.create()
        job_id = result["jobs"][0]["id"]
        record = self.stored()[job_id]
        self.assertEqual(record["source"], "cli")
        self.assertTrue(record["saved"])
        self.assertGreater(record["saved_at"], 0)
        self.assertEqual(self.client.get(result["web_url"]).status_code, 200)
        self.finish()
        self.assertIn(job_id.encode(), self.client.get("/").data)
        self.assertIn(job_id.encode(), self.client.get("/saved").data)
        self.assertEqual(self.client.get(f"/jobs/{job_id}/download").data, MODEL)
        self.assertEqual(self.client.get(f"/api/jobs/{job_id}").json["status"], "completed")

    def test_shared_server_defaults_and_gpu_queue_counters(self):
        result = self.create()
        options = self.submitted[-1][3]
        self.assertEqual(options.model, "trellis2")
        self.assertEqual(options.gpu_mode, "split")
        self.assertEqual((options.gpu_id, options.texture_gpu_id), (1, 0))
        self.assertEqual((options.trellis_pipeline, options.trellis_texture_size, options.trellis_steps), ("1536_cascade", 8192, 12))
        self.assertEqual(options.trellis_max_tokens, 65536)
        self.assertTrue(options.remove_background)
        self.assertEqual(self.client.get("/health").json["gpu_queue_load"], {"0": 1, "1": 1})
        self.finish()
        self.assertEqual(self.client.get("/health").json["gpu_queue_load"], {"0": 0, "1": 0})
        self.assertEqual(result["jobs"][0]["options"]["seed"], 1234)

    def test_configured_model_default_is_used_without_cli_override(self):
        with patch.dict(os.environ, {"APP_DEFAULT_MODEL": "hunyuan3d-2.1", "HUNYUAN_DEFAULT_GPU_MODE": "split"}):
            result = self.create()
        self.assertEqual(result["jobs"][0]["options"]["model"], "hunyuan3d-2.1")

    def test_same_key_replays_without_queue_or_gpu_increment(self):
        first = self.create()
        second = self.post()
        self.assertEqual(second.status_code, 200)
        self.assertTrue(second.json["replayed"])
        self.assertEqual([job["id"] for job in first["jobs"]], [job["id"] for job in second.json["jobs"]])
        self.assertEqual(len(self.submitted), 1)
        self.assertEqual(self.client.get("/health").json["gpu_queue_load"], {"0": 1, "1": 1})

    def test_replay_keeps_original_effective_options_when_server_defaults_change(self):
        original = self.create()
        with patch.dict(os.environ, {"APP_DEFAULT_MODEL": "hunyuan3d-2.1", "APP_DEFAULT_SEED": "999"}):
            replay = self.post()
        self.assertEqual(replay.status_code, 200)
        self.assertEqual(replay.json["jobs"][0]["options"], original["jobs"][0]["options"])
        self.assertEqual(len(self.submitted), 1)

    def test_concurrent_same_key_submits_one_worker(self):
        barrier = threading.Barrier(5)

        def submit(_):
            with self.app.test_client() as client:
                barrier.wait(timeout=10)
                response = self.post(key="concurrent-request-01", client=client)
                return response.status_code, response.get_json()

        with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
            responses = list(pool.map(submit, range(5)))
        self.assertEqual(sorted(code for code, _ in responses), [200, 200, 200, 200, 201])
        self.assertEqual(len({payload["jobs"][0]["id"] for _, payload in responses}), 1)
        self.assertEqual(len(self.submitted), 1)

    def test_conflicting_image_or_options_do_not_create_jobs(self):
        self.create()
        for kwargs in ({"images": [(PNG + b"different", "캐릭터.png")]}, {"fields": {"seed": "42"}}):
            response = self.post(**kwargs)
            self.assertEqual(response.status_code, 409)
            self.assertTrue(response.is_json)
        self.assertEqual(len(self.submitted), 1)
        self.assertEqual(len(self.stored()), 1)

    def test_invalid_idempotency_keys_and_uploads_return_json(self):
        for key in ("short", "x" * 121, "bad key with spaces", "invalid/route", "유효하지않음"):
            with self.subTest(key=key):
                response = self.post(key=key)
                self.assertEqual(response.status_code, 400)
                self.assertTrue(response.is_json)
        response = self.post(images=[(PNG, "invalid.exe")])
        self.assertEqual(response.status_code, 400)
        self.assertTrue(response.is_json)
        self.assertEqual(len(self.submitted), 0)

    def test_batch_jobs_share_request_and_are_visible_on_web(self):
        result = self.create(images=[(PNG, "first.png"), (PNG + b"second", "second.png")])
        self.assertTrue(result["batch_id"])
        self.assertEqual(len(result["jobs"]), 2)
        self.assertEqual(len(self.submitted), 2)
        self.assertEqual(self.client.get(result["web_url"]).status_code, 200)
        response = self.client.get("/api/batches/" + result["batch_id"])
        self.assertEqual(response.json["total"], 2)
        self.assertEqual({job["request_id"] for job in result["jobs"]}, {result["request_id"]})
        self.finish(0)
        self.finish(1)
        self.assertEqual(self.client.get("/api/batches/" + result["batch_id"]).json["completed"], 2)

    def test_inputs_have_original_names_hashes_and_sizes(self):
        result = self.create()
        expected = [{"name": "캐릭터.png", "sha256": hashlib.sha256(PNG).hexdigest(), "size": len(PNG)}]
        self.assertEqual(result["jobs"][0]["inputs"], expected)
        job_id = result["jobs"][0]["id"]
        response = self.client.get(f"/api/jobs/{job_id}/record")
        self.assertEqual(response.status_code, 200)
        self.assertEqual([{key: item[key] for key in ("name", "sha256", "size")} for item in response.json["inputs"]], expected)
        self.assertTrue(response.json["inputs"][0]["archive_name"].startswith("inputs/"))
        self.assert_safe(response.json)

    def test_list_filters_pagination_and_invalid_parameters(self):
        cli = self.create(key="cli-request-0001")
        api = self.create(key="api-request-0001", cli=False)
        self.client.post("/jobs", data={"images": (io.BytesIO(PNG), "web.png")}, content_type="multipart/form-data")
        all_jobs = self.client.get("/api/jobs?limit=1&offset=1").json
        self.assertEqual(all_jobs["total"], 3)
        self.assertEqual(len(all_jobs["jobs"]), 1)
        for source in ("cli", "api", "web"):
            response = self.client.get("/api/jobs?source=" + source)
            self.assertEqual(response.json["total"], 1)
            self.assert_safe(response.json["jobs"][0])
        request_jobs = self.client.get("/api/jobs?request_id=" + cli["request_id"]).json
        self.assertEqual([job["id"] for job in request_jobs["jobs"]], [cli["jobs"][0]["id"]])
        self.assertEqual(api["jobs"][0]["source"], "api")
        for query in ("limit=0", "limit=201", "limit=bad", "offset=-1", "offset=bad", "source=other"):
            response = self.client.get("/api/jobs?" + query)
            self.assertEqual(response.status_code, 400, query)
            self.assertTrue(response.is_json)

    def test_restart_preserves_request_lookup_without_resubmitting(self):
        result = self.create()
        job_id = result["jobs"][0]["id"]
        self.app = self.module.create_app()
        self.app.testing = True
        self.client = self.app.test_client()
        response = self.client.get("/api/jobs?request_id=" + result["request_id"])
        self.assertEqual(response.json["jobs"][0]["id"], job_id)
        self.assertEqual(response.json["jobs"][0]["status"], "failed")
        replay = self.post()
        self.assertEqual(replay.status_code, 200)
        self.assertEqual(replay.json["jobs"][0]["id"], job_id)
        self.assertEqual(len(self.submitted), 1)

    def test_cli_saved_records_survive_retention_cleanup(self):
        result = self.create()
        job_id = self.finish()
        records = self.stored()
        old = time.time() - 10 * 86400
        records[job_id].update(created_at=old, updated_at=old, finished_at=old)
        unsaved = "unsaved-old-job"
        records[unsaved] = {"id": unsaved, "status": "failed", "created_at": old, "updated_at": old, "finished_at": old}
        for root in (self.module.UPLOAD_DIR, self.module.OUTPUT_DIR):
            (root / unsaved).mkdir()
            (root / unsaved / "fixture.txt").write_text("remove me", encoding="utf-8")
        with patch.dict(os.environ, {"HUNYUAN_UNSAVED_JOB_RETENTION_DAYS": "1"}):
            self.module.cleanup_unsaved_jobs(records)
        self.assertIn(job_id, records)
        self.assertNotIn(unsaved, records)
        self.assertTrue(Path(records[job_id]["model_path"]).exists())
        self.assertFalse((self.module.OUTPUT_DIR / unsaved).exists())
        self.assertTrue(result["jobs"][0]["saved"])

    def test_safe_record_omits_internal_paths_and_sensitive_fields(self):
        result = self.create()
        job_id = self.finish()
        records = self.stored()
        records[job_id].update(password="do-not-disclose", cookie="do-not-disclose", api_key="do-not-disclose", token="do-not-disclose", secret="do-not-disclose", authorization="do-not-disclose")
        records[job_id]["rigging"] = {"status": "failed", "error_path": str(self.directory / "private-error.log"), "model_path": str(self.directory / "private.glb")}
        self.rewrite_and_restart(records)
        for endpoint in (f"/api/jobs/{job_id}", f"/api/jobs/{job_id}/record"):
            response = self.client.get(endpoint)
            self.assertEqual(response.status_code, 200)
            self.assert_safe(response.json)
            self.assertNotIn("do-not-disclose", response.get_data(as_text=True))
            self.assertFalse([key for key in response.json.get("rigging", {}) if key.endswith(("_path", "_paths"))])

    def test_completed_archive_includes_originals_and_safe_record(self):
        result = self.create()
        job_id = result["jobs"][0]["id"]
        unfinished = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertGreaterEqual(unfinished.status_code, 400)
        self.assertTrue(unfinished.is_json)
        self.finish()
        (self.module.OUTPUT_DIR / job_id / "not-an-export.secret").write_text("not included", encoding="utf-8")
        response = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertEqual(response.status_code, 200)
        self.assertIn("attachment", response.headers["Content-Disposition"])
        with zipfile.ZipFile(io.BytesIO(response.data)) as archive:
            names = archive.namelist()
            self.assertEqual(archive.read("model.glb"), MODEL)
            self.assertTrue(json.loads(archive.read("metadata.json"))["fixture"])
            record = json.loads(archive.read("generation-record.json"))
            self.assertEqual(record["id"], job_id)
            self.assert_safe(record)
            inputs = [name for name in names if name.startswith("inputs/") and not name.endswith("/")]
            self.assertEqual(len(inputs), 1)
            self.assertEqual(hashlib.sha256(archive.read(inputs[0])).hexdigest(), record["inputs"][0]["sha256"])
            self.assertEqual(archive.read("processed_inputs/01.png"), PNG)
            self.assertNotIn("not-an-export.secret", names)
            self.assertTrue(all(not name.startswith(("/", "\\")) and ".." not in Path(name).parts for name in names))

    def test_archive_preserves_original_mesh_and_pbr_authoring_outputs(self):
        self.create()
        job_id = self.finish()
        root = self.module.OUTPUT_DIR / job_id
        originals = {
            "model_initial.glb": b"original-generated-mesh",
            "textured/model.obj": b"mtllib model.mtl\nv 0 0 0\n",
            "textured/model.mtl": b"newmtl pbr\nmap_Kd albedo.jpg\n",
            "textured/albedo.jpg": b"original-pbr-image",
            "generation-settings.json": b'{"fixture_settings": true}',
        }
        for relative, content in originals.items():
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        response = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertEqual(response.status_code, 200)
        with zipfile.ZipFile(io.BytesIO(response.data)) as archive:
            record = json.loads(archive.read("generation-record.json"))
            artifacts = {artifact["name"]: artifact for artifact in record["artifacts"]}
            for relative, content in originals.items():
                name = "outputs/" + relative
                self.assertEqual(archive.read(name), content)
                self.assertEqual(artifacts[name]["sha256"], hashlib.sha256(content).hexdigest())
                self.assertEqual(artifacts[name]["size"], len(content))

    def test_archive_rejects_symlinked_authoring_output(self):
        self.create()
        job_id = self.finish()
        outside = self.directory / "private-original.obj"
        outside.write_bytes(b"private-authoring-source")
        link = self.module.OUTPUT_DIR / job_id / "model_original.obj"
        try:
            link.symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest("The current platform does not allow unprivileged symlink creation")
        response = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertGreaterEqual(response.status_code, 400)
        self.assertTrue(response.is_json)

    def test_archive_rejects_model_outside_its_job_directory(self):
        result = self.create()
        job_id = self.finish()
        outside = self.directory / "outside.glb"
        outside.write_bytes(b"must-not-leak")
        records = self.stored()
        records[job_id]["model_path"] = str(outside)
        self.rewrite_and_restart(records)
        response = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertGreaterEqual(response.status_code, 400)
        self.assertTrue(response.is_json)
        self.assertNotIn(b"must-not-leak", response.data)
        self.assertEqual(result["jobs"][0]["id"], job_id)

    def test_archive_rejects_input_path_outside_its_job_directory(self):
        self.create()
        job_id = self.finish()
        outside = self.directory / "outside.png"
        outside.write_bytes(b"private-input-must-not-leak")
        records = self.stored()
        records[job_id]["image_paths"] = [str(outside)]
        self.rewrite_and_restart(records)
        response = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertGreaterEqual(response.status_code, 400)
        self.assertTrue(response.is_json)

    def test_archive_rejects_symlink_escape(self):
        self.create()
        job_id = self.finish()
        outside = self.directory / "outside-private.png"
        outside.write_bytes(b"symlink-private-bytes")
        link = self.module.OUTPUT_DIR / job_id / "processed_inputs" / "02.png"
        try:
            link.symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest("The current platform does not allow unprivileged symlink creation")
        response = self.client.get(f"/api/jobs/{job_id}/archive")
        self.assertGreaterEqual(response.status_code, 400)
        self.assertTrue(response.is_json)

    def test_unknown_record_and_archive_errors_are_json(self):
        for endpoint in ("/api/jobs/unknown", "/api/jobs/unknown/record", "/api/jobs/unknown/archive"):
            response = self.client.get(endpoint)
            self.assertEqual(response.status_code, 404)
            self.assertTrue(response.is_json)

    def test_cli_process_submit_status_history_wait_and_download(self):
        server = self.start_http()
        source = self.directory / "원본 캐릭터.png"
        source.write_bytes(PNG)
        request_id = str(uuid.uuid4())
        created = self.cli(server, "generate", str(source), "--request-id", request_id)
        self.assertEqual(created["request_id"], request_id)
        job_id = created["jobs"][0]["id"]
        self.assertEqual(self.cli(server, "status", job_id)["job"]["status"], "queued")
        local = self.cli(server, "history", "--request-id", request_id)
        self.assertEqual(local["total"], 1)
        self.assertEqual(local["requests"][0]["inputs"][0]["sha256"], hashlib.sha256(PNG).hexdigest())
        remote = self.cli(server, "history", "--remote", "--request-id", request_id)
        self.assertEqual(remote["total"], 1)
        self.assertEqual(remote["jobs"][0]["id"], job_id)
        self.finish()
        waited = self.cli(server, "wait", job_id, "--timeout", "3", "--poll-interval", "1")
        self.assertEqual(waited["jobs"][0]["status"], "completed")
        output = self.directory / "downloaded"
        downloaded = self.cli(server, "download", job_id, "--output", str(output))
        archive = Path(downloaded["archive"])
        self.assertTrue(archive.is_file())
        self.assertEqual(downloaded["archive_sha256"], hashlib.sha256(archive.read_bytes()).hexdigest())
        receipt = json.loads(Path(downloaded["receipt"]).read_text(encoding="utf-8"))
        self.assertEqual(receipt["job_id"], job_id)
        with zipfile.ZipFile(archive) as package:
            self.assertEqual(package.read("model.glb"), MODEL)
        manifest = self.cli(server, "history", "--request-id", request_id)["requests"][0]
        self.assertEqual(manifest["downloads"][0]["job_id"], job_id)
        self.assertFalse(list(output.glob("*.part")))
        self.assertEqual(len(self.submitted), 1)

    def test_cli_process_timeout_and_failed_job_exit_codes(self):
        result = self.create()
        job_id = result["jobs"][0]["id"]
        server = self.start_http()
        timed_out = self.cli(server, "wait", job_id, "--timeout", "0.3", "--poll-interval", "1", expected=3)
        self.assertEqual(timed_out["jobs"][0]["status"], "queued")

        def fail_generation(*args, **kwargs):
            raise RuntimeError("Controlled validation failure")

        self.module.run_isolated_generation = fail_generation
        self.finish()
        self.assertEqual(self.cli(server, "status", job_id, expected=2)["job"]["status"], "failed")
        self.assertEqual(self.cli(server, "wait", job_id, "--timeout", "2", expected=2)["jobs"][0]["status"], "failed")
        self.assertIn("error", self.cli(server, "download", job_id, "--output", str(self.directory / "failed-download"), expected=2))

    def test_cli_process_recovers_uncertain_submission_from_preserved_input(self):
        from flask import jsonify, request
        lose_response = [True]

        @self.app.after_request
        def suppress_first_created_response(response):
            if request.path == "/api/jobs" and request.method == "POST" and response.status_code == 201 and lose_response[0]:
                lose_response[0] = False
                failed = jsonify(error="controlled_uncertain_response")
                failed.status_code = 502
                return failed
            return response

        server = self.start_http()
        source = self.directory / "recoverable.png"
        source.write_bytes(PNG)
        request_id = str(uuid.uuid4())
        uncertain = self.cli(server, "generate", str(source), "--request-id", request_id, expected=1)
        self.assertEqual(uncertain["request_id"], request_id)
        self.assertEqual(len(self.submitted), 1)
        manifests = list((self.directory / "cli-state" / "servers").glob(f"*/requests/{request_id}/manifest.json"))
        self.assertEqual(len(manifests), 1)
        manifest = json.loads(manifests[0].read_text(encoding="utf-8"))
        self.assertEqual((manifests[0].parent / manifest["inputs"][0]["local_path"]).read_bytes(), PNG)
        source.unlink()
        recovered = self.cli(server, "generate", "--request-id", request_id)
        self.assertTrue(recovered["replayed"])
        self.assertEqual(recovered["jobs"][0]["id"], self.submitted[0][1])
        self.assertEqual(len(self.submitted), 1)

    def test_cli_process_rejects_html_archive_and_keeps_previous_download(self):
        from flask import Response
        self.create()
        job_id = self.finish()
        requested = []

        def html_archive(job_id):
            requested.append(job_id)
            return Response("<html>unexpected login page</html>", mimetype="text/html")

        self.app.view_functions["job_archive"] = html_archive
        server = self.start_http()
        output = self.directory / "existing-download"
        output.mkdir()
        existing = output / f"{job_id}.zip"
        existing.write_bytes(b"previous-download-must-survive")
        failed = self.cli(server, "download", job_id, "--output", str(output), expected=1)
        self.assertIn("error", failed)
        self.assertEqual(requested, [job_id])
        self.assertEqual(existing.read_bytes(), b"previous-download-must-survive")
        self.assertFalse(list(output.glob("*.part")))

    def test_cli_process_rejects_tampered_and_unsafe_archives(self):
        from flask import Response
        self.create()
        job_id = self.finish()
        original_response = self.client.get(f"/api/jobs/{job_id}/archive")
        original = original_response.data
        original_response.close()
        variants = []
        for mode in ("tampered", "unsafe"):
            contents = io.BytesIO()
            with zipfile.ZipFile(io.BytesIO(original)) as source, zipfile.ZipFile(contents, "w") as target:
                for item in source.infolist():
                    content = source.read(item.filename)
                    if mode == "tampered" and item.filename == "model.glb":
                        content = b"tampered-model-does-not-match-record"
                    target.writestr(item.filename, content)
                if mode == "unsafe":
                    target.writestr("../escape.txt", b"unsafe-archive-path")
            variants.append(contents.getvalue())
        requested = []

        def bad_archive(job_id):
            requested.append(job_id)
            return Response(variants.pop(0), mimetype="application/zip")

        self.app.view_functions["job_archive"] = bad_archive
        server = self.start_http()
        output = self.directory / "rejected-downloads"
        for _ in range(2):
            failed = self.cli(server, "download", job_id, "--output", str(output), expected=1)
            self.assertIn("error", failed)
        self.assertEqual(requested, [job_id, job_id])
        self.assertFalse(list(output.glob("*.zip")))
        self.assertFalse(list(output.glob("*.part")))

    def test_cli_process_refuses_off_origin_download_url(self):
        from flask import jsonify
        self.create()
        job_id = self.finish()
        original_status = self.app.view_functions["job_status"]

        def off_origin_status(job_id):
            job = original_status(job_id).get_json()
            job["archive_url"] = "https://attacker.invalid/private.zip"
            return jsonify(job)

        self.app.view_functions["job_status"] = off_origin_status
        server = self.start_http()
        output = self.directory / "off-origin-download"
        failed = self.cli(server, "download", job_id, "--output", str(output), expected=1)
        self.assertIn("origin", failed["error"].lower())
        self.assertFalse(list(output.glob("*.zip")))

    def test_cli_process_direct_login_saves_config_without_password(self):
        server = self.start_http()
        logged_in = self.cli(server, "login")
        self.assertTrue(logged_in["authenticated"])
        config = json.loads((self.directory / "cli-state" / "config.json").read_text(encoding="utf-8"))
        self.assertEqual(config, {"server": server})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verbose", action="store_true", help="Show each test result")
    args = parser.parse_args()
    output = io.StringIO()
    result = unittest.TextTestRunner(stream=output, verbosity=2 if args.verbose else 1).run(unittest.defaultTestLoader.loadTestsFromTestCase(CliApiTests))
    if args.verbose or not result.wasSuccessful():
        print(output.getvalue(), end="")
    if result.wasSuccessful():
        print(f"CLI API validation: {result.testsRun} tests passed, {len(result.skipped)} skipped (no GPU inference).")
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())
