// 꼬마선충 커넥톰 시뮬레이션 — 사이클롭스 몸체, 별도 웹 가상공간.
import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import * as SkeletonUtils from "three/addons/utils/SkeletonUtils.js";
import { loadConnectomeData, breedConnections } from "./connectome.js";
import { createWorld, Food, WORLD_RADIUS } from "./world.js";
import { WormAgent } from "./agent.js";

const MODEL_URL = "/model/Cyclopse/mixamo/Walking.fbx";
const JUMP_URL = "/model/Cyclopse/mixamo/Jump.fbx";
const TEXTURE_URL = "/assets/characters/Cyclopse/source/model_textured.jpg";
const CHARACTER_HEIGHT = 1.9;
const MAX_POPULATION = 14;
const INITIAL_AGENTS = 3;
const INITIAL_FOOD = 4;

const statusText = document.querySelector("#status");
const setStatus = (msg) => {
  statusText.textContent = msg;
};

// --- 렌더러 / 씬 / 카메라 ---
const viewport = document.querySelector("#viewport");
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(55, window.innerWidth / window.innerHeight, 0.1, 400);
camera.position.set(0, 26, -36);

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
viewport.appendChild(renderer.domElement);

const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 0, 0);
controls.maxPolarAngle = Math.PI * 0.49;
controls.minDistance = 6;
controls.maxDistance = 90;
controls.enableDamping = true;

window.addEventListener("resize", () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});

const { obstacles } = createWorld(scene);

// --- 시뮬레이션 매니저 ---
const sim = {
  obstacles,
  agents: [],
  foods: [],
  deadCount: 0,
  maxGeneration: 0,

  findNearestFood(position, range) {
    let best = null;
    let bestDist = range;
    for (const food of this.foods) {
      if (food.depleted) continue;
      const d = position.distanceTo(food.position);
      if (d < bestDist) {
        bestDist = d;
        best = food;
      }
    }
    return best;
  },

  onAgentDeath(agent) {
    this.deadCount++;
    if (selectedAgent === agent) selectAgent(null);
  },

  removeAgent(agent) {
    agent.dispose(scene);
    const i = this.agents.indexOf(agent);
    if (i >= 0) this.agents.splice(i, 1);
  },
};

// --- 사이클롭스 모델 로딩 ---
let characterTemplate = null;
let walkClip = null;
let jumpClip = null;
let templateScale = 1;
let templateYOffset = 0;

function loadFbx(url) {
  return new Promise((resolve, reject) => {
    new FBXLoader().load(url, resolve, undefined, reject);
  });
}

function loadTexture(url) {
  return new Promise((resolve) => {
    new THREE.TextureLoader().load(
      url,
      (texture) => {
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.flipY = true;
        resolve(texture);
      },
      undefined,
      () => resolve(null),
    );
  });
}

async function loadCharacter() {
  setStatus("사이클롭스 모델 로딩 중...");
  const [walkFbx, jumpFbx, texture] = await Promise.all([
    loadFbx(MODEL_URL),
    loadFbx(JUMP_URL).catch(() => null),
    loadTexture(TEXTURE_URL),
  ]);

  // Mixamo 클립의 루트 모션(position 트랙) 제거 → 제자리 애니메이션으로 변환.
  // 이동은 커넥톰 운동 출력이 담당한다.
  const stripRootMotion = (clip) => {
    if (!clip) return null;
    clip.tracks = clip.tracks.filter((track) => !track.name.endsWith(".position"));
    return clip;
  };
  walkClip = stripRootMotion(walkFbx.animations[0] ?? null);
  jumpClip = stripRootMotion(jumpFbx?.animations[0] ?? null);

  // model_test에서 검증된 방식: Hunyuan 메시는 노멀이 불안정해 조명 기반 재질에서
  // 검게 보일 수 있으므로, 텍스처를 그대로 보여주는 MeshBasicMaterial을 사용한다.
  const material = texture
    ? new THREE.MeshBasicMaterial({ map: texture, color: 0xffffff, side: THREE.DoubleSide })
    : new THREE.MeshBasicMaterial({ color: 0xb17439, side: THREE.DoubleSide });

  walkFbx.traverse((child) => {
    if (child.isMesh || child.isSkinnedMesh) {
      const oldMaterials = Array.isArray(child.material) ? child.material : [child.material];
      for (const m of oldMaterials) m?.dispose?.();
      child.material = material;
      child.castShadow = true;
      child.frustumCulled = false;
    }
  });

  const box = new THREE.Box3().setFromObject(walkFbx);
  const height = Math.max(0.001, box.max.y - box.min.y);
  templateScale = CHARACTER_HEIGHT / height;
  // 발바닥이 지면(y=0)에 닿도록 피벗 오프셋 보정
  templateYOffset = -box.min.y * templateScale;
  characterTemplate = walkFbx;
}

