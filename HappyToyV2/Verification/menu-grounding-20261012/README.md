# Planted title Baeksugwi and original-surface eye glow

Follow-up to the red corridor title: the original renderer bounds contained a
conservative animation envelope and were not the posed foot surface. The title
now fits the actual skinned vertices, with no gameplay prefab or scene changes.

## Stance and lighting sources

`Assets/Scripts/CinematicTitleScene.cs` builds an owned stationary title pose
from the original Run_InPlace clip. Each of the twelve long support limbs takes
its planted segment from four original gait samples; the short secondary arms
retain their original elevated role. A small shoulder correction compensates
body sway between samples. The actual weighted distal hand vertices are used
for final grounding and for twelve soft contact-shadow footprints. Fitting uses
`EnemyStudioRig.PosedVertices/PosedBounds`, not Renderer.bounds. All twelve final
support gaps must be below 8 mm; the floor clearance target is 3 mm. The title
camera's model is moved in front of the doorway so the pose reads clearly.

`Assets/Resources/TitleAtmosphere.shader` places a very weak, bounded cool light
on the face and red emission on the original eye slits. Both use the original
Head joint's world-to-local matrix, so there are no floating replacement eyes.
The light does not illuminate the torso/corridor. Normal mode only varies eye
intensity slightly; reduced motion holds the intensity. The gait remains still.

Eye slit coordinates come from software triangle raycasts against the actual
posed source mesh. `Assets/Editor/TitlePoseReview.cs` renders an orthographic
inspection and outputs the Head-local surface coordinates. Run it with pinned
Unity 6000.6.0f1 using execute method
`HappyToy.V2.Editor.TitlePoseReview.Inspect` and `-quiet-diagnostics`.
Its optional probe images/logs are written to Builds/TitleGrounding-Work.

## Verification

`menu-face-report.json` is the actual release-player audit: twelve planted
supports, final surface/support floor gaps, face light and eye emission,
11 UI frames across four aspect ratios, settings, Escape pause/resume and
unobscured gameplay HUD. The two included PNGs are the actual final title
captures. `build-inputs.json` checks current Assets/Packages/ProjectSettings and
native build/credits hashes. `source-completeness.json` checks required editable
sources and hydrated LFS files after staging. These checks do not certify a
complete gameplay run or hardware performance.

The changes are committed locally. No remote push is performed for this work.
The previous verified player is preserved until a requested successful push
permits superseded-player cleanup.
