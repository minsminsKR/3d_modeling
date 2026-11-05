# Temple prologue — implementation and remaining review

The new-story title entry now assembles the forest and temple, two days of
incense and prayer, the directional plume and gate, then prepares the existing
corridor. The two days share the same physical path. The second-day gate is
shown only when the player is not looking towards it; E on the gate begins the
masked transition. A separate suspend slot saves the phase, prayer time, player
pose and flashlight without replacing school or corridor checkpoint formats.
The existing corridor slot is consumed only after the new corridor is ready.
The CPU/physics profile does not certify rendered appearance, audio listening
quality or player FPS. An isolated Windows player camera/mixer inspection is
described below; it does not replace manual listening or displayed FPS.

## Gate opening follow-up — 2026-11-05

E currently starts the masked corridor transition and does not animate the
gate leaves. The preserved `CE_temple-gate_LOD0` is a single generated mesh:
45,914 of its 46,212 vertices belong to the same connected component, so
rotating an existing child transform cannot open the leaves independently.
The source-derived `gate-opening/gate-opening.blend` now contains a
three-part frame and two hinged leaves, with an **unreviewed** 1–73 frame
opening at 24 fps. The full-depth leaf partition preserves the closed source
surface and UVs. The hidden `PROTOTYPE - thin leaf alternative` collection
translates the source's disconnected rear surface by 2.8 source units without
flattening it. It also repairs 31 left and 33 right planar wood faces whose
UVs pointed to unrelated atlas islands, using affine fits to adjacent intact
wood faces. Three left-side faces also sample slightly higher in the same atlas
to remove a yellow-green speck beside a stud; two are among the 31. Other
thin-leaf UVs and every front vertex are unchanged. The repair is tied
to the preserved source hash and rejects changed face counts. At the game's
5.5 m gate scale the full-depth leaves are about 1.9 m thick, while the
repaired alternate leaves are about .47–.48 m. Low-sample CPU Blender previews
show that the large triangular texture smears and flattened rear deformation
are reduced. `gate_edge_finish.py` now follows the cut front/rear boundaries
and bridges the four edges with wood-textured strips in the same leaf mesh.
Missing boundary intervals are interpolated from neighboring source edges; the
report records the largest midpoint-to-nearest-boundary distance. A low-sample CPU view confirms the
previously empty free edge is filled. Its appearance and hinges still require
Unity camera review before normal play. The thin UV layer is explicitly
detached before editing: Blender's shared mesh data had previously allowed the
repair to alter the full-depth comparison copy as well. The authoring tool
now checks every reference UV against its pre-repair value; the independent
proof also compares the three speck locations with the original source.
The standalone `gate-opening.fbx` embeds its original textures and exports
the static **full-depth** parts. `gate-opening-thin.fbx` exports the static
thin parts with a placeholder material; the Unity importer connects the
preserved `CorridorEntrance/temple-gate` material and its PBR maps. Neither
FBX contains the blend animation. The thin candidate is installed only by
the explicitly requested native review flag below; the ordinary game retains
the original gate.

Reproduce and check the prototype with Blender 4.0.2 (background):

```powershell
blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/prepare_openable_gate.py
blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/verify_openable_gate.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.TemplePrologueImport.ImportGateReview -v2-audit-no-desktop-interaction
```

The optional `preview_gate_cpu.py -- --output <new-directory>` Blender script
creates sixteen 512px, eight-sample CPU-only material views with two threads,
including free edges and close views of the hinges. Pass `--views` followed by
view names to render a subset; `--asset-directory` can inspect a separate
editable blend alongside a matching `geometry-report.json`. Run it
through `Tools/quality/run_isolated_desktop.py` with `-b -t 2`; the directory
must not already exist. Its images and hashes are asset evidence, not game
camera, device-audio or FPS validation. Preview outputs are not source assets.

`inspect_gate_sweep.py -- --output <new-json-file>` is a separate CPU-only
Blender diagnostic for nine opening poses. It records intersecting source mesh
triangle pairs and their approximate locations; triangle centres do not measure
penetration or establish acceptable appearance. The thin-leaf pivots now move
0.075 source units toward the rear without moving any closed leaf vertex. The
prior pivots produced 1,050 left and 920 right mid-height intersecting triangle
pairs at full opening; the revised pivots produce zero at each of the eight
sampled opening poses. The closed pose still has shared and intersecting source
surfaces, and unsampled angles and actual appearance require game-camera review. The
yellow-green patch beside a left-side stud is no longer visible in the
low-sample CPU preview after the three-face atlas adjustment; Unity camera
appearance remains unverified.

