"""Read-only checks of native audit data, retained PCM and post-build inputs.

This does not certify screenshot quality, device listening or GPU/presented FPS.
"""
from pathlib import Path
import argparse, hashlib, json, math, sys, wave

def sha256(path):
    digest=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):digest.update(block)
    return digest.hexdigest()

def verify_wav(path,entry,rate,channels):
    errors=[]
    if sha256(path).lower()!=entry['sha256'].lower():errors.append('PCM file SHA256 differs: '+str(path))
    with wave.open(str(path),'rb') as stream:
        if stream.getcomptype()!='NONE' or stream.getsampwidth()!=2:errors.append('WAV is not PCM16: '+str(path))
        if stream.getnchannels()!=channels or stream.getframerate()!=rate:errors.append('WAV rate/channel header differs: '+str(path))
        samples=stream.getnframes()*stream.getnchannels()
        if samples!=entry['samples'] or entry['channels']!=channels:errors.append('WAV sample/channel count differs: '+str(path))
        actual_bytes=0
        while True:
            block=stream.readframes(262144)
            if not block:break
            actual_bytes+=len(block)
        if actual_bytes!=samples*2:errors.append('WAV payload is incomplete: '+str(path))
        seconds=samples/(rate*channels)
        if not math.isclose(seconds,entry['seconds'],abs_tol=1e-5,rel_tol=1e-6):errors.append('WAV duration metadata differs: '+str(path))
    return errors

