"""Validate observed clock intervals in actual saved native audit JSON.

Run with native-corridor-play.json or native-school-play.json paths after root's
natural Windows runs. This reads evidence; it never creates substitute reports.
Failed gameplay reports are valid timing evidence and remain failed gameplay.
"""
import sys,json,math
from pathlib import Path

def close(actual,expected,field):
    assert math.isfinite(float(actual)),field+' is nonfinite'
    assert abs(actual-expected)<=max(.0001,abs(expected)*.00001),(field,actual,expected)

def validate(path):
    data=json.loads(Path(path).read_text(encoding='utf-8-sig'))
    t=data['timing'];frequency=t['stopwatchFrequency'];assert frequency>0
    start=t.get('routeBegin') if t['routeStarted'] else None
    end=t['routeEnd'];settle=t.get('settlingEnd') if t['audioSettlingRequested'] else None;capture=t['captureEnd']
    assert end and capture,'Failure/success snapshots must exist before writer'
    def interval(a,b):
        if not a or not b:return (0,0,0)
        assert b['stopwatchTicks']>=a['stopwatchTicks'],(a['phase'],b['phase'])
        return ((b['stopwatchTicks']-a['stopwatchTicks'])/frequency,
                max(0,b['gameplaySeconds']-a['gameplaySeconds']),
                max(0,b['dspSeconds']-a['dspSeconds']))
    route=interval(start,end)
    for key,value in zip(['routeWallSeconds','routeGameSeconds','routeDspSeconds'],route):close(t[key],value,key)
    settling=interval(end,settle) if t['audioSettlingRequested'] else (0,0,0)
    for key,value in zip(['settlingWallSeconds','settlingGameSeconds','settlingDspSeconds'],settling):close(t[key],value,key)
    cleanup=interval(settle or end,capture)
    for key,value in zip(['cleanupWallSeconds','cleanupGameSeconds','cleanupDspSeconds'],cleanup):close(t[key],value,key)
    assert t['writerStartedTicks']>=capture['stopwatchTicks'],'Writer began before final capture boundary'
    assert t['writerEndedTicks']>=t['writerStartedTicks']
    close(t['writerWallSeconds'],(t['writerEndedTicks']-t['writerStartedTicks'])/frequency,'writerWallSeconds')
    close(data['wallSeconds'],route[0],'top-level wallSeconds')
    close(data['gameSeconds'],route[1],'top-level gameSeconds')
    close(data.get('dspSeconds',data.get('routeDspSeconds')),route[2],'top-level DSP seconds')
    close(data['dspToGameRatio'],route[2]/route[1] if route[1]>0 else 0,'dspToGameRatio')
    close(data['dspToWallRatio'],route[2]/route[0] if route[0]>0 else 0,'dspToWallRatio')
    if data['status']=='PASS':
        assert t['routeCompleted'] and t['audioSettlingRequested'] and t['audioSettlingCompleted']
        assert settle['phase']=='natural-audio-settling-complete'
        assert settling[0]>=.18,'A completed .2-second real settling interval is missing'
        assert not data['errors'] and not data['failure']
        audio=data['audio'];assert not audio['truncated'] and not audio['writerError']
        assert audio['callbacks']>0 and audio['capturedSamples']>0
    else:
        assert data['failure'],'Failed timing evidence cannot hide its failure reason'
    print(json.dumps({'file':str(path),'native_status':data['status'],'timing_evidence':'PASS',
        'route_wall':route[0],'settling_wall':settling[0],'cleanup_wall':cleanup[0],'writer_wall':t['writerWallSeconds']}))

if len(sys.argv)<2:raise SystemExit('Pass actual native audit JSON paths; no synthetic fallback is generated.')
for filename in sys.argv[1:]:validate(filename)
