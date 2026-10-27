#!/usr/bin/env python3
"""Thin, resumable client for the existing Hunyuan3D web application's job API.

Python >= 3.10, requests >= 2.31, and the bundled modeling_contract.py,
modeling_collection.py, modeling_state.py and modeling_download.py modules are
the client dependencies. This file does not
import the server, torch, or any model runtime.
"""
from __future__ import annotations

import argparse
import contextlib
import getpass
import hashlib
import http.cookiejar
import json
import math
import mimetypes
import os
from pathlib import Path, PurePosixPath
import re
import sys
import tempfile
import time
import uuid
from html.parser import HTMLParser
from urllib.parse import quote, urlencode, urljoin, urlsplit
import zipfile

from modeling_contract import ModelingError, error_payload, http_error
from modeling_state import State, now, private_directory, atomic_json, read_json, cookie_signature, save_cookie_jar
from modeling_download import download_lock, safe_download_paths, transfer_archive

try:
    import requests
except ImportError:
    requests = None


SCHEMA_VERSION = 1
MODELS = {"hunyuan3d-2.1", "hunyuan3d-omni", "trellis2"}
OPTION_NAMES = {
    "model", "gpu_id", "seed", "remove_background", "texture",
    "trellis_pipeline", "trellis_steps", "trellis_texture_size",
    "num_inference_steps", "guidance_scale", "octree_resolution",
    "num_chunks", "texture_profile",
}
INTEGER_OPTIONS = {
    "seed": (0, 2**32 - 1), "trellis_steps": (1, 50),
    "trellis_texture_size": (4096, 8192), "num_inference_steps": (1, 100),
    "octree_resolution": (64, 512), "num_chunks": (1000, 300000),
}
JOB_ID = re.compile(r"^[A-Za-z0-9_-]{1,128}$")


class ClientError(ModelingError):
    """A client failure using the same stable error contract as the server."""


class WaitTimeout(ClientError):
    def __init__(self, jobs):
        super().__init__("Wait timed out; server jobs continue. Run status or wait with the preserved job ID.",
                         code="WAIT_TIMEOUT")
        self.jobs = jobs


class ArgumentParser(argparse.ArgumentParser):
    def error(self, message):
        # main handles this exception before any client or state is initialized.
        raise ClientError(message, code="INVALID_INPUT", details={"argument_error": True})


def normalize_server(value: str):
    parts = urlsplit(value.strip())
    try:
        port = parts.port
    except ValueError as exc:
        raise ClientError("Invalid server port.") from exc
    if (parts.scheme not in {"http", "https"} or not parts.hostname
            or parts.username or parts.password or parts.query or parts.fragment
            or parts.path not in {"", "/"}):
        raise ClientError("--server must be an http(s) origin without credentials, path, query, or fragment.")
    host = parts.hostname.lower()
    if ":" in host:
        host = f"[{host}]"
    default = 443 if parts.scheme == "https" else 80
    suffix = f":{port}" if port is not None and port != default else ""
    return f"{parts.scheme}://{host}{suffix}"


def validate_job_id(value, code="INVALID_INPUT"):
    if not isinstance(value, str) or not JOB_ID.fullmatch(value):
        raise ClientError("Invalid job ID.", code=code)
    return value


def validate_request_id(value):
    try:
        parsed = uuid.UUID(value)
        if str(parsed) != value.lower():
            raise ValueError()
        return str(parsed)
    except (ValueError, AttributeError) as exc:
        raise ClientError("--request-id must be a UUID, e.g. one printed by an earlier generate command.") from exc