The current measured poses and editable-source hash are preserved in
`gate-opening/sweep-proof.json`. A second pass samples each integer degree
from 1 to 80 using both Blender's object transform and an equivalent direct
matrix calculation. The object calculation finds no mid-height intersections;
the matrix calculation finds one left-side triangle pair at 3 degrees, near
the hinge. The numerical disagreement indicates a near-contact case, not a
measurement of penetration depth. The right side has none in either pass.
Generate a fresh report at a new path after any geometry or pivot change.

`geometry-report.json` records source and output hashes, repaired face indices
and provisional cut bounds. `surface-proof.json` records 4,713 original
closed-surface/UV samples, unchanged front geometry, the rear translation and
UV repair boundaries, leaf depth and keyframes, a static-FBX reimport check including packed textures,
and five central geometric rays that hit the closed leaves and clear at frame
73 in both variants. It also checks 39 rays through each of the four edge
strips on each leaf, both in the editable blend and after reimporting the
review FBX. Those rays do not verify player collision or visual quality.
The source area and sampled positions are preserved to numerical tolerance;
the FBX exporter drops 24 triangles and about 0.018 of 1,534 square model units.
These checks do not establish that either open version looks correct or that
the exported model is ready for normal play. The original corridor gate and its
source remain unchanged.

The opt-in isolated player review accepts `-v2-temple-gate-candidate` with
the `-v2-temple-review-output` diagnostics described below. It installs the
candidate during review only, captures the original gate camera at partially
and almost fully open states, and opens over three unscaled seconds before the
existing masked corridor transition. The candidate now keeps the central
blocker until the leaves finish opening, then enables a short invisible ramp
over the generated stone threshold. `temple-prologue-flow` verifies actual
E input, opening progress, the corridor transition and temporary mesh-collider
checks at closed/open states. A headless test configures the fully open state
and walks the actual player through three central lanes, using temporary
colliders on the visible gate mesh to expose contact with the stonework.
Without the ramp, those lanes stop at the roughly 0.35 m rear threshold.
The original `-v2-temple-gate-candidate` review still fades directly after
opening. A separate `-v2-temple-gate-walk-candidate` review keeps gameplay
active, pauses the opening and its sound with the game, and starts the same
masked transition only when the player crosses the central opening towards
the back. The headless flow exercises E, pause, a crossing outside the gate,
actual forward walking and corridor entry. The experimental route writes a
version 3 temple checkpoint with the opening state, progress and previous
crossing position. The gate collision state is restored before validating the
player pose, so suspension also works while opening and on the open threshold.
A pause just after crossing retains the preceding position and completes the
crossing after resumption. Version 1/2 saves still load; ordinary play continues
writing version 2. The gate review reuses the two Kenney CC0 door clips from
`ThirdParty/Audio/manifest.json` at opposite hinges: `door-open-1` plays at
0.85 pitch, then `door-open-0` starts 1.65 seconds into the motion at 0.70 pitch.
Their nominal playback ends near 1.66 and 2.96 seconds respectively. Pausing
holds the gameplay review and restoration seeks into whichever clip is active.
This timing is tested with the audio backend disabled; device listening and
the combined mix remain unverified.
The new native review path is prepared but has not yet been run with
graphics/audio. Normal play retains the original gate. These checks cannot
certify cut edges, material appearance, sound quality or FPS.

## Prayer furnishings follow-up — 2026-11-05

The prayer table and side offering jar now reuse the authored
`CorridorAltarChamber/offering-table` and `KoreanCorridorProps/onggi` assets.
Their original editable sources, physical textures and licenses remain in
`SourceArt/CorridorAltarChamber/`, `SourceArt/KoreanCorridorProps/` and
`Assets/Resources/GraphicsPbr/`. The table batches its surfaces by material;
two existing candle/flame models accompany a separate brass incense vessel.
The closed-lid jar remains a side offering. The old top collider,
interaction volume and incense release coordinates are unchanged. The CPU
profile checks their physical boundary and actual focus. The revised
arrangement has not yet received a game-camera visual or performance review.

## Isolated player inspection — 2026-11-04

The first game-camera captures exposed a sideways temple, a shiny blue indoor
floor used as outdoor ground, a bright sunset background and a hard-to-read
facade. The runtime placement now corrects the preserved model's import axes,
grounds its renderer bounds, uses matte soil and stone, and scopes the dark sky
and outdoor fill to the prologue. The camera and fog restore when the prologue
ends. Captures show the front facade, both incense states, sideways flowing
second-day smoke, the gate and the corridor; a separate hand-camera capture
shows the incense at release. The forest's generated tree geometry remains
visibly faceted. These are scripted camera views, not manual play.

