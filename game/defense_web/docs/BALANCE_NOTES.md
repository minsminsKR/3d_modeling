# Defense Web Balance Notes

## Integration

`static/src/balance.js` is the web balance source of truth for wave timing, enemy stats, exp pacing, gate rewards, stat caps, and weapon auto-upgrade thresholds.

`static/src/main.js` imports the helpers and keeps runtime responsibilities only:

- `resetState` reads initial exp/wave/gate/cap values from `BALANCE`.
- `updateEnemies` uses `BALANCE.waves.triggerLookahead` and `nextWaveInterval`.
- `spawnWave` uses `buildWavePlan`.
- `spawnEnemy` uses `enemyStats`.
- `killEnemy` uses `killExp`.
- `updateGates`, `spawnGatePair`, and `applyGate` use gate balance helpers.
- `updateWeapon` uses `weaponIndexForState`.
- `addExp` supports multiple level gains through `pendingUpgradeCount`.
- `applyUpgrade` uses `applyProgressionReward`.

## First 3 Minutes

The first wave now spawns immediately at about `player.z + 30`. With player speed near `5.15` and basic enemy speed near `1.75`, monsters are visible within the first few seconds instead of dying only near the horizon.

Wave spacing starts near 44 world units and decays toward a 32 floor. The next spawn trigger is reset from the current forward lookahead after each wave, which prevents the opening frames from chaining several waves at once.

## Wave Styles

The web version mirrors the original defense wave style idea:

- `line`: readable horizontal screen fill.
- `cluster`: loose center/random pack.
- `double_line`: two staggered rows.
- `pinch`: side-lane pressure plus a center threat.

The style cycles by wave so the opening sequence is deterministic enough to tune: line, cluster, double_line, pinch, then repeat.

## Enemy Unlocks

- `basic`: starts at wave 1.
- `fast`: starts at wave 4.
- `tank`: starts at wave 8.
- `midBoss`: every 6th wave unless it is also a big boss wave.
- `bigBoss`: every 12th wave.

Enemy HP scales by roughly 9.5 percent per wave. Speed scales much more gently and is capped, because runaway enemy speed makes hit detection and player readability feel unfair in the browser version.

## Leveling

Initial exp need is 90, and basic kills grant 3 exp. This makes early growth noticeable but avoids an upgrade modal every few seconds.

`addExp` now loops over overflow exp and records each earned level in `pendingUpgradeCount`. If a large reward crosses multiple levels, the player receives the correct number of upgrade choices one at a time instead of losing levels or showing only a single modal.

## Rewards And Caps

Gate rewards are intentionally smaller than level-up rewards:

- Fire rate gate: 8 percent faster.
- Damage gate: 14 percent stronger.
- Spread gate: +0.8 spread.
- Ally gates: +4, +7, +12, x1.5, then x2 later.

Level-up rewards are still stronger, but capped with gates:

- `allyCap`: 180
- `fireRateMult`: minimum 0.5
- `damageMult`: maximum 3.4
- `spreadBonus`: maximum 5.2
- `extraProjectiles`: maximum 3
- `critChance`: maximum 0.45

## Weapon Auto-Upgrade

Weapon auto-upgrade no longer depends directly on `allyCount`, because gates can inflate allies very quickly. The score now combines:

- kills, primary contributor
- level, confirms progression
- wave, confirms encounter depth
- elapsed time, small smoothing factor

This keeps weapon growth tied to actual combat progress rather than a single lucky gate.

## Upgrade Key Naming

The original Python keys use snake_case while the web config uses camelCase. `balance.js` exposes `UPGRADE_KEYS` for the web names:

- `fireRate`
- `damage`
- `allies`
- `spread`
- `projectile`
- `crit`

If the web upgrade config is expanded later, add new keys to `UPGRADE_KEYS` first, then reference that constant from config and runtime code to avoid key/name drift.
