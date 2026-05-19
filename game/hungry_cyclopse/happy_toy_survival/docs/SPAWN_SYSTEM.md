# Spawn System

`EnemyManager` keeps the active enemy population near the configured target.

## Normal Phase

Before player size `60`, the game maintains `20` regular enemies.

- Spawn distance: 30m to 80m from player
- Spawn attempts prefer positions outside the camera forward view
- Size `35+` weights:
  - Hwacat: 50%
  - Uncat: 30%
  - Hwacat_angry: 20%

## Giant Phase

At player size `60+`, regular enemies stop spawning. Only Giant Cyclopse enemies spawn.

- Giant size: `80`
- Count increases over survival time
- Giant speed is slightly slower than player sprint speed

## Pooling

Consumed or far-away enemies are disabled and moved into a pool. Later spawns reuse pooled enemy objects when possible.