`NativeTempleReview` is installed only with `-v2-temple-review-output <new
directory> -batchmode -v2-audit-no-desktop-interaction` in a Windows player
with graphics and audio. Run it on the private, never-switched desktop via
`Tools/quality/run_isolated_desktop.py`; its process-only audio mute guards
the real pre-device listener recording. The review performs a short isolated
keyboard walk, then controlled poses and interactions through the corridor.
The forest-step recording now starts at x=3 m, outside the visible stone path;
the previous center-path start produced stone steps and failed the review's
forest-surface check. A headless player-input test follows this same start for
four seconds and verifies forest contacts, without certifying the mixer output.
For the incense handoff it records a separate hand-camera image before
placement when the held visual is observed, then another after the placed
incense has appeared and the hand presentation has updated. The result fields
`incenseHeldBeforePlacement` and `incenseHeldAfterPlacement` record what the
review observed; a missing before image is not proof of a correct handoff.
When the gate candidate is enabled, the almost-open state is also captured
from left, right and behind in the same frame, using controlled camera poses.
The walking review returns to the approach pose before sending its isolated
keyboard input. These new captures are prepared but have not yet been run.
The 2026-11-04 D3D11 RTX 3090 Ti run recorded nine forest steps and 27.18s of
non-silent mixer PCM, with no clipped or nonfinite samples. It did not perform
subjective listening or test the output device.

At each of four controlled poses the same player submitted 120 1920x1080 eye
renders, synchronously reading one pixel per frame. Forest/prayer throughput
was 295–297 frames/s and the gate was 106 frames/s, with a 300 FPS diagnostic
cap and concurrent work; screenshots and audio capture are not manual gameplay.
FrameTimingManager reported no usable GPU timestamps. The readback and audit
overhead, frame cap and hidden offscreen rendering mean these are not displayed
FPS, GPU time, a supported-hardware guarantee or a substitute for device audio.

## Temple reference and generation

`generation-pipeline-records.json` preserves path-independent generation
stage options, outcomes and source archive-member hashes for the temple and
both pine jobs, including the rejected pine's shape and texture settings.
Original job records, metadata and model inputs remain alongside each model.
Complete path-normalized stage JSON and archive-independent verification are in [`service-pipeline-records/`](service-pipeline-records/README.md).

`temple/reference.png` was created using the built-in image generation tool.
Prompt: “Single isolated traditional small Korean Buddhist temple hall (법당),
complete freestanding building for 3D reconstruction, front three-quarter view
showing front and one side, realistic aged timber dancheong, dark grey tiled
roof, stone foundation, closed wooden lattice doors, modest rural mountain temple,
neutral diffuse studio lighting, plain white background, centered entire object
with generous margins, no trees, no people, no ground scenery, no text.
Physically plausible architecture, photorealistic material detail.”

Designated service: https://agrees-mic-icon-contribution.trycloudflare.com/
Request: `3f0897f2-2375-4f88-a040-039c6164ac33`
Job: `72e58e90-e6a0-485d-871b-d7b3e4c69dc1`
Model: TRELLIS.2, seed 1234, 1536 cascade, 12 steps, 8192 texture,
background removal enabled. The 1536 job failed on 2026-11-03 when CuMesh ran
out of GPU memory simplifying the 74-million-face remesh. No model resulted.
Retry with the same reference, seed and service: request
`31bafef0-612f-4e41-886d-1f82024795a8`, job
`8ce11907-69d7-4b74-ba4a-ded69ff731fc`, TRELLIS.2 1024 cascade, 4K texture.
The retry completed. The webapp front and side were inspected: roof, lattice
doors and stone base are present. The interior is not authored; this is an
exterior building. `temple/original.glb`, `processed.png`, `job.json` and
`metadata.json` are the downloaded source. `finish_temple.py` uses the shared
CorridorEntrance finishing recipe with Blender 4.0.2. It preserves the original
inside the packed editable blend and exports one 100,000-triangle derived mesh,
9m wide, and 4K PBR maps in `game-export/`. A 25,000-triangle second LOD failed
to reach its target (86,629 triangles remained) and was omitted. Front facing,
appearance of the reduced mesh and collision have not been fully verified. The
`TemplePrologueImport.ImportTemple` Unity batch importer creates the URP maps,
material and prefab from this export; the batch import succeeded, but the
prefab is now used by the outdoor prayer space. The 2026-11-04 in-game camera
review confirmed its corrected front-facing placement.

## Pine reference and generation

### Trunk-preserving review candidate — 2026-11-05

