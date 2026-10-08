# Cyclopse menu wallpaper

The title, pause and related menu pages use the project's single-eye Cyclopse
portrait, with full-viewport centred cover fit. The original 1600x900 safe-area
layout remains unchanged. The static black scrim and translucent passive cards
preserve text contrast; buttons remain solid. The background is non-pickable and
is hidden during Playing and ChapterTransition. The borrowed Resources texture
is retained across menu rebuilds rather than destroyed.

The complete editable bitmap is
`Assets/Resources/Menu/cyclopse-menace-v1.png`. Source provenance, creation/edit
prompts and tool pins are in `SourceArt/MenuBackdrop`. No external image, model,
new sound, animation or scene edit is required. `MenuBackdropImport` validates the
full-resolution Texture2D Resource and preserves its proportions and colour.

The native release audit passed 11 actual UI Toolkit captures, including normal
title, settings, title/pause at 1280x720, 1600x900, 1280x960 and 2560x1080 with
large text/high contrast, and the original playing world plus transparent HUD.
Pointer Settings/Back, keyboard focus/submit, real Escape input and focused Resume
were exercised. All menu buttons fit their viewports. A resource-off reference
comparison confirms the shipped bitmap contributes to the native rendering.
Preferences and the temporary input device were restored after the audit.

Representative PNGs are retained with the full JSON report and build-input hash
verification. Other transient captures and profile fixtures are optional. These
checks do not certify enemy AI, routes, survival, audio or performance.

Use Unity **6000.6.0f1** with the project's pinned packages. From the project root:

```powershell
unity run . --editor-version 6000.6.0f1 --timeout 1500 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/MenuFace-Player
Builds/MenuFace-Player/HappyToyV2.exe -v2-menu-face-output Verification/menu-face-review
python Tools/quality/verify_graphics_build_inputs.py --project . --build Builds/MenuFace-Player --output Verification/menu-face-build-inputs.json
# After staging required sources:
python Tools/quality/check_source_completeness.py
```

The audit is opt-in and quits after writing its finite report. Ordinary launch
shows the title and uses the original menu navigation and gameplay input gates.
