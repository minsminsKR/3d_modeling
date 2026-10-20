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

## Notices

The input image was made with the built-in image generator; its generation
prompt/settings are retained alongside it. Red staining is theatrical costume
paint. The generating tools are TRELLIS.2 and SkinTokens; their upstream pins,
retained license texts and notices are in `../CreatureStudio/license-provenance.json`,
`../CreatureStudio/licenses/` and `../CreatureStudio/LICENSES.md`. Those tool
licenses do not automatically relicense generated assets under MIT or CC0.
No generator weights, server caches, Blender binaries or personal settings are
required project sources or included here.
