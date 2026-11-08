"""Offline CLI transport and preflight tests; no server or GPU work."""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from unittest.mock import patch
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from modeling_cli import Client, ClientError, generation_options
from modeling_collection import resume_request


def options(*pairs, model=None):
    return generation_options(argparse.Namespace(option=list(pairs), model=model, seed=None, gpu=None))


class DummyState:
    def __init__(self, root):
        self.root = Path(root)
        self.saved = []

    def manifest_path(self, _request_id):
        return self.root / "manifest.json"

    def write_manifest(self, manifest):
        self.saved.append(dict(manifest))


class DualOptionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "input.png").write_bytes(b"test input")
        self.client = Client.__new__(Client)
        self.client.state = DummyState(self.root)
        self.calls = []
        self.capability = {
            "schema_version": 1,
            "defaults": {"model": "trellis2"},
            "models": {"trellis2": {"parameters": {
                "trellis_dual_postprocess": {"type": "string", "enum": ["on", "off"]}}}},
        }

        def request(method, path, **kwargs):
            self.calls.append((method, path, kwargs))
            if method == "GET":
                return self.capability
            return {"request_id": "request", "jobs": [{"id": "job-1", "status": "queued"}]}

        self.client.json = request

    def manifest(self, submitted):
        return {"request_id": "request", "state": "prepared", "submitted_options": submitted,
                "inputs": [{"filename": "input.png", "local_path": "input.png"}]}

    def test_values_are_explicitly_validated(self):
        for value in ("on", "off"):
            with self.subTest(value=value):
                self.assertEqual(options("trellis_dual_postprocess=" + value),
                                 {"trellis_dual_postprocess": value})
        for value in ("", "true", "false", "1", "ON"):
            with self.subTest(value=value):
                with self.assertRaises(ClientError):
                    options("trellis_dual_postprocess=" + value)
        with self.assertRaises(ClientError):
            options("trellis_dual_postprocess=on", "trellis_dual_postprocess=off")

    def test_explicit_option_is_checked_and_transported_without_mutation(self):
        submitted = options("trellis_dual_postprocess=on", model="trellis2")
        manifest = self.manifest(submitted.copy())
        self.client.generate(manifest)
        self.assertEqual([(x[0], x[1]) for x in self.calls],
                         [("GET", "/api/capabilities"), ("POST", "/api/jobs")])
        self.assertEqual(self.calls[-1][2]["data"], submitted)
        self.assertEqual(manifest["submitted_options"], submitted)
        self.assertEqual(manifest["state"], "submitted")
        self.assertEqual(self.client.state.saved[0]["state"], "submitting")

    def test_default_model_is_checked_and_off_is_preserved(self):
        manifest = self.manifest(options("trellis_dual_postprocess=off"))
        self.client.generate(manifest)
        self.assertEqual(self.calls[-1][2]["data"], {"trellis_dual_postprocess": "off"})

    def test_unsupported_server_or_model_never_submits_or_changes_manifest(self):
        for model, metadata in (("trellis2", {}), ("hunyuan3d-2.1", {"parameters": {}})):
            with self.subTest(model=model):
                self.calls.clear()
                self.client.state.saved.clear()
                self.capability["models"][model] = metadata
                manifest = self.manifest(options("trellis_dual_postprocess=on", model=model))
                with self.assertRaises(ClientError) as caught:
                    self.client.generate(manifest)
                self.assertEqual(caught.exception.code, "UNSUPPORTED_API")
                self.assertEqual(manifest["state"], "prepared")
                self.assertEqual(self.client.state.saved, [])
                self.assertEqual([(x[0], x[1]) for x in self.calls], [("GET", "/api/capabilities")])

    def test_malformed_model_capabilities_never_submits(self):
        self.capability["models"] = []
        manifest = self.manifest(options("trellis_dual_postprocess=on", model="trellis2"))
        with self.assertRaises(ClientError) as caught:
            self.client.generate(manifest)
        self.assertEqual(caught.exception.code, "UNSUPPORTED_API")
        self.assertEqual(manifest["state"], "prepared")
        self.assertEqual([(x[0], x[1]) for x in self.calls], [("GET", "/api/capabilities")])

    def test_omitted_option_preserves_previous_single_post_behavior(self):
        manifest = self.manifest(options(model="trellis2"))
        self.client.generate(manifest)
        self.assertEqual([(x[0], x[1]) for x in self.calls], [("POST", "/api/jobs")])
        self.assertEqual(self.calls[0][2]["data"], {"model": "trellis2"})

    def test_preserved_invalid_value_is_rejected_before_capability_or_submission(self):
        manifest = self.manifest({"trellis_dual_postprocess": "true"})
        with self.assertRaises(ClientError):
            self.client.generate(manifest)
        self.assertEqual(manifest["state"], "prepared")
        self.assertEqual(self.calls, [])

    def test_resume_with_existing_server_job_never_replays(self):
        existing = [{"id": "original-job", "status": "completed"}]
        self.client.request_jobs = lambda request: existing
        with patch("modeling_collection.collect_request", return_value=({"ok": True}, 0)) as collect:
            result, code = resume_request(self.client, "original-request", None)
        self.assertEqual(code, 0)
        self.assertTrue(result["ok"])
        self.assertEqual(self.calls, [])
        self.assertEqual(collect.call_args.kwargs["jobs"], existing)

    def test_uncertain_resume_rechecks_capability_before_replay(self):
        manifest = self.manifest({"model": "trellis2", "trellis_dual_postprocess": "on"})
        manifest.update(server="fixture", request_id="request", state="submitting")
        self.client.server = "fixture"
        self.client.request_jobs = lambda request: []
        self.client.state.manifest_path("request").write_text("{}", encoding="utf-8")
        self.client.state.load_manifest = lambda request: manifest
        self.client.state.prepare = lambda request, images, opts: manifest
        self.capability["models"]["trellis2"]["parameters"] = {}
        with self.assertRaises(ClientError) as caught:
            resume_request(self.client, "request", None)
        self.assertEqual(caught.exception.code, "UNSUPPORTED_API")
        self.assertEqual([(x[0], x[1]) for x in self.calls], [("GET", "/api/capabilities")])
        self.assertEqual(manifest["state"], "submitting")


