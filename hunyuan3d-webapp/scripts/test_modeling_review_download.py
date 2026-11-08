"""Regression tests for review-required GLB transport; no GPU or real server."""
from __future__ import annotations

import hashlib
import json
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from modeling_cli import ClientError
from modeling_state import atomic_json
from modeling_review_download import transfer_review

JOB = "review-job"
REQUEST = "f07e3a76-fc22-48d7-9ee4-968f06f3d095"


def make_glb(extra=65536):
    raw = json.dumps({"asset": {"version": "2.0"}, "buffers": [{"byteLength": extra}]},
                     separators=(",", ":")).encode()
    raw += b" " * (-len(raw) % 4)
    binary = b"x" * (extra + (-extra % 4))
    body = struct.pack("<II", len(raw), 0x4E4F534A) + raw + struct.pack("<II", len(binary), 0x004E4942) + binary
    return struct.pack("<4sII", b"glTF", 2, len(body) + 12) + body


class Response:
    def __init__(self, status, headers, body=b"", interrupt_after=None):
        self.status_code, self.headers, self.body = status, headers, body
        self.interrupt_after = interrupt_after

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return False

    def close(self):
        pass

    def iter_content(self, chunk_size=1024):
        if self.interrupt_after is not None:
            yield self.body[:self.interrupt_after]
            raise requests.ConnectionError("simulated interrupted transfer")
        for i in range(0, len(self.body), min(chunk_size, 4096)):
            yield self.body[i:i + min(chunk_size, 4096)]


class FakeClient:
    server = "https://model.example"
    timeout = 120
    def __init__(self, body):
        self.body = body
        self.etag = '"snapshot-v1"'
        self.calls = []
        self.interrupt_after = None
        self.ignore_range = False
        self.head_missing_etag = False
        self.head_missing_length = False
        self.get_wrong_etag = False
        self.bad_range = False
        self.get_bad_length = False
        self.change_on_recheck = False
        self.html = False

    def url(self, path):
        if not path.startswith("/") or path.startswith("//"):
            raise ClientError("invalid origin", code="PROTOCOL_ERROR")
        return self.server + path

    def check(self, response):
        if response.status_code >= 400:
            raise ClientError("HTTP failure", code="PROTOCOL_ERROR")

    def request(self, method, path, **kwargs):
        headers = dict(kwargs.get("headers") or {})
        self.calls.append((method, path, headers))
        assert path == f"/jobs/{JOB}/model"
        base = {"ETag": self.etag, "Content-Length": str(len(self.body)),
                "Content-Type": "text/html" if self.html else "model/gltf-binary"}
        if method == "HEAD":
            if self.change_on_recheck and any(x[0] == "GET" for x in self.calls):
                base["ETag"] = '"snapshot-after-transfer"'
            if self.head_missing_etag:
                base.pop("ETag")
            if self.head_missing_length:
                base.pop("Content-Length")
            return Response(200, base)
        if method != "GET":
            raise AssertionError(method)
        data, code = self.body, 200
        if "Range" in headers and not self.ignore_range and headers.get("If-Range") == self.etag:
            first = int(headers["Range"].removeprefix("bytes=").removesuffix("-"))
            data, code = self.body[first:], 206
            base["Content-Range"] = f"bytes {first}-{len(self.body)-1}/{len(self.body)}"
            base["Content-Length"] = str(len(data))
        if self.get_wrong_etag:
            base["ETag"] = '"different-version"'
        if self.bad_range and code == 206:
            base["Content-Range"] = f"bytes 0-{len(self.body)-1}/{len(self.body)}"
        if self.get_bad_length:
            base["Content-Length"] = str(len(data) + 1)
        return Response(code, base, data, self.interrupt_after)


class ReviewDownloadTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.out = Path(self.tmp.name)
        self.raw = make_glb()
        self.client = FakeClient(self.raw)
        self.record = {"id": JOB, "status": "review_required", "request_id": REQUEST, "artifacts": []}

    def transfer(self):
        return transfer_review(self.client, JOB, self.out, self.record, atomic_json=atomic_json, error=ClientError)

    @property
    def final(self):
        return self.out / f"{JOB}.review.glb"

    def interrupt(self):
        self.client.interrupt_after = 100
        with self.assertRaises(ClientError):
            self.transfer()
        self.client.interrupt_after = None
        self.assertFalse(self.final.exists())
        self.assertTrue((self.out / f".{JOB}.review.glb.part").exists())
        self.assertTrue((self.out / f".{JOB}.review.glb.part.json").exists())
        self.client.calls.clear()

    def test_full_download_and_quality_receipt(self):
        result = self.transfer()
        self.assertEqual(self.final.read_bytes(), self.raw)
        self.assertEqual(result["sha256"], hashlib.sha256(self.raw).hexdigest())
        self.assertEqual(result["size_bytes"], len(self.raw))
        self.assertEqual(result["quality_status"], "review_required")
        self.assertFalse(result["server_sha256_available"])
        receipt = json.loads((self.out / f"{JOB}.review.json").read_text(encoding="utf-8"))
        self.assertEqual(receipt["quality_status"], "review_required")
        self.assertFalse(receipt["server_sha256_available"])
        self.assertEqual(receipt["sha256"], result["sha256"])
        self.assertEqual(receipt["etag"], self.client.etag)

    def test_interruption_resumes_only_matching_verified_prefix(self):
        self.interrupt()
        result = self.transfer()
        get = next(item for item in self.client.calls if item[0] == "GET")
        self.assertEqual(get[2].get("Range"), "bytes=100-")
        self.assertEqual(get[2].get("If-Range"), self.client.etag)
        self.assertEqual(result["resumed_bytes"], 100)
        self.assertEqual(self.final.read_bytes(), self.raw)

    def test_changed_etag_restarts_without_using_partial(self):
        self.interrupt()
        self.client.etag = '"snapshot-v2"'
        result = self.transfer()
        self.assertEqual(result["resumed_bytes"], 0)
        self.assertNotIn("Range", next(x for x in self.client.calls if x[0] == "GET")[2])
        self.assertEqual(self.final.read_bytes(), self.raw)

    def test_tampered_partial_restarts_without_using_prefix(self):
        self.interrupt()
        part = self.out / f".{JOB}.review.glb.part"
        data = bytearray(part.read_bytes()); data[50] ^= 0x10; part.write_bytes(data)
        result = self.transfer()
        self.assertEqual(result["resumed_bytes"], 0)
        self.assertNotIn("Range", next(x for x in self.client.calls if x[0] == "GET")[2])
        self.assertEqual(self.final.read_bytes(), self.raw)

    def test_ignored_range_never_appends_full_file_to_prefix(self):
        self.interrupt()
        self.client.ignore_range = True
        result = self.transfer()
        self.assertEqual(result["resumed_bytes"], 0)
        self.assertEqual(self.final.read_bytes(), self.raw)

    def test_server_version_changes_between_head_and_get(self):
        self.client.get_wrong_etag = True
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertFalse(self.final.exists())

    def test_invalid_partial_range_is_rejected(self):
        self.interrupt()
        self.client.bad_range = True
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertFalse(self.final.exists())

    def test_incorrect_get_length_is_rejected(self):
        self.client.get_bad_length = True
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertFalse(self.final.exists())

    def test_changed_snapshot_after_transfer_is_rejected(self):
        self.client.change_on_recheck = True
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertFalse(self.final.exists())

    def test_missing_head_etag_or_length_rejected(self):
        for flag in ("head_missing_etag", "head_missing_length"):
            with self.subTest(flag=flag):
                setattr(self.client, flag, True)
                with self.assertRaises(ClientError):
                    self.transfer()
                self.assertFalse(self.final.exists())
                setattr(self.client, flag, False)

    def test_existing_unverified_final_is_never_overwritten(self):
        self.final.write_bytes(b"important old result")
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertEqual(self.final.read_bytes(), b"important old result")

    def test_completed_receipt_reused_without_redownloading(self):
        original = self.transfer()
        self.client.calls.clear()
        again = self.transfer()
        self.assertEqual(again["sha256"], original["sha256"])
        self.assertFalse(any(item[0] == "GET" for item in self.client.calls))
        self.assertEqual(self.final.read_bytes(), self.raw)

    def test_cached_receipt_with_changed_request_identity_is_not_reused(self):
        self.transfer()
        self.record["request_id"] = "e55a1789-d557-44db-91e1-eb095f6a17cc"
        self.client.calls.clear()
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertEqual(self.final.read_bytes(), self.raw)
        self.assertFalse(any(x[0] == "GET" for x in self.client.calls))

    def test_invalid_glb_and_html_rejected(self):
        for body, html in ((b"bad-glb-contents", False), (self.raw, True)):
            with self.subTest(html=html):
                self.client.body = body
                self.client.html = html
                with self.assertRaises(ClientError):
                    self.transfer()
                self.assertFalse(self.final.exists())

    def test_glb_with_wrong_gltf_asset_version_is_rejected(self):
        self.client.body = self.raw.replace(b'"version":"2.0"', b'"version":"1.0"', 1)
        self.assertNotEqual(self.client.body, self.raw)
        with self.assertRaises(ClientError):
            self.transfer()
        self.assertFalse(self.final.exists())

    def test_server_advertised_sha_is_verified_if_present(self):
        good_sha = hashlib.sha256(self.raw).hexdigest()
        for advertised in (good_sha, "0" * 64):
            with self.subTest(advertised=advertised), tempfile.TemporaryDirectory() as temp:
                record = dict(self.record, artifacts=[{"name": "model.glb", "size": len(self.raw), "sha256": advertised}])
                out = Path(temp)
                if advertised == good_sha:
                    result = transfer_review(self.client, JOB, out, record, atomic_json=atomic_json, error=ClientError)
                    self.assertTrue(result["server_sha256_available"])
                    self.assertEqual(result["sha256"], good_sha)
                else:
                    with self.assertRaises(ClientError):
                        transfer_review(self.client, JOB, out, record, atomic_json=atomic_json, error=ClientError)
                    self.assertFalse((out / f"{JOB}.review.glb").exists())

    def test_wrong_record_or_status_never_transfers(self):
        for patch in ({"status": "completed"}, {"id": "different-job"}):
            with self.subTest(patch=patch):
                self.record = {"id": JOB, "status": "review_required", "request_id": REQUEST, "artifacts": []}
                self.record.update(patch)
                self.client.calls.clear()
                with self.assertRaises(ClientError):
                    self.transfer()
                self.assertFalse(any(x[0] == "GET" for x in self.client.calls))
                self.assertFalse(self.final.exists())


class OptionalRequestsCliTests(unittest.TestCase):
    """Site packages disabled: help works and dependency failure stays structured."""

    def test_help_without_requests(self):
        cli = Path(__file__).resolve().parents[1] / "modeling_cli.py"
        result = subprocess.run([sys.executable, "-S", str(cli), "--help"],
                                capture_output=True, text=True, encoding="utf-8", timeout=20)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("download-review", result.stdout)

    def test_missing_requests_is_reported_before_network_access(self):
        cli = Path(__file__).resolve().parents[1] / "modeling_cli.py"
        with tempfile.TemporaryDirectory() as temp:
            result = subprocess.run([sys.executable, "-S", str(cli), "capabilities",
                                     "--server", "http://127.0.0.1:9", "--state-dir", temp,
                                     "--json"], capture_output=True, text=True, encoding="utf-8", timeout=20)
        self.assertEqual(result.returncode, 1, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["code"], "LOCAL_IO_ERROR")
        self.assertIn("requests", payload["message"])
        self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
