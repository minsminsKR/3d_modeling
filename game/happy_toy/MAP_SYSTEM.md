# Map System: Procedural Backrooms

The game `happy_toy` uses a dynamic, endless procedurally generated grid of Backrooms-themed chunks.

## Grid Coordinates & Cell Sizes
- **Grid Chunk Size**: $16\text{m} \times 16\text{m}$ area on the horizontal ($X, Z$) plane.
- **Ceiling Height**: Low, oppressive ceiling at $y = 2.8\text{m}$.
- **Grid Indexing**: The player starts at coordinate $(0, 0, 0)$ which corresponds to chunk $(0, 0)$.

## Chunk Templates
Chunks are generated based on deterministic, seed-based noise. Available types:
1. **Start Room** (`start`): The central chunk `(0, 0)` containing the final exit (Toy Box) and four sliding doors.
2. **Workshop Room** (`workshop`): Chunk `(2, 2)` containing Key 1.
3. **Playroom Room** (`playroom`): Chunk `(-2, 2)` containing Key 2.
4. **Storage Room** (`storage`): Chunk `(2, -2)` containing Key 3.
5. **Transformation Event Room** (`event`): Chunk `(-2, -2)` where the Mirror Hwacat Event occurs, rewarding Key 4.
6. **Corridors** (`corridor_ns`, `corridor_ew`): Infinite hallways.
7. **Narrow Corridor** (`narrow_ns`): Tight passage ($3.0\text{m}$ width).
8. **T-Junction** (`t_junction`): Halls split 3-ways.
9. **L-Corner** (`corner`): Turn hallways.
10. **Cross Junction** (`cross_junction`): Open crossroads.
11. **Dead End** (`dead_end`): Blocked passages.
12. **Pillar Room** (`pillar_room`): Large rooms with columns.
13. **Flickering Light Room** (`flicker_room`): Rooms where lights blink erratically.

## Dynamic Grid Loading
- As the player moves, the `MapBuilder` calculates the player's current grid coordinates:
  $$cx = \lfloor \frac{px + 8}{16} \rfloor, \quad cz = \lfloor \frac{pz + 8}{16} \rfloor$$
- Chunks within a radius of $2$ (forming a $5 \times 5$ square area) are loaded.
- Chunks outside this range are destroyed to conserve memory.
- Colliders, floor boundaries, and AI waypoints are dynamically registered and unregistered in `CollisionWorld` to ensure accurate and fast physics queries.
