"""Verify portable temple service records, without requiring archived ZIPs.

Use --archives <directory> only when the original job ZIPs are available and
their original byte hashes and normalization should also be checked.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import parse_qsl, urlsplit
import zipfile

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent
MANIFEST = ROOT / "manifest.json"
INDEX = PROJECT / "generation-pipeline-records.json"
ABSOLUTE = re.compile(r"^(?:/|[A-Za-z]:[\\/]|\\\\|file://)", re.I)
EMBEDDED = re.compile(r"(?:^|[\s=\"'])(?:/home/|/Users/|/workspace/|/tmp/|[A-Za-z]:[\\/])", re.I)
PLACEHOLDER = re.compile(r"<SERVER_ABSOLUTE_PATH_[0-9]{2,}>")
SECRET_KEY = re.compile(
    r"^(password|passwd|cookie|cookies|authorization|access_token|refresh_token|api_key|secret|private_key|client_secret|session_token)$",
    re.I,
)
SECRET_VALUE = re.compile(
    r"(?:\bBearer\s+\S+|\bsk-(?:proj-)?[A-Za-z0-9_-]{12,}|-----BEGIN [A-Z ]*PRIVATE KEY-----)",
    re.I,
)


def fail(message: str) -> None:
    raise SystemExit("SERVICE_RECORDS_FAIL: " + message)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def render(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def inspect(value: object, pointer: str, occurrences: dict[str, list[str]]) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if SECRET_KEY.fullmatch(key) and child not in (None, "", [], {}):
                fail("a sensitive field has a value")
            escaped = key.replace("~", "~0").replace("/", "~1")
            inspect(child, pointer + "/" + escaped, occurrences)
    elif isinstance(value, list):
        for position, child in enumerate(value):
            inspect(child, pointer + "/" + str(position), occurrences)
    elif isinstance(value, str):
        if SECRET_VALUE.search(value) or ABSOLUTE.match(value) or EMBEDDED.search(value):
            fail("a record contains a credential-like value or unnormalized path")
        if value.startswith(("http://", "https://")) and any(
            SECRET_KEY.fullmatch(key) for key, _ in parse_qsl(urlsplit(value).query, keep_blank_values=True)
        ):
            fail("a URL contains a sensitive field")
        if PLACEHOLDER.fullmatch(value):
            occurrences.setdefault(value, []).append(pointer)
        elif "<SERVER_ABSOLUTE_PATH_" in value:
            fail("a path symbol is malformed")


def normalized(value: object, tokens: dict[str, str]) -> object:
    if isinstance(value, dict):
        return {key: normalized(child, tokens) for key, child in value.items()}
    if isinstance(value, list):
        return [normalized(child, tokens) for child in value]
    if isinstance(value, str) and ABSOLUTE.match(value):
        if value not in tokens:
            tokens[value] = f"<SERVER_ABSOLUTE_PATH_{len(tokens) + 1:02d}>"
        return tokens[value]
    return value


def relative_file(relative: str, root: Path) -> Path:
    path = Path(relative)
    if path.is_absolute() or ".." in path.parts:
        fail("invalid repository-relative record path")
    resolved = (root / path).resolve()
    if not resolved.is_relative_to(root.resolve()) or not resolved.is_file():
        fail("a referenced record or source is missing")
    return resolved


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archives", type=Path, help="optional directory containing the original job ZIPs")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    inspect(manifest, "$", {})
    index = json.loads(INDEX.read_text(encoding="utf-8"))
    indexed = {job["job_id"]: job for job in index["jobs"]}
    jobs = manifest["jobs"]
    if (manifest.get("schema_version") != 1 or len(jobs) != 3 or len(indexed) != 3
            or {job["job_id"] for job in jobs} != set(indexed)):
        fail("unexpected manifest or job count")
    count = 0
    for job in jobs:
        prior = indexed.get(job["job_id"])
        if not prior or any(job[key] != prior[key] for key in ("label", "request_id", "job_id")):
            fail("job identity differs from the existing pipeline index")
        if job["tracked_model"] != prior["model"] or job["tracked_reference"] != prior["reference"]:
            fail("model or reference differs from the existing pipeline index")
        relative_file(job["tracked_model"], PROJECT)
        relative_file(job["tracked_reference"], PROJECT)
        if len(job["records"]) != len(prior["stages"]):
            fail("stage count differs from the existing pipeline index")
        if {record["source_archive_entry"] for record in job["records"]} != set(prior["stages"]):
            fail("the stage list differs from the existing pipeline index")
        occurrences: dict[str, list[str]] = {}
        archive = zipfile.ZipFile(args.archives / (job["job_id"] + ".zip")) if args.archives else None
        tokens: dict[str, str] = {}
        try:
            for record in job["records"]:
                name = record["source_archive_entry"]
                stage = prior["stages"].get(name)
                if not stage or record["original_sha256"] != stage["original_sha256"]:
                    fail("an original checksum differs from the existing pipeline index")
                raw = relative_file(record["record_file"], ROOT).read_bytes().replace(b"\r\n", b"\n")
                if sha(raw) != record["normalized_sha256"]:
                    fail("a normalized record checksum differs")
                value = json.loads(raw)
                inspect(value, record["record_file"] + "#$", occurrences)
                for key, expected in stage["settings"].items():
                    # Existing summaries add a model identifier derived from
                    # other provenance; it was not a key in the stage JSON.
                    if key == "model_identifier" and key not in value:
                        continue
                    if key not in value or value[key] != expected:
                        fail("a retained setting differs from the pipeline index")
                if archive:
                    original = archive.read(name)
                    if sha(original) != record["original_sha256"]:
                        fail("an archive entry checksum differs")
                    if render(normalized(json.loads(original), tokens)) != raw:
                        fail("an archive entry does not normalize to the stored record")
                count += 1
        finally:
            if archive:
                archive.close()
        if occurrences != job["path_placeholders"]:
            fail("path symbol locations differ from the manifest")
    if count != 9:
        fail("unexpected total record count")
    mode = "with archive cross-check" if args.archives else "without local archives"
    print(f"SERVICE_RECORDS_PASS: {count} records, {len(jobs)} jobs, {mode}")


if __name__ == "__main__":
    main()
