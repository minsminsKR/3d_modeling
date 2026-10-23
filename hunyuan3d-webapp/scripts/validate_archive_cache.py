"""Isolated archive cache, HTTP conditional/range and confinement validation."""
from __future__ import annotations

import concurrent.futures
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import unittest
from unittest.mock import patch
import zipfile

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))

from flask import Flask, jsonify
import generation_api
import modeling_archives as cache
from modeling_contract import ModelingError


class ArchiveCacheTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix=".archive-cache-test-", dir=BASE)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.uploads = self.root / "uploads"
        self.outputs = self.root / "outputs"
        self.job_id = "archive-test-job-01"
        for directory in (self.uploads / self.job_id, self.outputs / self.job_id):
            directory.mkdir(parents=True)
        self.model = self.outputs / self.job_id / "model.glb"
        self.model.write_bytes(b"glTF" + b"fixture-model-bytes" * 200)
        self.input = self.uploads / self.job_id / "reference.png"
        self.input.write_bytes(b"original-image-content")
        self.job = {"id": self.job_id, "status": "completed", "request_id": "request-0001",
                    "image_paths": [str(self.input)], "model_path": str(self.model),
                    "inputs": [{"name": "reference.png"}]}
        self.public = {"id": self.job_id, "status": "completed", "request_id": "request-0001",
                       "options": {"seed": 12}, "rigging": {"status": "idle"}}
        self.app = Flask(__name__)
        self.app.testing = True

        @self.app.errorhandler(ModelingError)
        def failure(error):
            return jsonify(error.as_dict()), error.http_status or 500

        @self.app.get("/archive")
        def archive():
            return generation_api.archive_for_job(self.job, self.public, self.uploads, self.outputs)

        @self.app.get("/record")
        def record():
            return jsonify(generation_api.record_for_job(self.job, self.public, self.uploads, self.outputs))

        self.client = self.app.test_client()

    def record(self):
        response = self.client.get("/record")
        self.assertEqual(response.status_code, 200, response.data)
        return response.json

    def archive(self, **kwargs):
        response = self.client.get("/archive", **kwargs)
        data = response.data
        response.close()
        return response, data

    def test_warm_record_and_archive_do_not_read_sources_or_rebuild(self):
        record = self.record()
        with patch.object(cache, "_copy_source", side_effect=AssertionError("source reread")), \
                patch.object(cache, "_build", side_effect=AssertionError("ZIP rebuilt")), \
                patch.object(generation_api, "file_digest", side_effect=AssertionError("source hashed")):
            self.assertEqual(self.record(), record)
            response, data = self.archive()
        self.assertEqual(hashlib.sha256(data).hexdigest(), record["archive"]["sha256"])
        self.assertEqual(response.headers["ETag"], record["archive"]["etag"])
        self.assertEqual(len(data), record["archive"]["size"])

    def test_zip_preserves_originals_and_hashes_and_omits_self_digest(self):
        paths = {"model_initial.glb": b"original-glb", "pbr/model.obj": b"obj", "pbr/model.mtl": b"mtl",
                 "pbr/albedo.jpg": b"pbr-texture", "editable.blend": b"editable-blender"}
        for relative, content in paths.items():
            path = self.model.parent / relative
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(content)
        record = self.record()
        _, data = self.archive()
        with zipfile.ZipFile(io.BytesIO(data)) as package:
            embedded = json.loads(package.read("generation-record.json"))
            self.assertNotIn("archive", embedded)
            self.assertEqual({**embedded, "archive": record["archive"]}, record)
            for artifact in embedded["artifacts"]:
                payload = package.read(artifact["name"])
                self.assertEqual(hashlib.sha256(payload).hexdigest(), artifact["sha256"])
            for relative, content in paths.items():
                self.assertEqual(package.read("outputs/" + relative), content)
            self.assertEqual(package.read("inputs/01_reference.png"), self.input.read_bytes())

    def test_stat_change_and_additional_rigging_file_invalidate(self):
        first = self.record()
        self.model.write_bytes(b"edited-model")
        second = self.record()
        self.assertNotEqual(first["archive"]["version"], second["archive"]["version"])
        rig = self.model.parent / "rigging" / "rigged.glb"
        rig.parent.mkdir()
        rig.write_bytes(b"rigged-model")
        third = self.record()
        self.assertNotEqual(second["archive"]["etag"], third["archive"]["etag"])
        rig.unlink()
        fourth = self.record()
        self.assertNotEqual(third["archive"]["version"], fourth["archive"]["version"])

    def test_same_size_restored_mtime_still_invalidates_via_ctime(self):
        first = self.record()
        info = self.model.stat()
        self.model.write_bytes(b"X" * info.st_size)
        os.utime(self.model, ns=(info.st_atime_ns, info.st_mtime_ns))
        second = self.record()
        self.assertNotEqual(first["archive"]["etag"], second["archive"]["etag"])

    def test_input_edit_and_public_provenance_invalidate(self):
        first = self.record()
        self.input.write_bytes(b"new-input")
        second = self.record()
        self.assertNotEqual(first["archive"]["version"], second["archive"]["version"])
        self.public["rigging"] = {"status": "completed", "method": "joint-edit"}
        third = self.record()
        self.assertNotEqual(second["archive"]["version"], third["archive"]["version"])

    def test_cache_payload_tamper_rebuilds_instead_of_serving_tamper(self):
        record = self.record()
        path = next((self.root / "archive-cache").glob("*/*.zip"))
        path.write_bytes(b"not-a-zip")
        self.assertEqual(self.record()["archive"]["sha256"], record["archive"]["sha256"])
        _, data = self.archive()
        self.assertEqual(hashlib.sha256(data).hexdigest(), record["archive"]["sha256"])

    def test_http_head_range_conditional_and_if_range_contract(self):
        record = self.record()
        etag = record["archive"]["etag"]
        full, data = self.archive()
        self.assertEqual(full.status_code, 200)
        self.assertEqual(full.headers["Accept-Ranges"], "bytes")
        self.assertTrue(full.headers["Cache-Control"].startswith("private"))
        head = self.client.head("/archive")
        self.assertEqual(head.status_code, 200)
        self.assertEqual(head.data, b"")
        self.assertEqual(int(head.headers["Content-Length"]), len(data))
        self.assertEqual(head.headers["ETag"], etag)
        head.close()
        ranged, part = self.archive(headers={"Range": "bytes=10-29", "If-Range": etag})
        self.assertEqual(ranged.status_code, 206)
        self.assertEqual(part, data[10:30])
        self.assertEqual(ranged.headers["Content-Range"], f"bytes 10-29/{len(data)}")
        changed, body = self.archive(headers={"Range": "bytes=10-", "If-Range": '"old-version"'})
        self.assertEqual(changed.status_code, 200)
        self.assertEqual(body, data)
        conditional, body = self.archive(headers={"If-None-Match": etag})
        self.assertEqual(conditional.status_code, 304)
        self.assertEqual(body, b"")
        invalid, _ = self.archive(headers={"Range": f"bytes={len(data)}-"})
        self.assertEqual(invalid.status_code, 416)
        self.assertEqual(invalid.headers["Content-Range"], f"bytes */{len(data)}")
        self.assertEqual(invalid.headers["ETag"], etag)
        self.assertEqual(invalid.headers["Accept-Ranges"], "bytes")
        self.assertEqual(invalid.json["http_status"], 416)

    def test_open_ended_suffix_and_head_range(self):
        record = self.record()
        _, data = self.archive()
        for range_header, expected in (("bytes=20-", data[20:]), ("bytes=-30", data[-30:])):
            response, result = self.archive(headers={"Range": range_header})
            self.assertEqual(response.status_code, 206)
            self.assertEqual(result, expected)
        response = self.client.head("/archive", headers={"Range": "bytes=1-10", "If-Range": record["archive"]["etag"]})
        self.assertEqual(response.status_code, 206)
        self.assertEqual(response.headers["Content-Length"], "10")
        self.assertEqual(response.data, b"")
        response.close()

    def test_pending_archive_is_409_and_pending_record_does_not_build(self):
        self.job["status"] = "queued"
        self.public["status"] = "queued"
        with patch.object(cache, "_build", side_effect=AssertionError("queued archive")):
            record = self.record()
            self.assertNotIn("archive", record)
            response, _ = self.archive()
            self.assertEqual(response.status_code, 409)
            self.assertEqual(response.json["code"], "JOB_NOT_READY")

    def test_concurrent_threads_build_once(self):
        barrier = threading.Barrier(6)
        original = cache._build

        def read(_):
            barrier.wait(timeout=10)
            return generation_api.record_for_job(self.job, self.public, self.uploads, self.outputs)

        with patch.object(cache, "_build", wraps=original) as build, concurrent.futures.ThreadPoolExecutor(max_workers=6) as executor:
            records = list(executor.map(read, range(6)))
        self.assertEqual(build.call_count, 1)
        self.assertEqual(len({record["archive"]["etag"] for record in records}), 1)

    def test_concurrent_processes_share_single_published_snapshot(self):
        arguments = [sys.executable, "-c", (
            "import json,sys; from pathlib import Path; import generation_api as g; "
            "cfg=json.loads(sys.argv[1]); p=Path(cfg['root']); "
            "print(json.dumps(g.record_for_job(cfg['job'],cfg['public'],p/'uploads',p/'outputs')['archive']))"
        ), json.dumps({"root": str(self.root), "job": self.job, "public": self.public})]
        processes = [subprocess.Popen(arguments, cwd=BASE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True) for _ in range(3)]
        results = [process.communicate(timeout=30) for process in processes]
        for process, (_, error) in zip(processes, results):
            self.assertEqual(process.returncode, 0, error)
        self.assertEqual(len({json.loads(output)["etag"] for output, _ in results}), 1)
        self.assertEqual(len(list((self.root / "archive-cache").glob("*/*.zip"))), 1)
        self.assertFalse(list((self.root / "archive-cache").rglob(".building-*")))

    def test_builder_failure_preserves_previous_version_no_partial_publish(self):
        first = self.record()
        old_path = next((self.root / "archive-cache").glob("*/*.zip"))
        old_bytes = old_path.read_bytes()
        self.model.write_bytes(b"new-model")
        with patch.object(cache, "_copy_source", side_effect=OSError("controlled disk failure")):
            with self.assertRaises(OSError):
                generation_api.record_for_job(self.job, self.public, self.uploads, self.outputs)
        self.assertEqual(hashlib.sha256(old_path.read_bytes()).hexdigest(), first["archive"]["sha256"])
        self.assertEqual(old_path.read_bytes(), old_bytes)
        self.assertFalse(list((self.root / "archive-cache").rglob(".building-*")))

    def test_source_change_during_copy_retries_before_publish(self):
        original = cache._copy_source
        changed = [False]

        def copy(archive, source, identity, maximum):
            result = original(archive, source, identity, maximum)
            if source.path == self.model and not changed[0]:
                changed[0] = True
                self.model.write_bytes(b"edited-during-copy")
            return result

        with patch.object(cache, "_copy_source", side_effect=copy):
            record = self.record()
        self.assertEqual(record["artifacts"][0]["sha256"], hashlib.sha256(b"edited-during-copy").hexdigest())
        self.assertEqual(len(list((self.root / "archive-cache").glob("*/*.zip"))), 1)
        self.assertFalse(list((self.root / "archive-cache").rglob(".building-*")))

    def test_constant_source_change_is_bounded_and_keeps_old_snapshot(self):
        self.record()
        old_path = next((self.root / "archive-cache").glob("*/*.zip"))
        old = old_path.read_bytes()
        self.model.write_bytes(b"new-edit")
        original = cache._copy_source

        def copy(archive, source, identity, maximum):
            result = original(archive, source, identity, maximum)
            if source.path == self.model:
                self.model.write_bytes(self.model.read_bytes() + b"x")
            return result

        with patch.object(cache, "_copy_source", side_effect=copy) as tracked:
            response = self.client.get("/record")
        self.assertEqual(response.status_code, 503)
        self.assertTrue(response.json["retryable"])
        self.assertLessEqual(tracked.call_count, 6)
        self.assertEqual(old_path.read_bytes(), old)
        self.assertEqual(len(list((self.root / "archive-cache").glob("*/*.zip"))), 1)

    def test_quota_prunes_only_generated_cache_and_preserves_sources(self):
        first = self.record()
        cache_root = self.root / "archive-cache"
        keep = cache_root / "operator-note.txt"
        keep.write_text("keep this", encoding="utf-8")
        maximum = first["archive"]["size"] * 2 + 8000
        with patch.dict(os.environ, {"MODELING_ARCHIVE_CACHE_MAX_BYTES": str(maximum)}):
            for count in range(12):
                self.public["revision"] = count
                self.record()
        files = list(cache_root.glob("*/*.zip")) + list(cache_root.glob("*/*.json"))
        self.assertLessEqual(sum(path.stat().st_size for path in files), maximum)
        self.assertLess(len(list(cache_root.glob("*/*.zip"))), 12)
        self.assertEqual(keep.read_text(), "keep this")
        self.assertTrue(self.model.is_file())
        self.assertTrue(self.input.is_file())

    def test_active_snapshot_cannot_be_pruned(self):
        first = self.record()
        lease = generation_api._snapshot(self.job, self.public, self.uploads, self.outputs)
        self.addCleanup(lease.close)
        maximum = lease.path.stat().st_size + lease.path.with_suffix(".json").stat().st_size + 1024
        self.public["revision"] = 20
        with patch.dict(os.environ, {"MODELING_ARCHIVE_CACHE_MAX_BYTES": str(maximum)}):
            response = self.client.get("/record")
        self.assertEqual(response.status_code, 503)
        self.assertTrue(lease.path.is_file())
        self.assertEqual(hashlib.sha256(lease.path.read_bytes()).hexdigest(), first["archive"]["sha256"])

    def test_too_large_archive_does_not_destroy_previous_cache(self):
        self.record()
        previous = next((self.root / "archive-cache").glob("*/*.zip"))
        with patch.dict(os.environ, {"MODELING_ARCHIVE_CACHE_MAX_BYTES": "1"}):
            self.public["revision"] = 99
            response = self.client.get("/record")
        self.assertEqual(response.status_code, 409)
        self.assertTrue(previous.is_file())
        self.assertFalse(list((self.root / "archive-cache").rglob(".building-*")))

    def test_rejects_source_and_cache_symlinks_and_outside_paths(self):
        outside = self.root / "outside.glb"
        outside.write_bytes(b"private")
        self.job["model_path"] = str(outside)
        response, _ = self.archive()
        self.assertEqual(response.status_code, 409)
        self.job["model_path"] = str(self.model)
        linked = self.model.parent / "original.blend"
        try:
            linked.symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest("Unprivileged symlink creation unavailable")
        response, _ = self.archive()
        self.assertEqual(response.status_code, 409)
        linked.unlink()
        external = self.root / "external-cache"
        external.mkdir()
        # Earlier rejected snapshots may have created a private cache skeleton.
        # Move only that fixture directory aside before testing a linked root.
        cache_root = self.root / "archive-cache"
        if cache_root.exists():
            cache_root.rename(self.root / "prior-cache-fixture")
        (self.root / "archive-cache").symlink_to(external, target_is_directory=True)
        response, _ = self.archive()
        self.assertEqual(response.status_code, 409)
        self.assertFalse(list(external.iterdir()))

    def test_internal_manifest_and_public_record_contain_no_absolute_paths(self):
        record = self.record()
        self.assertNotIn(str(self.root), json.dumps(record))
        for path in (self.root / "archive-cache").glob("*/*.json"):
            self.assertNotIn(str(self.root), path.read_text(encoding="utf-8"))

    def test_two_parallel_warm_read_leases_do_not_serialize(self):
        self.record()
        first = generation_api._snapshot(self.job, self.public, self.uploads, self.outputs)
        self.addCleanup(first.close)
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
            result = executor.submit(generation_api._snapshot, self.job, self.public, self.uploads, self.outputs)
            second = result.result(timeout=5)
            second.close()

    def test_first_cold_stream_does_not_block_another_record_or_download(self):
        first = self.client.get("/archive", buffered=False)
        try:
            with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
                pending = executor.submit(generation_api.record_for_job, self.job, self.public, self.uploads, self.outputs)
                record = pending.result(timeout=5)
            second, _ = self.archive()
            self.assertEqual(second.headers["ETag"], record["archive"]["etag"])
            self.assertEqual(first.headers["ETag"], second.headers["ETag"])
        finally:
            first.close()

    def test_abandoned_temporary_files_cleaned_without_touching_other_files(self):
        self.record()
        directory = next(path for path in (self.root / "archive-cache").iterdir() if path.is_dir())
        orphan = directory / (".building-" + "a" * 32 + ".zip")
        orphan.write_bytes(b"abandoned-worker-data")
        unknown = directory / ".building-user-note.zip"
        unknown.write_bytes(b"operator-data")
        self.record()
        self.assertFalse(orphan.exists())
        self.assertEqual(unknown.read_bytes(), b"operator-data")

    def test_quota_cleanup_skips_active_builder_and_cleans_it_after_release(self):
        self.record()
        root = self.root / "archive-cache"
        other = root / ("b" * 32)
        other.mkdir()
        temporary = other / (".building-" + "c" * 32 + ".zip")
        temporary.write_bytes(b"active-worker-data")
        with cache.FileLease(other / "builder.lock"):
            cache._make_room(root, 0, 10 * 1024 * 1024, None)
            self.assertTrue(temporary.exists())
        cache._make_room(root, 0, 10 * 1024 * 1024, None)
        self.assertFalse(temporary.exists())


if __name__ == "__main__":
    unittest.main()
