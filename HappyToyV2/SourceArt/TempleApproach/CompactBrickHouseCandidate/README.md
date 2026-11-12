# Compact brick two-story house candidate

Created 2026-11-12 using the built-in image generation tool. Reference SHA-256: `f1a7add2fe537a30c44c6c17f1572920ec52b6938839e63d05dd0b0bdc89eec0`.

Prompt: Use case: 3D modeling reference for a realistic 1990s South Korean residential alley in a grounded horror game. Depict ONE ordinary compact two-story Korean neighborhood house from the late 1970s-1980s, not hanok, not Western: narrow rectangular concrete and faded red brick building, asymmetrical flat rooftop with shallow concrete parapet, dark worn window frames, one modest closed second-floor enclosed balcony facing the street, aging gray cement patches, a small ground-floor metal entrance door, minor rain staining. Typical small Korean hillside residential building; simple plausible structural geometry and window proportions, inhabited and weathered, not ruined. Show entire isolated building, orthographic-like three-quarter view, front and one side and roof clearly readable, exact full silhouette and straight verticals, no perspective distortion, no ground shadow merging into building, neutral bright overcast light, photorealistic high detail. Plain uniform light-gray backdrop, no wall or gate, no surrounding objects, no signage, no lettering, no people, no plants, no cables, no extra buildings.

Designated A100 request `5f142e64-347c-4525-847a-145d8608e8a2`, job `d530123a-59e0-4136-9089-de4e5fc06025`, TRELLIS.2 1024 cascade, 4096 texture, seed 1234. Original GLB, input, processed image, server record and options are preserved. The same job was inspected interactively in the designated web 3D viewer from front, side and roof angles; its material and main silhouette were usable.


Blender 4.2.23: run `blender -b --threads 2 --python-exit-code 1 --python prepare_house.py`, then `prepare_mid_lod.py`. The original has 956,294 faces; the game FBX has 125,000 faces and the mid-range FBX 100,000 faces. `prepare_far_lod.py` is a retained 30,000-face experiment and is not used in the game: its surface broke up visibly at the mountain seam.

Copy `CompactHouse.fbx`, `CompactHouseMid.fbx`, and `albedo.png` to `Assets/Resources/AlleyCompactBrickHouse/` as `geometry.fbx`, `geometry-mid.fbx`, and `albedo.png`. Run `python ../extract_gltf_metallic_roughness.py model.glb metallic-roughness.png`, then `python ../convert_gltf_material.py metallic-roughness.png ../../../Assets/Resources/AlleyCompactBrickHouse/metallic-smoothness.png`. Run Unity editor method `HappyToy.V2.Editor.AlleyCompactBrickHousePbrImport.Apply`.

The full model is placed behind the left wall near the shop; the 100k mesh is used near the mountain. Both are visual-only, with the authored material. The `temple-approach-village-visual` and `temple-approach-seam-visual` D3D11 play-mode tests passed: house/wall/shop spacing, triangle budget, shop and snapper event, seam ground and tree-root clearance. The same game camera exposed severe visible 30k mesh breakup; 100k removed the obvious breakup in the inspected still. The full reference-level scene and independent player performance are not claimed.

## Distant window surface and light

The generated window had overlapping triangles which became conspicuous when illuminated. `prepare_window_mid.py` rebuilds the 100,000-face mid LOD, checks the source GLB and atlas hashes and verifies the measured window UV/position, then adds a stable window surface and a narrow central rail. It saves `CompactHouseWindowEditable.blend` and `CompactHouseMidWindow.fbx`; copy the latter to `Assets/Resources/AlleyCompactBrickHouse/geometry-mid-window.fbx`. `verify_window_repro.py` compares geometry and UV hashes against the Unity FBX, ignoring nondeterministic FBX metadata.

Run Unity editor method `HappyToy.V2.Editor.AlleyCompactWindowImport.Apply` after the regular PBR import. It checks the original atlas hash, derives a 1024px emission mask from the two UV islands of this one window, restores the atlas importer settings, and creates a separate warm material. The warm material is used only with the repaired mid LOD; the original mid LOD falls back to the original dark material. The near house keeps its original material except for the separate side-window surface described below. There is no added realtime light.

