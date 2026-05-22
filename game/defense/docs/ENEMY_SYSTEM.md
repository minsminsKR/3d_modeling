# Enemy System Documentation

The Enemy System is responsible for spawning hostile waves ahead of the player, managing their movement downwards, and resolving bullet and crowd collisions.

## Enemy Configurations

| Enemy Type | Spawn Tier | Color | Health | Base Speed | Points | Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Basic** | Wave 1+ | Orange | 24 | 1.8 | 1 | Medium stats, default runner enemy. |
| **Fast** | Wave 3+ | Red | 12 | 3.5 | 2 | Small scale, runs rapidly down the track. |
| **Tank** | Wave 5+ | Purple | 60 | 0.8 | 5 | Huge scale, massive health sponge. |

## Wave Spawning Styles

Every 20 units of distance (Z), the system spawns a new wave:
- **Line**: Spawn a flat row of enemies across the width of the road.
- **Gate Blocker**: Spawn dense blocker groups specifically lined up with the path of oncoming gate choices.
- **Cluster**: Spawn a scattered pack of enemies at random offsets.

## Collisions & Cancelling Out

- **Bullet Collision**: Simple bounding box checks deactivate the bullet, trigger flash visual on the enemy, and subtract damage. If health <= 0, the enemy is destroyed and adds points.
- **Crowd Collision**: When an enemy touches the player's crowd (player or ally):
  - The enemy is destroyed.
  - If the player has allies, the last ally in the group is removed (the units "cancel each other out").
  - If the player has 0 allies and is hit directly, the player dies, resulting in **Game Over**.
