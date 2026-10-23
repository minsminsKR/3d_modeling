"""Immutable, bounded on-disk archive snapshots shared by records and downloads.

Only this module's hex-named cache ZIP/JSON files are ever pruned. Sources are
opened without following links, copied and hashed once, then checked again
before publishing. Warm reads inspect file identities/stats rather than reading
large meshes again. Advisory file locks coordinate threads and WSGI processes;
per-version leases keep a response's snapshot alive until its file is closed.
"""
from __future__ import annotations

from dataclasses import dataclass
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import threading
import time
import uuid
import weakref
import zipfile

from modeling_contract import ModelingError

FORMAT_VERSION = 1
DEFAULT_MAX_BYTES = 10 * 1024 ** 3
_HEX = re.compile(r"[0-9a-f]{64}")
_JOB_KEY = re.compile(r"[0-9a-f]{32}")
_BUILDING = re.compile(r"\.building-[0-9a-f]{32}\.(?:zip|json)")
_THREAD_LOCKS = weakref.WeakValueDictionary()
_THREAD_LOCKS_GUARD = threading.Lock()
METRICS = {"builds": 0, "cache_hits": 0, "source_bytes_read": 0}


def _unavailable(message, *, retryable=False, next_action=None):
    return ModelingError(message, "ARTIFACT_UNAVAILABLE", retryable=retryable,
                         http_status=503 if retryable else 409,
                         next_action=next_action or ("Wait for artifact edits to finish and retry the same job ID." if retryable else None))


def _too_large():
    return _unavailable("This archive exceeds MODELING_ARCHIVE_CACHE_MAX_BYTES; increase the server cache limit.",
                        next_action="Increase MODELING_ARCHIVE_CACHE_MAX_BYTES on the same server, then download this same job ID again. Existing source artifacts and verified downloads are unchanged.")


def reject_links(path: Path):
    """Reject POSIX links and Windows junction/reparse ancestors, not just leaves."""
    absolute = path.absolute()
    current = Path(absolute.anchor)
    for part in absolute.parts[1:]:
        current /= part
        try:
            info = current.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise _unavailable("Linked or reparse-point storage cannot be exported.")


def file_identity(info, fd=None):
    result = [info.st_size, info.st_mtime_ns, info.st_ctime_ns, info.st_ino, info.st_dev]
    if os.name == "nt" and fd is not None:
        # Python <=3.11 reports NTFS creation time as st_ctime. NTFS ChangeTime
        # also catches an in-place edit whose original mtime was restored.
        import ctypes
        import msvcrt
        from ctypes import wintypes

        class FILE_BASIC_INFO(ctypes.Structure):
            _fields_ = [(name, ctypes.c_longlong) for name in
                        ("CreationTime", "LastAccessTime", "LastWriteTime", "ChangeTime")] + [
                            ("FileAttributes", wintypes.DWORD)]

        basic = FILE_BASIC_INFO()
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.GetFileInformationByHandleEx.argtypes = [wintypes.HANDLE, ctypes.c_int,
                                                        ctypes.c_void_p, wintypes.DWORD]
        if not kernel.GetFileInformationByHandleEx(msvcrt.get_osfhandle(fd), 0,
                                                   ctypes.byref(basic), ctypes.sizeof(basic)):
            raise ctypes.WinError(ctypes.get_last_error())
        result.append(basic.ChangeTime)
    return result


def _identity(path):
    reject_links(path)
    info = path.stat(follow_symlinks=False)
    if not stat.S_ISREG(info.st_mode):
        raise _unavailable("A required artifact is not a regular file.")
    if os.name == "nt":
        fd = os.open(path, os.O_RDONLY | getattr(os, "O_BINARY", 0))
        try:
            return file_identity(os.fstat(fd), fd)
        finally:
            os.close(fd)
    return file_identity(info)


@dataclass(frozen=True)
class Source:
    name: str
    path: Path
    scope: str
    relative: str
    original_name: str | None = None


