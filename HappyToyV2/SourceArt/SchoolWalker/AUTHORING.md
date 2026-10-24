# School flower man authoring

The base character and full-resolution PBR come from the designated A100 app,
using `reference-v2-purple-flower.png`. Preserve `original.glb`, the exact
generation request/record, both actual reference inputs and job provenance.
Auto-rigging uses the same app job. Preserve the collected `rigged-original.glb`,
its rigging request/result and any joint-editor records separately from exports.
The local tools never generate a replacement person or modify either original.

Use pinned Blender 4.0.2 and its bundled Python/NumPy. These tools run through
the Blender CLI. Unity asset reconstruction uses `SchoolWalkerImport.Apply`
through the existing Unity 6000.6.0f1 CLI/editor pipeline.
Install the official Blender 4.0.2 distribution separately. The command examples
use an optional `Builds/AuthoringTools` cache path; substitute your installed
4.0.2 executable when it lives elsewhere. All character inputs and authoring
tools are in tracked source folders.

CLI collection and `validate_character.py` were executed with Python 3.11.3.
The lightweight source validator needs Pillow 10.4.0; install
`python -m pip install -r HappyToyV2/SourceArt/SchoolWalker/requirements-validation.txt`.
Blender tools use Blender's own bundled Python/NumPy, not that system environment.
The designated modeling CLI setup and login recovery remain documented in
`hunyuan3d-webapp/CLI-A100.md`; authentication state is never part of this handoff.

## Inspect and record actual anatomy

```powershell
& HappyToyV2/Builds/AuthoringTools/blender-4.0.2-windows-x64/blender.exe -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolWalker/inspect_character.py -- --input rigged-original.glb --render
```

Inspection outputs go under `Builds/SchoolWalkerReview/source`. Review the
actual silhouette, face, ear flower, arm/torso separation, soles and AI skin.
Set `rig-plan.json` using the actual source bone names: `Hips`, `Spine`, `Head`
and both `Left`/`Right` `Arm`, `ForeArm`, `Hand`, `UpLeg`, `Leg`, `Foot`.
Optional `Chest` or `Spine1` supplies a small upper-body counterrotation.
`rotationZDegrees` must establish Blender front -Y; standard FBX conversion
then produces canonical Unity front +Z. `rigidHeadAboveMetres`, when needed,
is a measured normalized skull plane: the game-copy skull, hair and flower
follow the reviewed head bone. The original AI skin remains untouched.

Optional `voxelMetres` is zero unless an actual inspected generated topology
requires source-derived surface repair. Review face and flower preservation
before accepting a nonzero setting. A missed triangle budget fails explicitly.
This character's actual first collapse showed face/neck/cloth triangle cracks;
its final reviewed plan therefore requires a source-derived 2mm voxel surface
before LOD collapse. Actual AI motion also showed palm/finger branch wedges.
`handWeightRepair` keeps the game-copy relaxed palms/fingers rigid to the hand
and blends the measured 45mm wrist region into the forearm. Originals stay intact.

## Finish and export

```powershell
& HappyToyV2/Builds/AuthoringTools/blender-4.0.2-windows-x64/blender.exe -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolWalker/finish_character.py -- --plan rig-plan.json
```

The finisher normalizes the actual person to 1.77m with sole height zero. It
derives bounded 50K/12K/4K skinned LODs, transfers original source-triangle AI
weights with at most four influences, and bakes source colour/ORM and high-to-low
tangent normals into a coherent 4K hero near atlas. The middle LOD shares its material;
the far LOD uses 1K copies of those images and the same atlas/bone hierarchy.
Original geometry, UV, skin and bone-rest snapshots must match after finishing.

`heavy_walk.py` authors real bone curves with measured sole clearances,
constant-length limb IK, stance and swing phases, heel/toe roll, modest hip
weight transfer and relaxed counter-swing. This is authored animation, not
motion capture or an AI motion claim. A standing idle allows the runtime to
blend out of locomotion. Both clips are in place; runtime owns world travel.
Read the actual `walk.worldTravelPerCycleMetres` and duration from `export.json`
instead of assuming a stride. Defaults are 1.0m/s and 1.30s/cycle, or 1.30m.
The actual person's shorter measured leg chains required an excessive crouch at
that default travel. The reviewed `rig-plan.json` instead uses 0.70m/s and a
1.30s cycle: exactly 0.91m of world travel, with a 60% stance fraction. Hip height
is lowest at double support and weight shifts toward the single-support leg.
Near geometry uses source-derived face/flower and hand density priority. The
50K/4K hero budget was selected after actual 25K/2K closeups lost facial detail.
`review_character.py --render` reopens the packed source, checks the actual
three skinned LODs for closed manifold topology, samples both complete clips,
checks <=8mm penetration and <=18mm stance gap, and renders cardinal gait/face
views under neutral AgX lighting. The images and measured report stay in Builds.

Outputs are `SchoolWalker.blend`, `export.json`, and the game folder
`Assets/Resources/SchoolWalker`: `walker.fbx` (three skins plus walk), `idle.fbx`
(same rig plus idle), game maps and `asset.json`. The packed Blender source
retains the full-resolution actual AI original, editable game skin, both actions,
source snapshots and authoring scripts. Shared bake dependencies remain tracked
under `SourceArt/SchoolRenewal/finish_asset.py`,
`SourceArt/SchoolLoop/rebake_music_lod.py` and
`SourceArt/CorridorEntrance/finish_entrance_assets.py`; analytic IK primitives
come from `SourceArt/CreatureStudio/tools/creature_animation.py`.

`SchoolWalkerImport.Apply` creates `walker-visual.prefab`, `walk.anim`,
`idle.anim`, shared URP PBR materials and three bounded animated LODs. It adds
no physics, navigation or actor logic. Source checks and authoring exports do
not establish final game appearance: inspect mounted identity/flower, gait,
sole contact, pause/idle blend and corridor passage through the normal game
camera before delivery.

Runtime update, 2026-10-24: the hall pass now keeps its starting lane instead
of sidestepping around the player. A player obstructing the next step makes
the man stand; clearing the lane resumes the same walk. The masked connector
still provides his physical exit. Older saved passing lanes retain their exact
position and finish on that lane without a new lateral avoidance maneuver.

`SchoolWalkerFootsteps` reuses the five existing `step-stone` recordings from
`ExternalAudio`, already attributed in the shipped Audio-Credits.txt. It owns
runtime copies only. Each 0.91m authored cycle produces two shoe contacts from
real travelled distance, with a 3m near distance, 38m linear rolloff, and existing
physical wall attenuation. Blocking, pause and talisman stop contacts; restore
and loop resets synchronize distance without replaying missed footsteps. No new
audio asset, generated character, shared audio modification or enemy sensing is
introduced. `SchoolWalkerAudioTests` checks the actual quiet listener mixer,
stop/resume, pause, talisman, master mute, range and owned-clip teardown.
