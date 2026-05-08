import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
import * as SkeletonUtils from "three/addons/utils/SkeletonUtils.js";

const CONFIG = {
  gridSize: 13,
  tileSize: 2,
  maxRounds: 10,
  initialLives: 20,
  initialGold: 180,
  towerCost: 45,
  towerRange: 5.2,
  towerDamage: 18,
  towerCooldown: 0.65,
  cameraMinDistance: 18,
  cameraMaxDistance: 46,
  cameraZoomStep: 2.5,
  enemyModelHeight: 2.2,
  workerSpeed: 5.4,
  workerBuildTime: 1.15,
  workerBuildRange: 1.35,
  workerSpawnGrid: { x: 3, z: 9 },
  mineGrid: { x: 0, z: 12 },
  headquartersGrid: { x: 2, z: 11 },
  miningTime: 0.55,
  depositTime: 0.25,
  workerInteractRange: 1.55,
};

const TOWER_TYPES = {
  bolt: {
    id: "bolt",
    name: "속사포",
    cost: 45,
    range: 5.4,
    damage: 18,
    cooldown: 0.58,
    projectileSpeed: 15,
    color: 0xb68a44,
    accent: 0xfff0a3,
    description: "빠른 단일 대상 포탑",
  },
  cannon: {
    id: "cannon",
    name: "대포",
    cost: 75,
    range: 4.6,
    damage: 34,
    cooldown: 1.22,
    projectileSpeed: 10,
    splashRadius: 1.15,
    color: 0x8a4a32,
    accent: 0xff9f45,
    description: "느리지만 범위 피해를 주는 포탑",
  },
  frost: {
    id: "frost",
    name: "냉기탑",
    cost: 60,
    range: 4.9,
    damage: 9,
    cooldown: 0.9,
    projectileSpeed: 12,
    slowFactor: 0.55,
    slowDuration: 1.6,
    color: 0x4d91b7,
    accent: 0x9fe8ff,
    description: "적중한 적을 둔화시키는 포탑",
  },
  arc: {
    id: "arc",
    name: "번개탑",
    cost: 95,
    range: 5.0,
    damage: 22,
    cooldown: 1.05,
    projectileSpeed: 18,
    chains: 2,
    chainRange: 2.7,
    color: 0x6f5bb8,
    accent: 0xd7b8ff,
    description: "주변 적에게 번개가 연쇄되는 포탑",
  },
};

const TOWER_LEVEL_STATS = {
  1: {
    damage: 1,
    range: 0,
    cooldown: 1,
    projectileSpeed: 1,
    splash: 1,
    slowBonus: 0,
    slowDuration: 0,
    chains: 0,
    chainRange: 0,
  },
  2: {
    damage: 1.35,
    range: 0.35,
    cooldown: 0.9,
    projectileSpeed: 1.12,
    splash: 1.15,
    slowBonus: 0.08,
    slowDuration: 0.35,
    chains: 0,
    chainRange: 0.35,
  },
  3: {
    damage: 1.85,
    range: 0.75,
    cooldown: 0.8,
    projectileSpeed: 1.25,
    splash: 1.32,
    slowBonus: 0.15,
    slowDuration: 0.75,
    chains: 1,
    chainRange: 0.7,
  },
};

const TOWER_RESEARCH = {
  2: {
    level: 2,
    name: "2단계 타워 연구",
    cost: 140,
    time: 8,
    description: "타워 2레벨 진화를 해금합니다.",
  },
  3: {
    level: 3,
    name: "3단계 타워 연구",
    cost: 260,
    time: 12,
    description: "타워 3레벨 진화를 해금합니다.",
  },
};

const PATH = [
  [0, 6], [1, 6], [2, 6], [2, 5], [2, 4], [3, 4], [4, 4], [5, 4],
  [5, 5], [5, 6], [5, 7], [6, 7], [7, 7], [8, 7], [8, 6], [8, 5],
  [9, 5], [10, 5], [10, 6], [10, 7], [11, 7], [12, 7],
];

const ROUND_DEFS = Array.from({ length: CONFIG.maxRounds }, (_, index) => {
  const round = index + 1;
  const isMiniBoss = round === 5;
  const isBoss = round === 10;
  return {
    round,
    count: isBoss ? 1 : isMiniBoss ? 3 : 7 + round * 2,
    interval: isBoss ? 1.2 : Math.max(0.38, 0.9 - round * 0.04),
    hp: isBoss ? 760 : isMiniBoss ? 260 : 55 + round * 23,
    speed: isBoss ? 1.0 : isMiniBoss ? 0.82 : 1.25 + round * 0.035,
    reward: isBoss ? 260 : isMiniBoss ? 95 : 12 + round * 2,
    scale: isBoss ? 1.8 : isMiniBoss ? 1.35 : 1,
    title: isBoss ? "최종 보스" : isMiniBoss ? "중간 보스" : `웨이브 ${round}`,
  };
});

const state = {
  scene: null,
  camera: null,
  cameraTarget: new THREE.Vector3(0, 0, 0),
  cameraDistance: 0,
  renderer: null,
  raycaster: new THREE.Raycaster(),
  pointer: new THREE.Vector2(),
  clock: new THREE.Clock(),
  root: null,
  hud: null,
  status: null,
  startButton: null,
  resetButton: null,
  speedButtons: [],
  selectionPanel: null,
  selectionKind: null,
  selectionDetails: null,
  boardTiles: [],
  groundTiles: [],
  structures: [],
  pathTiles: new Set(PATH.map(([x, z]) => `${x},${z}`)),
  pathPoints: [],
  towers: [],
  enemies: [],
  projectiles: [],
  effects: [],
  mixers: [],
  gold: CONFIG.initialGold,
  lives: CONFIG.initialLives,
  gameSpeed: 1,
  round: 0,
  waveActive: false,
  spawning: null,
  selectedTowerType: "bolt",
  towerControls: null,
  towerButtons: [],
  selectedTower: null,
  selectedStructure: null,
  worker: null,
  research: {
    unlockedTowerLevel: 1,
    active: null,
  },
  assets: {
    walkModel: null,
    walkClip: null,
    deathClip: null,
    texture: null,
  },
};

window.tdDebug = {
  state,
  config: CONFIG,
  rounds: ROUND_DEFS,
  towerTypes: TOWER_TYPES,
  research: TOWER_RESEARCH,
};

init();

async function init() {
  bindDom();
  createScene();
  createEnvironment();
  bindEvents();
  updateHud("사이클롭스 에셋을 불러오는 중...");
  const hasCyclopse = await loadMonsterAssets();
  updateHud(
    hasCyclopse
      ? "건설 유닛을 선택하세요. 우클릭으로 이동하고, 빈 잔디 타일을 클릭해 건설합니다."
      : "사이클롭스 모델 파일을 사용할 수 없어 임시 적 모델을 사용합니다."
  );
  updateSelectionPanel();
  animate();
}

function bindDom() {
  state.root =
    document.querySelector("#game") ||
    document.querySelector("#viewport") ||
    document.querySelector("#scene") ||
    document.body;
  state.hud = document.querySelector("#hud");
  state.status = document.querySelector("#status");
  state.startButton = document.querySelector("#start-wave");
  state.resetButton = document.querySelector("#reset-game");
  state.speedButtons = Array.from(document.querySelectorAll("[data-game-speed]"));
  state.selectionPanel = document.querySelector("#selection-panel");
  state.selectionKind = document.querySelector("#selection-kind");
  state.selectionDetails = document.querySelector("#selection-details");
  state.selectionDetails?.addEventListener("click", onSelectionActionClick);
  createTowerControls();
}

function createTowerControls() {
  const existingButtons = Array.from(document.querySelectorAll("[data-tower-type]"));
  if (existingButtons.length) {
    state.towerButtons = existingButtons.filter((button) => TOWER_TYPES[button.dataset.towerType]);
    for (const button of state.towerButtons) {
      const type = TOWER_TYPES[button.dataset.towerType];
      button.title = `${type.description} | ${type.cost} 골드`;
      button.addEventListener("click", () => selectTowerType(type.id));
    }
    updateTowerControls();
    return;
  }

  const host = document.querySelector("#tower-controls") || state.hud?.parentElement || document.body;
  const panel = document.createElement("div");
  panel.id = "tower-controls-dynamic";
  panel.className = "tower-list";
  panel.style.display = "flex";
  panel.style.flexWrap = "wrap";
  panel.style.gap = "6px";
  panel.style.alignItems = "center";
  panel.style.margin = "8px 0";

  for (const type of Object.values(TOWER_TYPES)) {
    const button = document.createElement("button");
    button.type = "button";
    button.dataset.towerType = type.id;
    button.title = `${type.description} | ${type.cost} 골드`;
    button.textContent = `${type.name} ${type.cost}골드`;
    button.style.border = "1px solid rgba(255,255,255,0.35)";
    button.style.borderRadius = "6px";
    button.style.padding = "6px 9px";
    button.style.cursor = "pointer";
    button.style.background = type.id === state.selectedTowerType ? "#f6d365" : "#24313a";
    button.style.color = type.id === state.selectedTowerType ? "#1b1b1b" : "#f7fbff";
    button.addEventListener("click", () => selectTowerType(type.id));
    panel.appendChild(button);
  }

  host.appendChild(panel);
  state.towerControls = panel;
  state.towerButtons = Array.from(panel.querySelectorAll("button[data-tower-type]"));
}

function selectTowerType(typeId) {
  if (!TOWER_TYPES[typeId]) return;
  state.selectedTowerType = typeId;
  updateTowerControls();
  updateHud();
  const type = TOWER_TYPES[typeId];
  setStatus(
    state.worker?.selected
      ? `${type.name} 선택됨. 빈 잔디 타일을 클릭해 건설 명령을 내리세요.`
      : `${type.name} 선택됨. 배치하려면 먼저 건설 유닛을 선택하세요.`
  );
  updateSelectionPanel();
}

