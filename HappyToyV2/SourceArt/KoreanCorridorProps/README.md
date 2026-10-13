# Korean corridor props

The jangseung guardian post, onggi jar and dokkaebi roof-end tile were generated
with the user's designated A100 modeling webapp on 2026-10-14. They are generated
Korean-inspired horror set dressing, not scans or copies of a game asset.
`webapp-generation.json` records the real jobs and requested UI settings.
`reference-generation.json` preserves the built-in ImageGen prompts. The actual
uploaded PNGs and full original GLBs are retained beside those records.

Each `.blend` contains the complete original mesh/PBR in a separate collection
and the editable game LOD. The full original textures are packed. Game outputs
are under `Assets/Resources/KoreanCorridorProps`: FBX, up-to-2K PBR maps,
materials and prefabs with Unity metadata. Original GLBs and packed Blender
sources use Git LFS; run `git lfs pull` after cloning.

Use **Blender 4.0.2**, then Unity **6000.6.0f1** with the checked-in package lock:

```powershell
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/KoreanCorridorProps/prepare_game_props.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.KoreanCorridorPropsImport.Apply
```

The Blender Python/numpy shipped with Blender is sufficient. `-- --only onggi`
can reproduce one asset. The converter welds coincident glTF vertices while
retaining loop UVs, decimates copies to 12k/6k/10k triangles, normalizes physical
sizes and converts glTF roughness/metallic into Unity metallic/smoothness maps.
It does not generate replacement base geometry. Return to the designated
webapp if the underlying model must be generated again.

The runtime placement module reserves clear central routes, furniture, door
travel and candle stations. Wall reliefs have no extra collision; jars and
guardian posts keep compact physical colliders outside the main route.

These references were generated for this project; no third-party source artwork,
character model or game audio was copied. The project user's asset permissions
apply. Blender and the modeling backend remain separately licensed tools;
their installation and license information is in `hunyuan3d-webapp/INSTALL-A100.md`.
