#!/usr/bin/env python3
"""Thin, resumable client for the existing Hunyuan3D web application's job API.

Python >= 3.10 and requests >= 2.31 are the only client dependencies. This file
does not import the server, torch, or any model runtime.
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


class ClientError(Exception):
    pass


class WaitTimeout(ClientError):
    def __init__(self, jobs):
        super().__init__("Wait timed out; server jobs continue. Run status or wait with the preserved job ID.")
        self.jobs = jobs


def now():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def private_directory(path: Path):
    path.mkdir(parents=True, exist_ok=True)
    if os.name != "nt":
        path.chmod(0o700)
    return path


def atomic_json(path: Path, value):
    """Write one whole document; never leave a half-written manifest/cookie file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=".writing-", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            if os.name != "nt":
                os.fchmod(handle.fileno(), 0o600)
            json.dump(value, handle, ensure_ascii=False, indent=2, allow_nan=False)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        with contextlib.suppress(FileNotFoundError):
            os.unlink(temporary)


def read_json(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise ClientError(f"Cannot read local JSON file: {path}") from exc


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


def validate_job_id(value):
    if not isinstance(value, str) or not JOB_ID.fullmatch(value):
        raise ClientError("Invalid job ID.")
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
        raise ClientError("Archive contains duplicate filenames.")
    if not {"model.glb", "generation-record.json"}.issubset(names):
        raise ClientError("Archive is missing model or provenance files.")
    for item in archive.infolist():
        path = PurePosixPath(item.filename)
        if path.is_absolute() or ".." in path.parts or "\\" in item.filename or ":" in item.filename:
            raise ClientError("Archive contains an unsafe filename.")
        if (item.external_attr >> 16) & 0o170000 == 0o120000:
            raise ClientError("Archive contains a symbolic link.")
    if archive.getinfo("generation-record.json").file_size > 2 * 1024 * 1024:
        raise ClientError("Archive generation record is unexpectedly large.")
    embedded = json.loads(archive.read("generation-record.json"))
    if not isinstance(embedded, dict) or embedded.get("id") != job_id or record.get("id") != job_id:
        raise ClientError("Archive generation record belongs to a different job.")
    if embedded.get("request_id") != record.get("request_id"):
        raise ClientError("Archive generation record belongs to a different request.")
    for key in ("inputs", "artifacts"):
        if not isinstance(embedded.get(key), list) or not embedded[key]:
            raise ClientError("Archive generation record is missing input or artifact hashes.")
    expected = {}
    for key in ("inputs", "artifacts"):
        for entry in embedded[key]:
            if not isinstance(entry, dict):
                raise ClientError("Generation record contains an invalid artifact entry.")
            name = entry.get("archive_name") if key == "inputs" else entry.get("name")
            if not isinstance(name, str) or name in expected:
                raise ClientError("Generation record contains invalid or duplicate artifact names.")
            expected[name] = entry
    if set(names) != set(expected) | {"generation-record.json"}:
        raise ClientError("Archive contains missing or unrecorded artifacts.")
    for name, entry in expected.items():
        if archive.getinfo(name).file_size != entry.get("size"):
            raise ClientError(f"Archive size differs from generation record: {name}")
        digest = hashlib.sha256()
        with archive.open(name) as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
        if digest.hexdigest() != entry.get("sha256"):
            raise ClientError(f"Archive SHA-256 differs from generation record: {name}")
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


class State:
    def __init__(self, root: Path, server: str):
        self.root = private_directory(root.expanduser().resolve())
        self.server = server
        namespace = hashlib.sha256(server.encode()).hexdigest()[:24]
        self.directory = private_directory(self.root / "servers" / namespace)
        self.requests = private_directory(self.directory / "requests")
        self.records = private_directory(self.directory / "records")
        self.cookies = self.directory / "cookies.lwp"

    def manifest_path(self, request_id):
        return self.requests / validate_request_id(request_id) / "manifest.json"

    def manifests(self):
        return [read_json(path) for path in sorted(self.requests.glob("*/manifest.json"))]

    def write_manifest(self, manifest):
        manifest["updated_at"] = now()
        atomic_json(self.manifest_path(manifest["request_id"]), manifest)

    def prepare(self, request_id, images, options):
        """Persist the exact submitted bytes and request key before network I/O."""
        path = self.manifest_path(request_id)
        if path.exists():
            manifest = read_json(path)
            if manifest.get("server") != self.server:
                raise ClientError("Stored request belongs to another server.")
            if options and options != manifest["submitted_options"]:
                raise ClientError("Request ID already has different options. Reuse without options, or use a new UUID.")
            if images:
                supplied = [(Path(p).name, sha256_file(Path(p).expanduser())) for p in images]
                original = [(item["filename"], item["sha256"]) for item in manifest["inputs"]]
                if supplied != original:
                    raise ClientError("Request ID already has different images. Use a new UUID for a new request.")
            for item in manifest["inputs"]:
                copied = path.parent / item["local_path"]
                if not copied.is_file() or sha256_file(copied) != item["sha256"]:
                    raise ClientError("Preserved input is missing or modified; refusing to alter an existing request.")
            return manifest
        if not images:
            raise ClientError("Supply one or more images, or an existing local --request-id to resume.")
        private_directory(path.parent)
        private_directory(path.parent / "inputs")
        inputs = []
        for index, raw in enumerate(images):
            source = Path(raw).expanduser().resolve()
            extension = source.suffix.lower()
            if extension not in {".png", ".jpg", ".jpeg", ".webp"} or not source.is_file():
                raise ClientError(f"Not a supported image file: {source}")
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
            finally:
                with contextlib.suppress(FileNotFoundError):
                    os.unlink(temporary)
            inputs.append({"filename": source.name, "local_path": local_path,
                           "sha256": digest.hexdigest(), "size": copied.stat().st_size})
        manifest = {"schema_version": SCHEMA_VERSION, "server": self.server,
                    "request_id": request_id, "created_at": now(), "state": "prepared",
                    "submitted_options": options, "inputs": inputs, "jobs": [], "downloads": []}
        self.write_manifest(manifest)
        return manifest

    def remember_job(self, job):
        job_id = validate_job_id(job.get("id"))
        for manifest in self.manifests():
            changed = False
            for index, existing in enumerate(manifest.get("jobs", [])):
                if existing.get("id") == job_id:
                    manifest["jobs"][index] = job
                    changed = True
            if changed:
                self.write_manifest(manifest)


class Client:
    def __init__(self, state: State, timeout):
        if requests is None:
            raise ClientError("Install the client dependency: python -m pip install 'requests>=2.31,<3'")
        self.state = state
        self.server = state.server
        self.timeout = timeout
        self.session = requests.Session()
        # Never send ambient .netrc credentials or inherit a workstation proxy.
        self.session.trust_env = False
        self.session.headers.update({"X-Client-Type": "cli", "User-Agent": "hunyuan3d-cli/1"})
        self.jar = http.cookiejar.LWPCookieJar(str(state.cookies))
        if state.cookies.exists():
            try:
                self.jar.load(ignore_discard=True, ignore_expires=False)
            except (OSError, http.cookiejar.LoadError) as exc:
                raise ClientError("Cannot read saved session. Move aside cookies.lwp and log in again.") from exc
        self.session.cookies = self.jar

    def url(self, path):
        if not isinstance(path, str) or "\\" in path:
            raise ClientError("Server returned an invalid URL.")
        target = urljoin(self.server + "/", path)
        parts = urlsplit(target)
        if parts.username or parts.password or parts.fragment:
            raise ClientError("Refusing a URL containing credentials or a fragment.")
        origin = normalize_server(f"{parts.scheme}://{parts.netloc}")
        if origin != self.server:
            raise ClientError("Refusing a redirect or download URL to another origin.")
        return target

    def save_cookies(self):
        fd, temporary = tempfile.mkstemp(prefix=".cookies-", dir=self.state.directory)
        os.close(fd)
        try:
            self.jar.save(temporary, ignore_discard=True, ignore_expires=False)
            if os.name != "nt":
                os.chmod(temporary, 0o600)
            os.replace(temporary, self.state.cookies)
        finally:
            with contextlib.suppress(FileNotFoundError):
                os.unlink(temporary)

    def request(self, method, path, *, login_redirects=False, timeout=None, **kwargs):
        url = self.url(path)
        headers = dict(kwargs.pop("headers", {}))
        if method.upper() not in {"GET", "HEAD", "OPTIONS"}:
            headers["Origin"] = self.server
        for hop in range(6):
            try:
                response = self.session.request(method, url, headers=headers, allow_redirects=False,
                                                timeout=timeout or self.timeout, **kwargs)
            except requests.RequestException as exc:
                raise ClientError(f"Request failed ({type(exc).__name__}); preserved request IDs can be resumed.") from exc
            self.save_cookies()
            if response.status_code not in {301, 302, 303, 307, 308}:
                return response
            location = response.headers.get("Location", "")
            try:
                next_url = self.url(urljoin(url, location))
            finally:
                response.close()
            if not login_redirects:
                if urlsplit(next_url).path.startswith("/access/login"):
                    raise ClientError("Login required. Run the login command for this server.")
                raise ClientError("Unexpected redirect; use the server's final origin.")
            if response.status_code in {307, 308} and method.upper() != "GET":
                raise ClientError("Refusing to replay login credentials through a redirect.")
            method, url, kwargs = "GET", next_url, {}
            headers = {}
        raise ClientError("Too many same-origin login redirects.")

    @staticmethod
    def check(response):
        if response.status_code in {401, 403}:
            raise ClientError(f"Server rejected access (HTTP {response.status_code}). Run login or verify the server access policy.")
        if not response.ok:
            detail = ""
            with contextlib.suppress(ValueError):
                body = response.json()
                if isinstance(body, dict):
                    detail = str(body.get("error") or body.get("message") or "")[:300]
            raise ClientError(f"Server returned HTTP {response.status_code}" + (f": {detail}" if detail else "."))

    def json(self, method, path, **kwargs):
        with self.request(method, path, **kwargs) as response:
            self.check(response)
            try:
                result = response.json()
            except ValueError as exc:
                raise ClientError("Expected JSON from the job API; check the server origin and login.") from exc
            if not isinstance(result, dict):
                raise ClientError("Unexpected API response shape.")
            targets = [result]
            if isinstance(result.get("job"), dict):
                targets.append(result["job"])
            if isinstance(result.get("jobs"), list):
                targets.extend(job for job in result["jobs"] if isinstance(job, dict))
            for target in targets:
                if target.get("web_url"):
                    target["web_url"] = self.url(target["web_url"])
            return result

    def login(self, password_file=None):
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
        if password_file:
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
                    raise ClientError("Login failed.")
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
            raise ClientError("Unexpected job status response.")
        self.state.remember_job(job)
        return job

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
            raise ClientError("Unexpected create response; request ID is preserved. Check history --remote before retrying.")
        for job in result["jobs"]:
            if not isinstance(job, dict) or not isinstance(job.get("status"), str):
                raise ClientError("Unexpected job in create response; the request ID is preserved.")
            validate_job_id(job.get("id"))
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
                if latest.get(job_id, {}).get("status") == "completed":
                    continue
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise WaitTimeout(snapshot())
                try:
                    job = self.status(job_id, timeout=min(self.timeout, remaining))
                except ClientError:
                    if time.monotonic() >= deadline:
                        raise WaitTimeout(snapshot()) from None
                    raise
                latest[job_id] = job
                marker = (job["status"], job.get("message"))
                if marker != shown.get(job_id):
                    print(f"{job_id}: {job['status']} {job.get('message', '')}", file=sys.stderr)
                    shown[job_id] = marker
                if job["status"] in {"failed", "cancelled", "canceled"}:
                    return snapshot(), 2
            if len(latest) == len(job_ids) and all(job["status"] == "completed" for job in latest.values()):
                return [latest[job_id] for job_id in job_ids], 0
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise WaitTimeout(snapshot())
            time.sleep(min(interval, remaining))

    def download(self, job_id, output):
        job = self.status(job_id)
        if job["status"] in {"failed", "cancelled", "canceled"}:
            return {"job": job, "error": "Job failed; no completed archive is available."}, 2
        if job["status"] != "completed":
            raise ClientError("Job is not completed. Run wait first, or use generate --download.")
        record = self.json("GET", job.get("record_url") or f"/api/jobs/{job_id}/record")
        output = Path(output).expanduser().resolve()
        output.mkdir(parents=True, exist_ok=True)
        final = output / f"{job_id}.zip"
        fd, temporary = tempfile.mkstemp(prefix=f".{job_id}-", suffix=".part", dir=output)
        digest = hashlib.sha256()
        count = 0
        try:
            with os.fdopen(fd, "wb") as handle:
                with self.request("GET", job.get("archive_url") or f"/api/jobs/{job_id}/archive", stream=True) as response:
                    self.check(response)
                    try:
                        for chunk in response.iter_content(1024 * 1024):
                            if chunk:
                                handle.write(chunk)
                                digest.update(chunk)
                                count += len(chunk)
                    except requests.RequestException as exc:
                        raise ClientError("Archive download interrupted; rerun download with the same job ID.") from exc
                handle.flush()
                os.fsync(handle.fileno())
            try:
                with zipfile.ZipFile(temporary) as archive:
                    record = verify_archive(archive, record, job_id)
            except (zipfile.BadZipFile, ValueError, KeyError, RuntimeError) as exc:
                raise ClientError("Downloaded file is not a valid provenance ZIP.") from exc
            os.replace(temporary, final)
        finally:
            with contextlib.suppress(FileNotFoundError):
                os.unlink(temporary)
        receipt = {"schema_version": SCHEMA_VERSION, "server": self.server, "job_id": job_id,
                   "downloaded_at": now(), "archive": str(final), "archive_sha256": digest.hexdigest(),
                   "archive_bytes": count, "generation_record": record}
        receipt_path = output / f"{job_id}.download.json"
        atomic_json(receipt_path, receipt)
        atomic_json(self.state.records / f"{job_id}.json", receipt)
        for manifest in self.state.manifests():
            if any(item.get("id") == job_id for item in manifest.get("jobs", [])):
                manifest["downloads"] = [item for item in manifest.get("downloads", []) if item.get("job_id") != job_id]
                manifest["downloads"].append({key: value for key, value in receipt.items() if key != "generation_record"})
                self.state.write_manifest(manifest)
        return {"job": job, "archive": str(final), "receipt": str(receipt_path),
                "archive_sha256": digest.hexdigest(), "archive_bytes": count}, 0


def parser():
    common = argparse.ArgumentParser(add_help=False)
    # SUPPRESS lets global options appear before or after the subcommand.
    common.add_argument("--server", default=argparse.SUPPRESS, help="API origin; or MODELING_SERVER / saved login config")
    common.add_argument("--state-dir", default=argparse.SUPPRESS, help="private local state (default: ~/.hunyuan3d-cli)")
    common.add_argument("--json", action="store_true", default=argparse.SUPPRESS, help="JSON results on stdout; progress remains on stderr")
    common.add_argument("--request-timeout", type=positive, default=argparse.SUPPRESS, help="HTTP connect/read inactivity timeout seconds (default: 120)")
    root = argparse.ArgumentParser(description=__doc__, parents=[common])
    commands = root.add_subparsers(dest="command", required=True)
    login = commands.add_parser("login", parents=[common], help="log in through the existing private gateway")
    login.add_argument("--password-file", help="read password from a private UTF-8 file; default: hidden terminal prompt")
    for command in ("status", "wait", "download"):
        child = commands.add_parser(command, parents=[common], help=f"{command} an existing server job")
        child.add_argument("job", help="preserved server job ID")
        if command == "wait":
            child.add_argument("--timeout", type=positive, default=86400, help="total wait budget in seconds (default: 86400)")
            child.add_argument("--poll-interval", type=poll_interval, default=5, help="seconds, at least 1 (default: 5)")
        if command == "download":
            child.add_argument("--output", required=True, help="directory for provenance ZIP and download receipt")
    generate = commands.add_parser("generate", parents=[common], help="submit images to the same queue used by the web UI")
    generate.add_argument("images", nargs="*", help="PNG/JPEG/WebP files; omit when resuming a local request ID")
    generate.add_argument("--model", choices=sorted(MODELS), help="omit to retain the server default")
    generate.add_argument("--seed", type=int, help="0 through 4294967295; omit to retain the server default")
    generate.add_argument("--gpu", help="auto, split, or GPU index; omit to retain the server default")
    generate.add_argument("--option", action="append", default=[], metavar="KEY=VALUE", help="repeatable; allowed: " + ", ".join(sorted(OPTION_NAMES)))
    generate.add_argument("--request-id", help="UUID; reuse to recover an uncertain submission without duplicate inference")
    generate.add_argument("--wait", action="store_true", help="wait until all submitted jobs complete or a job fails")
    generate.add_argument("--download", metavar="DIR", help="wait, then save each completed provenance ZIP into DIR")
    generate.add_argument("--timeout", type=positive, default=86400, help="total wait budget in seconds (default: 86400)")
    generate.add_argument("--poll-interval", type=poll_interval, default=5, help="seconds, at least 1 (default: 5)")
    history = commands.add_parser("history", parents=[common], help="show local request manifests, or server CLI job history")
    history.add_argument("--remote", action="store_true", help="query server's persistent CLI job history")
    history.add_argument("--request-id", help="filter one UUID (also recovers a submitted ID from another client)")
    history.add_argument("--limit", type=nonnegative, default=50, help="remote page size, 1 to 200 (default: 50)")
    history.add_argument("--offset", type=nonnegative, default=0, help="remote pagination offset")
    return root


def render(result, json_output):
    if json_output:
        print(json.dumps(result, ensure_ascii=False, allow_nan=False))
        return
    if result.get("error"):
        print(result["error"])
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
        print(f"Archive: {download['archive']}")


def main(argv=None):
    args = parser().parse_args(argv)
    json_output = getattr(args, "json", False)
    request_id = None
    try:
        state_root = Path(getattr(args, "state_dir", "~/.hunyuan3d-cli")).expanduser().resolve()
        configured = state_root / "config.json"
        server = getattr(args, "server", None) or os.environ.get("MODELING_SERVER")
        if not server and configured.exists():
            server = read_json(configured).get("server")
        if not server:
            raise ClientError("Set --server or MODELING_SERVER to the existing webapp origin.")
        state = State(state_root, normalize_server(server))
        if args.command == "history" and not args.remote:
            manifests = state.manifests()
            if args.request_id:
                request_id = validate_request_id(args.request_id)
                manifests = [item for item in manifests if item["request_id"] == request_id]
            render({"server": state.server, "requests": manifests, "total": len(manifests)}, json_output)
            return 0
        client = Client(state, getattr(args, "request_timeout", 120))
        if args.command == "login":
            result, code = client.login(args.password_file), 0
            # Remember a direct internal origin as well as a gateway login.
            atomic_json(state.root / "config.json", {"server": state.server})
        elif args.command == "status":
            job = client.status(args.job)
            result, code = {"job": job}, 2 if job["status"] in {"failed", "cancelled", "canceled"} else 0
        elif args.command == "wait":
            jobs, code = client.wait([validate_job_id(args.job)], args.timeout, args.poll_interval)
            result = {"jobs": jobs}
        elif args.command == "download":
            result, code = client.download(validate_job_id(args.job), args.output)
        elif args.command == "history":
            if not 1 <= args.limit <= 200:
                raise ClientError("--limit must be between 1 and 200.")
            query = {"source": "cli", "limit": args.limit, "offset": args.offset}
            if args.request_id:
                query["request_id"] = validate_request_id(args.request_id)
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
                result["jobs"], code = client.wait([job["id"] for job in result["jobs"]], args.timeout, args.poll_interval)
            if args.download and code == 0:
                result["downloads"] = [client.download(job["id"], args.download)[0] for job in result["jobs"]]
        render(result, json_output)
        return code
    except WaitTimeout as exc:
        result = {"error": str(exc), "jobs": exc.jobs}
        if request_id:
            result["request_id"] = request_id
        render(result, json_output)
        return 3
    except (ClientError, OSError, ValueError) as exc:
        result = {"error": str(exc)}
        if request_id:
            result["request_id"] = request_id
        if json_output:
            render(result, True)
        else:
            print(f"Error: {exc}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        result = {"error": "Interrupted locally; server jobs continue. IDs and inputs remain in local history."}
        if request_id:
            result["request_id"] = request_id
        if json_output:
            render(result, True)
        else:
            print(result["error"], file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
