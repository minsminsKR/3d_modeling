"""Collect editable rigging inputs from CLI-verified refreshed job archives.

Run after `modeling_cli.py collect ... --refresh` completed successfully.
Original models and image inputs are already retained in generated-ghost/.
The large duplicate previews and action-preview GLBs remain optional.
"""
import hashlib
import json
from pathlib import Path
import zipfile

HERE = Path(__file__).resolve().parent
CACHE = HERE.parents[1] / "Builds" / "ModelingCache" / "SchoolStudyStudents" / "rigged-archives"
JOBS = ("10c8d883-8918-44d3-bcd5-7cf85bffff26",
        "b1f35610-5dd0-4d0d-ac15-e9c109bba82f",
        "0ac03514-069f-4afc-a8cc-90d4d86cff8f")
KEEP = {"rigged.glb", "analysis.glb", "rigging-request.json",
        "rigging-result.json", "preparation.json", "motion-actions-v2.json"}


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


for job in JOBS:
    archive = CACHE / (job + ".zip")
    receipt = archive.with_name(job + ".download.json")
    result = json.loads(receipt.read_text(encoding="utf-8"))
    if result.get("archive_sha256") != sha(archive):
        raise ValueError("The CLI archive receipt does not match " + job)
    output = HERE / "rigged" / job
    output.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        selected = [n for n in z.namelist() if n.startswith("outputs/rigging/")
                    and Path(n).name in KEEP]
        if len(selected) != len(KEEP) or len({Path(n).name for n in selected}) != len(KEEP):
            raise ValueError("Expected one complete active rig in " + job)
        for name in selected:
            (output / Path(name).name).write_bytes(z.read(name))
        (output / "generation-record.json").write_bytes(z.read("generation-record.json"))
    manifest = {"job": job, "archive_sha256": result["archive_sha256"],
                "files": {p.name: sha(p) for p in output.iterdir() if p.is_file() and p.name != "manifest.json"}}
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(job, "collected", len(manifest["files"]), "files")
