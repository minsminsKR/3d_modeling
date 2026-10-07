# Original corridor furnishings

These are original measured Blender models for the abandoned timber and washi
corridor. Geometry is released under CC0-1.0; there are no downloaded meshes.
Existing attributed PBR surface sets are reused from
`Assets/Resources/GraphicsPbr`. Their source/license inventory remains in
`ThirdParty/Graphics` and `SourceArt/GraphicsUpgrade`.

## Editable sources and regeneration

Blender **4.0.2** is the pinned authoring/export version. No add-ons or Python
packages are needed. From the `HappyToyV2` project directory, run:

```powershell
blender --background --python-exit-code 1 --python SourceArt/CorridorFurnishings/author_corridor_furnishings.py
```

Put Blender's installed executable on PATH or replace `blender` with the
executable path on your machine. The script derives all source/output locations
from its own repository location, never from a workstation path or the current
working directory. It writes the five editable `Models/*.blend`, five runtime
FBX files and `model-manifest.json`. Unity imports FBX files using their tracked
`.meta` configuration; it does not need Blender installed to build or play.

To make optional studio previews, pass an **absolute** review-output path after
the Blender argument delimiter. Blender can change its working directory while
loading/saving, so a relative preview path is deliberately discouraged:

```powershell
blender --background --python-exit-code 1 --python SourceArt/CorridorFurnishings/author_corridor_furnishings.py -- --preview-dir <absolute-optional-preview-directory>
```

Sources contain relative links to the existing tracked PBR maps. Pull the full
repository (and hydrate Git LFS if configured) before opening a `.blend`. No
cache, other checkout or untracked texture is required. Generated `.blend`
files preserve the actual case/drawer meshes, UV layers and named material slots
for direct hand editing. The generator is the reproducible measured baseline.

After generation, check every editable source and relative texture link with:

```powershell
blender --background --python-exit-code 1 --python SourceArt/CorridorFurnishings/validate_furnishing_sources.py
```

The validator opens all five `.blend` files read-only, verifies resource/source
digests, independent drawer pivot, supported FBX tangent topology, mesh budgets
and that every image resolves beneath the tracked project PBR source directory.

## Model contract

All authored dimensions are metres. Constructors use a measured source plan,
Y up and front **-Z**. FBX export performs its axis/handedness conversion; the
asymmetric source-plan X placements appear mirrored in Unity. Mount coordinates
in the manifest are explicit **runtime furniture owner-local** metres, with
that X conversion already accounted for. Runtime keeps the imported child
transform intact beneath an identity wrapper. There are no colliders,
lights, animation tracks or scripts in the FBX files.

| Resource key | Model |
| --- | --- |
| `CorridorFurnishings/writing-desk` | Five-plank hardwood top with end-board joinery, rounded mortised legs, stepped feet, recessed single drawer field, separate open-top drawer, dovetail ends, slotted brass bail hardware and keyhole |
| `CorridorFurnishings/archive-shelf` | Tall joined archive shelf with rear planks, molded crown, shelf supports, sewn ledgers with recessed page blocks and inventory labels, rolled washi archives, hollow footed celadon vessel and folios |
| `CorridorFurnishings/writing-set` | Desk ledger, separate loose manuscript leaves, modeled ink marks, recessed inkstone, tapered brush and open water cup |
| `CorridorFurnishings/firecracker-pack` | Three rolled red paper tubes, crimped ends, short fuse strands and actual hemp bundle ties |
| `CorridorFurnishings/battery-pack` | Two zinc cells with wrapper seams, identification bands, crimped metal caps, brass terminals and keeper; no floor stand |

`Drawer` is one independent mesh transform. Its closed pivot is
`(0,.708,-.052)` in desk metres. Opening moves the whole imported part .30m
toward desk **-Z**; runtime derives the movement through the identity wrapper,
so FBX conversion axes are not guessed. The drawer has four thin walls and a
bottom, with no solid box filling its cavity. Its floor surface is Y=.649m.
The solid under-top bead's underside is Y=.7685m; this is the actual closed
cavity ceiling. The compact firecracker bundle is at most .112m tall, so its
closed top is below Y=.761m with more than 7mm clearance beneath that bead.
The pickup and physical proxies move with the drawer. `model-manifest.json`
contains measured bounds, triangle budgets and explicit five-part proxy boxes.

The desk writing set fits on the left half when mounted at
`(-.25,.84,.05)`. The finite surface pickup stays clear at
`(+.35,.84,-.02)`. The shelf reserves its **runtime left** bay at Y=.50 and
Y=1.50 for pickups. The battery bottom mounts at `(-.40,.504,-.09)`, clear of
the seven standing ledgers on the runtime right. Source-plan books have negative
X while their imported Unity placement has positive X; new mount code must use
the declared runtime coordinates rather than raw source constructor positions.
At Y=1.0 the runtime left bay contains modeled scrolls.
The shelf's full `placementEnvelope` is only a navigation/placement boundary.
Its `collisionBoxes` describe open face proxies (posts, shelves, sides, back and
crown), never a solid cube across the pickable shelf interior.

Existing `GU_*` material slots use `GraphicsSurfaceLibrary.Pool.Resolve`.
`CF_ledger_blue`, `CF_ledger_red`, `CF_celadon` and `CF_dust` tint existing
`paper-aged` and `ceramic-tile` PBR sets using the documented `materialContract`.
No new texture set or opaque unlicensed reference was introduced. Structural
faces use measured UV metres, three-segment geometric bevels and area-weighted
normals. Cylindrical PBR UVs use actual circumference and height, avoiding a
whole wall-tile texture being stretched around a small vessel.

Meshes remain below 40,000 triangles per resource. Runtime owners batch the
static authored parts and retain `Drawer` separately; collider proxies and
finite collectible IDs remain the gameplay system's responsibility. Studio
previews are optional art checks, while production-camera tests verify actual
Unity materials, drawer access, lighting and placed props.
