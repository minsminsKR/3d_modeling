# CreatureStudio sources and game integration

Cyclopse, Chapter 1 Uncat (운캣, the four-legged cat) and Baby use newly generated
models from the user-designated modeling webapp in `hunyuan3d-webapp` at the
repository root. The retained generation jobs are listed in `generation-jobs.json`.
School Hwacat (화캣) is a distinct dancing-doll/demonic portrait monster. Its
original Hwacat_angry mesh, rig and clips are retained; it does not use UncatRuntime.

Cyclopse follows the user's 2026-10-12 round furry, single-eye human-face reference.
It retains the beard, purple flower, short legs and upright pursuit. Its original
Hunyuan3D 2.1 model fits the runtime geometry budget, so the game preserves its
geometry, UVs and source normals. `source-webapp-original.glb` is the downloaded
original; `source-original.glb` bakes its node coordinate basis into Y-up without
changing UVs or embedded images. The original AI rig is separate provenance.

Uncat and Baby retain their TRELLIS.2 original geometry and embedded 8K PBR.
Their game meshes use an outer-surface voxel reconstruction and collapse in
Blender to remove overlapping microscopic sheets. Source textures and normals
are transferred to a new game UV atlas by actual nearest-surface BVH queries.
Uncat's connected game skin is regularized across neighboring vertices while
preserving facial and distal limb ownership. Baby joints are fitted from actual
source limb cross sections. The final rigs have 22 semantic Mixamo-named bones.
They use fresh in-place patrol/chase actions; Baby also has cry. Chase uses a
two-stride supercycle with creature-specific, off-beat head yaw/roll/pitch snaps
and a small body lurch. Patrol and cry retain their calm authored curves.
Uncat runs on hands/feet; Cyclopse runs upright; Baby pursues on hands/knees.
The existing AI Chase state selects these own clips through V1MonsterMotion.

The 2026-10-13 gameplay feedback adds an editable runtime gait layer in
`Assets/Scripts/AI/EnemyLocomotionPose.cs`, composed by `V1MonsterMotion`.
It keeps these source rigs, weights and imported clips and restores the base
joint rotations before every sample. Patrol adds upper-limb swings of 28 degrees
for Cyclopse and 24 for Uncat; pursuit blends to 72 and 58 degrees respectively.
Leg stride grows from 18 to 42/48 degrees, with opposing diagonal pairs for
Uncat. Actual agent speed advances the phase; stationary/attack poses do not
apply this layer, and pause freezes it. Edit these tracked settings to reproduce
or tune the in-game action; a Blender clip-only export does not include this
runtime layer. No base model regeneration or texture re-export is required.

`AI/EnemyBodyClearance.cs` derives a one-time actor-local torso/head footprint
from the actual posed skin vertices with at least 65% torso/head bone influence.
It excludes animated distal limbs rather than shrinking the creature to fit
doorways. A trigger-only query is excluded from ray interactions and navigation
baking, and horizontal penetration corrections move the real NavMesh agent on
the same supported floor. It does not move the visual independently of AI.
`--profile readability` checks real wall overlap and door passage. Native frames
and pickup silhouette/depth comparisons use
`-quiet-diagnostics -v2-readability-output <new-output-directory>`.

Each folder includes the original GLBs, packed `<key>_Editable.blend`, body plan,
source-order surface weights, contacts, eye authoring and
reports. Uncat/Baby additionally retain precomputed game surface data. High geometry and 8K images stay in the editable source. Only the game
mesh is exported to `Assets/Resources/EnemyStudio/<key>/<key>.fbx`.
Runtime prefabs are named `<key>Runtime.prefab` to avoid the FBX resource-name
collision. Their own animation aliases and PBR maps are installed by
`HappyToy.V2.Editor.EnemyStudioGameImport.Apply`. The protected school scene is
not rewritten; existing navigation/encounter actors mount the new visual.

## Reproduce

