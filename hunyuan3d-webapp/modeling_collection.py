"""Batch recovery and result collection for the modeling CLI.

This module never creates replacement jobs. Only resume of a locally preserved,
uncertain submission may replay its original UUID and original bytes.
"""
from __future__ import annotations

import json
from pathlib import Path
import sys
import time

from modeling_contract import ModelingError, error_payload


TERMINAL = {"completed", "failed", "cancelled", "canceled"}
FAILED = TERMINAL - {"completed"}
CONNECTIVITY = {"NETWORK_ERROR", "REQUEST_TIMEOUT", "SERVER_UNAVAILABLE", "RATE_LIMITED"}


def local_manifest(client, request_id):
    path = client.state.manifest_path(request_id)
    if not path.exists():
        return None
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise ModelingError("Cannot read the preserved request manifest.", code="LOCAL_IO_ERROR") from exc
    if not isinstance(manifest, dict) or manifest.get("server") != client.server:
        raise ModelingError("Preserved request belongs to another server.", code="REQUEST_CONFLICT")
    if manifest.get("request_id") != request_id:
        raise ModelingError("Preserved request ID does not match its manifest path.", code="INTEGRITY_ERROR")
    return manifest


def exception_result(exc, job_id=None):
    if isinstance(exc, ModelingError):
        result = exc.as_dict()
    elif isinstance(exc, OSError):
        result = error_payload("LOCAL_IO_ERROR", str(exc))
    else:
        result = error_payload("PROTOCOL_ERROR", "Unexpected response while collecting a job.")
    if job_id:
        result["job_id"] = job_id
    return result


def persist_collection(client, request_id, result):
    # Downloads write receipts to the manifest too. Reload before merging so
    # those writes and the preserved input/options can never be overwritten.
    manifest = local_manifest(client, request_id)
    if manifest is None:
        manifest = client.state.recover_manifest(request_id, result["jobs"])
    manifest["jobs"] = result["jobs"]
    manifest["collection_summary"] = result["summary"]
    manifest["last_collection"] = result
    client.state.write_manifest(manifest)


def collect_request(client, request_id, output, *, wait=False, timeout=86400,
                    interval=5, refresh=False, jobs=None):
    # manifest_path validates the UUID before performing any network operation.
    client.state.manifest_path(request_id)
    initial_error = None
    if jobs is None:
        try:
            jobs = client.request_jobs(request_id)
        except ModelingError as exc:
            manifest = local_manifest(client, request_id)
            if exc.code not in CONNECTIVITY or not manifest or not manifest.get("jobs"):
                raise
            # Cached completed jobs can still be verified/recovered if remote
            # history is temporarily unavailable. Never infer new job IDs.
            jobs = manifest["jobs"]
            initial_error = exception_result(exc)
    if not jobs:
        raise ModelingError("No server jobs were found for this request UUID. Use the original server and request ID.",
                            code="NOT_FOUND")
    latest = {job["id"]: dict(job) for job in jobs}
    if len(latest) != len(jobs):
        raise ModelingError("Request contains duplicate job IDs.", code="PROTOCOL_ERROR")
    client.state.recover_manifest(request_id, list(latest.values()))
    downloads, download_errors, status_errors, shown = {}, {}, {}, {}
    if initial_error:
        status_errors["request"] = initial_error
    deadline = time.monotonic() + timeout
    timed_out = interrupted = False

    def report(job):
        marker = (job["status"], job.get("message"))
        if marker != shown.get(job["id"]):
            print(f"{job['id']}: {job['status']} {job.get('message', '')}", file=sys.stderr)
            shown[job["id"]] = marker

    def collect_completed(job):
        job_id = job["id"]
        if output is None or job["status"] != "completed" or job_id in downloads or job_id in download_errors:
            return
        try:
            result, code = client.download(job_id, output, refresh=refresh, job=job)
            if code:
                download_errors[job_id] = {**result, "job_id": job_id}
            else:
                downloads[job_id] = result
        except (ModelingError, OSError, ValueError) as exc:
            download_errors[job_id] = exception_result(exc, job_id)

    try:
        while True:
            for job in latest.values():
                report(job)
                collect_completed(job)
            if all(job["status"] in TERMINAL for job in latest.values()):
                break
            for job_id, previous in list(latest.items()):
                if previous["status"] in TERMINAL:
                    continue
                remaining = deadline - time.monotonic()
                if wait and remaining <= 0:
                    timed_out = True
                    break
                try:
                    job = client.status(job_id, timeout=min(client.timeout, remaining) if wait else None)
                    latest[job_id] = job
                    status_errors.pop(job_id, None)
                    report(job)
                    collect_completed(job)
                except (ModelingError, OSError, ValueError) as exc:
                    status_errors[job_id] = exception_result(exc, job_id)
                    if wait and time.monotonic() >= deadline:
                        timed_out = True
                    # Still visit every other job; completed outputs must not
                    # be lost because one status request failed.
            if all(job["status"] in TERMINAL for job in latest.values()):
                break
            if timed_out or status_errors or not wait:
                break
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                timed_out = True
                break
            time.sleep(min(interval, remaining))
    except KeyboardInterrupt:
        interrupted = True

    snapshot = list(latest.values())
    failed = [job for job in snapshot if job["status"] in FAILED]
    pending = [job for job in snapshot if job["status"] not in TERMINAL]
    result = {"schema_version": 1, "ok": True, "server": client.server,
              "request_id": request_id, "jobs": snapshot, "downloads": list(downloads.values()),
              "failed_jobs": failed, "pending_jobs": pending,
              "download_errors": list(download_errors.values()), "status_errors": list(status_errors.values()),
              "summary": {"total": len(snapshot),
                          "completed": sum(job["status"] == "completed" for job in snapshot),
                          "failed": len(failed), "pending": len(pending),
                          "downloaded": len(downloads), "download_errors": len(download_errors),
                          "status_errors": len(status_errors),
                          "skipped": sum(bool(item.get("skipped")) for item in downloads.values())}}
    code = 0
    if interrupted:
        result.update(error_payload("INTERRUPTED", request_id=request_id))
        code = 1
    elif timed_out:
        result.update(error_payload("WAIT_TIMEOUT", request_id=request_id))
        code = 3
    elif failed or download_errors or status_errors:
        partial = bool(downloads) or (bool(failed) and bool(result["summary"]["completed"]))
        error_code = "PARTIAL_FAILURE" if partial or download_errors else ("JOB_FAILED" if failed else next(iter(status_errors.values()))["code"])
        result.update(error_payload(error_code, request_id=request_id))
        code = 2 if failed or download_errors or partial else 1
    persist_collection(client, request_id, result)
    return result, code


def resume_request(client, request_id, output, *, timeout=86400, interval=5, refresh=False):
    # Always query the exact UUID first. An empty response is different from a
    # failed history query: connectivity/auth failures must never trigger POST.
    jobs = client.request_jobs(request_id)
    if not jobs:
        manifest = local_manifest(client, request_id)
        if not manifest or manifest.get("state") not in {"prepared", "submitting"} or not manifest.get("inputs"):
            raise ModelingError("No server jobs found and no uncertain local submission is available. No new generation was requested.",
                                code="NOT_FOUND")
        # prepare verifies the exact preserved input hashes before replay.
        manifest = client.state.prepare(request_id, [], {})
        jobs = client.generate(manifest)["jobs"]
    return collect_request(client, request_id, output, wait=True, timeout=timeout,
                           interval=interval, refresh=refresh, jobs=jobs)
