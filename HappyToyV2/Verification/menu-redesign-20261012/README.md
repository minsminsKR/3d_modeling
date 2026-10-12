# Red corridor title menu — 2026-10-12

The requested composition uses a small chapter/title block above compact outlined
navigation on the left, with the actual shipped Baeksugwi on the right in a dark
red corridor. Selection uses one real focus shared by mouse movement, keyboard
and gamepad. Context descriptions, separate corridor/school entry and resume,
settings, history and protected-save warnings retain the existing game actions.

## Editable sources and dependencies

- `Assets/Scripts/GameShellTitleView.cs`: UI Toolkit layout, shading and focus.
- `Assets/Scripts/CinematicTitleScene.cs`: owned title camera, corridor set,
  materials, animation sampling and RenderTexture lifetime.
- `Assets/Resources/TitleAtmosphere.shader`: textured title lighting and red
  depth haze, independent of the gameplay sun, ambient/reflection and fog.
- `Assets/Resources/Baeksugwi/Baeksugwi.prefab` and `Run_InPlace.anim`: existing
  generated creature and animation; editable sources and provenance remain in
  `SourceArt/BaeksugwiStudio`. No new base model is generated for this menu.
- Existing `GraphicsPbr/plaster-damp`, `GraphicsPbr/metal-rust` and
  `Corridor/aged-floor-v2` textures retain their source and license records.

The visual set is disabled outside the title. Reduced motion holds the pose;
normal mode samples slight motion at 15 fps. The stage owns temporary materials,
objects and render target and releases them on destruction. The authored school
scene and gameplay monster prefab are preserved.

Use Unity **6000.6.0f1** with the committed Packages/ProjectSettings. Follow the
project README clone/LFS setup. Build from the repository project directory with
`HappyToy.V2.Editor.QualityValidation.BuildWindows`, `-v2-release-player` and
`-v2-build-output`; this retains scene checks, credits and full input hashes.
For automated runs include `-quiet-diagnostics`.

## Verification

`playmode-final-ui.xml` and `playmode-saved-slots.xml` record the final menu
input/layout and separate saved-run checks. Bulky image payloads are removed
from test output; assertions/outcomes remain. `menu-face-report.json` is the
release-player check for 11 snapshots, four aspect ratios, settings submit,
Escape pause, focused resume, one selection and an unobscured gameplay HUD.
`title-normal-1600x900.png` is the final actual native title capture.
`build-inputs.json` checks all current Assets/Packages/ProjectSettings hashes
and the fresh Windows player/credits. These are menu/build checks; they do not
claim a complete survival playthrough or hardware performance certification.