function onSelectionActionClick(event) {
  const button = event.target.closest("[data-action]");
  if (!button) return;

  if (button.dataset.action === "research") {
    startTowerResearch(Number(button.dataset.level));
    return;
  }

  if (button.dataset.action === "upgrade-tower") {
    upgradeSelectedTower();
    return;
  }

  if (button.dataset.action === "mine") {
    orderWorkerMine();
  }
}

function updateTowerControls() {
  if (!state.towerButtons.length) return;
  for (const button of state.towerButtons) {
    const selected = button.dataset.towerType === state.selectedTowerType;
    button.classList.toggle("is-selected", selected);
    if (button.closest("#tower-controls-dynamic")) {
      button.style.background = selected ? "#f6d365" : "#24313a";
      button.style.color = selected ? "#1b1b1b" : "#f7fbff";
    }
  }
}

function createScene() {
  state.scene = new THREE.Scene();
  state.scene.background = new THREE.Color(0x9ec5d6);
  state.scene.fog = new THREE.Fog(0x9ec5d6, 34, 78);

  const width = Math.max(320, state.root.clientWidth || window.innerWidth);
  const height = Math.max(320, state.root.clientHeight || window.innerHeight * 0.72);

  state.camera = new THREE.PerspectiveCamera(50, width / height, 0.1, 200);
  state.camera.position.set(3, 28, 24);
  state.camera.lookAt(state.cameraTarget);
  state.cameraDistance = state.camera.position.distanceTo(state.cameraTarget);

  state.renderer = new THREE.WebGLRenderer({ antialias: true });
  state.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  state.renderer.setSize(width, height);
  state.renderer.shadowMap.enabled = true;
  state.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  state.root.appendChild(state.renderer.domElement);

  const hemi = new THREE.HemisphereLight(0xeaf7ff, 0x496b46, 2.1);
  state.scene.add(hemi);

  const sun = new THREE.DirectionalLight(0xffffff, 2.7);
  sun.position.set(-12, 26, 14);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -24;
  sun.shadow.camera.right = 24;
  sun.shadow.camera.top = 24;
  sun.shadow.camera.bottom = -24;
  state.scene.add(sun);
}

function createEnvironment() {
  const board = CONFIG.gridSize * CONFIG.tileSize;
  const offset = -board / 2 + CONFIG.tileSize / 2;

  const base = new THREE.Mesh(
    new THREE.BoxGeometry(board + 2, 0.5, board + 2),
    new THREE.MeshStandardMaterial({ color: 0x4f7f46, roughness: 0.9 })
  );
  base.position.y = -0.32;
  base.receiveShadow = true;
  state.scene.add(base);

  const tileGeo = new THREE.BoxGeometry(CONFIG.tileSize * 0.94, 0.12, CONFIG.tileSize * 0.94);
  const grassMat = new THREE.MeshStandardMaterial({ color: 0x6ea65b, roughness: 0.8 });
  const grassAltMat = new THREE.MeshStandardMaterial({ color: 0x76b664, roughness: 0.82 });
  const pathMat = new THREE.MeshStandardMaterial({ color: 0xa77b52, roughness: 0.95 });

  for (let x = 0; x < CONFIG.gridSize; x += 1) {
    for (let z = 0; z < CONFIG.gridSize; z += 1) {
      const isPath = state.pathTiles.has(`${x},${z}`);
      const mat = isPath ? pathMat : (x + z) % 2 ? grassMat : grassAltMat;
      const tile = new THREE.Mesh(tileGeo, mat);
      tile.position.set(offset + x * CONFIG.tileSize, 0, offset + z * CONFIG.tileSize);
      tile.receiveShadow = true;
      tile.userData.grid = { x, z, isPath, baseColor: mat.color.getHex() };
      state.scene.add(tile);
      state.boardTiles.push(tile);
      if (!isPath) state.groundTiles.push(tile);
    }
  }

  state.pathPoints = PATH.map(([x, z]) => new THREE.Vector3(
    offset + x * CONFIG.tileSize,
    0.08,
    offset + z * CONFIG.tileSize
  ));

  const line = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints(state.pathPoints.map((p) => p.clone().setY(0.22))),
    new THREE.LineBasicMaterial({ color: 0xf1c27d, transparent: true, opacity: 0.55 })
  );
  state.scene.add(line);

  addScenery(board);
  createEconomyStructures();
  createWorker();
}

function addScenery(board) {
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x6f4a2d, roughness: 0.8 });
  const leafMat = new THREE.MeshStandardMaterial({ color: 0x2f7a4d, roughness: 0.75 });
  const rockMat = new THREE.MeshStandardMaterial({ color: 0x879098, roughness: 0.9 });

  for (let i = 0; i < 26; i += 1) {
    const angle = (i / 26) * Math.PI * 2;
    const radius = board * 0.58 + (i % 5) * 0.55;
    const x = Math.cos(angle) * radius;
    const z = Math.sin(angle) * radius;
    const trunk = new THREE.Mesh(new THREE.CylinderGeometry(0.13, 0.18, 1.3, 7), trunkMat);
    trunk.position.set(x, 0.65, z);
    trunk.castShadow = true;
    const crown = new THREE.Mesh(new THREE.ConeGeometry(0.75, 1.8, 8), leafMat);
    crown.position.set(x, 1.9, z);
    crown.castShadow = true;
    state.scene.add(trunk, crown);
  }

  for (let i = 0; i < 16; i += 1) {
    const rock = new THREE.Mesh(new THREE.DodecahedronGeometry(0.25 + (i % 3) * 0.08), rockMat);
    rock.position.set((Math.random() - 0.5) * board * 1.1, 0.12, (Math.random() - 0.5) * board * 1.1);
    rock.rotation.set(Math.random(), Math.random(), Math.random());
    rock.castShadow = true;
    state.scene.add(rock);
  }
}

function createEconomyStructures() {
  const mineTile = getTileAt(CONFIG.mineGrid.x, CONFIG.mineGrid.z);
  const headquartersTile = getTileAt(CONFIG.headquartersGrid.x, CONFIG.headquartersGrid.z);
  if (mineTile) createGoldMine(mineTile);
  if (headquartersTile) createHeadquarters(headquartersTile);
}

function createGoldMine(tile) {
  const group = new THREE.Group();
  group.position.copy(tile.position);
  group.position.y = 0.16;

  const rockMat = new THREE.MeshStandardMaterial({ color: 0x6f6860, roughness: 0.88 });
  const goldMat = new THREE.MeshStandardMaterial({
    color: 0xf1be62,
    emissive: 0x8f5b16,
    emissiveIntensity: 0.28,
    metalness: 0.25,
    roughness: 0.34,
  });

  const base = new THREE.Mesh(new THREE.DodecahedronGeometry(0.95), rockMat);
  base.scale.set(1.25, 0.58, 1.05);
  base.position.y = 0.42;
  const veinA = new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.14, 0.14), goldMat);
  veinA.position.set(0.05, 0.82, 0.18);
  veinA.rotation.set(0.25, 0.35, -0.22);
  const veinB = new THREE.Mesh(new THREE.BoxGeometry(0.62, 0.12, 0.12), goldMat);
  veinB.position.set(-0.25, 0.58, -0.22);
  veinB.rotation.set(-0.12, -0.55, 0.18);
  const marker = new THREE.Mesh(
    new THREE.RingGeometry(0.95, 1.08, 36),
    new THREE.MeshBasicMaterial({ color: 0xf1be62, transparent: true, opacity: 0.5, side: THREE.DoubleSide })
  );
  marker.rotation.x = Math.PI * -0.5;
  marker.position.y = 0.05;

  for (const part of [base, veinA, veinB]) {
    part.castShadow = true;
    part.receiveShadow = true;
  }
  group.add(base, veinA, veinB, marker);

  const structure = registerStructure({
    kind: "mine",
    name: "금광",
    group,
    tile,
    marker,
    description: "건설 유닛이 왕복하며 1골드씩 채굴합니다.",
  });
  tile.material = tile.material.clone();
  tile.material.color.setHex(0x657345);
  return structure;
}

function createHeadquarters(tile) {
  const group = new THREE.Group();
  group.position.copy(tile.position);
  group.position.y = 0.18;

  const baseMat = new THREE.MeshStandardMaterial({ color: 0x425667, metalness: 0.15, roughness: 0.52 });
  const roofMat = new THREE.MeshStandardMaterial({ color: 0x9b5d45, roughness: 0.58 });
  const glowMat = new THREE.MeshStandardMaterial({
    color: 0x73c7d7,
    emissive: 0x226c78,
    emissiveIntensity: 0.35,
    metalness: 0.2,
    roughness: 0.24,
  });

  const base = new THREE.Mesh(new THREE.BoxGeometry(1.55, 0.8, 1.55), baseMat);
  base.position.y = 0.4;
  const roof = new THREE.Mesh(new THREE.ConeGeometry(1.12, 0.72, 4), roofMat);
  roof.position.y = 1.18;
  roof.rotation.y = Math.PI * 0.25;
  const beacon = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.18, 0.72, 10), glowMat);
  beacon.position.y = 1.72;
  const marker = new THREE.Mesh(
    new THREE.RingGeometry(1.05, 1.2, 40),
    new THREE.MeshBasicMaterial({ color: 0x73c7d7, transparent: true, opacity: 0.45, side: THREE.DoubleSide })
  );
  marker.rotation.x = Math.PI * -0.5;
  marker.position.y = 0.04;

  for (const part of [base, roof, beacon]) {
    part.castShadow = true;
    part.receiveShadow = true;
  }
  group.add(base, roof, beacon, marker);

  const structure = registerStructure({
    kind: "headquarters",
    name: "본진",
    group,
    tile,
    marker,
    description: "타워 2레벨과 3레벨 진화 연구를 진행합니다.",
  });
  tile.material = tile.material.clone();
  tile.material.color.setHex(0x526c7a);
  return structure;
}

