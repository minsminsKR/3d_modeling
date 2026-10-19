"""Lady recorded pulse/flatline authoring. Python 3.11, numpy 1.26.4,
scipy 1.14.1, soundfile 0.13.1. --download obtains public licensed previews;
normal rendering is offline. Never synthesizes oscillators or noise layers.
"""
from pathlib import Path
import argparse,hashlib,io,json,re,urllib.request,uuid
import numpy as np
import soundfile as sf
from scipy.signal import butter,sosfiltfilt,resample_poly

PROJECT=Path(__file__).resolve().parents[2]
ROOT=PROJECT/'ThirdParty/Audio/LadyFinale'
DEST=PROJECT/'Assets/Resources/Audio/External'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def fetch(url):
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'HappyToyV2 licensed audio authoring'}),timeout=60).read()
def acquire():
    (ROOT/'sources').mkdir(parents=True,exist_ok=True)
    rows=[]
    for sound,label in [('61792','monitor-original'),('63832','monitor-unhissed')]:
        page='https://freesound.org/people/Benboncan/sounds/'+sound+'/'
        html=fetch(page).decode('utf-8')
        urls=re.findall(r'https://[^\s"<>]+-hq\.mp3',html)
        urls=sorted(set(urls));assert len(urls)==1,urls
        url=urls[0];path=ROOT/'sources'/(label+'.mp3');path.write_bytes(fetch(url))
        rows.append(dict(file=path.name,creator='Benboncan',license='CC-BY-4.0',
            license_url='https://creativecommons.org/licenses/by/4.0/',page=page,download=url,
            source_file='Steady Heart Monitor To Flatline'+(' Unhissed' if sound=='63832' else '')+'.wav (public HQ MP3 preview)',
            source_file_sha256=sha(path),pack='recorded-hospital-monitor',
            source_kind='Author composition from a beep recorded in a hospital; 63832 adds author Izotope RX hiss removal.'))
    (ROOT/'sources.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
    (ROOT/'CC-BY-4.0.txt').write_bytes(fetch('https://creativecommons.org/licenses/by/4.0/legalcode.txt'))
    print(json.dumps(rows,indent=2))

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--download',action='store_true');args=parser.parse_args()
    if args.download:acquire();return
    rows=json.loads((ROOT/'sources.json').read_text(encoding='utf-8'))
    comparison=[];audio={}
    for row in rows:
        path=ROOT/'sources'/row['file'];assert sha(path)==row['source_file_sha256']
        x,rate=sf.read(path);x=x.mean(axis=1) if x.ndim>1 else x
        audio[row['file']]=(x,rate)
        tail=x[142*rate:146*rate]
        noise=sosfiltfilt(butter(4,2000,fs=rate,btype='highpass',output='sos'),tail)
        windows=x[:30*rate].reshape(-1,round(.02*rate))
        comparison.append(dict(file=row['file'],continuous_tone_rms=float(np.sqrt(np.mean(tail**2))),
            high_band_rms_including_tone_harmonics=float(np.sqrt(np.mean(noise**2))),
            first_30s_quiet_window_rms_p10=float(np.percentile(np.sqrt(np.mean(windows**2,axis=1)),10)),window_seconds=[142,146]))
    heart=PROJECT/'ThirdParty/Audio/RecordedHorror/sources/heart.mp3'
    heart_info=json.loads((PROJECT/'ThirdParty/Audio/RecordedHorror/sources.json').read_text(encoding='utf-8'))['heart']
    assert sha(heart)==heart_info['source_file_sha256']
    x,rate=sf.read(heart);x=x.mean(axis=1) if x.ndim>1 else x
    x=resample_poly(x,160,147);rate=48000
    x=sosfiltfilt(butter(2,[25,600],fs=rate,btype='bandpass',output='sos'),x)
    def fade(y,attack,release):
        y=y.copy();a=round(attack*rate);b=round(release*rate)
        y[:a]*=np.linspace(0,1,a)**2;y[-b:]*=np.linspace(1,0,b)**2;return y
    beat=np.zeros(round(.32*rate))
    for start,end,offset in [(2.415,2.595,0),(2.790,2.930,.17)]:
        y=fade(x[round(start*rate):round(end*rate)],.006,.03)
        begin=round(offset*rate);beat[begin:begin+len(y)]+=y
    monitor=next(row for row in rows if row['file']=='monitor-unhissed.mp3')
    raw,sr=audio[monitor['file']]
    # Process surrounding context, then trim: no filter startup transient in
    # the delivered clinical sustain. Remove the composition's buzzy harmonics.
    context=resample_poly(raw[round(141*sr):round(146*sr)],160,147)
    clinical=sosfiltfilt(butter(4,[580,900],fs=rate,btype='bandpass',output='sos'),context)
    tone=fade(clinical[rate:4*rate],.012,.09)
    manifest_path=PROJECT/'ThirdParty/Audio/manifest.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'));entries=[]
    def emit(name,samples,peak,source,scope):
        samples-=samples.mean();samples*=peak/max(1e-12,np.max(np.abs(samples)))
        path=DEST/(name+'-0.wav');sf.write(path,samples,rate,subtype='PCM_16')
        actual,_=sf.read(path)
        entry=dict(source,resource='Audio/External/'+path.stem,file=path.name,
            modifications=dict(sample_rate=rate,mono=True,format='PCM16 WAV',loop=False,scope=scope,
                recordings=[source],source_kind='Retained physical human/hospital recordings; no oscillator or noise synthesis'),
            seconds=len(actual)/rate,peak=float(np.max(np.abs(actual))),rms=float(np.sqrt(np.mean(actual**2))),sha256=sha(path))
        entries.append(entry)
        meta=path.with_suffix('.wav.meta')
        if not meta.exists():
            template=(DEST/'tension-heart-0.wav.meta').read_text(encoding='utf-8')
            template=re.sub(r'guid: \w+', 'guid: '+uuid.uuid4().hex,template)
            meta.write_text(template.replace('normalize: 1','normalize: 0'),encoding='utf-8')
    emit('lady-heartbeat',beat,.70,heart_info,
        'Real 61 BPM stethoscope source. 25–600Hz bandpass; retained first/second heart contacts 2.415–2.595/2.790–2.930s placed at 0/170ms in a 320ms one-shot, 6/30ms fades, peak .70. Runtime changes spacing 58–168 BPM with constant pitch, not playback speed.')
    emit('lady-flatline',tone,.20,monitor,
        'Clinical sustain from the author hospital recording: context 141–146s, zero-phase fourth-order 580–900Hz bandpass removes harsh 1420Hz and higher harmonics, then retains 142–145s. Exact 3.0s mono/48kHz, 12ms attack/90ms release, peak .20. Recorded 710Hz fundamental; no oscillator, loop or pitch shift.')
    names={entry['file'] for entry in entries}
    manifest=[entry for entry in manifest if entry['file'] not in names]+entries
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    selection=dict(candidates=comparison,selected=monitor['file'],reason='Select the author-cleaned revision of the hospital-beep composition. Both retain nearly identical steady-tone level/harmonics; quiet-window metrics compare their noise floors. The purely Audacity-created 210793 candidate was rejected in favor of documented hospital-recording origin.',
        cues=entries,toolchain=dict(python='3.11',numpy='1.26.4',scipy='1.14.1',soundfile='0.13.1'))
    def spectral(samples):
        y=samples[round(.2*rate):round(2.7*rate)]
        energy=abs(np.fft.rfft(y*np.hanning(len(y))))**2;freq=np.fft.rfftfreq(len(y),1/rate)
        return dict(seconds=len(samples)/rate,peak=float(max(abs(samples))),
            upper_harmonic_to_fundamental_db=float(10*np.log10(sum(energy[freq>1100])/sum(energy[(freq>650)&(freq<780)]))),
            first_sample=float(samples[0]),last_sample=float(samples[-1]))
    reference=fade(resample_poly(raw[round(142*sr):round(146.4*sr)],160,147),.02,.55)
    reference-=reference.mean();reference*=.32/max(abs(reference))
    buffer=io.BytesIO();sf.write(buffer,reference,rate,format='WAV',subtype='PCM_16');buffer.seek(0);reference,_=sf.read(buffer)
    final,_=sf.read(DEST/'lady-flatline-0.wav')
    selection['terminal_quality']=dict(scope='Recorded PCM spectrum/edge/duration comparison; no device listening certificate',before=spectral(reference),after=spectral(final))
    assert len(final)==rate*3 and selection['terminal_quality']['after']['upper_harmonic_to_fundamental_db'] < -45
    (ROOT/'selection.json').write_text(json.dumps(selection,indent=2)+'\n',encoding='utf-8')
    credits=PROJECT/'ThirdParty/Audio/Audio-Credits.txt';text=credits.read_text(encoding='utf-8')
    text=text.replace('Edited: recorded flatline excerpt 142–146.4s, mono/48kHz, level and edge fades.',
        'Edited: clinical 3.0s recorded flatline excerpt 142–145s, zero-phase 580–900Hz bandpass, mono/48kHz, level and 12/90ms edge fades.')
    if 'Steady Heart Monitor To Flatline Unhissed' not in text:
        text+='\nLady terminal tone — Steady Heart Monitor To Flatline Unhissed.wav — Benboncan\nhttps://freesound.org/people/Benboncan/sounds/63832/\nCreative Commons Attribution 4.0 International (CC BY 4.0)\nhttps://creativecommons.org/licenses/by/4.0/\nEdited: clinical 3.0s recorded flatline excerpt 142–145s, zero-phase 580–900Hz bandpass, mono/48kHz, level and 12/90ms edge fades.\nLady heartbeat additionally edits the credited Benboncan Heartbeat Mono.wav recording into a short two-contact pulse; pitch remains constant while spacing changes.\n'
    credits.write_text(text,encoding='utf-8')
    print(json.dumps(dict(comparison=comparison,cues=[dict(file=e['file'],seconds=e['seconds'],peak=e['peak']) for e in entries]),indent=2))

if __name__=='__main__':main()
