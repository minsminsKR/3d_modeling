# Player hands and ritual items

The 2026-10-24 hand replacement is authored in [PlayerHandsV2](../PlayerHandsV2/README.md).
Its new A100 original, photographic skin projection, measured rig and editable Blender
source supersede the historical hand described below. The old hand originals are
retained for provenance; the incense, talisman and flashlight sources remain here.
The hand export entry points now delegate to the replacement recipe.

The hand, incense, talisman and flashlight base surfaces were generated through
the repository's designated modeling webapp, on GPU 0. `generation-jobs.json`
records job IDs, batches, settings and SHA-256 values. `Originals/` contains the
unchanged downloaded GLBs with their embedded 4096px PBR maps. `References/`
contains the actual image inputs, exact image-generation prompts and the webapp's
processed images. The flashlight reuses the existing project-generated HUD image;
its original image provenance is included. No remote training process or GPU 1
was used for these jobs or for rigging.

All four packed `.blend` files are editable. The hand contains the original left
hand surface, wrist/palm/finger armature, skin weights and named grip actions.
Runtime mirrors it for the opposite hand. The talisman includes five separately
editable cut fragments of the generated paper, preserving its texture coordinates.
Full input originals remain available if further topology work is needed.

The October 16 hand finish reshapes that preserved surface into a shallower palm
while retaining rounded digits, its UVs and original textures. The depth mask
excludes the thumb base and fingers from the palm correction. The generated long
fingers are shortened locally beyond the MCP fan: index centre-line length is
93.8 mm against a 102.5 mm palm. The thenar weights blend back into the palm to
avoid folding the thumb base as a single thin flap. Its skin mesh
targets 80,000 triangles, with five small editable keratin nail caps conformed to
the original distal skin surface. Each cap follows its finger's skin weights and
uses the separate `hand_nail` material slot. The rig follows measured curved digit centre lines:
17 deform bones, five nondeforming fingertip markers and `palm_contact` (23 bones
in total). Each digit explicitly aligns its hinge frame to the original palmar
surface. Nearest-digit weights blend across 32% of each knuckle segment without allowing one
finger to pull the adjacent finger across a pinch. The packed `hand.blend` keeps
the finished geometry and editable rig; `Originals/hand.glb` stays unchanged.
The exported `hand/asset.json` records 40,586 vertices and 80,960 triangles in total
(the 80,000-triangle skin plus 960 nail triangles), with both skin and nail slots.
It also records actual normalized-weight error, the four-influence limit,
palmar hinge directions, neutral joint curvature and output SHA-256 values.
Blender uses linear skinning, matching Unity, for an honest deformation preview.

The October 16 grip review uses the preserved user target and before images in
`SourceArt/CorridorEntrance/References/user-target-20261016.png` and
`user-before-20261016.png`. `hand/asset.json` links both files and their hashes;
these review inputs do not replace the original webapp hand GLB or its provenance.

From the repository root, use **Blender 4.0.2** (its bundled Python and NumPy):

```powershell
blender -b --python-exit-code 1 --python HappyToyV2/SourceArt/PlayerItems/finish_player_items.py
python HappyToyV2/SourceArt/PlayerItems/finish_skin_detail.py
```

The script resolves paths relative to itself, writes the packed Blender sources
and the game FBX/PBR resources under `Assets/Resources/PlayerItems`. For one item,
append `-- --only=hand` (or `incense`, `talisman`, `flashlight`). In pinned Unity
**6000.6.0f1**, run `HappyToy.V2.Editor.PlayerItemsImport.Apply` to regenerate
material/prefab assets through Editor APIs. Keep all resulting `.meta` files.
PNG exports require complete CRC-checked chunks before replacement. Geometry
and packed Blender sources are written completely to temporary files before
replacing their prior versions; the JSON report is replaced atomically too.

