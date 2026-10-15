"""Derive three short reports from retained real CC0 party-snapper recording.
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
    # Independent recorded impacts; 18 ms pre-roll preserves the real transient.
    for index,peak_seconds in enumerate((1.418,3.408,5.914)):
        start=max(0,round((peak_seconds-.018)*rate));end=round((peak_seconds+.262)*rate)
        take=samples[start:end].copy();take-=take.mean()
        take*=.65/max(1e-9,np.max(np.abs(take)))
        ramp=round(.004*rate);take[:ramp]*=np.linspace(0,1,ramp);take[-ramp:]*=np.linspace(1,0,ramp)
        path=DEST/('firecracker-'+str(index)+'.wav');sf.write(path,take,rate,subtype='PCM_16')
        actual,_=sf.read(path,dtype='float64')
        cue=dict(entry,resource='Audio/External/firecracker-'+str(index),file=path.name,
            modifications=dict(sample_rate=rate,mono=True,format='PCM16 WAV',loop=False,
                scope='One recorded paper party-snapper impact. Trim, DC removal, gain limit, 4ms edge fades only.',
                recordings=[entry],source_kind='recorded; real physical Whipper Snapper Fireworks, no oscillator/noise layers',
                segment_start_seconds=start/rate,segment_end_seconds=end/rate),
            seconds=len(actual)/rate,peak=float(np.max(np.abs(actual))),rms=float(np.sqrt(np.mean(actual**2))),sha256=sha(path))
        cues.append(cue)
    by_name={cue['file']:cue for cue in cues}
    manifest=[by_name.get(cue['file'],cue) for cue in manifest]
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (SOURCE/'selection.json').write_text(json.dumps(cues,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps([dict(file=c['file'],seconds=c['seconds'],peak=c['peak']) for c in cues],indent=2))
if __name__=='__main__':main()
