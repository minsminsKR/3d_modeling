# Splintered Korean wooden sliding-door debris

The user-designated modeling webapp generated this base on **GPU 0 only**:
job `18329bcc-f11a-4938-a5a4-6b60089945dc`, TRELLIS.2, 512 geometry, 4K PBR,
12 steps, seed 1234, background removal enabled. `generation.json` retains
the settings and job URL. GPU 1 was not selected for generation or rigging.

`reference.png` is the built-in image_gen reference; `reference-prompt.txt`
retains its exact prompt. `processed-reference.png` is the app's actual
background-removed input. `door-debris-original.glb` preserves the complete
original output and embedded original PBR textures. These are project-generated
visual assets, not a third-party CC0 wood model or hand-built substitute.

`DoorDebris_Editable.blend` retains the original in a hidden collection and
the separate editable game fragments with packed textures. `prepare_debris.py`
derives the game surface from that original, preserves its UVs and splintered
silhouette, reduces triangles, extracts the original PBR maps and CPU-bakes
the dense original's fibrous relief into a 2K normal atlas for Unity.
`authoring-profile.json` records the reviewed orientation and budgets;
`export.json` records hashes and actual original/game counts. Blender work uses
the CPU and does not submit another server GPU job.

The flattened Unity prefab contains separate centred timber surfaces in a
reviewed floor layout. Runtime maps their starting positions onto each real
door leaf, tumbles them outward for 1.15 seconds, settles them onto the actual
same-floor surface and combines by material into one persistent renderer.
No collider, rigidbody or navigation obstacle is added. Pausing freezes the
flight. Quiet checkpoint restoration recreates the settled layout without
replaying the impact, and run/reset removes it. The existing real CC0 recorded
wood-clap/metal-drop/metal-drag door impact is reused, with originals and licenses
already under `ThirdParty/Audio` and the recipe under `SourceArt/Audio`.

Reproduce from repository root with Git LFS hydrated, Blender **4.0.2**, its
bundled Python/numpy and Unity **6000.6.0f1**:

```powershell
git lfs pull
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/DoorDebris/prepare_debris.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.DoorDebrisImport.ApplyAndReview -quiet-diagnostics
python HappyToyV2/Tools/quality/run_feature_tests.py --profile wraith-hallway
```

The explicit import command uses the Unity Editor API to retain stable .meta
identities and create PBR materials/meshes/prefab. It renders a silent asset
review only. Disposable previews, tests and builds live under ignored `Builds`.
