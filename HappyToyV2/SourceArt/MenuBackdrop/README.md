# Cyclopse title and pause wallpaper

`Assets/Resources/Menu/cyclopse-menace-v1.png` is the complete editable bitmap
source and runtime asset (1672x941). It was created with the built-in image_gen
tool on 2026-10-08, using the project's actual single-eye Cyclopse as the identity
reference. No external artwork or model was downloaded. Existing monster source
rights and attribution remain unchanged; this does not introduce a new license
for those source assets.

The project-relative identity reference is
`Verification/eye-surface-20261008/Cyclopse-close-torch-on.png`. `prompts.md`
records the creation prompt and the final composition correction. The initial
draft is optional preview output; only the selected final bitmap is needed to
continue editing. Preserve the full-resolution PNG and its Unity .meta GUID.

The final composition puts the eye above the right-hand menu card, with a dark
left region for buttons. Exactly one natural pupil/iris remains visible. No text
or UI is baked into the bitmap. GameShellView owns the cover fit, darkness and
passive panel transparency; gameplay HUD does not receive this wallpaper.

Use Unity 6000.6.0f1 with the pinned project packages. MenuBackdropImport enforces
Texture2D, sRGB colour, no mipmaps or NPOT rescaling, clamped edges and high quality
compression. `QualityValidation.BuildWindows` validates the Resource before
building. Build output and native UI screenshots are reproducible, optional
verification outputs rather than the only copy of this artwork.
