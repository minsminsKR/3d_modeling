# Forest-floor fern asset

Generated fern clump for the temple forest, used in four sparse, visual-only placements. The reference was generated with the prompt in `reference-prompt.txt`, then submitted to the designated A100 service. Request and job IDs, parameters, source checksum and the interactive review are recorded in `provenance.json` and `service/`. The embedded original PBR textures are retained in `original.glb` and `fern-clump.blend`; the original reference is `reference.png`.

## Shape and cost

- One compact green/brown fern clump; four CPU views show a recognizable fern with delicate connected fronds, appropriate for sparse Korean mountain forest undergrowth. It does not supply the separate fallen leaves or grass requested for the environment. It remains a generated interpretation, not a surveyed native species.
- Original GLB: 994,047 triangles, one UV set, two packed 4096-square RGBA images. Normalized to roughly 1.0 m wide and 0.344 m tall.
- Near `game-export/fern-clump-lod0.fbx`: 74,520 triangles in Blender; FBX reimport has 74,061 (459 fewer, 0.084% surface-area difference). Source-sampled p95 surface distance 1.25 mm at this width.
- Distant `game-export/fern-clump-lod1.fbx`: 34,651 triangles, unchanged after FBX reimport; p95 4.36 mm, sampled maximum 60.4 mm. Details have visibly simplified. Neither LOD is appropriate for broad ground coverage without further optimization and runtime measurement. CPU sample distances do not ensure all thin frond details survive.
- `surface-proof.json` records sampling, reimport, surface areas, zero normal counts, hashes, and limitations. CPU preview tool renders neutral, transparent four-angle source/LOD comparisons; it is not an in-game camera or lighting review.

## Reproduction

Blender 4.0.2 is required. Run from the repository root on a background/isolated desktop according to workspace instructions. The shared `SourceArt/CorridorEntrance/finish_entrance_assets.py` is part of the recipe. LOD1 needs `fast-simplification==0.1.12` installed into a disposable folder compatible with Blender's Python 3.10 and supplied through `PYTHONPATH`.

```
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/fern-clump/prepare_fern.py
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/fern-clump/prepare_fern_distance.py
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/fern-clump/verify_fern.py
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/fern-clump/preview_fern_cpu.py -- --output <new-output-directory>
```

The second command replaces its own generated LOD1, not the original or near source. The CPU review images are optional and reproducible, not required source. The 1K derived textures are in `game-export/fern-clump/`; the atlas represents base color and metallic/smoothness, without a generated normal map. `TempleForestFernImport.Import` creates the URP material and prefab. Keep texture factors from `export.json` and verify the target pipeline's settings instead of assuming interchange defaults.

## Review and remaining checks

A collaborating agent inspected the original interactive 3D model for the same job in the designated sidebar, from the front and after rotation. No joint edits were reported. Unity uses shared material and LOD meshes without added fern colliders; the forest camera review includes the plants, while individual LOD transitions and displayed FPS remain unmeasured. The service ZIP is a redundant download cache preserved under the excluded `../work/fern-clump-download/` directory; all required source and records are extracted here. Preserve generated source, image input, editable blend, scripts, texture maps and records when handing off; do not commit credentials or personal CLI state.

## Sparse outer-forest LOD — 2026-11-05

`prepare_fern_undergrowth.py` reads the preserved `fern-clump.blend` without
overwriting it, and writes a separate `fern-undergrowth.blend` and
`game-export/fern-undergrowth-lod1.fbx`. It uses Blender 4.0.2 and the pinned
`fast-simplification==0.1.12` Python 3.10 package. `verify_fern_undergrowth.py`
checks the original and editable hashes, UVs, normals, surface samples and
Blender FBX reimport. `preview_fern_undergrowth.py -- --output <new-directory>`
renders two-angle CPU comparisons without altering source assets.

The separate LOD contains 15,477 triangles versus 34,651 for the existing
near fern LOD; FBX reimport retains all faces. Across 1,778 sampled near-mesh
vertices, source-to-LOD surface distance is 1.07 cm p95 and 4.12 cm maximum
at a one-metre model width. This is a sampling measure, not a fidelity proof.
The far LOD loses visible leaflet detail, so use the existing 34,651-face
model for the closer level and the 15,477-face version only at distance.
Much stronger 9k/2.5k reductions removed most of the fronds in CPU review and
were rejected. Unity in-game appearance, LOD transition and frame cost still
require separate validation. Both Unity LODs reuse the existing fern atlas and
instanced material; the scatter is visual-only and does not alter collision.
