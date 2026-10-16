// 커넥톰 뇌를 가진 꼬마싸이클롭충 에이전트.
//
// 감각 입력 (전부 실제 C. elegans 감각 뉴런에 매핑):
//   - 코 촉각(FLP·ASH·IL1V·OLQ): 전방 장애물/경계
//   - 화학감각(ADF·ASG·ASI·ASJ): 먹이 농도 (허기에 비례)
//   - 페로몬 유인(ASK): 다른 개체가 내는 아스카로사이드 감지 → 군집·짝 찾기
//   - 페로몬 기피(ADL): 과밀 시 회피
//   - 몸통 촉각(ALM·AVM·PLM): 개체 간 접촉 반응
//
// 생활사 (실제 생물학 기반):
//   - 섭식으로 성장: 유충(작음) → 성체 (크기·나이 조건)
//   - 자웅동체(⚥): 자가수정으로 단독 산란 (실제 꼬마선충의 주 번식 방식)
//   - 수컷(♂, 소수): 페로몬을 따라 자웅동체를 찾아가 교배 → 커넥톰 교차
import * as THREE from "three";
import { Brain } from "./connectome.js";
import { WORLD_RADIUS } from "./world.js";

const BRAIN_HZ = 26; // 초당 커넥톰 스텝 수
const NOSE_RANGE = 2.6;
const FOOD_SENSE_RANGE = 22;
const PHEROMONE_RANGE = 20; // 페로몬 감지 범위
const CONTACT_RANGE = 1.7; // 개체 간 접촉 판정 거리
const EAT_RANGE = 2.2;
const MAX_SPEED = 3.2; // m/s
const MAX_TURN = 2.4; // rad/s

const ENERGY_MAX = 100;
const ENERGY_DECAY = 0.55;
const ENERGY_MOVE_COST = 0.35;
const EAT_RATE = 14;

// 성장 (유충 → 성체)
const NEWBORN_SIZE = 0.15;
const ADULT_SIZE = 0.8;
const GROWTH_PER_FOOD = 0.006; // 섭취량 1당 성장량
const MATURE_AGE = 15; // 초

// 번식
const SELF_FERTILIZE_ENERGY = 78; // 자가수정(자웅동체) 최소 에너지
const SELF_FERTILIZE_COST = 30;
const SELF_FERTILIZE_COOLDOWN = 45; // 초
const MATE_ENERGY = 62; // 교배 최소 에너지
const MATE_COST = 24;
const MATE_COOLDOWN = 28; // 초
const MATE_RANGE = 2.6;
export const MALE_RATIO = 0.2; // 자손이 수컷일 확률 (실제는 ~0.1%지만 관찰 가능하게 상향)

// 페로몬 농도 기준
const CROWD_CONCENTRATION = 1.35; // 이 이상이면 과밀 → 기피

let agentSerial = 0;

export class WormAgent {
  constructor(sim, { position, generation = 0, connections = null, sex = null, size = null }) {
    this.sim = sim;
    this.id = ++agentSerial;
    this.sex = sex ?? (Math.random() < MALE_RATIO ? "male" : "herm");
    this.name = `꼬마싸이클롭충-${String(this.id).padStart(2, "0")} ${this.sex === "male" ? "♂" : "⚥"}`;
    this.generation = generation;
    this.brain = new Brain(connections);

    this.position = position.clone();
    this.heading = Math.random() * Math.PI * 2;
    this.energy = ENERGY_MAX * 0.7;
    this.age = 0;
    this.size = size ?? 0.85 + Math.random() * 0.15; // 기본 생성 개체는 성체
    this.alive = true;
    this.breedTimer = MATE_COOLDOWN * 0.5;
    this.stateLabel = "탐색";

    this.brainAccumulator = 0;
    this.speedSignal = 0; // 부호 있음 (음수 = 후진)
    this.turnSignal = 0;
    this.avoidTurn = 0;
    this.smoothSpeed = 0;
    this.smoothTurn = 0;
    this.lastFiredCount = 0;
    this.inContact = false; // 접촉 자극 엣지 트리거용

    this.group = null;
    this.mixer = null;
    this.walkAction = null;
    this.jumpAction = null;
    this.selectionRing = null;
    this.deathTimer = 0;
  }

