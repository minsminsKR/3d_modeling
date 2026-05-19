# Hungry one eye Cyclopse.io

Browser-based `.io` style multiplayer prototype for `Hungry one eye Cyclopse`.

This version is separate from the Ursina prototype. The Ursina build is useful for local 3D gameplay experimentation; this project is the path toward a web link that friends can open in a browser.

## Run

```powershell
cd E:\AI\3d_modeling\game\hungry_cyclopse\happy_toy_cyclopse_io
node server/index.js
```

Open:

```text
http://localhost:8080
```

No `npm install` is required for this prototype server. The browser client loads Three.js from a CDN.

## Controls

- `WASD`: move
- `Shift`: sprint
- `Mouse`: look
- Eat smaller enemies and players
- Avoid larger enemies and players
- New spawns get a short shield and enemies spawn away from active players.
- Remote movement is smoothed client-side with interpolation, while the local player uses light input prediction.

## Architecture

- `server/index.js`: static HTTP server and WebSocket entry
- `server/ws.js`: dependency-free WebSocket handshake/frame handling
- `server/gameWorld.js`: authoritative simulation
- `public/src/network.js`: browser WebSocket client
- `public/src/input.js`: keyboard/mouse input
- `public/src/scene.js`: Three.js renderer
- `public/src/main.js`: UI and game loop glue
- `/assets/...`: server-mounted source asset folder from `E:\AI\3d_modeling\game\happy_toy\assets`

## Multiplayer Model

The server owns:

- player movement
- stamina
- score
- enemy spawning and AI
- eat/death/knockback contact rules
- respawn

The browser owns:

- input capture
- interpolation-ready rendering
- HUD
- FBX model loading and primitive fallback rendering

This is the correct shape for a public `.io` game because clients do not decide whether they ate something.

## Character Models

The browser client loads character FBX files through Three.js `FBXLoader` and follows the same application pattern as `game/happy_toy`:

- `MeshBasicMaterial` with source texture
- `texture.flipY = true`
- root motion drift removed from looping clips
- actor group positioned at ground level
- visual ground snap after animation updates

- Player and Giant: Cyclopse `Run.fbx`
- Hwacat: `Hip Hop Dancing.fbx`
- Uncat: `Run.fbx`
- Angry: Hwacat_angry `Zombie Run.fbx`

If a model or texture fails to load, the renderer keeps the primitive fallback so the match remains playable.

## Visual Size Calibration

Gameplay collision uses the server radius `size / 5 * 3.4`. The renderer now uses the same base diameter, so size `5` is drawn as a `6.8m` footprint for every character type. FBX files are normalized by horizontal footprint instead of raw height because Cyclopse, Hwacat, Uncat, and Angry have very different original proportions and animation poses.

Open this URL to verify all character models at the same size:

```text
http://localhost:8080/?calibrate=1
```

The green rings in calibration mode share the same gameplay radius. In normal gameplay, each character also has a subtle ground footprint/rim so relative size can be read visually without showing enemy size numbers.

## Third-Person Controls

Camera and movement follow `3d_modeling/model_test`:

- independent camera yaw/pitch
- pitch clamp from `-10` to `38` degrees
- mouse rotation speed `0.006`
- WASD movement relative to camera direction
- character faces movement direction, not raw camera yaw
