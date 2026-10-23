"""HTTP submission/provenance helpers. No model loading or separate job queue."""
from __future__ import annotations

import hashlib
import json
import re
import uuid
from pathlib import Path

from flask import abort, jsonify, send_file
from werkzeug.exceptions import RequestedRangeNotSatisfiable
from modeling_contract import ModelingError
from modeling_capabilities import validate_submission_fields
from modeling_archives import Source, reject_links, snapshot_for_job

GENERATION_FIELDS = {"model", "gpu_id", "seed", "remove_background", "texture", "trellis_pipeline",
                     "trellis_steps", "trellis_texture_size", "num_inference_steps", "guidance_scale",
                     "octree_resolution", "num_chunks", "texture_profile"}
SOURCE_SUFFIXES = {".glb", ".gltf", ".bin", ".obj", ".mtl", ".ply", ".stl", ".fbx", ".dae",
                   ".blend", ".bvh", ".abc", ".usd", ".usda", ".usdc", ".usdz", ".npz", ".npy",
                   ".png", ".jpg", ".jpeg", ".webp", ".exr", ".hdr", ".json", ".txt"}


class _LeasedBody:
    """Release on both WSGI close and full consumption by test/proxy clients."""
    def __init__(self, body, release):
        self.body = body
        self.iterator = iter(body)
        self.release = release
        self.closed = False

    def __iter__(self):
        return self

    def __next__(self):
        try:
            return next(self.iterator)
        except BaseException:
            self.close()
            raise

    def close(self):
        if self.closed:
            return
        self.closed = True
        try:
            if hasattr(self.body, "close"):
                self.body.close()
        finally:
            self.release()


def file_digest(path: Path) -> dict:
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)
    return {"sha256": digest.hexdigest(), "size": size}


def prepare_submission(request, validated_files: list) -> dict:
    validate_submission_fields(request.form)
    if set(request.form) - GENERATION_FIELDS:
        abort(400, "Unsupported generation fields: " + ", ".join(sorted(set(request.form) - GENERATION_FIELDS)))
    request_id = request.headers.get("Idempotency-Key") or str(uuid.uuid4())
    if not re.fullmatch(r"[A-Za-z0-9._-]{8,120}", request_id):
        abort(400, "Idempotency-Key must contain 8-120 letters, digits, dots, underscores or hyphens.")
    inputs = []
    for uploaded, _safe_name, original_name in validated_files:
        digest = hashlib.sha256()
        size = 0
        uploaded.stream.seek(0)
        for chunk in iter(lambda: uploaded.stream.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)
        uploaded.stream.seek(0)
        inputs.append({"name": original_name, "sha256": digest.hexdigest(), "size": size})
    # Hash submitted fields, rather than transient GPU placement or defaults that
    # may change before a client recovers an interrupted HTTP response.
    fields = {key: request.form.getlist(key) for key in sorted(request.form)}
    payload = json.dumps({"inputs": inputs, "fields": fields}, sort_keys=True, ensure_ascii=False).encode("utf-8")
    return {"request_id": request_id, "submission_fingerprint": hashlib.sha256(payload).hexdigest(),
            "inputs": inputs, "submitted_options": fields,
            "source": "cli" if request.headers.get("X-Client-Type") == "cli" else "api"}


def find_submission(jobs: dict, submission: dict) -> list[dict]:
    matches = [job for job in jobs.values() if job.get("request_id") == submission["request_id"]]
    if matches and any(job.get("submission_fingerprint") != submission["submission_fingerprint"] for job in matches):
        raise ModelingError("This request ID already belongs to different images or settings.",
                            "REQUEST_CONFLICT", http_status=409)
    return sorted(matches, key=lambda job: job.get("batch_index", 0))


def safe_public(value):
    if isinstance(value, dict):
        return {key: safe_public(item) for key, item in value.items()
                if not key.endswith(("_path", "_paths")) and key not in
                {"submission_fingerprint", "password", "secret", "secret_key", "cookie", "token", "api_key", "authorization"}}
    if isinstance(value, (tuple, list)):
        return [safe_public(item) for item in value]
    return value


def confined_file(path: str | Path, root: Path) -> Path:
    candidate = Path(path)
    root = root.absolute()
    try:
        relative = candidate.absolute().relative_to(root)
        candidate.resolve().relative_to(root.resolve())
    except ValueError:
        raise ModelingError("An artifact is outside this job's storage directory.", "ARTIFACT_UNAVAILABLE", http_status=409)
    current = root
    reject_links(root)
    if current.is_symlink():
        raise ModelingError("Symbolic-link artifacts cannot be exported.", "ARTIFACT_UNAVAILABLE", http_status=409)
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise ModelingError("Symbolic-link artifacts cannot be exported.", "ARTIFACT_UNAVAILABLE", http_status=409)
    if not candidate.is_file():
        raise ModelingError("A required job artifact is missing.", "ARTIFACT_UNAVAILABLE", http_status=409)
    return candidate


