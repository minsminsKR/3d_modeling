import assert from "node:assert/strict";
import { DreadDirector } from "../src/events/DreadDirector.js";
const point = (x = 0, y = 0, z = 0) => ({ x, y, z, clone() { return point(this.x, this.y, this.z); } });
const events = [];
let clears = 0;
const g = {
  isStarted: true, player: { position: point(), isSprinting: false, isHidden: false },
  hud: { setDread() {}, setStatus() {} }, finalExit: { position: point() },
  enemyManager: { enemies: [], notifyNoiseEvent(p, radius, options) { events.push({ position: { ...p }, ...options }); } },
  horrorEventManager: { getThreatContext() { return { nearSafeLight: false, chasingCount: 0, searchingCount: 0, nearestDistance: Infinity }; } },
  clearGame() { clears++; this.gameCleared = true; },
};
const d = new DreadDirector(g);
const tick = seconds => { for (let i = 0; i < Math.ceil(seconds * 10); i++) d.update(0.1); };
d.onRelic(point(7), 1, 4);
assert.equal(d.phase, "warning");
g.isPaused = true; tick(10); assert.equal(d.timer, 6);
g.isPaused = false; tick(6.1);
assert.equal(d.phase, "hunt");
assert.equal(events[0].position.x, 7);
tick(2); assert.equal(events.length, 1, "quiet movement never broadcasts location");
g.player.isSprinting = true; tick(0.1);
assert.equal(events.at(-1).source, "relic-rattle");
const before = events.length;
g.player.isHidden = true; tick(5);
assert.equal(events.length, before, "hidden player does not rattle");
g.player.isHidden = false; g.player.isSprinting = false;
tick(12); assert.equal(d.phase, "recovery");
tick(21); assert.equal(d.phase, "quiet");
d.beginRitual(); tick(2);
g.player.position.x = 5; tick(0.1);
assert.equal(d.ritualActive, false); assert.equal(clears, 0);
g.player.position.x = 0; d.beginRitual(); tick(6.1);
assert.equal(clears, 1);
d.reset(); assert.equal(d.fear, 0); assert.equal(d.lightScale, 1); assert.equal(d.ritualProgress, 0);
console.log("PASS: warning, pause, hunt, noise, hiding, recovery, interrupted ritual, victory, reset");
