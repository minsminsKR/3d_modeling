# Haunted Korean temple hall — 1024 retry source, not integrated

The user-directed reference calls for a recognizably Korean Buddhist temple oppressed by Japanese evil, not a replacement shrine. `reference.png`, `reference-prompt.txt` and `provenance.json` preserve the reference, exact prompt, A100 request/job identifiers, submitted settings, SHA-256 values and status.

The original 1536/8192 split-GPU attempt failed from GPU memory exhaustion; its request and job identifiers, settings and observed outcome remain in the `attempts` array. The separate 1024/4096 retry succeeded and is preserved here.

- `original.glb` is the unmodified successful A100 TRELLIS.2 result (993,349 triangles). Its 4096px base-color and metallic/roughness PNG textures are embedded in the GLB and intentionally not duplicated as loose files. The source contains no normal texture.
- `reference.png` is byte-identical to the archive's actual `inputs/01_reference.png` and is the preserved actual input without duplication. `service/processed_inputs/01.png` is the distinct processed input.
- `service/generation-record.json`, `service/metadata.json` and `service/outputs/` retain unmodified successful-generation records. Their service-side paths and URLs are historical provenance, not required local file locations. The verified ZIP remains a disposable download under Builds; this source folder does not depend on it.

The archive passed path and CRC checks, and the GLB header and declared byte length were validated. The same completed job was inspected in the existing in-app web viewer by rotating the building through front-three-quarter and side views; roof, colored brackets, wooden panels and foundation were visible. Back/underside were not inspected. Blender finishing, Unity integration, gameplay review and performance testing remain pending. Preserve the editable Blender source, derived LODs and reproducible processing settings alongside this original after finishing. Do not claim the model is approved or a visual match before those steps.


## Neutral comparison — 2026-11-07

The original was compared against the existing v2 original using the same isolated Blender CPU lighting and camera framing. The v3 entrance and long roof face a different source axis from v2. The generated lattice contains conspicuous dark gaps and similar door-like details repeat on the side; the v3 original does not establish a clear quality improvement. The eight 512px / 4-sample neutral comparison renders are diagnostic, not game-camera or performance evidence. Do not replace the currently integrated hall without correcting orientation and a separate in-game quality review.

## Modeling tool notice

The requested modeling backend is TRELLIS.2; its repository-preserved tool notice is [TRELLIS.2-LICENSE.txt](../../CorridorEntrance/TRELLIS.2-LICENSE.txt). Backend and Blender tool licenses do not by themselves determine rights in generated outputs. The preserved input, prompt, job record and project terms document this asset separately.
