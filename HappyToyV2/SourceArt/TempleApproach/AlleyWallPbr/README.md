# Alley red brick PBR

Poly Haven [Red Brick](https://polyhaven.com/a/red_brick), photographed by Rob Tuytel, CC0-1.0: https://polyhaven.com/license. The official 2K diffuse, OpenGL normal and ARM scans are preserved in `source/red_brick/`. `manifest.json` records official URLs, size, MD5 and SHA-256 checks, 1.4 m physical width, conversion and output hashes.

Run `python acquire_wall.py` to reproduce the PNG maps using the shared GraphicsUpgrade converter, then copy the four files from `staged/alley-red-brick/` into `Assets/Resources/GraphicsPbr/alley-red-brick/` and run `HappyToy.V2.Editor.AlleyWallPbrImport.Apply` in the pinned Unity Editor. ARM red becomes AO; blue is metallic; one minus green roughness becomes smoothness alpha.