Install Git LFS, run `git lfs pull`, and open `HappyToyV2` in Unity **6000.6.0f1**.
Use official Blender **4.0.2** and Python **3.11.7** with
`tools/requirements-authoring.txt`. Blender's bundled Python/numpy runs the
Blender tools. Commands below run from `HappyToyV2/SourceArt/CreatureStudio`.
All authoring paths are repository-relative; `.blend` images are packed.

```powershell
python -m pip install -r tools/requirements-authoring.txt
python tools/weight_body_surface.py Baby/source-original.glb Baby/body-rig-plan.json Baby/body-surface-weights.npz
python tools/weight_body_surface.py Uncat/source-original.glb Uncat/body-rig-plan.json Uncat/body-surface-weights.npz
python tools/weight_body_surface.py Cyclopse/source-original.glb Cyclopse/body-rig-plan.json Cyclopse/body-surface-weights.npz
blender --background --factory-startup --python-exit-code 1 --python tools/reduce_surface_blender.py -- --creature Baby --triangles 30000
blender --background --factory-startup --python-exit-code 1 --python tools/reduce_surface_blender.py -- --creature Uncat --triangles 30000
blender --background --factory-startup --python-exit-code 1 --python tools/author_creature.py -- --creature Baby --animation-module tools/creature_animation.py
blender --background --factory-startup --python-exit-code 1 --python tools/author_creature.py -- --creature Uncat --animation-module tools/creature_animation.py
blender --background --factory-startup --python-exit-code 1 --python tools/author_creature.py -- --creature Cyclopse --animation-module tools/creature_animation.py
blender --background --factory-startup --python-exit-code 1 --python tools/author_game_eyes.py -- --creature Baby
blender --background --factory-startup --python-exit-code 1 --python tools/author_game_eyes.py -- --creature Uncat
python tools/export_eye_masks.py Baby/eyes-authoring.json ../../Assets/Resources/EnemyStudio/Baby
python tools/export_eye_masks.py Uncat/eyes-authoring.json ../../Assets/Resources/EnemyStudio/Uncat
python tools/export_eye_masks.py Cyclopse/eyes-source-authoring.json ../../Assets/Resources/EnemyStudio/Cyclopse
```

The checked-in plans/weights are the reviewed source of truth. Optional anatomical
refitting uses `fit_baby_body.py` or `fit_round_cyclopse.py`; regenerating plans
resets later recorded posture overrides; retain/review those when experimenting.
`finish_creature.py --creature <key> --animation-only` changes clips without
rebaking textures. Run that command for each changed creature, then invoke
`HappyToy.V2.Editor.EnemyStudioGameImport.Apply` in Unity to update the exported
chase `.anim` and fitted prefab bounds. Profile overrides in `body-rig-plan.json`
apply last; a two-stride chase duration covers two complete gait cycles. The derived preview GLBs are reproducible optional outputs.
Do not use `export_pbr.py` to overwrite a reconstructed game atlas with source UV
textures. That extractor applies only when the original source UVs are retained.

## Game asset polish, 2026-10-21

`polish-settings.json` and `tools/polish_game_assets.py` finish the saved editable
models without regenerating their base geometry or animation. Original GLB/PBR,
near positions/UVs/weights, colour/normal/eye maps and all authored action keys
remain exact. Per-model `polish-report.json` records hashes and source distances.
Blender's game-only materials and Unity's metallic maps use zero metallic for
these organic/plastic creatures; the original generated PBR remains separate.

The tool derives distant copies with the existing atlas/rig, pinning rigid head,
hand and foot surfaces. It increases the retained geometry until maximum source
distance at retained rest vertices is below 25mm and P99 below 12mm, and keeps a LOD only if it reduces
triangles by at least 15%. Baby has 29,985 / 16,491 triangles; Cyclopse has
40,000 / 31,000. Uncat retains its single reviewed mesh: safe reduction was too
small to justify another skin. Unity still binds support, eye anchors and chase
expression deltas to the exact primary mesh. Distant skins receive the same
independently toggled eye emission. Chase/cabinet expression transitions hold the
unchanged near LOD; ordinary LOD selection resumes when the expression clears.