def output_artifacts(job: dict, output_root: Path) -> list[tuple[str, Path]]:
    root = output_root / job["id"]
    model = confined_file(job.get("model_path", ""), root)
    files = [("model.glb", model)]
    included = {model.absolute()}
    if job.get("metadata_path"):
        metadata = confined_file(job["metadata_path"], root)
        files.append(("metadata.json", metadata))
        included.add(metadata.absolute())
    for path in sorted(root.rglob("*")):
        if path.suffix.lower() not in SOURCE_SUFFIXES or path.absolute() in included:
            continue
        if path.is_file() or path.is_symlink():
            path = confined_file(path, root)
            relative = path.relative_to(root).as_posix()
            name = relative if relative.startswith("processed_inputs/") else "outputs/" + relative
            files.append((name, path))
    return files


def record_for_job(job: dict, public_job: dict, upload_root: Path, output_root: Path) -> dict:
    if job.get("status") == "completed":
        snapshot = _snapshot(job, public_job, upload_root, output_root)
        try:
            return {**snapshot.record, "archive": snapshot.archive}
        finally:
            snapshot.close()
    record = {"schema_version": 1, **safe_public(public_job)}
    inputs = []
    for index, filename in enumerate(job.get("image_paths", [])):
        path = confined_file(filename, upload_root / job["id"])
        original = (job.get("inputs") or [{}])[0].get("name") or job.get("source_filename") or path.name
        inputs.append({"name": original, "archive_name": f"inputs/{index + 1:02d}_{path.name}", **file_digest(path)})
    record["inputs"] = inputs
    record["artifacts"] = []
    return record


def _snapshot(job, public_job, upload_root, output_root):
    def sources():
        items = [Source(name, path, "output", path.relative_to(output_root / job["id"]).as_posix())
                 for name, path in output_artifacts(job, output_root)]
        for index, filename in enumerate(job.get("image_paths", [])):
            path = confined_file(filename, upload_root / job["id"])
            original = (job.get("inputs") or [{}])[0].get("name") or job.get("source_filename") or path.name
            items.append(Source(f"inputs/{index + 1:02d}_{path.name}", path, "input",
                                path.relative_to(upload_root / job["id"]).as_posix(), original))
        return items

    record = {"schema_version": 1, **safe_public(public_job)}
    # Archive metadata describes the final ZIP; embedding it would create a
    # self-referential digest. It is exposed only by the external record API.
    record.pop("archive", None)
    return snapshot_for_job(job["id"], output_root, record, sources)


def archive_for_job(job: dict, public_job: dict, upload_root: Path, output_root: Path):
    if job.get("status") != "completed":
        raise ModelingError("Wait for the model to complete before downloading its archive.", "JOB_NOT_READY", http_status=409)
    snapshot = _snapshot(job, public_job, upload_root, output_root)
    try:
        try:
            response = send_file(snapshot.path, mimetype="application/zip", as_attachment=True,
                                 download_name=f"model_{job['id']}.zip", conditional=True,
                                 etag=snapshot.archive["sha256"], max_age=0)
        except RequestedRangeNotSatisfiable:
            # Keep the validator on 416 as well: a client with an already
            # complete checkpoint can verify it without transferring the ZIP.
            error = ModelingError("Requested archive range is outside this snapshot.", "INVALID_INPUT", http_status=416,
                                  next_action="Read this job's current record and retry a valid byte range with its strong ETag, or download the complete archive.")
            response = jsonify(error.as_dict())
            response.status_code = 416
            response.headers["Content-Range"] = f"bytes */{snapshot.archive['size']}"
            response.set_etag(snapshot.archive["sha256"])
            snapshot.close()
        response.headers["Accept-Ranges"] = "bytes"
        response.headers["Cache-Control"] = "private, no-cache, max-age=0, must-revalidate"
        response.headers["X-Archive-SHA256"] = snapshot.archive["sha256"]
        response.headers["X-Archive-Version"] = snapshot.archive["version"]
        # send_file uses direct_passthrough: wrap its file iterable so WSGI
        # close releases the lease even if Response.close is bypassed.
        response.response = _LeasedBody(response.response, snapshot.close)
        response.call_on_close(snapshot.close)
        return response
    except BaseException:
        snapshot.close()
        raise