The archived game-camera forest shows conspicuous triangular damage on the
current pine trunk and roots. A CPU-only comparison of the preserved generated
original with the game LOD confirms that the reduction introduced much of the
damage. `prepare_pine_trunk_review.py` derives an alternative from the same
original, preserving source geometry below 4.6 m and wood-colored central
branches while reducing the canopy. It produces 195,666 triangles versus the current LOD0's 157,748. On 223
sampled source vertices below 4.6 m, the original-to-surface p95 distance is
0.354 m for the current LOD and less than 0.001 m for the candidate. These are
samples, not a full geometry or quality guarantee. For 920 upper vertices
classified by the source texture as wood, p95 is 0.094 m for the current LOD
and 0.074 m for the candidate. Texture classification is approximate and does
not identify every branch. A low-sample Blender CPU
comparison shows much improved roots and trunk; the canopy and in-game
appearance still require inspection. The Blender FBX reimport has 194,527
triangles and about 0.21% less total surface area; its sampled trunk distance
remains below 0.002 mm. Inspect the canopy in the actual Unity camera before
using the candidate in ordinary play.

The `pine-review/` editable blend, FBX and report retain the candidate while
the original `pine/pine.blend`, production LOD and textures remain intact.
Recreate with Blender 4.0.2 in background, run the independent verifier, then
import with the pinned Unity CLI:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/prepare_pine_trunk_review.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/verify_pine_trunk_review.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.TemplePrologueImport.ImportPineReview -v2-audit-no-desktop-interaction
```

`-v2-temple-pine-candidate` works only with the explicit isolated native
temple review. It replaces the visible trees for that review while retaining
the original tree roots, collision capsules and material. Both
baseline and candidate reviews capture `01b-pine-close.png`; compare these and
the forest views and timings before deciding whether to promote the candidate.
The additional geometry and real displayed FPS remain unmeasured.

The near editable mesh has 55 zero-length loop normals at 13 vertices. The
Blender-reimported FBX has none. An isolated Unity Editor audit of the four
imported meshes also found no zero or non-finite normals. Local attempts to
repair the editable mesh changed surrounding shading, so its source is left
unchanged until the game-camera review. `verify_pine_trunk_review.py` records
both counts and rejects zero normals in the Blender FBX reimport; it does not
claim the editable mesh has been repaired.

`TemplePineNormalsAudit.Run` checks both production and opt-in prefab LODs
using Editor-only mesh data without enabling runtime read/write copies. Unity
6000.6.0f1 imports production near/far as 433,273 / 117,280 vertices and review
near/far as 363,459 / 120,217 vertices. These are imported vertex counts, not
the Blender counts and not GPU timings. Repeat with:

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.TemplePineNormalsAudit.Run -v2-audit-no-desktop-interaction
```

The candidate now has its own distant LOD derived from the improved near
mesh. Its 40,697 triangles are below the production distant LOD's 44,161.
On 133 original lower-trunk samples, p95 surface distance changed from 1.13 m
to 0.139 m; upper branches remain simplified and require visual inspection.
The near and distant candidates share the same ground height. The generator
removes 3,659 QEM faces only when position, UV, normal and winding match exactly
before Blender encodes the custom normals. It separates another 9,075
coincident faces whose attributes or winding differ, so FBX reimport retains
them. Comparison with the earlier 44,356-face candidate found the same 40,697
unique oriented position/UV triangles. Normal encodings can differ slightly
after splitting. Three editable loops at one vertex had zero-length normals;
the generator now repairs those corners using the face normals and the
independent check rejects zero-length normals in both editable and reimported
FBX meshes. Their appearance still needs review.
Rounded per-corner position/UV/normal signatures match for 99.54% of faces;
rounding near thresholds can affect the comparison, so this is not exact proof.
The editable and exported versions have 36,273 vertices; actual GPU cost is
still unmeasured. Both versions and the
independent comparison are recorded in `pine-review/`.

