# Renewed Korean school environment

The visual kit covers eighteen new generated assets: wall panel, quarry-tile floor
slab, damaged acoustic ceiling panel, classroom door and window frame, straight
heating pipes and a valve manifold, shoe bank, student desk and chair, open archive
shelf, steel cabinet, low timber cabinet, fluorescent housing/tubes, concrete
fragments, loose stones, cast-iron radiator and projecting classroom sign. The
two pipe types prevent large valves repeating at every short section of corridor.

## Origin and editable sources

Each base model comes from the user's designated modeling webapp at
`https://agrees-mic-icon-contribution.trycloudflare.com`, using TRELLIS.2. Each
folder retains the exact original GLB with embedded full-resolution PBR, the
reference image and prompt/settings, actual service generation record, job/request
identifiers, download receipt, direct interactive web-viewer review, and an
editable packed `.blend`. See `LICENSE.md` for origin and tool notices.

`original.glb` is never edited. `finish_asset.py` duplicates and normalizes the
generated shape, repairs/simplifies only game copies, creates coherent UV atlases
and transfers source colour/metallic/roughness with real high-to-low tangent
normal baking. Game maps are normally 2K near / 1K far. Actual triangle budgets
and any topology limit are recorded honestly in each `export.json`; generated
triangle islands are not represented as a successfully achieved lower budget.
Asset-specific `finish_geometry.py` scripts preserve real window/cubby openings,
repair microtriangle tearing, or crop actual source panels. They retain source
coordinates for registered bakes. The fluorescent emission mask is derived from
the actual two tube surfaces and source albedo, leaving its casing dark.

The generated `room-sign` reverse face contains unwanted pseudo-symbols.
Its optional `finish_geometry.py.prepare_transfer_source` hook makes a separate
paint-transfer copy and remaps only reverse-enamel UVs to the same model's clean
front enamel, using BVH surface hits and barycentric interpolation of the actual
front triangles. The original GLB, original UVs and source images stay unchanged;
the corrected copy supplies game colour/ORM, while the untouched original remains
the high-to-low normal source. `finish_asset.py` runs this hook automatically and
records its source/derived UV hashes, surface measurements and limitations in
each export's `materialFinishing`. Korean names remain separate native lettering.

## Reproduction

Use the pinned Unity `6000.6.0f1` and Blender `4.0.2`. The repository's existing
tool bootstrap installs Blender under `Builds/AuthoringTools/`; this is a
rebuildable tool cache and is not the editable source. Normal Python 3.11 with
Pillow is sufficient for archive/handoff checks. Blender scripts use its bundled
Python/NumPy; standalone measurement/validation uses the NumPy and Pillow pins
in `Tools/requirements-authoring.txt`. Shared dependencies are checked in at
`SourceArt/CorridorEntrance/finish_entrance_assets.py` and
`SourceArt/SchoolLoop/rebake_music_lod.py`.
The commands below use the pinned Blender CLI; Unity reconstruction uses the
project's CLI-invoked editor importer. These are reproducible CLI operations and
do not depend on a Blender or Unity MCP connection.

With a logged-in local CLI, use `python hunyuan3d-webapp/modeling_cli.py resume
<preserved request UUID> --output <Builds download folder>` to recover the same
job. The `collect_*_sources.py` tools verify archive SHA-256 receipts and copy
complete authoring sources into this folder. They do not copy private CLI state.
Use each export's `settings` with the following command pattern:

```powershell
& HappyToyV2/Builds/AuthoringTools/blender-4.0.2-windows-x64/blender.exe -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolRenewal/finish_asset.py -- --key school-desk --axis z --metres .73 --budgets 12000,4000 --texture 2048
& HappyToyV2/Builds/AuthoringTools/blender-4.0.2-windows-x64/blender.exe -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolRenewal/reorient_finished_asset.py -- --key school-desk --rotation-z 90
```