function registerStructure(structure) {
  structure.group.userData.structure = structure;
  structure.group.traverse((child) => {
    child.userData.structure = structure;
  });
  structure.marker.visible = false;
  structure.tile.userData.grid.occupied = true;
  structure.tile.userData.grid.structure = structure;
  state.scene.add(structure.group);
  state.structures.push(structure);
  return structure;
}

function createWorker() {
  const spawnTile = getTileAt(CONFIG.workerSpawnGrid.x, CONFIG.workerSpawnGrid.z) || state.groundTiles[0];
  const group = new THREE.Group();
  group.position.copy(spawnTile.position);
  group.position.y = 0.18;

  const bootMat = new THREE.MeshStandardMaterial({ color: 0x232b30, metalness: 0.18, roughness: 0.55 });
  const bodyMat = new THREE.MeshStandardMaterial({ color: 0xd69b4a, metalness: 0.08, roughness: 0.58 });
  const packMat = new THREE.MeshStandardMaterial({ color: 0x47606f, metalness: 0.18, roughness: 0.42 });
  const glowMat = new THREE.MeshBasicMaterial({ color: 0x9fe8ff });

  const feet = new THREE.Mesh(new THREE.CylinderGeometry(0.34, 0.42, 0.18, 10), bootMat);
  feet.position.y = 0.09;
  const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.28, 0.52, 5, 10), bodyMat);
  body.position.y = 0.62;
  const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.23, 16, 12), bodyMat);
  helmet.position.y = 1.18;
  const visor = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.09, 0.05), glowMat);
  visor.position.set(0, 1.2, 0.2);
  const pack = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.48, 0.18), packMat);
  pack.position.set(0, 0.82, -0.24);
  const tool = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.62, 0.1), packMat);
  tool.position.set(0.34, 0.72, 0.06);
  tool.rotation.z = -0.32;

  for (const part of [feet, body, helmet, visor, pack, tool]) {
    part.castShadow = true;
    part.receiveShadow = true;
    part.userData.entity = "worker";
    group.add(part);
  }

  const selectionRing = new THREE.Mesh(
    new THREE.RingGeometry(0.55, 0.68, 36),
    new THREE.MeshBasicMaterial({ color: 0x7eff9f, transparent: true, opacity: 0.72, side: THREE.DoubleSide })
  );
  selectionRing.rotation.x = Math.PI * -0.5;
  selectionRing.position.y = 0.04;
  selectionRing.visible = false;
  group.add(selectionRing);

  const destinationMarker = new THREE.Mesh(
    new THREE.RingGeometry(0.42, 0.58, 32),
    new THREE.MeshBasicMaterial({ color: 0x9fe8ff, transparent: true, opacity: 0.58, side: THREE.DoubleSide })
  );
  destinationMarker.rotation.x = Math.PI * -0.5;
  destinationMarker.position.copy(group.position);
  destinationMarker.position.y = 0.1;
  destinationMarker.visible = false;
  state.scene.add(destinationMarker);

  group.userData.entity = "worker";
  state.scene.add(group);
  state.worker = {
    group,
    selectionRing,
    destinationMarker,
    startPosition: group.position.clone(),
    destination: null,
    selected: false,
    speed: CONFIG.workerSpeed,
    buildJob: null,
    resourceJob: null,
    carryingGold: 0,
  };
}

function bindEvents() {
  window.addEventListener("resize", resize);
  state.renderer.domElement.addEventListener("pointermove", onPointerMove);
  state.renderer.domElement.addEventListener("click", onCanvasClick);
  state.renderer.domElement.addEventListener("contextmenu", onCanvasContextMenu);
  state.renderer.domElement.addEventListener("wheel", onCanvasWheel, { passive: false });
  state.startButton?.addEventListener("click", startWave);
  state.resetButton?.addEventListener("click", resetGame);
  for (const button of state.speedButtons) {
    button.addEventListener("click", () => setGameSpeed(Number(button.dataset.gameSpeed) || 1));
  }
  updateSpeedButtons();
}

async function loadMonsterAssets() {
  try {
    const response = await fetch("/api/assets");
    if (!response.ok) throw new Error(`Asset endpoint returned ${response.status}`);
    const data = await response.json();
    const monster = data?.monster || {};
    const fbxLoader = new FBXLoader();
    const texture = monster.texture ? await loadTexture(monster.texture) : null;
    state.assets.texture = texture;

    if (monster.walk) {
      const walkModel = await loadFbx(fbxLoader, monster.walk);
      prepareModel(walkModel, texture);
      state.assets.walkModel = walkModel;
      state.assets.walkClip = createSeamlessWalkClip(walkModel.animations?.[0] || null);
    }

    if (monster.jump) {
      const deathModel = await loadFbx(fbxLoader, monster.jump);
      state.assets.deathClip = deathModel.animations?.[0] || null;
    }
    return Boolean(state.assets.walkModel);
  } catch (error) {
    console.warn("Monster FBX loading failed; using placeholder enemies.", error);
    setStatus("사이클롭스 모델 파일을 사용할 수 없어 임시 적 모델을 사용합니다.");
    return false;
  }
}

function loadFbx(loader, url) {
  return new Promise((resolve, reject) => {
    loader.load(url, resolve, undefined, reject);
  });
}

function loadTexture(url) {
  return new Promise((resolve) => {
    new THREE.TextureLoader().load(
      url,
      (texture) => {
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.flipY = true;
        texture.needsUpdate = true;
        resolve(texture);
      },
      undefined,
      () => resolve(null)
    );
  });
}

function prepareModel(model, texture) {
  model.traverse((child) => {
    if (!child.isMesh && !child.isSkinnedMesh) return;
    child.castShadow = true;
    child.receiveShadow = true;

    const hasUv = Boolean(child.geometry?.attributes?.uv);
    replaceMaterial(
      child,
      texture && hasUv
        ? new THREE.MeshBasicMaterial({
            map: texture,
            color: 0xffffff,
            side: THREE.DoubleSide,
          })
        : new THREE.MeshBasicMaterial({
            color: 0xb17439,
            side: THREE.DoubleSide,
          })
    );
  });
}

function replaceMaterial(mesh, material) {
  const existingMaterials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
  for (const existingMaterial of existingMaterials) {
    existingMaterial?.dispose?.();
  }
  mesh.material = material;
  mesh.material.needsUpdate = true;
}

function normalizeEnemyModel(model) {
  const box = new THREE.Box3().setFromObject(model);
  const size = box.getSize(new THREE.Vector3());
  const center = box.getCenter(new THREE.Vector3());
  const maxDimension = Number.isFinite(size.x) ? Math.max(size.x, size.y, size.z) || 1 : 1;

  if (Number.isFinite(center.x) && Number.isFinite(size.y)) {
    model.position.sub(center);
    model.position.y += size.y / 2;
  }
  model.scale.multiplyScalar(CONFIG.enemyModelHeight / maxDimension);
}

function createSeamlessWalkClip(clip) {
  if (!clip) return null;

  const tracks = clip.tracks.map((track) => {
    const clonedTrack = track.clone();
    if (isRootMotionPositionTrack(clonedTrack)) {
      lockHorizontalRootMotion(clonedTrack);
    }
    return clonedTrack;
  });

  const seamlessClip = new THREE.AnimationClip(`${clip.name || "walk"}_in_place`, clip.duration, tracks);
  seamlessClip.optimize();
  return seamlessClip;
}

function isRootMotionPositionTrack(track) {
  const trackName = track.name.toLowerCase();
  return (
    track.ValueTypeName === "vector" &&
    trackName.endsWith(".position") &&
    (trackName.includes("hips") || trackName.includes("root") || trackName.includes("armature"))
  );
}

function lockHorizontalRootMotion(track) {
  const values = track.values;
  if (values.length < 3) return;

  const baseX = values[0];
  const baseZ = values[2];
  for (let i = 0; i < values.length; i += 3) {
    values[i] = baseX;
    values[i + 2] = baseZ;
  }
}

function startWave() {
  if (state.waveActive || state.round >= CONFIG.maxRounds || state.lives <= 0) return;
  const def = ROUND_DEFS[state.round];
  state.round = def.round;
  state.waveActive = true;
  state.spawning = {
    def,
    remaining: def.count,
    timer: 0,
  };
  setStatus(`${def.title} 시작.`);
  updateHud();
}

function spawnEnemy(def) {
  const group = createEnemyMesh(def);
  group.position.copy(state.pathPoints[0]);
  group.rotation.y = Math.PI * 0.5;
  state.scene.add(group);

  const enemy = {
    group,
    mixer: null,
    walkAction: null,
    deathAction: null,
    hp: def.hp,
    maxHp: def.hp,
    speed: def.speed,
    reward: def.reward,
    pathIndex: 1,
    reachedEnd: false,
    dying: false,
    deathTimer: 0,
    bar: createHealthBar(),
  };

  enemy.group.add(enemy.bar);
  setupEnemyAnimation(enemy);
  state.enemies.push(enemy);
}

function createEnemyMesh(def) {
  if (state.assets.walkModel) {
    const group = new THREE.Group();
    const model = SkeletonUtils.clone(state.assets.walkModel);
    normalizeEnemyModel(model);
    model.position.y = 0;
    model.traverse((child) => {
      if (child.isMesh || child.isSkinnedMesh) {
        child.castShadow = true;
        child.receiveShadow = true;
      }
    });
    group.add(model);
    group.scale.setScalar(def.scale);
    return group;
  }

  const group = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.CapsuleGeometry(0.42, 0.78, 6, 10),
    new THREE.MeshStandardMaterial({ color: def.round >= 10 ? 0x8e2f3f : def.round === 5 ? 0x8057a8 : 0x466fb2 })
  );
  body.position.y = 0.78;
  body.castShadow = true;

  const eye = new THREE.Mesh(
    new THREE.SphereGeometry(0.12, 16, 12),
    new THREE.MeshStandardMaterial({ color: 0xffe9a6, emissive: 0xaa6a21, emissiveIntensity: 0.6 })
  );
  eye.position.set(0, 1.04, 0.39);

  const horn = new THREE.Mesh(
    new THREE.ConeGeometry(0.12, 0.36, 8),
    new THREE.MeshStandardMaterial({ color: 0xd7d0bf })
  );
  horn.position.set(0, 1.55, 0);
  horn.castShadow = true;

  group.add(body, eye, horn);
  const scale = def.round >= 10 ? 1.9 : def.round === 5 ? 1.45 : 1;
  group.scale.setScalar(scale);
  return group;
}

