"""Original short metal-latch clacks. Regenerate with Python 3, no dependencies.

Two mechanical contacts (latch/pawl then seating), with damped metal resonances.
No door creak, voice, cloth, ambience or external recording is mixed in.
"""
from pathlib import Path
import hashlib
import json
import math
import random
import struct
import wave

PROJECT = Path(__file__).resolve().parents[2]
RATE = 24000


def render(opening):
    seconds = .24 if opening else .28
    rng = random.Random(7821 if opening else 7822)
    samples = []
    for index in range(round(seconds * RATE)):
        t = index / RATE
        value = 0.0
        for start, gain, pitch in ((0, .70, 1.12 if opening else 1),
                                   (.055 if opening else .072, 1, .96)):
            u = t - start
            if u < 0:
                continue
            attack = min(1, u / .0007)
            # Heavy latch body followed by the sharper steel pawl's brief ring.
            value += gain * attack * (
                .34 * math.exp(-65*u) * math.sin(2*math.pi*210*pitch*u) +
                .22 * math.exp(-90*u) * math.sin(2*math.pi*1170*pitch*u) +
                .12 * math.exp(-115*u) * math.sin(2*math.pi*2860*pitch*u) +
                .28 * math.exp(-210*u) * (rng.random()*2-1))
        samples.append(value * min(1, (seconds-t)/.015))
    peak = max(abs(value) for value in samples)
    samples = [value * .78 / peak for value in samples]
    return samples


def main():
    manifest_path = PROJECT / 'ThirdParty/Audio/manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    for opening in (True, False):
        cue = 'cabinet-open' if opening else 'cabinet-close'
        samples = render(opening)
        path = PROJECT / 'Assets/Resources/Audio/External' / (cue+'-0.wav')
        with wave.open(str(path), 'wb') as stream:
            stream.setparams((1, 2, RATE, len(samples), 'NONE', 'not compressed'))
            stream.writeframes(struct.pack('<'+'h'*len(samples), *(round(x*32767) for x in samples)))
        record = dict(title='Original metal cabinet latch '+('release' if opening else 'seat'),
                      author='HappyToyV2 project', license='Original project synthesis',
                      source_file='SourceArt/Audio/render_cabinet_latch.py',
                      source_file_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                      resource='Audio/External/'+cue+'-0', file=path.name,
                      modifications=dict(sample_rate=RATE, mono=True, format='PCM16 WAV',
                                         layers=['original latch and pawl contacts'],
                                         scope='Short metal clack; no external samples or door creak'),
                      seconds=len(samples)/RATE, peak=max(map(abs,samples)),
                      rms=math.sqrt(sum(x*x for x in samples)/len(samples)),
                      sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        manifest = [record if entry.get('resource') == record['resource'] else entry for entry in manifest]
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


if __name__ == '__main__':
    main()
