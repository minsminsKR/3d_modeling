// Four finite school maps:
// 지하 1, 본관 1층 1, 별관 1층 1, 2층 1.
// 2F gallery and B1 cellar stay as authored stair maps; 1F is core + east annex.

export const CORE_RADIUS = 2;
export const PLAYABLE_RADIUS = 8;

export const MAP_LAYOUT = {
  f1a: { id: "f1a", label: "본관 1층", floor: 1, minX: -2, maxX: 2, minZ: -2, maxZ: 2 },
  f1b: { id: "f1b", label: "별관 1층", floor: 1, minX: 4, maxX: 8, minZ: -2, maxZ: 2 },
  b1: { id: "b1", label: "지하", floor: -1 },
  f2: { id: "f2", label: "본관 2층", floor: 2 },
};

export const CORE_EDGES = [
  ["0,0", "0,-1"],
  ["0,-1", "0,-2"],
  ["0,0", "0,1"],
  ["0,1", "0,2"],
  ["0,0", "-1,0"],
  ["-1,0", "-2,0"],
  ["0,0", "1,0"],
  ["1,0", "2,0"],
  ["0,1", "-1,1"],
  ["-1,1", "-2,1"],
  ["0,1", "1,1"],
  ["1,1", "2,1"],
  ["-2,0", "-2,-1"],
  ["-2,-1", "-2,-2"],
  ["-2,0", "-2,1"],
  ["-2,1", "-2,2"],
  ["2,0", "2,-1"],
  ["2,-1", "2,-2"],
  ["2,0", "2,1"],
  ["2,1", "2,2"],
  ["-1,0", "-1,-1"],
  ["-1,-1", "-1,-2"],
  ["-1,1", "-1,2"],
  ["1,0", "1,-1"],
  ["1,-1", "1,-2"],
  ["1,1", "1,2"],
  ["0,-1", "1,-1"],
  ["1,-1", "2,-1"],
  ["-2,-2", "-1,-2"],
  ["-1,-2", "0,-2"],
  ["0,-2", "1,-2"],
  ["1,-2", "2,-2"],
  ["-2,2", "-1,2"],
  ["-1,2", "0,2"],
];

export const CORE_SPOKE_PAIRS = [
  [[2, 0], [3, 0]],
];

export function isCoreCell(cx, cz) {
  return Math.abs(cx) <= CORE_RADIUS && Math.abs(cz) <= CORE_RADIUS;
}

export function isAnnexCell(cx, cz) {
  return cx >= 4 && cx <= 8 && cz >= -2 && cz <= 2;
}

export function isPlayableCell(cx, cz) {
  return isCoreCell(cx, cz) || isAnnexCell(cx, cz) || (cx === 3 && cz === 0);
}

export function isAuthoredCell(cx, cz) {
  return isPlayableCell(cx, cz);
}

export function isWorldCell(cx, cz) {
  return isPlayableCell(cx, cz);
}

export function playableCellCount() {
  return 25 + 1 + 25;
}

export function getMapId(cx, cz, y = 0) {
  if (y < -2.2) return "b1";
  if (y > 3.2) return "f2";
  if (isAnnexCell(cx, cz) || (cx === 3 && cz === 0)) return "f1b";
  if (isCoreCell(cx, cz)) return "f1a";
  return null;
}

export function cellKey(cx, cz) {
  return `${cx},${cz}`;
}

export const SCHOOL_RING_EDGES = [
  ["2,0", "3,0"],
  ["3,0", "4,0"],
  ...buildAnnexSchool(),
];

function buildAnnexSchool() {
  // Authored 별관: east-west spine, south nurse wing, north music wing, east classrooms.
  const pairs = [
    ["4,0", "5,0"], ["5,0", "6,0"], ["6,0", "7,0"], ["7,0", "8,0"],
    ["4,0", "4,1"], ["4,1", "4,2"], ["4,0", "4,-1"], ["4,-1", "4,-2"],
    ["5,0", "5,-1"], ["4,-2", "5,-2"],
    ["6,-2", "6,-1"], ["6,-1", "6,0"], ["6,0", "6,1"], ["6,1", "6,2"],
    ["5,-2", "6,-2"], ["6,-2", "7,-2"], ["7,-2", "8,-2"],
    ["4,1", "5,1"], ["5,0", "5,1"], ["5,1", "5,2"], ["4,2", "5,2"],
    ["5,2", "6,2"], ["6,2", "7,2"], ["7,2", "8,2"],
    ["6,1", "7,1"], ["7,1", "8,1"],
    ["8,-2", "8,-1"],
    ["7,-1", "7,0"], ["7,0", "7,1"],
    ["6,-1", "7,-1"],
  ];
  return pairs;
}

export const MANSION_EDGES = [...CORE_EDGES, ...SCHOOL_RING_EDGES];

export const ADJACENCY = new Map();
for (const [a, b] of MANSION_EDGES) {
  if (!ADJACENCY.has(a)) ADJACENCY.set(a, new Set());
  if (!ADJACENCY.has(b)) ADJACENCY.set(b, new Set());
  ADJACENCY.get(a).add(b);
  ADJACENCY.get(b).add(a);
}

export function getGraphOpenings(cx, cz) {
  const open = { N: false, S: false, E: false, W: false };
  if (!isPlayableCell(cx, cz)) return open;
  const neighbors = ADJACENCY.get(cellKey(cx, cz));
  if (!neighbors) return open;
  if (neighbors.has(cellKey(cx, cz - 1))) open.N = true;
  if (neighbors.has(cellKey(cx, cz + 1))) open.S = true;
  if (neighbors.has(cellKey(cx + 1, cz))) open.E = true;
  if (neighbors.has(cellKey(cx - 1, cz))) open.W = true;
  return open;
}

export function getRingHallType(cx, cz) {
  const open = getGraphOpenings(cx, cz);
  const count = Number(open.N) + Number(open.S) + Number(open.E) + Number(open.W);
  if (count >= 4) return "cross_junction";
  if (count === 3) return "t_junction";
  if (open.N && open.S && !open.E && !open.W) return "corridor_ns";
  if (open.E && open.W && !open.N && !open.S) return "corridor_ew";
  if (count === 1) return "classroom";
  if (count === 2) return "t_junction";
  return "dead_end";
}

export function bfsPlayable(startCx = 0, startCz = 0) {
  const visited = new Set();
  const queue = [[startCx, startCz]];
  visited.add(cellKey(startCx, startCz));
  const mismatched = [];
  while (queue.length) {
    const [cx, cz] = queue.shift();
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
      const key = cellKey(nx, nz);
      if (!visited.has(key) && isPlayableCell(nx, nz)) {
        visited.add(key);
        queue.push([nx, nz]);
      }
    }
  }
  return { visited, mismatched };
}