def sha256_file(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_archive(archive, record, job_id):
    """Verify one ZIP snapshot; web rigging may change extras after GET /record."""
    names = archive.namelist()
    if len(names) != len(set(names)):
        raise ClientError("Archive contains duplicate filenames.", code="INTEGRITY_ERROR")
    if not {"model.glb", "generation-record.json"}.issubset(names):
        raise ClientError("Archive is missing model or provenance files.", code="INTEGRITY_ERROR")
    for item in archive.infolist():
        path = PurePosixPath(item.filename)
        if path.is_absolute() or ".." in path.parts or "\\" in item.filename or ":" in item.filename:
            raise ClientError("Archive contains an unsafe filename.", code="INTEGRITY_ERROR")
        if (item.external_attr >> 16) & 0o170000 == 0o120000:
            raise ClientError("Archive contains a symbolic link.", code="INTEGRITY_ERROR")
    if archive.getinfo("generation-record.json").file_size > 2 * 1024 * 1024:
        raise ClientError("Archive generation record is unexpectedly large.", code="INTEGRITY_ERROR")
    embedded = json.loads(archive.read("generation-record.json"))
    if not isinstance(embedded, dict) or embedded.get("id") != job_id or record.get("id") != job_id:
        raise ClientError("Archive generation record belongs to a different job.", code="INTEGRITY_ERROR")
    if embedded.get("request_id") != record.get("request_id"):
        raise ClientError("Archive generation record belongs to a different request.", code="INTEGRITY_ERROR")
    for key in ("inputs", "artifacts"):
        if not isinstance(embedded.get(key), list) or not embedded[key]:
            raise ClientError("Archive generation record is missing input or artifact hashes.", code="INTEGRITY_ERROR")
    expected = {}
    for key in ("inputs", "artifacts"):
        for entry in embedded[key]:
            if not isinstance(entry, dict):
                raise ClientError("Generation record contains an invalid artifact entry.", code="INTEGRITY_ERROR")
            name = entry.get("archive_name") if key == "inputs" else entry.get("name")
            if not isinstance(name, str) or name in expected:
                raise ClientError("Generation record contains invalid or duplicate artifact names.", code="INTEGRITY_ERROR")
            expected[name] = entry
    if set(names) != set(expected) | {"generation-record.json"}:
        raise ClientError("Archive contains missing or unrecorded artifacts.", code="INTEGRITY_ERROR")
    for name, entry in expected.items():
        if archive.getinfo(name).file_size != entry.get("size"):
            raise ClientError(f"Archive size differs from generation record: {name}", code="INTEGRITY_ERROR")
        digest = hashlib.sha256()
        with archive.open(name) as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
        if digest.hexdigest() != entry.get("sha256"):
            raise ClientError(f"Archive SHA-256 differs from generation record: {name}", code="INTEGRITY_ERROR")
    return embedded


def positive(value):
    number = float(value)
    if not math.isfinite(number) or number <= 0:
        raise argparse.ArgumentTypeError("must be a finite number greater than zero")
    return number


def poll_interval(value):
    number = positive(value)
    if number < 1:
        raise argparse.ArgumentTypeError("must be at least 1 second")
    return number


def nonnegative(value):
    number = int(value)
    if number < 0:
        raise argparse.ArgumentTypeError("must be nonnegative")
    return number


def generation_options(args):
    options = {}
    for pair in args.option:
        if "=" not in pair:
            raise ClientError("--option requires KEY=VALUE.")
        key, value = pair.split("=", 1)
        if key not in OPTION_NAMES:
            raise ClientError(f"Unsupported option {key!r}. See generate --help or CLI-A100.md.")
        if key in options:
            raise ClientError(f"Duplicate option: {key}")
        options[key] = value
    for key, value in (("model", args.model), ("seed", args.seed), ("gpu_id", args.gpu)):
        if value is not None:
            if key in options:
                raise ClientError(f"Supply {key} once, using its flag or --option.")
            options[key] = str(value)
    for key, value in options.items():
        if key in INTEGER_OPTIONS:
            low, high = INTEGER_OPTIONS[key]
            try:
                integer = int(value)
            except ValueError as exc:
                raise ClientError(f"{key} must be an integer.") from exc
            if not low <= integer <= high:
                raise ClientError(f"{key} must be between {low} and {high}.")
            if key == "trellis_texture_size" and integer not in {4096, 8192}:
                raise ClientError("trellis_texture_size must be 4096 or 8192.")
            options[key] = str(integer)
        elif key in {"remove_background", "texture"}:
            if value not in {"on", "off"}:
                raise ClientError(f"{key} must be on or off.")
        elif key == "model" and value not in MODELS:
            raise ClientError("Unsupported model.")
        elif key == "gpu_id" and value not in {"auto", "split"}:
            if not value.isdecimal():
                raise ClientError("gpu_id must be auto, split, or a nonnegative GPU index.")
        elif key == "trellis_pipeline" and value not in {"512", "1024_cascade", "1536_cascade"}:
            raise ClientError("Unsupported trellis_pipeline.")
        elif key == "texture_profile" and value not in {"standard", "detail", "ultra"}:
            raise ClientError("texture_profile must be standard, detail, or ultra.")
        elif key == "guidance_scale":
            try:
                number = float(value)
            except ValueError as exc:
                raise ClientError("guidance_scale must be a number.") from exc
            if not math.isfinite(number) or not 0.1 <= number <= 20:
                raise ClientError("guidance_scale must be between 0.1 and 20.")
    return options


class LoginForm(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.hidden = {}

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag.lower() == "input" and values.get("type", "").lower() == "hidden":
            if values.get("name") in {"csrf", "next"}:
                self.hidden[values["name"]] = values.get("value", "")


class Client:
    def __init__(self, state: State, timeout):
        if requests is None:
            raise ClientError("Install the client dependency: python -m pip install 'requests>=2.31,<3'", code="LOCAL_IO_ERROR")
        self.state = state
        self.server = state.server
        self.timeout = timeout
        self.session = requests.Session()
        # Never send ambient .netrc credentials or inherit a workstation proxy.
        self.session.trust_env = False
        self.session.headers.update({"X-Client-Type": "cli", "User-Agent": "hunyuan3d-cli/1.1"})
        self.jar = http.cookiejar.LWPCookieJar(str(state.cookies))
        if state.cookies.exists():
            try:
                self.jar.load(ignore_discard=True, ignore_expires=False)
            except (OSError, http.cookiejar.LoadError) as exc:
                raise ClientError("Cannot read saved session. Move aside cookies.lwp and log in again.", code="LOCAL_IO_ERROR") from exc
        self.session.cookies = self.jar
        self._cookie_signature = cookie_signature(self.jar)
        self._auth_in_progress = False

    def credential_target(self):
        return "Hunyuan3D-CLI:" + hashlib.sha256(self.server.encode()).hexdigest()

    def saved_password(self):
        if os.name != "nt":
            return None
        try:
            import win32cred
            import pywintypes
        except ImportError:
            return None
        try:
            entry = win32cred.CredRead(self.credential_target(), win32cred.CRED_TYPE_GENERIC)
        except pywintypes.error as exc:
            if exc.winerror == 1168:
                return None
            raise ClientError("Cannot read Windows Credential Manager.", code="LOCAL_IO_ERROR") from exc
        return bytes(entry["CredentialBlob"]).decode("utf-16-le")

    def remember_password(self, password):
        if os.name != "nt":
            return False
        try:
            import win32cred
        except ImportError as exc:
            raise ClientError("Install pywin32 to remember login in Windows Credential Manager.", code="LOCAL_IO_ERROR") from exc
        win32cred.CredWrite({"Type": win32cred.CRED_TYPE_GENERIC,
                             "TargetName": self.credential_target(),
                             "UserName": "hunyuan3d-workspace",
                             "CredentialBlob": password,
                             "Persist": win32cred.CRED_PERSIST_LOCAL_MACHINE}, 0)
        return True

    def url(self, path):
        if not isinstance(path, str) or "\\" in path:
            raise ClientError("Server returned an invalid URL.", code="PROTOCOL_ERROR")
        target = urljoin(self.server + "/", path)
        parts = urlsplit(target)
        if parts.username or parts.password or parts.fragment:
            raise ClientError("Refusing a URL containing credentials or a fragment.", code="PROTOCOL_ERROR")
        origin = normalize_server(f"{parts.scheme}://{parts.netloc}")
        if origin != self.server:
            raise ClientError("Refusing a redirect or download URL to another origin.", code="PROTOCOL_ERROR")
        return target

    def save_cookies(self):
        self._cookie_signature = save_cookie_jar(self.jar, self.state.cookies, self._cookie_signature)

    def request(self, method, path, *, login_redirects=False, timeout=None, _auth_retry=True, **kwargs):
        if method.upper() not in {"GET", "HEAD", "OPTIONS"} and not self._auth_in_progress and not path.startswith("/access/") and self.saved_password():
            self.json("GET", "/api/jobs?limit=1")
        url = self.url(path)
        headers = dict(kwargs.pop("headers", {}))
        if method.upper() not in {"GET", "HEAD", "OPTIONS"}:
            headers["Origin"] = self.server
        for hop in range(6):
            try:
                response = self.session.request(method, url, headers=headers, allow_redirects=False,
                                                timeout=timeout or self.timeout, **kwargs)
            except requests.Timeout as exc:
                raise ClientError("Request timed out; preserved request IDs can be resumed.", code="REQUEST_TIMEOUT") from exc
            except requests.RequestException as exc:
                raise ClientError(f"Request failed ({type(exc).__name__}); preserved request IDs can be resumed.", code="NETWORK_ERROR") from exc
            self.save_cookies()
            if response.status_code == 401 and method.upper() in {"GET", "HEAD"} and not self._auth_in_progress and _auth_retry:
                try:
                    payload = response.json()
                except ValueError:
                    payload = None
                if isinstance(payload, dict) and payload.get("error") == "authentication_required":
                    password = self.saved_password()
                    if password:
                        response.close()
                        self._auth_in_progress = True
                        try:
                            self.login(password_value=password)
                        finally:
                            self._auth_in_progress = False
                            password = None
                        return self.request(method, path, login_redirects=login_redirects, timeout=timeout, _auth_retry=False, **kwargs)
            if response.status_code not in {301, 302, 303, 307, 308}:
                return response
            location = response.headers.get("Location", "")
            try:
                next_url = self.url(urljoin(url, location))
            finally:
                response.close()
            if not login_redirects:
                if urlsplit(next_url).path.startswith("/access/login"):
                    raise ClientError("Login required. Run the login command for this server.", code="AUTH_REQUIRED")
                raise ClientError("Unexpected redirect; use the server's final origin.", code="PROTOCOL_ERROR")
            if response.status_code in {307, 308} and method.upper() != "GET":
                raise ClientError("Refusing to replay login credentials through a redirect.", code="PROTOCOL_ERROR")
            method, url, kwargs = "GET", next_url, {}
            headers = {}
        raise ClientError("Too many same-origin login redirects.", code="PROTOCOL_ERROR")

    @staticmethod
    def check(response):
        if not response.ok:
            body = None
            with contextlib.suppress(ValueError):
                candidate = response.json()
                if isinstance(candidate, dict):
                    body = candidate
            raise http_error(response.status_code, payload=body,
                             path=urlsplit(getattr(response, "url", "")).path,
                             retry_after=response.headers.get("Retry-After"))

    def json(self, method, path, **kwargs):
        with self.request(method, path, **kwargs) as response:
            self.check(response)
            try:
                result = response.json()
            except ValueError as exc:
                raise ClientError("Expected JSON from the job API; check the server origin and login.", code="PROTOCOL_ERROR") from exc
            if not isinstance(result, dict):
                raise ClientError("Unexpected API response shape.", code="PROTOCOL_ERROR")
            targets = [result]
            if isinstance(result.get("job"), dict):
                targets.append(result["job"])
            if isinstance(result.get("jobs"), list):
                targets.extend(job for job in result["jobs"] if isinstance(job, dict))
            for target in targets:
                if target.get("web_url"):
                    target["web_url"] = self.url(target["web_url"])
            return result

    def login(self, password_file=None, password_value=None, remember=False):
        if remember:
            if os.name != "nt":
                raise ClientError("Remembered login is currently supported only on Windows.", code="LOCAL_IO_ERROR")
            try:
                import win32cred
            except ImportError as exc:
                raise ClientError("Install pywin32 to remember login in Windows Credential Manager.", code="LOCAL_IO_ERROR") from exc
            # Obtain a fresh login form so the remembered password is actually verified.
            self.jar.clear()
        with self.request("GET", "/access/login?next=%2Fapi%2Fjobs", login_redirects=True) as response:
            if response.status_code == 404:
                self.json("GET", "/api/jobs?limit=1")
                atomic_json(self.state.root / "config.json", {"server": self.server})
                return {"server": self.server, "authenticated": True,
                        "message": "Direct API access; no gateway login required."}
            self.check(response)
            if "application/json" in response.headers.get("Content-Type", ""):
                return {"server": self.server, "authenticated": True, "message": "Existing session is valid."}
            form = LoginForm()
            form.feed(response.text)
        csrf = form.hidden.get("csrf")
        if not csrf:
            # Direct internal app URLs have no private gateway login form.
            self.json("GET", "/api/jobs?limit=1")
            return {"server": self.server, "authenticated": True, "message": "Direct API access; no gateway login required."}
        if password_value is not None:
            password = password_value
        elif password_file:
            password = Path(password_file).expanduser().read_text(encoding="utf-8").rstrip("\r\n")
        else:
            password = getpass.getpass("Workspace password: ")
        if not password or len(password) > 256:
            raise ClientError("Password must contain 1 to 256 characters.")
        try:
            with self.request("POST", "/access/login", login_redirects=True,
                              data={"password": password, "csrf": csrf, "next": "/api/jobs?limit=1"}) as response:
                self.check(response)
                if "/access/login" in urlsplit(response.url).path:
                    raise ClientError("Login failed.", code="AUTH_REJECTED")
            if remember:
                self.remember_password(password)
        finally:
            password = None
        self.json("GET", "/api/jobs?limit=1")
        atomic_json(self.state.root / "config.json", {"server": self.server})
        return {"server": self.server, "authenticated": True}

    def status(self, job_id, timeout=None):
        result = self.json("GET", f"/api/jobs/{quote(validate_job_id(job_id))}", timeout=timeout)
        # Existing API returns the public job directly; accept an explicit wrapper too.
        job = result.get("job", result)
        if not isinstance(job, dict) or job.get("id") != job_id or not isinstance(job.get("status"), str):
            raise ClientError("Unexpected job status response.", code="PROTOCOL_ERROR")
        self.state.remember_job(job)
        return job

    def capabilities(self):
        try:
            result = self.json("GET", "/api/capabilities")
        except ModelingError as exc:
            if exc.http_status == 404:
                raise ClientError("This server does not support capabilities. Update the server CLI/API.", code="UNSUPPORTED_API", http_status=404) from exc
            raise
        if result.get("schema_version") != 1 or not isinstance(result.get("models"), dict):
            raise ClientError("Unsupported capabilities schema; update the client and server together.", code="UNSUPPORTED_API")
        return result

    def doctor(self):
        try:
            result = self.json("GET", "/api/doctor")
        except ModelingError as exc:
            if exc.http_status == 404:
                raise ClientError("This server does not support doctor. Update the server CLI/API.", code="UNSUPPORTED_API", http_status=404) from exc
            raise
        if result.get("schema_version") != 1 or not isinstance(result.get("ok"), bool) or not isinstance(result.get("checks"), dict):
            raise ClientError("Unsupported doctor schema; update the client and server together.", code="UNSUPPORTED_API")
        return result

    def request_jobs(self, request_id):
        """Recover every remote job for one UUID, including pages after 200."""
        request_id = validate_request_id(request_id)
        jobs, seen, offset = [], set(), 0
        while True:
            result = self.json("GET", "/api/jobs?" + urlencode({
                "request_id": request_id, "limit": 200, "offset": offset}))
            page = result.get("jobs")
            if not isinstance(page, list):
                raise ClientError("Unexpected request history response.", code="PROTOCOL_ERROR")
            for job in page:
                if (not isinstance(job, dict) or job.get("request_id") != request_id
                        or not isinstance(job.get("status"), str)):
                    raise ClientError("Server returned a job outside the requested UUID.", code="PROTOCOL_ERROR")
                job_id = validate_job_id(job.get("id"), code="PROTOCOL_ERROR")
                if job_id in seen:
                    raise ClientError("Request history pagination repeated a job.", code="PROTOCOL_ERROR")
                seen.add(job_id)
                jobs.append(job)
            offset += len(page)
            total = result.get("total")
            if isinstance(total, int) and total >= 0:
                if offset >= total:
                    break
                if not page:
                    raise ClientError("Request history pagination ended before its total.", code="PROTOCOL_ERROR")
            elif len(page) < 200:
                break
        return jobs

    def collect(self, request_id, output, *, wait=False, timeout=86400, interval=5,
                refresh=False, jobs=None):
        from modeling_collection import collect_request
        return collect_request(self, request_id, output, wait=wait, timeout=timeout,
                               interval=interval, refresh=refresh, jobs=jobs)

    def resume(self, request_id, output, *, timeout=86400, interval=5, refresh=False):
        from modeling_collection import resume_request
        return resume_request(self, request_id, output, timeout=timeout,
                              interval=interval, refresh=refresh)

    def generate(self, manifest):
        path = self.state.manifest_path(manifest["request_id"]).parent
        manifest["state"] = "submitting"
        self.state.write_manifest(manifest)
        with contextlib.ExitStack() as stack:
            files = [("images", (item["filename"], stack.enter_context((path / item["local_path"]).open("rb")),
                                  mimetypes.guess_type(item["filename"])[0] or "application/octet-stream"))
                     for item in manifest["inputs"]]
            result = self.json("POST", "/api/jobs", data=manifest["submitted_options"], files=files,
                               headers={"Idempotency-Key": manifest["request_id"]})
        if result.get("request_id") != manifest["request_id"] or not isinstance(result.get("jobs"), list) or not result["jobs"]:
            raise ClientError("Unexpected create response; request ID is preserved. Check history --remote before retrying.", code="PROTOCOL_ERROR")
        for job in result["jobs"]:
            if not isinstance(job, dict) or not isinstance(job.get("status"), str):
                raise ClientError("Unexpected job in create response; the request ID is preserved.", code="PROTOCOL_ERROR")
            validate_job_id(job.get("id"), code="PROTOCOL_ERROR")
        manifest.update(state="submitted", jobs=result["jobs"], batch_id=result.get("batch_id"),
                        web_url=result.get("web_url"), replayed=bool(result.get("replayed")))
        self.state.write_manifest(manifest)
        return result

    def wait(self, job_ids, timeout, interval):
        deadline = time.monotonic() + timeout
        latest = {}
        shown = {}
        def snapshot():
            return [latest.get(job_id, {"id": job_id, "status": "unknown"}) for job_id in job_ids]
        while True:
            for job_id in job_ids:
                if latest.get(job_id, {}).get("status") in {"completed", "failed", "cancelled", "canceled"}:
                    continue
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise WaitTimeout(snapshot())
                try:
                    job = self.status(job_id, timeout=min(self.timeout, remaining))
                except ModelingError:
                    if time.monotonic() >= deadline:
                        raise WaitTimeout(snapshot()) from None
                    raise
                latest[job_id] = job
                marker = (job["status"], job.get("message"))
                if marker != shown.get(job_id):
                    print(f"{job_id}: {job['status']} {job.get('message', '')}", file=sys.stderr)
                    shown[job_id] = marker
            if len(latest) == len(job_ids) and all(job["status"] in {"completed", "failed", "cancelled", "canceled"} for job in latest.values()):
                return snapshot(), 2 if any(job["status"] != "completed" for job in latest.values()) else 0
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise WaitTimeout(snapshot())
            time.sleep(min(interval, remaining))

    def cached_download(self, job_id, output, job=None):
        """Only reuse an archive whose same-origin receipt and full hash verify."""
        output = Path(output).expanduser().resolve()
        final, receipt_path = output / f"{job_id}.zip", output / f"{job_id}.download.json"
        safe_download_paths(output, job_id, ClientError)
        try:
            receipt = read_json(receipt_path)
            record = receipt.get("generation_record", {})
            if (not isinstance(record, dict) or receipt.get("schema_version") != 1 or receipt.get("server") != self.server
                    or receipt.get("job_id") != job_id or not final.is_file()
                    or Path(receipt.get("archive", "")).resolve() != final
                    or final.stat().st_size != receipt.get("archive_bytes")
                    or sha256_file(final) != receipt.get("archive_sha256")
                    or (job and job.get("request_id") != record.get("request_id"))):
                return None
            with zipfile.ZipFile(final) as archive:
                verify_archive(archive, record, job_id)
        except (ModelingError, OSError, ValueError, TypeError, KeyError, RuntimeError, zipfile.BadZipFile):
            return None
        return {"job": job or {"id": job_id, "status": "completed", "request_id": record.get("request_id")},
                "archive": str(final), "receipt": str(receipt_path),
                "archive_sha256": receipt["archive_sha256"], "archive_bytes": receipt["archive_bytes"],
                "archive_etag": receipt.get("archive_etag"), "resumed_bytes": 0, "received_bytes": 0,
                "skipped": True}

    def download(self, job_id, output, *, refresh=True, job=None):
        job_id = validate_job_id(job_id)
        output = Path(output).expanduser().resolve()
        with download_lock(output, job_id, ClientError, self.timeout):
            return self._download_locked(job_id, output, refresh=refresh, job=job)

    def _download_locked(self, job_id, output, *, refresh=True, job=None):
        if not refresh:
            cached = self.cached_download(job_id, output, job)
            if cached:
                return cached, 0
        job = job or self.status(job_id)
        if job["status"] in {"failed", "cancelled", "canceled"}:
            return {**error_payload("JOB_FAILED", "Job failed; no completed archive is available."), "job": job}, 2
        if job["status"] != "completed":
            raise ClientError("Job is not completed. Run wait first, or use generate --download.", code="JOB_NOT_READY")
        record = self.json("GET", job.get("record_url") or f"/api/jobs/{job_id}/record")
        final = output / f"{job_id}.zip"
        transfer = transfer_archive(self, job_id, output, record,
                                    job.get("archive_url") or f"/api/jobs/{job_id}/archive",
                                    verify=verify_archive, atomic_json=atomic_json, error=ClientError)
        receipt = {"schema_version": SCHEMA_VERSION, "server": self.server, "job_id": job_id,
                   "downloaded_at": now(), "archive": str(final), **transfer}
        receipt_path = output / f"{job_id}.download.json"
        atomic_json(receipt_path, receipt)
        atomic_json(self.state.records / f"{job_id}.json", receipt)
        request_id = transfer["generation_record"].get("request_id")
        try:
            request_id = validate_request_id(request_id)
        except ClientError:
            # Web/API jobs predate the CLI UUID contract. Their verified result
            # and receipt remain downloadable without inventing a CLI manifest.
            request_id = None
        if request_id and hasattr(self.state, "load_manifest"):
            manifest = self.state.load_manifest(request_id) if self.state.manifest_path(request_id).exists() else None
            manifests = [manifest] if manifest else []
        elif not request_id:
            manifests = []
        else:
            manifests = self.state.manifests()
        for manifest in manifests:
            if any(item.get("id") == job_id for item in manifest.get("jobs", [])):
                manifest["downloads"] = [item for item in manifest.get("downloads", []) if item.get("job_id") != job_id]
                manifest["downloads"].append({key: value for key, value in receipt.items() if key != "generation_record"})
                self.state.write_manifest(manifest)
        return {"job": job, "archive": str(final), "receipt": str(receipt_path),
                **{key: value for key, value in transfer.items() if key != "generation_record"}, "skipped": False}, 0


def parser():
    common = argparse.ArgumentParser(add_help=False)
    # SUPPRESS lets global options appear before or after the subcommand.
    common.add_argument("--server", default=argparse.SUPPRESS, help="API origin; or MODELING_SERVER / saved login config")
    common.add_argument("--state-dir", default=argparse.SUPPRESS, help="private local state (default: ~/.hunyuan3d-cli)")
    common.add_argument("--json", action="store_true", default=argparse.SUPPRESS, help="JSON results on stdout; progress remains on stderr")
    common.add_argument("--request-timeout", type=positive, default=argparse.SUPPRESS, help="HTTP connect/read inactivity timeout seconds (default: 120)")
    root = ArgumentParser(description=__doc__, parents=[common])
    commands = root.add_subparsers(dest="command", required=True)
    commands.add_parser("capabilities", parents=[common], help="inspect server models, defaults, limits, and supported API features")
    commands.add_parser("doctor", parents=[common], help="check authentication, worker/GPU readiness, and runtime health")
    login = commands.add_parser("login", parents=[common], help="log in through the existing private gateway")
    login.add_argument("--password-file", help="read password from a private UTF-8 file; default: hidden terminal prompt")
    login.add_argument("--remember", action="store_true", help="save password to Windows Credential Manager for automatic re-login")
    for command in ("status", "wait", "download"):
        child = commands.add_parser(command, parents=[common], help=f"{command} an existing server job")
        child.add_argument("job", help="preserved server job ID")
        if command == "wait":
            child.add_argument("--timeout", type=positive, default=86400, help="total wait budget in seconds (default: 86400)")
            child.add_argument("--poll-interval", type=poll_interval, default=5, help="seconds, at least 1 (default: 5)")
        if command == "download":
            child.add_argument("--output", required=True, help="directory for provenance ZIP and download receipt")
    for command in ("collect", "resume"):
        child = commands.add_parser(command, parents=[common], help="recover every job in an existing request and collect completed archives")
        child.add_argument("request", help="preserved request UUID; works on another client using the same server")
        child.add_argument("--output", required=True, help="directory for provenance ZIPs and download receipts")
        if command == "collect":
            child.add_argument("--wait", action="store_true", help="continue tracking every job until all are terminal")
        child.add_argument("--timeout", type=positive, default=86400, help="total wait budget in seconds (default: 86400)")
        child.add_argument("--poll-interval", type=poll_interval, default=5, help="seconds, at least 1 (default: 5)")
        child.add_argument("--refresh", action="store_true", help="download again, including new web rigging outputs, instead of reusing verified archives")
    generate = commands.add_parser("generate", parents=[common], help="submit images to the same queue used by the web UI")
    generate.add_argument("images", nargs="*", help="PNG/JPEG/WebP files; omit when resuming a local request ID")
    generate.add_argument("--model", choices=sorted(MODELS), help="omit to retain the server default")
    generate.add_argument("--seed", type=int, help="0 through 4294967295; omit to retain the server default")
    generate.add_argument("--gpu", help="auto, split, or GPU index; omit to retain the server default")
    generate.add_argument("--option", action="append", default=[], metavar="KEY=VALUE", help="repeatable; allowed: " + ", ".join(sorted(OPTION_NAMES)))
    generate.add_argument("--request-id", help="UUID; reuse to recover an uncertain submission without duplicate inference")
    generate.add_argument("--wait", action="store_true", help="track every job until completed, failed, or cancelled")
    generate.add_argument("--download", metavar="DIR", help="wait and save completed provenance ZIPs, including partial batch successes")
    generate.add_argument("--timeout", type=positive, default=86400, help="total wait budget in seconds (default: 86400)")
    generate.add_argument("--poll-interval", type=poll_interval, default=5, help="seconds, at least 1 (default: 5)")
    history = commands.add_parser("history", parents=[common], help="show local request manifests, or server CLI job history")
    history.add_argument("--remote", action="store_true", help="query server's persistent CLI job history")
    history.add_argument("--request-id", help="filter one UUID (also recovers a submitted ID from another client)")
    history.add_argument("--limit", type=nonnegative, default=50, help="local/remote page size, 1 to 200 (default: 50)")
    history.add_argument("--offset", type=nonnegative, default=0, help="local/remote pagination offset")
    return root


def render(result, json_output):
    result = {"schema_version": SCHEMA_VERSION, **result}
    if json_output:
        print(json.dumps(result, ensure_ascii=False, allow_nan=False))
        return
    if result.get("error"):
        print(result["error"])
        if result.get("code"):
            print(f"Code: {result['code']}  Retryable: {str(result.get('retryable', False)).lower()}")
        if result.get("next_action"):
            print(f"Next: {result['next_action']}")
    if "checks" in result:
        print(f"Server: {result.get('status', 'unknown')}")
        for name, check in result["checks"].items():
            print(f"  {name}: {json.dumps(check, ensure_ascii=False)}")
    if "models" in result:
        print(f"API: {result.get('api_version', 'unknown')}")
        for name, model in result["models"].items():
            available = model.get("available") if isinstance(model, dict) else model
            print(f"  {name}: {'available' if available else 'unavailable'}")
    if "archive" in result:
        print(f"Archive: {result['archive']}\nSHA-256: {result['archive_sha256']}\nReceipt: {result['receipt']}")
    if "request_id" in result:
        print(f"Request: {result['request_id']}")
    if "authenticated" in result:
        print(f"Ready: {result['server']}")
    jobs = result.get("jobs")
    if jobs is None and isinstance(result.get("job"), dict):
        jobs = [result["job"]]
    if jobs:
        for job in jobs:
            print(f"{job['id']}  {job['status']}  {job.get('source_filename', '')}")
            if job.get("web_url"):
                print(f"  Web: {job['web_url']}")
            if job.get("message"):
                print(f"  {job['message']}")
    for manifest in result.get("requests", []):
        print(f"{manifest['request_id']}  {manifest['state']}  {manifest['created_at']}")
        for job in manifest.get("jobs", []):
            print(f"  {job['id']}  {job['status']}")
    if result.get("total") is not None:
        print(f"Total: {result['total']}")
    for download in result.get("downloads", []):
        print(f"Archive: {download['archive']}" + (" (verified; reused)" if download.get("skipped") else ""))
    if result.get("summary"):
        print("Summary: " + json.dumps(result["summary"], ensure_ascii=False))


def main(argv=None):
    arguments = list(sys.argv[1:] if argv is None else argv)
    json_output = "--json" in arguments
    request_id = None
    try:
        args = parser().parse_args(arguments)
        json_output = getattr(args, "json", False)
        state_root = Path(getattr(args, "state_dir", "~/.hunyuan3d-cli")).expanduser().resolve()
        configured = state_root / "config.json"
        server = getattr(args, "server", None) or os.environ.get("MODELING_SERVER")
        if not server and configured.exists():
            server = read_json(configured).get("server")
        if not server:
            raise ClientError("Set --server or MODELING_SERVER to the existing webapp origin.")
        state = State(state_root, normalize_server(server))
        if args.command == "history" and not args.remote:
            if not 1 <= args.limit <= 200:
                raise ClientError("--limit must be between 1 and 200.")
            if args.request_id:
                request_id = validate_request_id(args.request_id)
                manifests = ([state.load_manifest(request_id)]
                             if state.manifest_path(request_id).is_file() else [])
                page = {"requests": manifests, "total": len(manifests)}
            else:
                page = state.manifest_page(args.limit, args.offset)
            render({"server": state.server, **page}, json_output)
            return 0
        client = Client(state, getattr(args, "request_timeout", 120))
        if args.command == "login":
            result, code = client.login(args.password_file, remember=args.remember), 0
            # Remember a direct internal origin as well as a gateway login.
            atomic_json(state.root / "config.json", {"server": state.server})
        elif args.command == "capabilities":
            result, code = client.capabilities(), 0
        elif args.command == "doctor":
            result = client.doctor()
            code = 0 if result["ok"] else 1
            if code:
                result.update(error_payload("SERVER_NOT_READY"))
        elif args.command == "status":
            job = client.status(args.job)
            result, code = {"job": job}, 2 if job["status"] in {"failed", "cancelled", "canceled"} else 0
            if code:
                result.update(error_payload("JOB_FAILED"))
        elif args.command == "wait":
            jobs, code = client.wait([validate_job_id(args.job)], args.timeout, args.poll_interval)
            result = {"jobs": jobs}
            if code:
                result.update(error_payload("JOB_FAILED"))
        elif args.command == "download":
            result, code = client.download(validate_job_id(args.job), args.output)
        elif args.command in {"collect", "resume"}:
            request_id = validate_request_id(args.request)
            if args.command == "resume":
                result, code = client.resume(request_id, args.output, timeout=args.timeout,
                                             interval=args.poll_interval, refresh=args.refresh)
            else:
                result, code = client.collect(request_id, args.output, wait=args.wait,
                                              timeout=args.timeout, interval=args.poll_interval,
                                              refresh=args.refresh)
        elif args.command == "history":
            if not 1 <= args.limit <= 200:
                raise ClientError("--limit must be between 1 and 200.")
            query = {"source": "cli", "limit": args.limit, "offset": args.offset}
            if args.request_id:
                request_id = validate_request_id(args.request_id)
                query["request_id"] = request_id
            result, code = client.json("GET", "/api/jobs?" + urlencode(query)), 0
        else:
            options = generation_options(args)
            request_id = validate_request_id(args.request_id) if args.request_id else str(uuid.uuid4())
            manifest = state.prepare(request_id, args.images, options)
            print(f"Preserved request ID: {request_id}", file=sys.stderr)
            result = client.generate(manifest)
            print("Job IDs: " + ", ".join(job["id"] for job in result["jobs"]), file=sys.stderr)
            code = 0
            if args.wait or args.download:
                collected, code = client.collect(request_id, args.download, wait=True,
                                                 timeout=args.timeout, interval=args.poll_interval,
                                                 jobs=result["jobs"])
                result.update(collected)
        render(result, json_output)
        return code
    except WaitTimeout as exc:
        result = {**exc.as_dict(request_id=request_id), "jobs": exc.jobs}
        render(result, json_output)
        return 3
    except (ModelingError, OSError, ValueError) as exc:
        if isinstance(exc, ModelingError):
            result = exc.as_dict(request_id=request_id)
        else:
            result = error_payload("LOCAL_IO_ERROR" if isinstance(exc, OSError) else "INVALID_INPUT",
                                   str(exc), request_id=request_id)
        if json_output:
            render(result, True)
        else:
            print(f"Error: {exc}", file=sys.stderr)
            print(f"Code: {result['code']}  Next: {result['next_action']}", file=sys.stderr)
        return 2 if result.get("details", {}).get("argument_error") else 1
    except KeyboardInterrupt:
        result = error_payload("INTERRUPTED", "Interrupted locally; server jobs continue. IDs and inputs remain in local history.",
                               request_id=request_id)
        if json_output:
            render(result, True)
        else:
            print(result["error"], file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
