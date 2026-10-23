# New school loose aggregate fragments

New single-object reference and new designated-service TRELLIS.2 geometry.
Four distinct rough concrete / granite aggregate stones form one small isolated
floor cluster. Clear fracture faces, damp edges and irregular thickness are real
source geometry, supported by source-derived PBR and tangent normal atlases.

- Request: `4810cde1-a314-459a-950e-a8c8747e3c44`
- Job: `0d29e70e-1537-46ae-a7bb-731e75571052`
- Seed: `2026102403`; split GPUs; `1024_cascade`, 12 steps, 4096 texture.
- `reference.png` / `reference-prompt.txt`: exact built-in image-generator input.
- `original.glb`: unchanged service base with full original embedded PBR.
- `service/`: exact supplied/processed images and unchanged generation records.
- `provenance.json`: source identity / hashes / archive mapping.
- Editable packed Blender source, two LODs and actual source-to-game map transfer
  use the shared `../finish_asset.py` with settings in `export.json`.

This generated cluster replaces former acquired aggregate dressing only where
the school assembly places it. No collision, navigation obstacle or new gameplay
is authored into the asset. Direct webapp inspection and game placement verification
are recorded by the integration owner. No third-party stock mesh was used.

Near budget is 2,500 triangles. The attempted 700-triangle distant mesh reached
a stable 1,074-triangle topology floor, so the distant target is 1,100 triangles.
The full four-fragment shape remains intact; no stone is removed to meet budget.
Runtime tiny fragments use this distant mesh with 1K maps and skip shadow casting
below 8cm height while retaining direct light, normal detail and ambient occlusion.
