"""Build performed pursuit screams and recorded animal paw contacts, silently.

Python 3.11 + requirements-corridor.txt. No synthesis, AI service or downloads.
Retained originals and candidates are hash-checked before producing Unity WAVs.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfiltfilt

PROJECT = Path(__file__).resolve().parents[2]
DEST = PROJECT / 'Assets/Resources/Audio/External'
RATE = 48000


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(folder, entry):
    path = folder / 'sources' / entry['file']
    if sha(path) != entry['source_file_sha256']:
        raise ValueError('Changed original: ' + str(path))
    audio, rate = sf.read(path, always_2d=True)
    audio = resample_poly(audio.mean(axis=1), RATE, rate)
    return audio


def fade(audio, attack=.003, release=.025):
    audio = audio.copy()
    a, b = min(round(attack * RATE), len(audio)//2), min(round(release * RATE), len(audio)//2)
    audio[:a] *= np.linspace(0, 1, a)
    audio[-b:] *= np.linspace(1, 0, b)
    return audio


def main():
    manifest_path = PROJECT / 'ThirdParty/Audio/manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    human = PROJECT / 'ThirdParty/Audio/RecordedHorror'
    animal = PROJECT / 'ThirdParty/Audio/BeastFootfalls'
    catalog = json.loads((human/'sources.json').read_text(encoding='utf-8-sig'))
    paws = json.loads((animal/'sources.json').read_text(encoding='utf-8-sig'))
    comparisons = []
    for kind, folder, candidates in [('voice', human, {key:catalog[key] for key in ('scream','panic','scared')}),
                                     ('paws', animal, paws)]:
        for key, entry in candidates.items():
            audio = load(folder, entry)
            comparisons.append(dict(kind=kind, key=key, source=entry, seconds=len(audio)/RATE,
                                    rms=float(np.sqrt(np.mean(audio**2)))))
    outputs = []

    def save(cue, audio, sources, edits, **settings):
        audio = audio.copy(); audio *= .76 / max(1e-8, float(np.max(np.abs(audio))))
        path = DEST/(cue+'.wav'); sf.write(path, audio, RATE, subtype='PCM_16')
        pcm, _ = sf.read(path)
        entry = dict(sources[0], resource='Audio/External/'+cue, file=path.name,
                     seconds=len(pcm)/RATE, peak=float(np.max(np.abs(pcm))), rms=float(np.sqrt(np.mean(pcm**2))),
                     sha256=sha(path), modifications=dict(sample_rate=RATE, mono=True, format='PCM16 WAV',
                        recordings=sources, source_kind='Recorded performance and physical animal contacts; no synthesized layers',
                        scope=edits, **settings))
        outputs.append(entry)

    # A sustained performed scream is more distinct than the former short gasp.
    scream = load(human, catalog['scream'])
    scream = resample_poly(scream, 50, 41)  # 0.82x playback, with natural pitch lowering.
    scream = sosfiltfilt(butter(2, [90, 6500], btype='bandpass', fs=RATE, output='sos'), scream)
    scream = np.pad(fade(scream, .008, .09), (0, round(.45*RATE)))
    save('enemy-cyclopse-chase-0', scream, [catalog['scream']],
         'Full recorded human scream at 0.82x speed; mono, 90Hz-6.5kHz bandpass, 8/90ms fades and .45s recovery gap. Repeated only during Chase. Peak .76.', loop=True)

    # Preserve real nail/pad transients, replacing human shoes and metal clangs.
    original = load(animal, paws['dog-wood'])
    for index, centre in enumerate((.92, 1.17, 1.55)):
        take = original[round((centre-.022)*RATE):round((centre+.085)*RATE)]
        bright = resample_poly(take, 25, 18)  # 0.72x, audible pads/nails.
        bright = sosfiltfilt(butter(2,[65,6500],btype='bandpass',fs=RATE,output='sos'),bright)
        body = resample_poly(take, 25, 12)  # 0.48x contact body, same real recording.
        body = sosfiltfilt(butter(2,[45,420],btype='bandpass',fs=RATE,output='sos'),body)
        audio = np.zeros(round(.26*RATE))
        audio[:len(bright)] += fade(bright)*.8
        audio[:len(body)] += fade(body)*.65
        save('mask-beast-step-'+str(index),audio,[paws['dog-wood']],
             'Real dog paw contact on wood; source window centre-22ms to centre+85ms, 0.72x pad/nail layer and 0.48x low body from the same recording, 65Hz-6.5kHz and 45-420Hz bands, 3/25ms fades, .26s length, peak .76. No human shoe, metal or synthetic layers.',
             loop=False, source_centre_seconds=centre)
    names={entry['resource'] for entry in outputs}
    manifest=[entry for entry in manifest if entry['resource'] not in names]+outputs
    manifest_path.write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    report=dict(selected_voice='scream',selected_paws='dog-wood',candidates=comparisons,
                reasons=dict(scream='Sustained human vocal performance rather than a brief gasp or soft pant.',
                    **{'dog-wood':'Close, separated real paw/nail transients on the same broad surface family as the corridor.',
                       'dog-linoleum':'Real animal comparator retained with attribution; the pass-by has changing distance and a different floor.'}),
                outputs=outputs,scope='Recorded-source and PCM comparison; no claim of final device listening or subjective realism certification.')
    (PROJECT/'SourceArt/Audio/chase-voice-selection.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print(json.dumps([dict(resource=entry['resource'],seconds=entry['seconds'],sha256=entry['sha256']) for entry in outputs],indent=2))


if __name__ == '__main__':
    main()
