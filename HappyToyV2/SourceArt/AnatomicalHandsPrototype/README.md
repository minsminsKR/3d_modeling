# Anatomical hand prototype (incomplete)

The reviewed horror-game and user-photo grip references, current visual
comparison, and item-specific acceptance targets are in
`GRIP_REFERENCE_REVIEW.md`.

The 2026-11-10 user-supplied hand photo and the local, manually aligned
joint-crease texture pass are recorded in `ReferenceWrinkles/README.md`.
This is a skin-detail adjustment, not a new skeleton or full-scene approval.

2026-11-10: the runtime atlas includes baked short nail detail using a CC0
source. Nail sources, reproducible bake and verification scope are in
`NailDetail/README.md`; the subsequent local ring correction is described below.

## Thumb placement and ring joint corrective — 2026-11-10

The thumb nail patch was moved toward the tip and reduced in width and length;
`NailDetail/bake_nails.py` and the shipped atlas are the reproducible sources.
In the subsequent user-directed adjustment its center was shifted left in the
first-person view and assessed on the neutral extended hand from dorsal,
oblique and side viewpoints, then in the Unity hand-camera still. The adjusted
2K atlas is installed with the unchanged ring-corrective geometry.
The existing hand is still the base: `generate_ring_corrective.py` derives a
single 60-degree ring metacarpophalangeal bend corrective from Blender's dual
quaternion/linear skinning difference, back-projected into bind space. The
editable result is `hand-rig-corrective.blend`; `export_candidate.py` exports
the FBX and `install_candidate.py` installs it with the revised skin. Rebuilding
both editable versions produced the same vertex/weight/shape data hash
`56a60c68a4f5a7053acaefc99053dcd803a205c4f90e25ae86e98626b36c3c5e`;
FBX container bytes can differ between exports. The existing bind, item poses
and action markers remain in use.

`HandAnimationRig` drives the corrective smoothly from ring curl, including
contact adjustments and pose restoration, on both mirrored and non-mirrored
hands. The Unity skinned-mesh test compares the same static talisman pose with
and without the corrective: triangles below 25% of neutral area decrease
from 30 to 11 on each hand. This is a deformation metric, not a collision or
visual-quality guarantee for all poses. The revised `HandGripSurfaceAudit`
includes active blendshape deltas before applying bone matrices; with the
corrective enabled and disabled the static flashlight's cylindrical
approximation reports 59 vertices deeper than 3 mm and a 6.38 mm maximum.
It does not establish zero penetration, other items or animated frames.

The candidate passed `hand-animation-logic` EditMode 113/113 and PlayMode
12/12, and `player-hands` D3D11 EditMode 104/104 and PlayMode 4/4. The latter
ran with the same joint correction and an earlier nearby thumb position; the
final atlas was reviewed again in the Unity hand-camera stills under local
`Builds/HandJointThumbStudioAdjusted-20261110/`. These are isolated hand-layer
captures, not full-scene player or FPS validation. The ring corrective is a
localized improvement; other fingers, palm and wrist, realistic skin and
item contact still need art review and possible further correction.

This candidate is installed in the game. Its appearance and grip quality have remaining issues described below. The base is Blender Studio Human Base Meshes v1.4.1, CC0, https://www.blender.org/download/demo-files/ . The unmodified archive SHA-256 is 811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515. Blender 4.2.23 LTS is required: https://download.blender.org/release/Blender4.2/ .

The editable v4 draft appends the Blender hand, approximates alignment to the existing rig, extends its wrist toward the elbow, adjusts the little finger and transfers initial weights, then creates a new UV layout. It does not have a valid finished texture for the new UV, or certified forearm anatomy, deformation, grip contacts, or Unity integration. The existing generated texture transfer has 116 zero-area UV faces; v4 smart UV has none, but needs a new texture. Pose audits show some old fingertip markers are up to 23.5 mm from the new surface. These are geometry signals, not proof of contact or visual quality. Those v4 observations are historical; the later v7 runtime candidate is described below.

To reproduce from the repository root, extract the original ZIP alongside these scripts into `base/`, then run Blender 4.2.23 in background with `--python-exit-code 1 --python` for prototype.py and each of prototype_v2.py, prototype_v3.py, prototype_v4.py in that order. The scripts output intermediate blends and audit files in this folder; those intermediates and the extracted base are optional. The tracked v4 blend is the draft result. The existing `PlayerHandAnimation/hand-animation.blend` is an additional required input. Use hidden BelowNormal two-thread execution. `audit_pose.py` examines pose markers but is not a visual or contact test.

Remaining: fit every finger chain to the new surface; correct pose and grip contact across all items; validate continuous forearm shape and UV; author and bake appropriate skin and nail maps; integrate and verify the real Unity prefab, runtime grips and animation, and game-camera appearance under the desktop/GPU restrictions. No visual or gameplay approval is asserted.

## Snapper palm-to-flick action — 2026-11-10

