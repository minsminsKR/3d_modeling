"""Extract four real forest-shoe contacts from the preserved CC0 preview.

Python 3.10+ and ffmpeg on PATH; no downloads, synthesis or device playback.
"""
from array import array
import argparse
from pathlib import Path
import hashlib
import json
import math
import shutil
import subprocess
import sys
import tempfile
import wave

argparse.ArgumentParser(description=__doc__).parse_args()

project = Path(__file__).resolve().parents[2]
source = project / 'ThirdParty/Audio/TempleForest/sources/forest-sneaker-hq.mp3'
destination = project / 'Assets/Resources/Audio/External'
expected = 'c50bc4343fce1cfdd308ab7f41334f459f1dcd9b54a94ef046d1f243385a4236'
rate = 48000
centres = (14.28, 16.75, 18.33, 20.63)
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
if digest(source) != expected:
    raise RuntimeError('Forest recording changed')
ffmpeg = shutil.which('ffmpeg')
if not ffmpeg:
    raise RuntimeError('ffmpeg is required to decode the preserved MP3')
with tempfile.TemporaryDirectory(prefix='temple-steps-') as directory:
    decoded = Path(directory) / 'decoded.wav'
    subprocess.run([ffmpeg, '-v', 'error', '-y', '-i', str(source), '-ac', '1', '-ar', str(rate),
                    '-acodec', 'pcm_s16le', str(decoded)], check=True)
    with wave.open(str(decoded), 'rb') as stream:
        if stream.getframerate() != rate or stream.getnchannels() != 1 or stream.getsampwidth() != 2:
            raise RuntimeError('Unexpected decoded format')
        samples = array('h', stream.readframes(stream.getnframes()))
if sys.byteorder != 'little': samples.byteswap()
manifest_path = project / 'ThirdParty/Audio/manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
entries = []
for index, centre in enumerate(centres):
    start = round((centre - .08) * rate); length = round(.38 * rate)
    clip = samples[start:start + length]
    if len(clip) != length: raise RuntimeError('Source excerpt is truncated')
    peak = max(abs(value) for value in clip)
    if peak < 64: raise RuntimeError('Source contact is unexpectedly silent')
    gain = .65 * 32767 / peak
    attack, release = round(.008 * rate), round(.06 * rate)
    for i in range(length):
        fade = min(1.0, i / attack, (length - 1 - i) / release)
        clip[i] = max(-32768, min(32767, round(clip[i] * gain * fade)))
    # Peak normalization alone makes the fourth regularly repeated contact
    # much louder than the first two. Limit the loudest 50 ms window without
    # boosting quieter recordings or removing their natural transients.
    window = round(.05 * rate)
    energy = sum(value * value for value in clip[:window])
    maximum = energy
    for i in range(window, length):
        energy += clip[i] * clip[i] - clip[i-window] * clip[i-window]
        maximum = max(maximum, energy)
    window_rms = math.sqrt(maximum / window) / 32768
    balance = min(1.0, .085 / window_rms) if window_rms else 1.0
    if balance < 1:
        for i, value in enumerate(clip): clip[i] = round(value * balance)
    path = destination / ('step-forest-' + str(index) + '.wav')
    if sys.byteorder != 'little': clip.byteswap()
    with wave.open(str(path), 'wb') as out:
        out.setnchannels(1); out.setsampwidth(2); out.setframerate(rate); out.writeframes(clip.tobytes())
    if sys.byteorder != 'little': clip.byteswap()
    entries.append(dict(resource='Audio/External/step-forest-'+str(index), file=path.name,
        pack='temple-forest-steps', creator='Sheyvan (Philipp Grzemba)', license='CC0-1.0',
        page='https://freesound.org/people/Sheyvan/sounds/830876/',
        download='https://cdn.freesound.org/previews/830/830876_3248005-hq.mp3',
        source_file='forest-sneaker-hq.mp3', source_file_sha256=expected,
        seconds=length/rate, peak=max(abs(x) for x in clip)/32768,
        rms=math.sqrt(sum((x/32768)**2 for x in clip)/length), sha256=digest(path),
        modifications=dict(sample_rate=rate, mono=True, format='PCM16 WAV', loop=False,
            scope='Physical forest sneaker contact, centre %.2fs; -80/+300ms, peak at most .65, 8ms attack, 60ms release, maximum 50ms RMS .085 without boosting; no synthesis.' % centre)))
by_name = {entry['resource']: entry for entry in entries}
existing = {entry.get('resource') for entry in manifest}
manifest = [by_name.get(entry.get('resource'), entry) for entry in manifest]
manifest.extend(entry for entry in entries if entry['resource'] not in existing)
manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print('TEMPLE_FOREST_STEPS', len(entries), 'PASS')
