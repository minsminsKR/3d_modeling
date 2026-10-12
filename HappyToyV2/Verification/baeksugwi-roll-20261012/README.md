# Pronounced irregular Baeksugwi head roll — 2026-10-12

The former head action used a regular 2.4-second yaw wave with only 0.04 radians
(2.3 degrees) of roll. The replacement is a 31.7-second head-only action with
69 authored beats and approximately -53.2 to +53.6 degrees of lateral roll.
Its deterministic seed preserves reproducibility while the unequal target
angles, hold times and transition times avoid a short, regular sway.
Fast cocks take 0.09–0.22 seconds; slower turns take 0.38 or 0.74 seconds.
Holds last 0.12–1.15 seconds and include small, smoothly gated tremors.
The action ends in a still neutral pose and closes without a rotation jump.

The packed editable `SourceArt/BaeksugwiStudio/Baeksugwi_Run.blend`, armature-only
`Head_Writhe.fbx`, independent Unity `Head_Writhe.anim`, profile, schedule
generator and reproduction tools are included. Both original Run action hashes
are unchanged. Geometry, skin weights, original GLB/PBR and the base game model
retain their existing sources. The original preserved school scene is untouched.

## Verification

- `import.json`: actual Unity import, four rotation-only curves, 31.7 seconds.
- `playmode.xml`: nine actual Unity 6000.6.0f1 cases passed. This includes strong
  positive/negative roll, unequal low-speed holds and fast transitions, the
  existing head-only/loop contract, Chase/pause/calm, the floating school mask,
  title/reduced-motion and original model/locomotion regressions.
- `source-envelope.json`: read-only Blender skin evaluation at every authored
  cock target; none drops below the neutral source envelope. This is separate
  from actual Unity corridor-floor validation.
- `baeksugwi-head.json`: PASS across 192 real player captures. Title roll
  reaches -51.54/+52.95 degrees; Chase reaches -51.96/+51.97 degrees. Non-head
  joint rotation/position errors are zero. Minimum captured world-space surface
  height is 0.0100 m, and every Chase frame passes the corridor floor/roof check.
  Pause freezes the head and leaving Chase stops it. The contact sheet was
  visually reviewed for the pronounced left/right cock and fixed body silhouette.
- `diagnostic-audio-silence.json`: process-only mute verified; original mute
  and user preferences successfully restored at exit; listener kept zero.
- `build-inputs.json`: GRAPHICS_BUILD_INPUTS_PASS for the new Windows release
  player and current complete Assets/Packages/ProjectSettings input graph.

The first release build ran out of disk space. This task's temporary portable
Blender installation/archive was removed after authoring; the successful retry
and its manifest are the delivered player. PlayMode-generated performance-test
Resources JSONs are transient test outputs and were removed before the final
build. They are not required source assets. Optional raw captures, GIFs, isolated
profiles and build logs remain under ignored Builds rather than source commits.

## Reproduce

Use Blender 4.0.2 and Unity 6000.6.0f1; pinned source dependencies and original
asset provenance are documented in `SourceArt/BaeksugwiStudio/README.md`.
From repository root:

```powershell
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/BaeksugwiStudio/author_head_motion.py
blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/Tools/quality/inspect_baeksugwi_head_pose.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.BaeksugwiHeadImport.Apply
unity test HappyToyV2 --editor-version 6000.6.0f1 --mode PlayMode --filter 'BaeksugwiHead;BaeksugwiTitleHead;StudioWraith;StudioImportedRun;StudioRunMoves;StudioCorridorClone' --output Verification/baeksugwi-roll-20261012/playmode.xml
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/Baeksugwi-IrregularRoll-20261012
```

Run the newly built player with `-quiet-diagnostics -v2-baeksugwi-head-output
<absolute-output-directory>`. The diagnostic now covers a full action in both
the actual title UI and a stopped real corridor Chase, with 96 captures each.
It requires strong roll in both directions, fixed body joints, corridor floor/
roof clearance, pause and calm gating. This controlled fixture does not certify
a complete survival playthrough. Raw captures and isolated profiles are optional
outputs and belong under the ignored Builds directory.

Use `python HappyToyV2/Tools/quality/preview_baeksugwi_head.py <output-directory>`
to assemble silent native GIFs; the contact sheet selects the largest lateral
tilts instead of showing arbitrary early frames. This preview uses Pillow 10.2.0.
