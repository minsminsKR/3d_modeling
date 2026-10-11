# New Uncat, Cyclopse and Baby creature sources

These three characters are newly generated geometry and PBR assets, with new
semantic rigs and independently authored in-place movement. The previous scanned
meshes, textures and animation clips are not inputs to this authoring pipeline.

Uncat retains the ginger cat mascot, human face, whiskers, long tongue and party
hat. Cyclopse retains a single central eye, round tawny body, beard and flower.
Baby is an aged ceramic/rubber doll in a red romper, with a disproportionate
cracked head and crawling limbs.

## Included authoring data

Each character folder contains the modeling `reference.png`, original 8K PBR
`source-original.glb`, separately preserved `source-rigged.glb`, reviewed
`body-rig-plan.json`, source-order `body-surface-weights.npz`, surface
`contact-specs.json`, editable packed
`<key>_Editable.blend`, and `authoring-report.json`. The Blender scene retains the
high-resolution source and the separate game mesh. Large editable sources use Git
LFS; run `git lfs pull` after cloning. All three final bodies use a newly fitted
22-bone semantic rig and new surface weights. The downloaded AI rigs remain
provenance: misplaced Uncat/Cyclopse joints and Baby's tearing skin are not used
as the final game bindings.

Cyclopse's generated mesh needed a physical face correction to preserve its
single-eye identity. The recorded landmarks and `cyclopse_eye_surgery.py` rebuild
one enlarged central socket, join the surrounding skin and bind the new face
surface to Head. Its original uncorrected generated GLB is retained separately.

`concept-prompts.json` records the exact reference prompts and the built-in
`image_gen.imagegen` tool used. `generation-jobs.json` records the three A100 Studio
TRELLIS.2 jobs and requested 1536/8K settings. References were created specifically
for this project. Geometry and PBR maps were generated from those references;
rig correction, game mesh preparation and animation are project authoring work.
The application's generator and rigging tool source/setup are included under
`hunyuan3d-webapp` at the repository root.

## Reproduce the game assets

Use Blender **4.0.2**. External image extraction uses Python **3.11** and Pillow
**10.4.0**, numpy **1.26.4** and scipy **1.14.1**; Blender's bundled Python/numpy
runs the authoring script. From this
folder, replace `Uncat` with `Cyclopse` or `Baby`:

```powershell
python tools/export_pbr.py Uncat/source-original.glb ../../Assets/Resources/EnemyStudio/Uncat --size 2048
blender --background --factory-startup --python tools/author_creature.py -- --creature Uncat --animation-module tools/creature_animation.py
```

The explicit anatomical body plan and recorded skin weights are part of the
source, not guessed at runtime. `tools/verify_source_binding.py` checks source
hash/order, normalized palettes and consistent skin weights across coincident
UV seams. Visible posed skin is validated separately.
Only the game mesh is reduced. Its tangent normal map is baked from the actual
high-resolution geometry. Base color and metallic/roughness come from the new
generated atlas; Unity smoothness is packed in alpha. Original 8K maps remain
embedded in the source GLBs.

Movement is baked to `patrol` and `chase`, with Baby's additional `cry`. Uncat
supports itself on hands and feet, Baby on hands and knees, and Cyclopse walks
upright. See [the animation contract](tools/ANIMATION-CONTRACT.md).

In Unity **6000.6.0f1**, run
`HappyToy.V2.Editor.EnemyStudioGameImport.Apply()` after exporting all three
characters. It produces the resource prefabs and their own animation aliases,
checks semantic joints, geometry budgets, eye UV profiles and posed floor support,
and does not edit the protected school scene. New visual assets are mounted on
the existing navigation and encounter actors by `EnemyStudioVisual`.

## Silent 3D preview

The derived game GLBs are optional previews of the same new mesh, weights and
actions. They are generated from the included Blender sources and are not the
only editable copy. Start the local viewer from the Unity project directory:

```powershell
python SourceArt/CreatureStudio/tools/preview_server.py --port 8062
```

Open `http://127.0.0.1:8062/`. Choose a character and its patrol/chase/cry clip;
the viewer supports speed, pause and skeleton inspection. It uses the repository's
pinned Three.js vendor files and produces no audio.

## Silent verification

Unity PlayMode checks use `-runTests`; native diagnostics require
`-quiet-diagnostics -v2-creature-studio-output <directory>`. The process-session
silence guard verifies mute before allowing the native diagnostic to start.
Normal user game launches retain the user's audio settings.

The scene's first Cyclopse fall/get-up/looking event and Baby's crying behavior
use the new semantic skeleton. Actual game camera captures and import/grounding
reports are saved in the project's `Verification` folder.

## Handoff snapshot (2026-10-12)

This commit preserves the current authoring work in progress. Baby and Uncat
have packed editable Blender scenes and FBX exports. Cyclopse currently has
original/AI-rigged GLBs, the body plan, weights and eye surgery data, but no
editable Blender scene or game FBX yet. Rebuild those with the included tools
before running the all-character Unity import. The resource prefabs and final
all-character game validation are still pending; the descriptions above specify
the intended pipeline. The existing source-binding report validates the raw
source weights only.

On another machine, install Git LFS before cloning, then run `git lfs pull`
from the repository root. Install the pinned authoring dependencies listed above
and open `HappyToyV2` with the pinned Unity version. Run
`python HappyToyV2/Tools/quality/check_source_completeness.py` from the repository
root to check the local editable source inventory.
