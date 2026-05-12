const CELL_SIZE = 3.2;
const GRID_WIDTH = 19;
const GRID_DEPTH = 27;
const WALL_HEIGHT = 3;
const WALL_THICKNESS = 0.28;

export function createChapterTwoMap(seed = Date.now()) {
  const rng = createRng(seed);
  const noise = new PerlinNoise(seed);
  const open = new Set();
  const mainPath = [];
  const branchEnds = [];
  const centerX = Math.floor(GRID_WIDTH / 2);

  let x = centerX;
  let z = GRID_DEPTH - 2;
  carve(open, mainPath, x, z);

  while (z > 1) {
    const drift = noise.fractal2(x * 0.18, z * 0.18, 3);
    if (rng() < 0.42 + Math.abs(drift) * 0.18) {
      x += drift >= 0 ? 1 : -1;
      x = clamp(x, 2, GRID_WIDTH - 3);
      carve(open, mainPath, x, z);
    }

    z -= 1;
    carve(open, mainPath, x, z);

    if (rng() < 0.14) {
      x += rng() < 0.5 ? -1 : 1;
      x = clamp(x, 2, GRID_WIDTH - 3);
      carve(open, mainPath, x, z);
    }
  }

  const sampledPath = mainPath.filter((_, index) => index % 2 === 0);
  for (const cell of sampledPath) {
    const branchNoise = noise.fractal2(cell.x * 0.23 + 18, cell.z * 0.19 - 8, 2);
    if (rng() > 0.42 + branchNoise * 0.2) {
      continue;
    }

    const horizontal = branchNoise >= 0 ? 1 : -1;
    const length = 2 + Math.floor(rng() * 5);
    let branchX = cell.x;
    let branchZ = cell.z;
    for (let step = 0; step < length; step += 1) {
      branchX = clamp(branchX + horizontal, 1, GRID_WIDTH - 2);
      carve(open, null, branchX, branchZ);
      if (rng() < 0.23) {
        branchZ = clamp(branchZ + (rng() < 0.5 ? -1 : 1), 1, GRID_DEPTH - 2);
        carve(open, null, branchX, branchZ);
      }
    }
    widenPocket(open, branchX, branchZ, rng);
    branchEnds.push({ x: branchX, z: branchZ });
  }

  const startCell = mainPath[0];
  const finalCell = mainPath[mainPath.length - 1];
  const allCells = [...open].map(parseCellKey);
  const farCells = allCells
    .filter((cell) => distanceCells(cell, startCell) > 7 && distanceCells(cell, finalCell) > 3)
    .sort((a, b) => (
      noise.fractal2(b.x * 0.41, b.z * 0.41, 2) + distanceCells(b, startCell) * 0.02
    ) - (
      noise.fractal2(a.x * 0.41, a.z * 0.41, 2) + distanceCells(a, startCell) * 0.02
    ));

  const keyCells = pickUniqueCells([...branchEnds, ...farCells], 3, startCell, finalCell);
  const cabinetCells = pickUniqueCells(farCells.slice().reverse(), 4, startCell, finalCell);
  const patrolCells = pickPatrolCells(mainPath, farCells, finalCell);

  return {
    id: "chapter-2",
    label: "Chapter 2",
    title: "소음 복도",
    seed,
    playerStart: toWorldArray(startCell, 0),
    floorAreas: allCells.map((cell) => makeArea(`chapter2-floor-${cell.x}-${cell.z}`, cell)),
    landingAreas: [
      { id: "chapter2-start", floor: 1, position: toWorldArray(startCell, 0), radius: 0.9 },
      { id: "chapter2-final", floor: 1, position: toWorldArray(finalCell, 0), radius: 0.9 },
    ],
    roomAreas: keyCells.map((cell, index) => ({
      ...makeArea(`chapter2-key-pocket-${index + 1}`, cell),
      type: "room",
      connectedDoorId: null,
    })),
    blockedAreas: [],
    voidAreas: [],
    dropZones: [],
    transitionWaypoints: [],
    ramps: [],
    floorPanels: allCells.map((cell) => ({
      id: `chapter2-floor-panel-${cell.x}-${cell.z}`,
      y: 0,
      position: toWorldArray(cell, 0),
      size: [CELL_SIZE, CELL_SIZE],
      color: 0x171d19,
      ceiling: false,
    })),
    ceilingPanels: allCells.map((cell) => ({
      id: `chapter2-ceiling-${cell.x}-${cell.z}`,
      y: 3.04,
      position: toWorldArray(cell, 3.04),
      size: [CELL_SIZE, 0.22, CELL_SIZE],
      color: 0x0d110d,
    })),
    stairways: [],
    walls: createWalls(open),
    doors: [],
    keys: keyCells.map((cell, index) => ({
      id: `chapter2-key-${index + 1}`,
      label: `소음 열쇠 ${index + 1}`,
      position: pointToArray(offsetInCell(cell, noise, index), 0),
    })),
    cabinets: cabinetCells.map((cell, index) => placeCabinet(cell, open, index)),
    finalExit: {
      id: "chapter2-final-exit",
      label: "낡은 출구 상자",
      position: pointToArray(offsetInCell(finalCell, noise, 8), 0),
    },
    props: createProps(allCells, mainPath, keyCells, finalCell, noise),
    patrolWaypoints: patrolCells.map((cell) => toWorldArray(cell, 0)),
    enemySpawns: [
      toWorldArray(mainPath[Math.min(mainPath.length - 1, Math.floor(mainPath.length * 0.34))], 0),
      toWorldArray(mainPath[Math.min(mainPath.length - 1, Math.floor(mainPath.length * 0.58))], 0),
    ],
  };
}

