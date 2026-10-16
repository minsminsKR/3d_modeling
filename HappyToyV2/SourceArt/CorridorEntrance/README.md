# Cave entrance and sealed temple gate

This source bundle preserves the designated modeling webapp's temple-gate and
cave-rock assets for the corridor's starting chamber. Each asset has its own
folder containing the actual `reference.png`, unchanged `original.glb`, `job.json`
and `provenance.json`. `processed.png` preserves the exact background-processed
image downloaded from the corresponding webapp job. Keep the image-generation prompt/settings, submitted
webapp settings, job identity and original SHA-256 with those inputs. A temporary
server job or a download URL is not a replacement for these local source files.

The gate and rock base meshes come from the repository's modeling webapp. Local
Blender finishing preserves the original surfaces, UVs and PBR while creating
editable sources and bounded game LODs. Return to the designated webapp when a
base needs regeneration. Do not replace these generated originals with another
model or a procedural substitute.

From the repository root, use **Blender 4.0.2** and the pinned Unity
**6000.6.0f1** project/packages:

```powershell
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/CorridorEntrance/finish_entrance_assets.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.CorridorEntranceImport.Apply -quiet-diagnostics
```

The finishing script resolves paths relative to its own location. It exports
`Assets/Resources/CorridorEntrance/temple-gate.fbx` and
`cave-rock-lod0.fbx` / `cave-rock-lod1.fbx`, their required textures and editable
packed `.blend` sources. Preserve Unity materials, prefabs and every `.meta`
created by the corresponding importer. Keep source-generation/LOD reports with
the assets so physical bounds, triangle budgets and hashes can be checked on
another machine. Python embedded in a Blender `SOURCE` text block must also be
preserved as a standalone repository-relative script for reproducible exports.

For one asset append `-- --only temple-gate` or `-- --only cave-rock` to the Blender
command. The gate is uniformly scaled to 15m wide, preserving its original height
and depth proportions; its game budget is 100,000 triangles. Each rock is 6m high
with source-derived 30,000 / 8,000-triangle LODs. Both are centred horizontally
and grounded. The intended gate front faces Unity +Z; inspect the generated
orientation and use `--gate-yaw 180` when the original is reversed. The reported
`gateYawDegrees` records this local finishing correction.

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
`temple-gate-visual.prefab` and a two-level `cave-rock-visual.prefab` LODGroup. Prefabs contain
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
`CorridorEntrancePresentation.Model` loads `CorridorEntrance/<key>-visual` and
instantiates that prefab directly. Imported meshes, external materials and PBR
maps stay shared and unchanged; the presentation owns only the spawned instances.
The removed entrance plaque is not part of the gate artwork. Gate inspection
must remain physically blocked and must not finish a run.

Originals, actual references, settings/provenance, packed Blender files, export
tools, licenses and Unity resources are required source. Optional rendered
previews, test captures, builds, caches and Blender automatic backups belong
outside required source commits. Hydrate Git LFS objects after clone/pull and run
`python HappyToyV2/Tools/quality/check_source_completeness.py` after staging.

See `LICENSE.md` for origin and tool-license notes. Verification commands and the
entrance profile are recorded in `CODE_MAP.md` and `Tools/quality/README.md`.