// 에이전트 하나 분량의 시각 요소를 복제 생성
function visualFactory() {
  const model = SkeletonUtils.clone(characterTemplate);
  model.scale.setScalar(templateScale);
  model.position.y = templateYOffset;

  const group = new THREE.Group();
  group.add(model);

  const mixer = new THREE.AnimationMixer(model);
  let walkAction = null;
  let jumpAction = null;
  if (walkClip) {
    walkAction = mixer.clipAction(walkClip);
    walkAction.play();
  }
  if (jumpClip) {
    jumpAction = mixer.clipAction(jumpClip);
  }
  return { group, mixer, walkAction, jumpAction };
}

// --- 개체 / 먹이 생성 ---
function randomWorldPosition(maxR = WORLD_RADIUS - 5) {
  const angle = Math.random() * Math.PI * 2;
  const r = 3 + Math.random() * (maxR - 3);
  return new THREE.Vector3(Math.sin(angle) * r, 0, Math.cos(angle) * r);
}

function spawnAgent({ position = null, generation = 0, connections = null } = {}) {
  if (sim.agents.length >= MAX_POPULATION) {
    setStatus(`개체 수 상한(${MAX_POPULATION})에 도달했습니다.`);
    return null;
  }
  const agent = new WormAgent(sim, {
    position: position ?? randomWorldPosition(),
    generation,
    connections,
  });
  agent.attachVisual(visualFactory, scene);
  sim.agents.push(agent);
  sim.maxGeneration = Math.max(sim.maxGeneration, generation);
  return agent;
}

function spawnFood(position = null, amount = 40) {
  sim.foods.push(new Food(scene, position ?? randomWorldPosition(WORLD_RADIUS - 4), amount));
}

// --- 번식 판정 ---
let breedCheckTimer = 0;
function checkBreeding(dt) {
  breedCheckTimer -= dt;
  if (breedCheckTimer > 0) return;
  breedCheckTimer = 0.5;

  const agents = sim.agents;
  for (let i = 0; i < agents.length; i++) {
    for (let j = i + 1; j < agents.length; j++) {
      const a = agents[i];
      const b = agents[j];
      if (!a.canBreed || !b.canBreed) continue;
      if (a.position.distanceTo(b.position) > 2.6) continue;
      if (sim.agents.length >= MAX_POPULATION) return;

      const childConnections = breedConnections(a.brain.connections, b.brain.connections);
      const mid = a.position.clone().add(b.position).multiplyScalar(0.5);
      mid.x += (Math.random() - 0.5) * 2;
      mid.z += (Math.random() - 0.5) * 2;
      const generation = Math.max(a.generation, b.generation) + 1;
      a.payBreedCost();
      b.payBreedCost();
      const child = spawnAgent({ position: mid, generation, connections: childConnections });
      if (child) {
        setStatus(`${a.name} × ${b.name} → ${child.name} 출생 (세대 ${generation}, 커넥톰 변이 적용)`);
      }
      return;
    }
  }
}

// --- 선택 / 입력 ---
let selectedAgent = null;
const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();
const groundPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
let pointerDownAt = null;

function selectAgent(agent) {
  if (selectedAgent) selectedAgent.setSelected(false);
  selectedAgent = agent;
  if (agent) agent.setSelected(true);
  document.querySelector("#worm-panel").hidden = !agent;
}

renderer.domElement.addEventListener("pointerdown", (event) => {
  pointerDownAt = { x: event.clientX, y: event.clientY };
});

renderer.domElement.addEventListener("pointerup", (event) => {
  // 드래그(카메라 조작)와 클릭 구분
  if (!pointerDownAt) return;
  const moved = Math.hypot(event.clientX - pointerDownAt.x, event.clientY - pointerDownAt.y);
  pointerDownAt = null;
  if (moved > 6) return;

  pointer.set(
    (event.clientX / window.innerWidth) * 2 - 1,
    -(event.clientY / window.innerHeight) * 2 + 1,
  );
  raycaster.setFromCamera(pointer, camera);

  // 1) 개체 클릭 → 선택
  const agentMeshes = [];
  for (const agent of sim.agents) {
    if (agent.group) agentMeshes.push(agent.group);
  }
  const hits = raycaster.intersectObjects(agentMeshes, true);
  const hitAgent = hits.find((h) => h.object.userData.agent)?.object.userData.agent;
  if (hitAgent && hitAgent.alive) {
    selectAgent(hitAgent);
    setStatus(`${hitAgent.name} 선택됨 — 뉴런 활동을 표시합니다.`);
    return;
  }

  // 2) 바닥 클릭 → 먹이 배치
  const point = new THREE.Vector3();
  if (raycaster.ray.intersectPlane(groundPlane, point)) {
    if (point.length() < WORLD_RADIUS - 1.5) {
      spawnFood(point);
      setStatus("먹이를 놓았습니다. 배고픈 개체가 화학감각으로 찾아갑니다.");
    }
  }
});

// --- UI 버튼 ---
let paused = false;
let speedMultiplier = 1;

document.querySelector("#btn-food").addEventListener("click", () => {
  for (let i = 0; i < 5; i++) spawnFood();
  setStatus("먹이 5개를 무작위로 뿌렸습니다.");
});

