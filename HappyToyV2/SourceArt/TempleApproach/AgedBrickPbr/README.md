# Alley aged brick wall PBR

Poly Haven [Brick Wall 09](https://polyhaven.com/a/brick_wall_09), processed by Rob Tuytel and Matterfield, [CC0-1.0](https://polyhaven.com/license). The official 2K diffuse, OpenGL normal and ARM maps are preserved under `source/`. `manifest.json` records official URLs, byte counts, source checksums, measured tile dimensions, conversion and derived hashes.

Run `python acquire_wall.py` to reproduce the PNG maps from the retained scans via the shared GraphicsUpgrade converter. Copy the four PNG maps from `staged/alley-aged-brick/` to `Assets/Resources/GraphicsPbr/alley-aged-brick/`, then run `HappyToy.V2.Editor.AlleyWallPbrImport.ApplyAgedBrick` in the pinned Unity editor. ARM red becomes AO; blue becomes metallic; inverse green becomes smoothness alpha.

The near right-hand brick wall by the shop uses this same measured 2.01 m
PBR surface as well as the separate curved section farther down the alley.
The adjacent authored gate and the original wall collider are unchanged.
A same-camera D3D11 comparison with the previous red brick was reviewed; an
extra runoff decal was rejected because it appeared unnaturally dark. The
focused village PlayMode check validates the visible wall material, original
colliders and gameplay-camera view. Target GPU frame time was not measured.
