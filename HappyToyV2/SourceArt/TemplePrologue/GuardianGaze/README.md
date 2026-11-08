# Prologue guardian head rig

This is a prologue-only, two-bone skin of the existing generated corridor
`guardian-statue` LODs. The preserved model, its reference, generation settings,
textures and license are in `SourceArt/CorridorEntrance/guardian-statue/` and
`SourceArt/CorridorEntrance/LICENSE.md`. The original corridor asset is unchanged.

`guardian-gaze.blend` is an editable rig with the two source-derived meshes,
head/body weights and an external, repository-relative reference to the existing
stone texture. `prepare_guardian_gaze.py` recreates it and the two Unity FBXs
from the original editable source. Run with the project-pinned Blender 4.0.2:

```
blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/TemplePrologue/GuardianGaze/prepare_guardian_gaze.py
```

The recipe keeps the raised staff's connected upper island in the rigid body,
and blends the neck's head weights from source Z 2.20 to 2.38 metres. The
prefab code reuses the existing stone material and the two original LOD switch
thresholds. Both LOD head bones receive the same rotation. The gaze begins only
when the second incense has actually been released into the vessel; it stops
advancing during pause and resumes from the checkpoint's existing phase and
incense-placement state. The torso and collision are not animated.

`temple-guardian-gaze-logic` checks two days of incense interaction, no movement
before the second release, moving-player gaze, stationary torso, pause and
checkpoint restore. `temple-guardian-gaze-review` takes isolated offscreen
before/after game-camera frames, including close views. The currently authored
horizontal neck range is ±85 degrees and the vertical range is -20 to +30
degrees; a player behind those limits cannot be looked at directly. These
checks are not a displayed FPS or actual-device performance measurement.