To regenerate this distant candidate, install the pinned `fast-simplification`
dependency described below, set its directory in `PYTHONPATH`, then run:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/prepare_pine_distance_review.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/verify_pine_distance_review.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.TemplePrologueImport.ImportPineReview -v2-audit-no-desktop-interaction
```

`pine/reference.png` was created with the built-in image generation tool.
Prompt: “A single mature Korean red pine tree for realistic 3D game asset
reconstruction, full tree visible including trunk roots and entire irregular
wind-shaped crown, straight-on three-quarter view, weathered reddish bark and
dark green pine needles, asymmetrical natural branching, isolated on pure white
background, soft neutral studio light, no ground scenery, no shadows outside
the tree, no people, no text, no other trees. Photorealistic.”
Request `aad4512f-d2b9-4082-b1d0-939f0fd14b1c`, job
`25f9cb91-165b-4569-9b11-f61dc916bd4d`, Hunyuan3D 2.1, seed 1234,
texture on. The webapp result lost almost all foliage and was rejected; its
original and record are preserved in `pine/`. A replacement using TRELLIS.2
1024 cascade and 4K texture was submitted as request
`cc51c42e-431d-4392-bd03-5e2dcb87f4f2`, job
`4ff43435-9370-4552-9e84-30d96ac8ec80`. Its front and side were inspected in
the webapp: foliage and branches are present. The downloaded `original.glb`,
`trellis-job.json`, `trellis-metadata.json` and `trellis-processed.png` preserve
the result. An initial Blender attempt with a 75,000-triangle budget failed at
253,120 triangles. The later source-derived LOD0 has 157,748 triangles and the
distant LOD1 has 44,161. Both use a shared 2K atlas, Unity LODGroup and original
materials; the original and packed editable `pine.blend` are preserved. The
derived LODs have had a scripted game-camera review; displayed FPS and a
subjective quality approval remain open.

The rejected Hunyuan result also included independent initial and remeshed
geometry plus an OBJ/MTL with separate albedo, metallic and roughness images.
Those unchanged service originals and their SHA-256 hashes are retained under
`pine/hunyuan-original-assets/`; the textured GLB, input images, full job
record and metadata remain in `pine/`. They are preserved for authoring and
comparison only, and do not enable the rejected tree in the game.

`TemplePrologueSequence` is an engine-independent interaction sequence used by
the runtime director. Its CPU check is `dotnet run --project
HappyToyV2/Tools/quality/TemplePrologueTests.csproj` from the repository root.
It does not verify a playable prologue or presentation quality.

The sequence accepts explicitly numbered, validated phase values; its separate
checkpoint owner stores them without changing corridor checkpoints. The CPU
test covers each restored phase and its next action. `inspect_pine.py` reports original and welded mesh topology
without exporting or modifying the source; run it in Blender 4.0.2 background.
The preserved TRELLIS mesh has 961,875 faces and 1,136,608 boundary edges
before welding. At the existing 0.00002 Blender-unit weld tolerance it has
954,712 faces, 10,144 boundary edges and 27,894 non-manifold edges. These
counts identify difficult topology, but do not establish that topology alone
caused the decimator's 253,120-face result. The later reductions and atlas
projection are documented below; visual and performance review remain required.

`TemplePrologueDirector` now coordinates the interaction
sequence and a four-simulation-second prayer clock, with an explicit hold
input and validated restoration inputs. The player equips the existing hand
asset before its placement action; the incense appears at the release marker,
and the prayer clock begins only after placement. Checkpoint v2 records whether
placement happened; v1 saves migrate and unfinished placements restart safely.
`temple-prologue-logic` checks the
actual component's hold and phase transition; the standalone CPU check also
exercises invalid time and restoration. `temple-prologue-flow` adds the real
title, physical E focus, directional smoke, both days, gate and corridor,
pause, checkpoint and conflict protection. `temple-prologue-regressions`
covers selected existing school/corridor storage and title-entry contracts.
`temple-prologue-restore` checks all six resumable phases, including whether
the incense and already revealed gate return, as well as unfinished hand
placement and migration from the first checkpoint version.

### Rebuilding the pine LODs

Run `finish_pine.py` with Blender 4.0.2 in background. It welds coincident source
vertices, splits non-manifold edges and reduces the derived LOD0 while keeping
the original collection and packed textures. Then run `finish_pine_lod.py` with
`fast-simplification==0.1.12` (MIT, https://github.com/pyvista/fast-simplification)
available to Blender's Python 3.10. On Windows x64, one reproducible setup from
the repository root is:

```powershell
python -m pip install --python-version 310 --only-binary=:all: --no-deps --target HappyToyV2/Tools/.venv-pine fast-simplification==0.1.12
$env:PYTHONPATH = (Resolve-Path HappyToyV2/Tools/.venv-pine).Path
& 'C:/Program Files/Blender Foundation/Blender 4.0/blender.exe' -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/finish_pine_lod.py
```

LOD1 projects each reduced triangle onto the nearest LOD0 triangle's UV atlas;
the recorded centroid projection distance is 0.0243m at the 95th percentile,
0.262m maximum. These geometric numbers do not prove that foliage or texture
seams look correct. `TemplePrologueImport.ImportPine` imports both levels and
`QualityValidation.BuildWindows` checks their presence and the 50k LOD1 budget.
Subjective hearing and displayed FPS evaluation remain separate.
