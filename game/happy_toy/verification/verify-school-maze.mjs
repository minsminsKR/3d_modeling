import assert from "node:assert/strict";
import {
  PLAYABLE_RADIUS,
  WORLD_RADIUS,
  bfsPlayable,
  getGraphOpenings,
  getRingHallType,
  isPlayableCell,
  isWorldCell,
  playableCellCount,
} from "../src/world/schoolMaze.js";

function collectMismatched(min, max) {
  const mismatched = [];
  for (let cx = min; cx <= max; cx += 1) {
    for (let cz = min; cz <= max; cz += 1) {
      if (!isWorldCell(cx, cz)) continue;
      const open = getGraphOpenings(cx, cz);
      const faces = [
        ["N", 0, -1, "S"],
        ["S", 0, 1, "N"],
        ["E", 1, 0, "W"],
        ["W", -1, 0, "E"],
      ];
      for (const [face, dx, dz, opposite] of faces) {
        if (!open[face]) continue;
        const nx = cx + dx;
        const nz = cz + dz;
        const back = getGraphOpenings(nx, nz);
        if (!back[opposite]) mismatched.push(`${cx},${cz} ${face} -> ${nx},${nz}`);
      }
    }
  }
  return mismatched;
}

const { visited, mismatched } = bfsPlayable(0, 0);
const expected = playableCellCount();
const types = { classroom: 0, corridor: 0, junction: 0, other: 0 };
for (let cx = -PLAYABLE_RADIUS; cx <= PLAYABLE_RADIUS; cx += 1) {
  for (let cz = -PLAYABLE_RADIUS; cz <= PLAYABLE_RADIUS; cz += 1) {
    if (!isPlayableCell(cx, cz)) continue;
    if (Math.abs(cx) <= 2 && Math.abs(cz) <= 2) continue;
    const type = getRingHallType(cx, cz);
    if (type === "classroom") types.classroom += 1;
    else if (type.startsWith("corridor")) types.corridor += 1;
    else if (type.includes("junction")) types.junction += 1;
    else types.other += 1;
  }
}

const east = getGraphOpenings(3, 0);
const beyond = getGraphOpenings(PLAYABLE_RADIUS + 1, 0);
const authoredOuter = getGraphOpenings(PLAYABLE_RADIUS, 0);
const far = getGraphOpenings(20, 0);
const hull = getGraphOpenings(WORLD_RADIUS + 1, 0);
const endlessMismatched = collectMismatched(-20, 20);
const farType = getRingHallType(20, 0);
const deepType = getRingHallType(36, 0);

console.log({
  reachable: visited.size,
  expected,
  mismatched: mismatched.length,
  types,
  ringWest: east.W,
  outerType: getRingHallType(PLAYABLE_RADIUS, 0),
  beyondWest: beyond.W,
  farType,
  deepType,
  endlessMismatched: endlessMismatched.length,
});

assert.equal(mismatched.length, 0, `unilateral openings: ${mismatched.join(", ")}`);
assert.equal(visited.size, expected, `expected ${expected} playable cells, got ${visited.size}`);
assert.equal(east.W, true, "school wing must open into the core at (3,0)");
assert.equal(beyond.W, authoredOuter.E, "endless wing must agree with the authored ring");
assert.equal(beyond.W, true, "school must continue past the authored 25x25");
assert.ok(far.W || far.E || far.N || far.S, "far cardinal hall must stay walkable");
assert.notEqual(farType, "dead_end");
assert.notEqual(deepType, "dead_end");
assert.equal(isWorldCell(20, 0), true);
assert.equal(isWorldCell(WORLD_RADIUS + 1, 0), false);
assert.equal(hull.N || hull.S || hull.E || hull.W, false);
assert.equal(endlessMismatched.length, 0, `endless unilateral openings: ${endlessMismatched.join(", ")}`);
assert.ok(types.classroom >= 8, `need classrooms in the repeating wing, got ${types.classroom}`);
assert.ok(types.corridor + types.junction >= 40, "outer school must keep long corridors");
console.log("PASS: repeating school maze stays fully connected and continues without a void wall");
