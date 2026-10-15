# Iron monster trap / 몬스터 덫

The original base mesh and embedded PBR are the designated modeling webapp's
GPU **0 only** job `d6d1f02c-07fb-4e5a-affd-3443cacb6f16` (TRELLIS.2, 512, 4K,
12 steps, seed 1234). `generation-job.json` records the job and two earlier
out-of-memory failures. No training process was stopped, restarted or configured.
The final job was explicitly assigned to GPU 0 after the user disclosed GPU 1
training. Training health was not inspected or certified.

`trap-reference.png` was generated with built-in imagegen; the exact prompt and
settings are in `reference-generation.json`. `trap-processed-input.png` is the
actual background-removed input returned by the webapp. `trap-original.glb`
retains the complete, unmodified output and its embedded original PBR maps.
These generated assets are not represented as third-party CC0 recordings.

`Trap_Editable.blend` retains the complete original in a hidden source collection,
plus an editable, packed game mesh with separate Base, JawFront, JawBack and
PressurePlate objects. Game geometry comes from the generated surface, with
voxel/decimation finishing, new UVs, and rebaked 2K colour/metallic-smoothness/
normal maps. The surface budget is 24k triangles before closing jaw cut seams;
`export.json` reports the final count including those caps. The authored width is 55cm. `authoring-profile.json` records the
reviewed axes, part cuts and budget; `export.json` records original/output counts
and hashes. Editable open/snap/hold/release keys are included at 24fps. Unity
owns the precise five-second gameplay hold and does not import these animation
keys. `TrapVisuals` converts the hinge and pressure-plate motion through imported
FBX axes/scales.

Reproduce from repository root with Blender **4.0.2**, Python **3.11** and Unity
**6000.6.0f1**. Hydrate Git LFS originals and editable `.blend` files first:

```powershell
git lfs pull
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/Traps/prepare_trap.py
python -m pip install -r HappyToyV2/SourceArt/Audio/requirements-traps.txt
python HappyToyV2/SourceArt/Audio/render_traps.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.TrapAssetsImport.Apply -quiet-diagnostics
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.TrapAssetsPreview.Render -quiet-diagnostics
python HappyToyV2/Tools/quality/run_feature_tests.py --profile traps
```

The explicit preview renders the actual runtime prefab's armed, caught and
released poses from both sides, with no gameplay/player launch and no sound.
It checks that closing raises the jaws rather than pushing them under the floor.
Review frames and builds are disposable and remain outside source commits.

R / gamepad LB places one trap on clear, supported floor in front of the player.
A new school/corridor run starts with one; capacity is three. Supplies near some
existing item sites grant one without consuming the pickup when full. Invalid,
overlapping, hidden or paused placement never spends stock. A trap catches one
active monster once, holds position, facing, attack timers and body animation
for five **gameplay** seconds, and displays the same threatening face used for
capture. Pausing freezes the hold. The jaws release once; the spent trap then
disappears. Physical actor movement sweeps prevent stepping across the footprint
between frames without triggering it. Different floors and inactive/introduction
actors are excluded. AI components retain their sensing/cabinet state and resume
after release.

Optional checkpoint data preserves stock, cooldown, picked-up supplies and armed
traps without replaying placement sound. Legacy saves without the field remain
valid. Suspension is deferred while a monster is actively held, as it is during
other transient attack events. Restore validates saved geometry and trap poses
before applying state.

Three distinct sound cues, each with two variants, use **actual CC0 trap, spring
and metal recordings**. No generated tones/noise or reversed substitutes are used.
All originals, authors, source-page license evidence and SHA-256 hashes are under
`ThirdParty/Audio/Traps/sources`; the catalog and retained license evidence are in
`ThirdParty/Audio/Traps`. The renderer and exact segments/gains are under
`SourceArt/Audio`. Output is mono 48kHz PCM24 with edit fades, a restrained rumble
filter and peak normalization. Runtime cues use spatial attenuation, occlusion,
master/pause settings and captions. `ThirdParty/Audio/Audio-Credits.txt` is copied
into the player build.

`Tools/quality/verify_recorded_audio.py` independently checks all mandatory audio
resources, the six PCM24 trap cues, canonical hashes and retained recording
provenance. It does not certify a human listening session or a device mix.
