# Haunted temple path — generated source, not integrated

The user-directed goal is an irregular, closely set Korean temple stone path matching the preserved concept. The exact reference and prompt are `reference.png` and `reference-prompt.txt`; `provenance.json` contains the A100 modeling request, job, submitted settings, SHA-256 values and completion state.

- `original.glb` is the unmodified A100 TRELLIS.2 job result (929,894 triangles). Its two 4096px PNG images are embedded in the GLB as base-color and metallic/roughness textures; they are intentionally not duplicated as loose files. The source contains no normal texture.
- `reference.png` is byte-identical to the archive's actual `inputs/01_reference.png` and therefore is the preserved actual input without a duplicate copy. `service/processed_inputs/01.png` is the service's separate processed input.
- `service/generation-record.json`, `service/metadata.json` and `service/outputs/` retain the unmodified generation records. Their service-side paths and URLs are historical provenance, not required local file locations. The verified ZIP remains a disposable download under Builds; this source folder does not depend on it.

The archive passed path and CRC checks and the GLB header/length was validated. The same completed job was inspected in the existing in-app web viewer from oblique and raised views. Individual stones and soil are visible, but the source has a straight rectangular perimeter whose repeated seams were later found visibly unsuitable in the Unity review below. Blender review candidates have since been prepared as described below. Gameplay integration, moving-player review and performance measurement remain pending. Do not treat the candidates as approved or a verified match until those stages are completed.

## Blender review candidates

`haunted-path-v2-20261107.blend` contains the complete imported source with its original packed 4096px textures, three normalized base LODs and six separate irregular-edge review variants. Blender imported 929,892 of the GLB's 929,894 triangles. The outputs are source-derived simplifications, not replacement generated models. The base LODs have 59,999 / 29,999 / 17,999 triangles and the A and B variants retain the exact base topology and UVs. The nonuniform feather affects only the outer tile region, with a maximum vertex displacement of 2.36 cm over a 2 m tile. This is intentionally subtle to avoid flipped faces, and the later Unity review found that this deformation did not hide the square seams.

`game-export/` holds the nine FBXs; its `haunted-path-v2-20261107/` subdirectory contains the derived 2048px base-color and metallic/smoothness PNGs. These are review assets: Blender FBX reimport reported unresolved image references, so the PNGs must be linked explicitly when setting up Unity materials. The original has no normal map. `export.json` records settings and hashes, and `surface-proof.json` records Blender CPU sampled-surface and nine-FBX reimport results. Those checks found no flipped or collapsed faces in the six editable variants, and no missing top-ray samples in a 25 x 25 grid. FBX round-trip lost 27, 31 and 31 triangles in LOD0, LOD1 and LOD2 respectively, with less than 0.002% area change. Sparse checks do not prove absence of seams, self-intersections or visual artifacts.

`finish_haunted_path_candidate.py` recreates the candidate from the original and the repository's `SourceArt/CorridorEntrance/finish_entrance_assets.py`. Run Blender 4.0.2 in background CPU mode with two threads from a clean copy of the full repository hierarchy:

```
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/haunted-path-v2-20261107/finish_haunted_path_candidate.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/haunted-path-v2-20261107/verify_haunted_path_candidate.py
```

The authoring script refuses to overwrite existing output; the verifier similarly refuses to replace an existing `surface-proof.json`. Preserve prior output separately when reproducing. Blender uses XY as the floor and Z as height; all exports use the same origin, 2 m nominal width and `-Z` forward / `Y` up export settings. The resulting axis mapping, materials, collisions, LOD transitions and seam appearance must be checked in Unity.


## Unity offscreen review — 2026-11-07

The three source-derived variants were imported with explicitly assigned PNG maps and checked in a frozen 1280x720 game camera at 12 and 16 rows, 75 and 105 mm relief, including a close seam view and a flashlight on/off comparison. The original layers and camera were restored in the test. The candidate showed obvious long straight seams both between columns and across rows; it is **not approved for gameplay**. The stones are more recognizable than the current surface, but this seam and repeated layout conflict with the intended natural scene. The screenshot review does not measure moving-player visual artifacts, drawn triangle counts, GPU time or displayed FPS.

The 25-tile 12-row configuration could contain up to 1,499,975 / 749,975 / 449,975 triangles if all tiles used LOD0 / LOD1 / LOD2 respectively; actual camera rendering uses mixed LOD and culling. The 33-tile 16-row configuration has corresponding totals 1,979,967 / 989,967 / 593,967. These totals are not measured draw counts. The imported review-only Unity asset folder and its sibling .meta were moved out of Assets into Builds/Verification/HauntedPathV2-Unused-Review-20261108 on this PC; that ignored cache is optional. A fresh clone can recreate review assets from the preserved game-export FBX and PNG inputs using the explicit Editor entry point `HappyToy.V2.Editor.TempleHauntedPathV2Import.Import`. Recreated Unity GUIDs may differ. The optional review test requires these assets to be imported again; normal gameplay does not. Reimport from a clean path and player build exclusion have not yet been verified in Unity.

CPU experiments with overlapping and transparent feathering reduced the straight seams but blurred/intersected stones and increased tile density approximately 1.90 times; Unity transparency and GPU cost were not tested and this approach is not approved. Reconnecting colocated vertices puts 99.92% of the original triangles in one component, so stones cannot be cleanly extracted by connectivity. A height-only segmentation cut off stone sides and misclassified soil; it is also not approved.

## Modeling tool notice

The requested modeling backend is TRELLIS.2; its repository-preserved tool notice is [TRELLIS.2-LICENSE.txt](../../CorridorEntrance/TRELLIS.2-LICENSE.txt). Backend and Blender tool licenses do not by themselves determine rights in generated outputs. The preserved input, prompt, job record and project terms document this asset separately.