function setupEnemyAnimation(enemy) {
  const clips = [state.assets.walkClip, state.assets.deathClip].filter(Boolean);
  if (!clips.length) return;
  enemy.mixer = new THREE.AnimationMixer(enemy.group);
  state.mixers.push(enemy.mixer);

  if (state.assets.walkClip) {
    enemy.walkAction = enemy.mixer.clipAction(state.assets.walkClip);
    enemy.walkAction.enabled = true;
    enemy.walkAction.clampWhenFinished = false;
    enemy.walkAction.zeroSlopeAtStart = true;
    enemy.walkAction.zeroSlopeAtEnd = true;
    enemy.walkAction.setLoop(THREE.LoopRepeat, Infinity);
    enemy.walkAction.reset();
    enemy.walkAction.play();
  }
  if (state.assets.deathClip) {
    enemy.deathAction = enemy.mixer.clipAction(state.assets.deathClip);
    enemy.deathAction.setLoop(THREE.LoopOnce, 1);
    enemy.deathAction.clampWhenFinished = true;
  }
}

function createHealthBar() {
  const group = new THREE.Group();
  const back = new THREE.Mesh(
    new THREE.PlaneGeometry(1.15, 0.13),
    new THREE.MeshBasicMaterial({ color: 0x271e25, side: THREE.DoubleSide })
  );
  const fill = new THREE.Mesh(
    new THREE.PlaneGeometry(1.08, 0.08),
    new THREE.MeshBasicMaterial({ color: 0x6ee06a, side: THREE.DoubleSide })
  );
  fill.position.z = 0.01;
  fill.userData.baseScale = fill.scale.x;
  group.add(back, fill);
  group.position.y = 2.25;
  group.userData.fill = fill;
  return group;
}

function onPointerMove(event) {
  const hit = getTileHit(event);
  state.groundTiles.forEach((tile) => {
    if (tile.userData.grid.occupied || tile.userData.grid.reserved) {
      tile.material.emissive?.setHex(0x000000);
      return;
    }
    if (!tile.material.emissive) return;
    tile.material.emissive.setHex(hit?.object === tile ? 0x223f1d : 0x000000);
  });
}

function onCanvasClick(event) {
  if (getWorkerHit(event)) {
    selectWorker();
    return;
  }

  const towerHit = getTowerHit(event);
  if (towerHit) {
    selectTower(towerHit);
    return;
  }

  const structureHit = getStructureHit(event);
  if (structureHit) {
    handleStructureClick(structureHit);
    return;
  }

  const hit = getTileHit(event, state.boardTiles);
  if (!hit) return;
  const tile = hit.object;
  const grid = tile.userData.grid;

  if (grid.occupied && grid.tower) {
    selectTower(grid.tower);
    return;
  }

  if (grid.occupied && grid.structure) {
    handleStructureClick(grid.structure);
    return;
  }

  if (grid.isPath || grid.occupied || grid.reserved) {
    setStatus("타워는 비어 있는 잔디 타일에만 건설할 수 있습니다.");
    return;
  }

  if (!state.worker?.selected) {
    setStatus("먼저 건설 유닛을 선택한 뒤 빈 잔디 타일을 클릭하세요.");
    updateSelectionPanel();
    return;
  }

  const type = TOWER_TYPES[state.selectedTowerType] || TOWER_TYPES.bolt;
  if (state.gold < type.cost) {
    setStatus(`${type.name} 건설에는 골드 ${type.cost}개가 필요합니다.`);
    return;
  }
  issueWorkerBuild(tile, type, hit.point);
}

function onCanvasContextMenu(event) {
  event.preventDefault();
  if (!state.worker?.selected) {
    setStatus("먼저 건설 유닛을 선택한 뒤 이동할 지점을 우클릭하세요.");
    return;
  }

  const hit = getTileHit(event, state.boardTiles);
  if (!hit) return;
  if (hit.object.userData.grid.structure?.kind === "mine") {
    orderWorkerMine();
    return;
  }

  cancelWorkerBuild(true);
  clearWorkerResourceJob();
  moveWorkerTo(hit.point, "건설 유닛 이동 중.");
}

function onCanvasWheel(event) {
  event.preventDefault();
  const currentDistance = state.camera.position.distanceTo(state.cameraTarget);
  const nextDistance = THREE.MathUtils.clamp(
    currentDistance + Math.sign(event.deltaY) * CONFIG.cameraZoomStep,
    CONFIG.cameraMinDistance,
    CONFIG.cameraMaxDistance
  );
  setCameraDistance(nextDistance);
}

function setCameraDistance(distance) {
  const direction = state.camera.position.clone().sub(state.cameraTarget).normalize();
  state.camera.position.copy(state.cameraTarget).addScaledVector(direction, distance);
  state.camera.lookAt(state.cameraTarget);
  state.cameraDistance = distance;
}

function getTileHit(event, tiles = state.groundTiles) {
  const rect = state.renderer.domElement.getBoundingClientRect();
  state.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  state.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  state.raycaster.setFromCamera(state.pointer, state.camera);
  return state.raycaster.intersectObjects(tiles, false)[0] || null;
}

function getWorkerHit(event) {
  if (!state.worker) return null;
  const rect = state.renderer.domElement.getBoundingClientRect();
  state.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  state.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  state.raycaster.setFromCamera(state.pointer, state.camera);
  return state.raycaster.intersectObject(state.worker.group, true)[0] || null;
}

function getTowerHit(event) {
  if (!state.towers.length) return null;
  const rect = state.renderer.domElement.getBoundingClientRect();
  state.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  state.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  state.raycaster.setFromCamera(state.pointer, state.camera);
  const hits = state.raycaster.intersectObjects(state.towers.map((tower) => tower.group), true);
  const hit = hits.find((candidate) => !candidate.object.userData.ignorePick);
  if (!hit) return null;
  return state.towers.find((tower) => isDescendantOf(hit.object, tower.group)) || null;
}

function getStructureHit(event) {
  if (!state.structures.length) return null;
  const rect = state.renderer.domElement.getBoundingClientRect();
  state.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  state.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  state.raycaster.setFromCamera(state.pointer, state.camera);
  const hits = state.raycaster.intersectObjects(state.structures.map((structure) => structure.group), true);
  return hits[0]?.object?.userData?.structure || null;
}

function getTileAt(x, z) {
  return state.boardTiles.find((tile) => tile.userData.grid.x === x && tile.userData.grid.z === z) || null;
}

function isDescendantOf(object, parent) {
  let current = object;
  while (current) {
    if (current === parent) return true;
    current = current.parent;
  }
  return false;
}

function issueWorkerBuild(tile, type, orderPoint = null) {
  cancelWorkerBuild(true);
  clearWorkerResourceJob();

  state.gold -= type.cost;
  tile.userData.grid.reserved = true;
  tile.material = tile.material.clone();
  tile.material.emissive?.setHex(type.accent);
  tile.material.emissiveIntensity = 0.12;

  const site = createBuildSite(tile, type);
  state.worker.buildJob = {
    tile,
    type,
    site,
    progress: 0,
    started: false,
  };

  const standPosition = getWorkerBuildStandPosition(tile, orderPoint);
  moveWorkerTo(standPosition, `${type.name} 건설 명령을 내렸습니다.`);
  updateHud();
  updateSelectionPanel();
}

function getWorkerBuildStandPosition(tile, orderPoint) {
  if (!orderPoint) {
    return tile.position.clone().add(new THREE.Vector3(CONFIG.tileSize * 0.38, 0, CONFIG.tileSize * 0.38));
  }

  const standPosition = orderPoint.clone();
  standPosition.y = tile.position.y;

  const offset = standPosition.clone().sub(tile.position);
  offset.y = 0;
  const maxDistance = CONFIG.workerBuildRange * 0.9;
  if (offset.length() > maxDistance) {
    offset.setLength(maxDistance);
    standPosition.copy(tile.position).add(offset);
  }

  return standPosition;
}

function createTowerStats(type, level) {
  const levelStats = TOWER_LEVEL_STATS[level] || TOWER_LEVEL_STATS[1];
  const stats = {
    level,
    damage: Math.round(type.damage * levelStats.damage),
    range: type.range + levelStats.range,
    cooldown: Math.max(0.12, type.cooldown * levelStats.cooldown),
    projectileSpeed: type.projectileSpeed * levelStats.projectileSpeed,
  };

  if (type.splashRadius) stats.splashRadius = type.splashRadius * levelStats.splash;
  if (type.slowDuration) {
    stats.slowFactor = Math.max(0.28, type.slowFactor - levelStats.slowBonus);
    stats.slowDuration = type.slowDuration + levelStats.slowDuration;
  }
  if (type.chains) {
    stats.chains = type.chains + levelStats.chains;
    stats.chainRange = type.chainRange + levelStats.chainRange;
  }

  return stats;
}

function buildTower(tile, type, options = {}) {
  if (!options.paid) state.gold -= type.cost;
  const stats = createTowerStats(type, 1);
  tile.userData.grid.occupied = true;
  tile.userData.grid.reserved = false;
  tile.material = tile.material.clone();
  tile.material.color.setHex(0x5f8f52);
  tile.material.emissive?.setHex(0x000000);

  const tower = new THREE.Group();
  tower.position.copy(tile.position);
  tower.position.y = 0.22;

  const parts = createTowerMesh(type);

  const range = new THREE.Mesh(
    new THREE.RingGeometry(stats.range - 0.04, stats.range, 56),
    new THREE.MeshBasicMaterial({ color: type.accent, transparent: true, opacity: 0.1, side: THREE.DoubleSide })
  );
  range.rotation.x = Math.PI * -0.5;
  range.position.y = 0.03;
  range.visible = false;
  range.userData.ignorePick = true;

  tower.add(parts.base, parts.turret, range);
  state.scene.add(tower);
  const builtTower = {
    group: tower,
    type,
    level: 1,
    stats,
    turret: parts.turret,
    muzzle: parts.muzzle,
    range,
    upgradeVisual: null,
    cooldown: stats.cooldown * 0.35,
    target: null,
  };
  state.towers.push(builtTower);
  tile.userData.grid.tower = builtTower;
  if (options.select !== false) selectTower(builtTower);
  setStatus(`${type.name} 건설 완료.`);
  updateHud();
}

