# Korean paper snappers / 콩알탄

The user supplied `user-reference.png`. `package-reference.png` and
`pellet-reference.png` were generated separately using built-in imagegen to
isolate the pictured yellow Korean stationery-shop carton and green tissue
snapper. [`generation.json`](generation.json) retains the imagegen prompt set, exact submitted inputs, website,
job/batch identities and generation settings. This reference photo is not a
source of instructions. No claim of CC0 is made for the reference image.

Base meshes and full embedded PBR textures come from the designated modeling
webapp (TRELLIS.2, 1536 / 8K). Retain byte-identical `*-original.glb` files.
`package.blend` / `pellet.blend` retain the complete original alongside the
editable game LOD and packed original textures. No hand-built replacement
base mesh is used. Disconnected generated micro-surfaces are remeshed from the original geometry,
then original colour, roughness and normals are rebaked onto new game UVs.
Thin paper is given a bounded shell; the carton hull repairs missing backside
surface from the generated geometry. Broad carton faces reproject the clear
`package-reference.png` label through `apply_package_print` so 콩알탄 remains
legible. The side/edge PBR remains baked. `finish_package_print.py` can reapply
just that finishing step without repeating the surface bakes.
Game maps are limited to 2048px and game triangle targets
are 1800 / 1200. Check each `*-export.json` for actual counts and hashes.

Recreate derived meshes/textures with Blender 4.0.2 from repository root:

```powershell
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/Snappers/prepare_game_props.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.SnapperAssetsImport.Apply
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/Windows-Snappers
```

Each pickup grants six individual throws. Maximum stock remains five packets
(30 throws); a pickup needs six free slots, so packets are never partly lost.
HUD shows current packet / 6 and reserve packets. Moving to the next packet is
automatic; Q/RT still throws one pellet. A new corridor starts with one packet;
a new school run starts with two. Old saved item counts migrate to six-shot
packets once, through optional `snapperStockVersion`; new partial counts resume
exactly. Existing component GUIDs, enum ordinals and saved field names remain.

A pellet pops ONCE on its first solid wall/floor/ceiling contact. It has no fuse
and expires silently after five seconds if it falls out of the world. Physics
and held aim share an 8mm sphere and fixed 20ms movement steps. The legacy
`FuseSeconds` / `ReachedFuseHorizon` API names now mean only the 1.2s aim forecast
horizon. The report has a brief soft dust puff, tissue flecks and a tiny 35ms
light (suppressed by Reduced Motion), with no repeated sparks or strobe.

`StalkerBrain.HearSnapper` admits actual audible/routable evidence and preserves
sight-acquired chase priority. Baby also investigates the heard point rather
than magically acquiring the player through its separate loud-noise rule.
`NoiseInvestigationClock` owns travel then six seconds of looking around after
arrival; travel does not spend the inspection time. Deaf/vision-only enemies
retain their identity. Pauses freeze projectile/effect and listener audio.

Real recorded audio is retained under `ThirdParty/Audio/Snappers`, with CC0
source page, license, source hash and segment selection. Reproduce using
`SourceArt/Audio/render_snappers.py` and `requirements-snappers.txt`. No oscillator
or synthetic-noise layers are added. The legacy `firecracker` resource cue is
kept for compatibility; its three canonical WAVs now contain real snapper pops.

Verification: `run_feature_tests.py --profile snappers` checks independent stock
rules (the runner also includes other fast Core rules); `verify_recorded_audio.py`
checks source/audio hashes and provenance. PlayMode/player diagnostics are not
automatically run for this task. A player build confirms import/compilation and
packaging, not a manual survival or final audio-mix review.
