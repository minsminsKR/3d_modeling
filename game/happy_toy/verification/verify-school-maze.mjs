import assert from "node:assert/strict";
import {
  PLAYABLE_RADIUS,
  bfsPlayable,
  getGraphOpenings,
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

const east = getGraphOpenings(3, 0);
const voidOpen = getGraphOpenings(PLAYABLE_RADIUS + 1, 0);

console.log({
  reachable: visited.size,
  expected,
  mismatched: mismatched.length,
  types,
  ringWest: east.W,
  outerType: getRingHallType(PLAYABLE_RADIUS, 0),
});

assert.equal(mismatched.length, 0, `unilateral openings: ${mismatched.join(", ")}`);
assert.equal(visited.size, expected, `expected ${expected} playable cells, got ${visited.size}`);
assert.equal(east.W, true, "school wing must open into the core at (3,0)");
assert.equal(voidOpen.N || voidOpen.S || voidOpen.E || voidOpen.W, false);
assert.ok(types.classroom >= 8, `need classrooms in the repeating wing, got ${types.classroom}`);
assert.ok(types.corridor + types.junction >= 40, "outer school must keep long corridors");
console.log("PASS: repeating school maze stays fully connected");
