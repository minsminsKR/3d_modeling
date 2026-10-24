# Recurring school pedestrian

`reference-v2-purple-flower.png` is the current modeling input, adding the user's
requested small purple flower above the subject's right ear (image left), as in
the original photograph. `reference-v2-settings.json` retains the exact localized
edit prompt and both reference filenames. The first version remains unchanged.

`reference-v1.png` is the initial single-person image-to-3D input created with the built-in
image generation tool from the user's `face-reference.jpg`. The exact prompt and
assumptions are preserved in `reference-settings.json`. The face reference is
copied unchanged; the source attachment remains in place.

Target body specification: 1.77m tall and 120kg, with a realistic heavy build.
The input uses a front-facing relaxed A-pose, separated hands/limbs and neutral
studio lighting for later reconstruction and rigging. The navy T-shirt follows
the user photo; charcoal trousers and plain shoes are authoring assumptions.

The actual base was generated through the designated A100 modeling CLI, request
`b8396dba-7db3-4a61-8c64-f198cba664b4`, job
`ba4c614f-570d-4433-b6ae-950ebaefadfe`. `generation-request.json` records the
TRELLIS2 1536 cascade / 8K / 12-step / seed 24102420 settings. `original.glb`
retains the full-resolution generated PBR, without replacing it with a stock person.

AI rigging used the same web job, version
`51fa4f72-d3c6-475c-bf13-4599c16eb4d3`: 34 joints and an original fully weighted
697,095-vertex body. `rigged-original.glb` and `service/rigged` retain its original
skin, analysis model, motion presets and generation records. `collect_sources.py`
reproduces the source handoff from verified CLI ZIP receipts; private login state
and redundant web previews are excluded. `provenance.json` records exact hashes.

`web-review.json` records actual interactive front/rear/side, joint and arm-motion
inspection in the existing sidebar webapp. `AUTHORING.md` documents pinned Blender
finishing, the measured 1.77m height, editable source, generated-surface LODs and
authored grounded walking/idle clips. The original user photo and both original
generated models stay unchanged. The 120kg value is a visual body specification;
this harmless pedestrian has no simulated rigidbody mass.

The runtime owns one person in the central corridor, approaching once per real
forward or backward loop traversal. He smoothly passes the player and waits beyond
the opaque entry corner. A new traversal repositions him only when both his old
body and new spawn are physically hidden from the player's camera. Looking around
does not restart him. Walking follows actual distance; pause and talisman time stop
hold his pose. Optional `loop.walker` progress preserves the exact current pass and
gait without invalidating older v3 school saves.

Run the `school-walker` feature profile and build the Windows player using
`Tools/quality/run_feature_tests.py` and `Editor/QualityValidation.BuildWindows`.
Opt-in normal-camera captures use `HAPPYTOY_SCHOOL_WALKER_REVIEW_DIR` under `Builds`.
Editor camera/animation checks do not claim a measured native-player frame rate.

Origin and usage terms: `LICENSE.md` and `TRELLIS.2-LICENSE.txt`. All required model,
PBR, rig/animation and editable authoring sources travel with this folder and the
Unity `Assets/Resources/SchoolWalker` assets and metadata. Large originals and
editable Blender/FBX files use Git LFS; hydrate LFS before rebuilding after a clone.
