"""Prepare documented recordings; no oscillator/noise synthesis.

Python 3 + numpy, scipy, soundfile. Run with --candidates DOWNLOAD_DIRECTORY
once to acquire selected takes, then rerun without it from retained sources.
All source audio is local; this script does not download or authenticate.
"""
import argparse
import hashlib
import io
import json
import pathlib
import shutil
import uuid
import zipfile

import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfiltfilt

PROJECT = pathlib.Path(__file__).resolve().parents[2]
ROOT = PROJECT / 'ThirdParty/Audio/RecordedHorror'
RAW = ROOT / 'sources'
DEST = PROJECT / 'Assets/Resources/Audio/External'
RATE = 48000

def sha(data):
    return hashlib.sha256(data).hexdigest()

def acquire(folder):
    RAW.mkdir(parents=True, exist_ok=True)
    catalog = json.loads((PROJECT / 'ThirdParty/Audio/manifest.json').read_text(encoding='utf8'))
    sources = {}
    def keep(key, data, pack, author, page, download, source_file, license='CC0-1.0', archive=None):
        extension = pathlib.Path(download).suffix if 'public high quality' in source_file else pathlib.Path(source_file).suffix
        filename = key + extension
        (RAW / filename).write_bytes(data)
        sources[key] = dict(file=filename, pack=pack, creator=author, license=license,
            page=page, download=download, source_file=source_file,
            source_file_sha256=sha(data), archive_sha256=archive)
    def zipped(archive, key, filename, pack, author, page, url):
        with zipfile.ZipFile(folder / archive) as z:
            keep(key, z.read(filename), pack, author, page, url, filename,
                 archive=sha((folder / archive).read_bytes()))
    owlish = ('owlish-recorded-foley','OwlishMedia',
        'https://opengameart.org/content/sound-effects-pack',
        'https://opengameart.org/sites/default/files/Owlish%20Media%20Sound%20Effects.zip')
    for key, file in [('scream','Human/SCREAM.wav'),('panic','Human/freakedbreath.wav'),
        ('breath','Human/breath-female2.wav'),('scared','Human/scared-breathing.wav')]:
        zipped('owlish-recorded.zip',key,file,*owlish)
    home = ('owlish-household','OwlishMedia','https://opengameart.org/content/202-more-sound-effects',
        'https://opengameart.org/sites/default/files/MoreSounds.zip')
    for key, file in [('latch-open','Keys, Locks, Door/Key_Lock_Door_30.wav'),
                      ('latch-close','Keys, Locks, Door/Key_Lock_Door_39.wav')]:
        zipped('owlish-household.zip',key,file,*home)
    workshop = ('recorded-workshop','bart','https://opengameart.org/content/68-workshop-sounds',
        'https://opengameart.org/sites/default/files/workshop.7z')
    for key, name in [('metal-drop','metal drop 3'),('wood-clap','wood clap'),
        ('metal-drag','dragging metal'),('metal-jingle','jingle3'),('ratchet','ratchet1')]:
        file = 'workshop - ' + name + '.wav'
        keep(key,(folder/'workshop/workshop'/file).read_bytes(),*workshop,
             'workshop/'+file,archive=sha((folder/'workshop.7z').read_bytes()))
    fireworks = ('recorded-fireworks','rubberduck',
        'https://opengameart.org/content/25-cc0-bang-firework-sfx',
        'https://opengameart.org/sites/default/files/25-CC0-bang-sfx.zip')
    for i in range(1,4):
        zipped('fireworks.zip','firecracker'+str(i),f'shot_{i:02}.ogg',*fireworks)
    zipped('bells.zip','bell','bell_01.ogg','recorded-household-rubberduck','rubberduck',
        'https://opengameart.org/content/100-cc0-sfx',
        'https://opengameart.org/sites/default/files/100-CC0-SFX_0.zip')
    zipped('scrapes.zip','scrape','scrape-3.wav','recorded-scrapes','AntumDeluge',
        'https://opengameart.org/content/scrapes','https://opengameart.org/sites/default/files/scrapes.zip')
    keep('match',(folder/'ignition.flac').read_bytes(),'recorded-match','qubodup',
        'https://opengameart.org/content/flare-ignition',
        'https://opengameart.org/sites/default/files/ignition.flac','ignition.flac')
    keep('heart',(folder/'heartbeat-hq.mp3').read_bytes(),'real-human-heart','Benboncan',
        'https://freesound.org/people/Benboncan/sounds/62912/',
        'https://cdn.freesound.org/previews/62/62912_634166-hq.mp3',
        'Heartbeat Mono.wav (public high quality MP3 preview)','CC-BY-4.0')
    # Preserve the actual encoding extension for preview-derived recordings.
    sources['heart']['file']='heart.mp3'
    (RAW/'heart.mp3').write_bytes((folder/'heartbeat-hq.mp3').read_bytes())
    extra=json.loads((folder/'additional-downloads.json').read_text())
    for key,author,title in [('music-note','sandocho','musicbox1.wav'),('baby-cry','jamesmbock','babycry.mp3')]:
        info=extra[key]
        keep(key,(folder/(key+'.mp3')).read_bytes(),'recorded-'+key,author,
            info['page'],info['download'],title+' (public high quality MP3 preview)')
        sources[key]['file']=key+'.mp3'
        (RAW/(key+'.mp3')).write_bytes((folder/(key+'.mp3')).read_bytes())
    entry=next(e for e in catalog if e['file']=='ambience-upper-0.wav')
    keep('room-air',(DEST/entry['file']).read_bytes(),entry['pack'],entry['creator'],
        entry['page'],entry['download'],'Prepared ambience-upper-0.wav; derived from '+entry['source_file'])
    (ROOT/'sources.json').write_text(json.dumps(sources,indent=2,ensure_ascii=False)+'\n',encoding='utf8')

