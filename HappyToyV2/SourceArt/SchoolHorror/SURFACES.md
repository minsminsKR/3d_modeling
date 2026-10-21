# Acquired photographic school surfaces

The previous small repeated stain texture and checkerboard floor have been
replaced with four newly acquired, photographed CC0 source sets from Poly Haven.
All **16 original lossless 2K PNG maps (223,589,666 bytes)** remain unchanged in
`Acquired/`, with the provider's API records, original author names, physical
dimensions, official MD5/size, local SHA256 and complete CC0 legal text.
`Acquired/README.md` and `acquisition-manifest.json` identify the actual inputs.

| Game surface | Photographed source | Game repeat |
|---|---|---|
| Wall / wall variation | Rough Plaster Broken / Peeling Painted Wall, both combined with photographed Worn Mossy Plasterwall organic staining | 1.5m / 1.8m |
| Lower wall | Peeling Painted Wall, muted green coating over exposed warm plaster, photographed dark organic growth | 1.8m |
| Ceiling / ceiling variation | Peeling Painted Wall / Rough Plaster Broken, reoriented with matching tangent normals and photographed moisture discoloration | 1.8m / 1.5m |
| Floor | Dirty Tiles, single-colour worn quarry tiles, graded charcoal grey-brown without checkerboard normalization | 2.26m, approximately 0.2825m tiles |
| Glass | Subtle smudges and droplets, narrow runoff and photographed organic residue localized at pane edges | 1.6m |

The 28 derived maps retain actual photographed peel edges, cracks, organic
staining, grout, worn ceramic grain, normal detail, AO and dry roughness.
Mold is extracted from the real mossy plaster scan rather than generated noise
circles. Strong colour casts are graded while the source grain and irregular
damage remain. Alternate wall/ceiling materials let architecture vary adjacent
panels. Glass keeps broad clear areas; it does not use blanket opaque streaks.

## Reproduce and inspect

Normal clone/pull setup hydrates the original assets through Git LFS. Offline
derivation needs no external download or machine path. Use Python **3.11**,
NumPy **1.26.4** and Pillow **10.4.0**, pinned in `requirements-surfaces.txt`:

```powershell
git lfs pull
python HappyToyV2/SourceArt/SchoolHorror/acquire_photographed_sources.py --verify-only
python HappyToyV2/SourceArt/SchoolHorror/build_surfaces.py
```

Reacquisition, only when originals need restoring from the provider, is separate:

```powershell
python HappyToyV2/SourceArt/SchoolHorror/acquire_photographed_sources.py
```

The acquisition script verifies official MD5 and byte sizes before preserving
originals. The offline generator checks every acquired input SHA256 before use
and records all output hashes in `surface-manifest.json`. Metallic is red and
smoothness is alpha; normals are tangent +Y/OpenGL and imported as Unity normal
maps. Albedo is sRGB, other maps are linear. Importer limits are 2K with mipmaps
and anisotropy 8. Original displacement maps remain editable authoring inputs.

`SchoolHorrorSurfaces` owns school-only material clones and releases them without
changing the shared graphics pool. The URP shader receives real lights/shadows,
SSAO, continuous world-mapped floor/ceiling scans and actual reflection data.
The source floor contains dry roughness only; connected asymmetric wetness is
controlled in world space by `SchoolDecaySurface.shader`. The school reflection
module supplies its rendered planar texture; highlights are not painted into
the albedo. Glass's retained premultiply variant preserves specular reflection,
skips opaque SSAO and avoids drawing both faces of a closed pane box twice.

Floor water uses the dedicated linear `puddle-field.png`, with a continuous
24m world-space repeat independent of the 2.26m photographed tile scan. R carries
isotropic broad 2–3.4m pool variation, G adds gentle approximately 1m edge
variation, and BA stores shallow isotropic water-normal XY encoded around 0.5.
Decoded BA has maximum magnitude 0.35 per axis and RMS 0.0825 before the shader's
0.05 normal scale (approximately 0.24 degrees RMS). It supplies small reflection
breakup while the water surface flattens the underlying tile relief. Coverage
uses R/G only, so droplet normals cannot carve narrow dry channels through the
film. The earlier `moisture-macro.png` remains for walls and glass; its directional
glass-runoff channel does not control floor water. The dedicated deterministic
seed leaves the other 27 maps unchanged.

Ceiling finish adapts photographed peeling wall paint to the authored damaged
ceiling geometry. These assets are not a photographic capture of the reference
school. Local light haze uses bounded single scattering without full volumetric
shadow integration; the renderer does not perform ray-traced displacement of
every flake or ray-traced reflection.

## Source rights

All four sources use [Poly Haven's CC0 asset license](https://polyhaven.com/license):

- [Rough Plaster Broken](https://polyhaven.com/a/rough_plaster_broken): Rob Tuytel.
- [Worn Mossy Plasterwall](https://polyhaven.com/a/worn_mossy_plasterwall): Amal Kumar.
- [Peeling Painted Wall](https://polyhaven.com/a/peeling_painted_wall): Dimitrios Savva.
- [Dirty Tiles](https://polyhaven.com/a/dirty_tiles): Matterfield (photography), Jenelle van Heerden (processing).

Powered by Poly Haven. Only actual asset maps and API metadata are preserved;
provider logos, example renders and website artwork are not game assets.
