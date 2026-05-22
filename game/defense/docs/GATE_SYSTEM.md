# Gate System Documentation

The Gate System controls the multiplier gates, a staple mechanic of casual mobile-ad runner games.

## Gate Spawning

- Spawns in pairs at set intervals (45 units of Z).
- Positioned at `x = -2.5` (Left) and `x = 2.5` (Right).
- When a pair of gates is generated, each side is randomly chosen to be either an **Addition** or **Multiplication** gate.

## Gate Modifiers

1. **Addition Gate (`add`)**:
   - Color: Greenish Cyan.
   - Text display: `+5`, `+10`, or `+15`.
   - Action: Adds the value directly to the user's ally count.
2. **Multiplication Gate (`mult`)**:
   - Color: Bright Cyan/Blue.
   - Text display: `x2` or `x3`.
   - Action: Multiplies the current ally count by the value.

## Collision & Deactivation

When the player (or an ally) passes through a gate:
- The gate registers a collision based on Z and X proximity:
  ```python
  if abs(player_pos.z - self.z) < 1.0:
      if abs(player_pos.x - self.left_gate.x) < 1.5:
          # Trigger left
  ```
- Proximity triggers the mathematical reward.
- Both gates in the pair immediately deactivate (turn gray and transparent) to prevent double-dipping or triggering both options.
- The gate assets are cleaned up and destroyed once they fall 20 units behind the player's camera viewpoint.
