# Authored corridor details

Run Blender **4.0.2** from the Unity project directory:

```powershell
blender --background --python SourceArt/GraphicsUpgrade/author_corridor_details.py
```

Use the same pinned official Blender distribution described in
`../Architecture/README.md`. The generator is independent from the architectural
wall and board generator, uses repository-relative paths, and imports no geometry.
All 28 `.blend` sources retain editable edge bevel, weighted-normal or physical
paper thickness modifiers. Exported FBX files apply those modifiers and preserve
metre-scale material UVs and normals. `../details-manifest.json` records both file
hashes, triangle counts and the actual Unity-axis safe envelope of each model.

The 14 modules each have two mesh LODs: folded memory packet, entrance boards and
return plaque, passage upright, header, ceiling joist, lantern suspension and
tassel, curled ceremonial paper, framed room plaque, binding marker, returned
memory page, altar count plaque, photograph frame, school window recess and
dog-eared exercise leaf. Paper folds, curled corners, cord knots, bent iron hooks
and separate tassel strands are actual mesh geometry. Profiled timber shoulders
are decorative: concealed structural mortises or boolean-cut joints are not
claimed. The night window remains opaque against the existing solid wall.

Runtime presentation combines static corridor details per spatial cell,
material, shadow eligibility and LOD. The disappearing memory item owns its
packet's two LODs. Classroom detail models use the high-detail version, batched
per object/material, keeping precisely five independent returned-paper states.
Existing colliders, navigation, interaction markers, text and monster meshes
remain authoritative. Imported resources are immutable; combined meshes and
materials are disposed with their presentation owner. All material slots resolve
to the already tracked licensed PBR maps; see project graphics credits.

The importer requires every module and LOD, metre bounds, readable vertices,
normals, measured UVs and no physics components. Generation and these structural
checks are complemented by the current complete native input escape and 27-view
native review. See [the scoped validation record](../../../Verification/corridor-realism/validation.json)
for measured GPU cost, mix evidence and the limits of those observations.
