# Ally System Documentation

The Ally System handles the spawn, formation, and combat behaviors of the player's crowd (mob).

## Formation Layout

Allies follow the main player using a structured layout that shifts dynamically as the crowd size changes. 

- **Offset Formula**:
  - The first member (index 0) remains directly on the player.
  - Subsequent members are laid out in a grid pattern extending behind the player:
    ```python
    row = (self.index - 1) // 4 + 1
    col = (self.index - 1) % 4
    x_offset = (col - 1.5) * 0.8
    z_offset = -row * 1.0
    ```
  - This forms a clean rectangular flocking group that follows the player smoothly.

## Movement Interpolation

To ensure allies move realistically and stay clustered, they interpolate toward their target positions:
```python
self.entity.position = self.entity.position + (target_pos - self.entity.position) * min(1.0, dt * 8.0)
```
This adds a lag-follow flocking aesthetic common in crowd-runner games.

## Automatic Combat

Each ally has an independent shooting timer:
- Initialized with a random value (`random.uniform(0, 0.3)`) to stagger bullet spawn timing and prevent performance bottlenecks.
- Fires straight ahead (+Z) automatically at the rate determined by the active weapon system.
- When an ally is touched by an enemy, the ally is instantly destroyed (cancels out 1-to-1 against the enemy).
