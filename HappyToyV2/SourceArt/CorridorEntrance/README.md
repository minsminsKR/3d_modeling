# Cave entrance and sealed temple gate

This source bundle preserves the designated modeling webapp's temple-gate,
cave-rock, ritual-floor and guardian-statue assets for the corridor's starting chamber. Each asset has its own
folder containing the actual `reference.png`, unchanged `original.glb`, `job.json`
and `provenance.json`. `processed.png` preserves the exact background-processed
image downloaded from the corresponding webapp job. Keep the image-generation prompt/settings, submitted
webapp settings, job identity and original SHA-256 with those inputs. A temporary
server job or a download URL is not a replacement for these local source files.

The four base meshes come from the repository's modeling webapp. Local
Blender finishing preserves the original surfaces, UVs and PBR while creating
editable sources and bounded game LODs. Return to the designated webapp when a
base needs regeneration. Do not replace these generated originals with another
model or a procedural substitute.

From the repository root, use **Blender 4.0.2** and the pinned Unity
**6000.6.0f1** project/packages:

```powershell
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/CorridorEntrance/finish_entrance_assets.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.CorridorEntranceImport.Apply -quiet-diagnostics
python HappyToyV2/SourceArt/CorridorEntrance/normalize_handoff_text.py --include-unity
python HappyToyV2/SourceArt/CorridorEntrance/record_ritual_assets.py
```

Run the recorder after the two new exports, Unity import and final asset/meta
saves. It validates the preserved completed/saved job records and hydrated GLBs,
then refreshes the floor/guardian provenance with exact input, source Blender,
recipe, game PBR/LOD/prefab and metadata hashes. It records source handoff only;
it does not generate assets or certify rendered quality/gameplay performance.
Run the normalizer with `--include-unity` only after Unity has exited; without
that flag it touches the listed SourceArt text inputs only. Scoped LF attributes
keep their exact SHA inputs stable across clone/pull. The recorder preserves
historical script/export-report raw hashes and binary artifact hashes, recording
current LF reproduction text and current Unity import outputs separately.

For the related hand-rig/game-mesh revision, the opt-in
`HappyToy.V2.Editor.CorridorRevisionAuthoring.ImportAndReview` batches those
three importers and renders the five actual held-item poses through
`PlayerHandsPreview`. Pass `-player-hands-preview-output <local-folder>` to
choose the still-review directory. It also retains the project GPU deformation
setting used by the player build; it never opens play mode or saves the scene.

The finishing script resolves paths relative to its own location. It exports
`Assets/Resources/CorridorEntrance/temple-gate.fbx` and
`cave-rock-lod0.fbx` / `cave-rock-lod1.fbx`, plus `ritual-floor-lod0/1.fbx`
and `guardian-statue-lod0/1.fbx`, their required textures and editable
packed `.blend` sources. Preserve Unity materials, prefabs and every `.meta`
created by the corresponding importer. Keep source-generation/LOD reports with
the assets so physical bounds, triangle budgets and hashes can be checked on
another machine. Python embedded in a Blender `SOURCE` text block must also be
preserved as a standalone repository-relative script for reproducible exports.

For one asset append `-- --only <asset-key>` to the Blender
command. The gate is uniformly scaled to 15m wide, preserving its original height
and depth proportions; its game budget is 100,000 triangles. Each rock is 6m high
with source-derived 30,000 / 8,000-triangle LODs. Both are centred horizontally
and grounded. The intended gate front faces Unity +Z; inspect the generated
orientation and use `--gate-yaw 180` when the original is reversed. The reported
`gateYawDegrees` records this local finishing correction.

The new floor is normalized to an approximately 8m square with relief at most
.14m high and 18,000 / 8,000 triangle budgets. Its game roughness multiplies the
source roughness by .36 for a damp appearance; the original GLB and original
full-resolution PBR remain unchanged. The stone guardian is normalized to 2.8m
high with 30,000 / 8,000 triangle budgets. Both use up to 4096px game maps and
two-level LODGroups. Actual exported counts, dimensions and map channel statistics
belong to each generated `export.json` and game `asset.json`.
The canonical guardian prefab remains 2.8m high. Scene dressing uniformly scales
it to 3.5m to flank the monumental gate, preserving its proportions; this is an
intentional placement scale, separate from authoring normalization.

