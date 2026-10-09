"""Prepared realistic corridor Foley; CPython 3.11 + requirements-corridor.txt.

Recorded shoes on an old wooden parquet, plus physical metal and performed voice
for recognition. No oscillator, noise generator or procedural tone is used.
Optional --candidates DIRECTORY installs the two publicly licensed preview files;
normal rebuilds require only the retained source recordings in the repository.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import uuid
import re
import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfiltfilt
from render_recorded_horror import read, fade, level, mix

PROJECT=pathlib.Path(__file__).resolve().parents[2]
ROOT=PROJECT/'ThirdParty/Audio/CorridorFoley'
RAW=ROOT/'sources'
DEST=PROJECT/'Assets/Resources/Audio/External'
RATE=48000
SOURCES={
 'parquet':dict(file='parquet.mp3',creator='evghenifloca',license='CC0-1.0',
     page='https://freesound.org/people/evghenifloca/sounds/817482/',
     download='https://cdn.freesound.org/previews/817/817482_14899252-hq.mp3',
     source_file='FEETHmn_Squeaky Wooden Parquet Footsteps.wav (public high quality MP3 preview)',
     source_file_sha256='e466f4d5c669f85267caa012a861ea3998c9ae852cd36f935cc6d4458963c923'),
 'creaking':dict(file='creaking.mp3',creator='vrodge',license='CC0-1.0',
     page='https://freesound.org/people/vrodge/sounds/119522/',
     download='https://cdn.freesound.org/previews/119/119522_1492767-hq.mp3',
     source_file='Footsteps_wooden floor_creaking.aif (public high quality MP3 preview)',
     source_file_sha256='dd7c4709e9806fc5f5a5548db6b7f04d15eb4abd46ddedeed1f95cd29e8a6659'),
 'abandoned-room':dict(file='abandoned-room.mp3',creator='vhio',license='CC0-1.0',
     page='https://freesound.org/people/vhio/sounds/791287/',
     download='https://cdn.freesound.org/previews/791/791287_15774433-hq.mp3',
     source_file='WINDInt Closed room in an abandoned defense base (public high quality MP3 preview)',
     source_file_sha256='45ff5a6890779f55699f8228e5f1d1d73bbecdefdb602df4ad3788d1b2a6de73')}

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def render(candidates=None):
    RAW.mkdir(parents=True,exist_ok=True)
    if candidates:
        for item in SOURCES.values():
            src=candidates/item['file'];assert sha(src)==item['source_file_sha256']
            shutil.copyfile(src,RAW/item['file'])
    for item in SOURCES.values(): assert sha(RAW/item['file'])==item['source_file_sha256']
    (ROOT/'sources.json').write_text(json.dumps(SOURCES,indent=2)+'\n',encoding='utf8')
    manifest_path=PROJECT/'ThirdParty/Audio/manifest.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf8'));updated=[]
    def emit(cue,x,origins,edits,peak,loop=False):
        x=level(x if loop else fade(x,.006,.09),peak);path=DEST/(cue+'.wav')
        sf.write(path,x,RATE,subtype='PCM_16');meta=path.with_suffix('.wav.meta')
        if not meta.exists():
            template=(DEST/'door-open-0.wav.meta').read_text(encoding='utf8')
            template=re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template)
            meta.write_text('\n'.join(line.rstrip() for line in template.splitlines())+'\n',encoding='utf8')
        record={k:v for k,v in origins[0].items() if k!='file'}
        y,_=sf.read(path)
        record.update(resource='Audio/External/'+cue,file=path.name,pack='corridor-recorded-foley',
            modifications=dict(sample_rate=RATE,mono=True,format='PCM16 WAV',scope=edits,recordings=origins,loop=loop,
                source_kind='Physical contact and human-performance recordings; no synthesized layers'),
            seconds=len(y)/RATE,peak=float(np.max(np.abs(y))),rms=float(np.sqrt(np.mean(y*y))),sha256=sha(path))
        updated.append(record)
    x,r=sf.read(RAW/'parquet.mp3',always_2d=True);x=x.mean(axis=1)
    x=resample_poly(x,RATE,r)
    x=sosfiltfilt(butter(2,65,fs=RATE,btype='highpass',output='sos'),x)
    x=sosfiltfilt(butter(2,10000,fs=RATE,output='sos'),x)
    # Five independent complete shoe contacts with their board creak, not five
    # transposed copies of one sound. 0.67 s captures heel/toe and the settling wood.
    for i,t in enumerate([2.12,3.68,4.48,5.39,10.10]):
        start=t-.11;end=t+.56
        segment=x[round(start*RATE):round(end*RATE)];segment-=segment.mean()
        emit('step-wood-'+str(i),segment,[SOURCES['parquet']],
             f'Distinct old-parquet shoe contact {start:.2f}–{end:.2f} s; mono, 65 Hz high-pass/10 kHz low-pass, 6 ms/90 ms fades; preserved board creak.',.46)
    old=json.loads((PROJECT/'ThirdParty/Audio/RecordedHorror/sources.json').read_text(encoding='utf8'))
    # The ring and abrasive sustained texture come from a real workshop contact.
    # A frightened vocal breath remains a quiet texture rather than a loud scream.
    impact=level(read(old,'metal-drop',trim=True,speed=.82,hp=70,lp=6800),.6)
    friction=level(read(old,'metal-drag',start=2.1,end=3.25,speed=.73,hp=120,lp=5700),.35)
    voice=level(read(old,'panic',trim=True,speed=.76,hp=130,lp=4100),.22)
    cue=mix(1.75,[(impact,0,.75),(friction,.028,.63),(voice,.16,.46)])
    emit('recognition-0',cue,[old['metal-drop'],old['metal-drag'],old['panic']],
         'Recorded metal impact, dragged-metal friction at 28 ms, performed frightened breath at 160 ms; distinct rate reductions and filtering; 1.75 s fading tail; no synthetic tone or noise.',.52)
    for kind,keys,layers in [
        ('cyclopse',['wood-clap','panic'],[('wood-clap',.88,0,.76),('panic',.68,.07,.24)]),
        ('hwacat',['ratchet','wood-clap'],[('ratchet',1.05,0,.62),('wood-clap',1.1,.035,.52)]),
        ('uncat',['wood-clap','scared'],[('wood-clap',1.05,0,.57),('scared',.92,.025,.28)]),
        ('baby',['wood-clap','baby-cry'],[('wood-clap',1.16,0,.50),('baby-cry',1.0,.015,.26)]),
    ]:
        contacts=[]
        for key,speed,offset,gain in layers:
            options=dict(speed=speed,hp=90,lp=7200,trim=True)
            if key=='baby-cry':options.update(start=3,end=4,trim=False)
            if key=='scared':options.update(start=8,end=9.2,trim=False)
            contacts.append((level(read(old,key,**options),.65),offset,gain))
        emit('enemy-'+kind+'-attack-0',mix(.46,contacts),[old[k] for k in keys],
            'Short actual wood/tool contact plus recorded vocal texture; .46 s action-synchronized contact, independent source-rate edits, filtered and faded; no glass or synthetic tone.',.42)
    room,r=sf.read(RAW/'abandoned-room.mp3',always_2d=True);room=room.mean(axis=1)
    for i,start in enumerate([395,230,95]):
        segment=resample_poly(room[start*r:(start+24)*r],RATE,r)
        segment-=segment.mean()
        segment=sosfiltfilt(butter(2,45,fs=RATE,btype='highpass',output='sos'),segment)
        segment=sosfiltfilt(butter(2,1900,fs=RATE,output='sos'),segment)
        overlap=round(1.5*RATE);t=np.linspace(0,1,overlap)
        tail=segment[-overlap:].copy();segment=segment[:-overlap].copy()
        segment[:overlap]=tail*(1-t)+segment[:overlap]*t
        emit('ambience-corridor-'+str(i),segment,[SOURCES['abandoned-room']],
            f'Actual sheltered room/wind field recording {start}–{start+24} s; distant road remains in source. Mono, 45 Hz high-pass/1.9 kHz low-pass, 1.5 s wrap crossfade to 22.5 s spatial loop; no added drone.',.20,True)
    names={r['file'] for r in updated};manifest=[r for r in manifest if r['file'] not in names]+updated
    manifest_path.write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
    (ROOT/'prepared-cues.json').write_text(json.dumps(updated,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
    print(json.dumps({'prepared':len(updated),'max_peak':max(r['peak'] for r in updated),'sources_verified':True}))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--candidates',type=pathlib.Path)
    render(parser.parse_args().candidates)
