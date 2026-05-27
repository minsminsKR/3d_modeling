# Transformation Event: Mirror Hwacat Sequence

The scripted mutation event has been preserved and adapted to occur dynamically inside the procedural Backrooms event room.

## Location & Coordinates
The mutation event takes place in the Transformation Event Room located at chunk `(-2, -2)`.
The coordinate offsets are aligned relative to the chunk center $(CX = -32, CZ = -32)$:
- **Trigger Area**: Center of the room at `(CX + 4.0, 0, CZ + 0.1)`, with a radius of $2.2\text{m}$.
- **Hwacat Spawn Position**: `(CX - 0.7, 0, CZ - 1.0)`.
- **Hwacat Look-At Target**: `(CX - 0.7, 1.05, CZ - 1.0)`.
- **Hwa Portrait Painting**: Mounted on the partition wall at `(CX - 4.78, 1.6, CZ)`.

## Event Sequence Flow
1. **Triggering**: When the player approaches the center of the event room, the event triggers. The status bar displays: *"벽에 걸린 액자가 아래로 떨어집니다."* (The portrait on the wall falls down).
2. **Painting Drop**: The painting slide-rotates and drops to the floor over $0.75$ seconds, producing a warning noise.
3. **Control Lock**: Player controls are locked, and the camera smooth-pans towards the falling painting.
4. **Spawn & Dance**: Hwacat spawns behind the fallen painting, standing up and performing a dance sequence while facing the player.
5. **Mutation**: After the dance, Hwacat mutates:
   - Embellished screen shake and red flickering alerts.
   - Hwacat model is replaced with `Hwacat_angry` running the `Zombie Run` animation.
   - The key `key-hwacat` (Twisted Key) is revealed at the spawn point.
   - Hwacat Angry is added to the dynamic chase manager, chasing the player relentlessly.
6. **Key Collection**: The player retrieves the Twisted Key to complete the 4th key objective.
