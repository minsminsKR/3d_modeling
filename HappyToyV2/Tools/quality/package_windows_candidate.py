#!/usr/bin/env python3
"""Create a conservative, verified Windows candidate from an existing Mono ZIP.

Python 3.10+, standard library only. No Unity invocation, extraction, asset edits,
network, release publication, or build-mode conversion. The original is read-only.
Example (output directory must already exist outside the repository):

  python Tools/quality/package_windows_candidate.py INPUT.zip OUTPUT.zip \
      --expected-source-sha256 SOURCE_SHA256

OUTPUT.manifest.json records every retained/excluded entry and its SHA-256/CRC.
Existing destinations (including symlinks) are never overwritten. ZIP entries are
sorted and use fixed metadata and DEFLATE level 9; identical content and the same
Python/zlib toolchain produce identical ZIP bytes. The manifest names that toolchain.
An external reviewer must still clear licensing and smoke-test the target Windows
player. Removing symbols/backup reports does NOT disable development build behavior.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import struct
import sys
import tempfile
import unicodedata
import zipfile
import zlib


POLICY_VERSION = 1
PLAYER = "HappyToyV2"
DATA = PLAYER + "_Data"
FIXED_TIME = (1980, 1, 1, 0, 0, 0)
CHUNK_SIZE = 1024 * 1024
MAX_ENTRIES = 10000
MAX_TOTAL_BYTES = 2 * 1024**3
MAX_ENTRY_BYTES = 1024**3
MAX_COMPRESSION_RATIO = 2000
MAX_METADATA_BYTES = 2 * 1024**2
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]

REQUIRED_FILES = (
    PLAYER + ".exe", "UnityPlayer.dll", "UnityCrashHandler64.exe",
    "D3D12/D3D12Core.dll", "dstorage.dll", "dstoragecore.dll",
    "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
    "MonoBleedingEdge/EmbedRuntime/MonoPosixHelper.dll",
    "MonoBleedingEdge/etc/mono/config",
    DATA + "/Managed/Assembly-CSharp.dll", DATA + "/Managed/mscorlib.dll",
    DATA + "/Managed/netstandard.dll", DATA + "/Plugins/x86_64/lib_burst_generated.dll",
    DATA + "/Resources/unity default resources", DATA + "/Resources/unity_builtin_extra",
    DATA + "/RuntimeInitializeOnLoads.json", DATA + "/ScriptingAssemblies.json",
    DATA + "/app.info", DATA + "/boot.config", DATA + "/globalgamemanagers",
    DATA + "/globalgamemanagers.assets", DATA + "/globalgamemanagers.assets.resS",
    DATA + "/level0", DATA + "/resources.assets", DATA + "/resources.assets.resS",
    DATA + "/sharedassets0.assets", DATA + "/sharedassets0.assets.resS",
)
NATIVE_PE_FILES = (
    PLAYER + ".exe", "UnityPlayer.dll", "UnityCrashHandler64.exe",
    "D3D12/D3D12Core.dll", "dstorage.dll", "dstoragecore.dll",
    "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
    "MonoBleedingEdge/EmbedRuntime/MonoPosixHelper.dll",
    DATA + "/Plugins/x86_64/lib_burst_generated.dll",
)
EXCLUSION_POLICY = {
    "unity-backup-directory": "Top-level *_BackUpThisFolder_ButDontShipItWithYourGame tree",
    "burst-debug-directory": "Top-level *_BurstDebugInformation_DoNotShip tree",
    "debug-symbol": "Files with .pdb, .mdb, or .dbg suffix (case-insensitive)",
    "development-log-report": "Root Player.log, Player-prev.log, output_log.txt, BuildReport.json, or TestResults.xml",
    "directory-entry": "Empty directory records; file parent directories remain implicit",
}
CAVEATS = [
    "NOT RELEASE READY: target Windows runtime/visual/audio/survival smoke tests have not been run by this tool.",
    "Licensing and redistribution clearance remain open; this tool does not grant or verify rights.",
    "Repackaging never changes development-build flags, debug instrumentation, audit code, app identity, or compiled assets.",
    "All non-excluded file payloads are preserved byte-for-byte, including referenced assemblies whose names contain Tests.",
    "Presence, CRC, hashes and native PE x64 headers are structural evidence, not proof that the game runs correctly.",
]


class PackagingError(ValueError):
    """A fail-closed packaging validation error."""


def sha256_file(path_or_stream):
    if hasattr(path_or_stream, "read"):
        stream = path_or_stream
        stream.seek(0)
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(CHUNK_SIZE), b""):
            digest.update(block)
        return digest.hexdigest()
    with Path(path_or_stream).open("rb") as stream:
        return sha256_file(stream)


def safe_name(info):
    """Reject unsafe or ambiguous names under Windows extraction semantics."""
    name = info.filename
    if info.orig_filename != name or not name or "\\" in name or name.startswith("/"):
        raise PackagingError(f"Unsafe ZIP path: {name!r}")
    parts = name[:-1].split("/") if name.endswith("/") else name.split("/")
    for part in parts:
        device_name = part.split(".", 1)[0].rstrip(" .")
        if (not part or part in (".", "..") or part.endswith((".", " "))
                or any(ord(c) < 32 or ord(c) == 127 or c in '<>:"|?*' for c in part)
                or re.fullmatch(r"(?i)(con|prn|aux|nul|conin\$|conout\$|com[1-9¹²³]|lpt[1-9¹²³])", device_name)):
            raise PackagingError(f"Unsafe Windows ZIP path: {name!r}")
    mode = info.external_attr >> 16
    kind = stat.S_IFMT(mode)
    expected_kind = stat.S_IFDIR if info.is_dir() else stat.S_IFREG
    if kind not in (0, expected_kind):
        raise PackagingError(f"Symlink/special/inconsistent entry prohibited: {name!r}")
    if info.external_attr & 0x10 and not info.is_dir():
        raise PackagingError(f"Inconsistent directory attribute: {name!r}")
    if info.flag_bits & 1:
        raise PackagingError(f"Encrypted ZIP entry prohibited: {name!r}")
    if info.compress_type not in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED):
        raise PackagingError(f"Unsupported compression method: {name!r}")
    if info.is_dir() and info.file_size:
        raise PackagingError(f"Directory has a payload: {name!r}")
    return "/".join(parts)


def exclusion_reason(name, is_directory=False):
    top = name.split("/", 1)[0].casefold()
    for suffix, reason in (
        ("_backupthisfolder_butdontshipitwithyourgame", "unity-backup-directory"),
        ("_burstdebuginformation_donotship", "burst-debug-directory"),
    ):
        if top.endswith(suffix) or top == suffix[1:]:
            return reason
    if not is_directory and Path(name).suffix.casefold() in (".pdb", ".mdb", ".dbg"):
        return "debug-symbol"
    if "/" not in name and name.casefold() in {
        "player.log", "player-prev.log", "output_log.txt", "buildreport.json", "testresults.xml"
    }:
        return "development-log-report"
    if is_directory:
        return "directory-entry"
    return None


def inspect_entries(archive):
    entries = archive.infolist()
    if not entries or len(entries) > MAX_ENTRIES:
        raise PackagingError("ZIP entry count is empty or exceeds the safety limit")
    names, spellings = {}, {}
    total = 0
    for info in entries:
        name = safe_name(info)
        key = unicodedata.normalize("NFC", name).casefold()
        if key in names:
            raise PackagingError(f"Duplicate/case/Unicode-colliding ZIP path: {info.filename!r}")
        names[key] = info
        # Also reject aliases for implicit directories, such as A/a.dll + a/b.dll.
        parts = name.split("/")
        for index in range(1, len(parts) + 1):
            spelling = "/".join(parts[:index])
            normalized = unicodedata.normalize("NFC", spelling).casefold()
            if normalized in spellings and spellings[normalized] != spelling:
                raise PackagingError(f"Ambiguous parent-directory spelling: {name!r}")
            spellings[normalized] = spelling
        total += info.file_size
        if (info.file_size > MAX_ENTRY_BYTES or total > MAX_TOTAL_BYTES
                or info.file_size > max(info.compress_size, 1) * MAX_COMPRESSION_RATIO):
            raise PackagingError(f"ZIP expansion safety limit exceeded: {name!r}")
    for key in names:
        parent = key.rpartition("/")[0]
        while parent:
            if parent in names and not names[parent].is_dir():
                raise PackagingError(f"File/directory path conflict: {names[key].filename!r}")
            parent = parent.rpartition("/")[0]
    return sorted(entries, key=lambda item: item.filename)


def read_small(archive, name):
    info = archive.getinfo(name)
    if info.file_size > MAX_METADATA_BYTES:
        raise PackagingError(f"Metadata exceeds safety limit: {name}")
    return archive.read(info)


def verify_player(archive, entries):
    retained = {info.filename: info for info in entries
                if exclusion_reason(info.filename, info.is_dir()) is None}
    missing = [name for name in REQUIRED_FILES if name not in retained or not retained[name].file_size]
    if missing:
        raise PackagingError("Missing/empty required player files: " + ", ".join(missing))
    scenes = sorted(name for name in retained if re.fullmatch(re.escape(DATA) + r"/level\d+", name))
    if scenes != [DATA + "/level0"]:
        raise PackagingError("Expected exactly the existing single compiled scene level0")
    try:
        scripting = json.loads(read_small(archive, DATA + "/ScriptingAssemblies.json"))
        runtime_initializers = json.loads(read_small(archive, DATA + "/RuntimeInitializeOnLoads.json"))
        names = scripting["names"]
        types = scripting["types"]
        if (not isinstance(names, list) or not names or not isinstance(types, list)
                or len(names) != len(types) or not all(isinstance(t, int) for t in types)):
            raise ValueError("Malformed names/types assembly lists")
        if not isinstance(runtime_initializers, (dict, list)):
            raise ValueError("Malformed runtime initializer metadata")
        seen = set()
        for name in names:
            if (not isinstance(name, str) or not name.lower().endswith(".dll")
                    or "/" in name or "\\" in name or name.casefold() in seen):
                raise ValueError("Malformed or duplicate assembly name")
            seen.add(name.casefold())
            if DATA + "/Managed/" + name not in retained:
                raise ValueError(f"Referenced assembly missing after exclusions: {name}")
            if not retained[DATA + "/Managed/" + name].file_size:
                raise ValueError(f"Referenced assembly is empty: {name}")
        boot_config = read_small(archive, DATA + "/boot.config").decode("utf-8")
        app_info = read_small(archive, DATA + "/app.info").decode("utf-8")
    except (ValueError, KeyError, TypeError, UnicodeError) as error:
        raise PackagingError(f"Invalid player metadata: {error}") from error
    for name in NATIVE_PE_FILES:
        with archive.open(name) as stream:
            header = stream.read(64)
            if len(header) < 64 or header[:2] != b"MZ":
                raise PackagingError(f"Missing DOS/PE header: {name}")
            offset = struct.unpack_from("<I", header, 0x3C)[0]
            if offset < 64 or offset > MAX_METADATA_BYTES or offset + 6 > retained[name].file_size:
                raise PackagingError(f"Invalid PE header offset: {name}")
            stream.read(offset - 64)
            if stream.read(6) != b"PE\x00\x00\x64\x86":
                raise PackagingError(f"Expected native Windows x64 PE header: {name}")
    return {
        "required_files": list(REQUIRED_FILES), "required_files_present_nonempty": True,
        "native_pe_x64_headers": list(NATIVE_PE_FILES), "compiled_scene_files": scenes,
        "referenced_managed_assemblies_present": len(names),
        "preserved_test_named_assemblies": [name for name in names if "test" in name.casefold()],
        "boot_config": boot_config, "app_info": app_info,
        "development_name_marker_present": "development" in app_info.casefold(),
        "development_build_flag": "NOT DETERMINED; inspect build settings/log and rebuild if necessary",
    }


def fixed_info(name):
    info = zipfile.ZipInfo(name, FIXED_TIME)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.create_system = 3
    info.external_attr = (stat.S_IFREG | 0o644) << 16
    return info


def copy_and_hash(source, destination=None):
    digest, crc, size = hashlib.sha256(), 0, 0
    for block in iter(lambda: source.read(CHUNK_SIZE), b""):
        size += len(block)
        if size > MAX_ENTRY_BYTES:
            raise PackagingError("Entry exceeds streamed size safety limit")
        digest.update(block)
        crc = zlib.crc32(block, crc)
        if destination is not None:
            destination.write(block)
    return {"sha256": digest.hexdigest(), "crc32": f"{crc:08x}", "bytes": size}


def validate_destinations(source, output, manifest, repository_root):
    if source.suffix.casefold() != ".zip" or not source.is_file():
        raise PackagingError("Input must be an existing ZIP file")
    if output.suffix.casefold() != ".zip":
        raise PackagingError("Output must have a .zip suffix")
    if manifest == output:
        raise PackagingError("ZIP and manifest destinations must be different")
    for target in (output, manifest):
        # lexists also detects dangling symlinks, before resolving aliases.
        if os.path.lexists(target):
            raise PackagingError(f"Destination already exists; will not overwrite: {target}")
        resolved = target.resolve()
        if resolved == source or resolved.is_relative_to(repository_root):
            raise PackagingError(f"Output must be separate from input and outside repository: {target}")
        if not target.parent.is_dir():
            raise PackagingError(f"Output directory must already exist: {target.parent}")


def package_candidate(source, output, *, expected_source_sha256=None, repository_root=REPOSITORY_ROOT):
    source, output = Path(source).resolve(), Path(output).absolute()
    manifest_path = output.with_suffix(".manifest.json")
    repository_root = Path(repository_root).resolve()
    validate_destinations(source, output, manifest_path, repository_root)
    if expected_source_sha256 is not None and not re.fullmatch(r"[0-9a-fA-F]{64}", expected_source_sha256):
        raise PackagingError("Expected source SHA-256 must contain exactly 64 hexadecimal digits")
    temporary_paths, published_paths = [], []
    try:
        with source.open("rb") as source_stream:
            original_stat = os.fstat(source_stream.fileno())
            source_sha256 = sha256_file(source_stream)
            if expected_source_sha256 is not None and source_sha256 != expected_source_sha256.lower():
                raise PackagingError("Source SHA-256 does not match the explicitly expected artifact")
            with zipfile.ZipFile(source_stream) as archive:
                entries = inspect_entries(archive)
                player_checks = verify_player(archive, entries)
                fd, temp_name = tempfile.mkstemp(prefix=".happytoy-candidate-", suffix=".zip", dir=output.parent)
                temporary_paths.append(Path(temp_name))
                records = []
                with os.fdopen(fd, "wb") as target_stream, zipfile.ZipFile(
                    target_stream, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9
                ) as target:
                    for info in entries:
                        reason = exclusion_reason(info.filename, info.is_dir())
                        with archive.open(info) as entry_stream:
                            if reason:
                                payload = copy_and_hash(entry_stream)
                            else:
                                # Explicit ZipInfo needs its own level; archive.open otherwise uses zlib default.
                                destination_info = fixed_info(info.filename)
                                destination_info._compresslevel = 9
                                with target.open(destination_info, "w") as target_entry:
                                    payload = copy_and_hash(entry_stream, target_entry)
                        if payload["bytes"] != info.file_size or payload["crc32"] != f"{info.CRC:08x}":
                            raise PackagingError(f"Source size/CRC mismatch: {info.filename}")
                        records.append({"path": info.filename, "action": "exclude" if reason else "retain",
                                        "reason": reason, **payload})
            retained = [item for item in records if item["action"] == "retain"]
            with zipfile.ZipFile(temp_name) as derivative:
                derived_entries = inspect_entries(derivative)
                verify_player(derivative, derived_entries)
                if derivative.namelist() != [item["path"] for item in retained]:
                    raise PackagingError("Derivative inventory differs from planned retained files")
                for record in retained:
                    with derivative.open(record["path"]) as stream:
                        measured = copy_and_hash(stream)
                    if any(measured[key] != record[key] for key in measured):
                        raise PackagingError(f"Derivative payload mismatch: {record['path']}")
            final_sha256 = sha256_file(source_stream)
            final_stat = os.fstat(source_stream.fileno())
            if (final_sha256 != source_sha256
                    or (original_stat.st_size, original_stat.st_mtime_ns) != (final_stat.st_size, final_stat.st_mtime_ns)
                    or not source.exists() or source.stat().st_ino != original_stat.st_ino
                    or source.stat().st_dev != original_stat.st_dev):
                raise PackagingError("Source changed during packaging; refusing to publish candidate")
            report = {
                "schema_version": 1, "policy_version": POLICY_VERSION,
                "status": "NOT_RELEASE_READY", "purpose": "safe derivative candidate packaging only",
                "source": {"filename": source.name, "sha256": source_sha256, "bytes": original_stat.st_size,
                           "entries": len(entries), "unchanged_after_packaging": True, "all_entry_crc_validated": True},
                "derivative": {"filename": output.name, "sha256": sha256_file(temp_name),
                               "bytes": Path(temp_name).stat().st_size, "entries": len(retained),
                               "all_entry_crc_and_payload_hashes_verified": True},
                "reproducibility": {"python": sys.version.split()[0], "zlib": zlib.ZLIB_RUNTIME_VERSION,
                                    "compression": "DEFLATE level 9", "entry_time": list(FIXED_TIME),
                                    "order": "Unicode path sort", "same_toolchain_required": True},
                "validation": player_checks, "exclusion_policy": EXCLUSION_POLICY,
                "excluded_file_count": sum(item["action"] == "exclude" and not item["path"].endswith("/") for item in records),
                "excluded_uncompressed_bytes": sum(item["bytes"] for item in records if item["action"] == "exclude"),
                "caveats": CAVEATS, "files": records,
            }
            fd, manifest_temp = tempfile.mkstemp(prefix=".happytoy-manifest-", suffix=".json", dir=output.parent)
            temporary_paths.append(Path(manifest_temp))
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(report, stream, indent=2, ensure_ascii=False, sort_keys=True)
                stream.write("\n")
            # Same-directory atomic, no-clobber hard links. No destination is truncated.
            for temporary, target in ((Path(temp_name), output), (Path(manifest_temp), manifest_path)):
                os.link(temporary, target)
                published_paths.append(target)
            return report
    except (zipfile.BadZipFile, NotImplementedError, RuntimeError, OSError, EOFError, zlib.error) as error:
        raise PackagingError(f"Packaging failed: {error}") from error
    finally:
        # If either final output was not published, remove only files created by this attempt.
        if len(published_paths) != 2:
            for path in published_paths:
                path.unlink(missing_ok=True)
        for path in temporary_paths:
            path.unlink(missing_ok=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--expected-source-sha256")
    args = parser.parse_args(argv)
    try:
        report = package_candidate(args.source, args.output, expected_source_sha256=args.expected_source_sha256)
    except PackagingError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print(json.dumps({"status": report["status"], "zip": str(args.output.absolute()),
                      "manifest": str(args.output.with_suffix('.manifest.json').absolute()),
                      "sha256": report["derivative"]["sha256"],
                      "retained_files": report["derivative"]["entries"],
                      "excluded_files": report["excluded_file_count"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
