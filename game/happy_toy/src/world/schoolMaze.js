// Deterministic school-wing graph around the 5×5 narrative core.
// Outer cells form repeating Chebyshev rings so the map reads as an anonymous
// abandoned school rather than a single looping hallway.

export const CORE_RADIUS = 2;
export const PLAYABLE_RADIUS = 12;
export const WORLD_RADIUS = 48;

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

// Only these links may punch into the authored 5×5 so key rooms stay sealed.
export const CORE_SPOKE_PAIRS = [
  [[0, 2], [0, 3]],
  [[2, 0], [3, 0]],
  [[-2, 0], [-3, 0]],
  [[2, 1], [3, 1]],
  [[2, -1], [3, -1]],
  [[-2, 1], [-3, 1]],
  [[-2, -1], [-3, -1]],
];

export function isCoreCell(cx, cz) {
  return Math.abs(cx) <= CORE_RADIUS && Math.abs(cz) <= CORE_RADIUS;
}

export function isPlayableCell(cx, cz) {
  return Math.abs(cx) <= PLAYABLE_RADIUS && Math.abs(cz) <= PLAYABLE_RADIUS;
}

export function isAuthoredCell(cx, cz) {
  return isPlayableCell(cx, cz);
}

export function isWorldCell(cx, cz) {
  return Math.abs(cx) <= WORLD_RADIUS && Math.abs(cz) <= WORLD_RADIUS;
}

export function playableCellCount() {
  const width = PLAYABLE_RADIUS * 2 + 1;
  return width * width;
}

export function cellKey(cx, cz) {
  return `${cx},${cz}`;
}

