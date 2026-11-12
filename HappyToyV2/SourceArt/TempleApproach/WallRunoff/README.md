# Village wall rain runoff

The transparent source was generated with the built-in image generation tool on 2026-11-12 for the Korean village wall. SHA-256: `5ad33b356b7caf415c4d6e92285b38e0b1bfd1c1d5da62c51654be3f3a589669`.

Prompt:

> Create one photorealistic transparent PNG decal for a 3D game, NOT a photograph of a wall. Subject: thin irregular trails of dark charcoal-brown rainwater dirt seeping down from the top of an old cement wall; use sparse hairline vertical streaks and speckled, irregular edges. The streaks themselves must be dark charcoal and muted brown, never white or pale gray. Everything outside and between the streaks is fully transparent alpha; no solid wall, no white or gray halo or cloud, no shadows, no background, no text, no border. Flat straight-on view. The stain occupies the central half of the image with generous clear margins; restrained natural variation, suitable for overlay on a grey concrete wall.

The alpha-bearing original is copied unchanged to `Assets/Resources/AlleyWallRunoff/rain-runoff.png`. Run `HappyToy.V2.Editor.AlleyWallRunoffImport.Apply` with the pinned project editor to recreate its URP transparent material. Three small non-colliding patches are placed on separate segments of the left curved wall; they share one material and texture. They do not replace the PBR wall surface.

The focused `temple-approach-village-visual` D3D11 editor play-mode test checked all three wall supports and absence of colliders, and captured a first-person close view. The screenshot shows a restrained vertical runoff beside the wall pipe. This is a local wall detail, not proof that the entire village matches the reference. Native-player performance has not been measured.
