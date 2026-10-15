# Player hands and ritual items

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

From the repository root, use **Blender 4.0.2** (its bundled Python and NumPy):

```powershell
blender -b --python HappyToyV2/SourceArt/PlayerItems/finish_player_items.py
```

The script resolves paths relative to itself, writes the packed Blender sources
and the game FBX/PBR resources under `Assets/Resources/PlayerItems`. For one item,
append `-- --only=hand` (or `incense`, `talisman`, `flashlight`). In pinned Unity
**6000.6.0f1**, run `HappyToy.V2.Editor.PlayerItemsImport.Apply` to regenerate
material/prefab assets through Editor APIs. Keep all resulting `.meta` files.

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
