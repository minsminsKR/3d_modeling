"""Recorded pounding contacts and human near-whistles; no speaker output.

Run with Python 3.11 and requirements-corridor.txt from any working directory.
Only retained, hash-checked physical contact and human whistle recordings enter
these cues. No oscillators, synthetic noise or missing-resource fallback.
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
ROOT = PROJECT / "ThirdParty/Audio/CorridorMask"
DEST = PROJECT / "Assets/Resources/Audio/External"
RATE = 48000


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(root, info, start=0, end=None, speed=1, hp=45, lp=9000):
    path = root / "sources" / info["file"]
    assert sha(path) == info["source_file_sha256"], str(path)
    x, rate = sf.read(path, always_2d=True)
    x = x[round(start * rate):round(end * rate) if end else None].mean(axis=1)
    x = resample_poly(x, RATE, round(rate * speed))
    x -= x.mean()
    if hp:
        x = sosfiltfilt(butter(2, hp, btype="highpass", fs=RATE, output="sos"), x)
    if lp:
        x = sosfiltfilt(butter(2, lp, fs=RATE, output="sos"), x)
    return x


def fade(x, attack=.004, release=.09):
    x = x.copy()
    n, m = min(round(attack * RATE), len(x) // 2), min(round(release * RATE), len(x) // 2)
    if n:
        x[:n] *= np.linspace(0, 1, n) ** 2
    if m:
        x[-m:] *= np.linspace(1, 0, m) ** 2
    return x


def level(x, peak):
    return x * peak / max(np.max(np.abs(x)), 1e-10)


def mix(seconds, layers):
    result = np.zeros(round(seconds * RATE))
    for x, offset, gain in layers:
        begin = round(offset * RATE)
        length = min(len(x), len(result) - begin)
        if length > 0:
            result[begin:begin + length] += x[:length] * gain
    return result


def render():
    sources = json.loads((ROOT / "sources.json").read_text(encoding="utf8"))
    old_root = PROJECT / "ThirdParty/Audio/RecordedHorror"
    old = json.loads((old_root / "sources.json").read_text(encoding="utf8"))
    parquet_root = PROJECT / "ThirdParty/Audio/CorridorFoley"
    parquet = json.loads((parquet_root / "sources.json").read_text(encoding="utf8"))["parquet"]
    for info in sources.values():
        assert sha(ROOT / "sources" / info["file"]) == info["source_file_sha256"]
    results = []

    def emit(name, x, origins, edits, peak):
        x = level(fade(x), peak)
        path = DEST / (name + ".wav")
        sf.write(path, x, RATE, subtype="PCM_16")
        meta = path.with_suffix(".wav.meta")
        if not meta.exists():
            template = (DEST / "enemy-baby-cry-0.wav.meta").read_text(encoding="utf8")
            template = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, template)
            meta.write_text("\n".join(line.rstrip() for line in template.splitlines()) + "\n", encoding="utf8")
        y, _ = sf.read(path)
        item = {key: value for key, value in origins[0].items() if key != "file"}
        item.update(resource="Audio/External/" + name, file=path.name, pack="corridor-mask-recorded-horror",
            modifications=dict(sample_rate=RATE, mono=True, format="PCM16 WAV", scope=edits,
                recordings=origins, loop=False,
                source_kind="Physical shoe/wood/metal contact and real human whistle recordings; edited and layered, no oscillator/noise synthesis."),
            seconds=len(y) / RATE, peak=float(np.max(np.abs(y))),
            rms=float(np.sqrt(np.mean(y * y))), sha256=sha(path))
        results.append(item)

    # Three distinct heel/board contacts become short, heavy, resonant pounding
    # contacts. Runtime cadence is driven by travelled distance, never a canned
    # running loop or a stationary/teleport-triggered invented footstep.
    plank = level(load(old_root, old["wood-clap"], speed=.69, hp=45, lp=1900), .65)
    body = level(load(old_root, old["metal-drop"], speed=.66, hp=45, lp=600), .32)
    for index, time in enumerate([2.12, 4.48, 5.39]):
        shoe = level(load(parquet_root, parquet, time - .1, time + .4, speed=.89, hp=45, lp=3800), .55)
        x = mix(.56, [(plank, 0, .66), (shoe, .015, .58), (body, .028, .30)])
        emit("mask-heavy-step-" + str(index), x, [parquet, old["wood-clap"], old["metal-drop"]],
            f"Distinct parquet heel contact near {time:.2f}s, slowed .89; physical plank clap .69 and low-passed metal body .66. Mono 48 kHz, 45Hz high-pass, separate 3.8/1.9/.6kHz low-pass, .56s contact with 4/90ms fades. Peak .72; gain/cadence controlled by runner emitter.", .72)
    # The peculiar three-breath shape is still a performed whistle. Independent
    # recorded pitches and uneven breath spacing preserve its mouth/breath detail.
    lip = level(load(ROOT, sources["lip-whistle"], .1, 1.25, speed=.96, hp=600, lp=6400), .50)
    pan = load(ROOT, sources["panflute-whistle"], 1.05, 2.8, speed=.91, hp=450, lp=6200)
    for index, spacing in enumerate([.39, .44]):
        dry = fade(lip[:round(.40 * RATE)], .014, .065)
        airy = fade(pan[round(index * .20 * RATE):round((index * .20 + .48) * RATE)], .017, .075)
        x = mix(1.90, [(dry, 0, .85), (airy, spacing, .72), (dry, spacing * 2 + .055, .58),
            (airy, .17, .10), (dry, .69, .10), (dry, 1.23, .07)])
        emit("mask-near-whistle-" + str(index), x, [sources["lip-whistle"], sources["panflute-whistle"]],
            f"Real lip whistle and tongue/front-teeth panflute-like human whistle; .96/.91 rate edits, triple uneven breath gestures at 0/{spacing:.2f}/{spacing*2+.055:.2f}s; quiet non-synthetic source delays. 450/600Hz high-pass, 6.2/6.4kHz low-pass, 1.90s fade. No generated pitch tone or noise layer; close-range only runtime emitter.", .43)
    metal = level(load(old_root, old["metal-drop"], speed=.87, hp=45, lp=6600), .50)
    scrape = level(load(old_root, old["metal-drag"], 2.1, 3.12, speed=.91, hp=85, lp=3400), .30)
    impact = level(load(old_root, old["wood-clap"], speed=.84, hp=45, lp=5400), .70)
    emit("mask-door-smash-0", mix(1.34, [(impact, 0, .84), (metal, .06, .55), (impact, .17, .38), (scrape, .19, .35)]),
        [old["wood-clap"], old["metal-drop"], old["metal-drag"]],
        "Physical plank clap, second split contact, dropped metal lock body and dragged metal friction; .84/.87/.91 rate edits. 1.34s captured impact and decay with 4/90ms fades, no synthetic crack. Bound to actual door break, not an unbreakable-door encounter.", .73)
    path = PROJECT / "ThirdParty/Audio/manifest.json"
    old_manifest = json.loads(path.read_text(encoding="utf8"))
    names = {item["file"] for item in results}
    path.write_text(json.dumps([item for item in old_manifest if item["file"] not in names] + results,
        ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    (ROOT / "prepared-cues.json").write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps({"status": "PASS", "cues": len(results), "source_hashes_verified": True,
        "scope": "Offline PCM authoring only; no speaker playback or human listening certification."}))


if __name__ == "__main__":
    render()
