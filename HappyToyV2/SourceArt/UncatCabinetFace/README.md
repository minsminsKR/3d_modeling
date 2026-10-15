# Uncat cabinet capture face

User direction, 2026-10-15: apply the revised Cyclopse cabinet expression to
Uncat as well: dropped/twisted lower jaw, threatening irregular teeth, and an
increasingly grotesque approach during a detected cabinet entry.

This is Uncat, the Chapter 1 quadruped. Hwacat is a separate school monster.
The existing webapp-generated creature, rig, clips and editable full-body source
remain in `../CreatureStudio/Uncat/`. No replacement base model is generated.

`Assets/Editor/UncatCabinetFaceImport` evaluates Uncat's actual patrol-zero skin
in the face frame recorded by `../ChaseFaces/Uncat/projection.json`, then bakes
bind-space jaw deltas and a smooth lower-muzzle finishing surface. The original
two eyes remain outside that surface/clip region and retain MonsterRedEyes.
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
   open/angled/isolated stages and 60 transition frames under `.../review`.
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
