"""Offline child-character Korean mutter preparation; never plays audio.

Retained neural speech is voiced/formant edited without speeding the sentence
up. Genuine recorded infant cries remain a separate game source. Generated
character speech is disclosed and must not be described as a real child actor.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
import numpy as np
import pyworld as world
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfiltfilt

PROJECT = Path(__file__).resolve().parents[2]
ROOT = PROJECT / "ThirdParty/Audio/CorridorBabyMutter"
DEST = PROJECT / "Assets/Resources/Audio/External"
RATE = 48000
VOICE_RATE = 24000


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def render():
    sources = json.loads((ROOT / "sources.json").read_text(encoding="utf8"))
    for item in sources.values():
        assert sha(ROOT / "sources" / item["file"]) == item["source_file_sha256"]
    records = []
    for index, key in enumerate(["seeking-soft", "seeking-hesitant"]):
        origin = sources[key]
        x, rate = sf.read(ROOT / "sources" / origin["file"], always_2d=True)
        x = resample_poly(x.mean(axis=1), VOICE_RATE, rate).astype(np.float64)
        x -= x.mean()
        f0, times = world.harvest(x, VOICE_RATE, f0_floor=85, f0_ceil=650, frame_period=5)
        f0 = world.stonemask(x, f0, times, VOICE_RATE)
        spectrum = world.cheaptrick(x, f0, times, VOICE_RATE)
        aperiodicity = world.d4c(x, f0, times, VOICE_RATE)
        voiced = f0[f0 > 0]
        assert len(voiced) > 8, "No voiced speech in original " + key
        original_median = float(np.median(voiced))
        # Change vocal fold pitch and tract resonances independently; preserving
        # the original timing avoids a chipmunk-rate/speed shortcut.
        pitch_ratio = float(np.clip((330 if index == 0 else 345) / original_median, 1.05, 1.65))
        formant_ratio = 1.16 if index == 0 else 1.18
        bins = np.arange(spectrum.shape[1])
        spectrum = np.array([np.interp(bins / formant_ratio, bins, row) for row in spectrum])
        shifted = f0 * pitch_ratio
        # Preserve the estimated breath/consonant pattern with a small increase
        # of the existing aperiodic component, not an added generic noise layer.
        aperiodicity = np.minimum(1, aperiodicity * 1.04)
        y = world.synthesize(shifted, spectrum, aperiodicity, VOICE_RATE, frame_period=5)
        y = resample_poly(y, RATE, VOICE_RATE)
        y = sosfiltfilt(butter(2, 105, fs=RATE, btype="highpass", output="sos"), y)
        y = sosfiltfilt(butter(2, 5600, fs=RATE, output="sos"), y)
        attack, release = round(.014 * RATE), round(.055 * RATE)
        y[:attack] *= np.linspace(0, 1, attack) ** 2
        y[-release:] *= np.linspace(1, 0, release) ** 2
        y *= .38 / max(float(np.max(np.abs(y))), 1e-10)
        path = DEST / ("enemy-baby-mutter-" + str(index) + ".wav")
        sf.write(path, y, RATE, subtype="PCM_16")
        meta = path.with_suffix(".wav.meta")
        if not meta.exists():
            template = (DEST / "enemy-baby-cry-0.wav.meta").read_text(encoding="utf8")
            template = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, template)
            meta.write_text("\n".join(line.rstrip() for line in template.splitlines()) + "\n", encoding="utf8")
        data, _ = sf.read(path)
        item = {field: value for field, value in origin.items() if field != "file"}
        item.update(resource="Audio/External/" + path.stem, file=path.name,
            pack="corridor-baby-generated-korean-mutter", seconds=len(data) / RATE,
            peak=float(np.max(np.abs(data))), rms=float(np.sqrt(np.mean(data * data))), sha256=sha(path),
            modifications=dict(sample_rate=RATE, mono=True, format="PCM16 WAV", loop=False,
                recordings=[origin], generated_voice=dict(model=origin["model"], model_revision=origin["model_revision"],
                    tool_revision=origin["tool_revision"], phrase=origin["phrase"], clones_person=False,
                    raw_source_sha256=origin["source_file_sha256"],
                    generator="SourceArt/Audio/generate_baby_mutter.py", renderer="SourceArt/Audio/render_baby_mutter.py"),
                source_kind="Locally generated Korean neural character speech; not a recording of a real child. Independent vocal-pitch and formant edits preserve timing and Korean consonants.",
                scope="Two question phrases '어디? 어디 있어?' with authored hesitation; preserved duration, WORLD 24kHz/5ms voiced/resonance analysis, 105Hz high-pass/5.6kHz low-pass, 14/55ms edge fades, peak .38; no voice service, cloned real person, oscillator or generic added noise.",
                raw_median_f0=original_median, pitch_ratio=pitch_ratio, formant_ratio=formant_ratio,
                prepared_median_f0=original_median * pitch_ratio, world_version="0.3.5"))
        records.append(item)
    manifest_path = PROJECT / "ThirdParty/Audio/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf8"))
    names = {record["file"] for record in records}
    manifest_path.write_text(json.dumps([record for record in manifest if record["file"] not in names] + records,
        ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    (ROOT / "prepared-cues.json").write_text(json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps({"status": "PASS", "prepared": len(records), "speaker_output": False,
        "voicing": [{"raw_f0": item["modifications"]["raw_median_f0"],
            "prepared_f0": item["modifications"]["prepared_median_f0"], "seconds": item["seconds"]} for item in records],
        "scope": "Offline data and authoring only, not human listening quality."}))


if __name__ == "__main__":
    render()
