# Recorded horror audio authoring

The corridor Baby also owns one **15.55-second actual recorded crying loop**.
After the other passes, run `python SourceArt/Audio/render_corridor_baby.py` with
the same CPython 3.11 / `requirements-corridor.txt` environment. Selected and
comparison recordings, original hashes, CC0 sources and decisions are retained
in `ThirdParty/Audio/CorridorBaby`. No network access is needed to regenerate it.
This pass preserves the natural voice pitch and breath gaps; the production
emitter supplies spatial positioning, physical occlusion and pause/mute behavior.

Baby state verification is separate from waveform/source validation:

```sh
dotnet run --project Tools/quality/BabyMemoryChecks.csproj
HappyToyV2.exe -quiet-diagnostics -v5-baby-output <external-output-directory>
```

The C# console executes the production memory transitions and rejects invalid
checkpoint states. Six EditMode `CorridorBabyMemoryTests` cover those event/save
sequences and Unity's null-inline-object JSON round trip inside Unity. The opt-in native probe exercises real navigation, genuine
sight and pursuit loss with explicitly controlled sound callbacks/poses, then
isolates the recorded spatial cry for PCM, pause, mute, cabinet danger admission
and owned cleanup. Automatic verification uses the diagnostic process-session
silence guard; ordinary play keeps the user's sound settings. A failed guard
retains listener volume zero and cannot prove positive mixer output.
It emits its exact scope and preserves the natural listener
WAV. These diagnostics do not certify human listening or natural survival balance.

The corridor revision has five new wood contacts, a revised recorded detection
cue, four creature contacts and three field-recorded room loops. Rebuild these
thirteen cues **after** the tool below with CPython 3.11 and
`requirements-corridor.txt`, then run `python SourceArt/Audio/render_corridor_foley.py`
from the Unity project directory. Selected MP3 preview sources, hashes, CC0 origins
and candidate comparisons are retained in `ThirdParty/Audio/CorridorFoley`.
The canonical final cue hashes are in `ThirdParty/Audio/manifest.json`; the older
RecordedHorror prepared-cues list describes its own earlier authoring pass.

For the current corridor revision, use this separate pinned environment from the
repository root. The renderer locates its sources relative to its own file, so the
working directory does not affect the output:

```powershell
py -3.11 -m venv HappyToyV2/SourceArt/Audio/.venv-corridor
HappyToyV2/SourceArt/Audio/.venv-corridor/Scripts/python.exe -m pip install -r HappyToyV2/SourceArt/Audio/requirements-corridor.txt
HappyToyV2/SourceArt/Audio/.venv-corridor/Scripts/python.exe HappyToyV2/SourceArt/Audio/render_corridor_foley.py
py -3.11 HappyToyV2/Tools/quality/verify_recorded_audio.py
```

The independent verifier needs only Python's standard library. It checks every
mandatory resource against the canonical manifest, retained recording hashes,
PCM data and Unity metadata. It does not render a Unity mix or certify listening
quality. The older authoring environment below applies to the preceding 23-cue
pass; run the corridor renderer last when rebuilding both passes.

The game imports the prepared WAVs in `Assets/Resources/Audio/External`.
Playing or building the Unity project does not require Python or a source-pack
download. The audio authoring tool was tested with CPython **3.12**, with all
Python dependencies pinned in `requirements.txt`.

From the repository root on Windows:

```powershell
py -3.12 -m venv HappyToyV2/SourceArt/Audio/.venv
HappyToyV2/SourceArt/Audio/.venv/Scripts/python.exe -m pip install -r HappyToyV2/SourceArt/Audio/requirements.txt
HappyToyV2/SourceArt/Audio/.venv/Scripts/python.exe HappyToyV2/SourceArt/Audio/render_recorded_horror.py
```

On other platforms, use Python 3.12 and the environment's `bin/python`.
The renderer resolves its project from its own location and uses no workstation
paths, credentials, local caches, or network downloads.

`ThirdParty/Audio/RecordedHorror/sources/` contains the exact selected source
recordings. `sources.json` records their names, origins, licenses and SHA-256
values, which the renderer checks before editing. Freesound inputs explicitly
retain the publicly accessible high-quality MP3 preview encoding; they are not
described as original lossless WAV downloads. `provenance.json` preserves author,
recording-origin and license facts with links and capture hashes, excluding the
websites' scripts and service credentials. The real human heartbeat requires **CC BY 4.0**
attribution; the other newly selected recordings use **CC0 1.0**. Distribution
must include `ThirdParty/Audio/Audio-Credits.txt`, which the Windows build copies
beside its executable.

The renderer overwrites the 23 selected prepared cues and their catalog entries,
preserves existing Unity `.meta` GUIDs and writes `prepared-cues.json`.
It uses recording excerpts, mono conversion, filtering, fades, rate changes and
layering. It does not synthesize oscillator or noise replacements. The retired
cabinet synthesis generator has been removed.

Reproducing the current cues needs no `--candidates` argument: the retained
recordings are sufficient. That optional argument supports the original
acquisition layout with Owlish/bell/firework/scrape ZIPs, the extracted
`workshop/workshop/*.wav`, ignition FLAC and publicly licensed MP3 previews;
it is intended only for deliberately selecting or updating sources.

