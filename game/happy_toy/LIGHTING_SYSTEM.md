# Lighting and architectural finishes

## Practical lights
- `ArchitecturalFinish.addLighting()` installs fluorescent housings and diffusers under actual ceilings, on a 4 m placement grid clipped to the walkable floor and collision clearance. Narrow areas receive a centered candidate. Stair openings receive no floating fixtures.
- Fixtures are placed on every authored floor, including the upstairs gallery and basement. Passive source intensity is 2, with warm-neutral light; E-key lamps provide the usable room lighting. Nearby fixtures are selected by the existing fixed pool; lamps from other floors are excluded.
- `StablePointLights` continues to render only 12 real point lights. More fixture meshes do not increase the shader light count. The player flashlight is still the only dynamic shadow-casting light.
- Ordinary switched and ceiling fixtures remain steady while moving, sprinting, or near monsters. Scripted cinematic dimming remains supported.

## Exposure and threat effects
- Monster proximity no longer raises the screen brightness. Glitch brightness is capped at 1; its overlay uses multiply blending so it cannot illuminate dark surfaces. Motion, sound, and darkening remain.
- The persistent vignette was reduced to preserve peripheral navigation visibility.

## Surfaces and placement
- `ArchitecturalFinish` creates deterministic painted-plaster, wood-board and upstairs tile materials. Surface UVs use world dimensions: 20 cm floorboards and 50 cm upstairs tiles. Adjacent slabs share the same pattern origin.
- Upstairs floor coloring no longer mutates the cached 1F floor material.
- Structural walls use consistent paint height, while trim and doors remain distinct. Instanced walls use world-based UVs without de-instancing.
- Wall labels, paper charms, clocks and chalkboards are projected onto real wall faces with clearance. Unsupported decorations and fake door decals are hidden. Existing passage and door collision geometry is retained.
- Oversized floating blood carpets and black corridor-center strips are removed; smaller localized stains remain.
- Per-surface geometry is disposed when a chunk unloads; reusable finish materials stay cached.

## Verification (2026-09-11)
- `node verification/check-syntax.mjs`: 49 modules passed.
- `node verification/verify-quality-polish.mjs`: corridor-only mannequin encounter, upper-floor and room rejection, facing direction, stationary cinematic camera, non-brightening glitch blend, material isolation and mounted decoration checks passed.
- `node verification/verify-gameplay-polish.mjs`: 28 m cabinet departure, stable destination, wander return, no repeated hide TTS, and flashlight-off visibility passed.
- `node verification/verify-stairs-walk.mjs http://127.0.0.1:8014/`: ascending to 2F and descending to B1 passed.
- `node verification/verify-shadow-run.mjs http://127.0.0.1:8014/`: corridor/classroom routes, basement/upstairs routes, key collection, hiding and ritual completion passed.
- Screenshots are in `verification/quality-qa/` and `verification/cabinet-departure-qa/`. Run `quality-audit.mjs` to reproduce visual captures. Playwright may be provided with `PLAYWRIGHT_MODULE` or `NODE_PATH`.

## Manual switch / flashlight correction
- E-key SafeLights: intensity 60 (previously 18), range 14 m, decay 1.35. This changes the actual switched-on light, not only its glowing mesh.
- Flashlight: intensity 18 on 1F/B1/annex, 20 on 2F (previously 40/52); camera fill reduced from 0.65 to 0.3.

## Actual play-screen correction
- Removed `body.flashlight-off` global 0.42 brightness filter. Its selector was overridden by the threat filter, causing a sudden brightness jump during pursuit. Flashlight state now changes only its own lights.
- Ambient / hemisphere fills lowered to 0.4 / 0.3 (with floor-specific variants); passive ceiling light intensity is 2. E-key light output remains 60 and the reduced flashlight output remains 18 / 20.
- Glitch/noise and threat overlays now only darken the image; additive colored noise was removed. Switched-light pool selection is floor-aware.
- `verify-lighting-play.mjs final` captures real DOM-composited play scenes with the flashlight off, with E-light on/off and an active nearby monster. `check-lighting-captures.py` validates screenshot pixels. Central-region mean RGB: off/far 9.96, on/far 106.66, off/near 8.80, on/near 78.25 (0–255).

## Movement stability correction
- Removed the two successive nearest-light allocations in Game and StablePointLights. Every ceiling fixture and E-key lamp now owns a fixed physical source; only StablePointLights selects the 12 GPU lights.
- Retained sources receive a 40% selection margin. Head bob is excluded from the vertical score. Retiring sources fade out over 0.32 seconds at their original position before their slot is reused; replacements fade in.
- `verify-steady-lighting.mjs` checks 480 movement frames for bright source relocation, bounded intensity steps and fixed active sources, and captures the lit hall and classroom.
- Annex room (8,-1), previously the small-content art-room layout, is now a 20-seat classroom with a central aisle, teacher desk, chalkboard and wall-aligned storage. Small corridor rooms are labeled by their actual use (supplementary study, practice, preparation, storage). Removed full-height fake desk-row obstacles and corner posts from teaching/special-purpose rooms.
