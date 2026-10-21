# Acquired photographed surface originals

These are actual downloaded Poly Haven lossless PNG originals, not previews or
procedural substitutes. Four selected sets preserve their 2K Diffuse, OpenGL
normal, packed AO/roughness/metallic and displacement maps unchanged. The
source binary total is 223,589,666 bytes. Git LFS retains these originals with
the repository; `git lfs pull` must hydrate them before offline derivation.

| Original | Author | Source use |
|---|---|---|
| [Rough Plaster Broken](https://polyhaven.com/a/rough_plaster_broken) | Rob Tuytel | Photographed thick peeled plaster and irregular substrate edges |
| [Worn Mossy Plasterwall](https://polyhaven.com/a/worn_mossy_plasterwall) | Amal Kumar | Actual dark organic staining, cracks and roughness |
| [Peeling Painted Wall](https://polyhaven.com/a/peeling_painted_wall) | Dimitrios Savva | Paint fractures, detached coating and exposed plaster; also adapted to damaged ceiling finish |
| [Dirty Tiles](https://polyhaven.com/a/dirty_tiles) | Matterfield (photography), Jenelle van Heerden (processing) | Single-colour worn quarry tiles with actual grime, chips and narrow grout |

All four assets are CC0 1.0 under the provider's [asset license](https://polyhaven.com/license).
The original Creative Commons legal code is included as `CC0-1.0.txt`.
Provider website artwork, logos and example renders were not copied into the
game. Per-asset `info.json` and `files.json` preserve the actual official API
metadata; `acquisition-manifest.json` records URLs, authors, physical dimensions,
official MD5/size and local SHA256 for every original map.

Acquisition uses the provider's public API with an identifying User-Agent and
bounded downloads. It is separate from the normal offline source workflow:

```powershell
python HappyToyV2/SourceArt/SchoolHorror/acquire_photographed_sources.py
python HappyToyV2/SourceArt/SchoolHorror/acquire_photographed_sources.py --verify-only
python HappyToyV2/SourceArt/SchoolHorror/build_surfaces.py
```

Reacquisition downloads only a missing or checksum-mismatching source. Normal
clone/pull setup restores the tracked original bytes without calling the API.
The surface generator keeps these source files immutable and writes separate
game derivatives with 2K importer limits, mips, normal-map typing and correlated
roughness/AO. Displacement originals remain editable authoring inputs; the
runtime finish does not claim ray-traced displacement of every peeled chip.
