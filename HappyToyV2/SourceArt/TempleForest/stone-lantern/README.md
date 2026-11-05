# Korean stone lantern

An isolated Korean Buddhist stone lantern was generated through the designated
A100 service from `reference.png`; the image prompt, request/job IDs, result
hashes and original server records are preserved alongside the 61 MB original
GLB. The same job was inspected in the sidebar's 3D viewer from the front and
rotated directions: roof, stone column, weathering and square openings are
visible. The ZIP download cache is kept separately under the excluded
`../work/stone-lantern-download/` and is not an authoring dependency.

`stone-lantern.blend` keeps the unmodified original and two source-derived game
LODs. The object is normalized to 1.45 m high. Blender 4.0.2 exported the near
mesh at 64,999 triangles and the distant mesh at 17,999; texture maps are
limited to 2K. Both LODs use the original material's base color and packed
metallic/smoothness. The generation result did not contain a normal map.
The application deliberately does not invent an unrelated normal map.

CPU previews showed the near/distant silhouette and fire-chamber openings from
three angles. `surface-proof.json` records 1,804 sampled source vertices with
near/far p95 distances of approximately 0.31/1.06 mm at normalized scale; the
FBX reimport retained all near and all but one far triangle. These samples and
neutral previews do not establish Unity or game-camera quality. Placement is
visual-only, outside the primary approach; actual light and surface appearance
must be judged in the game.

## Reproduce

From the repository root with Blender 4.0.2, using the project's isolated
background runner on the user's Windows desktop:

```powershell
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/stone-lantern/prepare_stone_lantern.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/stone-lantern/verify_stone_lantern.py
blender -b -t 2 --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TempleForest/stone-lantern/preview_stone_lantern_cpu.py -- --output <new-directory>
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -nographics -noaudio -executeMethod HappyToy.V2.Editor.TempleStoneLanternImport.Import -v2-audit-no-desktop-interaction
```

The finishing script imports the shared, repository-relative
`SourceArt/CorridorEntrance/finish_entrance_assets.py` recipe; no additional
base model generator is used. Generated preview images are optional and are
not needed to reproduce the asset. Preserve source GLB, editable blend,
generation inputs/records, textures, exports and their Unity `.meta` files.
