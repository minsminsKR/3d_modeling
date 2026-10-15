# Uncat cabinet capture face

User direction, 2026-10-15: apply the revised Cyclopse cabinet expression to
Uncat as well: dropped/twisted lower jaw, threatening irregular teeth, and an
increasingly grotesque approach during a detected cabinet entry.

This is Uncat, the Chapter 1 quadruped. Hwacat is a separate school monster.
The existing webapp-generated creature, rig, clips and editable full-body source
remain in `../CreatureStudio/Uncat/`. No replacement base model is generated.

`Assets/Editor/UncatCabinetFaceImport` evaluates Uncat's actual patrol-zero skin
using two measured original lip corners in `gape-profile.json` and the head
bone's up direction. The resulting frame contains the actual lip-line roll,
left/right depth difference and head pitch; it is separate from the horizontal
photo projection in `../ChaseFaces/Uncat/projection.json`. The mouth centre is
at measured muzzle depth rather than floating in front of it. Skin UV projection
stays in its original frame while jaw, teeth, clip footprint and lower-face
sculpt all use the anatomical frame. Bind-space deltas follow the original skin.
The cheek/chin seam samples the source triangles at its outer contour; bounded,
circularly filtered depths remove overlapping whisker-sheet spikes and bury
the edge in the head. The aperture retains its lip/gum depth. Original oral
surfaces are hidden through the full old tongue extent while rear head surfaces
remain present. This corrects the 2026-10-15 mismatched opening angle.

`lipCorners` are in the original photo frame (metres): viewer-left
image (u=.30, v=.65) maps to (.17118, -.12839, .04600), and viewer-right
(u=.70, v=.73) maps to (-.17118, -.19686, .14430). Depth is the foremost
intersection with the actual patrol-zero source triangles. U/V here use the
image's top-left origin. The fixed photo mapping is retained for every surface
point after anatomical-frame conversion; rotating the texture with the jaw
frame would reintroduce the mismatch. Both new meshes stay parented to the
original head bone, including the sampled patrol/chase review poses.

The original two eyes remain outside that surface/clip region and retain
MonsterRedEyes.
The shared deterministic mouth topology/sculpt is supplied by
`CyclopseCabinetFaceImport`, with Uncat-specific scale, depth, tooth recipes and
longer canine pairs in `gape-profile.json`. It creates 22 modeled teeth with
individual gaps, lengths, widths, lean, twist and broken/pointed ends, a gum
rim, recessed closed mouth bowl, and tongue. Upper/lower teeth retain their
shape while paired vertex poses move the lower jaw. Original overlapping oral
surfaces/tongue are clipped only during the cabinet expression, consistently
in lit, depth, normal and shadow passes.

`EnemyChaseFace`/`EnemyCabinetFace` own the existing chase and optional cabinet
layers. Detected cabinet rush opens the jaw in 0.6s; escaping closes it in 0.7s.
Pause freezes it, defeat holds it, and disable/retry restores and releases the
owned meshes/materials/objects. The old Uncat chase-only mouth cap is hidden
while the modeled cabinet mouth is active, then follows normal chase state.
Ordinary chase, navigation, cabinet detection probability, physical attack
timing, saves, and both original eye/emission surfaces retain their contracts.

`closed-lips-generated-original.png` is the built-in image_gen skin edit used
only on the finishing surface; painted teeth/tongue are removed so they cannot
duplicate the actual geometry. `generation-edit-target.png` is the exact input;
`generation.json` records the full prompt, mode/settings and both SHA-256 hashes.
Enamel UV detail reuses the existing approved Uncat chase texture. No new third-
party license is introduced; original creature/texture provenance remains in
the existing CreatureStudio and ChaseFaces source directories.

Reproduce with Unity 6000.6.0f1, Blender 4.0.2 and Python 3.11/Pillow 10.4:

1. From repository root, use `unity run HappyToyV2 --editor-version 6000.6.0f1 --`
   followed by `-executeMethod HappyToy.V2.Editor.UncatCabinetFacePreview.Render
   -quiet-diagnostics -logFile <new log>`. It ensures the optional Uncat resource,
   exports editable pose/mesh data under ignored `Builds/UncatCabinetFace/authoring`,
   and silently renders the actual owned runtime skin/mouth at neutral, chase,
   front/angled/both-side/isolated and patrol/chase head-pose stages, plus 60
   transition frames under `.../review`.
   The tool resets the single-frame Editor skin cache for every pose. It opens
   no gameplay scene/player; these images are asset reviews, not playthroughs.
2. From HappyToyV2, run Blender with `--background --factory-startup
   --python-exit-code 1 --python SourceArt/UncatCabinetFace/make_blend.py`.
   `Uncat_CabinetFace_Editable.blend` contains a hidden full original reference,
   editable face and mouth shape keys, separate dental/gum/cavity/tongue parts,
   packed textures and explanatory text. Axes deliberately match Unity Y-up.
   The importer/profile is the canonical reproducible construction recipe.
3. Run `python SourceArt/UncatCabinetFace/package_preview.py` for the GIF and
   chase/cabinet comparison. All required inputs are tracked; the ignored JSON
   exports are regenerable intermediates rather than unique authoring sources.

`QualityValidation.BuildWindows` calls `UncatCabinetFaceImport.Ensure` after
ordinary chase-face import. Hashes cover the model, pose, projection, skin,
recipe and both importers. The `uncat-cabinet-face` feature profile selects fast
Core checks plus Uncat actual capture/escape/pause/eye lifecycle and a Cyclopse
lifecycle regression. It does not run every monster's tests or launch a player.
