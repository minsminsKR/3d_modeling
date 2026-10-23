"""Versioned, dependency-free error contract shared by the CLI and HTTP API."""
from __future__ import annotations

from email.utils import parsedate_to_datetime
from datetime import datetime, timezone
import math

ERROR_SCHEMA_VERSION = 1
ERRORS = {
    "INVALID_INPUT": (False, "Correct the input or options and retry. Use capabilities --json for supported values."),
    "AUTH_REQUIRED": (False, "Run login for the same server, then resume using the original request ID."),
    "AUTH_REJECTED": (False, "Verify the workspace login and server origin; log in again before retrying."),
    "RATE_LIMITED": (True, "Wait for retry_after_seconds, then retry using the original request ID."),
    "SERVER_UNAVAILABLE": (True, "Check doctor or server availability, wait, then resume with the original request ID."),
    "SERVER_NOT_READY": (True, "Inspect doctor checks and unavailable_reasons; resolve readiness issues before generating."),
    "NETWORK_ERROR": (True, "Restore the connection; check history and resume with the original request ID. Do not create a new request."),
    "REQUEST_TIMEOUT": (True, "The submission may already exist. Check history and resume using the original request ID."),
    "WAIT_TIMEOUT": (True, "Server jobs continue. Run resume with the original request ID to collect completed and pending jobs."),
    "REQUEST_CONFLICT": (False, "Reuse this request ID only with its original images and options. For a different model, deliberately create a new request ID."),
    "NOT_FOUND": (False, "Check the server origin and request/job ID in history before submitting anything again."),
    "JOB_NOT_READY": (True, "Wait for this job to complete, then collect or download it; do not submit another generation."),
    "JOB_FAILED": (False, "Inspect the failed job and collect other successful jobs. Regeneration requires an intentional new request ID."),
    "PARTIAL_FAILURE": (False, "Keep downloaded successes. Inspect failed_jobs and download_errors, then run collect or resume for the same request ID."),
    "ARTIFACT_UNAVAILABLE": (False, "Inspect the job's stored artifacts on the server. Do not treat an incomplete download as a model."),
    "INTEGRITY_ERROR": (False, "Keep the last verified archive. Check server artifacts and download again with the same job ID."),
    "PROTOCOL_ERROR": (False, "Check the server origin, CLI/API versions and doctor output before retrying."),
    "LOCAL_IO_ERROR": (False, "Check local disk space, permissions and output path, then resume with the original request ID."),
    "INTERRUPTED": (True, "Only local execution stopped. Server jobs continue; resume using the original request ID."),
    "UNSUPPORTED_API": (False, "Use a compatible server version or update the server CLI/API. Do not change the request ID to work around this."),
    "INTERNAL_ERROR": (False, "Inspect server diagnostics and logs. Check the original request ID in history before retrying."),
}


class ModelingError(Exception):
    def __init__(self, message, code="INVALID_INPUT", *, retryable=None,
                 next_action=None, http_status=None, details=None):
        super().__init__(message)
        self.code = code if code in ERRORS else "INTERNAL_ERROR"
        default_retry, default_action = ERRORS[self.code]
        self.retryable = default_retry if retryable is None else bool(retryable)
        self.next_action = next_action or default_action
        self.http_status = http_status
        self.details = details

    def as_dict(self, request_id=None):
        result = {"schema_version": ERROR_SCHEMA_VERSION, "ok": False,
                  "error": str(self), "message": str(self), "code": self.code,
                  "retryable": self.retryable, "next_action": self.next_action}
        if request_id:
            result["request_id"] = request_id
        if self.http_status is not None:
            result["http_status"] = self.http_status
        if self.details is not None:
            result["details"] = self.details
        return result


def error_payload(code, message=None, *, request_id=None, details=None, http_status=None):
    return ModelingError(message or code.replace("_", " ").capitalize(), code,
                         details=details, http_status=http_status).as_dict(request_id)


def retry_seconds(value):
    if value is None:
        return None
    try:
        return max(0, min(86400, int(value)))
    except (TypeError, ValueError):
        try:
            stamp = parsedate_to_datetime(str(value))
            if stamp.tzinfo is None:
                stamp = stamp.replace(tzinfo=timezone.utc)
            return max(0, min(86400, int((stamp - datetime.now(timezone.utc)).total_seconds())))
        except (TypeError, ValueError, OverflowError):
            return None


def http_error(status, message=None, *, payload=None, request_id=None, path=None, retry_after=None):
    body = payload if isinstance(payload, dict) else {}
    explicit = body.get("code")
    known_code = isinstance(explicit, str) and explicit in ERRORS
    fallback = {400: "INVALID_INPUT", 401: "AUTH_REQUIRED", 403: "AUTH_REJECTED",
                404: "NOT_FOUND", 405: "INVALID_INPUT", 408: "REQUEST_TIMEOUT",
                409: "REQUEST_CONFLICT", 413: "INVALID_INPUT", 415: "INVALID_INPUT",
                422: "INVALID_INPUT", 429: "RATE_LIMITED", 500: "INTERNAL_ERROR",
                502: "SERVER_UNAVAILABLE", 503: "SERVER_UNAVAILABLE", 504: "REQUEST_TIMEOUT"}
    code = explicit if known_code else fallback.get(status, "SERVER_UNAVAILABLE" if status >= 500 else "PROTOCOL_ERROR")
    legacy = body.get("error")
    if legacy == "authentication_required":
        code = "AUTH_REQUIRED"
    elif legacy == "cross_origin_request_rejected":
        code = "AUTH_REJECTED"
    text = body.get("message") or message or (legacy if isinstance(legacy, str) else None) or f"Server returned HTTP {status}."
    details = {}
    # Only forward explicitly supported machine fields, never cookies, headers,
    # arbitrary upstream details or tracebacks from the authentication gateway.
    delay = retry_seconds(retry_after)
    if body.get("schema_version") == ERROR_SCHEMA_VERSION and known_code:
        supplied = body.get("details")
        if isinstance(supplied, dict):
            for key in ("retry_after_seconds", "field", "minimum", "maximum", "job_id", "request_id", "model"):
                item = supplied.get(key)
                if isinstance(item, (str, int, float, bool)) and not (isinstance(item, float) and not math.isfinite(item)):
                    details[key] = item[:500] if isinstance(item, str) else item
    if delay is not None:
        details["retry_after_seconds"] = delay
    trusted = body.get("schema_version") == ERROR_SCHEMA_VERSION and known_code
    can_retry = body.get("retryable") if trusted and isinstance(body.get("retryable"), bool) else None
    recovery = body.get("next_action") if trusted and isinstance(body.get("next_action"), str) else None
    result = ModelingError(str(text)[:2000], code, retryable=can_retry,
                           next_action=recovery[:1000] if recovery else None,
                           http_status=status, details=details or None)
    return result
