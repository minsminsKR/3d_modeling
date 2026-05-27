# Performance Guide: Optimization in Endless Maps

Procedural endless maps require careful culling and cleanup to maintain stable frame rates and low memory consumption.

## Chunk Pooling & Destruction
- Chunks are instantiated as the player approaches.
- When the player travels beyond a distance of $3$ chunks ($>32\text{m}$), the far chunks are destroyed.
- **Mesh Disposal**: The meshes, geometries, and materials inside the unloaded chunks are explicitly deleted and disposed:
  ```javascript
  mesh.geometry?.dispose();
  mesh.material?.dispose();
  ```
  This prevents GPU memory leaks.

## Light Pooling & Culling
- Three.js PointLights are only active for ceiling panels within a $24\text{m}$ radius of the player.
- Panels outside this range have their PointLight objects destroyed and removed from the scene.
- Distant panels rely entirely on basic emissive color textures, which do not contribute to dynamic shadow maps or render counts.

## Blocker & Collider Cleanup
- Static boxes, floor walkable zones, doors, and AI waypoints are linked to their respective `chunkId`.
- When a chunk is unloaded, the `CollisionWorld` removes them:
  ```javascript
  this.blockers = this.blockers.filter((b) => b.chunkId !== chunkId);
  ```
  This keeps collision checks fast ($O(N)$ where $N$ is kept small to only active colliders).

## AI Teleport Spawner
- Instead of spawning many active monsters across the endless map, we limit the active count.
- If a monster gets too far ($>52\text{m}$ from the player), it is teleported close to the player in a nearby loaded chunk that has no line of sight, preventing pathfinding computation overhead across massive distances.
