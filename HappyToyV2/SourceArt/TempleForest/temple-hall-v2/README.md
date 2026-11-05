# Korean temple hall v2

The earlier hall is preserved separately in `TemplePrologue/temple`. This
generated exterior was promoted after an isolated in-game camera review;
`TempleHallPresentation` owns its visual and restores the earlier renderers on
teardown. It is not an authored interior and does not replace the prologue
wall, altar interaction, gate, route or save coordinates.

`reference-first.png`, the framed `reference.png` actually submitted, their
prompts, original GLB, server records, job/request IDs and checksums are
preserved here. The designated A100 service used TRELLIS.2 1024 cascade and a
4K texture; a higher-resolution earlier hall generation had run out of GPU
memory. The parent agent inspected this same job in the sidebar 3D viewer from
front and rotated sides. Roof, wood columns, lattice, stone base and steps were
recognizable; the side walls are brighter/yellower than the desired night look.
The generated back repeats a near-identical front and staircase; do not mistake
the generated hall for a historically faithful or authored interior. The
source has no normal map. The verified ZIP cache under `../work/` is optional.

Blender 4.0.2 stores the unmodified original in the packed editable blend and
exports two source-derived LODs with shared 4K base-color and
metallic/smoothness maps. Original 947,120 triangles become 160,000 near and
85,000 distant triangles (Blender FBX reimport 159,972 and 84,969). UVs are
finite and neither exported mesh has zero-length loop normals in this check.
The `surface-proof.json` result samples 1,802 source points: p95 surface
distance is 5.43 mm / 9.25 mm at the 9 m width normalization. Reimport surface
area loss is below 0.0011%. These samples do not prove all details, LOD
switching or game performance. Neutral CPU previews were examined from front,
side, back and three-quarter views; Blender -Y is the side showing the front.

After 9 m width normalization the exported bounds are about 9.00 m wide,
9.20 m deep and 6.62 m high. If the Unity importer preserves those axes and the
hall is fitted to 6 m height at z=18, its estimated front lies at z≈13.83,
near the existing wall at z=13.8 and far behind the incense at z=7.2. This is
an estimate: the isolated Unity review checked wall clearance and the start,
approach and prayer-space cameras. Extra geometry and 4K textures still require
measurement on target hardware. The combined hall/wide-tree review used
offscreen readback timings; it did not isolate the hall's cost and does not
measure displayed frame rate.

## Reproduce

From the repository root with Blender 4.0.2 and the existing background
desktop/priority safeguards:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/temple-hall-v2/prepare_temple_hall.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/temple-hall-v2/verify_temple_hall.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/temple-hall-v2/preview_temple_hall_cpu.py -- --output <new-directory>
```

The recipe reuses the repository-relative
`SourceArt/CorridorEntrance/finish_entrance_assets.py`; no alternative base
generator was used. The Unity importer, production presentation and tests are
preserved with their `.meta` files. CPU preview
images are reproducible artifacts, not required source inputs.
