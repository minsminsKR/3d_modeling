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