document.querySelector("#btn-worm").addEventListener("click", () => {
  const agent = spawnAgent();
  if (agent) setStatus(`${agent.name} 추가됨 (원본 커넥톰).`);
});

document.querySelector("#btn-pause").addEventListener("click", (event) => {
  paused = !paused;
  event.target.textContent = paused ? "재개" : "일시정지";
});

document.querySelector("#btn-speed").addEventListener("click", (event) => {
  speedMultiplier = speedMultiplier >= 4 ? 1 : speedMultiplier * 2;
  event.target.textContent = `배속 x${speedMultiplier}`;
});

// --- 통계 / 뉴런 활동 패널 ---
const statPop = document.querySelector("#stat-pop");
const statFood = document.querySelector("#stat-food");
const statGen = document.querySelector("#stat-gen");
const statDead = document.querySelector("#stat-dead");
const wormName = document.querySelector("#worm-name");
const wormGen = document.querySelector("#worm-gen");
const wormAge = document.querySelector("#worm-age");
const wormState = document.querySelector("#worm-state");
const wormEnergy = document.querySelector("#worm-energy");
const firedCountLabel = document.querySelector("#fired-count");
const neuronCanvas = document.querySelector("#neuron-canvas");
const neuronCtx = neuronCanvas.getContext("2d");

const NEURON_COLS = 20;

function drawNeuronActivity(agent) {
  const brain = agent.brain;
  const n = brain.neuronCount;
  const rows = Math.ceil(n / NEURON_COLS);
  const cw = neuronCanvas.width / NEURON_COLS;
  const ch = neuronCanvas.height / rows;

  neuronCtx.fillStyle = "#070b16";
  neuronCtx.fillRect(0, 0, neuronCanvas.width, neuronCanvas.height);

  for (let i = 0; i < n; i++) {
    const x = (i % NEURON_COLS) * cw;
    const y = Math.floor(i / NEURON_COLS) * ch;
    if (brain.fired[i]) {
      neuronCtx.fillStyle = "#4ade80";
    } else {
      // 누적 전위에 비례한 잔광
      const v = Math.min(1, Math.abs(brain.acc[i]) / brain.threshold);
      const g = Math.floor(30 + v * 90);
      neuronCtx.fillStyle = `rgb(${Math.floor(g * 0.35)}, ${g}, ${Math.floor(g * 0.55)})`;
    }
    neuronCtx.fillRect(x + 0.5, y + 0.5, cw - 1, ch - 1);
    if (brain.stimulated[i]) {
      neuronCtx.strokeStyle = "#f87171";
      neuronCtx.strokeRect(x + 0.5, y + 0.5, cw - 1, ch - 1);
    }
  }
}

function updateUi() {
  statPop.textContent = String(sim.agents.filter((a) => a.alive).length);
  statFood.textContent = String(sim.foods.filter((f) => !f.depleted).length);
  statGen.textContent = String(sim.maxGeneration);
  statDead.textContent = String(sim.deadCount);

  if (selectedAgent) {
    wormName.textContent = selectedAgent.name;
    wormGen.textContent = `세대 ${selectedAgent.generation}`;
    wormAge.textContent = `${selectedAgent.age.toFixed(0)}초`;
    wormState.textContent = selectedAgent.stateLabel;
    wormEnergy.style.width = `${Math.max(0, selectedAgent.energy).toFixed(0)}%`;
    firedCountLabel.textContent = `${selectedAgent.lastFiredCount} / ${selectedAgent.brain.neuronCount}`;
    drawNeuronActivity(selectedAgent);
  }
}

// --- 메인 루프 ---
const clock = new THREE.Clock();

function animate() {
  requestAnimationFrame(animate);
  const rawDt = Math.min(clock.getDelta(), 0.05);

  if (!paused) {
    const dt = rawDt * speedMultiplier;
    for (const agent of [...sim.agents]) agent.update(dt);
    checkBreeding(dt);

    // 소진된 먹이 제거
    for (let i = sim.foods.length - 1; i >= 0; i--) {
      if (sim.foods[i].depleted) {
        sim.foods[i].dispose(scene);
        sim.foods.splice(i, 1);
      }
    }
  }

  controls.update();
  updateUi();
  renderer.render(scene, camera);
}

// --- 부팅 ---
async function boot() {
  try {
    setStatus("커넥톰 데이터 로딩 중...");
    const data = await loadConnectomeData();
    await loadCharacter();

    for (let i = 0; i < INITIAL_AGENTS; i++) spawnAgent();
    for (let i = 0; i < INITIAL_FOOD; i++) spawnFood();
    if (sim.agents.length > 0) selectAgent(sim.agents[0]);

    setStatus(
      `준비 완료 — 뉴런 ${data.meta.neuronCount}개, 시냅스 ${data.meta.synapseEntries}개 커넥톰 가동 중.`,
    );
  } catch (error) {
    console.error(error);
    setStatus(`로딩 실패: ${error.message}`);
  }
}

boot();
animate();

// 자동화 테스트/디버깅용 훅
window.__sim = { sim, camera, controls, selectAgent };
