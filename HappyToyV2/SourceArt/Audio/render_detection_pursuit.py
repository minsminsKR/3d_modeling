"""Original horror recognition stings and recorded pursuit music; never plays.

CPython 3.11, requirements-corridor.txt. All normal inputs are retained,
hash-checked CC0 recordings. No copied game music, oscillator or generic noise
replacement. The chase bed is bowed acoustic strings and physical percussion,
kept separate from the game's real recorded heartbeat.
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
ROOT = PROJECT / "ThirdParty/Audio/DetectionPursuit"
OLD_ROOT = PROJECT / "ThirdParty/Audio/RecordedHorror"
DEST = PROJECT / "Assets/Resources/Audio/External"
RATE = 48000
LOOP_SECONDS = 6.4
SEAM_SECONDS = .4


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(root, origin, start=0, end=None, speed=1, hp=60, lp=8000, trim=False):
    path = root / "sources" / origin["file"]
    assert sha(path) == origin["source_file_sha256"], "Source recording changed: " + str(path)
    x, rate = sf.read(path, always_2d=True)
    x = x[round(start * rate):round(end * rate) if end else None].mean(axis=1)
    assert len(x) > 256, "Empty recording excerpt"
    x = resample_poly(x, RATE, round(rate * speed)); x -= x.mean()
    if hp:
        x = sosfiltfilt(butter(2, hp, fs=RATE, btype="highpass", output="sos"), x)
    if lp:
        x = sosfiltfilt(butter(2, lp, fs=RATE, output="sos"), x)
    if trim:
        active = np.flatnonzero(np.abs(x) > max(float(np.max(np.abs(x))) * .018, .0001))
        assert len(active) > 0, "Silent selected recording"
        x = x[max(0, active[0] - 180):min(len(x), active[-1] + 2800)]
    return x


def level(x, peak):
    return x * peak / max(float(np.max(np.abs(x))), 1e-10)


def fade(x, attack=.003, release=.1):
    x = x.copy()
    a, b = min(round(attack * RATE), len(x) // 2), min(round(release * RATE), len(x) // 2)
    if a:
        x[:a] *= np.linspace(0, 1, a) ** 2
    if b:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def mix(seconds, layers):
    result = np.zeros(round(seconds * RATE))
    for x, offset, gain in layers:
        begin = round(offset * RATE); length = min(len(x), len(result) - begin)
        if length > 0:
            result[begin:begin + length] += x[:length] * gain
    return result


def loop_bed(x):
    length, seam = round(LOOP_SECONDS * RATE), round(SEAM_SECONDS * RATE)
    assert len(x) >= length + seam, "Source is too short for the authored continuous seam"
    result = x[:length].copy()
    blend = np.linspace(0, 1, seam)
    # The loop's last sample advances into the source tail at the first sample;
    # a .4s overlap returns to the head without a silence dip or hard cut.
    result[:seam] = x[length:length + seam] * (1 - blend) + result[:seam] * blend
    return result


def cyclic_contact(result, contact, offset, gain):
    begin = round(offset * RATE) % len(result)
    count = min(len(contact), len(result) - begin)
    result[begin:begin + count] += contact[:count] * gain
    remainder = len(contact) - count
    if remainder > 0:
        result[:remainder] += contact[count:] * gain


def render():
    origins = json.loads((ROOT / "sources.json").read_text(encoding="utf8"))
    old = json.loads((OLD_ROOT / "sources.json").read_text(encoding="utf8"))
    for item in origins.values():
        assert sha(ROOT / "sources" / item["file"]) == item["source_file_sha256"]
    prepared = []

    def emit(name, x, inputs, peak, edits, loop=False, **extra):
        x = level(x if loop else fade(x), peak)
        assert np.isfinite(x).all() and np.max(np.abs(x)) < 1
        path = DEST / (name + ".wav")
        sf.write(path, x, RATE, subtype="PCM_16")
        meta = path.with_suffix(".wav.meta")
        if not meta.exists():
            template = (DEST / "enemy-baby-cry-0.wav.meta").read_text(encoding="utf8")
            template = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, template)
            meta.write_text("\n".join(line.rstrip() for line in template.splitlines()) + "\n", encoding="utf8")
        y, _ = sf.read(path)
        item = {field: value for field, value in inputs[0].items() if field != "file"}
        modifications = dict(sample_rate=RATE, mono=True, format="PCM16 WAV", loop=loop, scope=edits,
            recordings=inputs, source_kind="Human performances, physical wood/metal contacts and licensed acoustic violin recordings. Original recorded-layer composition; no copied game audio, heartbeat substitution, oscillator or generic noise synthesis.")
        modifications.update(extra)
        item.update(resource="Audio/External/" + name, file=path.name, pack="recorded-detection-pursuit",
            modifications=modifications, seconds=len(y) / RATE,
            peak=float(np.max(np.abs(y))), rms=float(np.sqrt(np.mean(y * y))), sha256=sha(path))
        prepared.append(item)

    metal = level(read(OLD_ROOT, old["metal-drop"], speed=.82, hp=65, lp=5100, trim=True), .70)
    wood = level(read(OLD_ROOT, old["wood-clap"], speed=.76, hp=60, lp=3900, trim=True), .66)
    panic = level(read(OLD_ROOT, old["panic"], speed=.85, hp=145, lp=4500, trim=True), .52)
    slash = level(read(ROOT, origins["violin-scratch"], 12.15, 13.25, speed=.91, hp=650, lp=7200), .42)
    scream = level(read(OLD_ROOT, old["scream"], speed=.91, hp=240, lp=5200, trim=True), .40)
    emit("detection-impact-0", mix(1.06, [(metal, 0, .76), (wood, .011, .66),
        (panic, .058, .46), (slash, .09, .34)]),
        [old["metal-drop"], old["wood-clap"], old["panic"], origins["violin-scratch"]], .50,
        "Immediate real dropped-metal/plank attack, performed frightened breath at 58ms, acoustic violin rasp at 90ms. Source-rate .82/.76/.85/.91, distinct recorded contact/voice body; 1.06s, 3/100ms edge fades, measured peak .50. No lead-in delay or fabricated tone.")
    emit("detection-impact-1", mix(1.12, [(wood, 0, .76), (metal, .018, .61),
        (slash, .035, .52), (scream, .071, .40), (panic, .15, .19)]),
        [old["wood-clap"], old["metal-drop"], origins["violin-scratch"], old["scream"], old["panic"]], .52,
        "Independent sharp plank/metal recognition contact, 35ms acoustic bow rasp, shortened genuine performed scream at 71ms and frightened breath at 150ms. 1.12s, separate filtering/source-rate edits and 3/100ms fades; measured peak .52. Original layered recording, not copied game audio.")

    tremolo = read(ROOT, origins["violin-tremolo"], .20, 7.30, speed=1.035, hp=160, lp=6800)
    lower_tremolo = read(ROOT, origins["violin-tremolo"], .20, 7.30, speed=.973, hp=160, lp=5300)
    rasp = read(ROOT, origins["violin-scratch"], 13.0, 19.80, speed=.94, hp=480, lp=6400)
    bed = loop_bed(level(tremolo, .46)) * .53 + loop_bed(level(lower_tremolo, .36)) * .37
    bed += loop_bed(level(rasp, .35)) * .22
    low_contact = fade(level(read(OLD_ROOT, old["wood-clap"], speed=.59, hp=60, lp=1700, trim=True), .44), .004, .13)
    high_contact = fade(level(read(OLD_ROOT, old["wood-clap"], speed=.83, hp=180, lp=3300, trim=True), .34), .004, .12)
    rattle = fade(level(read(OLD_ROOT, old["ratchet"], speed=.92, hp=500, lp=5900, trim=True), .25), .005, .09)
    for beat in range(16):
        offset = beat * .4
        cyclic_contact(bed, low_contact if beat % 4 == 0 else high_contact, offset,
            .62 if beat % 4 == 0 else .35 if beat % 2 == 0 else .24)
        # Off-beat physical ratchet articulation, not a duplicated heartbeat.
        cyclic_contact(bed, rattle, offset + .2, .23 if beat % 2 == 0 else .15)
    emit("pursuit-loop-0", bed, [origins["violin-tremolo"], origins["violin-scratch"], old["wood-clap"], old["ratchet"]],
        .32, "Original 150BPM/16-beat/6.4s continuous pursuit bed. Two near-detuned rates of the recorded F3 violin tremolo (.20–7.30s at 1.035/.973) plus acoustic bow rasp (13–19.80s at .94), physical low/high plank percussion and off-beat ratchet. Each string bed has .4s natural tail/head overlap; percussion wraps its real decay across the bar. No global fade-to-silence seam, copied melody, synthetic oscillator/noise, or heartbeat layer. Measured peak .32; runtime owner controls onset/outro/gain.",
        loop=True, tempo_bpm=150, beats=16, seam_overlap_seconds=SEAM_SECONDS,
        recommended_runtime_max_gain=.22)
    manifest_path = PROJECT / "ThirdParty/Audio/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf8"))
    names = {item["file"] for item in prepared}
    manifest_path.write_text(json.dumps([item for item in manifest if item["file"] not in names] + prepared,
        ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    (ROOT / "prepared-cues.json").write_text(json.dumps(prepared, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    y, _ = sf.read(DEST / "pursuit-loop-0.wav")
    report = dict(status="PASS", prepared=len(prepared), speaker_output=False,
        loop_seconds=len(y) / RATE, loop_seam_delta=float(abs(y[0] - y[-1])),
        loop_derivative_p999=float(np.quantile(np.abs(np.diff(y)), .999)),
        scope="Offline composition/PCM only; not native game mix or human listening quality.")
    (ROOT / "authoring-check.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf8")
    print(json.dumps(report))


if __name__ == "__main__":
    render()
