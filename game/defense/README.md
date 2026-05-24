# Mob Shooter Defense Prototype

A 3D mobile-ad style casual crowd-shooting runner game built with Python and the Ursina Engine.

## Game Concept
Control a player who automatically shoots bullets forward and runs along an infinite linear road. Collect teammates through positive gate modifiers (+5, x2, etc.), upgrade your weapons dynamically, and defeat hordes of enemies (basic, fast, giant tank) coming down the road!

## Features
- **Endless Runner Mechanics**: Road tiles and barriers spawn and cycle automatically.
- **Formation Army System**: Allies keep adaptive rows, spacing, smooth follow, and throttled updates for large crowds.
- **Crowd Control Gate System**: Choose army gates, multiplier gates, weapon gates, and stat gates.
- **Class-Based Weapons**: Pistol, Dual Pistol, SMG, Rifle, Shotgun, Laser, Minigun, and Rocket Launcher.
- **High-Feedback Combat**: Bullet trails, muzzle flashes, hit flash, knockback, particles, combo UI, and camera shake.
- **Durable Enemy Wave System**: Basic, fast, tank, exploder, shooter, and boss enemies scale over time.
- **Run Progression**: Level ups trigger upgrade choices for fire rate, damage, allies, crit, spread, and projectiles.
- **Object Pooling**: Bullet, muzzle flash, enemy, enemy shot, pickup, and particle limits keep large fights playable.

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
Create or use the local virtual environment:
```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```
Then run the entry point script:
```powershell
.\.venv\Scripts\python.exe main.py
```

## System Docs
- `docs/COMBAT_SYSTEM.md`
- `docs/UPGRADE_SYSTEM.md`
- `docs/WAVE_SYSTEM.md`
- `docs/PERFORMANCE_GUIDE.md`