Run `finish_skin_detail.py` after the hand export with Python 3.11 and the pinned
NumPy 1.26.4 / Pillow 10.2.0 in `Tools/requirements-authoring.txt`. It derives subtle
tangent-space micro relief from the original albedo's high-frequency detail and
writes `hand/skin-normal.png`; it adds no replacement mesh or external texture.
The Unity importer keeps the hand's albedo, roughness/metallic and normal at up to
4096px, connects the normal to the world/held materials, and preserves the other
items' 2048px game import limit. Source GLBs retain their embedded 4096px maps.
`PlayerItemsImport` maps `hand_nail` to `hand/nail.mat`, a subtle glossy keratin
material. Nail geometry and source material remain editable inside `hand.blend`
and are recreated by `finish_player_items.py`; the original hand GLB is untouched.

`FirstPersonHands` owns the live poses and contact placement: the flashlight is
wrapped by all fingers, incense and talisman sit between the index/thumb tips,
and the snapper/trap rest on `palm_contact` with the hand turned upwards and
the fingers gently cupped together. The thumb separates bounded metacarpal
opposition/spread from phalanx flexion, with bounded extension at the thumb base;
the distal joint follows the middle joint instead of folding independently to
reach a target. Contact solving cannot add free axial roll. The flashlight grip
uses its narrow shaft, with its broad head excluded from contact checks against
the actual imported mesh triangles. The supporting palm turns diagonally toward
the view with a relaxed C-shaped finger cup. The snapper hand opens during release and
returns to its supporting cup. These
runtime poses use the imported rig and contact markers. Hands remain near the
lower corners of the view; throw/place motion is applied to their grip sockets.
The flashlight socket tilts 15 degrees to show the wrist and back of the hand
instead of only the stacked fingers, while the solved hand/handle contacts move
together. Pinched-item use lifts outward, keeping tall paper out of the central
forward sightline. `PlayerHandsPreview.Render` accepts the optional
`-player-hands-grip-review` flag to compare the measured camera-presentation variants.
The standalone Blender actions remain editable authoring references. FBX's
handedness conversion reverses local Y/Z pose rotations in Unity while retaining
the local X flexion axis.

`render_icons.py` renders transparent inventory icons from these editable sources
and the existing `SourceArt/Snappers/package.blend` / `SourceArt/Traps/Trap_Editable.blend`.
It uses the same Blender version and writes `Assets/Resources/Hud/Items`.

These are project-generated surfaces and image inputs, rather than downloaded
third-party prop models. The existing photographic candle-flame resource used by
the talisman retains its existing project provenance and attribution.

For silent action previews, use the actual runtime hand-pose code in the pinned
Editor, then assemble its PNG frames with **Pillow 10.2.0** (already pinned in
`Tools/requirements-authoring.txt`). From the repository root:

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 --timeout 600 -- -force-d3d11 -executeMethod HappyToy.V2.Editor.PlayerHandsPreview.RenderActions -quiet-diagnostics
python HappyToyV2/Tools/quality/render_hand_action_gifs.py game/verification/ritual-items/hand-action-gifs
```

The five clips sample the existing hand poses at 25 fps and play at 1x speed;
the talisman includes its complete ten-second burn. These are isolated Editor
hand-layer renders, not native gameplay recordings. The GIF assembler crops
unused transparent space and adds labels with the project's Korean font. Its
duration checks retain the renderer's time base. Frames, GIFs and manifests are
optional review outputs outside required source assets.

Incense now selects one nearby reachable memory by NavMesh route length and
keeps that target until collection; after all memories it selects the offering.
`IncenseSmokeTrail` and `IncenseSmoke.shader` render one continuous, softly
feathered plume with world-space motion history. Both simulation and shader
flow use the same pause-aware gameplay clock. No circular particle cards are
used for this plume. To review it without starting a player:

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 --timeout 600 -- -force-d3d11 -executeMethod HappyToy.V2.Editor.IncenseSmokePreview.Render -quiet-diagnostics
python HappyToyV2/Tools/quality/preview_incense_smoke.py game/verification/ritual-items/incense-smoke-review
```

The comparison includes the archived original shader/equations and the current
renderer in a controlled dim corridor, with the actual held incense tip. It is
an Editor visual review rather than native gameplay footage. `--profile incense`
in `Tools/quality/run_feature_tests.py` checks target collection, altar fallback,
pause and optional inventory-save compatibility.