Run any recorded per-asset emission/alignment postprocessor before the Unity
import. `SchoolRenewalImport.Apply` creates/remaps the materials and two-level
prefabs in `Assets/Resources/SchoolRenewal/<key>/`; `Ensure` guards player builds.
All paths in tools are repository-relative. Saved service records may contain
historical server paths as provenance; neither Unity nor reproduction depends on
those directories or a live tunnel once source files have been pulled.

## Runtime ownership and visual scope

`SchoolRenewalModels` supplies immutable generated geometry. Architecture retains
its original simple physical envelopes, locked-door inspect IDs and transparent
closed glazing. `SchoolRenewalArchitecture` tiles generated floor/ceiling modules
and uses clipped portions of the generated wall for low sills and upper lintels.
The structural backing is hidden. Source UVs remain intact, including the wet
floor's generated atlas; world-space shallow water and the existing bounded
planar reflection remain continuous. Static geometry joins the world's existing
8m spatial batches. All repeated sections use identical modules at the seams.
School-only material clones calibrate the pale generated albedos to the existing
approved exposure (wall 0.52, ceiling 0.42, floor 0.44 RGB gain); originals remain
unchanged. Full walls retain source indexed topology, and clipped wall parts
reuse matching complete vertex attributes without merging UV/normal seams.
Dry generated tile gloss is capped independently of the wet field. Water normals
level across authored tile facets so a pool reflects as one shallow surface,
while its dry borders retain source geometry and normal detail.

`SchoolDecayDressing` and `SchoolMusicRoomDressing` place the new furniture with
their original guards and teaching-wall orientation. `SchoolStructuralDecay`
places source-PBR rubble clusters and retains actual ceiling gaps, lath and
fracture edges. Small fragments use shared far geometry. `SchoolHorrorLighting`
keeps its existing light output, ranges, approved exposure, Forward+ scope and
shadow/reflection budgets; `SchoolFluorescent` drives both LOD emission atlases
with one paused-aware voltage clock. No new resource changes map version 3,
transfer coordinates, saves, player handling or progression.

`SchoolRadiatorDressing` replaces the old flat radiator covers with the generated
cast-iron assembly, including its feet, section gaps and short end connections.
Ten shared near/far assemblies face the aisle in each repeated section, retaining
the old clear walking width and the existing wall connection. They add no route
colliders or NavMesh obstacles. `SchoolRoomSign` mounts the generated enamel
casing and wall bracket perpendicular to each classroom wall, above the existing
door header. Two depth-tested text faces display the exact Korean names
매점 / 정독반 / 음악실 without mirroring either walking direction. The casing
uses generated source PBR and LODs; Korean lettering uses the bundled sign font.
Neither presentation module adds light sources or changes locked-door IDs.

`school-renewal` verifies generated resource use, PBR identity, seating direction,
both tube LODs and pause, physical navigation, transparency, actual wet reflection,
clock/gate/loop continuity and normal production-camera views. Explicit review
output uses the existing `HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR` and
`HAPPYTOY_SCHOOL_CONNECTOR_REVIEW_DIR` under Builds. `validate_kit.py` checks the
original/source/export/map hashes; the repository source-completeness audit runs
after staging. No credentials, build caches, redundant previews or private
machine state are authoring inputs.

For these two prop types alone, `school-wayfinding` selects the new radiator and
double-sided nameplate integration/camera tests, actual bidirectional loop walking,
matching seam views, gate/aisle navigation and the approved camera brightness,
alongside Core and school loop/save rules. Optional source review captures use
`HAPPYTOY_SCHOOL_RADIATOR_REVIEW_DIR` and `HAPPYTOY_SCHOOL_ROOM_SIGN_REVIEW_DIR`
under Builds. Production-camera tests use the normal camera render request, not
a preview-only exposure.

These are newly generated game assets with source-derived detail, not a whole
school captured by photogrammetry. Repeated modules, baked microdetail and
bounded real-time reflection remain performance choices. Final camera captures
show actual Unity rendering; they are not a manual playthrough or an FPS claim.
