# Wide zelkova — generated model under Unity review

The original `original.glb` is the 1536 TRELLIS.2 output from the designated
A100 service. `reference.png`, `reference-prompt.txt`, `provenance.json` and
`service/` preserve the submitted image, prompt, request/job IDs and generation
records. This is distinct from the earlier 1024 `arch-zelkova` candidate.
The same job was inspected in the designated in-app browser, front-on and
rotated. It keeps exposed roots and broader side branches than the 1024 result,
but its silhouette still does not form the sweeping tunnel of the art target.

The editable `wide-zelkova.blend` contains the unchanged generated model and
4K maps alongside the first two source-derived LODs. It is normalized to
roughly 8 m high, 11.83 m wide and 11.02 m deep. The generated shape was not
replaced by hand-built geometry.

* `game-export/wide-zelkova-lod0.fbx`: 754,538 triangles. The near source
  mesh had 1,801 coincident faces with different UVs or normals; separating
  those vertices retained every face on Blender FBX reimport.
* `game-export/wide-zelkova-lod1.fbx`: 199,893 triangles, source-projected
  UVs, with 5,191 coincident faces isolated and ten invalid loop normals
  repaired. Blender FBX reimport retained its faces and area.
* `game-export/wide-zelkova/` contains the derived 2K base colour and linear
  metallic/smoothness atlases; the generated source supplied no normal map.
  Original full-resolution maps remain packed in the editable blend and GLB.

These **first collapse LODs are rejected**: the game's camera showed large
triangular shards on the trunk and branches. On the 8 m normalized original,
the longest triangle edge is 0.254 m and none exceed 0.5 m. Collapse LOD0
contains 355 triangles over 0.5 m, including 41 over 1 m; its longest edge is
1.77 m. The earlier `surface-proof.json` only sampled source-to-LOD distance
and missed surfaces that protruded away from the source. For LOD0, testing edge
midpoints and centroids of 2,038 triangles over 0.25 m found 508 more than 3 cm
from the source; the worst was 34.6 cm. The 199,893-triangle LOD1 inherited
the problem, with 543 faces over 0.5 m and 450 long faces sampled over 3 cm
away from the source. Do not promote either collapse LOD on the basis of the
original one-directional sample alone.

`wide-zelkova-safe.blend` and `safe-export/wide-zelkova-safe-lod0.fbx`
preserve all 990,611 original triangles. Eleven zero-length loop normals are
locally repaired after splitting the adjacent edges. `safe-surface-proof.json`
checks every triangle corner against the normalized source, face/area/edge
statistics, the local effect on split normals, and Blender FBX reimport. The
maximum source-position difference is about 0.0000011 m; source and reimport
both have zero edges over 0.5 m and equal face count and surface area. Of
approximately 2.97 million comparable loop normals, 150 differ by more than
the 0.99 dot threshold around the repairs. Reimport has zero invalid normals.
This is a **high-cost review candidate**, not a completed game optimization.

`safe-export/wide-zelkova-safe-lod1.fbx` applies a cautious 3-degree,
UV-delimited planar dissolve but still has 986,361 triangles. It is not a
meaningful performance LOD and requires actual Unity visual and FPS review.
`safe-reverse-proof.json` samples all 348 far triangles with edges over 0.25 m:
their edge midpoints and centroids stay within 4.11 mm of the original source;
this is still a sample, not proof of every point on every face.
Using the old `ancient-zelkova` distant LOD is not an equivalent solution:
same-height CPU orthographic silhouette IoU was only 0.338 front, 0.225 side and
0.213 diagonal, so significant switching pop is expected. These samples do
not cover every viewing angle. There is currently no verified low-cost distant
LOD for this tree.

## Reproduction

Use Blender 4.0.2 in a background process. For the distance LOD, install the
pinned MIT-licensed `fast-simplification==0.1.12` into a disposable directory
compatible with Blender's Python 3.10 and set `PYTHONPATH` to that directory.
The shared `SourceArt/CorridorEntrance/finish_entrance_assets.py` is part of
the reproducible recipe. Run from the repository root:

```powershell
python -m pip install --python-version 310 --only-binary=:all: --no-deps --target HappyToyV2/Tools/.venv-pine fast-simplification==0.1.12
$env:PYTHONPATH = (Resolve-Path HappyToyV2/Tools/.venv-pine).Path
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/prepare_tree.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/prepare_tree_distance.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/verify_tree.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/preview_tree_cpu.py -- --output <new-directory>
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/prepare_tree_safe.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/verify_tree_safe.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/verify_safe_reverse.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/wide-zelkova/preview_tree_safe_cpu.py -- --output <new-directory>
```

The distance script expects the fresh near-only blend and does not regenerate
the service output. The downloaded ZIP cache is optional; `original.glb` and
the extracted generation records are the tracked authoring handoff. Do not
include authentication cookies, credentials, generated previews or caches in
source control.