The snapper now rests on a cupped, palm-up right hand. The UseSnapper action
closes the thumb and index around it, then extends the index at the existing
0.32-second release marker and returns to the palm pose. The presentation
interpolates the pellet from the animated palm surface to the pinch and then
the index pad; the launch predictor evaluates the same release pose and prop
frame. Existing inventory consumption, cancel, pause and throw ownership remain
unchanged. The catalog uses its distinct PalmToFlick grip and Palm idle anchor.

`snapper_palm_action.py` authors the bounded finger samples in the retargeting
pipeline; rerun `retarget_candidate.py` to regenerate the tracked candidate
JSON and copy it to `Assets/Resources/PlayerHandAnimation/poses.json`. The
new test exercises support, pickup, finger extension and marker prediction in
the CPU gameplay fixture. Static palm contact uses the prop bounds and palm
marker, not a full hand/prop triangle collision solver.
The final CPU `hand-animation-logic` passed 113/113 EditMode and 13/13 PlayMode.
Unity D3D11 `player-hands` passed 104/104 EditMode and 4/4 PlayMode after
the final socket adjustment. A real gameplay fixture produced separate world
and hand camera layers for the idle pose. The screenshot composite omits HUD;
standalone player presentation and device performance were not verified.

## 2026-11-09 v5/v6 exploration

`prototype_v5.py` fits tentative finger chains to manually estimated neutral surface landmarks and replaces digit weights with approximate bounded longitudinal assignments. The five distal groups each have 166–180 vertices above 0.6 weight, with no unweighted vertices and at most three influences. These counts do not demonstrate anatomically correct bending or finger contact. New bone bind frames differ from the game library, so the existing JSON cannot safely be shipped without retargeting and grip correction. `audit_pose_v5.py` forces frame reevaluation; nearest surface measurements alone do not detect penetrations and the identical distances across poses should not be treated as proof of working actions.

`prototype_v6_skin.py` performs a small CPU emission bake into the new UV using the existing generated front/back reference projections from `PlayerHandsV2/Derived`, and packs the draft map in v6. The 1024px draft map visibly contains flat fingertips and abrupt forearm texture transitions. It is not approved or scan data. The prior rejected hand geometry is not used; these references were generated and retained by the project. The v6 candidate was subsequently installed for runtime evaluation.

To reproduce after v4: run `prototype_v5.py` then `prototype_v6_skin.py` under the same background Blender settings. The original 22 actions exist in the draft blend but are not yet retargeted to the new bind frames. Nail material, anatomical weight painting, texture seams and actual item contacts require further work, followed by relevant Unity tests and a permitted game-camera visual review.

## Runtime evaluation — 2026-11-09

The candidate geometry and 1024px draft skin are installed in the default hand prefab for evaluation. `export_candidate.py` exports neutral geometry from v6. `retarget_candidate.py` combines original authored clip samples with the separately recorded new rest rotations and grip deltas, interpolating corrections through equip/unequip/use while preserving gameplay markers; `install_candidate.py` copies those outputs and creates flat roughness and normal maps. Run the Unity `PlayerItemsImport.ApplyHand` editor method afterward. The new hand geometry uses the Blender Studio CC0 base; the skin map uses the existing generated PlayerHandsV2 photographic-reference projections, not a human scan.

`hand-animation-logic` passed EditMode 107/107 and PlayMode 11/11 after grip retargeting; an earlier failure in hand-barrel contact was corrected. The checks are CPU/headless and do not certify full surface contact, silhouette, skin realism, nails, animation continuity, or FPS. The forearm has a simplified extrusion, skin has visible flat fingertip regions and forearm seams, the 1024px map is a draft, and nails and game-camera visual review remain outstanding. No GPU/player visual review has been performed under the desktop isolation policy.

## GPU hand-layer review and repairs — 2026-11-09

The first Unity D3D11 stills showed severe black fragmentation across both hands. The FBX exported `TransferredOldUV` as UV0 even though the skin was baked to `NewSkinUV`. `export_candidate.py` now removes the obsolete layer and exports only the baked UV. The hand atlas coverage regression rejects the old result: on the old FBX approximately 51% of UV corners sampled black, compared with 0% for the repaired FBX in the source-side check. The GPU hand-layer preview confirmed the broad black patches disappeared.

The 2048px v7 skin projection adjusts reference alignment and extends the projection along the forearm; it is not a scanned texture. Snapper moves 1cm toward the hand camera to avoid being completely occluded, and the predicted launch point uses the same offset. GPU preview showed the green pellet between the fingertips. Prior static-frame flashlight curl experiments were discarded because they did not convincingly improve grip.

