# School horror surface finish

`build_surfaces.py` derives the school-only 2K mapped surfaces from the complete,
tracked Poly Haven scans already under `Assets/Resources/GraphicsPbr`.
The photographed plaster chips, substrate, tile wear, original tangent normals
and occlusion remain the physical source. The tool adds correlated authored
mold, dampness, fine cracks and dirty grey school tile grading. Mold growth is
concentrated in the photograph's exposed plaster rather than a high-contrast
repeating cloud pattern. It does not claim these are photographs of the target
Korean school.

Run with Python 3.11, NumPy 1.26.4 and Pillow 10.4.0:

```powershell
python HappyToyV2/SourceArt/SchoolHorror/build_surfaces.py
```

No downloads, credentials or machine paths are required. The inputs, outputs,
actual SHA256 values, physical tile dimensions and original source attribution
are in `surface-manifest.json`. Metallic is red and smoothness is alpha; normals
use tangent +Y/OpenGL and the Unity normal-map importer. Albedo is sRGB; other
maps are linear. All maps use mipmaps and repeat with maximum anisotropy 8.

The runtime `SchoolHorrorSurfaces` owns its school materials and releases them
without modifying the shared graphics pool. `SchoolDecaySurface.shader` uses
URP PBR, real additional lights/shadows, probe reflections, SSAO-compatible depth
normals and Unity distance fog. A second world-scale moisture field reduces
obvious repetition. Tile UVs use continuous world XZ coordinates across the
4.4m culling pieces. The tile texture contains dry ceramic roughness only;
irregular roof-leak pools plus a narrow, wandering damp route drive wet
darkening, smoothness and pore flattening in metre space. The first leak at
z=10.8m runs toward the entry, making a pool centred at (-0.35m, 9.8m) with
1.2m/3.7m radii; a second pool is at (0.55m, 24.3m). Pools reach 0.94 smoothness
and the thin route film stays around 0.50–0.59, with matte edges beside the walls.
This catches actual overhead-light reflection footprints without painting
highlights into the tile. Repeat sections account for their actual 14m/34m
placement and the school root offset.

Glass grime is a low-contrast smudge/drop layer with very shallow normal relief.
Glass and water films use retained URP transparent/premultiply local shader
variants. URP premultiplies diffuse only so genuine glass specular survives,
while a separate geometric coverage fade removes water highlights at film
borders. Transparent materials skip opaque SSAO and do not cast solid shadows.
Closed thin glass boxes cull backfaces to avoid rendering both interfaces twice.

This is a scan-derived production prototype, not a photogrammetric rebuild of
the exact reference school. It has no true displaced plaster or real-time planar
or ray-traced reflection. The localized light haze does perform a bounded
12-step, depth-clipped single-scattering integration under selected lamps. It
approximates the lit volume locally and does not integrate volumetric shadows
through the scene. Water uses actual light highlights and bounded reflection
probes rather than painted reflections.

## Source rights

- `worn_plaster_wall`: Dimitrios Savva, Poly Haven, CC0 1.0;
  <https://polyhaven.com/a/worn_plaster_wall>.
- `floor_tiles_06`: Rob Tuytel, Poly Haven, CC0 1.0;
  <https://polyhaven.com/a/floor_tiles_06>.
- License: <https://polyhaven.com/license>.
- Original source maps, authors, official checksums and license are preserved in
  `ThirdParty/Graphics/polyhaven-pbr-manifest.json` and graphics credits.
- The moisture/crack finish code and custom shader are authored for HappyToyV2;
  derived textures retain the CC0 source permissions.
