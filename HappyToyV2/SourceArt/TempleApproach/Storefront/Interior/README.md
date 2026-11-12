# Reproducible walk-in shop candidate

This is a local derivative of the generated storefront already documented in
`../README.md`; no new 3D generator or third-party model was used. The original
`../StorefrontEditable.blend`, GLB, textures and generation provenance remain
unchanged. `Storefront-door-candidate-with-markers.blend` is an editable 4.2.23
Blender copy of the door-cut candidate; `Storefront-door-candidate.fbx` is its
Unity export. Its SHA-256 is
`51470ba6f88d80c5494399528c0239918a9a5b18c694b783a6c0bbe76ec2f6cd`.

From the repository root (with Blender 4.2.23 installed), run:

```
blender -b HappyToyV2/SourceArt/TempleApproach/Storefront/StorefrontEditable.blend --threads 1 --python-exit-code 1 --python HappyToyV2/SourceArt/TempleApproach/Storefront/Interior/cut_door_candidate.py
blender -b HappyToyV2/SourceArt/TempleApproach/Storefront/Interior/Storefront-door-candidate.blend --threads 1 --python-exit-code 1 --python HappyToyV2/SourceArt/TempleApproach/Storefront/Interior/export_door_candidate.py
```

The scripts decimate the existing mesh, remove the doorway faces and export
four empty position markers; they do not create a new base model. An offline
triangle/UV comparison found all 244,102 surviving triangles matched the
original 249,999-face game export within 1e-5. CPU front and oblique renders
show the opening and preserve the surrounding windows, but also reveal that
the scan has no authored interior. Unity's opt-in
`HappyToy.V2.Editor.ShopDoorUnityAudit.Run` verified matching prefab bounds and
the marker positions. The normalised opening is ~0.9875 m wide and 1.859 m
high; its world centre is (496.110, 0, 391.394) and the rear marker is
x=493.931. The current interior floor, walls, counter and human silhouette are
replaceable placeholders created by the game. This measurement does not prove
visual quality, physical clearance, FPS or native player rendering.

## Weathered horizontal shop sign

Generated with the built-in image generation tool on 2026-11-12. `woam-front-sign-original.png` is copied byte-for-byte to `Assets/Resources/AlleyStorefrontInterior/woam-front-sign.png`. SHA-256: `cd745fca0383a221c41963aa7202cdd105ff8a518f11e9a3ca0f4d525bedd326`.

Prompt: Use case: photorealistic game environment texture. A single long horizontal signboard image for an ordinary late-1980s/1990s South Korean neighborhood corner grocery, shown perfectly straight-on, flat and filling the entire rectangular image edge-to-edge. Old sun-faded warm ivory painted board, dirty weathered surface, subtle rain stains, cracked paint and age. In the center, large, legible, period-appropriate dark hand-painted Korean lettering exactly: "우암상회". Four Hangul syllables 우 암 상 회 in that order, evenly spaced and large, centered across the sign. No other text, no logo, no numbers, no borders, no graphics or illustrations, no surrounding wall, no perspective, no frame, no light glare. Understated realism; the sign should be readable in a small in-game view.

`AlleyStorefrontFrontSign` places a non-colliding surface on the measured front fascia of the walk-in storefront only. Its central section preserves the image aspect ratio while the blank ends stretch to fit the fascia. It uses the existing paper PBR template and no additional light. `HappyToy.V2.Editor.AlleyStorefrontSignImport.Apply` imports this one image at up to 1024px. Missing source/template preserves the earlier storefront. Two previous cube-shaped bulb placeholders are now small non-colliding globes and sockets; their existing point lights are unchanged.

The focused D3D11 village review inspected the start viewpoint and a test-only repositioned close-up. Temporary mesh rays checked the sign against the actual storefront fascia at three positions; tests also checked the bulb/socket connection and lack of colliders. A standalone player and FPS were not measured.
