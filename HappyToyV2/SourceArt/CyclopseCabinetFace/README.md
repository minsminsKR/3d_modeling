# Cyclopse cabinet capture face

User direction, 2026-10-15: start with Cyclopse only. When it detects the player
inside a cabinet and approaches to kill, open the mouth very wide, expose
threatening teeth, and intensify the grotesque expression.

`EnemyChaseFace` owns the cloned original skin and original chase expression.
Its optional `EnemyCabinetFace` collaborator receives only the existing
`CabinetAggressive` decision and delta time, and owns the cabinet mouth layer.
Only Cyclopse's expression resource has `cabinetFace` assigned. Ordinary chase,
Uncat/Baby, detection probabilities, navigation, attack timing and saves retain
their existing rules. The layer opens over 0.6 seconds during detected rush,
closes over 0.7 seconds after escape, freezes in pause, stays open in defeat,
and resets on disable. The mouth now uses paired closed/open vertex poses instead of scaling the
mouth vertically. Upper crowns retain their shape and position while the lower
row follows the dropped and pitched jaw; broken tips, long crooked fangs and
missing-tooth gaps remain different throughout opening. Mouth and lip vertices
share the same sculpt and aperture, including depth/shadow passes. Every owned
mouth/patch mesh, material and object is released.

The original generated Cyclopse base/rig remains in
`../CreatureStudio/Cyclopse/`, including its original webapp GLBs and editable
Blender source. This work is local facial finishing of that existing base:

- `gape-profile.json`: bind-space cheek/jaw/lid sculpt, mouth dimensions and
  deterministic tooth construction settings. The 2026-10-15 revision drops
  the lower jaw by 32.5cm, tapers/cants it, and keeps a shallow upper arch.
  `upperTeeth`/`lowerTeeth` specify each of 22 crowns independently: position,
  length, width, lean, twist, depth, pointed/broken tip. Gaps and clusters are
  authored explicitly instead of using equally spaced angular rows.
  No replacement creature generator.
- `closed-lips-generated-original.png`: built-in image_gen edit used only on
  the lower-face patch. Removes painted teeth to prevent a second tooth row
  appearing while the modeled mouth opens. `generation-edit-target.png` is the
  exact input; `generation.json` preserves the exact prompt, settings and hashes.
- Actual geometry: 22 individually authored irregular curved teeth, a continuous gum rim, a recessed
  closed mouth bowl, and tongue. Enamel UVs reuse detailed pixels from the
  existing approved chase-face texture. Five URP Lit materials retain scene
  lighting/shadows. The lip patch uses the new generated skin texture.
- The original mouth has overlapping coarse surfaces. A dense lower-face patch
  samples the existing model's front depth and UV, then opens with the sculpt.
  The runtime shader clips covered original lower-face pixels and the new mouth
  aperture consistently in forward, shadow, depth and normal passes. The sole
  original eye is outside the patch and retains MonsterRedEyes ownership.
- `Cyclopse_CabinetFace_Editable.blend`: packed original-skin reference with
  neutral/gape shape key; editable lower-face patch with its own gape shape key;
  separate enamel, gums, cavity and tongue geometry with paired hinged-gape shape keys. The hidden full original
  body is a reference, not another runtime model. Authoring JSON uses Unity Y-up
  axes deliberately. `make_blend.py` reconstructs this file from the importer
  export. Canonical runtime construction is the profile plus the import script;
  adjust these and regenerate when changing the approved shape.

Reproduce from repository root (Unity 6000.6.0f1, Blender 4.0.2, Python 3.11 /
Pillow 10.4). Use `unity run HappyToyV2 --editor-version 6000.6.0f1 --` followed
by `-executeMethod HappyToy.V2.Editor.CyclopseCabinetFacePreview.Render
-quiet-diagnostics -logFile <new log>`. This imports the resource, exports the
editable surface data under ignored `Builds/CyclopseCabinetFace/authoring`, and
renders 60 silent front/oblique asset-review frames under `.../review` using the
actual owned runtime skin and mouth. It opens no gameplay scene/player. Then,
from HappyToyV2, run Blender with `--background --factory-startup
--python-exit-code 1 --python SourceArt/CyclopseCabinetFace/make_blend.py`.
`package_preview.py` packages those frames and the frontal comparison into GIF.
These are asset reviews, not a claim of human gameplay or difficulty testing.

`QualityValidation.BuildWindows` reaches `CyclopseExpressionImport.Ensure`,
which rebuilds this resource when the FBX/profile/generated texture/importer
input hash changes. The focused `cyclopse-cabinet-face` quality profile runs
fast Core checks and only three Cyclopse integration checks: actual detected
capture retaining full gape, pause/escape closing it, and ordinary chase/eye/
disable lifecycle with the shared original skin preserved.

No new third-party model or texture license is introduced. Generated skin is
project-authored with built-in image_gen; existing approved texture and original
creature provenance/licenses remain in their existing SourceArt directories.
