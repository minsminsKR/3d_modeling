# Alley formed concrete wall PBR

Poly Haven [Concrete Wall 004](https://polyhaven.com/a/concrete_wall_004), photographed by Charlotte Baglioni and processed by Dario Barresi, [CC0-1.0](https://polyhaven.com/license). The official 2K diffuse, OpenGL normal and ARM maps are preserved under `source/`. `manifest.json` records official URLs, byte counts, source checksums, measured tile dimensions, conversion and derived hashes.

Run `python acquire_wall.py` to reproduce the PNG maps from the retained scans via the shared GraphicsUpgrade converter. Copy the four PNG maps from `staged/alley-form-wall/` to `Assets/Resources/GraphicsPbr/alley-form-wall/`, then run `HappyToy.V2.Editor.AlleyWallPbrImport.ApplyForm` in the pinned Unity editor. ARM red becomes AO; blue becomes metallic; inverse green becomes smoothness alpha.