| Asset | Imported physical normalization | Exported triangles / vertices | Game map limit |
|---|---|---|---|
| Temple gate | 15m wide, approximately 10.77m high | 100,000 / 46,212 | 4096px |
| Cave rock | LOD0 6m high; both LODs share this normalization | LOD0 30,000 / 13,956; LOD1 8,000 / 2,956 | 2048px |

The converter welds coincident seam vertices while retaining per-loop UVs. It
keeps the full original geometry/PBR in a hidden source collection and the
editable game LODs in another collection. `temple-gate/temple-gate.blend` and
`cave-rock/cave-rock.blend` use compressed saves without automatic backup copies.
Each also embeds the standalone finishing script in a `SOURCE` text block.

Gate game textures are at most 4096px and rock maps at most 2048px; original 8K
maps remain packed in the original GLBs and editable sources. glTF roughness G
and metallic B are converted to Unity metallic R / smoothness A, including source
factors. Original normal maps and optional original AO are retained as derived
game maps; AO is not invented when absent. The importer creates URP Lit materials,
`temple-gate-visual.prefab` and two-level `cave-rock-visual.prefab`,
`ritual-floor-visual.prefab` and `guardian-statue-visual.prefab` LODGroups. Prefabs contain
visuals only, leaving colliders and navigation to the runtime chamber.
Its `Ensure` also rejects missing/nonfinite/collapsed UV0 on readable game meshes.
The `-visual` suffix keeps each extensionless Resources key distinct from the
imported FBX GameObject. Preserve it when changing the importer or runtime paths;
loading the FBX instead bypasses the prefab's external materials and normalization.

For an explicit import diagnosis, invoke
`HappyToy.V2.Editor.CorridorEntranceImport.InspectMeshes` in the pinned Editor.
It only logs existing prefab mesh UV0 counts/ranges, materials/keywords, map
GUIDs/sizes and property-block bindings. It does not refresh/reimport assets,
change a scene or certify the final appearance. Normal builds do not run it.

`<asset>/export.json` and `Resources/CorridorEntrance/<asset>/asset.json` record
original/FBX hashes, material slots, texture limits, physical dimensions, source
face counts and actual per-LOD triangles/vertices. Regeneration uses local
originals and the pinned tools; it does not depend on a server job remaining live.
Each `provenance.json` also records the submitted/processed inputs, exact server
options, packed source and finishing tool hashes, actual Unity material/prefab
outputs and their metadata. Unity import/visual/gameplay/build evidence is separate.

The large cavern is assembled from the rock model and the generated Korean
temple gate. The runtime entrance module owns their placement and collision;
corridor layout/checkpoints and interaction rules keep their explicit owners.
After final placement and scaling, `CorridorEntranceChamber.AddRockGeometry`
uses each rock LODGroup's lowest-detail mesh as static collision. Gate steps,
plinth and pillars use the actual gate mesh; its closed-door body also blocks
the entrance. The chamber attaches these colliders before the NavMesh bake.
The continuous stone backing closes joints between the visual rocks.
`CorridorEntrancePresentation` only assembles collaborators.
`CorridorEntranceModels.Attach` loads `CorridorEntrance/<key>-visual` and directly
instantiates the prefab with its shared imported mesh/material/maps. The cavern
owner preserves the existing 23 rock placements/colliders and closed backing,
then places shallow generated floor tiles whose top is at most .025m above the
unchanged physical floor. Their .14m total relief spans -.115m to +.025m around
the existing y=0 support. All further relief extends downwards; there are no new steps,
floor colliders, NavMesh obstacles or route/pickup changes.

