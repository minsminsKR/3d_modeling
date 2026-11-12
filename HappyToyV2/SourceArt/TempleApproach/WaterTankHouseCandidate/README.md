# Korean rooftop water-tank house

Built-in image generation reference, 2026-11-12. Reference SHA-256: `f8d337f63cb37a8c60f1e57d3e57bb6418d4b79c22f07fa4d71236537e9eadaa`.
The user-provided `../References/user-alley.png` was a construction and
atmosphere reference, not the model input. Exact image-generation prompt:

> Use the attached image only as a reference for the authentic inhabited 1990s South Korean residential alley, understated age and plausible building proportions. Create one isolated production 3D-modeling reference: an ordinary compact two-story 1970s-1980s Korean hillside neighborhood house, realistic and NOT Western and NOT hanok. Narrow rectangular faded warm-gray cement-plaster and weathered concrete structure, a flat low concrete parapet roof with ONE modest dark blue cylindrical rooftop water tank, a small simple metal rooftop access hatch, asymmetrically spaced Korean dark metal-framed rectangular windows, a plain faded brown metal entry door, subtle old stains and minor patch repairs. No balconies protruding far and no fantastic or decayed ruins. Neutral overcast daylight and detailed physically realistic texture, clear simple construction, full straight-sided building visible from foundation to roof and all of the water tank, orthographic-like three-quarter view showing FRONT and ONE SIDE and roof, sharp silhouette, correct upright verticals. The scene must contain exactly this one complete independent building only, on uniform light-gray background with no ground plane and no shadow merging into the silhouette. No street, no other buildings, no boundary wall, no gate, no wires, no trees, no vegetation, no people, no cars, no furniture, no letters, no signs, no logo, no cropped parts. This image is for a 3D generation service, so no extra elements and no montage.

Designated A100 request `97271867-3cdf-4876-8230-946fb78f8c06`, job
`53780944-f80b-4fc1-9d1b-ea48463ad23b`, TRELLIS.2 1024 cascade, 4K texture,
seed 73489, GPU 1, 12 steps and effective 49,152-token budget. Verified ZIP
SHA-256 `8f79423f60120d23085c05e43fd91f684ee162af38ecb515c0ccaea0bc9c7b04`;
original GLB SHA-256
`aba0578db817f6323a7dc257cf6d17b1e7f50cb31913d1cf5f8a30a43a7767fc`.
The job, original and processed inputs, request and result, original GLB,
original albedo and metallic/roughness maps, and download receipt are retained.

The same completed job was rotated in the designated interactive viewer to
inspect front, back, walls, roof and tank. Blender 4.2.23 retains the packed
972,407-triangle editable source and derives LODs sequentially: 250,000,
125,000, 60,000 and approximately 30,000 triangles. A direct 60k/30k reduction
visibly broke the walls and tank; the sequential 125k version retained a much
cleaner surface in CPU front/back previews and is the only runtime LOD applied.
The 125k model replaces a repeated background house behind the shop and reuses
the house placement, visual-only collision policy and original URP PBR maps.
Its orientation was compared in the actual D3D11 village camera; the two visible
elevations and rooftop water tank were checked. Focused village tests passed
for its mesh budget, original PBR channels, wall/shop/house separation and
absence of added collision. This is an Editor PlayMode review, not native-player
FPS or a whole-game regression; the additional mesh and 4K textures have not
been benchmarked on a target GPU.

Reproduce from the project root with Blender 4.2.23 `--background --threads 2
--python-exit-code 1 --python SourceArt/TempleApproach/WaterTankHouseCandidate/prepare_house.py`.
Copy `WaterTankHouseHigh.fbx` and `albedo.png` to
`Assets/Resources/AlleyWaterTankHouse/geometry-high.fbx` and `albedo.png`.
Extract the packed channel with `python SourceArt/TempleApproach/extract_gltf_metallic_roughness.py SourceArt/TempleApproach/WaterTankHouseCandidate/model.glb SourceArt/TempleApproach/WaterTankHouseCandidate/metallic-roughness.png`,
convert it with `python SourceArt/TempleApproach/convert_gltf_material.py SourceArt/TempleApproach/WaterTankHouseCandidate/metallic-roughness.png Assets/Resources/AlleyWaterTankHouse/metallic-smoothness.png`,
and run Unity editor method `HappyToy.V2.Editor.AlleyWaterTankHouseImport.Apply`.
The other LOD FBXs are reproducible alternatives, not runtime dependencies.
