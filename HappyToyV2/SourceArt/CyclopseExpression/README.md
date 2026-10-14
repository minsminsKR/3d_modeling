# Cyclopse chase-expression proposal

User art direction (2026-10-14): a grotesque, increasingly frightening and
repulsive grin. Preview **Cyclopse only** as a GIF before gameplay integration.

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