function selectTower(tower) {
  if (state.worker) {
    state.worker.selected = false;
    state.worker.selectionRing.visible = false;
  }
  state.selectedTower = tower;
  state.selectedStructure = null;
  updateStructureMarkers();
  for (const existingTower of state.towers) {
    existingTower.range.visible = existingTower === tower;
  }
  setStatus(
    `${tower.type.name} ${tower.level}레벨: 피해 ${tower.stats.damage}, 사거리 ${tower.stats.range.toFixed(1)}.`
  );
  updateSelectionPanel();
}

function upgradeSelectedTower() {
  const tower = state.selectedTower;
  if (!tower) return;
  const nextLevel = tower.level + 1;
  if (nextLevel > 3) {
    setStatus("이미 최고 레벨 타워입니다.");
    return;
  }
  if (state.research.unlockedTowerLevel < nextLevel) {
    setStatus(`본진에서 ${nextLevel}단계 타워 연구를 먼저 완료해야 합니다.`);
    return;
  }

  const cost = getTowerUpgradeCost(tower);
  if (state.gold < cost) {
    setStatus(`${tower.type.name} ${nextLevel}레벨 진화에는 골드 ${cost}개가 필요합니다.`);
    return;
  }

  state.gold -= cost;
  applyTowerUpgrade(tower, nextLevel);
  setStatus(`${tower.type.name}이 ${nextLevel}레벨로 진화했습니다.`);
  updateHud();
  updateSelectionPanel();
}

function applyTowerUpgrade(tower, level) {
  tower.level = level;
  tower.stats = createTowerStats(tower.type, level);
  tower.cooldown = Math.min(tower.cooldown, tower.stats.cooldown);
  tower.range.geometry.dispose();
  tower.range.geometry = new THREE.RingGeometry(tower.stats.range - 0.04, tower.stats.range, 56);
  applyTowerLevelVisual(tower);
}

function applyTowerLevelVisual(tower) {
  if (tower.upgradeVisual) {
    state.scene.remove(tower.upgradeVisual);
    tower.group.remove(tower.upgradeVisual);
    tower.upgradeVisual.traverse((child) => {
      child.geometry?.dispose?.();
      child.material?.dispose?.();
    });
  }

  if (tower.level <= 1) {
    tower.upgradeVisual = null;
    return;
  }

  const visual = new THREE.Group();
  const mat = new THREE.MeshBasicMaterial({ color: tower.type.accent, transparent: true, opacity: 0.72 });
  for (let i = 0; i < tower.level - 1; i += 1) {
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.48 + i * 0.12, 0.025, 8, 28), mat.clone());
    ring.rotation.x = Math.PI * 0.5;
    ring.position.y = 1.22 + i * 0.18;
    ring.userData.ignorePick = true;
    visual.add(ring);
  }
  tower.group.add(visual);
  tower.upgradeVisual = visual;
}

function getTowerUpgradeCost(tower) {
  return Math.round(tower.type.cost * (tower.level === 1 ? 0.75 : 1.2));
}

function selectWorker() {
  if (!state.worker) return;
  state.worker.selected = true;
  state.worker.selectionRing.visible = true;
  state.selectedTower = null;
  state.selectedStructure = null;
  updateStructureMarkers();
  for (const tower of state.towers) tower.range.visible = false;
  setStatus("건설 유닛 선택됨. 우클릭으로 이동하거나 빈 잔디 타일을 클릭해 선택한 타워를 건설하세요.");
  updateSelectionPanel();
}

function handleStructureClick(structure) {
  if (structure.kind === "mine" && state.worker?.selected) {
    orderWorkerMine();
    return;
  }
  selectStructure(structure);
}

function selectStructure(structure) {
  if (state.worker) {
    state.worker.selected = false;
    state.worker.selectionRing.visible = false;
  }
  state.selectedTower = null;
  state.selectedStructure = structure;
  for (const tower of state.towers) tower.range.visible = false;
  updateStructureMarkers();
  setStatus(`${structure.name} 선택됨.`);
  updateSelectionPanel();
}

function updateStructureMarkers() {
  for (const structure of state.structures) {
    if (structure.marker) structure.marker.visible = structure === state.selectedStructure;
  }
}

function orderWorkerMine() {
  if (!state.worker) return;
  const mine = getStructureByKind("mine");
  const headquarters = getStructureByKind("headquarters");
  if (!mine || !headquarters) {
    setStatus("금광 또는 본진이 없어 채굴할 수 없습니다.");
    return;
  }

  cancelWorkerBuild(true);
  state.worker.selected = true;
  state.worker.selectionRing.visible = true;
  state.selectedTower = null;
  state.selectedStructure = null;
  updateStructureMarkers();
  for (const tower of state.towers) tower.range.visible = false;
  state.worker.resourceJob = {
    mine,
    headquarters,
    phase: "toMine",
    progress: 0,
  };
  state.worker.carryingGold = 0;
  moveWorkerTo(getWorkerStructureStandPosition(mine), "건설 유닛이 금광으로 이동합니다.");
  updateSelectionPanel();
}

function clearWorkerResourceJob() {
  if (!state.worker?.resourceJob) return;
  state.worker.resourceJob = null;
  state.worker.carryingGold = 0;
  updateSelectionPanel();
}

function getStructureByKind(kind) {
  return state.structures.find((structure) => structure.kind === kind) || null;
}

function getWorkerStructureStandPosition(structure) {
  const direction = state.worker?.group.position.clone().sub(structure.group.position) || new THREE.Vector3(1, 0, 1);
  direction.y = 0;
  if (direction.lengthSq() < 0.001) direction.set(1, 0, 1);
  direction.setLength(CONFIG.workerInteractRange * 0.72);
  const position = structure.group.position.clone().add(direction);
  position.y = state.worker?.group.position.y || 0.18;
  return position;
}

function moveWorkerTo(worldPosition, message) {
  if (!state.worker) return;
  state.worker.destination = worldPosition.clone();
  state.worker.destination.y = state.worker.group.position.y;
  state.worker.destinationMarker.position.copy(state.worker.destination);
  state.worker.destinationMarker.position.y = 0.1;
  state.worker.destinationMarker.visible = true;
  if (message) setStatus(message);
  updateSelectionPanel();
}

function cancelWorkerBuild(refund) {
  const job = state.worker?.buildJob;
  if (!job) return;
  if (refund) state.gold += job.type.cost;
  job.tile.userData.grid.reserved = false;
  job.tile.material.emissive?.setHex(0x000000);
  removeBuildSite(job.site);
  state.worker.buildJob = null;
  updateHud();
  updateSelectionPanel();
}

function createBuildSite(tile, type) {
  const group = new THREE.Group();
  group.position.copy(tile.position);
  group.position.y = 0.2;

  const base = new THREE.Mesh(
    new THREE.CylinderGeometry(0.55, 0.62, 0.12, 18),
    new THREE.MeshStandardMaterial({ color: 0x1f2a30, transparent: true, opacity: 0.82, roughness: 0.62 })
  );
  const scaffold = new THREE.Mesh(
    new THREE.BoxGeometry(0.9, 0.9, 0.9),
    new THREE.MeshBasicMaterial({ color: type.accent, wireframe: true, transparent: true, opacity: 0.55 })
  );
  scaffold.position.y = 0.5;

  const spinGroup = new THREE.Group();
  spinGroup.add(base, scaffold);
  group.add(spinGroup);

  const progressRoot = new THREE.Group();
  progressRoot.position.set(0, 1.18, 0);

  const barWidth = 1.04;
  const barHalf = barWidth / 2;

  const back = new THREE.Mesh(
    new THREE.PlaneGeometry(1.1, 0.12),
    new THREE.MeshBasicMaterial({ color: 0x121920, side: THREE.DoubleSide })
  );
  const fill = new THREE.Mesh(
    new THREE.PlaneGeometry(barWidth, 0.08),
    new THREE.MeshBasicMaterial({ color: type.accent, side: THREE.DoubleSide })
  );
  back.position.set(0, 0, 0.01);
  fill.position.set(0, 0, 0.03);
  fill.userData.barHalf = barHalf;

  progressRoot.add(back, fill);
  group.add(progressRoot);

  group.userData.spinGroup = spinGroup;
  group.userData.progressRoot = progressRoot;
  group.userData.progressFill = fill;
  group.userData.progressBack = back;
  state.scene.add(group);
  return group;
}

function removeBuildSite(site) {
  if (!site) return;
  state.scene.remove(site);
  site.traverse((child) => {
    child.geometry?.dispose?.();
    if (Array.isArray(child.material)) {
      child.material.forEach((material) => material.dispose?.());
    } else {
      child.material?.dispose?.();
    }
  });
}

