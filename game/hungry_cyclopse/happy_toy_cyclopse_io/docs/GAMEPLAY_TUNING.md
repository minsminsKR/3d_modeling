# Gameplay Tuning Notes

Current tuning reference for the browser `.io` prototype. Values below reflect the active implementation in `server/gameWorld.js`, `public/src/input.js`, and `public/src/scene.js`.

## Flashlight Reveal Behavior

- `F` toggles the local flashlight.
- The flashlight is a physical Three.js lighting setup, active only while the local player is alive.
- Visuals fade toward on/off at roughly `dt * 10`; the head lamp fades at `dt * 12`.
- Current light targets when active:
  - head lamp spot intensity: `1500`
  - small camera fill intensity: `14`
  - short-range spill light intensity: `38`
- The spotlight source is attached near the local Cyclopse body and aims along the character/camera yaw direction with a slight downward pitch, creating a cone-shaped lit area in front of the player.
- The head lamp casts a small soft shadow map so corridor wall panels can interrupt the light path without adding fake white reveal geometry.
- The fake additive monster glow/shell was removed. Monsters no longer get white spheres when approached.
- Character FBX meshes use `MeshLambertMaterial` so the spotlight/fill lights brighten models through the renderer lighting path instead of manual material color boosting.
- FBX vertex normals are recomputed on load so character surfaces respond correctly to physical lights.
- The map baseline is slightly brighter through lower fog density, higher tone-mapping exposure, and stronger ambient/hemisphere/moon lights.
- Eatable footprint reveal is not currently gated by the flashlight. It is based on the local player's size compared with each enemy/player snapshot.

## Enemy AI Probabilities

- Normal enemies use a base sense distance of `90` world units, then personality-specific tuning.
- Idle normal enemies reconsider decisions every `0.8` to `1.8` seconds after spotting a target.
- Base chase chance is `0.4`; base flee chance is `0.4`.

| Personality | Speed Multiplier | Sense | Chase Chance | Flee Chance | Chase Give-Up | Flee Give-Up |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `bold` | `1.12` | `106` | `0.54` | `0.32` | `170` | `120` |
| `skittish` | `1.05` | `96` | `0.28` | `0.58` | `138` | `165` |
| `erratic` | `1.0` | `90` | `0.4` | `0.4` | `150` | `135` |
| `lazy` | `0.88` | `76` | `0.32` | `0.34` | `125` | `112` |

- A normal enemy chases only if it is larger than the target.
- A normal enemy flees only if it is smaller than the target.
- Regular enemy mix scales with largest player size:
  - below size `15`: `hwacat` only
  - size `15` to `34`: `65%` `hwacat`, `35%` `uncat`
  - size `35+`: `50%` `hwacat`, `30%` `uncat`, `20%` `angry`
- Giant phase starts when the largest player reaches size `60`; regular enemies are replaced by giants.

## Chase Give-Up Rules

- Normal chase ends when:
  - target is gone/dead,
  - distance exceeds the enemy's personality give-up distance,
  - chase time reaches `10` seconds.
- Normal fleeing ends when:
  - target is gone/dead,
  - distance exceeds the enemy's personality flee give-up distance,
  - flee time reaches `10` seconds.
- Stopped normal chases apply a `2.5` second chase cooldown before the enemy can pick another chase target.
- Enemies ignore spawn-protected and god-mode players when choosing AI targets.
- Giants look for targets within `95` units when not chasing and `140` units while already chasing.
- Giant chases end after `10` seconds, or immediately when no alive target is found in the current search range.

## Verification Checklist

- Start the prototype with `node server/index.js` from `happy_toy_cyclopse_io`.
- Open `http://localhost:8080` and join a match.
- Press `F`; confirm the world brightens only from actual light sources attached to the camera/player.
- Confirm monsters in the flashlight path become brighter without white glow spheres or fake shell overlays.
- Confirm eatable enemy/player footprints still appear based on relative size, independent of flashlight state.
- Grow the player past size `15`, `35`, and `60`; confirm enemy composition changes at those thresholds.
- Observe larger normal enemies chasing and smaller normal enemies fleeing only after they are within sense range.
- Confirm normal chases stop after distance/time limits and do not immediately restart during cooldown.
- In giant phase, confirm giants replace regular enemies and give up after the configured chase duration or lost target range.

## Environment Props

- The Node server mounts `E:\AI\3d_modeling\game\assets` at `/assets`.
- `public/src/scene.js` loads prop GLB files with `GLTFLoader`; no asset copy step is required.
- Random prop placement is deterministic with `WORLD_PROP_SEED = 73491`, making visual QA repeatable.
- Static props are distance-culled around the local player every few frames.
- Active prop families:
  - `barricade`
  - `barred-window`
  - `corridor-wire`
  - `silent-mannequin-1f`
  - `silent-mannequin-2f`
  - `upper-doll-circle`
  - `placeholder-wrapped-body-1f`
  - `upper-mirror-shards`
- Boundary walls use `/assets/textures/walls/wall.png`; intermittent outer panels use `/assets/textures/doors/doors.png`.
- Walls are placed only on the map boundary ring, leaving the playable interior open for large characters.
- Boundary walls use a continuous inner cylindrical liner plus instanced outer panels, so approaching the edge no longer exposes a hollow wall interior.
- Boundary panels are rendered as instanced box panels rather than dozens of independent meshes.
- Red floor stains use `/assets/textures/props/placeholder-red-puddle-1f/basecolor.png`.

## Performance Budget

- Renderer pixel ratio is capped at `1.5` and can adapt down toward `0.9` when the local frame average falls below the target range.
- Character FBX animation uses a distance budget:
  - local player: always full model
  - normal far actors: primitive fallback after roughly `260` world units
  - giants: full model kept farther out because their silhouettes matter
- Animation mixers are skipped for hidden/far LOD actors.
- Character and prop textures cap anisotropy to avoid high-DPI texture bandwidth spikes.
- Server snapshots are interest-area filtered per player:
  - nearby players are sent to the scene
  - global top leaders remain available for the leaderboard
  - regular enemies are sent within the local enemy AOI
  - giants use a wider AOI
- Server broadcast cadence is `66ms` instead of `50ms`; local input prediction keeps the local player smooth.
- Newly visible enemies/players are placed directly at their first server coordinate before interpolation starts. This prevents spawn/AOI entities from streaking across the screen from world origin.
- Enemy spawn points avoid active players' forward view cone as well as the minimum safe distance, reducing visible pop-in.