def read(sources,key,start=0,end=None,speed=1,hp=35,lp=9000,trim=False):
    info=sources[key]; data=(RAW/info['file']).read_bytes()
    assert sha(data)==info['source_file_sha256'],key+' original changed'
    x,rate=sf.read(io.BytesIO(data),always_2d=True);x=x.mean(axis=1)
    x=x[int(start*rate):int(end*rate) if end else None]
    x=resample_poly(x,RATE,round(rate*speed));x-=np.mean(x)
    if hp:x=sosfiltfilt(butter(2,hp,fs=RATE,btype='highpass',output='sos'),x)
    if lp:x=sosfiltfilt(butter(2,lp,fs=RATE,output='sos'),x)
    if trim:
        active=np.flatnonzero(np.abs(x)>max(np.max(np.abs(x))*.018,.0001))
        if len(active):x=x[max(0,active[0]-240):min(len(x),active[-1]+2400)]
    return x

def fade(x,a=.008,b=.09):
    x=x.copy();n=min(int(a*RATE),len(x)//2);m=min(int(b*RATE),len(x)//2)
    if n:x[:n]*=np.linspace(0,1,n)**2
    if m:x[-m:]*=np.linspace(1,0,m)**2
    return x

def level(x,peak):
    return x*(peak/max(np.max(np.abs(x)),1e-10))

def mix(seconds,layers):
    y=np.zeros(round(seconds*RATE))
    for x,t,gain in layers:
        offset=round(t*RATE);n=min(len(x),len(y)-offset)
        y[offset:offset+n]+=x[:n]*gain
    return y

def render():
    sources=json.loads((ROOT/'sources.json').read_text(encoding='utf8'))
    manifest=json.loads((PROJECT/'ThirdParty/Audio/manifest.json').read_text(encoding='utf8'))
    updated=[]
    def emit(cue,x,keys,edits,peak=.48,loop=False):
        x=level(fade(x,.006,.08 if not loop else .03),peak)
        path=DEST/(cue+'.wav');sf.write(path,x,RATE,subtype='PCM_16')
        meta=path.with_suffix('.wav.meta')
        if not meta.exists():
            template=(DEST/'door-open-0.wav.meta').read_text()
            import re
            text = re.sub(r'guid: \w+', 'guid: '+uuid.uuid4().hex, template)
            meta.write_text('\n'.join(line.rstrip() for line in text.splitlines()) + '\n')
        d,_=sf.read(path); first=sources[keys[0]]
        record={k:v for k,v in first.items() if k!='file'}
        record.update(resource='Audio/External/'+cue,file=path.name,
            modifications=dict(sample_rate=RATE,mono=True,format='PCM16 WAV',
                scope=edits,loop=loop,recordings=[sources[k] for k in keys],
                source_kind='recorded; edited physical foley or human performance; no oscillator/noise layers'),
            seconds=len(d)/RATE,peak=float(np.max(np.abs(d))),rms=float(np.sqrt(np.mean(d*d))),
            sha256=sha(path.read_bytes()))
        updated.append(record)
    get=lambda key,**kw:read(sources,key,**kw)
    impact=level(get('metal-drop',trim=True,speed=.88,lp=7000),.7)
    vocal=level(get('scream',trim=True,speed=.84,hp=140,lp=6500),.5)
    emit('recognition-0',mix(2.45,[(impact,0,.78),(vocal,.09,.64)]),
        ['metal-drop','scream'],'Recorded dropped metal plus performed scream; mono, filtered, slowed, layered at 90 ms; short tail.',.575)
    # One real stethoscope cycle, tightened to 83 BPM. Pitch follows current stress.
    heart=get('heart',start=2.23,end=3.2136,hp=25,lp=600)
    heart=resample_poly(heart,7200,9836)
    emit('tension-heart-0',heart,['heart'],'Actual 61 BPM stethoscope recording; one cycle trimmed 2.23–3.2136 s and rate-converted to approximately 83 BPM.',.28,True)
    emit('tension-air-0',get('room-air',start=0,end=5,hp=25,lp=350),['room-air'],
        'Recorded refrigeration room tone; low-pass 350 Hz; 5 second restrained tension bed.',.26,True)
    emit('tension-breath-0',get('scared',start=2,end=8,hp=100,lp=2600),['scared'],
        'Recorded frightened breathing; 6 second excerpt, darkened, quiet loop.',.32,True)
    emit('player-breath-0',get('breath',hp=100,lp=5200),['breath'],
        'Recorded solo breathing, mono, high-pass handling noise, edge fades.',.28,True)
    for i in range(3):
        emit('firecracker-'+str(i),get('firecracker'+str(i+1),hp=65,lp=14000,trim=True),
            ['firecracker'+str(i+1)],'Short recorded firework report; independent take; mono, trimmed and gain-limited.',.67)
    emit('candle-ignite-0',get('match',start=.28,end=.98,hp=100,lp=10000,speed=.92),
        ['match'],'Recorded match ignition; ignition onset excerpt and slightly slowed; no synthesized fire.',.42)
    for name in ['latch-open','latch-close']:
        emit('cabinet-'+name.split('-')[-1]+'-0',get(name,trim=True,hp=130,lp=8500),
            [name],'Actual key/lock/door contact recording, cropped to short mechanical contact; distinct opening and closing takes.',.60)
    scrape=get('scrape',trim=True,hp=120,lp=6000)
    emit('door-rail-0',scrape,['scrape'],'Object scraped on cinder block as sliding-rail foley; trimmed, filtered, quiet movement-only playback.',.32,True)
    emit('chair-scrape-0',get('scrape',speed=.88,hp=100,lp=4500,trim=True),['scrape'],
        'Slowed recorded scrape as chair-drag foley, retaining abrasive physical texture.',.43)
    emit('mannequin-joint-0',get('metal-drag',start=2.1,end=3.0,hp=100,lp=5000),
        ['metal-drag'],'Recorded metal dragged across workshop surface; short joint creak foley.',.42)
    emit('lantern-warning-0',get('metal-jingle',trim=True,hp=140,lp=6500),
        ['metal-jingle'],'Recorded tool jingle; metal warning rattle.',.40)
    emit('cyclopse-roar-0',get('panic',trim=True,speed=.68,hp=55,lp=5000),['panic'],
        'Performed frightened vocal breath, slowed for creature weight; no synthesized sawtooth roar.',.48)
    emit('nursery-whimper-0',get('baby-cry',start=3,end=5.7,hp=130,lp=6000),['baby-cry'],
        'Recorded infant vocal excerpt; mono and quiet gain; original public-preview encoding retained.',.38)
    emit('wraith-growth-0',get('scared',start=8,end=11.8,speed=.8,hp=60,lp=3200),['scared'],
        'Recorded frightened breath, slowed and darkened for apparition emergence.',.40)
    emit('lantern-rise-0',get('metal-drag',start=7,end=8.2,hp=100,lp=4000),['metal-drag'],
        'Different recorded metallic scrape excerpt for transformation.',.40)
    emit('hwacat-jaw-0',get('ratchet',trim=True,hp=140,lp=5500),['ratchet'],
        'Recorded workshop ratchet contact as mechanical jaw foley.',.42)
    bell=get('bell',trim=True,hp=90,lp=10000)
    emit('story-bell-0',bell,['bell'],'Recorded physical bell, mono, trimmed tail and calibrated gain.',.45)
    emit('memory-bell-0',mix(4,[(bell,0,.45),(bell,.3,.25)]),['bell'],
        'Two soft recorded bell strikes followed by silence for spatial clue loop.',.40,True)
    note=get('music-note',trim=True,hp=120,lp=7500)
    emit('doll-musicbox-0',mix(3.2,[(note,0,.45),(resample_poly(note,100,112),.52,.40),
        (resample_poly(note,100,106),1.04,.4),(note,1.60,.28)]),['music-note'],
        'Antique wind-up toy note recording, four-note phrase assembled by sample rate changes; retained physical mechanism texture.',.38)
    replaced={r['file'] for r in updated}
    manifest=[r for r in manifest if r['file'] not in replaced]+updated
    (PROJECT/'ThirdParty/Audio/manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
    (ROOT/'prepared-cues.json').write_text(json.dumps(updated,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
    print(json.dumps({'prepared':len(updated),'seconds':sum(r['seconds'] for r in updated),
        'max_peak':max(r['peak'] for r in updated),'manifest_entries':len(manifest)}))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--candidates',type=pathlib.Path)
    args=parser.parse_args()
    if args.candidates:acquire(args.candidates)
    render()
