# Acquired scan rubble and exposed school structure

The primary originals are complete and remain unmodified in `Originals/`:

- **Concrete Debris**, Amal Kumar / Poly Haven, CC0. The original editable
  Blender material and all linked 4K photographs/physical maps are retained.
  This is a scanned PBR/height field, not a volumetric rubble mesh. Three game
  patches sample its real height/UV data with a tapered outline and an authored
  75mm height amplitude. They are described as scan-derived relief throughout.
- **Namaqualand Stones 01**, Greg Zaal (photography), Jenelle van Heerden
  (modeling) / Poly Haven, CC0. The actual five individual LOD0 scanned stone
  meshes and original UVs are used for small exposed aggregate. They are not
  described as scanned plaster or objects found in the reference school.

See `LICENSE.md`, `source-manifest.json` and the primary asset URLs recorded
there for authors, rights, official MD5 values and actual SHA-256 hashes.
The acquired originals total **154,115,772 bytes**. No preview/site artwork,
account data, external installed cache or absolute source location is required.

## Reproduce

The acquisition script uses Python 3.11 standard libraries. Its saved manifest
pins the exact official requests and validates matching files offline.

```powershell
python HappyToyV2/SourceArt/SchoolHorror/StructuralDecay/acquire_sources.py
```

Use **Blender 4.0.2** (official build hash `9be62e85b727`) for deterministic
mesh derivation. The script rejects other versions. Its bundled NumPy is used;
no additional Blender add-on is required.

```powershell
blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolHorror/StructuralDecay/derive_game_assets.py
```

Outputs are eight FBX assets plus their `.meta` files and 2K physical maps under
`Assets/Resources/SchoolHorror/Structural/`. The five scan pieces target 1,500
triangles each; the three heightfield patches contain 2,880 triangles each.
Actual mesh/map hashes, counts and authoring settings are in
`game-derivation.json`. The packed, editable `school-structural-decay.blend`
preserves the final assemblies/materials; originals remain beside it separately.
Only game maps are reduced to 2K/8-bit. Original 4K image/EXR precision remains
available for later authoring, rebaking and closer LODs.

The acquired source was saved by Blender 4.2; Blender 4.0.2 loads the explicit
LOD0 mesh datablocks without its newer UI panels. The retained original file is
unchanged. The derivative works on copied mesh data, triangulates and decimates
those copies with original UVs, and exports the preserved metre scale.

## Runtime ownership and actual structural damage

`SchoolStructuralDecay` owns static presentation under each school repeat
section. It reuses imported meshes, creates only school-local material copies,
and gives generated architectural meshes to `SchoolLoopWorld.owned` for cleanup.
The world combines compatible material/shadow groups spatially after assembly.
The per-section scene has 14 interrupted concrete-heightfield deposits, 70 small scan
aggregate instances at the edges and three tiny isolated route fragments.
Large residue remains outside the central 3m route. It adds no collider,
navigation obstacle, update loop, sensing rule or save field.

The code adds 16mm chipped/curling coating lips around asymmetric textured
exposed plaster patches with satellite flakes. Only selected narrow seams are
darkened; the acquired plaster scan stays visible across each exposed patch.
Fallen fragments vary from 14–58mm in thickness, with larger tilted pieces below
the real ceiling breaches, and the leaned slabs have visible 38mm broken edges.
The scan deposits vary in width, length, rotation and damp tint to avoid a
continuous pale skirting border. Ceiling edges
query `SchoolLoopArchitecture.MissingCeilingPanel` so they follow the actual
missing finish cells, with a separate roof deck above. Real expanded-metal
lath, hanger wires and pipes are visible through those gaps. These are editable
architectural construction elements; they do not replace a generated base model.

`SchoolDecayDressing` no longer adds the old underslung fake damaged finish
panels or flat triangular plaster speckles. Dark printed pages are wrinkled and
concentrated in a few uneven edge heaps between the scan rubble, with only three
loose sheets in the central route. There are 238 leaves in ten heaps per section,
mixing torn scraps and larger sheets, with physical corner tears and folds.
Existing classroom, shop and music meshes
remain their original authored assets.
