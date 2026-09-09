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

function edgeKey(a, b) {
  return a < b ? `${a}|${b}` : `${b}|${a}`;
}

function createRandom(seed) {
  return function random() {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

class UnionFind {
  constructor() {
    this.parent = new Map();
  }

  add(id) {
    if (!this.parent.has(id)) this.parent.set(id, id);
  }

  find(id) {
    this.add(id);
    let current = id;
    while (this.parent.get(current) !== current) {
      const parent = this.parent.get(current);
      this.parent.set(current, this.parent.get(parent));
      current = this.parent.get(current);
    }
    return current;
  }

  union(a, b) {
    const parentA = this.find(a);
    const parentB = this.find(b);
    if (parentA === parentB) return false;
    this.parent.set(parentA, parentB);
    return true;
  }

  connected(a, b) {
    return this.find(a) === this.find(b);
  }
}

export function buildRectMaze(minX, maxX, minZ, maxZ, seed, extraEdges = []) {
  const random = createRandom(seed);
  const seen = new Set();
  const edges = [];
  const uf = new UnionFind();

  const tryAdd = (cx1, cz1, cx2, cz2) => {
    const a = cellKey(cx1, cz1);
    const b = cellKey(cx2, cz2);
    const key = edgeKey(a, b);
    if (seen.has(key)) return false;
    seen.add(key);
    edges.push([a, b]);
    uf.union(a, b);
    return true;
  };

  const candidates = [];
  for (let cx = minX; cx <= maxX; cx += 1) {
    for (let cz = minZ; cz <= maxZ; cz += 1) {
      uf.add(cellKey(cx, cz));
      if (cx < maxX) candidates.push([cx, cz, cx + 1, cz]);
      if (cz < maxZ) candidates.push([cx, cz, cx, cz + 1]);
    }
  }

  for (let i = candidates.length - 1; i > 0; i -= 1) {
    const j = Math.floor(random() * (i + 1));
    [candidates[i], candidates[j]] = [candidates[j], candidates[i]];
  }

  for (const [ax, az, bx, bz] of extraEdges) tryAdd(ax, az, bx, bz);
  for (const [ax, az, bx, bz] of candidates) {
    if (!uf.connected(cellKey(ax, az), cellKey(bx, bz))) tryAdd(ax, az, bx, bz);
  }
  for (const [ax, az, bx, bz] of candidates) {
    if (random() < 0.22) tryAdd(ax, az, bx, bz);
  }

  const adjacency = new Map();
  const rebuild = () => {
    adjacency.clear();
    for (const [a, b] of edges) {
      if (!adjacency.has(a)) adjacency.set(a, new Set());
      if (!adjacency.has(b)) adjacency.set(b, new Set());
      adjacency.get(a).add(b);
      adjacency.get(b).add(a);
    }
  };
  rebuild();

  const root = cellKey(minX, 0);
  const connectedThrough = (skip) => {
    const walk = new UnionFind();
    for (const [a, b] of edges) {
      if (edgeKey(a, b) === skip) continue;
      walk.union(a, b);
    }
    for (let cx = minX; cx <= maxX; cx += 1) {
      for (let cz = minZ; cz <= maxZ; cz += 1) {
        if (!walk.connected(cellKey(cx, cz), root)) return false;
      }
    }
    return true;
  };

  const spurs = [];
  for (let cx = minX; cx <= maxX; cx += 1) {
    for (let cz = minZ; cz <= maxZ; cz += 1) {
      if (cx === minX && cz === 0) continue;
      const key = cellKey(cx, cz);
      if ((adjacency.get(key)?.size || 0) >= 3) spurs.push(key);
    }
  }
  for (let i = spurs.length - 1; i > 0; i -= 1) {
    const j = Math.floor(random() * (i + 1));
    [spurs[i], spurs[j]] = [spurs[j], spurs[i]];
  }

  let classrooms = 0;
  for (const key of spurs) {
    if (classrooms >= 6) break;
    for (const neighbor of [...(adjacency.get(key) || [])]) {
      if ((adjacency.get(key)?.size || 0) <= 1) break;
      const skip = edgeKey(key, neighbor);
      if (!connectedThrough(skip)) continue;
      const index = edges.findIndex(([a, b]) => edgeKey(a, b) === skip);
      if (index < 0) continue;
      edges.splice(index, 1);
      seen.delete(skip);
      rebuild();
    }
    if ((adjacency.get(key)?.size || 0) === 1) classrooms += 1;
  }

  return edges;
}

export const SCHOOL_RING_EDGES = [
  ["2,0", "3,0"],
  ["3,0", "4,0"],
  ...buildRectMaze(4, 8, -2, 2, 0x51F1B0, [[4, 0, 5, 0]]),
];

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
