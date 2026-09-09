import assert from "node:assert/strict";
import { StoryDirector } from "../src/events/StoryDirector.js";

const lines = [];
const g = {
  isStarted: true,
  gameOver: false,
  gameCleared: false,
  isPaused: false,
  playTime: 4,
  player: { position: { x: 0, y: 0, z: 0 } },
  _storyBeats: new Set(),
  voiceAnnouncer: { announce(key, text) { lines.push([key, text]); } },
  hud: { setStatus() {} },
};

const director = new StoryDirector(g);
director.update();
assert.equal(director.fired.size, 0);

g.player.position = { x: 12, y: 0, z: 0 };
director.update();
assert.equal(director.fired.has("leaveStart"), true);

g.player.position = { x: 20, y: 0, z: 0 };
director.update();
assert.equal(director.fired.has("f1maze"), true);

g.player.position = { x: 28, y: 0, z: 0 };
director.update();
assert.equal(director.fired.has("f1ring"), true);

g.player.position = { x: 22.7, y: 0, z: -6.45 };
director.update();
assert.equal(director.fired.has("throughClass"), true);

g.player.position = { x: 16, y: 0, z: 24.8 };
director.update();
assert.equal(director.fired.has("stairB1"), true);

g.player.position = { x: 16, y: -5, z: 32 };
director.update();
assert.equal(director.fired.has("b1floor"), true);
assert.equal(g._storyBeats.has("map:b1"), true);

g.player.position = { x: -3.5, y: -5, z: 28.5 };
director.update();
assert.equal(director.fired.has("nursery"), true);

g.player.position = { x: -16, y: 0, z: -8.5 };
director.update();
assert.equal(director.fired.has("stairF2"), true);

g.player.position = { x: -20, y: 5, z: -22 };
director.update();
assert.equal(director.fired.has("f2floor"), true);
assert.equal(g._storyBeats.has("map:f2"), true);

g.player.position = { x: -27.5, y: 5, z: -22 };
director.update();
assert.equal(director.fired.has("shrine"), true);

g.player.position = { x: 8.4, y: -5, z: 48 };
director.update();
assert.equal(director.fired.has("b1deep"), true);

g.player.position = { x: -22.5, y: 5, z: -42 };
director.update();
assert.equal(director.fired.has("f2deep"), true);

g.player.position = { x: 32, y: -5, z: 38 };
director.update();
assert.equal(director.fired.has("b1east"), true);

g.player.position = { x: -22.5, y: 5, z: 2 };
director.update();
assert.equal(director.fired.has("f2south"), true);

const before = lines.length;
director.update();
assert.equal(lines.length, before, "already-fired beats must stay silent");

console.log("PASS: height-based campaign VO fires once per floor and shrine/nursery");
