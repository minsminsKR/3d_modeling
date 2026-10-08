# Asset provenance

No new third-party assets are distributed by this change. Red core/rim geometry,
UV measurement helpers and escutcheon backing geometry are original project code.
The existing stalker, doll and lantern-mask assets and their textures remain the
original project assets; this change does not replace their provenance or grant
new rights over them.

The forged door hardware is the project's original detailed authored Blender
model, preserved in `SourceArt/GraphicsUpgrade/Props/door-hardware.blend` with the
generator `SourceArt/GraphicsUpgrade/author_props_v3.py`. Its tracked model/PBR
manifests continue to describe the source materials.

Reused rust PBR scan: **rusty_metal_02**, Rob Tuytel, Poly Haven, CC0 1.0.
Original source, hash, attribution and license are preserved in
`ThirdParty/Graphics/polyhaven-pbr-manifest.json` and
`ThirdParty/Graphics/Graphics-Credits.txt`.
The charred cloth/brass surface maps and paper emissive template are the project's
existing procedural graphics sources under `SourceArt/GraphicsUpgrade`.

The emissive template is used with a white base map, an original code-generated
radial iris heat map and existing
shader keywords. Its paper appearance is not copied into eye textures. No shader
or texture resource generated only in a local cache is required.
