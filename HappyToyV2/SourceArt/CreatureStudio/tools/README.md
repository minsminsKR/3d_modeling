# New creature authoring tools

These tools consume newly generated Uncat, Cyclopse and Baby GLBs. They never load
or repaint `EnemyRefinementV2` geometry. Original generated GLBs and their embedded
8K PBR images remain editable inputs under the corresponding creature folder.

Pinned tools: Blender 4.0.2; standalone Python 3.11.3, numpy 1.26.4,
Pillow 10.4.0 and scipy 1.14.1. All
paths below are relative to `HappyToyV2/SourceArt/CreatureStudio` in the Unity project.
CLI examples run with that folder as the current directory.

1. Inspect a new source with `python tools/inspect_rig.py Uncat/source-rigged.glb --output Uncat/rig-inspection.json`.
2. Review its actual joint positions and make `Uncat/joint-map.json`. Format:
   `{"version":1,"target_height_m":2.2,"map":{"Hips":"sourceBoneName","Spine":"..."}}`.
   Keys are canonical Mixamo suffixes. Required keys are Hips, Spine, Spine1, Head,
   both Arm/ForeArm/Hand, and both UpLeg/Leg/Foot. Neck, Spine2, shoulders, toes and
   fingers should be included when those real source joints exist. Unknown joints
   remain Aux bones; a missing anatomical chain fails instead of guessing a body.
3. `blender --background --factory-startup --python tools/author_creature.py -- --creature Uncat --animation-module tools/creature_animation.py`
   renames the reviewed source rig, normalizes the rest floor and scale, creates a
   game-only 60k-triangle LOD, bakes actual high-resolution geometry tangent normals,
   and invokes `apply_creature_animation(rig, creature, contact_specs)` from the
   supplied motion module. Blender coordinates are Z-up, front -Y.
4. `python tools/export_pbr.py Uncat/source-original.glb ../../Assets/Resources/EnemyStudio/Uncat --size 2048`
   creates a downsampled game albedo and converts glTF roughness/metalness into URP
   metallic R / smoothness A. It does not invent a normal map from albedo brightness.
5. Review actual new iris surfaces/UVs and save `Uncat/eyes-authoring.json` with the
   runtime v2 fields and optional `blackCutouts` UV polygons for pupils/eyelids.
   `python tools/export_eye_masks.py Uncat/eyes-authoring.json ../../Assets/Resources/EnemyStudio/Uncat`
   outputs the white iris mask and profile using bottom-left Blender/Unity UVs.

The `.blend` retains the high-resolution original and game LOD. Only the LOD and
armature enter `Assets/Resources/EnemyStudio/{key}/{key}.fbx`. Required baked action
names are `patrol`, `chase`, and Baby `cry`; an incomplete inspection run can use
`--skip-export` but cannot be reported as a complete game asset. `--skip-normal`
also prevents a game export. FBX uses `axis_forward='-Z'`, `axis_up='Y'`, unit scale
and no leaf bones. Unity's importer must verify the actual front direction and
vertex count; the authoring estimate rejects more than 80k split vertices.

Preview renders belong outside source commits:
`blender --background --python tools/preview_geometry.py -- Uncat/source-original.glb /your/work/Uncat-preview`.
This entry point renders the original generated geometry without saving changes.

The AI skin and manual joint mapping still require inspection after download.
Renaming nodes cannot repair misplaced joints or semantic skin ownership. If a
source has incorrect anatomy/weights, the pipeline instead consumes the original
new geometry plus `body-rig-plan.json` and `body-surface-weights.npz`. The explicit
plan is generated with `fit_body_plan.py` and inspected against the source surface;
`weight_body_surface.py` binds its 22 semantic bones through a voxel geodesic graph,
with rigid head and real palm/finger ownership. Original vertex positions, faces,
UVs and 8K texture data are retained. These source-order weights and the JSON plan
rebuild the corrected editable rig without another full-size corrected GLB.

All three creatures use this reviewed body plan. Uncat and Cyclopse generated AI
joints and hand/foot ownership did not match the actual surface. Baby's reviewed
52-bone AI joint positions supply its 22-bone plan, while its original skin was
rejected after actual crawling revealed split skin seams and stretched clothing.
The raw AI GLB and `rig-inspection.json`
remain provenance for each creature. The final rest heights are Uncat 2.2 m,
Cyclopse 2.38 m and Baby 1.48 m. Plan `contact_overrides` and
`animation_profile_overrides` record actual-source skin grounding fits and are
passed to the animation module; the generated `contact-specs.json` also keeps the
game LOD's measured surface evidence.