function createTowerMesh(type) {
  const base = new THREE.Mesh(
    new THREE.CylinderGeometry(0.5, 0.62, 0.45, 14),
    new THREE.MeshStandardMaterial({ color: 0x3f4f55, metalness: 0.15, roughness: 0.55 })
  );
  base.castShadow = true;
  base.receiveShadow = true;

  const turret = new THREE.Group();
  turret.position.y = 0.5;

  const headMat = new THREE.MeshStandardMaterial({ color: type.color, metalness: 0.24, roughness: 0.36 });
  const darkMat = new THREE.MeshStandardMaterial({ color: 0x263139, metalness: 0.36, roughness: 0.24 });
  const accentMat = new THREE.MeshStandardMaterial({
    color: type.accent,
    emissive: type.accent,
    emissiveIntensity: type.id === "arc" || type.id === "frost" ? 0.35 : 0.08,
    metalness: 0.12,
    roughness: 0.3,
  });

  let head;
  let barrel;
  if (type.id === "cannon") {
    head = new THREE.Mesh(new THREE.SphereGeometry(0.38, 18, 12), headMat);
    head.scale.y = 0.78;
    barrel = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.24, 0.95, 14), darkMat);
    barrel.rotation.x = Math.PI * 0.5;
    barrel.position.set(0, 0.05, -0.55);
  } else if (type.id === "frost") {
    head = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.36, 0.58, 6), headMat);
    barrel = new THREE.Mesh(new THREE.ConeGeometry(0.18, 0.88, 10), accentMat);
    barrel.rotation.x = -Math.PI * 0.5;
    barrel.position.set(0, 0.06, -0.58);
  } else if (type.id === "arc") {
    head = new THREE.Mesh(new THREE.OctahedronGeometry(0.36), headMat);
    barrel = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.1, 0.9, 8), accentMat);
    barrel.rotation.x = Math.PI * 0.5;
    barrel.position.set(0, 0.02, -0.56);
  } else {
    head = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.36, 0.72, 12), headMat);
    barrel = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.18, 0.92), darkMat);
    barrel.position.set(0, 0.05, -0.56);
  }

  head.position.y = 0.22;
  head.castShadow = true;
  barrel.castShadow = true;
  turret.add(head, barrel);

  const muzzle = new THREE.Object3D();
  muzzle.position.set(0, 0.57, -0.98);
  turret.add(muzzle);

  return { base, turret, muzzle };
}

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(0.04, state.clock.getDelta()) * state.gameSpeed;
  updateWave(dt);
  updateMixers(dt);
  updateEnemies(dt);
  updateWorker(dt);
  updateResearch(dt);
  updateTowers(dt);
  updateProjectiles(dt);
  updateEffects(dt);
  state.renderer.render(state.scene, state.camera);
}

function setGameSpeed(speed) {
  state.gameSpeed = THREE.MathUtils.clamp(speed, 1, 3);
  updateSpeedButtons();
  updateHud(`${state.gameSpeed}배속으로 변경했습니다.`);
}

function updateSpeedButtons() {
  for (const button of state.speedButtons) {
    button.classList.toggle("is-selected", Number(button.dataset.gameSpeed) === state.gameSpeed);
  }
}

function updateResearch(dt) {
  const research = state.research.active;
  if (!research) return;

  research.progress = Math.min(research.def.time, research.progress + dt);
  if (state.selectedStructure?.kind === "headquarters") updateSelectionPanel();

  if (research.progress >= research.def.time) {
    state.research.unlockedTowerLevel = Math.max(state.research.unlockedTowerLevel, research.level);
    state.research.active = null;
    setStatus(`${research.def.name} 완료. 타워 ${research.level}레벨 진화가 해금되었습니다.`);
    updateHud();
    updateSelectionPanel();
  }
}

function startTowerResearch(level) {
  const def = TOWER_RESEARCH[level];
  if (!def) return;
  if (state.research.active) {
    setStatus("이미 연구가 진행 중입니다.");
    return;
  }
  if (state.research.unlockedTowerLevel >= level) {
    setStatus(`${level}레벨 타워 연구는 이미 완료되었습니다.`);
    return;
  }
  if (level > state.research.unlockedTowerLevel + 1) {
    setStatus("이전 단계 연구를 먼저 완료해야 합니다.");
    return;
  }
  if (state.gold < def.cost) {
    setStatus(`${def.name}에는 골드 ${def.cost}개가 필요합니다.`);
    return;
  }

  state.gold -= def.cost;
  state.research.active = {
    level,
    def,
    progress: 0,
  };
  setStatus(`${def.name} 시작.`);
  updateHud();
  updateSelectionPanel();
}

function updateWave(dt) {
  if (!state.spawning) return;
  state.spawning.timer -= dt;
  if (state.spawning.remaining > 0 && state.spawning.timer <= 0) {
    spawnEnemy(state.spawning.def);
    state.spawning.remaining -= 1;
    state.spawning.timer = state.spawning.def.interval;
  }
  if (state.spawning.remaining <= 0 && state.enemies.length === 0) {
    state.spawning = null;
    state.waveActive = false;
    if (state.round >= CONFIG.maxRounds) {
      setStatus("10라운드를 모두 클리어했습니다. 경로를 지켜냈습니다.");
    } else {
      state.gold += 35 + state.round * 5;
      setStatus(`${state.round}라운드 클리어. 보너스 골드를 획득했습니다.`);
    }
    updateHud();
  }
}

function updateMixers(dt) {
  for (const mixer of state.mixers) mixer.update(dt);
}

function updateEnemies(dt) {
  for (let i = state.enemies.length - 1; i >= 0; i -= 1) {
    const enemy = state.enemies[i];
    updateEnemyStatus(enemy, dt);
    if (enemy.dying) {
      enemy.deathTimer -= dt;
      enemy.group.position.y += dt * 0.3;
      enemy.group.rotation.z += dt * 1.6;
      if (enemy.deathTimer <= 0) removeEnemy(enemy, i);
      continue;
    }

    const targetPoint = state.pathPoints[enemy.pathIndex];
    if (!targetPoint) {
      state.lives -= enemy.maxHp > 500 ? 5 : enemy.maxHp > 180 ? 3 : 1;
      removeEnemy(enemy, i);
      if (state.lives <= 0) {
        state.lives = 0;
        state.waveActive = false;
        state.spawning = null;
        setStatus("패배했습니다. 초기화 후 다시 방어를 준비하세요.");
      }
      updateHud();
      continue;
    }

    const toTarget = targetPoint.clone().sub(enemy.group.position);
    const distance = toTarget.length();
    if (distance < 0.08) {
      enemy.pathIndex += 1;
    } else {
      const step = Math.min(distance, enemy.speed * getEnemySpeedMultiplier(enemy) * dt);
      const direction = toTarget.normalize();
      enemy.group.position.addScaledVector(direction, step);
      enemy.group.rotation.y = Math.atan2(direction.x, direction.z);
    }

    updateHealthBar(enemy);
  }
}

function updateEnemyStatus(enemy, dt) {
  if (!enemy.slowTimer) return;
  enemy.slowTimer = Math.max(0, enemy.slowTimer - dt);
  if (enemy.slowTimer <= 0) {
    enemy.slowFactor = 1;
  }
}

function getEnemySpeedMultiplier(enemy) {
  return enemy.slowTimer > 0 ? enemy.slowFactor || 1 : 1;
}

function updateHealthBar(enemy) {
  if (!enemy.bar) return;
  enemy.bar.lookAt(state.camera.position);
  const fill = enemy.bar.userData.fill;
  const ratio = Math.max(0, enemy.hp / enemy.maxHp);
  fill.scale.x = ratio;
  fill.position.x = -0.54 * (1 - ratio);
  fill.material.color.setHex(ratio > 0.5 ? 0x6ee06a : ratio > 0.25 ? 0xffc857 : 0xff5a5f);
}

function updateWorker(dt) {
  const worker = state.worker;
  if (!worker) return;

  if (worker.destination) {
    const toDestination = worker.destination.clone().sub(worker.group.position);
    toDestination.y = 0;
    const distance = toDestination.length();
    if (distance < 0.06) {
      worker.destination = null;
      worker.destinationMarker.visible = false;
    } else {
      const direction = toDestination.normalize();
      worker.group.position.addScaledVector(direction, Math.min(distance, worker.speed * dt));
      worker.group.rotation.y = Math.atan2(direction.x, direction.z);
    }
  }

  if (worker.resourceJob) updateWorkerResourceJob(worker, worker.resourceJob, dt);
  if (worker.buildJob) updateWorkerBuildJob(worker.buildJob, dt);
}

function updateWorkerResourceJob(worker, job, dt) {
  if (worker.destination) return;

  if (job.phase === "toMine") {
    if (distance2D(worker.group.position, job.mine.group.position) > CONFIG.workerInteractRange) {
      moveWorkerTo(getWorkerStructureStandPosition(job.mine));
      return;
    }
    job.phase = "mining";
    job.progress = 0;
    setStatus("금광에서 1골드를 채굴 중입니다.");
    updateSelectionPanel();
    return;
  }

  if (job.phase === "mining") {
    facePosition(worker.group, job.mine.group.position);
    job.progress = Math.min(CONFIG.miningTime, job.progress + dt);
    if (job.progress >= CONFIG.miningTime) {
      worker.carryingGold = 1;
      job.phase = "toBase";
      job.progress = 0;
      moveWorkerTo(getWorkerStructureStandPosition(job.headquarters), "골드 1개를 본진으로 운반 중입니다.");
    } else if (worker.selected) {
      updateSelectionPanel();
    }
    return;
  }

  if (job.phase === "toBase") {
    if (distance2D(worker.group.position, job.headquarters.group.position) > CONFIG.workerInteractRange) {
      moveWorkerTo(getWorkerStructureStandPosition(job.headquarters));
      return;
    }
    job.phase = "depositing";
    job.progress = 0;
    updateSelectionPanel();
    return;
  }

  if (job.phase === "depositing") {
    facePosition(worker.group, job.headquarters.group.position);
    job.progress = Math.min(CONFIG.depositTime, job.progress + dt);
    if (job.progress >= CONFIG.depositTime) {
      state.gold += worker.carryingGold || 1;
      worker.carryingGold = 0;
      job.phase = "toMine";
      job.progress = 0;
      updateHud();
      moveWorkerTo(getWorkerStructureStandPosition(job.mine), "골드 1개 입금. 다시 채굴하러 이동합니다.");
    } else if (worker.selected) {
      updateSelectionPanel();
    }
  }
}

