"""Adversarial, stdlib-only tests for derivative packaging (no Unity needed)."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import stat
import struct
import tempfile
import unittest
from unittest import mock
import warnings
import zipfile

import package_windows_candidate as pack


def pe_x64():
    data = bytearray(80)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 64)
    data[64:70] = b"PE\x00\x00\x64\x86"
    return bytes(data)


def fixture_files():
    files = {name: (name + " retained payload\n").encode() for name in pack.REQUIRED_FILES}
    for name in pack.NATIVE_PE_FILES:
        files[name] = pe_x64()
    names = ["Assembly-CSharp.dll", "Unity.Collections.Tests.CoreCLR.PrivateJobNested.dll"]
    files[pack.DATA + "/Managed/" + names[1]] = b"referenced runtime dependency"
    files[pack.DATA + "/ScriptingAssemblies.json"] = json.dumps({"names": names, "types": [16, 16]}).encode()
    files[pack.DATA + "/RuntimeInitializeOnLoads.json"] = b"{}"
    files[pack.DATA + "/boot.config"] = b"wait-for-native-debugger=0\n"
    files[pack.DATA + "/app.info"] = b"Happy Toy\nHappy Toy V2 - Development"
    return files


class PackagingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.source = self.root / "original.zip"
        self.output = self.root / "candidate.zip"
        self.manifest = self.output.with_suffix(".manifest.json")

    def tearDown(self):
        self.temp.cleanup()

    def write_zip(self, files=None, extras=(), *, path=None, reverse=False, date=(2020, 1, 1, 0, 0, 0)):
        items = list((fixture_files() if files is None else files).items()) + list(extras)
        if reverse:
            items.reverse()
        with warnings.catch_warnings(), zipfile.ZipFile(path or self.source, "w") as archive:
            warnings.simplefilter("ignore", UserWarning)  # Intentional duplicate-name fixture.
            for name, payload in items:
                info = name if isinstance(name, zipfile.ZipInfo) else zipfile.ZipInfo(name, date)
                archive.writestr(info, payload)
        return path or self.source

    def run_package(self, **kwargs):
        return pack.package_candidate(self.source, self.output, repository_root=self.repo, **kwargs)

    def assert_rejected(self, pattern=None):
        before = self.source.read_bytes()
        with self.assertRaisesRegex(pack.PackagingError, pattern or ".+"):
            self.run_package()
        self.assertEqual(self.source.read_bytes(), before)
        self.assertFalse(self.output.exists())
        self.assertFalse(self.manifest.exists())
        self.assertFalse(list(self.root.glob(".happytoy-*")))

    def test_valid_package_preserves_payloads_and_original(self):
        files = fixture_files()
        files["custom-runtime-file.bin"] = b"Do not remove unknown runtime dependencies."
        self.write_zip(files)
        before = self.source.read_bytes()
        report = self.run_package(expected_source_sha256=hashlib.sha256(before).hexdigest())
        self.assertEqual(report["status"], "NOT_RELEASE_READY")
        self.assertEqual(self.source.read_bytes(), before)
        self.assertEqual(json.loads(self.manifest.read_text()), report)
        self.assertEqual(pack.sha256_file(self.output), report["derivative"]["sha256"])
        self.assertTrue(report["source"]["unchanged_after_packaging"])
        self.assertTrue(report["source"]["all_entry_crc_validated"])
        self.assertTrue(report["validation"]["development_name_marker_present"])
        self.assertEqual(report["validation"]["preserved_test_named_assemblies"],
                         ["Unity.Collections.Tests.CoreCLR.PrivateJobNested.dll"])
        with zipfile.ZipFile(self.output) as archive:
            self.assertEqual(archive.namelist(), sorted(files))
            for info in archive.infolist():
                self.assertEqual(archive.read(info), files[info.filename])
                self.assertEqual(info.date_time, pack.FIXED_TIME)
                self.assertEqual(stat.S_IMODE(info.external_attr >> 16), 0o644)
                self.assertEqual(info.extra, b"")
                self.assertEqual(info.comment, b"")

    def test_exclusions_are_explicit_and_hashed(self):
        extras = {
            "HappyToyV2_BackUpThisFolder_ButDontShipItWithYourGame/source.txt": b"source backup",
            "HappyToyV2_BurstDebugInformation_DoNotShip/disassembly.txt": b"assembly listing",
            "UnityPlayer.PDB": b"native symbols", pack.DATA + "/Managed/a.dll.mdb": b"mono symbols",
            "native.dbg": b"debug symbols", "Player.log": b"runtime log",
            "Player-prev.log": b"previous log", "output_log.txt": b"old log",
            "BuildReport.json": b"{}", "TestResults.xml": b"<test-results/>",
            "Empty/": b"", pack.DATA + "/StreamingAssets/story.txt": b"keep text",
            pack.DATA + "/StreamingAssets/gameplay.log": b"keep data log",
        }
        self.write_zip(extras=extras.items())
        report = self.run_package()
        self.assertEqual(report["excluded_file_count"], 10)
        records = {item["path"]: item for item in report["files"]}
        for name, payload in extras.items():
            self.assertEqual(records[name]["sha256"], hashlib.sha256(payload).hexdigest())
            if "StreamingAssets" in name:
                self.assertEqual(records[name]["action"], "retain")
            else:
                self.assertEqual(records[name]["action"], "exclude")
                self.assertIn(records[name]["reason"], pack.EXCLUSION_POLICY)
        with zipfile.ZipFile(self.output) as archive:
            self.assertNotIn("Empty/", archive.namelist())

    def test_reproducible_despite_input_order_timestamps_and_comments(self):
        self.write_zip()
        self.run_package()
        second_source, second_output = self.root / "different.zip", self.root / "second.zip"
        self.write_zip(path=second_source, reverse=True, date=(2025, 12, 2, 1, 2, 4))
        with zipfile.ZipFile(second_source, "a") as archive:
            archive.comment = b"input-only archive metadata"
        pack.package_candidate(second_source, second_output, repository_root=self.repo)
        self.assertEqual(self.output.read_bytes(), second_output.read_bytes())

    def test_reject_unsafe_windows_paths(self):
        for name in ("../escape", "/absolute", "C:/drive", "foo\\bar", "foo/../bar", "./foo",
                     "foo//bar", "foo/", "NUL.txt", "dir/COM1", "LPT².txt", "aux.file",
                     "CON .txt", "CONIN$", "conout$.log",
                     "name. ", "name.", "name ", "file:stream", "foo?bar", "x\x01y", "x\x7fy"):
            with self.subTest(name=name):
                self.write_zip(extras=[(name, b"unsafe")])
                self.assert_rejected()

    def test_reject_nul_truncated_zip_name(self):
        info = zipfile.ZipInfo("foo\x00bar")
        with self.assertRaisesRegex(pack.PackagingError, "Unsafe"):
            pack.safe_name(info)

    def test_reject_duplicate_case_and_unicode_collisions(self):
        for names in (("A.bin", "A.bin"), ("A.bin", "a.bin"), ("é.bin", "e\u0301.bin"),
                      ("Foo/a.bin", "foo/b.bin")):
            with self.subTest(names=names):
                self.write_zip(extras=[(name, b"duplicate") for name in names])
                self.assert_rejected("collid|Duplicate|Ambiguous")

    def test_reject_file_directory_conflicts(self):
        for names in (("folder", "folder/child"), ("folder", "folder/")):
            with self.subTest(names=names):
                self.write_zip(extras=[(name, b"" if name.endswith("/") else b"x") for name in names])
                self.assert_rejected("conflict|Duplicate")

    def test_accept_explicit_parent_directories(self):
        self.write_zip(extras=[(pack.DATA + "/", b""), ("MonoBleedingEdge/", b"")])
        report = self.run_package()
        self.assertEqual(report["derivative"]["entries"], len(fixture_files()))

    def test_reject_symlink_fifo_device_or_inconsistent_type(self):
        for kind in (stat.S_IFLNK, stat.S_IFIFO, stat.S_IFCHR, stat.S_IFBLK, stat.S_IFSOCK, stat.S_IFDIR):
            with self.subTest(kind=kind):
                info = zipfile.ZipInfo("special")
                info.create_system = 3
                info.external_attr = (kind | 0o777) << 16
                self.write_zip(extras=[(info, b"target")])
                self.assert_rejected("Symlink/special")

    def test_reject_windows_directory_attribute_on_file(self):
        info = zipfile.ZipInfo("special")
        info.external_attr = 0x10
        self.write_zip(extras=[(info, b"not a directory")])
        self.assert_rejected("directory attribute")

    def test_reject_encrypted_entry(self):
        info = zipfile.ZipInfo("encrypted")
        info.flag_bits = 1
        with self.assertRaisesRegex(pack.PackagingError, "Encrypted"):
            pack.safe_name(info)

    def test_reject_unsupported_compression(self):
        info = zipfile.ZipInfo("compressed")
        info.compress_type = zipfile.ZIP_BZIP2
        self.write_zip(extras=[(info, b"compressed")])
        self.assert_rejected("compression")

    def test_reject_entry_count_limit(self):
        self.write_zip()
        with mock.patch.object(pack, "MAX_ENTRIES", 1):
            self.assert_rejected("count")

    def test_reject_uncompressed_size_limits(self):
        self.write_zip()
        for limit in ("MAX_ENTRY_BYTES", "MAX_TOTAL_BYTES"):
            with self.subTest(limit=limit), mock.patch.object(pack, limit, 1):
                self.assert_rejected("safety limit")

    def test_reject_compression_ratio_limit(self):
        info = zipfile.ZipInfo("huge.txt")
        info.compress_type = zipfile.ZIP_DEFLATED
        self.write_zip(extras=[(info, b"a" * 10000)])
        with mock.patch.object(pack, "MAX_COMPRESSION_RATIO", 10):
            self.assert_rejected("safety limit")

    def test_reject_empty_and_non_zip(self):
        self.write_zip(files={})
        self.assert_rejected("count")
        self.source.write_bytes(b"not a zip")
        self.assert_rejected("not a zip")

    def test_reject_missing_or_empty_required_files(self):
        for name in pack.REQUIRED_FILES:
            for empty in (False, True):
                with self.subTest(name=name, empty=empty):
                    files = fixture_files()
                    if empty:
                        files[name] = b""
                    else:
                        del files[name]
                    self.write_zip(files)
                    self.assert_rejected("Missing/empty")

    def test_reject_unexpected_extra_compiled_scene(self):
        self.write_zip(extras=[(pack.DATA + "/level1", b"second scene")])
        self.assert_rejected("single compiled scene")

    def test_reject_malformed_scripting_metadata(self):
        for payload in (b"not json", b"[]", b'{}', b'{"names":[],"types":[]}',
                        b'{"names":["Assembly-CSharp.dll"],"types":[]}',
                        b'{"names":["../a.dll"],"types":[16]}',
                        b'{"names":["Assembly-CSharp.dll","Assembly-CSharp.dll"],"types":[16,16]}',
                        b'{"names":[7],"types":[16]}'):
            with self.subTest(payload=payload):
                files = fixture_files()
                files[pack.DATA + "/ScriptingAssemblies.json"] = payload
                self.write_zip(files)
                self.assert_rejected("metadata")

    def test_reject_missing_and_empty_referenced_assembly(self):
        for empty in (False, True):
            with self.subTest(empty=empty):
                files = fixture_files()
                name = pack.DATA + "/Managed/Unity.Collections.Tests.CoreCLR.PrivateJobNested.dll"
                if empty:
                    files[name] = b""
                else:
                    del files[name]
                self.write_zip(files)
                self.assert_rejected("Referenced assembly")

    def test_reject_bad_runtime_initializer_json_and_text_encoding(self):
        for name, payload in (("RuntimeInitializeOnLoads.json", b'"string"'),
                              ("RuntimeInitializeOnLoads.json", b"not json"),
                              ("boot.config", b"\xff"), ("app.info", b"\xff")):
            with self.subTest(name=name):
                files = fixture_files()
                files[pack.DATA + "/" + name] = payload
                self.write_zip(files)
                self.assert_rejected("metadata")

    def test_reject_oversized_metadata(self):
        self.write_zip()
        with mock.patch.object(pack, "MAX_METADATA_BYTES", 4):
            self.assert_rejected("Metadata exceeds")

    def test_reject_invalid_native_pe_headers(self):
        variants = [b"not PE", b"X" * 80]
        for offset in (0, 81, pack.MAX_METADATA_BYTES + 1):
            data = bytearray(pe_x64())
            struct.pack_into("<I", data, 0x3C, offset)
            variants.append(data)
        x86 = bytearray(pe_x64())
        x86[68:70] = b"\x4c\x01"
        variants.append(x86)
        for payload in variants:
            with self.subTest(payload=payload):
                files = fixture_files()
                files[pack.PLAYER + ".exe"] = payload
                self.write_zip(files)
                self.assert_rejected("PE header")

    def corrupt_stored_entry(self, name):
        with zipfile.ZipFile(self.source) as archive:
            info = archive.getinfo(name)
            offset = info.header_offset
        with self.source.open("r+b") as stream:
            stream.seek(offset + 26)
            name_length, extra_length = struct.unpack("<HH", stream.read(4))
            stream.seek(offset + 30 + name_length + extra_length)
            first = stream.read(1)
            stream.seek(-1, 1)
            stream.write(bytes([first[0] ^ 0xFF]))

    def test_crc_failure_in_retained_unknown_file_is_rejected(self):
        self.write_zip(extras=[("unchanged.bin", b"must be checked")])
        self.corrupt_stored_entry("unchanged.bin")
        self.assert_rejected("CRC")

    def test_crc_failure_in_excluded_file_is_still_rejected(self):
        self.write_zip(extras=[("debug.pdb", b"must also be checked")])
        self.corrupt_stored_entry("debug.pdb")
        self.assert_rejected("CRC")

    def test_expected_source_hash_failure_creates_nothing(self):
        self.write_zip()
        for digest in ("0" * 64, "not a digest"):
            with self.subTest(digest=digest), self.assertRaises(pack.PackagingError):
                self.run_package(expected_source_sha256=digest)
        self.assertFalse(self.output.exists())
        self.assertFalse(self.manifest.exists())

    def test_derivative_payload_mismatch_is_rejected(self):
        self.write_zip(extras=[("z-extra.bin", b"payload to corrupt")])
        real_copy = pack.copy_and_hash

        def corrupt_copy(source, destination=None):
            if destination is not None and source.name == "z-extra.bin":
                measured = real_copy(source)
                destination.write(b"corrupted payload")
                return measured
            return real_copy(source, destination)

        with mock.patch.object(pack, "copy_and_hash", side_effect=corrupt_copy):
            self.assert_rejected("Derivative payload mismatch")

    def test_existing_zip_or_manifest_never_overwritten(self):
        self.write_zip()
        for target in (self.output, self.manifest):
            with self.subTest(target=target):
                target.write_bytes(b"keep existing")
                with self.assertRaisesRegex(pack.PackagingError, "already exists"):
                    self.run_package()
                self.assertEqual(target.read_bytes(), b"keep existing")
                target.unlink()

    def test_reject_broken_destination_symlinks(self):
        self.write_zip()
        for target in (self.output, self.manifest):
            with self.subTest(target=target):
                target.symlink_to(self.root / "absent")
                with self.assertRaisesRegex(pack.PackagingError, "already exists"):
                    self.run_package()
                self.assertTrue(target.is_symlink())
                target.unlink()

    def test_refuse_in_place_or_repository_output(self):
        self.write_zip()
        self.output = self.source
        with self.assertRaisesRegex(pack.PackagingError, "already exists"):
            self.run_package()
        for target in (self.repo / "candidate.zip", self.repo / "nested/../candidate.zip"):
            self.output = target
            with self.assertRaisesRegex(pack.PackagingError, "outside repository"):
                self.run_package()

    def test_reject_symlink_parent_into_repository(self):
        self.write_zip()
        linked = self.root / "alias"
        linked.symlink_to(self.repo, target_is_directory=True)
        self.output = linked / "candidate.zip"
        with self.assertRaisesRegex(pack.PackagingError, "outside repository"):
            self.run_package()

    def test_require_existing_output_directory_and_zip_suffix(self):
        self.write_zip()
        for target in (self.root / "missing/candidate.zip", self.root / "candidate.dat"):
            self.output = target
            with self.assertRaises(pack.PackagingError):
                self.run_package()

    def test_source_hash_change_during_packaging_rejects_publication(self):
        self.write_zip()
        real_hash = pack.sha256_file
        stream_hash_calls = 0

        def changing_hash(value):
            nonlocal stream_hash_calls
            if hasattr(value, "read"):
                stream_hash_calls += 1
                if stream_hash_calls == 2:
                    return "0" * 64
            return real_hash(value)

        with mock.patch.object(pack, "sha256_file", side_effect=changing_hash):
            self.assert_rejected("Source changed")

    def test_no_clobber_publication_race_preserves_competing_file(self):
        self.write_zip()
        real_link = pack.os.link

        def raced_link(source, target):
            Path(target).write_bytes(b"another writer won")
            return real_link(source, target)

        with mock.patch.object(pack.os, "link", side_effect=raced_link), self.assertRaises(pack.PackagingError):
            self.run_package()
        self.assertEqual(self.output.read_bytes(), b"another writer won")
        self.assertFalse(self.manifest.exists())
        self.assertFalse(list(self.root.glob(".happytoy-*")))

    def test_manifest_publish_failure_rolls_back_only_created_zip(self):
        self.write_zip()
        real_link = pack.os.link

        def raced_manifest(source, target):
            if Path(target) == self.manifest:
                self.manifest.write_bytes(b"keep competing manifest")
            return real_link(source, target)

        with mock.patch.object(pack.os, "link", side_effect=raced_manifest), self.assertRaises(pack.PackagingError):
            self.run_package()
        self.assertFalse(self.output.exists())
        self.assertEqual(self.manifest.read_bytes(), b"keep competing manifest")
        self.assertFalse(list(self.root.glob(".happytoy-*")))

    def test_cli_reports_failures_without_success_output(self):
        self.source.write_bytes(b"broken archive")
        stderr, stdout = io.StringIO(), io.StringIO()
        with contextlib.redirect_stderr(stderr), contextlib.redirect_stdout(stdout):
            code = pack.main([str(self.source), str(self.output)])
        self.assertEqual(code, 1)
        self.assertIn("ERROR:", stderr.getvalue())
        self.assertEqual(stdout.getvalue(), "")


if __name__ == "__main__":
    unittest.main()
