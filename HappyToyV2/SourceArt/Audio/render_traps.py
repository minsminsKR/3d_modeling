"""Rebuild six distinct recorded-foley trap cues. Python 3.11, NumPy/SciPy.

No oscillator, generated noise, synthesized impact or reversed substitute is used.
All layers are actual CC0 trap/spring/metal recordings preserved in ThirdParty.
"""
from pathlib import Path
import io,json,math,wave,warnings,zipfile,hashlib
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter,sosfilt,resample_poly

ROOT=Path(__file__).resolve().parents[2]
CATALOG=ROOT/'ThirdParty/Audio/Traps'
RAW=CATALOG/'sources'
originals={item['file']:item for item in json.loads((CATALOG/'sources.json').read_text()).values()}
manifest_path=ROOT/'ThirdParty/Audio/manifest.json'
manifest=[item for item in json.loads(manifest_path.read_text(encoding='utf-8')) if not item['file'].startswith('trap-')]
OUT=ROOT/'Assets/Resources/Audio/External'
RATE=48000
archive=zipfile.ZipFile(RAW/'27-metal-recordings-original.zip')

def load(name):
    source=RAW/name
    if not source.exists():
        entry=next(p for p in archive.namelist() if Path(p).name==name)
        source=io.BytesIO(archive.read(entry))
    with warnings.catch_warnings():
        warnings.simplefilter('ignore',wavfile.WavFileWarning)
        rate,data=wavfile.read(source)
    if np.issubdtype(data.dtype,np.integer):data=data.astype(np.float64)/2**(np.iinfo(data.dtype).bits-1)
    else:data=data.astype(np.float64)
    if data.ndim>1:data=data.mean(axis=1)
    g=math.gcd(int(rate),RATE)
    if rate!=RATE:data=resample_poly(data,RATE//g,int(rate)//g)
    return sosfilt(butter(2,65,fs=RATE,btype='highpass',output='sos'),data)

cache={}
def layer(name,start,end,gain,at):
    if name not in cache:cache[name]=load(name)
    data=cache[name][round(start*RATE):round(end*RATE)].copy()
    # Tiny edit fades remove cut clicks while retaining recorded impact attacks.
    fade=min(round(.003*RATE),len(data)//4);tail=min(round(.07*RATE),len(data)//4)
    data[:fade]*=np.linspace(0,1,fade);data[-tail:]*=np.linspace(1,0,tail)
    return dict(source=name,start=start,end=end,gain=gain,at=at),data*gain

TRAP='trap-close-1234.wav';SPRING='large-spring-0206.wav'
METAL=lambda n:f'dull_metal_collision_{n:02}_44k_32bit_stereo.wav'
GRIT=lambda n:f'metal_grit_{n:02}_44k_32bit_stereo.wav'
recipes={
 'trap-place-0':[(GRIT(1),.01,.56,.42,0),(SPRING,6.94,7.68,.25,.035),(METAL(1),0,.315,.9,.64)],
 'trap-place-1':[(GRIT(2),.03,.66,.38,0),(SPRING,28.22,28.96,.23,.03),(METAL(3),0,.244,1,.67)],
 'trap-trigger-0':[(TRAP,.32,1.66,.88,0),(METAL(8),0,.807,.48,.015),(SPRING,.05,.83,.12,.03)],
 'trap-trigger-1':[(TRAP,.33,1.52,.84,0),(METAL(3),0,.244,.72,.008),(SPRING,35.25,36.06,.14,.02)],
 'trap-release-0':[(SPRING,15.0,16.23,.42,0),(GRIT(2),.13,.91,.52,.055),(METAL(1),0,.315,.35,.39)],
 'trap-release-1':[(SPRING,23.1,24.28,.44,0),(GRIT(1),.08,.83,.46,.06),(METAL(3),0,.244,.38,.42)],
}
report={"format":"48000 Hz mono PCM24", "method":"Original recorded layers, highpass 65 Hz, edit fades and linear peak normalization only", "cues":[]}
OUT.mkdir(parents=True,exist_ok=True)
for cue,recipe in recipes.items():
    layers=[layer(*r) for r in recipe]
    size=max(round(r['at']*RATE)+len(a) for r,a in layers)+round(.06*RATE)
    signal=np.zeros(size)
    for r,a in layers:
        at=round(r['at']*RATE);signal[at:at+len(a)]+=a
    signal*=.92/max(abs(signal))
    rms=float(np.sqrt(np.mean(signal**2)));peak=float(max(abs(signal)))
    assert np.isfinite(signal).all() and rms>.012 and peak<.99
    values=np.rint(signal*8388607).astype(np.int32)
    pcm=np.stack((values&255,(values>>8)&255,(values>>16)&255),axis=1).astype(np.uint8).tobytes()
    path=OUT/(cue+'.wav')
    with wave.open(str(path),'wb') as f:f.setnchannels(1);f.setsampwidth(3);f.setframerate(RATE);f.writeframes(pcm)
    recordings=[originals[r['source']] for r,a in layers]
    measured=values.astype(np.float64)/8388608
    manifest.append(dict(recordings[0],resource='Audio/External/'+cue,file=cue+'.wav',
        modifications=dict(sample_rate=RATE,mono=True,format='PCM24 WAV',loop=False,recordings=recordings,
            layers=[r for r,a in layers],method=report['method']),seconds=len(signal)/RATE,
        peak=float(max(abs(measured))),rms=float(np.sqrt(np.mean(measured**2))),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    report['cues'].append(dict(cue=cue,layers=[r for r,a in layers],seconds=len(signal)/RATE,
        peakDbFS=20*math.log10(peak),rmsDbFS=20*math.log10(rms),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
(ROOT/'SourceArt/Audio/traps-foley-recipe.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
manifest_path.write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps({"status":"RECORDED_TRAP_AUDIO_PASS","cues":len(recipes),"peakDbFS":20*math.log10(.92)},indent=2))
