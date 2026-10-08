# Original-eye emission and moving door pulls

The glow follows the original eye material surfaces. Cyclopse retains its one
central eye; Uncat, Hwacat, Baby, the mannequin and lantern mask retain their
original two eye regions. No replacement eyeball, dome, disc, button, light or
new eye mesh is authored. The original BaseMaps, normals, meshes, UVs and rig
remain unchanged. The pupil/iris or stone-eyeball centre receives exactly zero
emission. The lantern mask has genuinely empty apertures, so only its original
porcelain lips/visible inner walls receive the mask; no centre surface is added.

`eye_regions.py` is the editable anatomical authoring source. Its ellipses and
aperture polygons are measured on the existing optional 800x800 source face crops.
`build_surface_emission.py` opens the tracked editable refinement .blend sources
or original static FBX files read-only. It rasterizes the anatomical annulus into
the existing mesh's UV triangles, tests frontmost visibility, and preserves UV
islands/seams. It never saves a source .blend or modifies original albedo images.
The output grayscale RGB emission masks are 2048x2048 and use face material slot0.
The annulus uses a smooth radial intensity ridge, broad smooth edge fades and
restrained low-frequency variation around its arc. It has no solid-white plateau.
Baby's larger stone eye regions receive a softer/lower scalar. The original
pupil/iris/white eyeball exclusion regions remain fully clear and unchanged.

Resources are `Assets/Resources/ThreatEyes/<key>-profile.json` and
`<key>-emission.png`. JSON is compatible with Unity JsonUtility: `version=2`,
`key`, `maskResource`, `materialSlot=0`, and `eyes`. Each eye has `uv{x,y}`,
`sourceMesh`, `materialSlot`, `uvBoundarySamples[]`, `sourceWorldRadius`,
`sourceWorldHeight`, source normal/anchor/centre vectors, screen region shape,
clear-region extents, `centreHasSurface` and `aperture` (the inverse of that field).
Runtime reads `aperture=true` for the mask and false for all natural eyes.
Shader integration must retain the
original `_BaseMap` and its colour; use the mask for `_EmissionMap` with a moderate
red `_EmissionColor`. Import masks as linear RGB, opaque, uncompressed 2048 data.

For natural eyes the anchor UV hits the original central pupil/eyeball/dark eye
cavity, with mask value exactly zero across its bilinear footprint. The outer
boundary UVs measure the existing anatomical ring. `sourceWorldRadius /
sourceWorldHeight` provides a source-scale-independent fallback ROI radius;
resolving the actual posed UV boundary provides the most precise runtime ROI.

For LanternMask, `centreHasSurface=false`: the anchor UV is a real lower-lip hit,
because no valid UV exists at an empty hole centre. Average the resolved boundary
world positions for its aperture-centred ROI, rather than measuring a pupil disk
at the lower lip. `sourceCentreWorld` documents the source aperture centre and
`sourceAnchorWorld` the actual lip hit. Apertures require original topology/BaseMap
preservation checks; iris-contrast metrics do not apply to an empty hole.

The four source rigs contain `mixamorig:Head` but no eye bones. Updated
`inspect_head_sources.py` and `inspect_static_faces.py` record the corrected
original-eye hits in their JSON inventories. Source crops are placement aids,
not native Unity visual acceptance. No workstation paths are required.

Pinned tools: Blender **4.0.2** (bundled bpy/mathutils/NumPy) for authoring,
Python **3.11** and Pillow **10.2.0** for validation, Unity **6000.6.0f1** for the
runtime integration owned by the game code. Run from the HappyToyV2 project root:

```powershell
blender --background --python-exit-code 1 --python SourceArt/ThreatEyes/build_surface_emission.py
blender --background --python-exit-code 1 --python SourceArt/ThreatEyes/inspect_head_sources.py -- --output SourceArt/ThreatEyes/eye-surface-anchors.json
blender --background --python-exit-code 1 --python SourceArt/ThreatEyes/inspect_static_faces.py -- --output SourceArt/ThreatEyes/static-eye-surface-anchors.json
python -m pip install -r SourceArt/ThreatEyes/requirements-validation.txt
python SourceArt/ThreatEyes/validate_visual_sources.py
```

Both generator/inspectors accept `--resource-project` for read-only source review;
the generator accepts `--output-project` for mirrored staging. Outputs preserve
their existing Unity .meta GUIDs on regeneration. Optional `--preview-dir` source
crops belong outside source commits. Commit the generators, region definitions,
measurements, masks, profiles, .meta files, source inventory and licenses together.
The referenced .blend/FBX sources and original attribution remain tracked project
dependencies. Native close/distance/torch-on/off and opaque occlusion views must
verify texture readability, correct original eye count, anatomical placement and
release shader variants. Source checks alone cannot certify the final appearance.

Door hardware continues to use `CorridorDoorHardware.Attach(owner,leaf,width,
height,thickness,pullEdge)` on the actual primary/secondary moving leaf. Its raised
U pulls, screw heads and finger wells reuse the existing detailed authored
`SourceArt/GraphicsUpgrade/Props/door-hardware.blend` and generator. Both face
pulls follow opening/restoration. This eye-authoring revision does not change the
door, its physics, IDs, NavMesh or hardware implementation.
