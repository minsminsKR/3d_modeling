"""Derive a looping Cyclopse pant from retained CC0 human performances; no synthesis."""
from pathlib import Path
import hashlib
import json
from math import gcd
import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

PROJECT=Path(__file__).resolve().parents[2]
RAW=PROJECT/'ThirdParty/Audio/RecordedHorror/sources'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    catalog=json.loads((RAW.parent/'sources.json').read_text(encoding='utf-8-sig'))
    comparisons=[]
    for key,reason in [('scared','Repeated performed frightened inhales/exhales, suitable for ongoing laboured creature breathing.'),
                       ('panic','Short vocal gasp already used for the entrance roar; unsuitable as the ongoing panting loop.'),
                       ('breath','Quieter solo breath retained for the player; keep the creature distinct.')]:
        audio,rate=sf.read(RAW/catalog[key]['file'],always_2d=True)
        comparisons.append(dict(key=key,file=(RAW/catalog[key]['file']).relative_to(PROJECT).as_posix(),sha256=sha(RAW/catalog[key]['file']),seconds=len(audio)/rate,rms=float(np.sqrt(np.mean(audio**2))),reason=reason))
    source=catalog['scared'];audio,rate=sf.read(RAW/source['file'],always_2d=True)
    audio=audio.mean(axis=1)[int(rate):int(rate*9)]
    common=gcd(rate,48000);audio=resample_poly(audio,48000//common,rate//common)
    audio=resample_poly(audio,25,22) # Recorded performance slowed to 0.88x, lowering pitch naturally.
    audio=sosfilt(butter(2,[65,5000],btype='bandpass',fs=48000,output='sos'),audio)
    fade=7200;weight=np.linspace(0,1,fade)
    audio[:fade]=audio[-fade:]*(1-weight)+audio[:fade]*weight;audio=audio[:-fade]
    audio*=.58/max(1e-8,float(np.max(np.abs(audio))))
    target=PROJECT/'Assets/Resources/Audio/External/enemy-cyclopse-breath-0.wav'
    sf.write(target,audio,48000,subtype='PCM_16')
    entry={key:source[key] for key in ('pack','creator','license','page','download','source_file','source_file_sha256','archive_sha256')}
    recording=dict(source)
    entry.update(resource='Audio/External/'+target.stem,file=target.name,seconds=len(audio)/48000,peak=float(np.max(np.abs(audio))),rms=float(np.sqrt(np.mean(audio**2))),sha256=sha(target),
        modifications=dict(sample_rate=48000,mono=True,format='PCM16 WAV',loop=True,recordings=[recording],source_kind='Recorded human vocal breathing; no synthetic layers',
            scope='Original source 1-9 seconds; mono, 0.88x playback-rate edit, 65Hz-5kHz bandpass, 150ms loop wrap crossfade and peak .58. Spatial actor gain .44 patrol/.66 pursuit; pursuit pitch 1.12.'))
    manifest=PROJECT/'ThirdParty/Audio/manifest.json';data=json.loads(manifest.read_text(encoding='utf-8-sig'))
    data=[item for item in data if item['resource']!=entry['resource']]+[entry]
    manifest.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    recipe=dict(selected='scared',candidates= comparisons,source=source,resource=entry['resource'],outputSha256=entry['sha256'],edits=entry['modifications']['scope'],
                scope='Source/performance comparison and measured edits; does not certify the final device mix or subjective listening quality.')
    (PROJECT/'SourceArt/Audio/cyclopse-breath-selection.json').write_text(json.dumps(recipe,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print(json.dumps(dict(resource=entry['resource'],seconds=entry['seconds'],sha256=entry['sha256'])))

if __name__=='__main__':main()
