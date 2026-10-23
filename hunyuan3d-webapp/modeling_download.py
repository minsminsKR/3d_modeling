"""Crash-safe, bounded-memory archive transfer shared by download/collect/resume.

Only strong entity tags can authorize byte reuse. Partial files are private to
one origin, job and URL; durable prefix hashes make interrupted writes safe to
resume in another process. The caller holds ``download_lock`` through receipt
publication and supplies its existing ZIP/provenance verifier.
"""
from __future__ import annotations

import contextlib
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import time
import zipfile

try:
    import requests
except ImportError:
    requests = None


CHUNK_BYTES = 1024 * 1024
CHECKPOINT_BYTES = 32 * 1024 * 1024
CHECKPOINT_SECONDS = 5.0
_STRONG_ETAG = re.compile(r'^"[\x21\x23-\x7e\x80-\xff]+"$')
_SHA_ETAG = re.compile(r'^"([a-f0-9]{64})"$')
_RANGE = re.compile(r'^bytes (\d+)-(\d+)/(\d+)$')


def _regular(path, error, *, missing=True):
    """Reject links/reparse points, directories and device files at owned paths."""
    try:
        info = path.lstat()
    except FileNotFoundError:
        if missing:
            return None
        raise error("Download file disappeared; retry with the same job ID.", code="LOCAL_IO_ERROR")
    if (stat.S_ISLNK(info.st_mode) or not stat.S_ISREG(info.st_mode)
            or getattr(info, "st_file_attributes", 0) & 0x400):
        raise error("Download paths must be regular files without symbolic links or reparse points.", code="LOCAL_IO_ERROR")
    return info


def _open(path, mode, error):
    _regular(path, error)
    flags = os.O_RDWR | os.O_CREAT | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0)
    if mode == "wb":
        flags |= os.O_TRUNC
    descriptor = os.open(path, flags, 0o600)
    try:
        if not stat.S_ISREG(os.fstat(descriptor).st_mode):
            raise error("Download path is not a regular file.", code="LOCAL_IO_ERROR")
        return os.fdopen(descriptor, "r+b")
    except BaseException:
        os.close(descriptor)
        raise


@contextlib.contextmanager
def download_lock(output, job_id, error, timeout=60):
    """One interprocess lock per output/job, released even after process death."""
    output.mkdir(parents=True, exist_ok=True)
    path = output / f".{job_id}.download.lock"
    handle = _open(path, "r+b", error)
    acquired = False
    try:
        if handle.seek(0, os.SEEK_END) == 0:
            handle.write(b"\0")
            handle.flush()
        deadline = time.monotonic() + min(max(timeout, 1), 120)
        while True:
            try:
                handle.seek(0)
                if os.name == "nt":
                    import msvcrt
                    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    import fcntl
                    fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                acquired = True
                break
            except OSError:
                if time.monotonic() >= deadline:
                    raise error("Another process is downloading this job to the same output. Retry when it finishes.",
                                code="LOCAL_IO_ERROR", retryable=True)
                time.sleep(0.1)
        yield
    finally:
        if acquired:
            handle.seek(0)
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl
                fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
        handle.close()


def safe_download_paths(output, job_id, error):
    for suffix in (".zip", ".download.json"):
        _regular(output / (job_id + suffix), error)


def _strong(value):
    return value if isinstance(value, str) and _STRONG_ETAG.fullmatch(value) else None


def _integer(value):
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _drop(part, sidecar, error):
    for path in (part, sidecar):
        _regular(path, error)
        with contextlib.suppress(FileNotFoundError):
            path.unlink()


def _prefix(handle, count):
    digest = hashlib.sha256()
    handle.seek(0)
    remaining = count
    while remaining:
        chunk = handle.read(min(CHUNK_BYTES, remaining))
        if not chunk:
            raise ValueError("Incomplete prefix")
        digest.update(chunk)
        remaining -= len(chunk)
    return digest


def _load_partial(part, sidecar, identity, advertised, error):
    _regular(part, error)
    info = _regular(sidecar, error)
    if not part.exists() or info is None:
        _drop(part, sidecar, error)
        return None
    try:
        if info.st_size > 16384:
            raise ValueError("Oversized sidecar")
        data = json.loads(sidecar.read_text(encoding="utf-8"))
        if not isinstance(data, dict) or data.get("schema_version") != 1:
            raise ValueError("Wrong partial schema")
        if any(data.get(key) != value for key, value in identity.items()):
            raise ValueError("Partial belongs to another origin/job/URL")
        count, size = data.get("checkpoint_bytes"), data.get("size")
        if (not _strong(data.get("etag")) or not _integer(size) or not _integer(count)
                or not 0 <= count <= size or part.stat().st_size < count
                or not re.fullmatch(r"[a-f0-9]{64}", str(data.get("prefix_sha256", "")))):
            raise ValueError("Invalid partial checkpoint")
        if advertised and (data["etag"] != advertised["etag"] or size != advertised["size"]):
            raise ValueError("Server snapshot changed")
        with _open(part, "r+b", error) as handle:
            digest = _prefix(handle, count)
            if digest.hexdigest() != data["prefix_sha256"]:
                raise ValueError("Partial prefix was modified")
            # A hard kill can leave bytes newer than the last durable sidecar.
            # They have no committed hash and must never be silently trusted.
            handle.truncate(count)
        return data, digest
    except (ValueError, TypeError, KeyError, OSError):
        _drop(part, sidecar, error)
        return None


