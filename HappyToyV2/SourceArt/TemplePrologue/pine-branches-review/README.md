# Branch-preserving pine: production near LOD

This is a derived mesh of the existing generated temple pine, not a newly
generated base model. It is used as the near LOD after comparing the Unity
camera and the original and improved art in an isolated native player. A
separate protected middle LOD and the earlier low-cost very-far LOD preserve
distance scaling; existing tree transforms and capsules are unchanged.
The earlier near mesh is still available through the opt-in original-pine
comparison in `NativeTempleReview`.

The preserved source, reference, prompt and service records are in
[`../pine/`](../pine/) and the history is in [`../README.md`](../README.md).
The foliage-bearing original came from the designated A100 TRELLIS.2 service,
request `cc51c42e-431d-4392-bd03-5e2dcb87f4f2`, job
`4ff43435-9370-4552-9e84-30d96ac8ec80`. The source editable file is
`../pine/pine.blend`, SHA-256
`a1b98378393acd446f48a15fc77439d390df1f6c02134e41c6e6bab470d364f4`.
The existing pine colour atlas used for material classification is
`../game-export/pine/CE_pine_0-baseColor.png`, SHA-256
`64eb21a16a0ced3c1d4555a5521c79f895911761209ea834bcaf63fcd0e58867`.

`prepare_pine_branches.py` normalizes the original to 10 m, welds only the
existing 0.00002-unit tolerance, splits non-manifold edges and uses the
existing atlas colour heuristic to preserve wood-coloured vertices across the
whole crown instead of only the central 1.6 m. The colour heuristic is an
approximation, not a semantic classification of every branch. It protects the
source below 4.6 m, uses Blender decimation ratio 0.075 on unprotected areas,
and changes 42 otherwise-zero split-normal loops to their 42 valid face
normals by marking those affected faces flat. Other source materials and
images are retained. `pine-branches-review.blend` is editable and
`pine-branches-review.fbx` is the derived near LOD. The unchanged original
GLB and source Blender are not replaced.

| Measurement | Existing near | Candidate near |
| --- | ---: | ---: |
| Editable triangles | 195,666 | 257,499 |
| Triangles after FBX reimport | 194,527 | 256,441 |
| Canopy triangles with an edge over 0.5 m | 1,134 | 2 |
| Canopy triangles with an edge over 1 m | 193 | 0 |
| Largest canopy edge | 2.61 m | 0.56 m |

The original has 961,875 triangles and no edges over 0.5 m at the same scale.
The 42 repaired loops and FBX reimport both have zero invalid normal loops.
FBX reimport loses about 0.19% of editable surface area. In fixed, 1,200-point
per-region samples, original-to-candidate canopy p95 is 4.62 cm (existing near:
4.84 cm); candidate-to-original p95 is 1.16 cm (existing: 1.51 cm). Original
trunk samples remain within 0.002 mm of the exported surface. On a 2,601-ray
overhead grid, the original is hit at 1,443 points, of which the candidate
misses 51 and the old near LOD misses 58. The p95 top-hit height difference is
about 0.66 m, so these samples do **not** prove there are no canopy gaps or
ensure identical foliage. See the JSON proofs for scope and sample counts.

The existing far LOD is 40,697 triangles and must be shared. Attempts to protect
branches in an independent lower-detail model produced 178,760–206,572
triangles and were rejected as distant LODs. At worst the 14-pine all-near
comparison adds roughly 867,000 triangles over existing near FBX geometry;
actual visible geometry, draw calls, GPU timing and display FPS depend on
camera, LOD selection and hardware and have not yet been measured.

With Blender 4.0.2 on the PATH, reproduce and independently reimport into new
paths without replacing the handoff:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/pine-branches-review/prepare_pine_branches.py -- --output <new-directory>
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/pine-branches-review/verify_pine_branches.py -- --output <new-proof.json>
```

Use the project's private-desktop and low-priority wrapper when running Blender
on the user's machine. CPU studio previews do not replace actual Unity camera,
reimport, movement, shadows, LOD switch, or device performance checks.