  attachVisual(templateFactory, scene) {
    const { group, mixer, walkAction, jumpAction } = templateFactory(this.sex);
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
      new THREE.MeshBasicMaterial({ color: 0xffe08a, transparent: true, opacity: 0.95, side: THREE.DoubleSide }),
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.03;
    ring.visible = false;
    this.selectionRing = ring;
    this.group.add(ring);

    scene.add(this.group);
    this.updateVisual(0);
  }

  setSelected(selected) {
    if (this.selectionRing) this.selectionRing.visible = selected;
  }

  get hunger() {
    return 1 - this.energy / ENERGY_MAX;
  }

  get isAdult() {
    return this.size >= ADULT_SIZE && this.age > MATURE_AGE;
  }

  // 내가 발산하는 페로몬 양 (성체일수록 강함)
  get pheromoneEmission() {
    return 0.3 + 0.7 * this.size;
  }

  get canMate() {
    return this.alive && this.isAdult && this.energy > MATE_ENERGY && this.breedTimer <= 0;
  }

  update(dt) {
    if (!this.alive) {
      this.updateDeath(dt);
      return;
    }

    this.age += dt;
    this.breedTimer = Math.max(0, this.breedTimer - dt);
    this.selfTimer = Math.max(0, (this.selfTimer ?? SELF_FERTILIZE_COOLDOWN * 0.6) - dt);

    this.brainAccumulator += dt;
    const stepInterval = 1 / BRAIN_HZ;
    while (this.brainAccumulator >= stepInterval) {
      this.brainAccumulator -= stepInterval;
      this.brainStep();
    }

    this.applyMotion(dt);
    this.updateEnergy(dt);
    this.trySelfFertilize();
    this.updateVisual(dt);
  }

  // 주변 개체 스캔: 페로몬 총 농도, 최근접 개체, 짝 후보
  senseNeighbors() {
    let concentration = 0;
    let nearest = null;
    let nearestDist = Infinity;
    let mate = null;
    let mateDist = Infinity;
    for (const other of this.sim.agents) {
      if (other === this || !other.alive) continue;
      const d = this.position.distanceTo(other.position);
      if (d < PHEROMONE_RANGE) {
        concentration += other.pheromoneEmission / (1 + d * d * 0.12);
      }
      if (d < nearestDist) {
        nearestDist = d;
        nearest = other;
      }
      // 수컷의 짝 후보: 교배 가능한 자웅동체
      if (this.sex === "male" && other.sex === "herm" && other.canMate && d < mateDist) {
        mateDist = d;
        mate = other;
      }
    }
    return { concentration, nearest, nearestDist, mate, mateDist };
  }

