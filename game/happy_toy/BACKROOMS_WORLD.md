# Backrooms World Design & Gameplay Objects

This document describes the visual style and rules of the infinite Backrooms world.

## Environment Style & Textures
The atmosphere of the Backrooms is designed to evoke discomfort, confinement, and disorientation:
- **Wallpaper**: Worn, faded yellow wallpaper color (`0xdecb6c`) with a high roughness texture.
- **Carpet**: Stained, damp brownish carpet color (`0x8d8363`) that squelches underfoot.
- **Ceiling**: Stained acoustic panels (`0xbfbda6`) with repeating grids.
- **Fog**: Murky, dusty beige-grey fog (`0x15140f`) extending from $14\text{m}$ (near) to $50\text{m}$ (far), causing distant rooms and corridors to fade into darkness.

## Key & Exit Positions
To win the game, the player must explore the 4 quadrants to locate and collect the 4 keys, then return to the start room to unlock the Toy Box:
1. **Toy Box (Final Exit)**: Center of chunk `(0, 0)`.
2. **Key 1 (Rust Key)**: Inside the Workshop room at chunk `(2, 2)`.
3. **Key 2 (Playroom Key)**: Inside the Playroom room at chunk `(-2, 2)`.
4. **Key 3 (Porcelain Key)**: Inside the Storage room at chunk `(2, -2)`.
5. **Key 4 (Twisted Key)**: Spawns in chunk `(-2, -2)` following the successful completion of the Mirror Hwacat Event.

## Cabinet Hiding Spots
Cabinets are spawned to provide escape from pursuing monsters:
- Special rooms (Workshop, Playroom, Storage) are guaranteed to contain cabinets.
- Procedural room chunks have a 40% chance of spawning corner cabinets.
- The player can hide inside these cabinets by pressing `E`. Doing so breaks line of sight and prevents capture.