def verify(manifest_path,project,report_path):
    report_path=Path(report_path).resolve();project=Path(project).resolve()
    report=json.loads(report_path.read_text(encoding='utf-8-sig'))
    errors=[]
    def check(condition,message):
        if not condition:errors.append(message)
    check(report.get('status')=='PASS' and not report.get('failure'),'Native route did not report PASS')
    check(report.get('records')==5 and report.get('escaped') is True,'Five-memory escape not proven')
    check(not report.get('errors'),'Native runtime errors are present')
    check(report.get('inputDevicesRestored') is True,'Virtual input devices were not restored')
    check(bool(report.get('device')) and report.get('graphics') not in ('Null','',None),'Actual graphics API/device missing')
    check(report.get('width',0)>0 and report.get('height',0)>0 and bool(report.get('cpu')),'Native CPU/resolution metadata missing')
    check('frameMeasurement' in report,'Frame-measurement scope missing')
    school=report.get('chapter') is True
    if school:
        check(all(report.get(key) is True for key in ('portraitWitnessed','portraitCompleted','nurseryReleased')),'Mandatory school actor milestones incomplete')
        check(report.get('stairLegs',0)>=4 and report.get('minY',0)<-4.8 and report.get('maxY',0)>4.8 and report.get('physicalMeters',0)>100,'School stairs/floors/full walking route unproven')
        check('targetFrameRate' in report and 'vSyncCount' in report and 'lightingScope' in report,'School default frame/light scope missing')
        pause_fields=('listenerPaused','positionFrozen','staminaFrozen','gameClockFrozen')
    else:
        check(report.get('seed') in (73,211),'Requested corridor seed 73/211 not recorded')
        check(report.get('routePassed') is True,'Natural input route did not pass')
        check(report.get('batteriesCollected',0)>=1 and report.get('candlesIgnited',0)>=1,'Actual finite-light input proof incomplete')
        check(report.get('auditTargetFrameRate')==60 and 'originalTargetFrameRate' in report and 'vSyncCount' in report,'Corridor frame-rate override metadata missing')
        pause_fields=('listenerPaused','gameFrozen','playerFrozen','staminaFrozen','flashlightFrozen')
    pause=report.get('pause') or {}
    check(all(pause.get(field) is True for field in pause_fields),'Natural Escape pause did not freeze live state')
    audio=report.get('audio') or {}
    check(audio.get('progressMode')==('school-memories' if school else 'corridor-memories'),'Listener progress mode differs from the actual route')
    rate=audio.get('sampleRate',0);channels=audio.get('channels',0)
    check(rate>0 and channels==2 and audio.get('callbacks',0)>10,'Natural stereo listener callbacks not proven')
    check(audio.get('nonSilent') is True and not audio.get('truncated') and audio.get('nonfiniteSamples')==0 and audio.get('overflowSamples')==0 and not audio.get('writerError'),'PCM capture is silent, truncated, nonfinite or lost')
    check(audio.get('observedSamples',0)==audio.get('capturedSamples',-1)>0,'Observed/captured PCM scalar-sample counts differ')
    stages=audio.get('stages') or [];floors=audio.get('floors') or []
    check(len(stages)==6 and all(stage.get('samples',0)>0 and stage.get('clipped')==0 for stage in stages),'Six actual memory-stage PCM segments missing or clipped')
    check(sum(part.get('samples',0) for part in stages)==audio.get('observedSamples'),'Stage counts do not cover observed PCM')
    check(len(floors)==3 and sum(part.get('samples',0) for part in floors)==audio.get('observedSamples'),'Floor PCM partition incomplete')
    if school:check(all(floor.get('samples',0)>rate and floor.get('clipped')==0 and floor.get('rms',0)>2e-5 for floor in floors),'Three school floor mixes are not audible/unclipped')
    else:check(len(floors)>0 and floors[0].get('rms',0)>2e-5,'Corridor ground mix is silent')
    apause=audio.get('pause') or {}
    check(apause.get('steadySamples',0)==0 or apause.get('steadyPeak',1)<1e-5,'Steady pause leaked audible PCM')
    audio_dir=Path(report.get('audioDirectory','')).resolve()
    check(audio_dir.is_relative_to(report_path.parent),'Audio directory is outside this audit run')
    if audio_dir.is_relative_to(report_path.parent):
        audio_file=audio_dir/'audio.json'
        check(audio_file.is_file(),'Retained audio.json missing')
        if audio_file.is_file():check(json.loads(audio_file.read_text(encoding='utf-8-sig'))==audio,'Embedded and retained audio evidence differ')
        files=audio.get('files') or []
        check(bool(files) and sum(entry.get('samples',0) for entry in files)==audio.get('capturedSamples'),'Retained WAV file counts do not cover captured samples')
        for entry in files:
            name=entry.get('file','')
            check(name==Path(name).name and bool(name),'Unsafe WAV filename in report')
            if name==Path(name).name and name:
                try:errors.extend(verify_wav(audio_dir/name,entry,rate,channels))
                except Exception as error:errors.append('Cannot validate WAV '+name+': '+str(error))
    if rate>0 and channels>0:
        check(math.isclose(audio.get('seconds',-1),audio.get('capturedSamples',0)/(rate*channels),abs_tol=1e-5,rel_tol=1e-6),'Overall PCM duration/count mismatch')
    timing=report.get('timing') or {};frequency=timing.get('stopwatchFrequency',0)
    check(all(timing.get(key) is True for key in ('routeStarted','routeCompleted','audioSettlingRequested','audioSettlingCompleted')) and frequency>0,'Route/settling clock boundaries incomplete')
    settling_requested=timing.get('audioSettlingRequested') is True
    # JsonUtility writes an absent nested class as an all-zero object. A failed
    # route that never requested settling has no real settling boundary to compare.
    keys=('routeBegin','routeEnd','settlingEnd','captureEnd') if settling_requested else ('routeBegin','routeEnd','captureEnd')
    points=[timing.get(key) or {} for key in keys]
    for before,after in zip(points,points[1:]):
        check(all(after.get(key,-1)>=before.get(key,0) for key in ('stopwatchTicks','gameplaySeconds','dspSeconds','engineRealtimeSeconds')),'Observed clock boundary moved backwards')
    def clock_equal(prefix,before,after):
        if frequency<=0:return
        deltas={'WallSeconds':(after.get('stopwatchTicks',0)-before.get('stopwatchTicks',0))/frequency,
                'GameSeconds':after.get('gameplaySeconds',0)-before.get('gameplaySeconds',0),
                'DspSeconds':after.get('dspSeconds',0)-before.get('dspSeconds',0)}
        for suffix,value in deltas.items():
            check(math.isclose(timing.get(prefix+suffix,-1),value,abs_tol=.002,rel_tol=1e-5),'Reported '+prefix+suffix+' does not match raw boundary clocks')
    clock_equal('route',points[0],points[1])
    if settling_requested:
        clock_equal('settling',points[1],points[2]);clock_equal('cleanup',points[2],points[3])
        check(timing.get('settlingWallSeconds',0)>=.19,'Requested natural .2-second settling was shortened')
    else:
        clock_equal('cleanup',points[1],points[2])
        check(all(timing.get('settling'+suffix,0)==0 for suffix in ('WallSeconds','GameSeconds','DspSeconds')),'Unrequested settling has a nonzero interval')
    check(timing.get('writerStartedTicks',0)>=points[-1].get('stopwatchTicks',1) and timing.get('writerEndedTicks',0)>=timing.get('writerStartedTicks',1),'PCM writer boundary overlaps the active route')
    if frequency>0:check(math.isclose(timing.get('writerWallSeconds',-1),(timing.get('writerEndedTicks',0)-timing.get('writerStartedTicks',0))/frequency,abs_tol=.002,rel_tol=1e-5),'PCM writer wall interval mismatch')
    check(timing.get('routeGameSeconds',0)>0 and timing.get('routeDspSeconds',0)>0 and timing.get('routeWallSeconds',0)>0,'Active route clocks did not advance')
    images=report.get('images') or []
    check(bool(images) and all((report_path.parent/name).is_file() for name in images),'Native milestone images missing')
    if not school:check('native-corridor-first-candle.png' in images,'Native first-candle emission screenshot missing')
    manifest=json.loads(Path(manifest_path).read_text(encoding='utf-8-sig'))
    check(manifest.get('status')=='PASS' and bool(manifest.get('inputs')),'Successful build input manifest missing')
    check(manifest.get('unityVersion')==report.get('unity'),'Native player Unity version differs from its build manifest')
    check(manifest.get('scene')=='Assets/Annex/SchoolAnnex.unity','Build used a different authored scene')
    expected_paths={entry.get('path','') for entry in manifest.get('inputs',[])}
    actual_paths={path.relative_to(project).as_posix()
                  for directory in ('Assets','Packages','ProjectSettings')
                  for path in (project/directory).rglob('*') if path.is_file()}
    check(expected_paths==actual_paths,'Current build-input inventory changed: added '+str(sorted(actual_paths-expected_paths))+'; removed '+str(sorted(expected_paths-actual_paths)))
    anchor_count=0
    for entry in manifest.get('inputs',[]):
        relative=entry.get('path','');path=(project/relative).resolve()
        check(path.is_relative_to(project) and relative.startswith(('Assets/','Packages/','ProjectSettings/')),'Unexpected build input path')
        if not path.is_relative_to(project):continue
        try:check(sha256(path)==entry['sha256'],'Source changed since this build: '+relative)
        except Exception as error:errors.append('Cannot fingerprint '+relative+': '+str(error))
        if relative.startswith('Assets/Resources/FeedbackShaderVariants/') and relative.endswith('.mat'):anchor_count+=1
    check(anchor_count>=4,'Build fingerprint omits minimum actual runtime shader anchors')
    return {'status':'FAIL' if errors else 'PASS',
            'scope':'Native report/clock consistency, retained PCM headers/checksums/counts and current build-input fingerprint only; screenshot visual approval, device listening, resource balance and GPU/presented FPS remain separate.',
            'report':str(report_path),'school':school,'errors':errors,'shaderAnchors':anchor_count,
            'device':report.get('device'),'graphics':report.get('graphics'),'resolution':[report.get('width'),report.get('height')],
            'routeDspToGameRatio':report.get('dspToGameRatio'),'routeDspToWallRatio':report.get('dspToWallRatio'),
            'audioCallbackWallSeconds':audio.get('callbackWallSeconds'),'audioSeconds':audio.get('seconds'),
            'writerWallSeconds':timing.get('writerWallSeconds'),'frameMeasurement':report.get('frameMeasurement'),
            'lightingScope':report.get('lightingScope','Early battery/candle proof followed by a predominantly flashlight-off known-map route; no finite-light difficulty certification.')}

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',required=True,type=Path);parser.add_argument('--project',required=True,type=Path)
    parser.add_argument('--report',required=True,type=Path);parser.add_argument('--output',type=Path)
    args=parser.parse_args()
    try:result=verify(args.manifest,args.project,args.report)
    except Exception as error:result={'status':'FAIL','scope':'Evidence parser failure; no native pass claimed','errors':[str(error)]}
    rendered=json.dumps(result,ensure_ascii=False,indent=2)
    if args.output:args.output.write_text(rendered,encoding='utf-8')
    print(rendered)
    sys.exit(0 if result['status']=='PASS' else 1)
