# School walking NPC reference

`reference-v2-purple-flower.png` is the current modeling input, adding the user's
requested small purple flower above the subject's right ear (image left), as in
the original photograph. `reference-v2-settings.json` retains the exact localized
edit prompt and both reference filenames. The first version remains unchanged.

`reference-v1.png` is the initial single-person image-to-3D input created with the built-in
image generation tool from the user's `face-reference.jpg`. The exact prompt and
assumptions are preserved in `reference-settings.json`. The face reference is
copied unchanged; the source attachment remains in place.

Target body specification: 1.77m tall and 120kg, with a realistic heavy build.
The input uses a front-facing relaxed A-pose, separated hands/limbs and neutral
studio lighting for later reconstruction and rigging. The navy T-shirt follows
the user photo; charcoal trousers and plain shoes are authoring assumptions.

This handoff currently contains the requested image only. Future model generation
uses the designated modeling CLI/service under AGENTS.md, followed by actual web
inspection, Blender finishing and Unity integration. Actual metric height must be
set on the model during authoring; it is not measured from this raster image.

Origin: user-provided face photograph and new project-generated reference image.
No stock character model, model-service job or animation has been created here.
