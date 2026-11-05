# Ancient zelkova — generated forest asset

This image-to-3D model supplies two prominent old trees in the forest; the
remaining trees use a separate pine asset. The preserved `original.glb` came from the
designated A100 modeling service. `reference.png`, `reference-prompt.txt`,
`provenance.json`, and `service/` retain the input, generation parameters,
job/request IDs and server record; the original GLB includes both 4K maps.
The same job was inspected in the IAB model viewer from front, side and rear.
It has gnarled roots and bark, though its source canopy is shorter and denser
than the reference. No rigging is appropriate for this static prop.

`ancient-zelkova.blend` retains the complete imported source and a full-detail
`Zelkova proportion review (full detail)` object with a reversible
`Raised canopy` shape key: after uniform normalization, heights above 1m are
extended by 35%. The game candidate is derived from that shape; the original
source is not overwritten. The editable blend packs the source maps. Output
files are preserved SourceArt and also imported as Unity resources:

- `game-export/ancient-zelkova-lod0.fbx`: 483,427 editable triangles and
  231,999 Blender vertices. Bounding dimensions approximately 10.2m × 10.9m ×
  9.9m (width × depth × height); ground is near z=0 in Blender.
- `game-export/ancient-zelkova-lod1.fbx`: 139,544 triangles and 98,442
  Blender vertices. Exact equal position/UV/normal/winding faces are removed;
  distinct coincident faces are split to preserve them through FBX reimport.
- `game-export/ancient-zelkova/CE_ancient-zelkova_0-baseColor.png` is a
  2K sRGB colour map. `CE_ancient-zelkova_0-metallicSmoothness.png` is a
  2K linear texture with metallic in R and smoothness in A, derived from the
  original glTF metallic/roughness texture. The source contains no normal map.
  glTF material is opaque, single-sided. Preserve original packed maps.

For Unity 6000.6.0f1 / URP, export uses Blender `axis_forward=-Z`, `axis_up=Y`
as the existing pine pipeline does. Import with no collider or animation,
assign one shared URP/Lit material to each LOD, `_BaseMap` = sRGB base colour,
`_MetallicGlossMap` = linear map, `_Smoothness` = 1 and enable
`_METALLICSPECGLOSSMAP`; retain factors and settings in `export.json`. Turn off
`weldVertices` for LOD1 so its preserved coincident faces are not merged.
Use a two-level LODGroup only after checking actual Unity import counts, bounds,
textures and switching distances. Use a separate authored collider; do not
block the existing player route, altar, gate or saved positions. Resolve actual
root grounding and axis orientation using Unity renderer bounds rather than
assuming FBX orientation. `TempleForestTreeImport.Import` creates the Unity
models, maps, material, metadata and prefab from these source outputs.

`surface-proof.json` records 1,813 original-surface samples (245 below 3m).
LOD0 lower-trunk p95/maximum are both effectively zero; canopy p95 is 1.44cm
and max 3.30cm. LOD1 lower-trunk p95 is 1.47cm and max 2.54cm; canopy p95 is
5.44cm and max 18.98cm. These samples are not complete shape comparison. In
Blender's FBX reimport LOD0 contains 482,399 faces (1,028 fewer; total surface
area decreases ~0.08%); LOD1 retains all faces and near-identical area. Both
editable and reimported LODs have finite UVs and no zero-length loop normals
in this Blender check; Unity import remains a separate verification step.

Transparent eight-sample CPU previews were inspected at front, side, rear and
three-quarter angles for the source and both candidates. The 483k/140k pair
preserves the original silhouette more faithfully than the earlier 301k/43k
trial, especially around the canopy and trunk. Even this pair is expensive and
was reviewed with the actual game camera, including a close view and an
isolated offscreen render benchmark. The asset still
lacks the target image's large arching limbs and should complement, not blindly
replace, the rest of the forest.

## Reproduce

Run from the repository root, via `Tools/quality/run_isolated_desktop.py` when
on the user's Windows desktop. `prepare_tree.py` uses the repository-relative
`SourceArt/CorridorEntrance/finish_entrance_assets.py` helpers. For Blender 4.0.2's
Python 3.10 install the pinned QEM dependency to a project-local directory:
`fast-simplification` 0.1.12 is MIT licensed
(https://github.com/pyvista/fast-simplification). Blender is GPL-licensed
(https://www.blender.org/about/license/). These tools are dependencies, not
new sources for the generated base model.

```powershell
python -m pip install --python-version 310 --only-binary=:all: --no-deps --target HappyToyV2/Tools/.venv-pine fast-simplification==0.1.12
$env:PYTHONPATH = (Resolve-Path HappyToyV2/Tools/.venv-pine).Path
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/ancient-zelkova/inspect_source.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/ancient-zelkova/prepare_tree.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/ancient-zelkova/prepare_tree_distance.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/ancient-zelkova/verify_tree.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/ancient-zelkova/preview_tree_cpu.py -- --output <new-directory>
```

The distance script expects a fresh near-only blend from `prepare_tree.py`,
and refuses to overwrite an existing LOD1. Temporary downloaded ZIPs and CPU
preview images are not source inputs; keep them out of source commits.
