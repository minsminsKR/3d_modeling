"""Private, schema-1 CLI storage with change-aware writes and manifest lookup.

This module has no HTTP, server, or model-runtime dependencies. Cached documents
are checked against filesystem metadata before use; callers receive independent
copies so presentation-only changes cannot mutate the stored cache.
"""
from __future__ import annotations

import contextlib
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile
import time
import uuid

from modeling_contract import ModelingError

SCHEMA_VERSION = 1
_JOB_ID = re.compile(r"^[A-Za-z0-9_-]{1,128}$")


class _Manifest(dict):
    """A mutable result carrying its own immutable read/merge baseline."""
    def __init__(self, value, baseline=None):
        super().__init__(value)
        self._baseline = baseline


if os.name == "nt":
    import ctypes
    from ctypes import wintypes

    class _FileBasicInfo(ctypes.Structure):
        _fields_ = [(name, ctypes.c_longlong) for name in (
            "CreationTime", "LastAccessTime", "LastWriteTime", "ChangeTime")]
        _fields_ += [("FileAttributes", wintypes.DWORD)]

    _kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    _kernel32.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                    wintypes.LPVOID, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    _kernel32.CreateFileW.restype = wintypes.HANDLE
    _kernel32.GetFileInformationByHandleEx.argtypes = [wintypes.HANDLE, ctypes.c_int,
                                                     wintypes.LPVOID, wintypes.DWORD]
    _kernel32.GetFileInformationByHandleEx.restype = wintypes.BOOL
    _kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    _kernel32.CloseHandle.restype = wintypes.BOOL


def _windows_change_time(path):
    """NTFS ChangeTime also detects same-size writes with a restored mtime."""
    handle = _kernel32.CreateFileW(str(path), 0x80, 0x7, None, 3, 0x02000000, None)
    if handle == ctypes.c_void_p(-1).value:
        return None
    try:
        info = _FileBasicInfo()
        if _kernel32.GetFileInformationByHandleEx(handle, 0, ctypes.byref(info), ctypes.sizeof(info)):
            return info.ChangeTime
        return None
    finally:
        _kernel32.CloseHandle(handle)


@contextlib.contextmanager
def _manifest_lock(path):
    """Serialize the current-read/merge/replace transaction across processes."""
    private_directory(path.parent)
    lock_path = path.parent / ".manifest.lock"
    descriptor = None
    handle = None
    acquired = False
    try:
        if lock_path.is_symlink():
            raise OSError("Manifest lock cannot be a symbolic link")
        flags = os.O_RDWR | os.O_CREAT | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(lock_path, flags, 0o600)
        if not stat.S_ISREG(os.fstat(descriptor).st_mode):
            raise OSError("Manifest lock must be a regular file")
        handle = os.fdopen(descriptor, "r+b")
        descriptor = None
        if handle.seek(0, os.SEEK_END) == 0:
            handle.write(b"\0")
            handle.flush()
        deadline = time.monotonic() + 30
        while not acquired:
            handle.seek(0)
            try:
                if os.name == "nt":
                    import msvcrt
                    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    import fcntl
                    fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                acquired = True
            except OSError:
                if time.monotonic() >= deadline:
                    raise ModelingError("Another process is updating this request's local manifest. Retry shortly.",
                                        code="LOCAL_IO_ERROR", retryable=True)
                time.sleep(0.05)
        yield
    except OSError as exc:
        raise ModelingError("Cannot lock or update the request's local manifest.", code="LOCAL_IO_ERROR") from exc
    finally:
        if handle is not None:
            try:
                if acquired:
                    handle.seek(0)
                    if os.name == "nt":
                        import msvcrt
                        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
                    else:
                        import fcntl
                        fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
            finally:
                handle.close()
        if descriptor is not None:
            os.close(descriptor)


def now():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def private_directory(path: Path):
    path = Path(path)
    try:
        path.mkdir(parents=True, exist_ok=True)
        if os.name != "nt":
            path.chmod(0o700)
    except OSError as exc:
        raise ModelingError(f"Cannot create private local directory: {path}", code="LOCAL_IO_ERROR") from exc
    return path


def atomic_json(path: Path, value):
    """Persist one private, fsynced document and atomically replace its target."""
    path = Path(path)
    temporary = None
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        fd, temporary = tempfile.mkstemp(prefix=".writing-", dir=path.parent)
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            if os.name != "nt":
                os.fchmod(handle.fileno(), 0o600)
            json.dump(value, handle, ensure_ascii=False, indent=2, allow_nan=False)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    except (OSError, ValueError, TypeError) as exc:
        raise ModelingError(f"Cannot write local JSON file: {path}", code="LOCAL_IO_ERROR") from exc
    finally:
        if temporary is not None:
            with contextlib.suppress(FileNotFoundError):
                os.unlink(temporary)