function carve(open, path, x, z) {
  const key = cellKey(x, z);
  open.add(key);
  if (path && !path.some((cell) => cell.x === x && cell.z === z)) {
    path.push({ x, z });
  }
}

function widenPocket(open, x, z, rng) {
  const shape = rng() < 0.5
    ? [[0, 0], [1, 0], [0, 1], [1, 1]]
    : [[0, 0], [-1, 0], [0, -1], [-1, -1]];
  for (const [dx, dz] of shape) {
    const px = clamp(x + dx, 1, GRID_WIDTH - 2);
    const pz = clamp(z + dz, 1, GRID_DEPTH - 2);
    open.add(cellKey(px, pz));
  }
}

function createWalls(open) {
  const walls = [];
  const directions = [
    { dx: 0, dz: -1, edge: "north" },
    { dx: 1, dz: 0, edge: "east" },
    { dx: 0, dz: 1, edge: "south" },
    { dx: -1, dz: 0, edge: "west" },
  ];

  for (const key of open) {
    const cell = parseCellKey(key);
    const center = toWorld(cell);
    for (const direction of directions) {
      if (open.has(cellKey(cell.x + direction.dx, cell.z + direction.dz))) {
        continue;
      }

      const horizontal = direction.edge === "north" || direction.edge === "south";
      const position = [
        center.x + direction.dx * CELL_SIZE / 2,
        WALL_HEIGHT / 2,
        center.z + direction.dz * CELL_SIZE / 2,
      ];
      const size = horizontal
        ? [CELL_SIZE + WALL_THICKNESS, WALL_HEIGHT, WALL_THICKNESS]
        : [WALL_THICKNESS, WALL_HEIGHT, CELL_SIZE + WALL_THICKNESS];
      walls.push({
        id: `chapter2-wall-${cell.x}-${cell.z}-${direction.edge}`,
        position,
        size,
      });
    }
  }

  return walls;
}

function createProps(allCells, mainPath, keyCells, finalCell, noise) {
  const props = [];
  for (let index = 4; index < mainPath.length; index += 7) {
    const position = pointToArray(offsetInCell(mainPath[index], noise, index), 0.02);
    props.push({
      id: `chapter2-stain-${index}`,
      type: "blood-stain",
      position,
      size: [1.1 + Math.abs(noise.fractal2(index, 0, 2)) * 0.8, 0.55, 1],
    });
  }

  for (let index = 3; index < mainPath.length; index += 9) {
    const cell = mainPath[index];
    const center = toWorld(cell);
    props.push({
      id: `chapter2-wall-lamp-${index}`,
      type: "wall-lamp",
      position: [center.x - CELL_SIZE / 2 + 0.15, 1.55, center.z],
      size: [0.22, 0.42, 0.12],
      rotation: [0, Math.PI / 2, 0],
      intensity: 0.34,
      distance: 4.2,
    });
  }

  for (let index = 0; index < keyCells.length; index += 1) {
    const center = toWorld(keyCells[index]);
    props.push({
      id: `chapter2-paper-mark-${index}`,
      type: "paper-strip",
      position: [center.x, 1.48, center.z - CELL_SIZE / 2 + 0.08],
      size: [0.72, 0.42, 0.03],
    });
  }

  const final = toWorld(finalCell);
  props.push({
    id: "chapter2-final-mannequin",
    type: "mannequin",
    position: [final.x + 1.0, 0, final.z + 0.75],
    size: [0.55, 1.62, 0.34],
    rotation: [0, -Math.PI / 4, 0],
  });

  return props;
}

function pickUniqueCells(candidates, count, startCell, finalCell) {
  const used = new Set([cellKey(startCell.x, startCell.z), cellKey(finalCell.x, finalCell.z)]);
  const result = [];
  for (const candidate of candidates) {
    const key = cellKey(candidate.x, candidate.z);
    if (used.has(key)) {
      continue;
    }
    used.add(key);
    result.push(candidate);
    if (result.length >= count) {
      break;
    }
  }
  return result;
}

