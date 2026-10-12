# Baeksugwi creature rig and sprint

Editable authoring sources for the user-supplied TRELLIS.2 creature generated
in A100 Studio job `a2b07007-da21-466d-b147-0ccf19be6669`.

The original GLB and the web editor's AI rig are preserved separately. The new
creature rig has 63 bones: a torso/head chain and eighteen asymmetric arms.
Twelve main arms use a grounded, alternating sprint, and six smaller arms move
above the floor. The mask and distal hands receive rigid ownership. This is a
generic creature rig; it does not use a human Mixamo retarget.

## Sources and outputs

- `source-original.glb`: original supplied mesh, UVs and PBR textures.
- `source-ai-rig.glb`: previous web rig retained as a diagnosis reference.
- `fitted-limbs.json`: reviewed limb landmarks in glTF Y-up coordinates.
- `skeleton-plan.json`: complete bone hierarchy and gait parameters.
- `surface-weights.npz`: reproducible vertex skinning data.
- `hair-lock-mask.npy`: reviewed hair/neck-decoration ownership in the original
  vertex order; `hair-lock-provenance.json` records its source and selection.
- `decoration-lock-mask.npy`: complete hanging bell components assigned to the
  head; `decoration-lock-provenance.json` documents this reviewed selection.
- `Baeksugwi_Run.blend`: editable Blender scene, rig, packed textures and clips.
- `Baeksugwi_Run.glb`: full textured model and baked run clips.
- `previews/Baeksugwi_Run_Preview.glb`: derived 2K textures for the browser viewer;
  geometry, skin and animation are identical to the full model.

The original AI skin contained stale joint indices after the edited skeleton
was reordered. The new skin is computed against the final hierarchy using
surface-path distances, consistent UV-seam weights and rigid distal ownership.
The supplied model fuses dark hair/decoration with the front arm surface.
Reviewed hair ownership follows the Head bone to prevent these strands being
pulled into long stretched strips. Independent black wrist bands stay on arms.
Hanging metal bells follow the head as whole components so the face bounding
region cannot split one bell between the head and a moving arm.
Blender's Preserve Volume is disabled so deformation QA uses linear skinning,
as glTF does.

## Regenerate

Pinned authoring runtime: Blender 4.0.2. Weight authoring uses Python 3.11,
numpy 1.26.4, scipy 1.14.1 and Pillow 10.4.0. The supplied original model is
required and included. No external workstation path is required by the scripts.

From this directory:

```powershell
python -m pip install -r requirements-weight.txt
python make_plan.py
python weight_surface.py source-original.glb skeleton-plan.json surface-weights.npz
blender --background --python author_blender.py -- --skip-preview
python make_preview_glb.py Baeksugwi_Run.glb previews/Baeksugwi_Run_Preview.glb
python preview_server.py --port 8061
```

Run clips are `Run_InPlace` and `Run_Forward`. The source plan uses a 0.72 s
cycle at 60 fps (43 intervals, 0.7167 s). `Run_Forward` includes forward root
translation; `Run_InPlace` is appropriate for a game actor driven by navigation.
The plan declares the model's floor and body height offset. Import with a
generic skeleton and preserve its authored rest matrices and joint order.

The preview is local and silent. It reuses this repository's pinned Three.js
vendor files; see `README-preview.md`. The full original-quality GLB remains
available independently of the lighter preview texture copy.

## Provenance

Reference: the user's supplied image, “폐허의 복도를 덮치는 백수귀.png”.
Original geometry/textures: the user's A100 Studio TRELLIS.2 generation.
Rig, weights and sprint: authored for this request, with the included scripts.
No third-party character, animation or audio is included. This record does not
assert a separate license for the supplied reference image.

The Unity game uses this creature through `MaskHorrorVisual` and the generated
`Assets/Resources/Baeksugwi/Baeksugwi.prefab`. The original navigation actor,
door destruction, detection rules and recorded audio retain their owners.
The integrated head replaces the separate floating face when the body grows.

To regenerate Unity resources, run `export_game.py` in Blender, then
`python export_game_textures.py`. With Unity 6000.6.0f1, execute
`HappyToy.V2.Editor.BaeksugwiGameImport.Apply` in batch mode. This exports the
reviewed full mesh and skin, remaps the in-place clip beneath its fitting root,
converts glTF metallic/roughness channels for URP, and writes the prefab and
import audit without editing the preserved school scene.

## Head-only writhing action

`Head_Writhe` is a separate 61-second looping action in `Baeksugwi_Run.blend`.
Its four quaternion channels rotate Head only; body/neck/arm/root translations,
scales and the original Run actions are preserved. `head-motion-profile.json`
controls seeded irregular yaw/roll/pitch targets with unequal transition and hold
intervals and roll up to roughly 55 degrees to either side; `author_head_motion.py` records unchanged
Run curve hashes in `head-motion-report.json`. The original full run GLB and
base Unity FBX still contain their existing gait; the new armature-only
`Assets/Resources/Baeksugwi/Head_Writhe.fbx` supplies the independent head take.

From repository root, with Blender 4.0.2 and Unity 6000.6.0f1:

```powershell
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/BaeksugwiStudio/author_head_motion.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.BaeksugwiHeadImport.Apply
```

The Unity importer intentionally copies only Head rotation curves into
`Head_Writhe.anim`, verifying their paths against the real prefab. During Chase,
MaskHorrorVisual samples this action after the existing run pose, on a separate
scaled-time clock. It freezes with gameplay pause and stops on leaving Chase. Before school
transformation, the same sampled head delta rotates the separate visible mask
after its encounter-owned pose, without accumulating rotation or moving the body.
The stationary title creature samples the same head action with an unscaled
clock; planted body/arms stay frozen. Hidden title scenes stop their clock, and
Reduced Motion retains a neutral head instead of the writhing title animation.
Existing eye glow and face lighting follow the current Head matrix.
