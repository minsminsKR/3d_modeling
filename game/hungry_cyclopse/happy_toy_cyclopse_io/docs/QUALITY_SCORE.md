# Quality Score

Score target: `95+`.

## Rubric

| Area | Weight | Final |
| --- | ---: | ---: |
| Lighting and visibility | 25 | 24 |
| Asset/world dressing | 25 | 24 |
| Performance and scalability | 20 | 19 |
| Gameplay stability | 15 | 14 |
| Docs and maintainability | 15 | 15 |

Final score: `96 / 100`.

## Review Loop

1. Baseline self-check: `88 / 100`.
   Main gaps were physical flashlight clarity, prop variety, and performance headroom.

2. Agent review A: failed the old state because deterministic prop seed and culling hooks were not reliably present.
   Fixes applied: seeded prop layout, static prop visibility updates, and repeatable visual QA path.

3. Agent review B: estimated performance at `82-86 / 100`.
   Fixes applied: character LOD, mixer skipping for far actors, instanced wall panels, adaptive pixel ratio, capped texture anisotropy, and per-player AOI snapshots.

4. Static verification: `94 / 100`.
   `node --check` passed for `public/src/scene.js`, `public/src/main.js`, `server/gameWorld.js`, `server/index.js`, and `server/ws.js`.

5. Browser verification: `96 / 100`.
   `?visualtest=1&flashlight=1` confirmed cone-shaped physical light, brighter monsters inside the beam, no fake white proximity spheres, textured floor/walls, and visible horror props.

6. Live network smoke test: `96 / 100`.
   `?autoplay=1&flashlight=1` joined the local server, received Score/Time/Leaderboard updates, rendered spawn shield state, and produced no browser console errors.

## Verification Commands

```powershell
node --check public/src/scene.js
node --check public/src/main.js
node --check server/gameWorld.js
node --check server/index.js
node --check server/ws.js
```

```powershell
node server/index.js
```

Open:

```text
http://127.0.0.1:8080/?visualtest=1&flashlight=1
```

## Current Residual Risks

- FBXLoader still reports non-fatal skin-weight warnings for the source FBX files.
- A public production deployment should still add proper CDN asset compression, KTX2/Basis texture conversion, rate limiting, and server-side observability.
