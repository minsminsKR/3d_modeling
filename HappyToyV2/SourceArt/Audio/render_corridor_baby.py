"""Prepare one real spatial Baby cry loop; CPython 3.11/requirements-corridor.txt.

Selected CC0 public HQ MP3 recording and comparison source are retained in Git.
No download, oscillator, invented voice, pitch shift or procedural noise layer.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfiltfilt

PROJECT = Path(__file__).resolve().parents[2]
ROOT = PROJECT / "ThirdParty/Audio/CorridorBaby"
DEST = PROJECT / "Assets/Resources/Audio/External"
RATE = 48000


def render():
    sources = json.loads((ROOT / "sources.json").read_text(encoding="utf8"))
    for item in sources.values():
        raw = ROOT / "sources" / item["file"]
        assert hashlib.sha256(raw.read_bytes()).hexdigest() == item["source_file_sha256"]
    selected = sources["zoom"]
    x, rate = sf.read(ROOT / "sources" / selected["file"], always_2d=True)
    x = x[round(6 * rate):round(22 * rate)].mean(axis=1)
    x = resample_poly(x, RATE, rate)
    x -= x.mean()
    x = sosfiltfilt(butter(2, 85, fs=RATE, btype="highpass", output="sos"), x)
    x = sosfiltfilt(butter(2, 7600, fs=RATE, output="sos"), x)
    overlap = round(.45 * RATE)
    blend = np.linspace(0, 1, overlap)
    tail = x[-overlap:].copy()
    x = x[:-overlap].copy()
    x[:overlap] = tail * (1 - blend) + x[:overlap] * blend
    x *= .50 / max(float(np.max(np.abs(x))), 1e-10)
    path = DEST / "enemy-baby-cry-0.wav"
    sf.write(path, x, RATE, subtype="PCM_16")
    meta = path.with_suffix(".wav.meta")
    if not meta.exists():
        template = (DEST / "door-open-0.wav.meta").read_text(encoding="utf8")
        template = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, template)
        meta.write_text("\n".join(line.rstrip() for line in template.splitlines()) + "\n", encoding="utf8")
    y, _ = sf.read(path)
    record = {key: value for key, value in selected.items() if key != "file"}
    record.update(resource="Audio/External/" + path.stem, file=path.name,
        pack="corridor-baby-recorded-cry", seconds=len(y) / RATE,
        peak=float(np.max(np.abs(y))), rms=float(np.sqrt(np.mean(y * y))),
        sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        modifications=dict(sample_rate=RATE, mono=True, format="PCM16 WAV", loop=True,
            recordings=[selected], source_kind="Actual baby voice recorded on a Zoom H4n Pro; edited recording, no synthesized voice/noise/tone.",
            scope="Selected source 6–22 s; mono, 85 Hz high-pass/7.6 kHz low-pass, preserved natural pitch and breath gaps, .45 s wrap crossfade to 15.55 s loop; measured peak .50. Spatial gain/occlusion is applied by the real actor emitter."))
    manifest_path = PROJECT / "ThirdParty/Audio/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf8"))
    manifest = [item for item in manifest if item["file"] != path.name] + [record]
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf8")
    (ROOT / "prepared-cues.json").write_text(json.dumps([record], indent=2, ensure_ascii=False) + "\n", encoding="utf8")
    print(json.dumps({"status": "PASS", "cue": record["resource"], "seconds": record["seconds"], "sha256": record["sha256"]}))


if __name__ == "__main__":
    render()
