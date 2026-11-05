# Temple approach stone cluster — review candidate

This is a world-space 3D stone-paver cluster for comparing against the
current temple approach. It is not a background image or a camera plane.
The reference is `reference.png`; its built-in image generation prompt is
`reference-prompt.txt`. The designated A100 service request and job IDs,
generation options, original GLB checksum and the same-job IAB inspection
are recorded in `provenance.json` and `service/`. The archive cache is
preserved separately under excluded `../work/temple-stone-cluster-download/`.

The 981,654-triangle generated original with two 4K PBR textures remains
unchanged in `original.glb`. The reproducible Blender 4.0.2 recipe
`prepare_stone_cluster.py` produces the editable `temple-stone-cluster.blend`,
2K derived base-color and metallic/smoothness maps, and two FBX LODs. It
normalizes the candidate to about 1.8 m square and at most 0.12 m high; the
complete original geometry and original maps remain packed in the blend.
The original has no separate normal map. `inspect_source.py` records source
dimensions and materials; `verify_stone_cluster.py` checks sampled surface
distance in both directions, top projection, UVs, normals and FBX reimport.
`preview_stone_cluster.py -- --output <new-directory>` makes CPU previews.

| Model | Editable faces | Blender FBX reimport | Source-to-LOD p95 | LOD-to-source p95 | Source-only holes out of 625 rays |
| --- | ---: | ---: | ---: | ---: | ---: |
| Near | 24,000 | 23,888 | 1.75 mm | 1.38 mm | 1 |
| Far | 20,000 | 19,885 | 1.95 mm | 1.43 mm | 2 |

These are geometry samples, not complete topology or perceptual guarantees.
FBX reimport surface-area loss was below 0.02% for either LOD. An earlier
13,000-face far candidate produced a conspicuous open hole in the CPU preview
and was rejected; 18k/20k/22k exploratory reductions were compared before
selecting the 20k review candidate. The original cluster has a rectangular
outline and the repeated asset may still reveal boundaries. The proposed
visual-only scatter offsets/rotates cells and retains the existing physical
path and footstep reference, but seams, extreme viewpoints, z-fighting,
gameplay, PBR appearance and real rendering cost require Unity player review.

`probe_overlap.py` samples front-facing FBX triangles at the comparison
fixture's 4 cm height. At 4,096 vertical rays it found one adjacent pair less
than 0.1 mm apart in the near LOD and none in the far LOD. This is asset-space
sampling, not proof of GPU depth behavior or an absence of all intersections.
