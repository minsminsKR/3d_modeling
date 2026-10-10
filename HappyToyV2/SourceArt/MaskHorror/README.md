# Many-handed corridor mask wraith

This original Blender model interprets the user's private visual reference as a
low, long creature with eight pairs of human arms and five-finger hands, dark
mottled wet flesh, intertwined black cords, modeled greasy hair strands and
hollow black-bronze bells. Its ivory human mask has volumetric cheeks, a nasal
bridge/tip, a complete chin, two actual narrow eye apertures and a crooked open
smile with ten individually modeled yellow incisors and a recessed dark mouth.
The shell has physical thickness around every aperture.

The private reference is **not redistributed and is not relicensed**. Its SHA256
is `0667ecacad32d2904108bc26f79616732babcf66f4d825253a3720c2d7190295`.
The original generated meshes, procedural materials, bakes and authoring code
are CC0-1.0. This is authored procedural art, not a person scan or photogrammetry.
No online service, downloaded reference asset or third-party add-on is required.

Blender **4.0.2** is pinned. From the Unity project directory, the complete
reproducible authoring, 2K bake, LOD and independent validation chain is:

```powershell
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/regenerate_reference.py
```

Replace `blender` with an installed 4.0.2 executable if necessary. All required
project/source/texture paths derive from the scripts' repository locations.
The manifest preserves source/FBX/map hashes, actual vertex bounds, counts,
landmarks, private-reference provenance and material contracts.

The stages are `author_manyhand_reference.py`, `sculpt_and_bake.py`,
`finish_surface_bakes.py`, `export_body_lod.py` and `validate_sources.py`.
To refresh maps/UVs/exports while preserving existing fused anatomy and high
sculpts, use the tracked `rebake_reference.py` entry point with the same Blender
command. Both paths use the same prioritized atlas pack: skin occupies a
dedicated 72%-width region, face shell 74%, and hair/cord/bells use separate
remaining regions. Actual skin/face UV area must exceed .25 of the full atlas;
the independent validator checks this density, actual channels and hashes.
The old `author_mask_horror.py` name is a compatibility entry point to the current
eight-pair geometry; it cannot recreate the superseded upright two-arm model.
That earlier model remains recoverable in Git history. The old normal-repair
entry point now performs read-only validation because the current generator
authors outward normals.

Each editable `.blend` contains the exported low geometry and a hidden separate
source collection with genuine Blender multires subdivision. Skin and face use
two subdivision levels; small hair/cord/teeth/bells use one. Skin high geometry
retains editable scar/fold and pore displacement. Each high object keeps its
original procedural material separately from the low atlas preview materials.
Human arms, fingers, wrists and anatomical torso forms are fused with Blender's
voxel remesh, relaxed and decimated while preserving separate animation pivots.
The original octagonal construction planes are then rounded with the tracked
`relax_skin_surface.py` volume-preserving Laplacian stage. Explicit zero-weight
pins preserve every hand/finger vertex and all contact pivots; weighting blends
the wrist transition. Low topology/UVs remain unchanged, and the editable high
base receives identical coordinates before a genuine tangent-normal rebake.
Skin roughness varies .28–.70, retaining wet patches and softer dry tissue.
Both sources remain below GitHub's 100 MiB regular-file limit.

The unique 2048x2048 UV atlases are actual Cycles bakes: diffuse color,
ambient occlusion, roughness, selected-high-to-low tangent normals and metallic.
Diffuse/AO/roughness use direct low-surface shader baking to avoid color
projection misses. Only pure-black missed normal rays/background become a
neutral tangent normal; valid sculpt normal detail is preserved. The packed
metallic/smoothness texture uses **R=actual metallic EMIT bake, A=1-roughness**.
Black-bronze bells retain their .78 source metallic value; flesh, mask and hair
remain nonmetal. Color imports as sRGB; normal/AO/roughness/packed channels are
linear. All maps and relative source links are tracked; no local cache is needed.
The narrowed black eye backing surfaces remain rough and nonemissive even when
the game's other monsters use the global red-eye option.

The runtime visual fit is 3.95 m long, 2.26 m wide and 1.64 m high for the body.
The face shell alone fits to .74 m high; long hair and neck bells are excluded
from that face measurement. The assembled creature remains below the 2.44 m
hall lintel. Source construction is metres, Y-up, front -Z. The imported FBX
root/child transforms are preserved, and only new wrappers receive a 180 degree
Y rotation to align the modeled front with the actor's +Z heading.

Eight `Segment00`–`Segment07` visual pivots trail behind the existing navigation
root. The front `HeadSocket` remains within .25 m of that root in XZ, rather
than centering the original capsule on the long body's bounds. `FaceJoint`
attaches the mask to that anatomical front-neck socket. `FaceFront`,
`FaceRear`, `HeadFrontTarget` and nose landmarks record the true modeled
orientation. Each segment has `ArmSwing00L/R` through `ArmSwing07L/R` with
a corresponding `HandContact` point. The independently exported body LOD
preserves all these pivots, atlas UVs and the same silhouette at lower topology.

`MaskHorrorVisual` hides the old body's/face's renderers while retaining the
original source FBX, rig, animation clip, root, NavMesh agent and capsule. Its
visual-only trailing segments follow actual root path history through turns;
measured imported rest rotations and ground offsets are preserved. Arm gait
uses actual agent travel distance and the existing 1.35 m audio contact stride.
Ground compensation uses actual world contact height and world up, respecting
FBX axis conversion. Scaled-time pause freezes the pose, and comfort mode reduces
swing. The original five-second school growth and anatomical late attachment
remain in `LanternMaskEncounter`. The narrow eye renderers bind through
`MonsterRedEyes.BindAuthoredStaticEyes(eyes, false)`, retaining black eyes and
existing ownership/anchor cleanup without changing other monster profiles.

Silent optional review images go outside the source repository:

```powershell
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/render_reference_review.py -- --output <absolute-review-directory> --samples 32
blender --background --python-exit-code 1 --python SourceArt/MaskHorror/validate_sources.py
```

Studio views assemble the body and face using the same real landmarks and face
shell measurement. They establish geometry/material review. Live game-camera
images, animated ground bounds, actual corner following and unchanged original
navigation/physics still require Unity verification. Automatic game/audio
validation uses the project's muted diagnostics guard.