function updateWorkerBuildJob(job, dt) {
  const worker = state.worker;
  const distanceToSite = distance2D(worker.group.position, job.tile.position);
  if (distanceToSite > CONFIG.workerBuildRange) return;

  worker.destination = null;
  worker.destinationMarker.visible = false;
  job.started = true;
  facePosition(worker.group, job.tile.position);
  job.progress = Math.min(CONFIG.workerBuildTime, job.progress + dt);
  updateBuildSite(job);
  const panelTick = Math.floor((job.progress / CONFIG.workerBuildTime) * 10);
  if (worker.selected && panelTick !== job.panelTick) {
    job.panelTick = panelTick;
    updateSelectionPanel();
  }

  if (job.progress >= CONFIG.workerBuildTime) {
    removeBuildSite(job.site);
    worker.buildJob = null;
    buildTower(job.tile, job.type, { paid: true, select: false });
    setStatus(`${job.type.name} 건설 완료.`);
    updateSelectionPanel();
  }
}

function updateBuildSite(job) {
  if (!job.site) return;
  const ratio = THREE.MathUtils.clamp(job.progress / CONFIG.workerBuildTime, 0, 1);
  const spinGroup = job.site.userData.spinGroup;
  if (spinGroup) spinGroup.rotation.y += 0.035;

  const progressRoot = job.site.userData.progressRoot;
  if (progressRoot) {
    progressRoot.lookAt(state.camera.position);
  }

  const fill = job.site.userData.progressFill;
  if (fill) {
    const half = fill.userData.barHalf ?? 0.52;
    fill.scale.set(Math.max(0.001, ratio), 1, 1);
    fill.position.x = -half * (1 - ratio);
  }
}

function facePosition(group, worldPosition) {
  const direction = worldPosition.clone().sub(group.position);
  direction.y = 0;
  if (direction.lengthSq() > 0.0001) group.rotation.y = Math.atan2(direction.x, direction.z);
}

function distance2D(a, b) {
  return Math.hypot(a.x - b.x, a.z - b.z);
}

function updateTowers(dt) {
  for (const tower of state.towers) {
    tower.cooldown = Math.max(0, tower.cooldown - dt);
    const target = findTowerTarget(tower);
    tower.target = target;
    tower.range.visible = !state.waveActive && tower === state.selectedTower;
    if (!target) {
      tower.turret.rotation.y += dt * 0.45;
      continue;
    }

    const aim = target.group.position.clone();
    aim.y += 0.9;
    aimTurretAt(tower, aim);

    if (tower.cooldown <= 0) {
      tower.cooldown = tower.stats.cooldown;
      fireProjectile(tower, target);
    }
  }
}

function aimTurretAt(tower, worldPoint) {
  const localPoint = tower.group.worldToLocal(worldPoint.clone());
  tower.turret.rotation.y = Math.atan2(localPoint.x, localPoint.z) + Math.PI;
}

function findTowerTarget(tower) {
  let best = null;
  let bestProgress = -1;
  for (const enemy of state.enemies) {
    if (enemy.dying) continue;
    const distance = tower.group.position.distanceTo(enemy.group.position);
    if (distance > tower.stats.range) continue;
    const progress = enemy.pathIndex * 100 - distance;
    if (progress > bestProgress) {
      bestProgress = progress;
      best = enemy;
    }
  }
  return best;
}

function fireProjectile(tower, target) {
  const color = tower.type.accent;
  const stats = tower.stats;
  const mesh = new THREE.Mesh(
    tower.type.id === "cannon"
      ? new THREE.SphereGeometry(0.18, 14, 10)
      : new THREE.SphereGeometry(0.12, 12, 10),
    new THREE.MeshBasicMaterial({ color })
  );
  tower.muzzle.getWorldPosition(mesh.position);
  state.scene.add(mesh);
  state.projectiles.push({
    mesh,
    target,
    type: tower.type,
    stats,
    damage: stats.damage,
    speed: stats.projectileSpeed,
  });
}

function updateProjectiles(dt) {
  for (let i = state.projectiles.length - 1; i >= 0; i -= 1) {
    const projectile = state.projectiles[i];
    if (!projectile.target || projectile.target.dying || !state.enemies.includes(projectile.target)) {
      removeProjectile(projectile, i);
      continue;
    }

    const targetPos = projectile.target.group.position.clone().add(new THREE.Vector3(0, 0.9, 0));
    const toTarget = targetPos.sub(projectile.mesh.position);
    const distance = toTarget.length();
    if (distance < 0.22) {
      applyProjectileImpact(projectile, projectile.target);
      removeProjectile(projectile, i);
      continue;
    }
    projectile.mesh.position.addScaledVector(toTarget.normalize(), Math.min(distance, projectile.speed * dt));
  }
}

function applyProjectileImpact(projectile, target) {
  const type = projectile.type;
  const stats = projectile.stats || type;
  damageEnemy(target, projectile.damage);
  if (stats.splashRadius) {
    createPulse(target.group.position, stats.splashRadius, type.accent);
    for (const enemy of state.enemies) {
      if (enemy === target || enemy.dying) continue;
      const distance = enemy.group.position.distanceTo(target.group.position);
      if (distance <= stats.splashRadius) {
        damageEnemy(enemy, Math.round(projectile.damage * (1 - distance / (stats.splashRadius * 1.6))));
      }
    }
  }
  if (stats.slowDuration) {
    target.slowFactor = stats.slowFactor;
    target.slowTimer = Math.max(target.slowTimer || 0, stats.slowDuration);
    createPulse(target.group.position, 0.8, type.accent);
  }
  if (stats.chains) {
    chainDamage(target, type, stats, stats.chains);
  }
}

function chainDamage(source, type, stats, jumps) {
  let current = source;
  const hit = new Set([source]);
  for (let i = 0; i < jumps; i += 1) {
    let next = null;
    let bestDistance = Infinity;
    for (const enemy of state.enemies) {
      if (hit.has(enemy) || enemy.dying) continue;
      const distance = current.group.position.distanceTo(enemy.group.position);
      if (distance < bestDistance && distance <= stats.chainRange) {
        bestDistance = distance;
        next = enemy;
      }
    }
    if (!next) return;
    createBeam(current.group.position, next.group.position, type.accent);
    damageEnemy(next, Math.round(stats.damage * 0.65));
    hit.add(next);
    current = next;
  }
}

function createPulse(position, radius, color) {
  const pulse = new THREE.Mesh(
    new THREE.RingGeometry(radius * 0.25, radius, 40),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.32, side: THREE.DoubleSide })
  );
  pulse.rotation.x = Math.PI * -0.5;
  pulse.position.copy(position);
  pulse.position.y = 0.18;
  state.scene.add(pulse);
  state.effects.push({ mesh: pulse, life: 0.32, maxLife: 0.32, kind: "pulse" });
}

function createBeam(from, to, color) {
  const points = [
    from.clone().add(new THREE.Vector3(0, 0.9, 0)),
    to.clone().add(new THREE.Vector3(0, 0.9, 0)),
  ];
  const beam = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints(points),
    new THREE.LineBasicMaterial({ color, transparent: true, opacity: 0.75 })
  );
  state.scene.add(beam);
  state.effects.push({ mesh: beam, life: 0.16, maxLife: 0.16, kind: "beam" });
}

function updateEffects(dt) {
  for (let i = state.effects.length - 1; i >= 0; i -= 1) {
    const effect = state.effects[i];
    effect.life -= dt;
    const ratio = Math.max(0, effect.life / effect.maxLife);
    effect.mesh.material.opacity = effect.kind === "beam" ? ratio * 0.75 : ratio * 0.32;
    if (effect.kind === "pulse") {
      const scale = 1 + (1 - ratio) * 0.35;
      effect.mesh.scale.setScalar(scale);
    }
    if (effect.life <= 0) {
      state.scene.remove(effect.mesh);
      effect.mesh.geometry.dispose();
      effect.mesh.material.dispose();
      state.effects.splice(i, 1);
    }
  }
}

function damageEnemy(enemy, amount) {
  enemy.hp -= amount;
  if (enemy.hp <= 0 && !enemy.dying) {
    state.gold += enemy.reward;
    playDeath(enemy);
    updateHud();
  }
}

function playDeath(enemy) {
  enemy.dying = true;
  enemy.deathTimer = enemy.deathAction ? Math.min(1.5, enemy.deathAction.getClip().duration || 1.1) : 0.65;
  if (enemy.walkAction) enemy.walkAction.stop();
  if (enemy.deathAction) {
    enemy.deathAction.reset();
    enemy.deathAction.play();
  }
}

function removeEnemy(enemy, index) {
  state.scene.remove(enemy.group);
  if (enemy.mixer) {
    const mixerIndex = state.mixers.indexOf(enemy.mixer);
    if (mixerIndex >= 0) state.mixers.splice(mixerIndex, 1);
  }
  state.enemies.splice(index, 1);
}

function removeProjectile(projectile, index) {
  state.scene.remove(projectile.mesh);
  projectile.mesh.geometry.dispose();
  projectile.mesh.material.dispose();
  state.projectiles.splice(index, 1);
}

function resetGame() {
  cancelWorkerBuild(false);
  for (const enemy of [...state.enemies]) {
    state.scene.remove(enemy.group);
  }
  for (const projectile of [...state.projectiles]) {
    state.scene.remove(projectile.mesh);
  }
  for (const effect of [...state.effects]) {
    state.scene.remove(effect.mesh);
  }
  for (const tower of [...state.towers]) {
    state.scene.remove(tower.group);
  }
  for (const tile of state.groundTiles) {
    if (tile.userData.grid.structure) {
      tile.userData.grid.occupied = true;
      tile.userData.grid.reserved = false;
      tile.userData.grid.tower = null;
      tile.material.emissive?.setHex(0x000000);
      continue;
    }
    tile.userData.grid.occupied = false;
    tile.userData.grid.tower = null;
    tile.userData.grid.reserved = false;
    tile.material.color.setHex(tile.userData.grid.baseColor);
    tile.material.emissive?.setHex(0x000000);
  }
  resetWorker();
  state.towers = [];
  state.enemies = [];
  state.projectiles = [];
  state.effects = [];
  state.mixers = [];
  state.selectedTower = null;
  state.selectedStructure = null;
  updateStructureMarkers();
  state.research.unlockedTowerLevel = 1;
  state.research.active = null;
  state.gold = CONFIG.initialGold;
  state.lives = CONFIG.initialLives;
  state.round = 0;
  state.waveActive = false;
  state.spawning = null;
  setStatus("새 방어 준비 완료. 타워를 배치하려면 건설 유닛을 선택하세요.");
  updateHud();
  updateSelectionPanel();
}