  brainStep() {
    const brain = this.brain;
    brain.clearStimulationFlags();

    // --- 코 촉각: 전방 장애물 ---
    const obstacle = this.senseObstacle();
    const noseTouch = obstacle.ahead || obstacle.left || obstacle.right;
    if (noseTouch) {
      brain.stimulateGroup("noseTouch", 1);
      this.stateLabel = "회피";
      if (obstacle.left && !obstacle.right) this.avoidTurn = -1;
      else if (obstacle.right && !obstacle.left) this.avoidTurn = 1;
      else if (this.avoidTurn === 0) this.avoidTurn = Math.random() < 0.5 ? -1 : 1;
    } else {
      this.avoidTurn = 0;
    }

    // --- 개체 간 감각: 페로몬 + 몸통 접촉 ---
    const social = this.senseNeighbors();
    this.socialInfo = social;

    // 몸통 접촉 (새로 닿는 순간에만 자극 — 습관화 근사)
    const touching = social.nearest && social.nearestDist < CONTACT_RANGE * (this.size + social.nearest.size) * 0.55 + 0.6;
    if (touching && !this.inContact) {
      brain.stimulateGroup("bodyTouch", 0.8);
    }
    this.inContact = Boolean(touching);

    const crowded = social.concentration > CROWD_CONCENTRATION;
    if (social.concentration > 0.03) {
      if (crowded) {
        brain.stimulateGroup("pheromoneAvoid", Math.min(1.2, social.concentration - CROWD_CONCENTRATION + 0.4));
      } else {
        brain.stimulateGroup("pheromoneAttract", Math.min(1, social.concentration));
      }
    }

    // --- 먹이 화학감각 ---
    const food = this.sim.findNearestFood(this.position, FOOD_SENSE_RANGE);
    this.sensedFood = food;
    if (food && !noseTouch) {
      const d = this.position.distanceTo(food.position);
      const foodConcentration = 1 - d / FOOD_SENSE_RANGE;
      const drive = (0.35 + 0.65 * this.hunger) * (0.4 + 0.6 * foodConcentration);
      const bearing = this.bearingTo(food.position);
      const bias = THREE.MathUtils.clamp(Math.sin(bearing), -1, 1) * 0.5;
      brain.stimulateGroup("foodLeft", drive * (1 + bias));
      brain.stimulateGroup("foodRight", drive * (1 - bias));
    } else if (!noseTouch) {
      // 약한 상시 탐색 자극 (자발적 배회)
      brain.stimulateGroup("foodLeft", 0.35);
      brain.stimulateGroup("foodRight", 0.35);
    }

    // --- 커넥톰 스텝 및 운동 출력 ---
    const { left, right, firedCount } = brain.step();
    this.lastFiredCount = firedCount;

    const total = Math.abs(left) + Math.abs(right);
    if (total > 0) {
      const direction = left + right >= 0 ? 1 : -1;
      this.speedSignal = direction * Math.min(1, total / 75);
      const emergentTurn = THREE.MathUtils.clamp((right - left) / (total + 20), -1, 1);
      const { reflexTurn, reflexWeight } = this.chooseReflex(noseTouch, social);
      this.turnSignal = THREE.MathUtils.clamp(
        emergentTurn * 0.6 + reflexTurn * reflexWeight + this.avoidTurn * 1.1,
        -1,
        1,
      );
    } else {
      this.speedSignal *= 0.92;
      this.turnSignal *= 0.9;
    }
  }

  // 조향 반사(클리노택시스 근사) 대상 선택 — 우선순위:
  // 장애물 회피 > 배고픔·먹이 > 수컷 짝 찾기 > 과밀 이탈 > 약한 군집
  chooseReflex(noseTouch, social) {
    if (noseTouch) {
      this.stateLabel = "회피";
      return { reflexTurn: 0, reflexWeight: 0 };
    }

    if (this.sensedFood && this.hunger > 0.25) {
      const d = this.position.distanceTo(this.sensedFood.position);
      this.stateLabel = this.hunger > 0.55 ? "먹이 추적" : "탐색";
      return {
        reflexTurn: THREE.MathUtils.clamp(this.bearingTo(this.sensedFood.position) / Math.PI, -1, 1),
        reflexWeight: 0.5 + 0.5 * this.hunger + 0.4 * (1 - Math.min(1, d / 6)),
      };
    }

    if (this.sex === "male" && this.canMate && social.mate) {
      this.stateLabel = "짝 찾기";
      return {
        reflexTurn: THREE.MathUtils.clamp(this.bearingTo(social.mate.position) / Math.PI, -1, 1),
        reflexWeight: 0.75,
      };
    }

    if (social.concentration > CROWD_CONCENTRATION && social.nearest) {
      this.stateLabel = "과밀 회피";
      return {
        reflexTurn: THREE.MathUtils.clamp(-this.bearingTo(social.nearest.position) / Math.PI, -1, 1),
        reflexWeight: 0.5,
      };
    }

    if (social.nearest && social.nearestDist < PHEROMONE_RANGE && this.hunger < 0.5 && social.nearestDist > 3) {
      this.stateLabel = "군집";
      return {
        reflexTurn: THREE.MathUtils.clamp(this.bearingTo(social.nearest.position) / Math.PI, -1, 1),
        reflexWeight: 0.18, // 약한 사회적 유인 (군집 섭식 근사)
      };
    }

    this.stateLabel = "탐색";
    return { reflexTurn: 0, reflexWeight: 0 };
  }

  senseObstacle() {
    const probe = (angleOffset) => {
      const px = this.position.x + Math.sin(this.heading + angleOffset) * NOSE_RANGE;
      const pz = this.position.z + Math.cos(this.heading + angleOffset) * NOSE_RANGE;
      if (Math.hypot(px, pz) > WORLD_RADIUS - 0.8) return true;
      for (const obstacle of this.sim.obstacles) {
        const d = Math.hypot(px - obstacle.position.x, pz - obstacle.position.z);
        if (d < obstacle.radius + 0.7) return true;
      }
      return false;
    };
    return { ahead: probe(0), left: probe(0.62), right: probe(-0.62) };
  }

