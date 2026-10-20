# Lady authoring source

The current reference is `lady-concept-v4.png` with its exact prompt/provenance.
The user-designated A100 Studio webapp generated the base model and AI skeleton.
`webapp/generation.json` records the job, settings, input and original hashes.
`webapp/source-original.glb` retains original generated geometry and embedded 8K PBR;
`webapp/source-ai-rig.glb` retains the unmodified 28-joint provider output.

`Lady_Editable.blend` packs the high-resolution original, retained source skin,
reviewed game surface, semantic skeleton, three game LODs and authored chase.
The 37.3-second 60fps action uses reproducible independent head/neck/limb beats,
fast head tremor, sudden angles and uneven holds. It contains no purchased or
downloaded motion capture. Runtime pause and talisman stop both pose and pursuit;
the reduced-motion setting scales pose angles to 18 percent.

The game surface is a voxel-remeshed/decimated copy of the actual generated
model. Its source textures and normals are reprojected through the original
surface BVH into a 2048 atlas. Game-only sleeve/hair boundaries and weights are
reviewed to avoid fused cloth being dragged across unrelated joints. Original
generated geometry, UVs, textures and AI skin remain separately recoverable.
`surface-report.json`, `authoring-report.json` and `pbr-export-report.json`
record the final transfer, topology, joint schedules and texture hashes.

## Reproduce from a fresh clone

Run `git lfs pull` and use Python 3.11 with Pillow 10.4.0. Blender 4.0.2 includes
its required Python/numpy. Obtain the official portable Windows build:
<https://download.blender.org/release/Blender4.0/blender-4.0.2-windows-x64.zip>
SHA256 `bddb7ab6880bbb80297b93731506a48bbebdce214ee5edd17dbef8380daecded`.
Unpack it outside tracked sources. From the repository root:

```powershell
& '<Blender4.0.2>/blender.exe' --background --factory-startup --threads 4 --python-exit-code 1 --python HappyToyV2/SourceArt/Lady/author_lady.py
python HappyToyV2/SourceArt/Lady/export_pbr.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.LadyGameImport.Apply
python HappyToyV2/Tools/quality/run_feature_tests.py --profile lady --graphics-api d3d11
```

To finish a deliberately edited existing blend without replacing the generated
source, pass `-- --refine-existing` to the Blender command. Preview renders go
under ignored `HappyToyV2/Builds/LadyReview-20261018/`; no preview is required to
rebuild. The `.fbx`, `.anim`, `.prefab`, maps, material and Unity metadata in
`Assets/Resources/Lady/` are the portable game outputs. The importer measures
the animated skin envelope, validates real joint paths and retains three LODs.

The robe hem stays on the pelvis; only the separated exposed foot surfaces
follow the leg chain. Every LOD preserves the deliberate hair/sleeve/robe/foot
seams. The stride hinge is resolved through each generated bone rest frame,
while independent head, elbow, wrist and knee beats retain abnormal angles.
Unity stores complete frame-zero FBX transforms in the prefab and only moving
curves in the action. `EnemyStudioRig.BakeLocalPose` evaluates the actual skin
matrices for the 65-pose culling envelope and optional URP preview; it avoids
legacy FBX `BakeMesh` scale conventions. `LadyModelReview.ImportAndRender`
checks 64 visible action frames and genuine head movement before declaring pass.

## 2026-10-21 game polish

Lady remains a grotesquely contorting pursuer. The original 76 quaternion
curves and their exact 170,164 keys are unchanged (`rotationSha256` in the
authoring report). Do not replace the independent joint snaps/tremor with a
normal walk or smooth them as an optimization. The 37.3-second clock and saved
pose time keep their original meaning.

`polish-settings.json` pins game-only UV, LOD and support-height finishing.
`polish_game.py` preserves the reviewed near UVs and reprojects the actual
original PBR and normals. The near geometry is unchanged. The 32k middle LOD
keeps that atlas; the 12k far
LOD is a 2cm remesh of the retained model, re-split at the anatomical boundaries
by `refine_game.py`, with non-overlapping Smart Project charts (0.001 margin)
and its own 1K source-baked atlas. `far-surface-report.json`
records its source distance and topology. This fills distant sub-pixel tunnels,
without replacing the generated base. The game ORM metallic channel is zero
for skin, robe, hair and feet; original 8K material inputs remain untouched.

`Lady vertical support only` is a separate editable action on `Lady_Fit`.
It compensates only visual root height, using 2,239 samples of the actual lower
surface, at most 18cm and two-frame circular smoothing. All original bone
rotations remain exact. Navigation, speed, attacks, pause and checkpoint
schemas are unchanged. `LadyMotion` attenuates this offset with joint angles
when Reduced Motion is selected.

Unity's importer evaluates the complete native FBX frame-zero baseline,
including constant quaternion channels absent from `GetCurveBindings`. Only
nearly constant scale channels and whole static rotation groups use 1e-5
tolerance; moving rotation curves retain the original strict threshold.
`lady-polish` checks every 60Hz frame against the actual imported FBX, with a
half-millimetre positional tolerance, plus real skin support, reduced-motion
and LOD/rig budgets. It runs without graphics/audio/desktop input.

The reviewed Unity 6000.6.0f1 import retains 123,861 near vertices, reduces the
middle LOD from 84,512 to 66,819 and the distant LOD from 61,602 to 18,681.
Triangle counts are 79,593 / 32,000 / 12,000. The runtime action has 77 curves
and 172,403 keys (formerly 159 / 356,001); measured clip memory is 13.08 MiB
(formerly 22.91 MiB). This does not measure gameplay FPS. The close-up colour
atlas retains its exact original SHA256. At 19.7 seconds, actual skinned lower
surfaces are 2.7–3.1cm above the origin instead of 13.6–14.3cm.

Reproduce on Blender 4.0.2 and the documented Python/Pillow toolchain:

```powershell
blender --background --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/Lady/polish_game.py
blender --background HappyToyV2/SourceArt/Lady/Lady_Editable.blend --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/Lady/polish_far_lod.py
python HappyToyV2/SourceArt/Lady/export_pbr.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.LadyGameImport.Apply
python HappyToyV2/Tools/quality/run_feature_tests.py --profile lady-polish --no-desktop-interaction
```

`render_polish_review.py` uses Cycles **CPU**, OpenImageDenoise, adaptive samples,
two threads and a fixed review camera. Pass `--source <blend> --output <new
frame-directory> --frames 64 --samples 8` after Blender's `--`. It renders every
third actual source frame at true 20fps playback, with a temporary floor and
studio lights. `make_polish_gifs.py --before <frames> --after <frames> --output
<new-directory>` assembles the genuine frames with Pillow. The 3.2-second GIF
is an excerpt, not the full loop or live gameplay/FPS evidence. Render outputs
belong under `Builds/`; no render step saves or edits the source blend.

## Notices

The input image was made with the built-in image generator; its generation
prompt/settings are retained alongside it. Red staining is theatrical costume
paint. The generating tools are TRELLIS.2 and SkinTokens; their upstream pins,
retained license texts and notices are in `../CreatureStudio/license-provenance.json`,
`../CreatureStudio/licenses/` and `../CreatureStudio/LICENSES.md`. Those tool
licenses do not automatically relicense generated assets under MIT or CC0.
No generator weights, server caches, Blender binaries or personal settings are
required project sources or included here.
