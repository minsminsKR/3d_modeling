"""Independently verify mandatory recorded resources and retained provenance.

Uses only Python's standard library. PCM, manifest and source-file checks do not
certify human listening quality, Unity import, the game mix or pause/mute output.
Run from any directory; optional --output saves this narrowly scoped report.
"""
from __future__ import annotations

import argparse
from array import array
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import wave

PROJECT = Path(__file__).resolve().parents[2]
SCOPE = ("Mandatory resource inventory, canonical hashes, retained recording "
         "hashes, mono PCM16 data and Unity GUIDs only; not Unity import, native "
         "mix, pause/mute output or human/device listening certification.")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify() -> dict:
    errors: list[str] = []
    def require(condition: bool, message: str) -> None:
        if not condition:
            errors.append(message)

    resources = PROJECT / "Assets/Resources/Audio/External"
    source = (PROJECT / "Assets/Scripts/ExternalAudio.cs").read_text(encoding="utf8")
    variants_block = source.split("static readonly Dictionary<string, int> variants", 1)[1].split(
        "static readonly Dictionary<string, AudioClip> resources", 1)[0]
    pairs = re.findall(r'\{\s*"([a-z0-9-]+)"\s*,\s*(\d+)\s*\}', variants_block)
    require(bool(pairs), "No mandatory cue variants found in ExternalAudio")
    require(len(pairs) == len(dict(pairs)), "Duplicate mandatory cue names")
    require(all(int(count) > 0 for _, count in pairs), "A mandatory cue has zero variants")
    expected = {f"{cue}-{index}.wav" for cue, count in pairs for index in range(int(count))}
    manifest = json.loads((PROJECT / "ThirdParty/Audio/manifest.json").read_text(encoding="utf8"))
    counts = Counter(item["file"] for item in manifest)
    require(all(count == 1 for count in counts.values()), "Duplicate canonical manifest files")
    actual = {path.name for path in resources.glob("*.wav")}
    require(expected == actual, f"Mandatory/file inventory mismatch: missing={sorted(expected-actual)}, extra={sorted(actual-expected)}")
    require(expected == set(counts), f"Mandatory/manifest inventory mismatch: missing={sorted(expected-set(counts))}, extra={sorted(set(counts)-expected)}")

    retained = {}
    source_count = 0
    for catalog in sorted((PROJECT / "ThirdParty/Audio").glob("*/sources.json")):
        for key, item in json.loads(catalog.read_text(encoding="utf8")).items():
            relative = Path(item["file"])
            require(not relative.is_absolute() and ".." not in relative.parts,
                    f"Unsafe retained recording path: {catalog.parent.name}/{key}")
            if relative.is_absolute() or ".." in relative.parts:
                continue
            recording = catalog.parent / "sources" / relative
            require(recording.is_file(), f"Missing retained recording: {catalog.parent.name}/{key}")
            digest = item.get("source_file_sha256")
            require(bool(digest), f"Missing original hash: {catalog.parent.name}/{key}")
            if recording.is_file():
                require(sha(recording) == digest, f"Original hash changed: {catalog.parent.name}/{key}")
            for field in ("creator", "license", "page", "download", "source_file"):
                require(bool(item.get(field)), f"Missing {field}: {catalog.parent.name}/{key}")
            retained[digest] = item
            source_count += 1

    guids = set()
    cues = []
    for item in manifest:
        name = item["file"]
        path = resources / name
        require(Path(name).name == name and name.endswith(".wav"), f"Unsafe cue name: {name}")
        if Path(name).name != name or not name.endswith(".wav"):
            continue
        for field in ("resource", "license", "creator", "page", "download", "source_file", "modifications", "sha256"):
            require(bool(item.get(field)), f"Missing canonical {field}: {name}")
        require(item.get("resource") == "Audio/External/" + path.stem, f"Wrong resource path: {name}")
        require(item.get("license") in ("CC0-1.0", "CC-BY-4.0"), f"Unreviewed license: {name}")
        for recording in item.get("modifications", {}).get("recordings", []):
            digest = recording.get("source_file_sha256")
            require(digest in retained, f"Cue origin is not a retained recording: {name}")
            if digest in retained:
                require(all(recording.get(field) == retained[digest].get(field)
                            for field in ("creator", "license", "page", "download", "source_file")),
                        f"Cue provenance differs from retained source: {name}")
        if not path.is_file():
            continue
        require(sha(path) == item["sha256"], f"Canonical WAV hash changed: {name}")
        meta = path.with_suffix(".wav.meta")
        require(meta.is_file(), f"Missing Unity metadata: {name}")
        if meta.is_file():
            found = re.findall(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf8"), re.MULTILINE)
            require(len(found) == 1, f"Invalid Unity GUID: {name}")
            if len(found) == 1:
                require(found[0] not in guids, f"Duplicate audio GUID: {name}")
                guids.add(found[0])
        try:
            with wave.open(str(path), "rb") as wav:
                channels, width, rate, frames = wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()
                require(channels == 1 and width == 2 and wav.getcomptype() == "NONE", f"Expected mono PCM16: {name}")
                require(rate == item["modifications"].get("sample_rate"), f"Sample-rate metadata differs: {name}")
                require(frames > 0 and abs(frames/rate-item["seconds"]) < 1/rate, f"Duration metadata differs: {name}")
                samples = array("h", wav.readframes(frames))
                if sys.byteorder != "little":
                    samples.byteswap()
        except (wave.Error, ValueError) as error:
            errors.append(f"Invalid WAV {name}: {error}")
            continue
        if not samples:
            continue
        peak = max(abs(value) for value in samples) / 32768
        rms = math.sqrt(sum(value*value for value in samples)/len(samples)) / 32768
        require(0 < peak < 1, f"Silent or clipped PCM: {name}")
        require(abs(peak-item["peak"]) < 2/32768 and abs(rms-item["rms"]) < 2/32768,
                f"Canonical PCM metrics differ: {name}")
        cues.append(dict(file=name, rate=rate, seconds=frames/rate, peak=peak, rms=rms, sha256=item["sha256"]))
    credits = (PROJECT / "ThirdParty/Audio/Audio-Credits.txt").read_text(encoding="utf8")
    for item in manifest:
        # Catalog descriptions may carry '(submitted by ...)' after the creator;
        # the credit can preserve the same fact with punctuation or a separate line.
        creator = item["creator"].split("(", 1)[0].strip()
        require(creator in credits, f"Creator absent from credits: {item['creator']}")
    require("Benboncan" in credits and "CC BY 4.0" in credits and
            "https://creativecommons.org/licenses/by/4.0/" in credits, "Heartbeat attribution incomplete")
    return dict(status="FAIL" if errors else "PASS", scope=SCOPE, mandatoryCues=len(expected),
                verifiedCues=len(cues), retainedRecordings=source_count, errors=errors, cues=cues)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = verify()
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf8")
    print(json.dumps({key: value for key, value in result.items() if key != "cues"}, ensure_ascii=True))
    sys.exit(0 if result["status"] == "PASS" else 1)
