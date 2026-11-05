# Pine protected middle LOD

This middle LOD is derived from the existing generated pine rather than a new
base model. It was selected after native camera comparisons and a forced-LOD
workload check; the original 40,697-triangle mesh remains the third LOD for
very distant trees. The input original,
reference image, prompt and designated A100 job records remain under
`HappyToyV2/SourceArt/TemplePrologue/pine/`; request
`cc51c42e-431d-4392-bd03-5e2dcb87f4f2`, job
`4ff43435-9370-4552-9e84-30d96ac8ec80`. The source editable input is
`pine/pine.blend`, SHA-256
`a1b98378393acd446f48a15fc77439d390df1f6c02134e41c6e6bab470d364f4`.

`prepare_pine_far_branches.py` normalizes the source to 10 m, welds using the
existing 0.00002-unit threshold, splits non-manifold edges, protects vertices
below 1 m and one in twelve atlas-classified wood vertices, then runs Blender
4.0.2 decimation at ratio 0.0125 on unprotected regions. Atlas colour is an
approximate material heuristic, not a semantic classification of every branch.
It fixes 46 zero-normal loops by using valid face normals only on affected
faces. The current game near LOD and the original source remain untouched.

| Result | Editable | FBX reimport |
| --- | ---: | ---: |
| Triangles | 178,760 | 177,597 |
| Zero-length loop normals | 0 | 0 |
| Source-hit 2,601 overhead rays missed | 62 of 1,443 | 62 of 1,443 |

The FBX lost about 0.209% of editable surface area, with finite UVs and normals.
For independent fixed samples of 1,200 source vertices per region, original
to FBX p95 distance is 3.28 cm at the trunk and 4.98 cm in the crown. The
previous 40,697-triangle far LOD has source-to-mesh p95 distances of 21.89 cm
and 14.48 cm, and misses 189 of the same 1,443 overhead source hits. Samples
do **not** prove there are no holes or establish subjective visual quality.
`export-proof.json` records hashes, ray counts, finite normal/UV checks and
surface errors. `surface-comparison.json` from the neighboring CPU experiment
also includes reverse samples and the previous comparison.

This middle LOD costs much more than the original far LOD. If all 14 trees
were visible at this LOD simultaneously, 177,597 vs 40,697 triangles would
add approximately 1,916,600 triangles, subject to culling and LOD selection.
In the isolated Windows player, forced-middle versus forced-original offscreen
sampling measured 155 versus 172 renders/s at spawn and 170 versus 209 at
approach. Each sample includes synchronous readback and concurrent load; GPU
timestamp samples were unavailable, and these are not displayed FPS. The
third LOD switches back to 40,697 triangles at a screen-relative height of
0.06, reducing cost when trees become very small. The current map and camera
may rarely reach that threshold, so the middle-LOD cost can remain throughout
most play. Test target hardware before inferring actual performance.

The editable files here are `pine-far-branches-review.blend` and
`pine-far-branches-review.fbx`. Invoke Blender 4.0.2 with a new output path:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/pine-branches-far-review/prepare_pine_far_branches.py -- --output <new-directory>
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/pine-branches-far-review/verify_pine_far_branches.py -- --output <new-proof.json>
```

On the user's machine wrap these commands in the project's private-desktop,
BelowNormal runner. The generator and verifier refuse to overwrite output.

Run `HappyToy.V2.Editor.TemplePineFarReviewImport.Import` in isolated Unity to
rebuild the production prefab from the preserved near, middle and original
very-far FBXs. `NativeTempleReview` exposes original-far comparison flags,
and can capture each forced LOD at the same camera position. Single-tree and
all-tree comparisons keep the existing transforms and collision capsules.
