# Old Korean tiled-roof house

The built-in image generator created `reference.png` from the exact prompt in
`prompt.txt` on 2026-11-12. The reference image was generated for this model;
the user's alley image remains the overall atmosphere reference. The designated
A100 service generated request `353832be-3026-4a77-abd3-25a853e3e675`, job
`aa95bc0d-813c-477c-b662-40e90fb09c8b`, using TRELLIS.2 1024 cascade,
4K texture, seed 82943, GPU 1 and 12 steps. The verified download archive had
SHA-256 `cebe5d9213fae63fcc899d936c95e47a74f65e5d970b026ff4541395f5b8e52f`;
the preserved original GLB has SHA-256
`c88d4eabab1c4d8c3e5b6b8efa8aafd689db64925d09dc986dc805785d520f33`.
Inputs, processed input, generation records, original GLB/PBR and the verified
receipt are retained here; machine-specific source and archive paths in the JSON
have been replaced with repository-relative names. The completed original was rotated in the designated
web viewer to review its front, side, back and roof.

`TiledHouseEditable.blend` contains the packed original (961,642 triangles).
`prepare_house.py` uses Blender 4.2.23 to normalize the building and reduce it
sequentially to 250k, 125k, 60k and 30k faces. CPU preview comparison showed
visible damage at 60k; the runtime therefore uses the 125k version. The house
replaces a repeated background house behind the shop. Its roof is visible over
the courtyard wall from the village camera; much of its facade remains hidden.
It has no collision. The focused D3D11 village PlayMode test checked camera
views, model budget, PBR channels, house separation and unchanged visual-only
collision. Native-player appearance, GPU frame time and target-hardware FPS were
not measured. This does not establish reference-level fidelity for the village.

Reproduce from the project root:

```powershell
blender -b --threads 2 --python-exit-code 1 --python SourceArt/TempleApproach/TiledHouseCandidate/prepare_house.py
Copy-Item SourceArt/TempleApproach/TiledHouseCandidate/TiledHouseHigh.fbx Assets/Resources/AlleyTiledHouse/geometry-high.fbx
Copy-Item SourceArt/TempleApproach/TiledHouseCandidate/albedo.png Assets/Resources/AlleyTiledHouse/albedo.png
python SourceArt/TempleApproach/extract_gltf_metallic_roughness.py SourceArt/TempleApproach/TiledHouseCandidate/model.glb SourceArt/TempleApproach/TiledHouseCandidate/metallic-roughness.png
python SourceArt/TempleApproach/convert_gltf_material.py SourceArt/TempleApproach/TiledHouseCandidate/metallic-roughness.png Assets/Resources/AlleyTiledHouse/metallic-smoothness.png
```

Then run Unity editor method `HappyToy.V2.Editor.AlleyTiledHouseImport.Apply`.
The other derived FBXs are reproducible alternatives and are not required to
run the game.
