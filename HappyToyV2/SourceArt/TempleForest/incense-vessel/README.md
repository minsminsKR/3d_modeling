# Korean bronze incense vessel

This open, ash-filled vessel replaces the primitive cylinder on the prayer
table. The incense sticks are separate existing game objects: no sticks are
baked into this model, so the player still places them through the existing
interaction. The table collider, focus target, action marker, incense position
and save data are not changed by the visual replacement.

The isolated reference and its framing edit, both prompts, original GLB, PBR,
request/job IDs, hashes and service records are preserved here. The parent
agent examined the same designated A100 job in the IAB viewer from the front,
rotated side and above: the rim, gray ash and empty holder were visible. The
source did not include a normal map. The already verified download ZIP is a
redundant cache under the excluded `../work/incense-vessel-download/` folder.

Blender 4.0.2 keeps the full original in the packed editable blend, and exports
two derived LODs with 2K PBR: 64,999 near and 19,999 far triangles. The FBX
reimport retained 64,992 and 19,989 triangles, finite UVs and nonzero loop
normals. `surface-proof.json` records 1,805 original-surface samples; p95
distance is about 0.23/0.81 mm at the 0.44 m width normalization. Neutral CPU
previews showed both LODs, including the ash, from above and oblique angles.
These samples and neutral pictures do not establish in-game quality.

The near model measures roughly 0.440 m wide, 0.373 m deep and 0.221 m high.
`ash-surface.json` uses Blender BVH rays to measure the central ash at 0.16726 m
above the vessel base. With the base on the existing table at y=0.845, the ash
is approximately y=1.01226, about 7.7 mm below the existing placed incense
origin at y=1.02. This is a source-space estimate; the Unity test raycasts the
actual imported mesh and the in-game camera must check the final relationship.
The near Unity mesh is readable to support that geometry validation; no runtime
vessel collider is installed. The material retains source PBR but limits
smoothness and environment reflections so it does not appear self-lit.

## Reproduce

From the repository root with Blender 4.0.2 and the user's isolated background
desktop/priority safeguards:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/incense-vessel/prepare_incense_vessel.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/incense-vessel/verify_incense_vessel.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/incense-vessel/inspect_ash_surface.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/incense-vessel/preview_incense_vessel_cpu.py -- --output <new-directory>
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.TempleIncenseVesselImport.Import -v2-audit-no-desktop-interaction
```

The finishing script reuses the repository-relative
`SourceArt/CorridorEntrance/finish_entrance_assets.py` recipe and does not
generate a substitute base. Keep source GLB, editable blend, generated maps,
exports, provenance and Unity assets with their `.meta` files together.