  // 표적까지의 상대 방위각 (heading 증가 방향이 양수)
  bearingTo(target) {
    const angle = Math.atan2(target.x - this.position.x, target.z - this.position.z);
    let diff = angle - this.heading;
    while (diff > Math.PI) diff -= Math.PI * 2;
    while (diff < -Math.PI) diff += Math.PI * 2;
    return diff;
  }

  applyMotion(dt) {
    this.smoothSpeed = THREE.MathUtils.lerp(this.smoothSpeed, this.speedSignal, 1 - Math.exp(-4 * dt));
    this.smoothTurn = THREE.MathUtils.lerp(this.smoothTurn, this.turnSignal, 1 - Math.exp(-5 * dt));

    this.heading += this.smoothTurn * MAX_TURN * dt;

    // 유충은 몸이 작아 절대 속도도 느리다
    const velocity = this.smoothSpeed * MAX_SPEED * (0.55 + 0.45 * this.size);
    this.position.x += Math.sin(this.heading) * velocity * dt;
    this.position.z += Math.cos(this.heading) * velocity * dt;

    const r = Math.hypot(this.position.x, this.position.z);
    const maxR = WORLD_RADIUS - 1.2;
    if (r > maxR) {
      this.position.x *= maxR / r;
      this.position.z *= maxR / r;
    }
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
    // 개체 간 겹침 방지 (부드럽게 밀어내기)
    for (const other of this.sim.agents) {
      if (other === this || !other.alive) continue;
      const dx = this.position.x - other.position.x;
      const dz = this.position.z - other.position.z;
      const dist = Math.hypot(dx, dz);
      const minDist = (this.size + other.size) * 0.55 + 0.25;
      if (dist < minDist && dist > 0.001) {
        const push = (minDist - dist) * 0.5;
        this.position.x += (dx / dist) * push;
        this.position.z += (dz / dist) * push;
      }
    }
  }

  updateEnergy(dt) {
    this.energy -= (ENERGY_DECAY + ENERGY_MOVE_COST * Math.abs(this.smoothSpeed)) * dt;

    const food = this.sensedFood;
    if (food && !food.depleted && this.position.distanceTo(food.position) < EAT_RANGE) {
      const eaten = food.consume(EAT_RATE * dt);
      this.energy = Math.min(ENERGY_MAX, this.energy + eaten);
      // 섭식 성장: 유충 → 성체
      this.size = Math.min(1, this.size + eaten * GROWTH_PER_FOOD);
      this.stateLabel = "섭식";
    }

    if (this.energy <= 0) {
      this.alive = false;
      this.deathTimer = 2.2;
      this.stateLabel = "사망";
      this.sim.onAgentDeath(this);
    }
  }

  // 자웅동체 자가수정 (실제 꼬마선충의 주 번식 방식)
  trySelfFertilize() {
    if (this.sex !== "herm" || !this.isAdult) return;
    if (this.energy < SELF_FERTILIZE_ENERGY || this.selfTimer > 0) return;
    const child = this.sim.spawnChild?.({
      parents: [this],
      position: this.position,
      generation: this.generation + 1,
    });
    if (child) {
      this.energy -= SELF_FERTILIZE_COST;
      this.selfTimer = SELF_FERTILIZE_COOLDOWN;
      this.playJump();
    }
  }

  payMateCost() {
    this.energy -= MATE_COST;
    this.breedTimer = MATE_COOLDOWN;
    this.playJump();
  }

  playJump() {
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
    this.group.rotation.z = this.smoothTurn * 0.12;
    // 성장 반영 (유충은 작게)
    const visualScale = 0.4 + 0.6 * this.size;
    this.group.scale.setScalar(visualScale);

    if (this.mixer) {
      const walkRate = Math.abs(this.smoothSpeed);
      if (this.walkAction) {
        this.walkAction.setEffectiveWeight(1);
        this.walkAction.timeScale =
          walkRate < 0.03 ? 0 : Math.sign(this.smoothSpeed) * (0.3 + walkRate * 1.5);
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

export { MATE_RANGE, NEWBORN_SIZE };
