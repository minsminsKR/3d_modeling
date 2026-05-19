# Map System

The world uses a chunk-based infinite-world illusion.

- `CHUNK_SIZE`: controlled in `core/config.py`
- `CHUNK_RADIUS`: number of chunks kept around the player
- Far chunks are destroyed.
- New chunks spawn floor planes and random props.
- `ValueNoise2D` gives subtle height variation without external dependencies.

The map avoids blocking maze walls because the player can grow without limit. Corridor horror is represented by fog, dark lighting, silhouettes, and props.
