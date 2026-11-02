# Cleaning anomaly: barefoot blood print

`bare-blood-footprint.png` is the original transparent RGBA image produced by
the built-in image generation tool on 2026-11-02 for HappyToyV2. SHA-256:
`4969505f9794ad59c3c4eda2c74efef90148548715d326e016c44285d151d5cb`.
The identical Unity resource is retained at
`Assets/Resources/SchoolCleaningBlood/bare-blood-footprint.png`; its alpha
channel supplies the actual barefoot silhouette. The right foot mirrors the
same image. `BloodDecal.shader` uses explicit alpha blending, not the school's
opaque surface shader. Runtime droplets are generated as a circular alpha
texture and do not use an external image.

Image prompt:

> Game asset texture: one realistic LEFT bare human bloody footprint, a single foot sole impression viewed perfectly from directly above, heel at bottom, five distinct toes at top, entirely contained with generous transparent margins. The sole is made of translucent dark crimson blood with natural broken skin-contact patches, fine fingerprint/ridge detail, slight smearing and irregular liquid edges. Accurate barefoot anatomy with individual toe imprints and an arch; no shoe sole and no footwear. Sparse tiny blood flecks close to the print. Flat orthographic isolated decal intended to overlay a dark wet floor. Real alpha transparency everywhere outside the footprint; no rectangular border, no ground, no text, no additional footprints or objects.
