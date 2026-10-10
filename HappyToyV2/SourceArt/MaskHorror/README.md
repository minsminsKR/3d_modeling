# Grotesque running mask wraith

Original authored geometry and new procedural surface bakes, released under
CC0-1.0. No downloaded creature, face scan or reference asset is embedded. The
initial generator's tracked PBR references retain the existing credits in
`ThirdParty/Graphics`; final skin/porcelain atlases come from original Blender
procedural sculpt materials rather than those large repeated surface patterns.

Blender **4.0.2** is the pinned tool. No add-ons or third-party Python dependencies
are required. From the Unity project directory:

```powershell
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/author_mask_horror.py
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/sculpt_and_bake.py
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/export_body_lod.py
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/finish_surface_bakes.py
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/validate_sources.py
```

Blender's executable may be replaced with its installed path. All resource paths
are derived from the generator's repository location. The two editable `.blend`
files preserve the individual shell, ribs, claws, veil and ligaments, the actual
voxel-fused limb anatomy, low-detail body LOD and hidden editable two-level
multires sculpt collection. Pore/raised-scar displacement remains editable on
the high-resolution sources. Relative texture links resolve to the tracked
initial PBR references and final `Assets/Resources/MaskHorror/Textures` atlases;
pull/hydrate the entire repository before opening. Unity builds FBX files without
Blender installed. Their tracked import metadata keeps geometry readable, imported
transforms and four limb pivots, and excludes physics, lights and animation tracks.

Optional silent studio previews go outside the source repository:

```powershell
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/render_sculpt_review.py -- --output <absolute-review-directory>
```

The body has a crooked vertebral column, separate irregular ribs, open thorax,
asymmetric bent arms, swollen knuckles, oversize hooked fingers/nails,
reverse-bent knees and a cut,
double-sided torn shoulder veil. The thick curved mask has **real open sockets
and mouth**, irregular old teeth, recessed tiny red eye slits, a volumetric
forehead/cheek/jaw, modeled nasal bridge and torn gum,
lifted forehead lamina and hanging cheek ligaments. Aperture edges are smoothed
independently so they stay hollow instead of becoming a solid flat billboard.

The 2048x2048 unique UV atlases include actual Cycles color, tangent normal,
ambient occlusion and roughness bakes, plus packed metallic/smoothness. Color,
AO and roughness use direct surface bakes from the procedural sculpt materials
to avoid projection misses on thin torn edges. Normal detail is baked from the
actual multires/displaced source; only pure-black failed ray/background texels
are made neutral, with no fabricated replacement normal detail. These are
original procedural authored surfaces, not photogrammetry or a scanned person.

Source construction is in metres, Y up and front -Z, following the existing
CorridorFurnishings conversion. The imported resources face **local -Z**, while
navigation actors face **local +Z**. Runtime rotates only the three new visual
wrappers (body, body LOD and mask) 180 degrees around Y before attachment; fitted
centering applies that same rotation. Original FBX child transforms, rig and
navigation root remain intact. Actual nose/teeth/rib vertex landmarks verify
that the modeled front leads the actor's heading. `model-manifest.json` records actual source vertex bounds, counts,
material contracts and hashes. Mesh assets contain only visuals.

`MaskHorrorVisual` attaches the authored geometry beneath the existing `body` and
`mask` pivots and retains the imported wraith rig and running clip. Original body
and mask renderers are hidden to avoid duplicate limbs/old eyes; their source,
rig, clip and gameplay attachment anchors remain intact. The growing
body remains owned by `LanternMaskEncounter`, including its five-second school
transformation, visibility and late mask attachment. The original rig is fitted
to 1.61m and the new mask to .72m; the combined silhouette stays below the lowest
2.44m lintel. The actual arm geometry is fitted to 2.44m of the 2.8m hall. Neither
navigation root scale nor its capsule is expanded. A 12,368-triangle body LOD
retains the same atlas and four limb pivots. Limb swing uses actual navigation
velocity and the 1.35m audio contact stride (two contacts per cycle), scaled
time and the comfort setting; pause freezes the pose. Tiny emitted eye surfaces
are rebound to the existing `MonsterRedEyes` ownership, anchors and mute toggle.

Studio renders establish modeled geometry and material review only. Production
camera images, live bounds while running and original root/navigation invariants
must be verified in Unity. All automatic game/audio validation remains muted by
the project quiet-diagnostics guard.