function pickPatrolCells(mainPath, farCells, finalCell) {
  const cells = [];
  const mainStep = Math.max(2, Math.floor(mainPath.length / 7));
  for (let index = 0; index < mainPath.length; index += mainStep) {
    cells.push(mainPath[index]);
  }
  cells.push(finalCell);
  cells.push(...farCells.slice(0, 6));
  return pickUniqueCells(cells, 14, mainPath[0], { x: -999, z: -999 });
}

function placeCabinet(cell, open, index) {
  const center = toWorld(cell);
  const options = [
    { dx: 0, dz: -1, yaw: Math.PI, offset: [0, -CELL_SIZE / 2 + 0.48] },
    { dx: 0, dz: 1, yaw: 0, offset: [0, CELL_SIZE / 2 - 0.48] },
    { dx: -1, dz: 0, yaw: -Math.PI / 2, offset: [-CELL_SIZE / 2 + 0.48, 0] },
    { dx: 1, dz: 0, yaw: Math.PI / 2, offset: [CELL_SIZE / 2 - 0.48, 0] },
  ];
  const placement = options.find((option) => !open.has(cellKey(cell.x + option.dx, cell.z + option.dz))) || options[index % options.length];
  return {
    id: `chapter2-cabinet-${index + 1}`,
    label: `소음 복도 캐비넷 ${index + 1}`,
    position: [center.x + placement.offset[0], 0, center.z + placement.offset[1]],
    yaw: placement.yaw,
  };
}

function makeArea(id, cell) {
  const center = toWorld(cell);
  const half = CELL_SIZE / 2;
  return {
    id,
    floor: 1,
    type: "walkable",
    y: 0,
    minX: center.x - half,
    maxX: center.x + half,
    minZ: center.z - half,
    maxZ: center.z + half,
  };
}

function offsetInCell(cell, noise, salt) {
  const center = toWorld(cell);
  return {
    x: center.x + noise.fractal2(cell.x + salt, cell.z, 2) * 0.52,
    z: center.z + noise.fractal2(cell.x, cell.z - salt, 2) * 0.52,
  };
}

function toWorld(cell) {
  return {
    x: (cell.x - Math.floor(GRID_WIDTH / 2)) * CELL_SIZE,
    z: (cell.z - Math.floor(GRID_DEPTH / 2)) * CELL_SIZE,
  };
}

function toWorldArray(cell, y) {
  const world = toWorld(cell);
  return [world.x, y, world.z];
}

function pointToArray(point, y) {
  return [point.x, y, point.z];
}

function cellKey(x, z) {
  return `${x},${z}`;
}

function parseCellKey(key) {
  const [x, z] = key.split(",").map(Number);
  return { x, z };
}

function distanceCells(a, b) {
  return Math.abs(a.x - b.x) + Math.abs(a.z - b.z);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function createRng(seed) {
  let state = Number(seed) >>> 0;
  if (state === 0) {
    state = 0x9e3779b9;
  }
  return () => {
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    return ((state >>> 0) / 4294967296);
  };
}

class PerlinNoise {
  constructor(seed) {
    this.random = createRng(seed ^ 0x6d2b79f5);
    this.permutation = Array.from({ length: 256 }, (_, index) => index);
    for (let index = this.permutation.length - 1; index > 0; index -= 1) {
      const swapIndex = Math.floor(this.random() * (index + 1));
      [this.permutation[index], this.permutation[swapIndex]] = [this.permutation[swapIndex], this.permutation[index]];
    }
    this.permutation.push(...this.permutation);
  }

  fractal2(x, z, octaves) {
    let value = 0;
    let amplitude = 1;
    let frequency = 1;
    let max = 0;
    for (let octave = 0; octave < octaves; octave += 1) {
      value += this.noise2(x * frequency, z * frequency) * amplitude;
      max += amplitude;
      amplitude *= 0.5;
      frequency *= 2;
    }
    return value / max;
  }

  noise2(x, z) {
    const xi = Math.floor(x) & 255;
    const zi = Math.floor(z) & 255;
    const xf = x - Math.floor(x);
    const zf = z - Math.floor(z);
    const u = fade(xf);
    const v = fade(zf);

    const aa = this.permutation[this.permutation[xi] + zi];
    const ab = this.permutation[this.permutation[xi] + zi + 1];
    const ba = this.permutation[this.permutation[xi + 1] + zi];
    const bb = this.permutation[this.permutation[xi + 1] + zi + 1];

    const x1 = lerp(grad2(aa, xf, zf), grad2(ba, xf - 1, zf), u);
    const x2 = lerp(grad2(ab, xf, zf - 1), grad2(bb, xf - 1, zf - 1), u);
    return lerp(x1, x2, v);
  }
}

function fade(value) {
  return value * value * value * (value * (value * 6 - 15) + 10);
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

function grad2(hash, x, z) {
  switch (hash & 3) {
    case 0:
      return x + z;
    case 1:
      return -x + z;
    case 2:
      return x - z;
    default:
      return -x - z;
  }
}