The focused village and seam D3D11 tests passed and the actual game camera showed the overlapping-triangle artifact removed and the window frame preserved. Re-running the Blender recipe matched all mesh positions, triangle indices and UV data; re-running the Unity importer produced the same emission PNG SHA-256 `cc6f906438b350c9cd47184e97de612cc08bd6c95232642b9c6dc0035f0b8e7f`. Native-player appearance and FPS were not tested.


## Near house ground-floor window repair

The near house's ground-floor window had overlapping folded triangles and
scattered atlas fragments. Game-camera ray samples located the damaged face;
`prepare_ground_window.py` verifies the retained `CompactHouse.fbx` and atlas
hashes, retains the 125,000-face house and adds one UV-mapped four-vertex
window surface inside the existing frame. The clean glass sample comes from the
original atlas. It produces `CompactHouseGroundWindowEditable.blend` (packed
albedo) and `CompactHouseGroundWindow.fbx`. The full original GLB, its PBR
textures and original editable Blender file remain unchanged.

Run Blender 4.2.23 with `--background --threads 2 --python-exit-code 1
--python SourceArt/TempleApproach/CompactBrickHouseCandidate/prepare_ground_window.py`,
then copy the resulting FBX to
`Assets/Resources/AlleyCompactBrickHouse/geometry-ground-window.fbx`. Unity
uses the existing `CompactBrickHousePbr` material. The original prefab is the
fallback if the repair or material is unavailable. The repaired near mesh is
visual-only; existing collision remains untouched. The actual D3D11 village
camera and house/wall placement checks passed. The patch removes the visible
large fragment from the sampled view; it is not a reconstruction of an interior
and native-player FPS has not been measured.

## Near side-window mullion

The near house has a second-floor side window painted into the original atlas. Its dark panes appeared as one blank surface from the curved lane. `AlleySideWindowFrame` adds a small non-emissive, visual-only central mullion under the repaired near-house mesh; it does not modify the FBX or collision. Its mesh-local coordinates were fitted to the original atlas window UV bounds (approximately U .3663–.4169, V .8163–.8544) using game-camera surface ray samples. The original FBX/material remain the fallback when the repaired mesh is unavailable. The focused D3D11 village review showed the divider inside the window in the sampled route views and checked it adds no collision. This is not an interior or transparent glass reconstruction; standalone player appearance and FPS were not tested.

## Near side-window curtain surface

Generated with the built-in image generation tool on 2026-11-12; `side-window-curtain-original.png` is copied byte-for-byte to `Assets/Resources/AlleyCompactBrickHouse/side-window-curtain.png`. SHA-256: `8312ec24a66336bb08df4c9a71138e2991b73f04f876382d714db488c0a7240e`.

Prompt: Use case: game texture. A single flat rectangular window GLASS SURFACE ONLY for the small second-floor window of an ordinary 1990s Korean neighborhood brick house, seen straight on from outside at dusk. Photorealistic subtle warm dim room light behind old translucent off-white curtains, a few gentle vertical fabric folds visible through slightly dusty blue-gray window glass; very faint natural reflections. Low contrast and dark exposure, restrained, no bright bulbs, no objects or people discernible inside. Entire image is one continuous glass pane surface; NO mullions, no frames, no wall, no window borders, no text, no letters, no logos, no perspective, no highlights along image edge. Intended to be mapped as texture behind an existing 3D window frame in a realistic horror game.

`AlleySideWindowFrame` adds a four-vertex surface over the measured glass rectangle while preserving its existing centre mullion. A clone of the existing warm PBR material uses the curtain as base and emission texture; no realtime light is added. The rest of the near house remains on its original dark material. If the curtain or template is unavailable, the original glass stays visible. Recreate the import using `HappyToy.V2.Editor.AlleyCompactWindowImport.ApplySideCurtain` (512px maximum, clamp, sRGB and mipmaps). The focused D3D11 village camera showed subdued curtain folds in the sampled view; checks cover material, texture size, mesh ownership and collision. This is a textured surface, not actual translucent glass or a modeled interior. Standalone player appearance and FPS remain unmeasured.
