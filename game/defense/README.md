# Mob Shooter Defense Prototype

A 3D mobile-ad style casual crowd-shooting runner game built with Python and the Ursina Engine.

## Game Concept
Control a player who automatically shoots bullets forward and runs along an infinite linear road. Collect teammates through positive gate modifiers (+5, x2, etc.), upgrade your weapons dynamically, and defeat hordes of enemies (basic, fast, giant tank) coming down the road!

## Features
- **Endless Runner Mechanics**: Road tiles and barriers spawn and cycle automatically.
- **Crowd Control Gate System**: Choose the best gates (Addition vs. Multiplication) to boost your ally count.
- **Weapon Upgrade Progression**: Automatic upgrades from Pistol to Dual Pistol, SMG, Rifle, and Minigun based on total allies and kills.
- **Durable Enemy Wave System**: Waves grow dynamically, spawning fast runners and high-health tank giants.
- **Object Pooling**: Fast particle and bullet pooling for high performance.

## Controls
- **A / D** or **Left / Right Arrow**: Move Left/Right
- **Escape**: Pause / Unpause
- **R**: Restart on Game Over screen
- **Auto Shoot**: Automatically active

## Project Structure
```text
game/defense/
├─ main.py                # Entry point
├─ core/                  # Engine configurations, Game Manager
├─ player/                # Player controller and movement
├─ allies/                # Teammate spawns and flocking AI
├─ enemies/               # Enemy types (basic, fast, tank) and wave system
├─ weapons/               # Guns, fire rate, and bullet pooling
├─ gates/                 # Operator gate blocks (+, x)
├─ ui/                    # HUD, Pause menu, Game Over menu
├─ effects/               # Particle hit and explosion sparks
├─ audio/                 # Background music and sfx trigger fallbacks
├─ docs/                  # System architecture design docs
└─ README.md
```

## Running the Game
Ensure you have `ursina` installed:
```bash
pip install ursina
```
Then run the entry point script:
```bash
python main.py
```
