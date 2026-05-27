# Cyclopse Tower Defense

Flask and Three.js tower defense game using the shared Cyclopse assets in `game/assets/characters/Cyclopse`.

## Run

```powershell
cd E:\AI\3d_modeling\game\TD
conda activate 3d
pip install -r requirements.txt
python app.py
```

Open `http://localhost:8123`.

## Routes

- `/` serves the game page.
- `/api/assets` returns the cyclopse asset manifest for `Walking.fbx`, `Jump.fbx`, and `model_textured.jpg` when present.
- `/assets/<path>` serves files from `E:\AI\3d_modeling\game\assets` after resolving the path under that directory.

## Gameplay

- Click the builder unit to select it.
- Right-click a tile to move the selected builder.
- The lower-left gold mine can be mined by the builder, depositing 1 gold per trip at headquarters.
- Pick a tower type, then click open grass to order the builder to construct it.
- Construction reserves the tile, moves the builder to the site, and completes after a short build time.
- Click an existing tower or its tile to show tower stats in the selection panel.
- Click headquarters to research tower evolution levels 2 and 3.
- After research completes, click an existing tower and evolve it to the unlocked level.
- The build panel exposes four tower buttons:
  `bolt`, `cannon`, `frost`, and `arc`.
- Tower heads rotate toward their current target when an enemy is in range.
- Cannon shots splash nearby enemies, Frost slows enemies, and Arc chains to nearby targets.
- Tower evolution improves each tower's matching strengths: damage, range, cooldown, splash radius, slow strength/duration, or chain count/range.
- Start Wave begins the next round.
- Use the 1x, 2x, and 3x speed buttons to change simulation speed.
- There are 10 rounds total.
- Round 5 is the miniboss wave.
- Round 10 is the final boss.
- Use the mouse wheel over the battlefield to zoom the camera.
- Cyclopse `Walking.fbx` is used for enemies.
- Cyclopse `Jump.fbx` is used as the death animation for now.

## UI Contract

The page keeps the existing JavaScript hooks:

- `#game` for the Three.js canvas root.
- `#hud` for live gold, lives, round, and next-wave text.
- `#status` for current game status.
- `#start-wave` and `#reset-game` for round controls.
- `#selection-panel`, `#selection-kind`, and `#selection-details` for selected worker/tower details.

Optional tower controls use `.tower-button[data-tower-type]` so `game.js` can add multiple
tower types without another template change.
