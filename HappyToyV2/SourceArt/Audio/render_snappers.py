"""Derive three rapid three-tick bursts from retained real CC0 snapper impacts.
Python 3.11+, numpy==1.26.4 and soundfile==0.13.1. No synthesis or generated layers.
Run from any directory: python HappyToyV2/SourceArt/Audio/render_snappers.py
"""
from pathlib import Path
import hashlib,json
import numpy as np
import soundfile as sf
PROJECT=Path(__file__).resolve().parents[2]
SOURCE=PROJECT/'ThirdParty/Audio/Snappers'
DEST=PROJECT/'Assets/Resources/Audio/External'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    entry=json.loads((SOURCE/'sources.json').read_text(encoding='utf-8'))['whipper-snappers']
    original=SOURCE/'sources'/entry['file']
    if sha(original)!=entry['source_file_sha256']:raise ValueError('Original recording changed')
    samples,rate=sf.read(original,dtype='float64')
    if samples.ndim>1:samples=samples.mean(axis=1)
    if rate!=48000:raise ValueError('Expected retained 48kHz recording')
    manifest_path=PROJECT/'ThirdParty/Audio/manifest.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    cues=[]
    # Three separate recorded impacts, shortened so their attacks remain distinct.
    takes=[]
    for peak_seconds in (1.418,3.408,5.914):
        start=max(0,round((peak_seconds-.006)*rate));end=round((peak_seconds+.054)*rate)
        take=samples[start:end].copy();take-=take.mean()
        take*=.65/max(1e-9,np.max(np.abs(take)))
        attack=round(.001*rate);release=round(.012*rate)
        take[:attack]*=np.linspace(0,1,attack);take[-release:]*=np.linspace(1,0,release)
        takes.append(take)
    for index in range(3):
        take=np.zeros(round(.28*rate))
        for tick,offset in enumerate((0,.075,.150)):
            hit=takes[(index+tick)%3]*(1 if tick!=1 else .9)
            begin=round(offset*rate);take[begin:begin+len(hit)]+=hit
        path=DEST/('firecracker-'+str(index)+'.wav');sf.write(path,take,rate,subtype='PCM_16')
        actual,_=sf.read(path,dtype='float64')
        cue=dict(entry,resource='Audio/External/firecracker-'+str(index),file=path.name,
            modifications=dict(sample_rate=rate,mono=True,format='PCM16 WAV',loop=False,
                scope='One collision plays three distinct recorded paper snapper ticks at 0/75/150ms. Source windows are -6/+54ms around 1.418/3.408/5.914s; DC removal, peak .65, 1/12ms fades, second tick gain .9. One AI hearing observation per burst.',
                recordings=[entry],source_kind='recorded; real physical Whipper Snapper Fireworks, no oscillator/noise layers',
                tick_offsets_seconds=[0,.075,.150],source_peak_seconds=[1.418,3.408,5.914]),
            seconds=len(actual)/rate,peak=float(np.max(np.abs(actual))),rms=float(np.sqrt(np.mean(actual**2))),sha256=sha(path))
        cues.append(cue)
    by_name={cue['file']:cue for cue in cues}
    manifest=[by_name.get(cue['file'],cue) for cue in manifest]
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (SOURCE/'selection.json').write_text(json.dumps(cues,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps([dict(file=c['file'],seconds=c['seconds'],peak=c['peak']) for c in cues],indent=2))
if __name__=='__main__':main()