`StaticScaleCurveCleanup` drops only scale bindings invariant across every
relevant action. The complete native first pose supplies the prefab baseline.
Rotations, joint snaps, motion clocks and state-specific scale curves remain.
Baeksugwi uses the same scale cleanup while retaining its protected geometry,
original title source, 63-bone rig, eighteen arms and head-writhe action.

From the repository root, with documented Blender 4.0.2:

```powershell
blender --background --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/CreatureStudio/tools/polish_game_assets.py -- --creature Baby
# Repeat for Uncat and Cyclopse, then run the named importers in Unity.
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.CreatureGamePolishImport.Apply
python HappyToyV2/Tools/quality/run_feature_tests.py --profile model-polish --no-desktop-interaction
python HappyToyV2/Tools/quality/run_feature_tests.py --profile full --no-desktop-interaction --graphics-api d3d11
```

Rerun this finish after `finish_creature.py` changes an editable model/action.
`CreatureGamePolishImport.Apply` runs studio import, the dependent eye/expression
Ensure methods and the existing Baeksugwi importers. BuildWindows repeats its
dependency checks. The pinned
near-mesh fingerprint checks every imported position, UV, triangle and four-weight
binding against the pre-polish Unity skin. Full pose tests compare every source
FBX frame, and release checks retain the full EditMode/PlayMode suites.

The read-only `Tools/quality/model_inventory_unity.csx` audits all imported model
sources and Resources prefabs through Unity MCP. Other already-budgeted legacy
monsters, props and architecture retain their reviewed source geometry. Review
renders and raw inventories belong under ignored Builds, not source commits.

## Validation and ownership

Run `verify_source_binding.py` for original source correspondence and seam-safe
weights, then `audit_creature_poses.py` for actual posed skin. Numeric binding
checks alone do not certify a visible pose. Unity import validates actual mesh
budgets, clips, eye surfaces and full posed support. PlayMode covers actual
corridor/school actors plus F1/F2 and checkpoint behavior. Native verification
uses `-quiet-diagnostics -v2-creature-studio-output <directory>`; it must remain
silent. It is a controlled rendering fixture and does not certify a full survival
playthrough or all hardware performance.

The references, generated geometry/PBR and authoring tools are project sources.
User-supplied Cyclopse reference rights remain with their owner. Do not describe
those references as CC0. Provider license snapshots and pinned source links are retained in
`licenses/` and `license-provenance.json`; see `LICENSES.md`. Generator setup is
documented in the repository's `hunyuan3d-webapp` sources. Blender
and Python dependencies are installed tools, not bundled model weights or caches.
Before a handoff push run `python HappyToyV2/Tools/quality/check_source_completeness.py`
from the repository root after staging all required sources and Unity `.meta` files.


## Baby game-eye emission repair, 2026-10-15

`Assets/Editor/BabyEyeMaskImport.Apply` is the final Baby game-atlas eye baker.
The original `eyes-authoring.json` remains the source-landmark input; its older
single-triangle iris clipping left only 6/13 white texels. The new baker poses
the actual pinned Unity FBX/patrol skin, clips each visible iris ellipse against
all original game UV triangles, handles seams independently and leaves the
central pupil black. It adds no eye geometry, light or overlay. Boundary samples
come from the visible surface patches and sit slightly inside their source
triangles so float serialization cannot strand them outside a tiny UV chart.

`Baby/eyes-surface-mask.json` records source/recipe and output hashes, canonical
centres, radii, selected triangle counts and coverage. The reproducible outputs
are `eyes-surface-emission.png` and `eyes-surface-profile.json`, copied into the
existing Unity eye assets while retaining their GUIDs. BuildWindows calls Ensure
after the base studio importer. Models, original UVs, rig, clips and Blender
sources remain unchanged. The native cabinet expression audit renders matching
eye-emission on/off portraits and requires visible red differences for each eye
during chase, calm and cabinet rush; optional taunt mode verifies the third-entry
capture with safe RNG and same-frame patrol re-entry.
