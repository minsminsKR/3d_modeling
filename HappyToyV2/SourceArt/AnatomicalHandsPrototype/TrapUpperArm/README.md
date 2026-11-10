# Connected upper arm for trap placement

The upper-arm surface is cut from `GEO-body_male_realistic` in the existing
`../blender-human-base-meshes-v1.4.1.zip` (Blender Studio human base, CC0;
see the parent README for provenance). It is joined to the existing
`../hand-rig-corrective.blend`: the open forearm and upper-arm rings are
bridged, with two intermediate skinning bands. The hand, its finger bones,
corrective shape key and existing skin atlas remain in use. The independent
`upperarm` bone is synchronized with the forearm at runtime; during floor
placement only, it is aimed and stretched toward the lower screen edge.

`hand-with-upperarm.blend` is the editable final source. Rebuild it with
Blender 4.2.23 from the repository root, with an empty scratch directory:

```
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/AnatomicalHandsPrototype/TrapUpperArm/extract_upperarm.py -- SCRATCH
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/AnatomicalHandsPrototype/TrapUpperArm/finish_upperarm.py -- SCRATCH
blender -b -t 2 --python-exit-code 1 --python HappyToyV2/SourceArt/AnatomicalHandsPrototype/TrapUpperArm/attach_upperarm.py -- SCRATCH
```

The resulting `bridged-smooth-upper-arm.blend` has the same semantic mesh,
UV, weight and shape-key data as the saved source (SHA-256 of the inspected
data: `2fbf4f5af5a8b89540ab27a2f845a28d4b1e1020edc8257f25a632dc86e18346`).
FBX container bytes can differ between Blender exports. Use the parent
`export_candidate.py` and `install_candidate.py` to regenerate the runtime
asset, then reimport the hand prefab with the project import pipeline.

An opt-in 20 Hz PlayMode capture uses the actual game and hand cameras at a
45-degree downward view; it is a layered composite without HUD, not a native
player capture. The floor position, release and blocked-placement rules were
also tested. The arm no longer terminates in mid-screen in that view, but the
long reach still stretches the arm and the elbow skin transition remains
visible. This is not a full body IK solution or an approval of all camera
angles. Historical surface reports in the parent folder apply to the geometry
hash recorded in each report, not automatically to this new mesh.
