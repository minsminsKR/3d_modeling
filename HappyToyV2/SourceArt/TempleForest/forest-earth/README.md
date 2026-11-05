# Forest earth material source

`imagegen-original.png` is a generated color texture made with the built-in
image generation tool on 2026-11-05. SHA-256:
`27ca10c3e6fab171c0feba68eab3afbaea5ab7351fb814f34c8e1cc2e6cb28bf`.
The untouched image is copied to the Unity albedo and height inputs. Unity's
`TempleForestEarthImport` imports both at a maximum 1024 pixels and derives
the tangent-space normal from luminance; this is an approximation, not a
separately captured physical height map. The albedo is readable for seam tests.

Prompt: "Create a square, photorealistic seamless tileable PBR BASE COLOR
(albedo) texture for an actual 3D game's forest ground: top-down orthographic
close-up of a neglected old Korean mountain temple forest floor after rain.
Densely and naturally mixed dark damp humus, fine charcoal-grey earth, tiny
weathered stones, scattered muted russet and decomposing maple leaves, a little
dark green moss and occasional thin root fragments. Realistic fine detail, low
contrast, no obvious large repeated features. Edge-to-edge, equal physical scale
across image, opposite edges seamlessly match. Neutral soft flat lighting, no cast
shadows, no highlights, no ambient occlusion baked, no directional light, no
perspective, no horizon, no path, no buildings, no characters, no text or
watermark. This is a color texture to be tiled over a 3D mesh, not a scene
illustration."

Run the pinned Unity Editor in batch mode with
`-executeMethod HappyToy.V2.Editor.TempleForestEarthImport.VerifyImports` to
force and validate the two texture import settings. Actual scene appearance,
edge repetition and normal strength require game-camera review.
