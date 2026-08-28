// 커넥톰 뇌를 가진 사이클롭스 에이전트.
// 감각 입력: 코 촉각(테두리/장애물), 먹이 화학감각(허기에 비례)
// 운동 출력: 좌/우 근육 누적값 → 전후진 속도와 회전
import * as THREE from "three";
import { Brain } from "./connectome.js";
import { DISH_RADIUS } from "./world.js";

const BRAIN_HZ = 26; // 초당 커넥톰 스텝 수
const NOSE_RANGE = 2.6; // 코 촉각 감지 거리
const FOOD_SENSE_RANGE = 22; // 화학감각 최대 거리
const EAT_RANGE = 1.6;
const MAX_SPEED = 3.2; // m/s
const MAX_TURN = 2.4; // rad/s
const ENERGY_MAX = 100;
const ENERGY_DECAY = 0.55; // 초당 기본 소모
const ENERGY_MOVE_COST = 0.35; // 최고 속도 기준 초당 추가 소모
const EAT_RATE = 14; // 초당 섭취량
const MATURE_AGE = 15; // 번식 가능 나이(초)
const BREED_ENERGY = 68; // 번식 최소 에너지
const BREED_COST = 26;
const BREED_COOLDOWN = 25; // 초
const BREED_RANGE = 2.6;

let agentSerial = 0;

export class WormAgent {
  constructor(sim, { position, generation = 0, connections = null }) {
    this.sim = sim;
    this.id = ++agentSerial;
    this.name = `Cyclopse-${String(this.id).padStart(2, "0")}`;
    this.generation = generation;
    this.brain = new Brain(connections);

    this.position = position.clone();
    this.heading = Math.random() * Math.PI * 2;
    this.energy = ENERGY_MAX * 0.7;
    this.age = 0;
    this.alive = true;
    this.breedTimer = BREED_COOLDOWN * 0.5;
    this.stateLabel = "탐색";

    this.brainAccumulator = 0;
    this.speedSignal = 0; // 부호 있음 (음수 = 후진)
    this.turnSignal = 0;
    this.smoothSpeed = 0;
    this.smoothTurn = 0;
    this.lastFiredCount = 0;

    this.group = null; // 3D 오브젝트 (attachVisual에서 설정)
    this.mixer = null;
    this.walkAction = null;
    this.jumpAction = null;
    this.selectionRing = null;
    this.deathTimer = 0;
  }

