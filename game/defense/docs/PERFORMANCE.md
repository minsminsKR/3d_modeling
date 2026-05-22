# Performance Optimization Documentation

To deliver a smooth 60 FPS prototype on Ursina (which runs on a single Python thread), several optimization techniques were integrated:

## 1. Object Pooling
- **Bullets**: Bullets are pre-instantiated in a `BulletPool`. Instead of creating and destroying bullet entities (which causes Python garbage collection spikes and micro-stutters), bullets are activated and deactivated via the `.enabled` property.
- **Enemies**: Enemies are recycled via a similar list pool. Once they go off-screen or die, they are disabled and stored for reuse in subsequent waves.

## 2. Lightweight Collision System
Using standard Ursina / Panda3D raycasting or bounding box physics for dozens of bullets checking against dozens of enemies would degrade performance.
- We bypassed default mesh collider checks for bullet-enemy and crowd-enemy interactions.
- We implemented a direct X/Z coordinate proximity check:
  ```python
  x_dist = abs(bullet.x - enemy.x)
  z_dist = abs(bullet.z - enemy.z)
  if x_dist < limit_x and z_dist < limit_z:
      # Collision!
  ```
- This is mathematical, runs in pure Python arrays, and scales up to 100+ entities without lagging the game thread.

## 3. Dynamic Garbage Collection
- **Infinite Roads**: Tiling road blocks are recycled. Instead of spawning new roads and deleting old ones, we use exactly two road blocks and shift them forward along the Z axis as the player advances.
- **Gate Despawner**: Passed gates are proactively destroyed to keep active entity count low.
- **Staggered Shooting**: Allies shoot on staggered, randomized offsets to prevent heavy single-frame processing peaks.
