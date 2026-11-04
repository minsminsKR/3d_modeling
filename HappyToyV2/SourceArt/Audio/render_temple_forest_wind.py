"""Derive one seamless mono forest-wind loop from the preserved CC0 preview.

Python 3.10+ and ffmpeg on PATH; no download, synthesis or device playback.
"""
from array import array
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import wave

project = Path(__file__).resolve().parents[2]
source = project / 'ThirdParty/Audio/TempleForest/sources/wind-through-trees-hq.mp3'
destination = project / 'Assets/Resources/Audio/External/ambience-forest-0.wav'
expected = '2ee7c4bf9fc169a0727a6d7bde2d8b801987fc8b6f0cf27f6bb5040ac9ba38d5'
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
if digest(source) != expected:
    raise RuntimeError('Forest wind recording changed')
ffmpeg = shutil.which('ffmpeg')
if not ffmpeg:
    raise RuntimeError('ffmpeg is required to decode the preserved MP3')
rate, start, duration, fade = 24000, 8, 52, 2
with tempfile.TemporaryDirectory(prefix='temple-wind-') as directory:
    decoded = Path(directory) / 'decoded.wav'
    subprocess.run([ffmpeg, '-v', 'error', '-y', '-i', str(source), '-ac', '1', '-ar', str(rate),
                    '-acodec', 'pcm_s16le', str(decoded)], check=True)
    with wave.open(str(decoded), 'rb') as stream:
        if stream.getframerate() != rate or stream.getnchannels() != 1 or stream.getsampwidth() != 2:
            raise RuntimeError('Unexpected decoded format')
        stream.setpos(start * rate)
        samples = array('h', stream.readframes(duration * rate))
if sys.byteorder != 'little': samples.byteswap()
if len(samples) != duration * rate:
    raise RuntimeError('Forest wind excerpt is truncated')
overlap = fade * rate
cross = array('h')
for i in range(overlap):
    t = i / (overlap - 1)
    cross.append(round(samples[-overlap + i] * (1 - t) + samples[i] * t))
loop = samples[overlap:-overlap] + cross
peak = max(abs(x) for x in loop)
if peak < 64: raise RuntimeError('Forest wind excerpt is unexpectedly silent')
gain = .5 * 32767 / peak
for i, value in enumerate(loop):
    loop[i] = max(-32768, min(32767, round(value * gain)))
if sys.byteorder != 'little': loop.byteswap()
with wave.open(str(destination), 'wb') as out:
    out.setnchannels(1); out.setsampwidth(2); out.setframerate(rate); out.writeframes(loop.tobytes())
if sys.byteorder != 'little': loop.byteswap()
entry = dict(resource='Audio/External/ambience-forest-0', file=destination.name,
    pack='temple-forest-wind', creator='Noted451', license='CC0-1.0',
    page='https://freesound.org/people/Noted451/sounds/531016/',
    download='https://cdn.freesound.org/previews/531/531016_9818404-hq.mp3',
    source_file='wind-through-trees-hq.mp3', source_file_sha256=expected,
    seconds=len(loop)/rate, peak=max(abs(x) for x in loop)/32768,
    rms=math.sqrt(sum((x/32768)**2 for x in loop)/len(loop)), sha256=digest(destination),
    modifications=dict(sample_rate=rate, mono=True, format='PCM16 WAV', loop=True,
        scope='Actual wind through a wooded area; 8–60s excerpt, mono 24kHz, 2s linear wrap crossfade, peak .5; no synthesis.'))
manifest_path = project / 'ThirdParty/Audio/manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
manifest = [item for item in manifest if item.get('resource') != entry['resource']] + [entry]
manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print('TEMPLE_FOREST_WIND PASS', len(loop)/rate, 'seconds', entry['sha256'])
