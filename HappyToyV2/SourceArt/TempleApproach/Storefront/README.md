# Korean alley storefront candidate

The user-provided alley reference was supplied to the built-in imagegen tool
on 2026-11-11. Prompt: "The supplied image is a mood and architectural reference only. Create ONE isolated physically plausible 3D modeling reference of a small 1980s Korean neighborhood corner shop as a whole exterior building. Three-quarter view shows front and one side, camera at eye level, neutral even studio lighting. Aged stucco plaster low one-story structure with a modest shallow Korean dark tiled sloping roof, old dark wood framed glass shop windows and one door, deep red and beige striped fabric awning visibly projecting out, old worn signboard with no visible letters or logos, tiny mixed-goods shelves visible behind the window. Full building and roof entirely in frame, real volume and side walls, standing alone on a plain neutral gray background with no ground plane, no trees, no pavement, no adjacent buildings, no cars, no furniture, no electric wires, no people, no text, no brands. Photorealistic PBR asset reference, accurate straight structural geometry, not a flat facade or an illustration."

Designated A100 service request `7e3be353-a933-4e8d-9f32-0b897ea055e5`,
job `044a7932-0750-43a8-8a73-536ceca6d351`, TRELLIS.2 1024 cascade,
4096 texture, seed 27891, GPU 1. The actual input is `reference.png`. The
user's reference remains under `../References/user-alley.png`; no public
licensing is implied. Web model inspection is pending when the user's existing
right-sidebar browser is available; avoid claiming visual approval from CLI.

The CLI downloaded the SHA-verified 59,051,246-byte archive; original GLB,
processed input, request/result and metadata are also extracted in `generation/`.
Blender 4.2.23 `prepare_storefront.py` derives `Storefront.fbx` at 249,999 faces
from the 950,037-face source, preserving the original UV and base color, and
packs the original in `StorefrontEditable.blend`. Rebuild with
`blender -b --threads 2 --python-exit-code 1 --python prepare_storefront.py`.
`render_storefront_review.py` renders either source or candidate on CPU.

In three-quarter CPU renders the candidate retains the tiled roof, projecting
awning and window/door image; the rear view confirms an enclosed mass. The
shop-window goods are baked into the generated surface and it is not an
interactive interior. These stills do not establish the appearance at actual
player eye height, collision, a complete visual match, GPU cost or frame rate.
The 250k-face candidate was imported by `AlleyStorefrontImport.Apply` into the
game Resources prefab and connected as visual-only alley shop geometry. The
first game-camera capture showed the back toward the alley; its placement was
rotated, after which the facade was visible and the real-input pickup/route
test passed. The game-camera still contains no HUD and is not native player,
material-fidelity or performance certification. The user's original image
remains more detailed than this implementation.
The input image
and generation output contain no intended third-party logos; this description
does not assert a third-party license for generated output.

The separately authored walk-in derivative, marker measurements and its current placeholder interior are documented under `Interior/README.md`. The original generated facade and its sources remain unchanged for fallback.