export function chebyshevRing(radius) {
  const cells = [];
  if (radius <= 0) return [[0, 0]];
  for (let x = -radius; x < radius; x += 1) cells.push([x, -radius]);
  for (let z = -radius; z < radius; z += 1) cells.push([radius, z]);
  for (let x = radius; x > -radius; x -= 1) cells.push([x, radius]);
  for (let z = radius; z > -radius; z -= 1) cells.push([-radius, z]);
  return cells;
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

function inwardNeighbor(cx, cz) {
  const radius = Math.max(Math.abs(cx), Math.abs(cz));
  if (Math.abs(cx) === radius && Math.abs(cz) === radius) return null;
  const nx = Math.abs(cx) === radius ? cx - Math.sign(cx) : cx;
  const nz = Math.abs(cz) === radius ? cz - Math.sign(cz) : cz;
  return [nx, nz];
}

function spokeWingKey(pair) {
  const core = pair[0];
  const wing = pair[1];
  return isCoreCell(core[0], core[1]) ? cellKey(wing[0], wing[1]) : cellKey(core[0], core[1]);
}

export function buildSchoolWingEdges(seed = 0x5C0001) {
  const random = createRandom(seed);
  const seen = new Set();
  const edges = [];

  const tryAdd = (cx1, cz1, cx2, cz2) => {
    if (!isPlayableCell(cx1, cz1) || !isPlayableCell(cx2, cz2)) return false;
    const aCore = isCoreCell(cx1, cz1);
    const bCore = isCoreCell(cx2, cz2);
    if (aCore && bCore) return false;
    if (aCore || bCore) {
      const key = edgeKey(cellKey(cx1, cz1), cellKey(cx2, cz2));
      const allowed = CORE_SPOKE_PAIRS.some(([left, right]) => (
        edgeKey(cellKey(left[0], left[1]), cellKey(right[0], right[1])) === key
      ));
      if (!allowed) return false;
    }
    const a = cellKey(cx1, cz1);
    const b = cellKey(cx2, cz2);
    const key = edgeKey(a, b);
    if (seen.has(key)) return false;
    seen.add(key);
    edges.push([a, b]);
    return true;
  };

  for (let radius = 3; radius <= PLAYABLE_RADIUS; radius += 1) {
    const ring = chebyshevRing(radius);
    for (let i = 0; i < ring.length; i += 1) {
      const [ax, az] = ring[i];
      const [bx, bz] = ring[(i + 1) % ring.length];
      tryAdd(ax, az, bx, bz);
    }
  }

  for (let radius = 4; radius <= PLAYABLE_RADIUS; radius += 1) {
    for (const [cx, cz] of chebyshevRing(radius)) {
      const inward = inwardNeighbor(cx, cz);
      if (!inward) continue;
      const cardinal = cx === 0 || cz === 0;
      if (cardinal || random() < 0.34) {
        tryAdd(cx, cz, inward[0], inward[1]);
      }
    }
  }

  for (const [core, wing] of CORE_SPOKE_PAIRS) {
    tryAdd(core[0], core[1], wing[0], wing[1]);
  }

  const ROOT = "__core__";
  const buildUf = (skip = null) => {
    const uf = new UnionFind();
    uf.add(ROOT);
    for (const pair of CORE_SPOKE_PAIRS) {
      uf.union(ROOT, spokeWingKey(pair));
    }
    for (const [a, b] of edges) {
      if (skip && edgeKey(a, b) === skip) continue;
      uf.union(a, b);
    }
    return uf;
  };

  const wingConnected = (uf) => {
    for (let cx = -PLAYABLE_RADIUS; cx <= PLAYABLE_RADIUS; cx += 1) {
      for (let cz = -PLAYABLE_RADIUS; cz <= PLAYABLE_RADIUS; cz += 1) {
        if (isCoreCell(cx, cz) || !isPlayableCell(cx, cz)) continue;
        if (!uf.connected(cellKey(cx, cz), ROOT)) return false;
      }
    }
    return true;
  };

  let uf = buildUf();
  for (let radius = 3; radius <= PLAYABLE_RADIUS; radius += 1) {
    for (const [cx, cz] of chebyshevRing(radius)) {
      const key = cellKey(cx, cz);
      if (uf.connected(key, ROOT)) continue;
      const inward = inwardNeighbor(cx, cz);
      if (!inward) continue;
      if (tryAdd(cx, cz, inward[0], inward[1])) {
        uf.union(key, cellKey(inward[0], inward[1]));
      }
    }
  }

  const adjacency = new Map();
  const rebuildAdjacency = () => {
    adjacency.clear();
    for (const [a, b] of edges) {
      if (!adjacency.has(a)) adjacency.set(a, new Set());
      if (!adjacency.has(b)) adjacency.set(b, new Set());
      adjacency.get(a).add(b);
      adjacency.get(b).add(a);
    }
  };
  rebuildAdjacency();

  const candidates = [];
  for (let radius = 3; radius <= PLAYABLE_RADIUS; radius += 1) {
    for (const [cx, cz] of chebyshevRing(radius)) {
      if (cx === 0 || cz === 0) continue;
      const key = cellKey(cx, cz);
      if ((adjacency.get(key)?.size || 0) >= 3) candidates.push(key);
    }
  }
  for (let i = candidates.length - 1; i > 0; i -= 1) {
    const j = Math.floor(random() * (i + 1));
    [candidates[i], candidates[j]] = [candidates[j], candidates[i]];
  }

  let classrooms = 0;
  const classroomTarget = Math.max(24, Math.floor(playableCellCount() * 0.045));
  for (const key of candidates) {
    if (classrooms >= classroomTarget) break;
    const neighbors = [...(adjacency.get(key) || [])];
    for (const neighbor of neighbors) {
      if ((adjacency.get(key)?.size || 0) <= 1) break;
      const skip = edgeKey(key, neighbor);
      if (!wingConnected(buildUf(skip))) continue;
      const index = edges.findIndex(([a, b]) => edgeKey(a, b) === skip);
      if (index < 0) continue;
      edges.splice(index, 1);
      seen.delete(skip);
      rebuildAdjacency();
    }
    if ((adjacency.get(key)?.size || 0) === 1) classrooms += 1;
  }

  return edges;
}

export const SCHOOL_RING_EDGES = buildSchoolWingEdges();
export const MANSION_EDGES = [...CORE_EDGES, ...SCHOOL_RING_EDGES];

export const ADJACENCY = new Map();
for (const [a, b] of MANSION_EDGES) {
  if (!ADJACENCY.has(a)) ADJACENCY.set(a, new Set());
  if (!ADJACENCY.has(b)) ADJACENCY.set(b, new Set());
  ADJACENCY.get(a).add(b);
  ADJACENCY.get(b).add(a);
}

function hash01(x, z, salt = 0) {
  let h = 0x9e3779b9
    ^ Math.imul(x | 0, 0x85ebca6b)
    ^ Math.imul(z | 0, 0xc2b2ae35)
    ^ Math.imul(salt | 0, 0x27d4eb2d);
  h = Math.imul(h ^ (h >>> 16), 0x7feb352d);
  h = Math.imul(h ^ (h >>> 15), 0x846ca68b);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

function edgeUnit(cx1, cz1, cx2, cz2, salt = 1) {
  const ordered = cx1 < cx2 || (cx1 === cx2 && cz1 < cz2);
  const x1 = ordered ? cx1 : cx2;
  const z1 = ordered ? cz1 : cz2;
  const x2 = ordered ? cx2 : cx1;
  const z2 = ordered ? cz2 : cz1;
  return hash01(x1 * 131 + z1, x2 * 131 + z2, salt);
}

function chebyshevRadius(cx, cz) {
  return Math.max(Math.abs(cx), Math.abs(cz));
}

function sameRingAdjacent(cx1, cz1, cx2, cz2) {
  if (chebyshevRadius(cx1, cz1) !== chebyshevRadius(cx2, cz2)) return false;
  return Math.abs(cx1 - cx2) + Math.abs(cz1 - cz2) === 1;
}

function isAuthoredClassroomCell(cx, cz) {
  if (!isAuthoredCell(cx, cz) || isCoreCell(cx, cz)) return false;
  return (ADJACENCY.get(cellKey(cx, cz))?.size || 0) === 1;
}

function classroomHash(cx, cz) {
  if (cx === 0 || cz === 0) return false;
  const radius = chebyshevRadius(cx, cz);
  if (Math.abs(cx) === radius && Math.abs(cz) === radius) return false;
  if (!inwardNeighbor(cx, cz)) return false;
  return hash01(cx, cz, 77) < 0.07;
}

function isInfiniteClassroomCell(cx, cz) {
  if (isAuthoredCell(cx, cz) || !isWorldCell(cx, cz)) return false;
  if (!classroomHash(cx, cz)) return false;
  const inward = inwardNeighbor(cx, cz);
  if (!inward || !isWorldCell(inward[0], inward[1])) return false;
  if (isAuthoredCell(inward[0], inward[1])) {
    return !isAuthoredClassroomCell(inward[0], inward[1]);
  }
  return !classroomHash(inward[0], inward[1]);
}

function infiniteEdgeOpen(cx1, cz1, cx2, cz2) {
  if (!isWorldCell(cx1, cz1) || !isWorldCell(cx2, cz2)) return false;
  if (Math.abs(cx1 - cx2) + Math.abs(cz1 - cz2) !== 1) return false;

  const aAuth = isAuthoredCell(cx1, cz1);
  const bAuth = isAuthoredCell(cx2, cz2);
  if (aAuth && bAuth) return false;

  const classroomA = aAuth ? isAuthoredClassroomCell(cx1, cz1) : isInfiniteClassroomCell(cx1, cz1);
  const classroomB = bAuth ? isAuthoredClassroomCell(cx2, cz2) : isInfiniteClassroomCell(cx2, cz2);
  if (classroomA && classroomB) return false;

  const connectClassroom = (cx, cz, nx, nz) => {
    const inward = inwardNeighbor(cx, cz);
    return Boolean(inward) && inward[0] === nx && inward[1] === nz;
  };

  if (classroomA) return connectClassroom(cx1, cz1, cx2, cz2);
  if (classroomB) return connectClassroom(cx2, cz2, cx1, cz1);

  if (sameRingAdjacent(cx1, cz1, cx2, cz2)) return true;

  const radiusA = chebyshevRadius(cx1, cz1);
  const radiusB = chebyshevRadius(cx2, cz2);
  if (Math.abs(radiusA - radiusB) !== 1) return false;

  const outer = radiusA > radiusB ? [cx1, cz1] : [cx2, cz2];
  const inner = radiusA > radiusB ? [cx2, cz2] : [cx1, cz1];
  const inward = inwardNeighbor(outer[0], outer[1]);
  if (!inward || inward[0] !== inner[0] || inward[1] !== inner[1]) return false;
  if (isAuthoredCell(inner[0], inner[1]) && isAuthoredClassroomCell(inner[0], inner[1])) return false;
  if (isInfiniteClassroomCell(inner[0], inner[1])) return false;
  if (outer[0] === 0 || outer[1] === 0) return true;
  return edgeUnit(cx1, cz1, cx2, cz2, 34) < 0.34;
}

export function hasMazeEdge(cx1, cz1, cx2, cz2) {
  if (!isWorldCell(cx1, cz1) || !isWorldCell(cx2, cz2)) return false;
  if (isAuthoredCell(cx1, cz1) && isAuthoredCell(cx2, cz2)) {
    return ADJACENCY.get(cellKey(cx1, cz1))?.has(cellKey(cx2, cz2)) === true;
  }
  return infiniteEdgeOpen(cx1, cz1, cx2, cz2);
}

export function getGraphOpenings(cx, cz) {
  const open = { N: false, S: false, E: false, W: false };
  if (!isWorldCell(cx, cz)) return open;
  if (hasMazeEdge(cx, cz, cx, cz - 1)) open.N = true;
  if (hasMazeEdge(cx, cz, cx, cz + 1)) open.S = true;
  if (hasMazeEdge(cx, cz, cx + 1, cz)) open.E = true;
  if (hasMazeEdge(cx, cz, cx - 1, cz)) open.W = true;
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
