# Alley weathered concrete wall PBR

Poly Haven [Concrete Wall 007](https://polyhaven.com/a/concrete_wall_007), photographed by Charlotte Baglioni, processed by Dario Barresi, tiled by Rico Cilliers; [CC0-1.0](https://polyhaven.com/license). The official 2K diffuse, OpenGL normal and ARM maps are preserved under `source/`. `manifest.json` contains official URLs, byte counts, source checksums, measured 2.16 m tile dimensions, conversion and derived hashes.

Run `python acquire_wall.py` to reproduce the PNG maps from the retained scans via the shared GraphicsUpgrade converter. Copy the four maps from `staged/alley-concrete-wall/` to `Assets/Resources/GraphicsPbr/alley-concrete-wall/`, then run `HappyToy.V2.Editor.AlleyWallPbrImport.ApplyConcrete` in the pinned Unity editor. ARM red becomes AO; blue becomes metallic; inverse green becomes smoothness alpha.
