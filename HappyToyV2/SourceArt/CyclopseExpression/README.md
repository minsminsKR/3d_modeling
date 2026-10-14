# Cyclopse chase-expression proposal

User art direction (2026-10-14): a grotesque, increasingly frightening and
repulsive grin. Preview **Cyclopse only** as a GIF before gameplay integration.

The user approved this generated face for gameplay on the same date. The
runtime `CyclopseChaseFace` now attaches to the actual studio Cyclopse through
`V1MonsterMotion`, morphs into the grin during Chase/cabinet detection rush,
and restores the original mesh/material during Search/Patrol/disable. Pause
freezes it. Defeat retains the striking face. Other creatures keep their own
faces while sharing the cabinet encounter sequence. The subsequent Uncat/Baby
face extension is documented in `../ChaseFaces/README.md`; all three now use
the shared `EnemyChaseFace` owner, retaining the Cyclopse component API.

`Assets/Editor/CyclopseExpressionImport.Apply` reproduces
`Assets/Resources/CyclopseExpression/Expression.asset` from the approved profile,
source PNG and existing FBX/patrol pose. Per-vertex deltas are converted through
the inverse weighted skin matrix into bind space, so they follow the head's
authored animation instead of sliding in world space. Existing vertex order,
UVs, bones, support samples and source files remain intact. Each live actor
owns its cloned mesh/materials and restores/disposes them on exit.

`Resources/CyclopseExpression/ChaseFace.shader` is a lit URP material with
shadows, additional lights/cookies and fog, retaining the original material
outside the face. It uses a second UV projection that follows the skinned
surface. Source textures and facial shape can be edited here and re-imported
without regenerating the base monster. A Resources material anchors the needed
shader variants in Windows builds. `QualityValidation.BuildWindows` ensures
the authored resource exists before building.

Cabinet behavior keeps the existing player-owned 75% survival / 25% vulnerable
entry result and saved schema. Only an enemy that physically saw that entry
gets the entrance location. It approaches and visibly scans; success retreats,
failure becomes aggressive and rushes through a valid route. A fresh 0.75s
door warning starts only within physical strike reach. Exit, a new entry,
cover, pause, disable and safe mode invalidate or freeze that strike. The same
clock/navigation collaborator is used by StalkerBrain, LanternMaskEncounter
and WeepingAngelEncounter; it never substitutes current hidden-body coordinates
for witnessed evidence.

## Gameplay verification

Run `python Tools/quality/run_feature_tests.py --profile cabinet-expression`
from the project root. The profile contains fast Core rules, existing physical
cabinet safety/checkpoint tests, all six monsters' protected scan/retreat and
detected rush/kill cases, and Cyclopse chase/pause/calm/disable restoration.
Large actors may strike when their actual torso/head surface is at the door;
their navigation pivot need not penetrate the cabinet. A hard root-distance
cap, same floor and fresh physical line of sight still apply.

A freshly built player accepts
`-quiet-diagnostics -v2-cabinet-expression-output <new-directory>` for the
controlled native cycle and lit camera PNGs. It uses an isolated save directory
and the existing process audio guard. Ordinary players do not install it.

The user then requested a newly generated, freely redesigned face texture
instead of small existing-face edits. `chase-face-generated-original.png` is
the original built-in ImageGen output. `generation-edit-target.png` is its exact
input; `generation.json` records the full prompt, tool and SHA-256 hashes.

This is an opt-in 3D art proposal. `Assets/Editor/CyclopseExpressionPreview.cs`
poses the existing `EnemyStudio/Cyclopse/CyclopseRuntime` at patrol time zero,
copies its actual skin into a temporary preview surface, and preserves its
original albedo texture and UV mapping. The copied face receives two linear
triangle subdivisions before the nonlinear sculpt, keeping the coarse original
lips from turning into wedges. All original vertices and UVs are preserved;
the extra midpoint samples exist only in the preview. It never saves the protected scene, edits the
source FBX/Blender files, or installs the expression in ordinary gameplay.

`grin-profile.json` is the editable sculpt source, in Unity world metres for
that exact prefab/pose. The mouth opens and widens, its corners rise unevenly,
and the eyelids pinch. One side leads during the transition, with a continuous
spatial falloff. The existing eye and mouth surfaces are deformed. No replacement
base model is generated. The new texture supplies crooked teeth, gums, a pale
predatory eye and diseased-looking skin; teeth are painted surface detail rather
than independently modelled geometry.

`Assets/Editor/CyclopseExpressionPreview.shader` uses the original UVs for the
neutral albedo and a second UV set projected from the full-grin 3D endpoint for
the new face. Projection coordinates remain attached to deforming vertices,
not the screen. A soft front-only facial mask preserves surrounding fur/flower.
Texture and shape blend together. This is an unlit art review, not a reproduction
of the shipped PBR lighting or an assertion of completed gameplay integration.

The editor tool and profile together reproduce the editable proposal. Original
model, authoring Blender source, webapp provenance and licences stay in
`../CreatureStudio/Cyclopse/` and `../CreatureStudio/licenses/`.

## Reproduce from the Unity project directory

Use pinned Unity **6000.6.0f1**, Python **3.11**, and Pillow **10.4.0**.

```powershell
& '<Unity 6000.6.0f1>/Editor/Unity.exe' -batchmode -quit -projectPath . -executeMethod HappyToy.V2.Editor.CyclopseExpressionPreview.Render -expression-output Builds/CyclopseExpression-Preview -logFile Builds/CyclopseExpression-Preview/editor.log
python SourceArt/CyclopseExpression/package_preview.py Builds/CyclopseExpression-Preview --output Builds/CyclopseExpression-Preview/Cyclopse-grotesque-grin.gif
```

Frames/GIF/inspection OBJ and logs are reproducible, ignored preview outputs.
The 90-frame, 5-second loop holds neutral, transitions into a grin, holds, and
returns. GIF packaging verifies frame dimensions, actual pixel changes and
animation decoding. These checks do not certify chase AI, animation layering,
pause/restore behavior or gameplay integration. No audio plays in this editor
rendering workflow.