function resetWorker() {
  if (!state.worker) return;
  state.worker.group.position.copy(state.worker.startPosition);
  state.worker.group.rotation.set(0, 0, 0);
  state.worker.destination = null;
  state.worker.destinationMarker.visible = false;
  state.worker.selected = false;
  state.worker.selectionRing.visible = false;
  state.worker.buildJob = null;
  state.worker.resourceJob = null;
  state.worker.carryingGold = 0;
}

function resize() {
  const width = Math.max(320, state.root.clientWidth || window.innerWidth);
  const height = Math.max(320, state.root.clientHeight || window.innerHeight * 0.72);
  state.camera.aspect = width / height;
  state.camera.updateProjectionMatrix();
  state.renderer.setSize(width, height);
}

function updateHud(message) {
  if (message) setStatus(message);
  if (state.hud) {
    const waveLabel = getWaveLabel();
    state.hud.textContent =
      `골드: ${state.gold} | 생명: ${state.lives} | 라운드: ${state.round}/${CONFIG.maxRounds} | 속도: ${state.gameSpeed}배 | 건설: ${(TOWER_TYPES[state.selectedTowerType] || TOWER_TYPES.bolt).name} | ${waveLabel}`;
  }
  if (state.startButton) {
    state.startButton.disabled = state.waveActive || state.round >= CONFIG.maxRounds || state.lives <= 0;
    state.startButton.textContent = state.waveActive
      ? "웨이브 진행 중"
      : state.round >= CONFIG.maxRounds
        ? "모든 웨이브 완료"
        : "웨이브 시작";
  }
}

function setStatus(text) {
  if (state.status) state.status.textContent = text;
}

function getWaveLabel() {
  if (state.waveActive && state.round > 0) {
    return `진행 중: ${ROUND_DEFS[state.round - 1]?.title || `웨이브 ${state.round}`}`;
  }
  if (state.round >= CONFIG.maxRounds) return "다음: 완료";
  const nextRound = state.round + 1;
  return `다음: ${ROUND_DEFS[nextRound - 1]?.title || `웨이브 ${nextRound}`}`;
}

function updateSelectionPanel() {
  if (!state.selectionDetails || !state.selectionKind) return;

  if (state.selectedTower) {
    const tower = state.selectedTower;
    const nextLevel = tower.level + 1;
    state.selectionKind.textContent = "타워";
    state.selectionDetails.innerHTML = renderSelectionDetails(
      `${tower.type.name} ${tower.level}레벨`,
      [
        ["피해", `${tower.stats.damage}`],
        ["사거리", tower.stats.range.toFixed(1)],
        ["재사용 대기", `${tower.stats.cooldown.toFixed(2)}초`],
        ["대상", tower.target ? "적 추적 중" : "대상 없음"],
        ["특수 효과", getTowerSpecial(tower.type, tower.stats)],
        ["진화 상태", nextLevel <= 3 ? getTowerUpgradeStatus(tower) : "최고 레벨"],
      ],
      "본진 연구가 완료된 레벨까지만 타워를 진화시킬 수 있습니다.",
      getTowerUpgradeAction(tower)
    );
    return;
  }

  if (state.selectedStructure) {
    if (state.selectedStructure.kind === "headquarters") {
      renderHeadquartersPanel();
      return;
    }
    if (state.selectedStructure.kind === "mine") {
      state.selectionKind.textContent = "자원";
      state.selectionDetails.innerHTML = renderSelectionDetails(
        "금광",
        [
          ["채굴량", "왕복당 1골드"],
          ["채굴 시간", `${CONFIG.miningTime.toFixed(2)}초`],
          ["입금 위치", "본진"],
        ],
        "건설 유닛에게 채굴 명령을 내리면 금광과 본진을 계속 왕복합니다.",
        `<div class="selection-actions"><button class="selection-action-button" type="button" data-action="mine">채굴 명령</button></div>`
      );
      return;
    }
  }

  if (state.worker?.selected) {
    const worker = state.worker;
    const type = TOWER_TYPES[state.selectedTowerType] || TOWER_TYPES.bolt;
    const job = worker.buildJob;
    const mining = worker.resourceJob;
    const status = mining
      ? getWorkerMiningStatus(worker)
      : job
      ? job.started
        ? `${job.type.name} 건설 중 ${Math.round((job.progress / CONFIG.workerBuildTime) * 100)}%`
        : `${job.type.name} 건설 위치로 이동 중`
      : worker.destination
        ? "이동 중"
        : "대기 중";
    state.selectionKind.textContent = "건설 유닛";
    state.selectionDetails.innerHTML = renderSelectionDetails(
      "건설 유닛",
      [
        ["상태", status],
        ["소지 골드", `${worker.carryingGold || 0}`],
        ["건설 명령", `${type.name} (${type.cost}골드)`],
        ["이동", "지점 우클릭"],
        ["건설", "빈 잔디 클릭"],
      ],
      "금광을 클릭하면 1골드씩 자동 채굴하고, 건설/이동 명령을 내리면 채굴을 중단합니다.",
      `<div class="selection-actions"><button class="selection-action-button" type="button" data-action="mine">금광 채굴</button></div>`
    );
    return;
  }

  const type = TOWER_TYPES[state.selectedTowerType] || TOWER_TYPES.bolt;
  state.selectionKind.textContent = "없음";
  state.selectionDetails.innerHTML = renderSelectionDetails(
    "선택된 유닛 없음",
    [
      ["현재 건설", `${type.name} (${type.cost}골드)`],
      ["건설 유닛", "건설 유닛 클릭"],
      ["이동", "선택 후 우클릭"],
      ["건설", "선택 후 빈 잔디 클릭"],
    ],
    "타워를 배치하려면 건설 유닛이 필요합니다."
  );
}

function renderHeadquartersPanel() {
  const active = state.research.active;
  const nextResearchLevel = state.research.unlockedTowerLevel >= 3 ? null : state.research.unlockedTowerLevel + 1;
  const rows = [
    ["해금된 진화", `${state.research.unlockedTowerLevel}레벨`],
    ["연구 상태", active ? `${active.def.name} ${Math.round((active.progress / active.def.time) * 100)}%` : "대기 중"],
  ];
  if (nextResearchLevel) {
    const next = TOWER_RESEARCH[nextResearchLevel];
    rows.push(["다음 연구", `${next.name} (${next.cost}골드)`]);
  }

  state.selectionKind.textContent = "본진";
  state.selectionDetails.innerHTML = renderSelectionDetails(
    "본진",
    rows,
    "연구가 끝나면 개별 타워 정보 패널에서 다음 레벨로 진화시킬 수 있습니다.",
    getResearchActions()
  );
}

function renderSelectionDetails(title, rows, note, actions = "") {
  const rowHtml = rows
    .map(([label, value]) => `<div class="selection-row"><span>${label}</span><span>${value}</span></div>`)
    .join("");
  return `<strong>${title}</strong>${rowHtml}${actions}<p>${note}</p>`;
}

function getResearchActions() {
  const buttons = [2, 3].map((level) => {
    const def = TOWER_RESEARCH[level];
    const completed = state.research.unlockedTowerLevel >= level;
    const locked = level > state.research.unlockedTowerLevel + 1;
    const running = state.research.active;
    const disabled = completed || locked || running || state.gold < def.cost;
    const label = completed ? `${level}단계 완료` : `${level}단계 연구`;
    return `<button class="selection-action-button" type="button" data-action="research" data-level="${level}" ${disabled ? "disabled" : ""}>${label}</button>`;
  }).join("");
  return `<div class="selection-actions">${buttons}</div>`;
}

function getTowerUpgradeAction(tower) {
  if (tower.level >= 3) return "";
  const nextLevel = tower.level + 1;
  const cost = getTowerUpgradeCost(tower);
  const unlocked = state.research.unlockedTowerLevel >= nextLevel;
  const disabled = !unlocked || state.gold < cost;
  const label = unlocked ? `${nextLevel}레벨 진화 (${cost}골드)` : `${nextLevel}레벨 연구 필요`;
  return `<div class="selection-actions"><button class="selection-action-button" type="button" data-action="upgrade-tower" ${disabled ? "disabled" : ""}>${label}</button></div>`;
}

function getTowerUpgradeStatus(tower) {
  const nextLevel = tower.level + 1;
  if (state.research.unlockedTowerLevel < nextLevel) return `${nextLevel}레벨 연구 필요`;
  return `${nextLevel}레벨 진화 가능 (${getTowerUpgradeCost(tower)}골드)`;
}

function getWorkerMiningStatus(worker) {
  const job = worker.resourceJob;
  if (!job) return "대기 중";
  if (job.phase === "toMine") return "금광으로 이동 중";
  if (job.phase === "mining") return `채굴 중 ${Math.round((job.progress / CONFIG.miningTime) * 100)}%`;
  if (job.phase === "toBase") return "본진으로 운반 중";
  if (job.phase === "depositing") return "본진에 입금 중";
  return "채굴 중";
}

function getTowerSpecial(type, stats = type) {
  if (stats.splashRadius) return `범위 피해 ${stats.splashRadius.toFixed(1)}`;
  if (stats.slowDuration) return `둔화 ${Math.round((1 - stats.slowFactor) * 100)}% / ${stats.slowDuration.toFixed(1)}초`;
  if (stats.chains) return `${stats.chains}회 연쇄 / ${stats.chainRange.toFixed(1)} 사거리`;
  return type.description;
}
