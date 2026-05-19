# Architecture

## Flow

`main.py` creates the Ursina app. `GameManager` creates all runtime systems. Every frame:

1. Score time is updated.
2. Player movement, camera, flashlight, and battery pickup checks run.
3. World chunks around the player are created or removed.
4. Enemy population is maintained and AI updates run.
5. Distance contact rules resolve eating, death, or knockback.
6. UI and screen effects refresh.

## Dependency Direction

High-level systems depend on lower-level modules. Enemies know about the player interface, but the player does not know about enemies. UI reads managers but does not own game state.

## Fallback Design

External assets are optional at runtime. `safe_entity()` retries with a primitive if an FBX/GLB/texture load fails.