class CliHttpTests(unittest.TestCase):
    """Exercise the actual CLI subprocess and multipart wire without GPU inference."""
    @classmethod
    def setUpClass(cls):
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args):
                pass

            def send_json(self, payload):
                body = json.dumps(payload).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def do_GET(self):
                self.server.calls.append(("GET", self.path, b""))
                parameters = {"trellis_dual_postprocess": {"enum": ["on", "off"]}} if self.server.advertised else {}
                self.send_json({"schema_version": 1, "defaults": {"model": "trellis2"},
                                "models": {"trellis2": {"parameters": parameters}}})

            def do_POST(self):
                body = self.rfile.read(int(self.headers["Content-Length"]))
                self.server.calls.append(("POST", self.path, body))
                self.send_json({"request_id": self.headers["Idempotency-Key"],
                                "jobs": [{"id": "wire-job", "status": "queued"}]})

        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        cls.worker = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.worker.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.worker.join(2)

    def run_cli(self, advertised, *extra):
        self.server.calls = []
        self.server.advertised = advertised
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "input.png"
            image.write_bytes(b"fixture")
            command = [sys.executable, "-B", str(Path(__file__).resolve().parents[1] / "modeling_cli.py"),
                       "generate", str(image), "--server", "http://127.0.0.1:" + str(self.server.server_port),
                       "--state-dir", str(root / "state"), "--json", *extra]
            result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", timeout=20)
            payload = json.loads(result.stdout)
            return result.returncode, payload, list(self.server.calls)

    def test_supported_server_receives_explicit_option_as_multipart(self):
        code, payload, calls = self.run_cli(True, "--model", "trellis2", "--option", "trellis_dual_postprocess=on")
        self.assertEqual(code, 0, payload)
        self.assertEqual([x[0] for x in calls], ["GET", "POST"])
        self.assertIn(b'name="trellis_dual_postprocess"\r\n\r\non\r\n', calls[1][2])

    def test_older_server_is_rejected_before_post(self):
        code, payload, calls = self.run_cli(False, "--option", "trellis_dual_postprocess=on")
        self.assertEqual(code, 1, payload)
        self.assertEqual(payload["code"], "UNSUPPORTED_API")
        self.assertEqual([x[0] for x in calls], ["GET"])

    def test_legacy_request_omits_preflight_and_new_field(self):
        code, payload, calls = self.run_cli(False, "--model", "trellis2")
        self.assertEqual(code, 0, payload)
        self.assertEqual([x[0] for x in calls], ["POST"])
        self.assertNotIn(b"trellis_dual_postprocess", calls[0][2])


if __name__ == "__main__":
    unittest.main()