class FileLease:
    def __init__(self, path: Path, *, blocking=True, shared=False):
        self.path = path
        self.stream = None
        with _THREAD_LOCKS_GUARD:
            self.thread_lock = _THREAD_LOCKS.setdefault(str(path.absolute()), threading.Lock())
        self.shared = shared
        if not shared and not self.thread_lock.acquire(blocking=blocking):
            raise BlockingIOError("Cache file is leased")
        try:
            reject_links(path)
            flags = os.O_RDWR | os.O_CREAT | getattr(os, "O_NOFOLLOW", 0)
            self.stream = os.fdopen(os.open(path, flags, 0o600), "r+b")
            if os.name == "nt":
                import ctypes
                import msvcrt
                from ctypes import wintypes

                class OVERLAPPED(ctypes.Structure):
                    _fields_ = [("Internal", ctypes.c_size_t), ("InternalHigh", ctypes.c_size_t),
                                ("Offset", wintypes.DWORD), ("OffsetHigh", wintypes.DWORD),
                                ("hEvent", wintypes.HANDLE)]

                self.overlapped = OVERLAPPED()
                self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
                self.kernel.LockFileEx.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.DWORD,
                                                   wintypes.DWORD, wintypes.DWORD, ctypes.POINTER(OVERLAPPED)]
                self.kernel.UnlockFileEx.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.DWORD,
                                                     wintypes.DWORD, ctypes.POINTER(OVERLAPPED)]
                self.handle = msvcrt.get_osfhandle(self.stream.fileno())
                while True:
                    if self.kernel.LockFileEx(self.handle, 1 | (0 if shared else 2), 0, 1, 0,
                                              ctypes.byref(self.overlapped)):
                        break
                    code = ctypes.get_last_error()
                    if code not in (33, 158):
                        raise ctypes.WinError(code)
                    if not blocking:
                        raise BlockingIOError("Cache file is leased")
                    time.sleep(0.05)
            else:
                import fcntl
                fcntl.flock(self.stream, (fcntl.LOCK_SH if shared else fcntl.LOCK_EX) |
                            (0 if blocking else fcntl.LOCK_NB))
        except BaseException:
            if self.stream:
                self.stream.close()
            if not shared:
                self.thread_lock.release()
            raise

    def close(self):
        if self.stream is None:
            return
        try:
            if os.name == "nt":
                import ctypes
                self.kernel.UnlockFileEx(self.handle, 0, 1, 0, ctypes.byref(self.overlapped))
            else:
                import fcntl
                fcntl.flock(self.stream, fcntl.LOCK_UN)
        finally:
            self.stream.close()
            self.stream = None
            if not self.shared:
                self.thread_lock.release()

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.close()


@dataclass
class Snapshot:
    path: Path
    record: dict
    archive: dict
    lease: FileLease

    def close(self):
        self.lease.close()


def _canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def _source_signature(sources):
    names = set()
    result = []
    for source in sources:
        if (source.name in names or source.name.startswith(("/", "\\")) or
                ".." in Path(source.name).parts or "\\" in source.name):
            raise _unavailable("Artifact archive names must be unique and confined.")
        names.add(source.name)
        result.append({"name": source.name, "scope": source.scope,
                       "relative": source.relative, "original_name": source.original_name,
                       "identity": _identity(source.path)})
    return result


def _max_bytes():
    raw = os.environ.get("MODELING_ARCHIVE_CACHE_MAX_BYTES", str(DEFAULT_MAX_BYTES))
    try:
        result = int(raw)
    except ValueError:
        result = 0
    if result < 1:
        raise _unavailable("MODELING_ARCHIVE_CACHE_MAX_BYTES must be a positive byte count.")
    return result


def _root(output_root):
    root = Path(output_root).absolute().parent / "archive-cache"
    reject_links(root)
    root.mkdir(parents=True, exist_ok=True)
    return root


def _hash_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_snapshot(directory, version, signature, provenance):
    archive_path = directory / (version + ".zip")
    manifest_path = directory / (version + ".json")
    try:
        reject_links(manifest_path)
        if manifest_path.stat().st_size > 32 * 1024 * 1024:
            return None
        with manifest_path.open("r", encoding="utf-8") as stream:
            manifest = json.load(stream)
        if (manifest.get("cache_format") != FORMAT_VERSION or
                manifest.get("version") != version or manifest.get("sources") != signature or
                manifest.get("provenance") != provenance or
                manifest.get("zip_identity") != _identity(archive_path)):
            return None
        metadata = manifest["archive"]
        if (metadata.get("version") != version or not _HEX.fullmatch(metadata.get("sha256", "")) or
                metadata.get("etag") != '"' + metadata["sha256"] + '"' or
                metadata.get("size") != archive_path.stat().st_size):
            return None
        return manifest
    except (OSError, ValueError, KeyError, TypeError, ModelingError):
        return None


