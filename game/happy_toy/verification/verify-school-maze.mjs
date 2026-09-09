import assert from "node:assert/strict";
import {
  PLAYABLE_RADIUS,
  bfsPlayable,
  getGraphOpenings,
  getMapId,
  getRingHallType,
  isPlayableCell,
  playableCellCount,
} from "../src/world/schoolMaze.js";

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

const link = getGraphOpenings(3, 0);
const annex = getGraphOpenings(4, 0);
const far = getGraphOpenings(8, 0);
const voidEast = getGraphOpenings(9, 0);
const voidNorth = getGraphOpenings(0, 3);

console.log({
  reachable: visited.size,
  expected,
  mismatched: mismatched.length,
  types,
  linkWest: link.W,
  annexWest: annex.W,
  farType: getRingHallType(8, 0),
  maps: {
    core: getMapId(0, 0, 0),
    annex: getMapId(6, 0, 0),
    basement: getMapId(1, 2, -5),
    upper: getMapId(-1, -1, 5),
  },
});

assert.equal(mismatched.length, 0, `unilateral openings: ${mismatched.join(", ")}`);
assert.equal(visited.size, expected, `expected ${expected} playable 1F cells, got ${visited.size}`);
assert.equal(link.W, true, "annex corridor must open into the core at (3,0)");
assert.equal(link.E, true, "annex corridor must open into the east school");
assert.equal(annex.W, true, "별관 must connect back through (4,0)");
assert.equal(visited.has("8,0"), true, "별관 far cell must be reachable");
assert.ok(far.W || far.E || far.N || far.S, "별관 far hall must stay walkable");
assert.equal(voidEast.N || voidEast.S || voidEast.E || voidEast.W, false);
assert.equal(voidNorth.N || voidNorth.S || voidNorth.E || voidNorth.W, false);
assert.equal(isPlayableCell(9, 0), false);
assert.equal(isPlayableCell(20, 0), false);
assert.equal(getMapId(0, 0, 0), "f1a");
assert.equal(getMapId(6, 0, 0), "f1b");
assert.equal(getMapId(1, 2, -5), "b1");
assert.equal(getMapId(-1, -1, 5), "f2");
const nurseOpen = getGraphOpenings(5, -1);
const musicOpen = getGraphOpenings(8, 2);
const eastClass = getGraphOpenings(8, 0);
assert.equal(getRingHallType(5, -1), "classroom", "보건실 must be a dead-end classroom");
assert.equal(nurseOpen.S, true);
assert.equal(nurseOpen.N || nurseOpen.E || nurseOpen.W, false);
assert.equal(getRingHallType(8, 2), "classroom", "음악실 must be a dead-end classroom");
assert.equal(musicOpen.W, true);
assert.equal(musicOpen.N || musicOpen.S || musicOpen.E, false);
assert.equal(getRingHallType(8, 0), "classroom");
assert.equal(eastClass.W, true);
assert.equal(getGraphOpenings(0, -1).E, true, "north hall must side-door into the flicker room");
assert.equal(getGraphOpenings(1, -1).W, true);
assert.equal(getGraphOpenings(1, -1).E, true, "flicker room must loop east around the first maze tile");
assert.equal(getGraphOpenings(2, -1).W, true);
assert.ok(types.classroom >= 2, `need classrooms in the annex, got ${types.classroom}`);
assert.ok(types.corridor + types.junction >= 8, "annex must keep corridors");
console.log("PASS: four finite maps (지하1, 1층2, 2층1) stay connected without an endless hull");
