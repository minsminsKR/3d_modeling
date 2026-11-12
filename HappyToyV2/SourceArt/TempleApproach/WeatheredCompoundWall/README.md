# Weathered Korean compound wall

The built-in image generation tool produced `reference.png` on 2026-11-12,
using `../References/user-alley.png` as a mood/construction reference. Input
SHA-256: `7f98924d024271d8cdbc1697e56a2e8481cdb95b51da13ca6ad146f0b4298c98`.
Prompt:

> Use the attached image ONLY as a reference for the lived-in 1990s Korean residential alley mood, construction and restrained weathering. Create an isolated 3D-modeling reference of ONE ordinary Korean residential boundary-wall segment: a 3 metre long, 2 metre high aged gray cement-plaster wall, with a low dark red-brick foundation, a narrow concrete coping, and ONE modest square concrete pillar at one end. Fine irregular rain streaks, small chipped plaster exposing some brick, faint moss along the bottom, realistic material scale and mild age. This is an inhabited neighborhood, not a ruin; no fantasy, no grand traditional roof and no gate. Orthographic-like three-quarter eye-level view showing full front, thin top, one side and entire object, straight accurate geometry, not a flat painting. Neutral even studio light, photographic PBR surface detail, sharp realistic textures. Isolated on uniform light gray background with no ground plane, no shadow merging into it, no furniture, no plants, no people, no buildings, no sky, no text, no sign, no logo. One complete standalone wall segment.

Designated A100 modeling service request `6554b77a-55da-4d16-a655-c0dee19197b2`,
job `fafced54-a94d-4247-bd33-6d2077f432fb`, TRELLIS.2 1024 cascade,
4096 texture, seed 64281, GPU 1. The completed archive was verified as
SHA-256 `650fc80e47b4650778f353ef80d4af92bb2030e960d8cbef8043415d12ff3b77`;
the original `model.glb` SHA-256 is
`7dc0d933305dea1b99ff6287304fb10981846294f19989f87427a92b7dbeaf45`.
The request, original and processed reference, result metadata, download
receipt, original GLB and extracted PBR maps are retained here.

The same job was rotated in the designated service viewer to inspect both
sides and the end pillar. Blender 4.2.23 normalized the full-resolution mesh
and saved a packed, editable source before decimating 988,462 triangles to
60,000 for Unity. CPU previews of both faces retained the brick base and
weathering. Unity uses two curved-path sections and the original path collider;
no new mesh collider is added to gameplay. A D3D11 PlayMode camera review
and sampled visual-to-collider alignment checks passed. The focused real-input
shop, firecracker, dog and village-to-mountain seam test also passed. These
checks are not a complete game regression or an FPS benchmark.

To reproduce, run Blender 4.2.23 with `--background --threads 2
--python-exit-code 1 --python SourceArt/TempleApproach/WeatheredCompoundWall/prepare_weathered_wall.py`
from the Unity project root. Copy the generated FBX and albedo to
`Assets/Resources/AlleyWeatheredWall/geometry.fbx` and `albedo.png`, then run
`python SourceArt/TempleApproach/convert_gltf_material.py SourceArt/TempleApproach/WeatheredCompoundWall/metallic-roughness.png Assets/Resources/AlleyWeatheredWall/metallic-smoothness.png`.
Run Unity editor method `HappyToy.V2.Editor.AlleyWeatheredWallImport.Apply` to
create/update the URP material. The original editable mesh is intentionally
high resolution; the FBX is the reduced runtime export.
