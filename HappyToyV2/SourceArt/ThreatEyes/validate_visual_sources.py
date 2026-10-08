"""Validate original-eye emission resources/anatomy and source handoff; no Unity."""
from pathlib import Path
import hashlib
import json
import math
from PIL import Image

PROJECT=Path(__file__).resolve().parents[2]
EXPECTED={'Cyclopse':1,'Uncat':2,'Hwacat_angry':2,'Baby':2,'LanternMask':2,'Mannequin':2}


def main():
    records=[]
    for key,count in EXPECTED.items():
        directory=PROJECT/'Assets/Resources/ThreatEyes'
        profile_path=directory/(key+'-profile.json');mask_path=directory/(key+'-emission.png')
        profile=json.loads(profile_path.read_text(encoding='utf-8-sig'))
        assert profile['version']==2 and profile['key']==key and len(profile['eyes'])==count
        assert profile['materialSlot']==0 and profile['maskResource']=='ThreatEyes/'+key+'-emission'
        image=Image.open(mask_path);assert image.size==(2048,2048) and image.mode=='RGB'
        assert 0<image.getextrema()[0][1]<240,'No solid-white/neon annular plateau: '+key
        assert len(image.getcolors(2048*2048))>=16,'Emission must retain graded scalar falloff: '+key
        pupil_checks=[]
        for eye in profile['eyes']:
            assert eye['aperture'] is (not eye['centreHasSurface'])
            assert eye['emissionFalloff']=='smooth radial ridge with low-frequency arc modulation'
            assert eye['materialSlot']==0 and 0<eye['sourceWorldRadius']<eye['sourceWorldHeight']*.2
            assert len(eye['uvBoundarySamples'])>=8
            for uv in [eye['uv']]+eye['uvBoundarySamples']:
                assert 0<=uv['x']<=1 and 0<=uv['y']<=1
            if eye['centreHasSurface']:
                # Unity's bilinear centre footprint must contain no emission,
                # not merely the nearest PNG pixel. PNG rows are top-down.
                x=eye['uv']['x']*2048-.5;y=eye['uv']['y']*2048-.5
                samples=[image.getpixel((i,2047-j)) for i in (math.floor(x),math.ceil(x))
                    for j in (math.floor(y),math.ceil(y))]
                assert all(pixel==(0,0,0) for pixel in samples),(key,eye['name'],samples)
                pupil_checks.append(dict(name=eye['name'],centreBilinearEmission='exact zero'))
            else:
                assert key=='LanternMask' and eye['shape']=='apertureLip'
                assert eye['preservedFeature']=='open empty eye aperture'
                pupil_checks.append(dict(name=eye['name'],centre='empty original aperture; no new surface'))
        for path in (profile_path,mask_path): assert Path(str(path)+'.meta').is_file(),str(path)
        records.append(dict(key=key,eyeCount=count,pupilChecks=pupil_checks))
    manifest=json.loads((Path(__file__).parent/'emission-mask-manifest.json').read_text())
    for entry in manifest['assets']:
        assert entry['addsGeometry'] is False and entry['preservesOriginalBaseMap'] is True
        assert entry['materialSlots']==[0]
    sources=[]
    for folder in ('Assets/Resources/ThreatEyes','SourceArt/ThreatEyes'):
        for path in sorted((PROJECT/folder).rglob('*')):
            if not path.is_file() or '__pycache__' in path.parts or path.name=='source-inventory.json': continue
            sources.append(dict(path=path.relative_to(PROJECT).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    result=dict(status='PASS',version=2,anatomy=records,sources=sources,
        scope='PNG/profile/source validation only. Native anatomy, bloom, original texture readability and release materials require separate Unity review.')
    (Path(__file__).parent/'source-inventory.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(dict(status='PASS',profiles=len(records),sourceCount=len(sources),originalEyeCount=sum(EXPECTED.values())),indent=2))


if __name__=='__main__': main()