def read_json(path: Path):
    try:
        result = json.loads(Path(path).read_text(encoding="utf-8"))
        if not isinstance(result, dict):
            raise ValueError("Local state must be a JSON object.")
        return result
    except (OSError, ValueError) as exc:
        raise ModelingError(f"Cannot read local JSON file: {path}", code="LOCAL_IO_ERROR") from exc


def sha256_file(path: Path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def cookie_signature(jar):
    """Opaque fingerprint of every persisted cookie attribute; never log values."""
    cookies = []
    for cookie in jar:
        fields = {key: getattr(cookie, key) for key in (
            "version", "name", "value", "port", "port_specified", "domain",
            "domain_specified", "domain_initial_dot", "path", "path_specified",
            "secure", "expires", "discard", "comment", "comment_url", "rfc2109")}
        fields["rest"] = cookie._rest
        cookies.append(fields)
    cookies.sort(key=lambda item: (item["domain"], item["path"], item["name"]))
    return hashlib.sha256(json.dumps(cookies, sort_keys=True, ensure_ascii=True,
                                     separators=(",", ":")).encode()).hexdigest()


def save_cookie_jar(jar, path: Path, previous_signature):
    """Write only changed cookies, retaining private atomic replacement and fsync."""
    path = Path(path)
    jar.clear_expired_cookies()
    signature = cookie_signature(jar)
    if signature == previous_signature and path.is_file():
        return signature
    temporary = None
    try:
        private_directory(path.parent)
        fd, temporary = tempfile.mkstemp(prefix=".cookies-", dir=path.parent)
        os.close(fd)
        jar.save(temporary, ignore_discard=True, ignore_expires=False)
        if os.name != "nt":
            os.chmod(temporary, 0o600)
        with open(temporary, "r+b") as handle:
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    except OSError as exc:
        raise ModelingError("Cannot preserve the workspace session locally.", code="LOCAL_IO_ERROR") from exc
    finally:
        if temporary is not None:
            with contextlib.suppress(FileNotFoundError):
                os.unlink(temporary)
    return signature


def _request_id(value):
    try:
        parsed = uuid.UUID(value)
        if str(parsed) != value.lower():
            raise ValueError()
        return str(parsed)
    except (ValueError, AttributeError, TypeError) as exc:
        raise ModelingError("--request-id must be a UUID, e.g. one printed by an earlier generate command.") from exc


def _stamp(path):
    try:
        info = path.stat()
        change_time = _windows_change_time(path) if os.name == "nt" else None
        return info.st_mtime_ns, info.st_ctime_ns, info.st_size, info.st_ino, change_time
    except FileNotFoundError:
        return None
    except OSError as exc:
        raise ModelingError(f"Cannot inspect local state file: {path}", code="LOCAL_IO_ERROR") from exc


def _content(document):
    return {key: value for key, value in document.items() if key != "updated_at"}


def _merge_delta(original, proposed, current):
    """Preserve external changes to fields untouched by this caller.

    The caller holds the manifest transaction lock. Atomic replacement prevents
    torn writes; the merge protects unrelated fields updated since this specific
    mutable manifest was read. A concurrent change to the same field follows the
    last intentional write, while independent receipts/jobs are retained.
    """
    if isinstance(original, dict) and isinstance(proposed, dict) and isinstance(current, dict):
        result = copy.deepcopy(dict(current))
        for key in set(original) | set(proposed):
            if key == "updated_at":
                continue
            if key not in proposed:
                result.pop(key, None)
            elif key not in original:
                result[key] = copy.deepcopy(proposed[key])
            elif proposed[key] != original[key]:
                result[key] = _merge_delta(original[key], proposed[key], current.get(key))
        return result
    if isinstance(original, list) and isinstance(proposed, list) and isinstance(current, list):
        identity = next((key for key in ("id", "job_id") if all(
            isinstance(item, dict) and key in item for item in original + proposed + current)), None)
        if identity:
            old = {item[identity]: item for item in original}
            new = {item[identity]: item for item in proposed}
            result = {item[identity]: copy.deepcopy(item) for item in current}
            for key in old.keys() - new.keys():
                result.pop(key, None)
            for key, value in new.items():
                if key not in old or value != old[key]:
                    result[key] = _merge_delta(old.get(key), value, result.get(key))
            return list(result.values())
    return copy.deepcopy(proposed)


class State:
    def __init__(self, root: Path, server: str):
        self.root = private_directory(Path(root).expanduser().resolve())
        self.server = server
        namespace = hashlib.sha256(server.encode()).hexdigest()[:24]
        self.directory = private_directory(self.root / "servers" / namespace)
        self.requests = private_directory(self.directory / "requests")
        self.records = private_directory(self.directory / "records")
        self.cookies = self.directory / "cookies.lwp"
        self._cache = {}
        self._baselines = {}
        self._index = {}
        self._index_stamps = {}

    def manifest_path(self, request_id):
        return self.requests / _request_id(request_id) / "manifest.json"

    def _read(self, path):
        stamp = _stamp(path)
        if stamp is None:
            self._cache.pop(path, None)
            return None
        cached = self._cache.get(path)
        if cached is None or cached[0] != stamp:
            document = read_json(path)
            self._cache[path] = stamp, document
        else:
            document = cached[1]
        # Cached documents are never handed out or mutated, so the immutable
        # cache value itself is also a safe merge baseline.
        self._baselines[path] = document
        return _Manifest(copy.deepcopy(document), document)

    def load_manifest(self, request_id):
        path = self.manifest_path(request_id)
        manifest = self._read(path)
        if manifest is None:
            raise ModelingError(f"Cannot read local JSON file: {path}", code="LOCAL_IO_ERROR")
        return manifest

    def manifests(self):
        try:
            paths = sorted(self.requests.glob("*/manifest.json"))
        except OSError as exc:
            raise ModelingError("Cannot list local generation history.", code="LOCAL_IO_ERROR") from exc
        return [document for path in paths if (document := self._read(path)) is not None]

    def manifest_page(self, limit=50, offset=0):
        """Page local history before parsing documents, using schema-1 path order.

        Enumerating names still discovers external additions/removals immediately.
        Only the selected page needs metadata checks, JSON reads and independent
        copies; a history page never parses unrelated request/input provenance.
        """
        if not 1 <= limit <= 200 or offset < 0:
            raise ModelingError("History limit must be between 1 and 200 and offset must be nonnegative.")
        try:
            paths = sorted(self.requests.glob("*/manifest.json"))
        except OSError as exc:
            raise ModelingError("Cannot list local generation history.", code="LOCAL_IO_ERROR") from exc
        selected = paths[offset:offset + limit]
        documents = [document for path in selected if (document := self._read(path)) is not None]
        return {"requests": documents, "total": len(paths), "limit": limit, "offset": offset}

    def write_manifest(self, manifest):
        path = self.manifest_path(manifest["request_id"])
        with _manifest_lock(path):
            return self._write_locked(path, manifest)

    def _write_locked(self, path, manifest):
        previous = getattr(manifest, "_baseline", self._baselines.get(path))
        # Read the current file before writing, including changes by another CLI.
        current = self._read(path)
        proposed = copy.deepcopy(dict(manifest))
        if previous is not None and current is not None and current != previous:
            proposed = _merge_delta(previous, proposed, current)
        if current is not None and _content(proposed) == _content(current):
            manifest.clear()
            manifest.update(current)
            if isinstance(manifest, _Manifest):
                manifest._baseline = self._cache[path][1]
            return False
        proposed["updated_at"] = now()
        atomic_json(path, proposed)
        cached = copy.deepcopy(proposed)
        self._cache[path] = _stamp(path), cached
        self._baselines[path] = cached
        manifest.clear()
        manifest.update(proposed)
        if isinstance(manifest, _Manifest):
            manifest._baseline = cached
        return True

    def recover_manifest(self, request_id, jobs):
        """Attach remote jobs without replacing preserved local request inputs."""
        path = self.manifest_path(request_id)
        manifest = self._read(path)
        if manifest is not None:
            self._validate_manifest(manifest, request_id)
        else:
            manifest = _Manifest({"schema_version": SCHEMA_VERSION, "server": self.server,
                        "request_id": request_id, "created_at": now(), "state": "recovered",
                        "submitted_options": {}, "inputs": [], "jobs": [], "downloads": [],
                        "recovered_from_server": True})
        manifest["jobs"] = jobs
        self.write_manifest(manifest)
        return manifest

    def _validate_manifest(self, manifest, request_id):
        if manifest.get("server") != self.server:
            raise ModelingError("Stored request belongs to another server.", code="REQUEST_CONFLICT")
        if manifest.get("request_id") != request_id:
            raise ModelingError("Stored request ID does not match its manifest path.", code="INTEGRITY_ERROR")

    def prepare(self, request_id, images, options):
        """Persist exact submitted bytes and request key before network I/O."""
        path = self.manifest_path(request_id)
        manifest = self._read(path)
        if manifest is not None:
            self._validate_manifest(manifest, request_id)
            if options and options != manifest["submitted_options"]:
                raise ModelingError("Request ID already has different options. Reuse without options, or use a new UUID.", code="REQUEST_CONFLICT")
            if not manifest.get("inputs"):
                raise ModelingError("This request was recovered from the server without local input copies. Use resume or collect with its UUID.")
            if images:
                supplied = [(Path(p).name, sha256_file(Path(p).expanduser())) for p in images]
                original = [(item["filename"], item["sha256"]) for item in manifest["inputs"]]
                if supplied != original:
                    raise ModelingError("Request ID already has different images. Use a new UUID for a new request.", code="REQUEST_CONFLICT")
            for item in manifest["inputs"]:
                copied = path.parent / item["local_path"]
                if not copied.is_file() or sha256_file(copied) != item["sha256"]:
                    raise ModelingError("Preserved input is missing or modified; refusing to alter an existing request.", code="INTEGRITY_ERROR")
            return manifest
        if not images:
            raise ModelingError("Supply one or more images, or an existing local --request-id to resume.")
        private_directory(path.parent)
        private_directory(path.parent / "inputs")
        inputs = []
        for index, raw in enumerate(images):
            source = Path(raw).expanduser().resolve()
            extension = source.suffix.lower()
            if extension not in {".png", ".jpg", ".jpeg", ".webp"} or not source.is_file():
                raise ModelingError(f"Not a supported image file: {source}")
            local_path = f"inputs/{index:03d}{extension}"
            copied = path.parent / local_path
            digest = hashlib.sha256()
            fd, temporary = tempfile.mkstemp(prefix=".input-", dir=copied.parent)
            try:
                with os.fdopen(fd, "wb") as output, source.open("rb") as original:
                    if os.name != "nt":
                        os.fchmod(output.fileno(), 0o600)
                    for chunk in iter(lambda: original.read(1024 * 1024), b""):
                        output.write(chunk)
                        digest.update(chunk)
                    output.flush()
                    os.fsync(output.fileno())
                os.replace(temporary, copied)
            except OSError as exc:
                raise ModelingError("Cannot preserve the generation input locally.", code="LOCAL_IO_ERROR") from exc
            finally:
                with contextlib.suppress(FileNotFoundError):
                    os.unlink(temporary)
            inputs.append({"filename": source.name, "local_path": local_path,
                           "sha256": digest.hexdigest(), "size": copied.stat().st_size})
        manifest = _Manifest({"schema_version": SCHEMA_VERSION, "server": self.server,
                    "request_id": request_id, "created_at": now(), "state": "prepared",
                    "submitted_options": options, "inputs": inputs, "jobs": [], "downloads": []})
        self.write_manifest(manifest)
        return manifest

    def _lookup(self, job_id):
        """Compatibility lookup: rebuild only entries whose metadata changed."""
        paths = sorted(self.requests.glob("*/manifest.json"))
        stamps = {path: _stamp(path) for path in paths}
        changed = {path for path, stamp in stamps.items() if self._index_stamps.get(path) != stamp}
        removed = self._index_stamps.keys() - stamps.keys()
        if changed or removed:
            affected = changed | removed
            self._index = {key: retained for key, paths in self._index.items()
                           if (retained := [path for path in paths if path not in affected])}
            # Each job normally has one request; retain legacy duplicates as well.
            for path in changed:
                manifest = self._read(path)
                if manifest is not None:
                    for job in manifest.get("jobs", []):
                        self._index.setdefault(job.get("id"), []).append(path)
            self._index_stamps = stamps
        return self._index.get(job_id, [])

    def remember_job(self, job):
        job_id = job.get("id")
        if not isinstance(job_id, str) or not _JOB_ID.fullmatch(job_id):
            raise ModelingError("Invalid job ID.")
        request_id = job.get("request_id")
        if request_id:
            try:
                paths = [self.manifest_path(request_id)]
            except ModelingError as exc:
                if exc.code != "INVALID_INPUT":
                    raise
                # The HTTP API also accepts non-UUID idempotency keys. Those
                # web/API jobs have no corresponding UUID directory unless an
                # older local manifest already references their job identity.
                paths = self._lookup(job_id)
        else:
            paths = self._lookup(job_id)
        for path in paths:
            manifest = self._read(path)
            if manifest is None:
                continue
            self._validate_manifest(manifest, path.parent.name)
            changed = False
            for index, existing in enumerate(manifest.get("jobs", [])):
                # elapsed_seconds is a live display counter, not a new generation
                # event. All other fields, including server updated_at, persist.
                comparable = lambda value: {key: item for key, item in value.items() if key != "elapsed_seconds"}
                if existing.get("id") == job_id and comparable(existing) != comparable(job):
                    manifest["jobs"][index] = copy.deepcopy(job)
                    changed = True
            if changed:
                self.write_manifest(manifest)