  attachVisual(templateFactory, scene) {
    const { group, mixer, walkAction, jumpAction } = templateFactory();
    this.group = group;
    this.mixer = mixer;
    this.walkAction = walkAction;
    this.jumpAction = jumpAction;
    this.group.position.copy(this.position);
    this.group.rotation.y = this.heading;
    this.group.traverse((child) => {
      if (child.isMesh || child.isSkinnedMesh) child.userData.agent = this;
    });

    const ring = new THREE.Mesh(
      new THREE.RingGeometry(1.0, 1.25, 40),
      new THREE.MeshBasicMaterial({ color: 0x8fd3a6, transparent: true, opacity: 0.9, side: THREE.DoubleSide }),
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.03;
    ring.visible = false;
    this.selectionRing = ring;
    this.group.add(ring);

    scene.add(this.group);
  }

  setSelected(selected) {
    if (this.selectionRing) this.selectionRing.visible = selected;
  }

  get hunger() {
    return 1 - this.energy / ENERGY_MAX; // 0(포만) ~ 1(굶주림)
  }

  update(dt) {
    if (!this.alive) {
      this.updateDeath(dt);
      return;
    }

    this.age += dt;
    this.breedTimer = Math.max(0, this.breedTimer - dt);

    // 고정 주기로 커넥톰 스텝 실행
    this.brainAccumulator += dt;
    const stepInterval = 1 / BRAIN_HZ;
    while (this.brainAccumulator >= stepInterval) {
      this.brainAccumulator -= stepInterval;
      this.brainStep();
    }

    this.applyMotion(dt);
    this.updateEnergy(dt);
    this.updateVisual(dt);
  }

  brainStep() {
    const brain = this.brain;
    brain.clearStimulationFlags();

    // --- 감각 입력 ---
    const noseTouch = this.senseObstacleAhead();
    if (noseTouch) {
      brain.stimulateGroup("noseTouch", 1);
      this.stateLabel = "회피";
    }

    const food = this.sim.findNearestFood(this.position, FOOD_SENSE_RANGE);
    this.sensedFood = food;
    if (food && !noseTouch) {
      // 농도(거리 반비례) x 허기 → 자극 강도. 좌/우 감각 클래스에 방향 편향을 준다.
      const d = this.position.distanceTo(food.position);
      const concentration = 1 - d / FOOD_SENSE_RANGE;
      const drive = (0.35 + 0.65 * this.hunger) * (0.4 + 0.6 * concentration);
      const bearing = this.bearingTo(food.position); // -PI..PI (양수 = 왼쪽)
      const bias = THREE.MathUtils.clamp(Math.sin(bearing), -1, 1) * 0.5;
      brain.stimulateGroup("foodLeft", drive * (1 + bias));
      brain.stimulateGroup("foodRight", drive * (1 - bias));
      this.stateLabel = this.hunger > 0.55 ? "먹이 추적" : "탐색";
    } else if (!noseTouch) {
      this.stateLabel = "탐색";
    }

    // --- 커넥톰 스텝 및 운동 출력 ---
    const { left, right, firedCount } = brain.step();
    this.lastFiredCount = firedCount;

    const total = Math.abs(left) + Math.abs(right);
    if (total > 0) {
      const direction = left + right >= 0 ? 1 : -1;
      this.speedSignal = direction * Math.min(1, total / 120);
      // 창발적 회전 신호: 좌우 근육 비대칭
      const emergentTurn = THREE.MathUtils.clamp((right - left) / (total + 20), -1, 1);
      // 클리노택시스 반사: 먹이 방향으로의 약한 직접 조향 (근사 보정)
      let reflexTurn = 0;
      if (this.sensedFood && !noseTouch) {
        reflexTurn = THREE.MathUtils.clamp(-this.bearingTo(this.sensedFood.position) / Math.PI, -1, 1);
      }
      this.turnSignal = THREE.MathUtils.clamp(emergentTurn * 0.75 + reflexTurn * 0.55, -1, 1);
    } else {
      this.speedSignal *= 0.92;
      this.turnSignal *= 0.9;
    }
  }

  // 진행 방향 전방에 테두리/장애물이 있는지 검사
  senseObstacleAhead() {
    const ahead = new THREE.Vector3(
      this.position.x + Math.sin(this.heading) * NOSE_RANGE,
      0,
      this.position.z + Math.cos(this.heading) * NOSE_RANGE,
    );
    if (ahead.length() > DISH_RADIUS - 0.8) return true;
    for (const obstacle of this.sim.obstacles) {
      if (ahead.distanceTo(obstacle.position) < obstacle.radius + 0.7) return true;
    }
    return false;
  }

  // 표적까지의 상대 방위각. 양수 = 왼쪽, 음수 = 오른쪽
  bearingTo(target) {
    const angle = Math.atan2(target.x - this.position.x, target.z - this.position.z);
    let diff = angle - this.heading;
    while (diff > Math.PI) diff -= Math.PI * 2;
    while (diff < -Math.PI) diff += Math.PI * 2;
    return -diff;
  }

  applyMotion(dt) {
    // 신호 스무딩 (근육의 관성 근사)
    this.smoothSpeed = THREE.MathUtils.lerp(this.smoothSpeed, this.speedSignal, 1 - Math.exp(-4 * dt));
    this.smoothTurn = THREE.MathUtils.lerp(this.smoothTurn, this.turnSignal, 1 - Math.exp(-5 * dt));

    this.heading -= this.smoothTurn * MAX_TURN * dt;

    const velocity = this.smoothSpeed * MAX_SPEED;
    this.position.x += Math.sin(this.heading) * velocity * dt;
    this.position.z += Math.cos(this.heading) * velocity * dt;

    // 접시 밖으로 나가지 않게 클램프
    const r = Math.hypot(this.position.x, this.position.z);
    const maxR = DISH_RADIUS - 1.2;
    if (r > maxR) {
      this.position.x *= maxR / r;
      this.position.z *= maxR / r;
    }
    // 장애물 밀어내기
    for (const obstacle of this.sim.obstacles) {
      const dx = this.position.x - obstacle.position.x;
      const dz = this.position.z - obstacle.position.z;
      const dist = Math.hypot(dx, dz);
      const minDist = obstacle.radius + 0.8;
      if (dist < minDist && dist > 0.001) {
        this.position.x = obstacle.position.x + (dx / dist) * minDist;
        this.position.z = obstacle.position.z + (dz / dist) * minDist;
      }
    }
  }

  updateEnergy(dt) {
    this.energy -= (ENERGY_DECAY + ENERGY_MOVE_COST * Math.abs(this.smoothSpeed)) * dt;

    // 섭식: 가까운 먹이에서 에너지 흡수
    const food = this.sensedFood;
    if (food && !food.depleted && this.position.distanceTo(food.position) < EAT_RANGE) {
      const eaten = food.consume(EAT_RATE * dt);
      this.energy = Math.min(ENERGY_MAX, this.energy + eaten);
      this.stateLabel = "섭식";
    }

    if (this.energy <= 0) {
      this.alive = false;
      this.deathTimer = 2.2;
      this.stateLabel = "사망";
      this.sim.onAgentDeath(this);
    }
  }

  get canBreed() {
    return (
      this.alive &&
      this.age > MATURE_AGE &&
      this.energy > BREED_ENERGY &&
      this.breedTimer <= 0
    );
  }

  payBreedCost() {
    this.energy -= BREED_COST;
    this.breedTimer = BREED_COOLDOWN;
    if (this.jumpAction) {
      this.jumpAction.reset();
      this.jumpAction.setLoop(THREE.LoopOnce, 1);
      this.jumpAction.play();
    }
  }

  updateVisual(dt) {
    if (!this.group) return;
    this.group.position.copy(this.position);
    this.group.rotation.y = this.heading;
    // 회전 방향으로 살짝 기울이기
    this.group.rotation.z = this.smoothTurn * 0.12;

    if (this.mixer) {
      const walkRate = Math.abs(this.smoothSpeed);
      if (this.walkAction) {
        this.walkAction.timeScale = Math.sign(this.smoothSpeed || 1) * (0.4 + walkRate * 1.4);
        this.walkAction.setEffectiveWeight(Math.min(1, walkRate * 3));
      }
      this.mixer.update(dt);
    }
  }

  updateDeath(dt) {
    this.deathTimer -= dt;
    if (this.group) {
      this.group.position.y -= dt * 0.45;
      this.group.rotation.z += dt * 0.8;
    }
    if (this.deathTimer <= 0) this.sim.removeAgent(this);
  }

  dispose(scene) {
    if (this.group) scene.remove(this.group);
  }
}
