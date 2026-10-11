# Creature animation authoring

Blender 4.0.2, using its bundled Python and mathutils; no pip dependencies or
audible tests are needed. Import `apply_creature_animation` from
`creature_animation.py` in the model authoring pipeline.

```python
report = apply_creature_animation(
    rig, 'Baby', contact_specs,
    fps=60, ground_height=0,
    report_path=str(asset_folder / 'animation-report.json'))
```

The corrected rig must be upright in its rest/bind pose, in Blender Z-up and
facing -Y. Object transforms should be identity. Existing mesh geometry,
materials, bind poses and weights are never changed by this module.

Required semantic bones, with namespace retained: `mixamorig:Hips`, `Head`,
`Left/RightArm`, `ForeArm`, `Hand`, `UpLeg`, `Leg`, `Foot`. Shoulder, Spine,
Spine1, Spine2, Neck, ToeBase, fingers and head accessories are retained and
keyed when present. Limb bone heads must be the anatomical shoulder/elbow/
wrist or hip/knee/ankle joints. Segment lengths derive from actual head-to-head
distances; imported tail heuristics are not used as anatomical lengths.

All actions are in place: `idle`, `patrol`, `chase`, plus Baby `cry`. Source
generators should export each monster separately to retain these exact take
names. NavMesh movement controls actual travel in the game.

- Uncat uses a low, forward-leaning four-point posture with hands and rear feet;
  the chase shortens support time and adds strong alternating arm recovery.
- Baby supports its palms and knees, with trailing feet above the ground. Cry
  keeps the support stance and animates head/shoulder/body shudder silently.
- Cyclopse walks upright on its feet, with heavy arm swing and a faster,
  slightly lowered chase posture.

`contact_specs` is a dictionary keyed `left_hand`, `right_hand`, `left_foot`,
`right_foot`, optionally `left_knee`, `right_knee`. Entries use armature-local
Blender coordinates:

- `clearance`: exact desired wrist/ankle/knee height above the support floor.
- `contact_point`: measured rest palm/sole surface point; clearance is derived
  after the support bone's rotation, so the point must lie on the actual palm
  surface rather than just be the lowest dangling fingertip of an A-pose.
- `palm_normal` and `finger_direction`: measured rest palm/finger directions;
  crawling aligns fingers to -Y and palm normal to -Z.
- `rotation`: explicit desired support quaternion `(w,x,y,z)` if known.
- `anchor`: optional XY support placement; Z remains floor plus clearance.

If contacts are absent, conservative model-scaled defaults permit skeleton
testing but do **not** certify actual surface grounding. Evaluate real weighted
skin after fitting, particularly palms, doll knees, broad feet, belly, ceramic
head and Uncat tongue. Tongue/face/hat/flower must have consistent rigid Head
weights; the animation module does not split or reshape them.

The return report includes each action's cycle, pelvis height, projection/reach
correction per limb, constant-length error, matrix scale error and loop seam.
Actual-model fitting can pass `pelvis_height` in floor-relative model units,
or `profile_overrides={'chase': {'pitch': 0.9, 'pelvis_height': 0.4,
'stride': 0.35, 'head_pitch': -0.1}}` without changing the generator. Store these
values in the per-model source plan after reviewing its weighted skin.
`spine_bend` optionally flexes the upper back at Spine (70%) and Spine1 (30%)
while retaining every joint length; `neck_bend` changes the neck separately.
This supports a large belly and short arms without forcing the entire abdomen
down to reach the ground. Head orientation remains independently stable.
`verify_animation_contract.py` exercises all three profiles on a disposable
synthetic Mixamo skeleton; that report is separate from real model validation.

`audit_creature_poses.py --blend <editable.blend> --output <diagnostics>` opens
an authored source without saving it. It evaluates actual game skin at four
cycle phases and the seam, measures floor penetration per hand/foot/head/body
region, records edge stretch and head facing, and checks the skin loop. Add
`--render` for silent 3/4 and side previews of patrol/chase (or `--actions cry`
for the infant's stationary event). Its JSON and images are review evidence;
contact and body overlap must still be inspected on the actual model.

`fit_motion_profile.py --plan <body-rig-plan.json> --surface
<body-surface-weights.npz> --output <report.json>` creates a disposable rig and
computes linear skin deformation from the actual recorded source vertices and
weights. It searches body pitch, pelvis height and optional `--spine-bends`.
Use `--sample-step 1` for complete-surface confirmation of a selected candidate.
No model, palette, weights or source plan is changed. Its contact outputs are
an initial source fit; the authored game LOD and visible surface still need the
pose audit before delivery.
