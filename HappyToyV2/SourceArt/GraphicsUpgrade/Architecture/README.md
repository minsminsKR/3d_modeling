# Authored corridor architecture

Run Blender **4.0.2** in the Unity project directory:

```powershell
blender --background --python SourceArt/GraphicsUpgrade/author_corridor_architecture.py
```

[Official portable Blender 4.0 archive](https://download.blender.org/release/Blender4.0/)
provides `blender-4.0.2-windows-x64.zip`. Verified official SHA-256:
`bddb7ab6880bbb80297b93731506a48bbebdce214ee5edd17dbef8380daecded`.
The portable tool itself belongs outside project source control.

All eighteen `.blend` files are editable original sources. They retain bevel and
weighted-normal modifiers. The exporter applies them for the FBX, preserves
measured UVs and normals, and exports metre units without cameras, animation or
colliders. `architecture-manifest.json` records model sizes, source/FBX hashes,
features and tool version. No imported third-party geometry is used.

Wall modules include profiled uprights and rails, separate lower boards,
recessed nail heads and slightly slack paper meshes with real thickness.
Floor courses have staggered butt joints and 0.65 mm cupping built into a closed
mesh. Five transverse samples in LOD0 and three in LOD1 retain the central valley;
the flat underside and board footprint preserve the support envelope, with no
top vertex above the Y=0 support surface. Bevels soften the perimeter without adding
ridges to the shallow top curve. Walking-board joints are 0.8 mm with a 0.5 mm perimeter bevel; ceiling
joints are 0.8 mm with a 0.4 mm bevel. Continuous timber subfloor spans Y=-0.029
to -0.025 m beneath the 22 mm boards, so joints expose wood rather than empty
space. Four-sample MSAA and SMAA cover these small edges at grazing view angles.
The source stays inside the original floor slab's collision envelope.
Boarded ceilings fit below the existing ceiling
collider. Door leaves, posts with decorative shoulders, profiled lintels and the
final classroom's undulating plaster and wainscot use the same authoring pipeline.
The rails and shoulders are decorative geometry; hidden structural mortises,
tenons and scarf joints are not modeled. Materials resolve to the
existing licensed PBR maps in `Assets/Resources/GraphicsPbr`; see graphics credits.

Unity's import validates axes and dimensions. Runtime architecture is combined
per cell/material with two LODs and frustum culling. It adds no physics components
and uses the original wall openings, cabinet markers and navigation. Monster
hierarchies and meshes are outside this authoring pass.

The current release passed the complete native input escape, 27-view native review,
recorded mix and ownership assays. See
[the scoped validation record](../../../Verification/corridor-realism/validation.json).
1080p GPU timing was measured on RTX 2060 SUPER; RTX 3060 itself was not measured.