Candidate decisions: [SELECTION.md](../../ThirdParty/Audio/RecordedHorror/SELECTION.md).
Prepared audio and all transformations: [manifest.json](../../ThirdParty/Audio/manifest.json).
Keep the sources, manifests, resulting WAVs, their `.meta` files and credits in
Git together when revising a cue. Run the source completeness check after staging.

## Corridor mask runner and Korean Baby mutter

`render_corridor_mask.py` uses the existing pinned corridor requirements and
retained real parquet, workshop contacts and two CC0 human whistle candidates.
It creates three heavy pounding contacts, two near three-gesture human whistles
and one physical door-break contact. No source acquisition is needed to rebuild.
The new emitter owns live clips and fixed door impact sources, uses actual
distance travelled for steps and a seven-metre near range for its whistle.

The Korean Baby searching phrase **어디..? 어디있어??** is disclosed as generated
character speech. Its two raw local neural utterances and comparison take are
retained under `ThirdParty/Audio/CorridorBabyMutter/sources/`. The prepared
speech changes voiced pitch and formants separately to preserve Korean speech
timing; the existing real Baby cry remains a separate source. Normal offline
regeneration does not download a model or use a speech account:

```powershell
py -3.11 -m venv HappyToyV2/SourceArt/Audio/.venv-mutter
HappyToyV2/SourceArt/Audio/.venv-mutter/Scripts/python.exe -m pip install -r HappyToyV2/SourceArt/Audio/requirements-baby-mutter.txt
HappyToyV2/SourceArt/Audio/.venv-mutter/Scripts/python.exe HappyToyV2/SourceArt/Audio/render_baby_mutter.py
```

To deliberately regenerate raw neural speech, first install the MIT MeloTTS
library at the fixed commit with **no dependency auto-install**, then the
documented generation requirements in a separate CPython 3.11 environment:

```powershell
py -3.11 -m venv HappyToyV2/SourceArt/Audio/.venv-generation
HappyToyV2/SourceArt/Audio/.venv-generation/Scripts/python.exe -m pip install --no-deps https://github.com/myshell-ai/MeloTTS/archive/209145371cff8fc3bd60d7be902ea69cbdb7965a.zip
HappyToyV2/SourceArt/Audio/.venv-generation/Scripts/python.exe -m pip install -r HappyToyV2/SourceArt/Audio/requirements-baby-generation.txt
HappyToyV2/SourceArt/Audio/.venv-generation/Scripts/python.exe HappyToyV2/SourceArt/Audio/generate_baby_mutter.py
```

This explicit optional generation step fetches the fixed public MeloTTS-Korean
revision and checks the configuration/model SHA-256 values. The Korean context
encoder revision is also fixed. Use `--model-dir FOLDER` to supply those checked
`config.json` and `checkpoint.pth` files locally. Do not install the `unidic`
package, whose empty dictionary would override `unidic-lite`. The two actor
words have an authored standard Korean pronunciation table; unreviewed words
fail instead of invoking an auto-installing morphology backend. Generation
seeds, weights, licenses, text, phonetics and CPU parameters are retained in
`generation.json` and the generator. Cross-hardware raw inference may vary;
ordinary offline preparation is deterministic from the retained raw WAVs.

All authoring commands above only write files and never play sound. Automated
Unity/player diagnostics must use `-quiet-diagnostics` and the confirmed current
Windows process-session mute. Ordinary players retain the user's volume.

The optional independent `Tools/quality/check_baby_mutter_intelligibility.py`
uses `faster-whisper==1.1.0` and a fixed public large-v2 model conversion on
CPU/int8 to check the two Korean phrases without a text prompt or playback.
Install that package in a separate environment and use `--download` once on a
new machine to acquire the fixed model; subsequent checks are offline. Its
report hashes both prepared WAVs. It assesses recognized words, not human
listening quality, perceived speaker age, Unity import or the native game mix.

## Original detection and pursuit composition

`render_detection_pursuit.py` creates two immediate 1.06/1.12-second detection
stings and a 6.4-second, 150-BPM recorded pursuit bed. It uses the pinned
`requirements-corridor.txt` environment, existing retained human/physical
recordings and two newly retained CC0 acoustic violin preview files. It does not
copy the reference game's audio, use generic oscillator/noise synthesis, or add
a duplicate heartbeat. Normal regeneration is fully offline:

```powershell
HappyToyV2/SourceArt/Audio/.venv-corridor/Scripts/python.exe HappyToyV2/SourceArt/Audio/render_detection_pursuit.py
```

Independent string tail/head overlaps and percussion tails preserve a continuous
loop with no global silence fade at the seam. Unity/runtime ownership controls
onset, release, intensity and comfort gain. Candidate decisions, retained source
hashes and all edits are in `ThirdParty/Audio/DetectionPursuit/`; the canonical
manifest and distributed credits are updated together. Offline PCM validation
checks loop duration/headroom and unusual boundary cuts in addition to the full
mandatory inventory. It does not certify human listening or the actual game
mix; all engine diagnostics must remain protected by `-quiet-diagnostics`.
