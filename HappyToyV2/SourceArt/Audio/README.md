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
