"""Prepare the corridor Uncat's recorded bell; no synthesis or downloads."""
from pathlib import Path
import hashlib
import json
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly
from math import gcd

PROJECT = Path(__file__).resolve().parents[2]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    manifest_path = PROJECT / 'ThirdParty/Audio/manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    source = PROJECT / 'ThirdParty/Audio/RecordedHorror/sources/bell.ogg'
    candidate = PROJECT / 'ThirdParty/Audio/RecordedHorror/sources/metal-jingle.wav'
    origin = next(item for item in manifest if item['resource'] == 'Audio/External/story-bell-0')
    audio, rate = sf.read(source, always_2d=True)
    audio = audio.mean(axis=1)
    common = gcd(rate, 48000)
    audio = resample_poly(audio, 48000 // common, rate // common)
    # Keep the actual bell onset and ring, ending at a quiet 0.70-second tail.
    onset = max(0, int(np.flatnonzero(np.abs(audio) > .01)[0]) - 240)
    audio = audio[onset:onset + 33600].copy()
    fade = min(4800, len(audio))
    audio[-fade:] *= np.linspace(1, 0, fade)
    audio *= .60 / max(.00001, float(np.max(np.abs(audio))))
    target = PROJECT / 'Assets/Resources/Audio/External/enemy-uncat-bell-0.wav'
    sf.write(target, audio, 48000, subtype='PCM_16')
    entry = {key: origin[key] for key in ('pack','creator','license','page','download','source_file','source_file_sha256','archive_sha256')}
    entry.update(resource='Audio/External/enemy-uncat-bell-0', file=target.name,
        modifications=dict(sample_rate=48000, mono=True, format='PCM16 WAV', loop=False,
            scope='Actual recorded small bell onset and ring, shortened with a 100ms tail fade. Runtime uses soft isolated patrol rings and faster pursuit rings; no synthesized layers.',
            recordings=origin['modifications']['recordings'], source_kind='recorded physical bell'),
        seconds=len(audio)/48000, peak=float(np.max(np.abs(audio))), rms=float(np.sqrt(np.mean(audio**2))), sha256=sha(target))
    manifest = [item for item in manifest if item['resource'] != entry['resource']] + [entry]
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    selection = dict(selected=dict(file=source.relative_to(PROJECT).as_posix(), sha256=sha(source),
        reason='A real pitched bell ring reads as a small carried bell; retain its natural pitch.'),
        compared=dict(file=candidate.relative_to(PROJECT).as_posix(), sha256=sha(candidate),
        reason='Loose metal jingle is noisier and less clearly a bell, so it is not mixed into the cue.'),
        gain='Patrol 0.50; pursuit 1.00 on the same spatial emitter; waveform peak 0.60 (previously 0.42)',
        interval_seconds=dict(patrol=.88,pursuit=.23), licensing=entry['license'],
        listening_scope='Selection from known retained physical recordings; PCM/source checks do not certify the final device mix.')
    (PROJECT/'SourceArt/Audio/uncat-bell-selection.json').write_text(json.dumps(selection,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(resource=entry['resource'],sha256=entry['sha256'],seconds=entry['seconds'])))

if __name__ == '__main__': main()