def _copy_source(archive, source, expected_identity, maximum):
    reject_links(source.path)
    flags = os.O_RDONLY | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(source.path, flags)
    digest = hashlib.sha256()
    size = 0
    with os.fdopen(fd, "rb") as stream:
        if file_identity(os.fstat(stream.fileno()), stream.fileno()) != expected_identity:
            raise _unavailable("Artifact changed while preparing its archive.", retryable=True)
        # Stable timestamps make identical snapshots independent of wall-clock time.
        info = zipfile.ZipInfo(source.name, date_time=(1980, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        with archive.open(info, "w", force_zip64=True) as destination:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                destination.write(chunk)
                digest.update(chunk)
                size += len(chunk)
                METRICS["source_bytes_read"] += len(chunk)
                if archive.fp.tell() > maximum:
                    raise _too_large()
        if file_identity(os.fstat(stream.fileno()), stream.fileno()) != expected_identity:
            raise _unavailable("Artifact changed during archive creation.", retryable=True)
    return {"sha256": digest.hexdigest(), "size": size}


def _build(path, version, provenance, sources, signature, maximum):
    record = copy.deepcopy(provenance)
    record["inputs"] = []
    record["artifacts"] = []
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED, allowZip64=True) as archive:
        for source, identity in zip(sources, signature):
            digest = _copy_source(archive, source, identity["identity"], maximum)
            if source.scope == "input":
                record["inputs"].append({"name": source.original_name, "archive_name": source.name, **digest})
            else:
                record["artifacts"].append({"name": source.name, **digest})
        info = zipfile.ZipInfo("generation-record.json", date_time=(1980, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(info, json.dumps(record, ensure_ascii=False, indent=2))
    with path.open("r+b") as stream:
        os.fsync(stream.fileno())
    # Exercise ZIP CRCs before publishing, then hash the exact package bytes.
    with zipfile.ZipFile(path) as package:
        if package.testzip() is not None:
            raise _unavailable("The newly created ZIP failed integrity verification.")
    sha256 = _hash_file(path)
    METRICS["builds"] += 1
    return record, {"etag": '"' + sha256 + '"', "sha256": sha256,
                    "size": path.stat().st_size, "version": version}


def _entries(root):
    """Inventory only generated files in non-linked, hex-named job directories."""
    entries = []
    for directory in root.iterdir():
        if not _JOB_KEY.fullmatch(directory.name):
            continue
        try:
            reject_links(directory)
            if not directory.is_dir():
                continue
            for path in directory.iterdir():
                if path.suffix not in {".zip", ".json"} or not _HEX.fullmatch(path.stem):
                    continue
                try:
                    identity = _identity(path)
                except (OSError, ModelingError):
                    continue
                entries.append((path, identity[0], path.stat().st_mtime_ns))
        except (OSError, ModelingError):
            continue
    return entries


def _discard_abandoned(directory):
    """Caller owns this job's builder lock, so no matching file can be active."""
    for path in directory.iterdir():
        if not _BUILDING.fullmatch(path.name):
            continue
        try:
            _identity(path)
            path.unlink()
        except FileNotFoundError:
            pass
        except (OSError, ModelingError):
            # Unknown/linked/busy files are never followed or force-removed.
            continue


def _clean_idle_builds(root):
    for directory in root.iterdir():
        if not _JOB_KEY.fullmatch(directory.name):
            continue
        try:
            reject_links(directory)
            if not directory.is_dir():
                continue
            lease = FileLease(directory / "builder.lock", blocking=False)
        except (OSError, ModelingError):
            continue
        with lease:
            _discard_abandoned(directory)


def _make_room(root, required, maximum, protected):
    # A killed worker cannot run finally. Reclaim only inactive workers' exact
    # generated temporary names; active builders retain their files and locks.
    _clean_idle_builds(root)
    entries = _entries(root)
    total = sum(size for _, size, _ in entries)
    versions = {}
    for path, size, modified in entries:
        key = (path.parent, path.stem)
        grouped = versions.setdefault(key, {"files": [], "size": 0, "modified": modified})
        grouped["files"].append(path)
        grouped["size"] += size
        grouped["modified"] = max(grouped["modified"], modified)
    for (directory, version), entry in sorted(versions.items(), key=lambda item: item[1]["modified"]):
        if total + required <= maximum:
            break
        if (directory, version) == protected:
            continue
        try:
            lease = FileLease(directory / (version + ".pin"), blocking=False)
        except (OSError, ModelingError):
            continue
        with lease:
            for path in entry["files"]:
                try:
                    reject_links(path)
                    size = path.stat().st_size
                    path.unlink()
                    total -= size
                except FileNotFoundError:
                    pass
                except OSError:
                    # Windows refuses removal of a file opened by another reader.
                    continue
    if total + required > maximum:
        raise _unavailable("Archive cache is full while snapshots are in use. Retry after active downloads finish.", retryable=True,
                           next_action="Retry this same job ID after active downloads finish, or increase MODELING_ARCHIVE_CACHE_MAX_BYTES on the server. Do not delete source artifacts.")


def snapshot_for_job(job_id, output_root, provenance, collect_sources):
    """Return a leased immutable snapshot. Call close after consuming its record/file."""
    root = _root(output_root)
    # A 128-bit directory key leaves room for Windows' legacy path limit;
    # full job provenance is still part of the 256-bit version fingerprint.
    job_key = hashlib.sha256(str(job_id).encode("utf-8")).hexdigest()[:32]
    directory = root / job_key
    reject_links(directory)
    directory.mkdir(exist_ok=True)
    maximum = _max_bytes()
    with FileLease(directory / "builder.lock"):
        _discard_abandoned(directory)
        for attempt in range(3):
            sources = collect_sources()
            signature = _source_signature(sources)
            key = {"cache_format": FORMAT_VERSION, "provenance": provenance, "sources": signature}
            version = hashlib.sha256(_canonical(key)).hexdigest()
            lease = FileLease(directory / (version + ".pin"), shared=True)
            manifest = _read_snapshot(directory, version, signature, provenance)
            if manifest is not None:
                METRICS["cache_hits"] += 1
                return Snapshot(directory / (version + ".zip"), copy.deepcopy(manifest["record"]),
                                dict(manifest["archive"]), lease)
            lease.close()
            # A rebuild also waits for any older response using a damaged
            # version before replacing it (necessary for Windows open files).
            lease = FileLease(directory / (version + ".pin"))
            temporary = directory / (".building-" + uuid.uuid4().hex + ".zip")
            manifest_temp = temporary.with_suffix(".json")
            try:
                record, metadata = _build(temporary, version, provenance, sources, signature, maximum)
                if signature != _source_signature(collect_sources()):
                    raise _unavailable("Artifacts changed during archive creation.", retryable=True)
                if metadata["size"] > maximum:
                    raise _too_large()
                path = directory / (version + ".zip")
                manifest_path = directory / (version + ".json")
                manifest = {**key, "version": version, "record": record, "archive": metadata,
                            "zip_identity": _identity(temporary)}
                data = _canonical(manifest)
                # Serialize publication/quota accounting across all jobs/processes.
                with FileLease(root / "prune.lock"):
                    existing = sum(p.stat().st_size for p in (path, manifest_path) if p.is_file() and not p.is_symlink())
                    _make_room(root, metadata["size"] + len(data) + 1024 - existing, maximum, (directory, version))
                    reject_links(path)
                    reject_links(manifest_path)
                    os.replace(temporary, path)
                    # Rename preserves the inode and times on Linux, but ctime can
                    # change; record the final published identity on both platforms.
                    manifest["zip_identity"] = _identity(path)
                    with manifest_temp.open("xb") as stream:
                        stream.write(_canonical(manifest))
                        stream.flush()
                        os.fsync(stream.fileno())
                    os.replace(manifest_temp, manifest_path)
                    # Downgrade before returning: even the very first cold
                    # response can stream beside another GET or /record read.
                    # The global prune lease protects the brief unlock gap.
                    lease.close()
                    lease = FileLease(directory / (version + ".pin"), shared=True)
                return Snapshot(path, copy.deepcopy(record), metadata, lease)
            except ModelingError as error:
                lease.close()
                if not error.retryable or attempt == 2:
                    raise
            except BaseException:
                lease.close()
                raise
            finally:
                for candidate in (temporary, manifest_temp):
                    try:
                        candidate.unlink()
                    except FileNotFoundError:
                        pass
        raise _unavailable("Artifacts are still changing. Retry this job after editing finishes.", retryable=True)
