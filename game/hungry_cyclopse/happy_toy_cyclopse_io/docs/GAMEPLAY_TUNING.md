# Gameplay Tuning Notes

Current tuning reference for the browser `.io` prototype. Values below reflect the active implementation in `server/gameWorld.js`, `public/src/input.js`, and `public/src/scene.js`.

## Flashlight Reveal Behavior

- `F` toggles the local flashlight.
- The flashlight is a client-side visual aid, active only while the local player is alive.
- Visuals fade toward on/off at roughly `dt * 10`; the head lamp fades at `dt * 12`.
- Current light targets when active:
  - head lamp intensity: `18.5`
  - fill lamp intensity: `2.4`
  - aura intensity: `6.2`
  - ground aura opacity: `0.38`
  - cone opacity: `0.46`
- The ground aura radius is `34`; the forward cone is `115` long and `46` wide.
- Characters inside the flashlight aura/cone are also visually revealed:
  - nearby reveal radius: `115`
  - forward reveal range: `260`
  - forward reveal angle threshold: `dot > 0.16`
  - model material brightness is boosted client-side because character FBX meshes use `MeshBasicMaterial`
  - a warm additive sprite glow and subtle shell glow are shown on revealed enemies/other players
  - fog is temporarily disabled on strongly revealed character materials so monsters do not disappear into the floor lighting
  - cloned FBX materials are made unique per character instance so one revealed monster does not brighten every monster using the same model
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
- Press `F`; confirm the cone and warm ground aura fade in/out and follow camera yaw.
- Confirm monsters inside the cone/aura become visibly brighter, not just the floor.
- Confirm eatable enemy/player footprints still appear based on relative size, independent of flashlight state.
- Grow the player past size `15`, `35`, and `60`; confirm enemy composition changes at those thresholds.
- Observe larger normal enemies chasing and smaller normal enemies fleeing only after they are within sense range.
- Confirm normal chases stop after distance/time limits and do not immediately restart during cooldown.
- In giant phase, confirm giants replace regular enemies and give up after the configured chase duration or lost target range.