`hand-animation-logic`: EditMode 108/108, PlayMode 11/11. D3D11 `player-hands`: EditMode 103/103, PlayMode 4/4. A separate D3D11 play-mode hand-camera evidence run also passed and recorded idle plus sampled actions for flashlight, incense, talisman, snapper and trap at 16:9. Evidence is under local `Builds/HandGpuReview-20261109/`; these are hand-layer images, not a full scene composite, native player capture, or performance measurement. The atlas guard is in HandAnimationAssetTests. `HAPPYTOY_HAND_GPU_REVIEW_DIR` opt-in writes review PNGs from the actual gameplay hand camera.

Remaining: flashlight grip still shows awkward dangling/flattened fingers, full mesh-to-prop intersections have not been certified, nail detail and realistic skin remain limited, and the trap conceals most of the supporting hand. These are not high-quality art approval. No GPU timing or displayed FPS measurement was made.

## Lower-corner talisman grip — 2026-11-09

The user reference is `References/talisman-grip-user-20261109.jpg` (SHA-256 `6e27a130380367f2c0f251686e74f64cf400b1cdafe12e9d80cf51146ea41acb`). The paper is positioned at its lower screen-right corner between the thumb and index, while the other three fingers use a more curled pose. The sheet extends upward and left. Paper deformation is pinned at the held bottom edge; the flame and smoke still advance from the top toward the bottom. `HeldPaperEffects.BendOffset` matches the new shader flex direction. This approximates the supplied photo but does not establish a photorealistic full-surface match.

`hand-animation-logic` passed EditMode 109/109 and PlayMode 11/11; D3D11 `player-hands` passed EditMode 103/103 and PlayMode 4/4, with an actual runtime hand-camera image and item actions captured under `Builds/HandBottomGrip-20261109/`. The first new test attempt failed to compile due to a test-assembly direct reference; it was changed to reflection before the successful run. These checks do not certify every triangle intersection, actual full-scene appearance or device FPS.

A nail-material face patch was rendered and discarded because it appeared as dark blotches. A localized skin-weight smoothing trial changed faces below 25% original area from 20 to 22 in the authored flashlight pose and was not applied. Flashlight finger shape, nails, realistic skin, and all-surface penetration quality remain unresolved; no high-quality art approval is claimed.

## Grip catalog and surface-aware flashlight candidate — 2026-11-09

The live viewmodel instantiates one generic FBX hand twice. The left instance mirrors the root with a negative X scale; both use the same named 24-bone hierarchy: forearm, wrist, palm, 15 deforming digit bones, five nondeforming fingertip markers and a palm marker. Runtime samples 22 authored pose/action channels onto actual imported bind rotations rather than driving a Mecanim Avatar or an Animator IK layer. Only forearm and wrist are present; there is no elbow/upper-arm chain for a meaningful Two Bone IK setup. Adding Animation Rigging alone would not correct the skin topology or object penetration. The existing bounded digit CCD is retained for small contact adjustments.

The v5 Blender hand uses approximate axis-based digit assignment and linear weights; that surface and its inherited generated skin are not a verified anatomical scan. Old runtime grips optimized fingertip marker distance but did not inspect the actual skin surface. `Assets/Editor/HandGripSurfaceAudit.cs` is an opt-in batch, no-graphics auditor and bounded offline coordinate search against the actual weighted skin and a measured cylindrical barrel approximation. On this static pose, skin vertices more than 3mm inside the approximation decreased from 337 to 59; maximum depth decreased from 12.89mm to 6.38mm. The intermediate search result passed the regression only after a small ring-finger adjustment. The final numbers and hashes are recorded in `flashlight-surface-review.json`. This is not a triangle-intersection test and does not certify zero penetration, other items, movement frames, or visually natural skin.

The applied offsets are in `grip-deltas.json`, and `retarget_candidate.py` rebuilds pose/clip JSON. Equip, unequip and use clip endpoints match each item's grip. Held flashlight, incense and talisman retain their correction at the use marker; snapper and trap are allowed to open at release.

`Assets/Resources/PlayerHandAnimation/GripCatalog.asset` is the editable ScriptableObject catalog, with five entries for grip type, supported hand/anchor, finger pose, socket position, wrist and prop rotation, local anchor offset, scale and transition duration. It drives the live presenter; invalid or unsupported hand/anchor assignments fail validation. Snapper release prediction uses its release-pose prop frame and the same local offset. Timing and stock effects remain owned by the existing gameplay components. The catalog is required by the build importer. The user's lower-corner talisman reference remains the target.

Verification: `hand-animation-logic` EditMode 112/112 and PlayMode 12/12; D3D11 `player-hands` EditMode 104/104 and PlayMode 4/4. Captures in `Builds/HandRigAudit-20261109/gameplay-complete/` are the real play-mode hand layer at sampled times and aspect ratios, not an entire game frame or FPS measurement. Compared with the prior view, the isolated flashlight frame has fewer obviously dangling digits. Nails, skin quality, wrist/forearm anatomy, exhaustive prop/hand and self intersections, and full game-camera visual and performance validation are still outstanding. The requested complete high-quality system is not certified.
