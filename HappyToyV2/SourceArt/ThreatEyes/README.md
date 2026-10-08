# Paired threat eyes and moving door pulls

The four refined stalkers, lantern mask and mannequin have two opaque blood-red
cores in measured face locations. Core domes and charred rims are authored by
`Assets/Scripts/MonsterRedEyes.cs`; this code is the editable geometry source.
There are no point lights, spot lights, billboards, transparent overlay halos,
new shaders or downloaded assets. URP's existing bloom profile supplies the halo.
The front-only core geometry writes normal depth and is occluded by real walls.
A small original radial heat texture is generated in `CoreMaterial`: the inner
pupil is hot red and the curved edge fades to deep red. This avoids a uniform
flat-button appearance. The editable C# is the texture/geometry authoring source.

The four Mixamo models contain a Head bone, but no eye bones. Face UV points are
ray-hit from the existing editable refinement sources. At runtime a single
readable BakeMesh snapshot resolves their posed world position and normal. Both
anchors become children of the animated head before presentation enlargement.
The snapshot uses the same verified `BakeMesh(mesh,true)` convention as
`EnemyVisualRefinement.BakedBounds`; no large skinned FBX import becomes readable.
The original Cyclopse has one painted eye. Its two ritual cores flank that
orbital band, fulfilling the requested pair without changing its face mesh/UVs.

The mask contains actual eye holes. Lower/upper lip UV pairs determine aperture
centres and a shared face plane. The doll's cores are on its existing eye sockets.
`ThreatEyeModelImport.Ensure` changes only these two static FBX imports to readable,
permitting one startup sample of their actual mesh. The existing movement,
visibility, introduction, scaling and mask attachment continue to own their face
pivots and automatically carry the new children.

Integration points:

- `EnemyVisualRefinement.TryApply`: `MonsterRedEyes.Attach(replacement.transform,key)`
  after mesh/material/pose fitting, before retiring the original.
- `WeepingAngelEncounter.Awake`, after visual enlargement:
  `if (visual) MonsterRedEyes.AttachStatic(visual,"Mannequin");`
- `LanternMaskEncounter.Awake`, after the mask model resize:
  `if (mask) MonsterRedEyes.AttachStatic(mask,"LanternMask");`
- `HauntedCorridorPresentation.DressSlidingLeaves` replaces the small flat-fit
  hardware with `CorridorDoorHardware.Attach(door,door.movingLeaf,2.6f,2.36f,.12f)`.
  Its returned transform is registered in the existing presentation cleanup list.

`CorridorDoorHardware.Attach(Interactable owner,Transform movingLeaf,float widthMetres,
float heightMetres,float thicknessMetres,int pullEdge=1)` uses metre dimensions,
with X width/Y height/Z thickness and a centred moving-leaf pivot. It returns a
component exposing `Owner`, `Leaf`, `FaceRoots`, `LeafDimensions` and `Prepared`.
The actual leaf's scale is countered in the new visual child. Both pulls move and
restore with the leaf. Raised U handles, screw heads, inset finger wells and
slotted escutcheons reuse the existing detailed `door-hardware` asset and editable
`SourceArt/GraphicsUpgrade/Props/door-hardware.blend` source. Backings use the
existing measured brass PBR material and procedural bevel geometry. No collider,
obstacle, interactable, door state, stable ID or NavMesh settings are added.

From a clone, use Blender **4.0.2** and the project's Unity **6000.6.0f1**. No Python
packages beyond Blender's bundled bpy/mathutils are needed for measurements.
Commands below run from the HappyToyV2 project root, with the executable on PATH:

```powershell
blender --background --python-exit-code 1 --python SourceArt/ThreatEyes/inspect_head_sources.py -- --output SourceArt/ThreatEyes/eye-surface-anchors.json
blender --background --python-exit-code 1 --python SourceArt/ThreatEyes/inspect_static_faces.py -- --output SourceArt/ThreatEyes/static-eye-surface-anchors.json
python SourceArt/ThreatEyes/validate_visual_sources.py
```

Optional `--preview-dir` emits source studio crops outside committed source. These
crops are placement aids, not native Unity visual acceptance. No source .blend
is modified or resaved. Source inputs, UV measurements, runtime generator code,
material-template dependency and licenses must be committed with this work.

Unity PlayMode checks:

- `EveryHostileFaceHasTwoOpaqueHeadBoundRedCoresWithoutAdditionalLightsOrPhysics`
  checks all six profiles, paired head inheritance, red emission, release-retained
  keyword combination, opaque depth/backface culling and absence of new physics/lights.
- `RaisedDoorPullsStayOnBothMovingFacesDuringOpeningAndCheckpointPoseRestore`
  opens actual corridor leaves, verifies both face pulls follow, restores their
  saved pose, and checks IDs/colliders/NavMesh remain intact.

Final visual acceptance requires native release views of all six faces with torch
on/off, including a distant front/side view, plus handles from both sides of open
and closed leaves. Automated component checks do not certify anatomical placement,
bloom strength, wall occlusion or the final horror presentation.