def _advertised(record, error):
    value = record.get("archive")
    if value is None:
        return None
    if (not isinstance(value, dict) or not _strong(value.get("etag"))
            or not _integer(value.get("size"))
            or not re.fullmatch(r"[a-f0-9]{64}", str(value.get("sha256", "")))):
        raise error("Server archive metadata is invalid.", code="PROTOCOL_ERROR")
    matched = _SHA_ETAG.fullmatch(value["etag"])
    if matched and matched.group(1) != value["sha256"]:
        raise error("Server archive metadata has inconsistent hashes.", code="PROTOCOL_ERROR")
    return value


def _length(response, error):
    value = response.headers.get("Content-Length")
    if value is None:
        return None
    if not isinstance(value, str) or not re.fullmatch(r"\d+", value):
        raise error("Archive Content-Length is invalid.", code="PROTOCOL_ERROR")
    return int(value)


def transfer_archive(client, job_id, output, record, archive_url, *, verify, atomic_json, error):
    """Transfer, verify and atomically publish an archive; preserve failed partials.

    At most two GETs are issued: a failed 416 can trigger one full restart. A
    Range ignored with 200 is consumed as a new full response in the same GET.
    Neither this module nor a retry ever submits a generation request.
    """
    safe_download_paths(output, job_id, error)
    final = output / f"{job_id}.zip"
    part, sidecar = output / f".{job_id}.zip.part", output / f".{job_id}.zip.part.json"
    identity = {"server": client.server, "job_id": job_id, "url": client.url(archive_url)}
    advertised = _advertised(record, error)
    existing = _load_partial(part, sidecar, identity, advertised, error)
    data, digest = existing if existing else (None, hashlib.sha256())
    offset = data["checkpoint_bytes"] if data else 0
    resumed, received, durable = offset, 0, offset
    transfer_etag, total = data["etag"] if data else None, data["size"] if data else None
    tag_sha = _SHA_ETAG.fullmatch(transfer_etag or "")
    # A local sidecar is never authoritative for the remote full-file hash.
    expected_sha = advertised["sha256"] if advertised else (tag_sha.group(1) if tag_sha else None)
    count = offset
    started = time.monotonic()

    def details():
        return {"job_id": job_id, "partial_bytes": durable,
                "resume_available": bool(transfer_etag and total is not None and durable),
                "resumed_bytes": resumed, "received_bytes": received}

    def checkpoint(handle):
        nonlocal durable
        handle.flush()
        os.fsync(handle.fileno())
        if transfer_etag and total is not None:
            atomic_json(sidecar, {"schema_version": 1, **identity, "etag": transfer_etag,
                                 "size": total, "sha256": expected_sha,
                                 "checkpoint_bytes": count, "prefix_sha256": digest.hexdigest()})
        durable = count

    try:
        for attempt in range(2):
            headers = {"Accept-Encoding": "identity"}
            if offset and transfer_etag:
                headers.update({"Range": f"bytes={offset}-", "If-Range": transfer_etag})
            with client.request("GET", archive_url, stream=True, headers=headers) as response:
                response_etag = _strong(response.headers.get("ETag"))
                if response.status_code == 416:
                    complete = (offset and offset == total and response_etag == transfer_etag
                                and response.headers.get("Content-Range") == f"bytes */{total}")
                    if complete:
                        break
                    if attempt == 0:
                        _drop(part, sidecar, error)
                        data, digest = None, hashlib.sha256()
                        offset = resumed = count = durable = 0
                        transfer_etag = total = expected_sha = None
                        continue
                    raise error("Server rejected a fresh archive byte range.", code="PROTOCOL_ERROR")
                client.check(response)
                if response.status_code not in {200, 206}:
                    raise error("Archive response must be HTTP 200 or 206.", code="PROTOCOL_ERROR")
                encoding = response.headers.get("Content-Encoding", "identity").strip().lower()
                if encoding not in {"", "identity"}:
                    raise error("Archive response is encoded; byte offsets cannot be verified.", code="PROTOCOL_ERROR")
                content_type = response.headers.get("Content-Type", "").lower()
                if "text/html" in content_type or "application/xhtml" in content_type:
                    raise error("Archive download returned a login or HTML page. Run login for this server.", code="AUTH_REQUIRED")
                length = _length(response, error)
                if response.status_code == 206:
                    match = _RANGE.fullmatch(response.headers.get("Content-Range", ""))
                    if not offset or not transfer_etag or not match:
                        raise error("Unexpected or invalid archive partial response.", code="PROTOCOL_ERROR")
                    first, last, response_total = map(int, match.groups())
                    if (response_etag != transfer_etag or first != offset or response_total != total
                            or last != total - 1 or first > last
                            or (length is not None and length != last - first + 1)):
                        raise error("Archive partial response does not match its checkpoint.", code="PROTOCOL_ERROR")
                    expected_length = total - offset
                    mode = "r+b"
                else:
                    # If-Range mismatch or a server ignoring Range means a full,
                    # new representation. Never append it to the old prefix.
                    offset = resumed = count = durable = 0
                    digest = hashlib.sha256()
                    transfer_etag, total = response_etag, length
                    if advertised and response_etag is None:
                        raise error("Archive response omitted its advertised strong ETag.", code="PROTOCOL_ERROR")
                    if advertised and response_etag == advertised["etag"]:
                        if total is not None and total != advertised["size"]:
                            raise error("Archive size differs from server metadata.", code="PROTOCOL_ERROR")
                        total, expected_sha = advertised["size"], advertised["sha256"]
                    else:
                        matched = _SHA_ETAG.fullmatch(response_etag or "")
                        expected_sha = matched.group(1) if matched else None
                    _drop(part, sidecar, error)
                    expected_length, mode = total, "wb"
                with _open(part, mode, error) as handle:
                    handle.seek(offset)
                    checkpoint(handle)
                    last_checkpoint = time.monotonic()
                    try:
                        for chunk in response.iter_content(CHUNK_BYTES):
                            if not chunk:
                                continue
                            if total is not None and count + len(chunk) > total:
                                raise error("Archive response exceeded its declared size.", code="PROTOCOL_ERROR")
                            handle.write(chunk)
                            digest.update(chunk)
                            count += len(chunk)
                            received += len(chunk)
                            if (count - durable >= CHECKPOINT_BYTES
                                    or time.monotonic() - last_checkpoint >= CHECKPOINT_SECONDS):
                                checkpoint(handle)
                                last_checkpoint = time.monotonic()
                    except BaseException:
                        checkpoint(handle)
                        raise
                    checkpoint(handle)
                if expected_length is not None and count - offset != expected_length:
                    raise error("Archive ended before its declared size. Resume this job's download.", code="NETWORK_ERROR")
                break
        if total is not None and count != total:
            raise error("Archive size differs from its checkpoint.", code="INTEGRITY_ERROR")
        if expected_sha is not None and digest.hexdigest() != expected_sha:
            raise error("Archive SHA-256 differs from the server snapshot.", code="INTEGRITY_ERROR")
        try:
            with zipfile.ZipFile(part) as archive:
                embedded = verify(archive, record, job_id)
        except (zipfile.BadZipFile, ValueError, KeyError, RuntimeError) as exc:
            raise error("Downloaded file is not a valid provenance ZIP.", code="INTEGRITY_ERROR") from exc
        safe_download_paths(output, job_id, error)
        os.replace(part, final)
        with contextlib.suppress(FileNotFoundError):
            sidecar.unlink()
        return {"generation_record": embedded, "archive_sha256": digest.hexdigest(),
                "archive_bytes": count, "archive_etag": transfer_etag,
                "resumed_bytes": resumed, "received_bytes": received,
                "transfer_seconds": round(time.monotonic() - started, 6)}
    except requests.Timeout as exc:
        raise error("Archive download timed out; resume with the same job ID and output directory.",
                    code="REQUEST_TIMEOUT", details=details()) from exc
    except requests.RequestException as exc:
        raise error("Archive download interrupted; resume with the same job ID and output directory.",
                    code="NETWORK_ERROR", details=details()) from exc
    except KeyboardInterrupt as exc:
        raise error("Archive download interrupted; resume with the same job ID and output directory.",
                    code="INTERRUPTED", details=details()) from exc
    except OSError as exc:
        raise error("Cannot write archive; check disk space and resume with the same output directory.",
                    code="LOCAL_IO_ERROR", details=details()) from exc
    except Exception as exc:
        if getattr(exc, "code", None) == "INTEGRITY_ERROR":
            _drop(part, sidecar, error)
            durable = 0
        if hasattr(exc, "details"):
            exc.details = {**(exc.details if isinstance(exc.details, dict) else {}), **details()}
        raise