`CorridorEntranceRitualDressing` owns two stone guardian instances, 55 worn candles,
four old paper lanterns, 67 hanging inked seals, sagging cords, a faded lotus
floor trace and small wet accents. It reuses the retained graphics props,
`hanging-seal` detail's paper surface and aged-paper UVs, onggi vessel and incense
models. Dark ink strokes follow the hanging paper's curved surface and join the
static detail batches. These are visuals only. Fixed small detail combines once by
material/shadow state in four
spatial zones; floor and guardian LODGroups remain independent. Only two local
point lights are shadow candidates, with the existing global shadow selector
still authoritative. The nine local lights use warm pools and a dim neutral
fill. The foreground hero lantern is uniformly fitted to an actual 1.25m height;
its suspension starts at the measured model top.
The foreground lantern's dark 佛 uses the existing bundled OFL Korean font
and its depth-tested world lettering; its font material follows the owned sign lifecycle.
The actual new-run start is chamber-local `(0, .03, -9.2)`, facing yaw 0 degrees
from beside the sealed gate toward the onward passage. Two retained generated
jangseung, offering jars, dark authored timber thresholds, extra hanging seals and
foreground candle gatherings frame that view. Existing gate/collision/model
scales, navigation and saved positions remain unchanged. These props reuse the
tracked `KoreanCorridorProps` and `CorridorArchitecture` sources; no new base model
or generated original is substituted. The composition retains nine local lights
and two shadow candidates.
Owned wet-accent material/roughness texture and batches are released on
removal; source material maps are never replaced or edited.

`CorridorEntranceVapour` reuses only two bounded `IncenseSmokeTrail` histories,
advancing at most 20Hz while the actual player is nearby and playing. Long low
fog sheets are excluded because their grazing silhouettes read as grey lines.
Pause freezes the plume clock and skips catchup.
There are no accumulating particles, extra renders/readbacks or global fog edits.
`GraphicsLightingPresentation` selects the actual 28x12x30m cave reflection box
while inside its bounds, retaining the existing 128px probe, finished-render guard,
three-second refresh limit, original maze/school planning and scope restoration.
An additional owned 128px fixed cavern probe captures the static lantern-lit
interior once and covers the floor through the connecting passage. It remains
after the travelling probe moves into the maze, preventing the wet floor from
falling back to bright outdoor sky reflections. It has a black clear colour,
bounded range and higher local priority, with no movement-triggered refresh.
Both probes are released at mode teardown. The real-camera boundary regression
backs from local z=8 through z=20 and checks the lower floor's brightness.
The taller cave passage meets a lower maze ceiling. A continuous stone backing
closes the height step above the original lintel without changing collision or
the physical doorway. Dark backing also continues under the first maze floor
so cracks at the passage-to-timber joint cannot expose the background below.
Production-camera renders replace the background with
magenta at all three aspect ratios and reject visible sky leakage in this joint.
The final entrance requires the generated floor/guardian keys; the temporary
authoring bridge `CorridorEntranceModels.PendingAssetFallback` must be false
before final verification/build.

`References/user-target-20261016.png` and `user-before-20261016.png` are the two
unchanged user-supplied design/grip-review screenshots. Their original filenames,
roles and hashes are in `References/provenance.json`; they are design evidence,
separate from the exact images submitted to the individual modeling jobs. Hand
authoring can refer to these same repository-relative inputs without duplicating
them. Model generation inputs/settings remain in each model's source folder.

The `entrance` profile includes real support/clearance/navigation/save/E checks,
source/owned-resource lifecycle checks, 16:9/4:3/21:9 production-camera offscreen
renders preserving actual start framing and recording FOV/camera pose, with
independent floor/seal/candle/guardian contributions, and actual cave
reflection-box/pause/maze-plan checks. Visual review and same-route native frame
measurement remain separate; these tests do not certify subjective realism or 60fps.
The removed entrance plaque is not part of the gate artwork. Gate inspection
must remain physically blocked and must not finish a run.

Originals, actual references, settings/provenance, packed Blender files, export
tools, licenses and Unity resources are required source. Optional rendered
previews, test captures, builds, caches and Blender automatic backups belong
outside required source commits. Hydrate Git LFS objects after clone/pull and run
`python HappyToyV2/Tools/quality/check_source_completeness.py` after staging.

See `LICENSE.md` for origin and tool-license notes. Verification commands and the
entrance profile are recorded in `CODE_MAP.md` and `Tools/quality/README.md`.
# 2026-10-17 start-space visibility

The existing nine local lights keep their original physical room scope and two
point-shadow candidates. Lantern/candle pools are stronger; the unshadowed
neutral vault fill is at local `(0, 7.5, -2)`, intensity 320, range 25, marked
important for local light selection. A rear rock graze uses intensity 4.8.
`CorridorEntranceRitualDressing.LanternsAndLight` owns these values. No shared
ambient exposure, maze illumination, authored model or collision is changed.
The `corridor-feedback` profile compares the actual start camera with the prior
local lights and checks existing surface views at three aspect ratios.
