#!/usr/bin/env python3
"""Focused change-aware CLI state tests; no network, GPU or production files."""
from __future__ import annotations

import copy
import http.cookiejar
import json
import multiprocessing
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest.mock import patch
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import modeling_state as storage
from modeling_contract import ModelingError


def cookie(value="example", expires=None):
    return http.cookiejar.Cookie(0, "session", value, None, False, "test.invalid", True,
                                 False, "/", True, True, expires, True, None, None,
                                 {"HttpOnly": None}, False)


def concurrent_writer(root, server, request_id, field, value, ready, release, results):
    """Each process loads the same old document before either can write."""
    try:
        state = storage.State(Path(root), server)
        manifest = state.load_manifest(request_id)
        manifest[field] = value
        ready.put(field)
        if not release.wait(15):
            raise RuntimeError("Concurrent writer was not released")
        state.write_manifest(manifest)
        results.put((field, True))
    except Exception as exc:
        results.put((field, type(exc).__name__))


class StateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.state = storage.State(self.root / "state", "https://test.invalid")
        self.request_id = str(uuid.uuid4())
        self.job = {"id": "job-one", "request_id": self.request_id,
                    "status": "running", "message": "Generating", "elapsed_seconds": 1,
                    "updated_at": "initial"}
        self.manifest = self.state.recover_manifest(self.request_id, [self.job])

    def saved(self):
        return storage.read_json(self.state.manifest_path(self.request_id))

    def test_unchanged_polls_do_not_read_write_or_fsync(self):
        with patch.object(storage, "read_json", wraps=storage.read_json) as reader, \
                patch.object(storage, "atomic_json", wraps=storage.atomic_json) as writer, \
                patch.object(storage.os, "fsync", wraps=os.fsync) as sync:
            for elapsed in range(2, 10):
                self.state.remember_job(dict(self.job, elapsed_seconds=elapsed))
            self.assertEqual((reader.call_count, writer.call_count, sync.call_count), (0, 0, 0))

    def test_changed_message_options_and_terminal_status_are_saved(self):
        for field, value in (("message", "Texturing"), ("options", {"texture": "on"}),
                             ("updated_at", "next"), ("status", "completed")):
            self.job[field] = value
            self.state.remember_job(self.job)
            self.assertEqual(self.saved()["jobs"][0][field], value)

    def test_explicit_manifest_updated_at_alone_is_not_a_write(self):
        manifest = self.state.load_manifest(self.request_id)
        manifest["updated_at"] = "presentation-only"
        with patch.object(storage, "atomic_json", wraps=storage.atomic_json) as writer:
            self.assertFalse(self.state.write_manifest(manifest))
            self.assertEqual(writer.call_count, 0)

    def test_request_identity_lookup_does_not_enumerate_history(self):
        with patch.object(self.state, "_lookup", side_effect=AssertionError("history lookup")), \
                patch.object(self.state, "manifests", side_effect=AssertionError("history scan")):
            self.state.remember_job(dict(self.job, status="completed"))
        self.assertEqual(self.saved()["jobs"][0]["status"], "completed")

    def test_local_history_request_filter_does_not_scan(self):
        import modeling_cli
        with patch.object(modeling_cli, "State", return_value=self.state), \
                patch.object(self.state, "manifests", side_effect=AssertionError("history scan")), \
                patch.object(modeling_cli, "render") as render:
            code = modeling_cli.main(["history", "--server", self.state.server,
                                      "--request-id", self.request_id, "--json"])
            self.assertEqual(code, 0)
            result = render.call_args.args[0]
            self.assertEqual(result["total"], 1)
            self.assertEqual(result["requests"][0]["request_id"], self.request_id)

    def test_local_history_unknown_request_returns_empty(self):
        import modeling_cli
        with patch.object(modeling_cli, "State", return_value=self.state), \
                patch.object(self.state, "manifests", side_effect=AssertionError("history scan")), \
                patch.object(modeling_cli, "render") as render:
            code = modeling_cli.main(["history", "--server", self.state.server,
                                      "--request-id", str(uuid.uuid4()), "--json"])
            self.assertEqual(code, 0)
            self.assertEqual(render.call_args.args[0]["requests"], [])

    def test_history_pages_read_only_selected_documents_and_keep_order(self):
        for index in range(1, 6):
            self.state.recover_manifest(str(uuid.UUID(int=index)), [{"id": f"page-{index}", "status": "completed"}])
        fresh = storage.State(self.root / "state", self.state.server)
        with patch.object(storage, "read_json", wraps=storage.read_json) as reader:
            page = fresh.manifest_page(limit=2, offset=1)
            self.assertEqual(reader.call_count, 2)
            self.assertEqual(page["total"], 6)
            self.assertEqual([item["request_id"] for item in page["requests"]],
                             [str(uuid.UUID(int=index)) for index in (2, 3)])
            fresh.manifest_page(limit=2, offset=1)
            self.assertEqual(reader.call_count, 2)
        self.assertEqual(fresh.manifest_page(limit=2, offset=100)["requests"], [])

    def test_page_cache_copies_and_external_changes_are_isolated(self):
        page = self.state.manifest_page()
        page["requests"][0]["state"] = "caller mutation"
        self.assertNotEqual(self.state.manifest_page()["requests"][0]["state"], "caller mutation")
        external = self.saved()
        external["state"] = "external process"
        storage.atomic_json(self.state.manifest_path(self.request_id), external)
        self.assertEqual(self.state.manifest_page()["requests"][0]["state"], "external process")

    def test_local_history_uses_paging(self):
        import modeling_cli
        with patch.object(modeling_cli, "State", return_value=self.state), \
                patch.object(self.state, "manifests", side_effect=AssertionError("unbounded history")), \
                patch.object(self.state, "manifest_page", wraps=self.state.manifest_page) as page, \
                patch.object(modeling_cli, "render"):
            code = modeling_cli.main(["history", "--server", self.state.server,
                                      "--limit", "10", "--offset", "5", "--json"])
            self.assertEqual(code, 0)
            page.assert_called_once_with(10, 5)

    def test_legacy_job_lookup_reads_unchanged_index_once(self):
        legacy = dict(self.job)
        legacy.pop("request_id")
        with patch.object(storage, "read_json", wraps=storage.read_json) as reader:
            self.state.remember_job(legacy)
            first = reader.call_count
            self.state.remember_job(legacy)
            self.assertEqual(reader.call_count, first)
        self.assertNotIn("request_id", self.saved()["jobs"][0])

    def test_web_api_non_uuid_request_job_does_not_fail_status(self):
        self.state.remember_job({"id": "api-job-without-local-request", "request_id": "request-00000001",
                                 "status": "completed"})
        self.assertEqual(self.saved()["jobs"][0], self.job)

    def test_non_uuid_api_key_uses_legacy_local_job_lookup(self):
        self.state.remember_job(dict(self.job, request_id="request-00000001", status="completed"))
        saved = self.saved()["jobs"][0]
        self.assertEqual(saved["status"], "completed")
        self.assertEqual(saved["request_id"], "request-00000001")

    def test_legacy_index_discovers_external_new_request(self):
        self.state.remember_job({"id": "unknown", "status": "running"})
        another = storage.State(self.root / "state", self.state.server)
        second_id = str(uuid.uuid4())
        another.recover_manifest(second_id, [{"id": "legacy-two", "status": "running"}])
        self.state.remember_job({"id": "legacy-two", "status": "completed"})
        self.assertEqual(storage.read_json(another.manifest_path(second_id))["jobs"][0]["status"], "completed")

    def test_external_receipt_update_survives_status_change(self):
        manifest = self.saved()
        manifest["downloads"] = [{"job_id": "job-one", "archive_sha256": "retained"}]
        storage.atomic_json(self.state.manifest_path(self.request_id), manifest)
        self.state.remember_job(dict(self.job, status="completed"))
        self.assertEqual(self.saved()["downloads"], manifest["downloads"])

    def test_stale_caller_merges_unrelated_external_fields(self):
        stale = self.state.load_manifest(self.request_id)
        external = copy.deepcopy(stale)
        external["downloads"] = [{"job_id": "job-one", "archive_sha256": "preserved"}]
        external["inputs"] = [{"filename": "original.png", "sha256": "source"}]
        storage.atomic_json(self.state.manifest_path(self.request_id), external)
        stale["collection_summary"] = {"completed": 1}
        self.state.write_manifest(stale)
        saved = self.saved()
        self.assertEqual(saved["downloads"], external["downloads"])
        self.assertEqual(saved["inputs"], external["inputs"])
        self.assertEqual(saved["collection_summary"], {"completed": 1})

    def test_old_mutable_manifest_keeps_its_baseline_after_another_load(self):
        stale = self.state.load_manifest(self.request_id)
        newer = self.state.load_manifest(self.request_id)
        newer["downloads"] = [{"job_id": "job-one", "archive_sha256": "retained"}]
        self.state.write_manifest(newer)
        # This load used to overwrite the baseline belonging to stale as well.
        self.state.load_manifest(self.request_id)
        stale["collection_summary"] = {"completed": 1}
        self.state.write_manifest(stale)
        self.assertEqual(self.saved()["downloads"], newer["downloads"])
        self.assertEqual(self.saved()["collection_summary"], {"completed": 1})

    def test_concurrent_process_writers_preserve_independent_fields(self):
        context = multiprocessing.get_context("spawn")
        ready, results = context.Queue(), context.Queue()
        release = context.Event()
        updates = {"downloads": [{"job_id": "job-one", "archive_sha256": "retained"}],
                   "collection_summary": {"completed": 1}}
        processes = [context.Process(target=concurrent_writer, args=(
            str(self.root / "state"), self.state.server, self.request_id, field,
            value, ready, release, results)) for field, value in updates.items()]
        try:
            for process in processes:
                process.start()
            self.assertEqual({ready.get(timeout=15) for _ in processes}, set(updates))
            release.set()
            self.assertEqual(dict(results.get(timeout=15) for _ in processes),
                             {field: True for field in updates})
            for process in processes:
                process.join(timeout=15)
                self.assertEqual(process.exitcode, 0)
            saved = self.saved()
            for field, value in updates.items():
                self.assertEqual(saved[field], value)
        finally:
            release.set()
            for process in processes:
                if process.pid is not None and process.is_alive():
                    process.terminate()
                    process.join(timeout=5)
            ready.close()
            results.close()

    def test_same_size_in_place_edit_with_restored_mtime_is_detected(self):
        path = self.state.manifest_path(self.request_id)
        self.state.load_manifest(self.request_id)
        info = path.stat()
        original = path.read_bytes()
        modified = original.replace(b'"state": "recovered"', b'"state": "submitted"')
        self.assertNotEqual(original, modified)
        self.assertEqual(len(original), len(modified))
        time.sleep(0.002)
        path.write_bytes(modified)
        os.utime(path, ns=(info.st_atime_ns, info.st_mtime_ns))
        self.assertEqual(path.stat().st_size, info.st_size)
        self.assertEqual(path.stat().st_mtime_ns, info.st_mtime_ns)
        self.assertEqual(self.state.load_manifest(self.request_id)["state"], "submitted")

    def test_history_cache_notices_external_update(self):
        self.state.manifests()
        external = self.saved()
        external["state"] = "externally changed"
        storage.atomic_json(self.state.manifest_path(self.request_id), external)
        self.assertEqual(self.state.manifests()[0]["state"], "externally changed")

    def test_input_source_hash_schema_and_recovery_are_preserved(self):
        image = self.root / "source.png"
        image.write_bytes(b"source image fixture")
        request = str(uuid.uuid4())
        prepared = self.state.prepare(request, [image], {"model": "trellis2"})
        copied = self.state.manifest_path(request).parent / prepared["inputs"][0]["local_path"]
        self.assertEqual(copied.read_bytes(), image.read_bytes())
        self.assertEqual(prepared["inputs"][0]["sha256"], storage.sha256_file(image))
        self.assertEqual(prepared["schema_version"], 1)
        recovered = self.state.recover_manifest(request, [{"id": "new", "status": "queued"}])
        self.assertEqual(recovered["inputs"], prepared["inputs"])
        self.assertEqual(recovered["submitted_options"], prepared["submitted_options"])
        self.assertEqual(self.state.prepare(request, [], {}), recovered)

    def test_corrupt_manifest_has_structured_local_error(self):
        self.state.manifest_path(self.request_id).write_text("[]", encoding="utf-8")
        with self.assertRaises(ModelingError) as caught:
            self.state.remember_job(self.job)
        self.assertEqual(caught.exception.code, "LOCAL_IO_ERROR")

    def test_atomic_failure_preserves_original_document(self):
        original = self.state.manifest_path(self.request_id).read_bytes()
        with patch.object(storage.os, "replace", side_effect=OSError("simulated disk error")):
            with self.assertRaises(ModelingError) as caught:
                self.state.remember_job(dict(self.job, message="Changed"))
        self.assertEqual(caught.exception.code, "LOCAL_IO_ERROR")
        self.assertEqual(self.state.manifest_path(self.request_id).read_bytes(), original)
        self.assertFalse(list(self.state.manifest_path(self.request_id).parent.glob(".writing-*")))

    @unittest.skipIf(os.name == "nt", "POSIX mode verification runs on the A100 host")
    def test_private_permissions(self):
        self.assertEqual(self.state.directory.stat().st_mode & 0o777, 0o700)
        self.assertEqual(self.state.manifest_path(self.request_id).stat().st_mode & 0o777, 0o600)


class CookieTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.path = Path(self.temporary.name) / "cookies.lwp"
        self.jar = http.cookiejar.LWPCookieJar(str(self.path))
        self.jar.set_cookie(cookie())

    def test_unchanged_responses_write_nothing(self):
        signature = storage.save_cookie_jar(self.jar, self.path, None)
        with patch.object(self.jar, "save", wraps=self.jar.save) as save, \
                patch.object(storage.os, "fsync", wraps=os.fsync) as sync:
            for _ in range(100):
                signature = storage.save_cookie_jar(self.jar, self.path, signature)
            self.assertEqual((save.call_count, sync.call_count), (0, 0))

    def test_set_cookie_and_attributes_write_new_session(self):
        signature = storage.save_cookie_jar(self.jar, self.path, None)
        for changed in (cookie("changed"), cookie("changed", int(time.time()) + 100)):
            self.jar.set_cookie(changed)
            with patch.object(self.jar, "save", wraps=self.jar.save) as save:
                updated = storage.save_cookie_jar(self.jar, self.path, signature)
                self.assertNotEqual(updated, signature)
                self.assertEqual(save.call_count, 1)
                signature = updated
        loaded = http.cookiejar.LWPCookieJar(str(self.path))
        loaded.load(ignore_discard=True, ignore_expires=False)
        self.assertEqual([(item.name, item.value, item.expires) for item in loaded],
                         [(item.name, item.value, item.expires) for item in self.jar])

    def test_expired_cookie_removal_is_persisted(self):
        signature = storage.save_cookie_jar(self.jar, self.path, None)
        self.jar.set_cookie(cookie(expires=int(time.time()) - 1))
        updated = storage.save_cookie_jar(self.jar, self.path, signature)
        self.assertNotEqual(updated, signature)
        self.assertEqual(list(self.jar), [])
        loaded = http.cookiejar.LWPCookieJar(str(self.path))
        loaded.load(ignore_discard=True, ignore_expires=False)
        self.assertEqual(list(loaded), [])

    def test_fingerprint_is_opaque_and_json_serializable(self):
        signature = storage.cookie_signature(self.jar)
        self.assertNotIn("example", signature)
        self.assertEqual(len(signature), 64)
        self.assertEqual(json.loads(json.dumps(signature)), signature)

    def test_failure_is_structured_and_preserves_old_cookie_file(self):
        signature = storage.save_cookie_jar(self.jar, self.path, None)
        before = self.path.read_bytes()
        self.jar.set_cookie(cookie("changed"))
        with patch.object(storage.os, "replace", side_effect=OSError("simulated disk error")):
            with self.assertRaises(ModelingError) as caught:
                storage.save_cookie_jar(self.jar, self.path, signature)
        self.assertEqual(caught.exception.code, "LOCAL_IO_ERROR")
        self.assertEqual(before, self.path.read_bytes())

    @unittest.skipIf(os.name == "nt", "POSIX mode verification runs on the A100 host")
    def test_private_cookie_permissions(self):
        storage.save_cookie_jar(self.jar, self.path, None)
        self.assertEqual(self.path.stat().st_mode & 0o777, 0o600)


if __name__ == "__main__":
    unittest.main()
