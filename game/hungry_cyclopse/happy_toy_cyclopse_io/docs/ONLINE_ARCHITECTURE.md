# Online Architecture

## Goal

Turn `Hungry one eye Cyclopse` into a browser-playable `.io` style game. Players should be able to open a URL, enter a name, and join a shared arena.

## Current Prototype Choice

This scaffold uses:

- Node.js HTTP server
- Dependency-free WebSocket implementation
- Authoritative server simulation
- Three.js browser client loaded from CDN

This avoids the currently broken local `npm` installation while still producing a playable web multiplayer foundation.

## Why Not Flask First

Flask is useful for account pages, leaderboards, room lists, and admin APIs. It is not the best core loop for a fast real-time `.io` arena. The real-time game loop needs a persistent socket protocol and frequent state snapshots.

## Future Upgrade Path

When package management is fixed, replace the custom WebSocket room layer with one of:

- Colyseus: room/matchmaking/state sync
- Nakama: accounts, storage, authoritative matches, operations tooling
- uWebSockets.js or ws: lighter custom server

The gameplay rules in `server/gameWorld.js` are intentionally isolated so they can be moved into Colyseus room logic later.

## Server Authority

Clients send:

- movement keys
- sprint flag
- yaw
- optional display name

Server sends:

- player snapshots
- enemy snapshots
- score/time
- death/eat events

Clients never send size, score, or kill/eat results.

## Asset Serving

The Node server mounts `E:\AI\3d_modeling\game\happy_toy\assets` at `/assets`. The browser loads FBX character files and JPG textures from this route. This keeps the web prototype connected to the same source art as the Ursina prototype while avoiding browser filesystem access restrictions.

## Scaling Model

Initial:

- one Node process
- one arena
- dozens of players

Next:

- room codes
- one `GameWorld` per room
- room list HTTP endpoint

Later:

- process clustering
- load balancer with sticky sessions
- Redis/Nakama/Colyseus presence for matchmaking
- regional servers
