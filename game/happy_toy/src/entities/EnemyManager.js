// 여러 적 캐릭터의 생성과 업데이트를 총괄하는 모듈입니다.
// 새 적을 추가할 때는 config/gameConfig.js의 ENEMY_CONFIGS만 확장하면 됩니다.

import { ENEMY_CONFIGS } from "../config/gameConfig.js";
import { CharacterLoader } from "../loaders/CharacterLoader.js";
import { Enemy } from "./Enemy.js";

export class EnemyManager {
  constructor(scene, collisionWorld, doors, hud, enemyConfigs = ENEMY_CONFIGS) {
    this.scene = scene;
    this.collisionWorld = collisionWorld;
    this.doors = doors;
    this.hud = hud;
    this.enemyConfigs = enemyConfigs;
    this.loader = new CharacterLoader();
    this.enemies = [];
    this.directorProgress = 0;
    this.lastNoiseResponseCount = 0;
  }

  async loadEnemies() {
    this.hud.setStatus("Uncat과 Cyclopse가 맵 어딘가에 배치되는 중입니다.");
    const loadedEnemies = await Promise.all(
      this.enemyConfigs.map(async (config) => {
        return this.createEnemy(config);
      }),
    );

    for (const enemy of loadedEnemies) {
      this.enemies.push(enemy);
      this.scene.add(enemy.group);
    }
    this.hud.setStatus("문 너머에서 발소리가 들립니다.", 1800);
  }

  async createEnemy(config) {
    const asset = await this.loader.load(config);
    return new Enemy(config, asset, this.collisionWorld, this.doors);
  }

  async addEnemy(config, options = {}) {
    const enemy = await this.createEnemy(config);
    if (options.spawn) {
      enemy.group.position.set(...options.spawn);
      this.collisionWorld.snapToValidSurface(enemy.group.position, { actorId: config.id });
    }
    if (Number.isFinite(options.yaw)) {
      enemy.group.rotation.y = options.yaw;
    }
    if (options.state) {
      enemy.state = options.state;
    }
    if (options.lastKnownPlayerPosition) {
      enemy.lastKnownPlayerPosition = options.lastKnownPlayerPosition.clone();
      enemy.memoryTimer = config.memorySeconds;
    }
    enemy.isDynamic = Boolean(options.dynamic);
    enemy.snapModelToGround(false);
    enemy.setDormant(false);
    this.enemies.push(enemy);
    this.scene.add(enemy.group);
    return enemy;
  }


  removeEnemyById(id) {
    const nextEnemies = [];
    for (const enemy of this.enemies) {
      if (enemy.config.id === id) {
        this.scene.remove(enemy.group);
        enemy.dispose();
        continue;
      }
      nextEnemies.push(enemy);
    }
    this.enemies = nextEnemies;
  }

  update(deltaTime, playerState) {
    const playerPosition = playerState.position || playerState;
    let threat = 0;
    let caught = false;
    const detectionEvents = [];
    const game = window.__happyToy;
    const totalKeys = Math.max(1, game?.keys?.length || 4);
    const targetProgress = Math.min(1, Math.max(0, (game?.keyCount || 0) / totalKeys));
    this.directorProgress += (targetProgress - this.directorProgress) * Math.min(1, deltaTime * 0.65);
    const difficulty = game?.menuSystem?.currentMode || "normal";
    const pursuitBudget = Math.min(
      this.enemies.length,
      1 + (this.directorProgress >= 0.45 ? 1 : 0) + (difficulty === "hardcore" ? 1 : 0),
    );
    let activePursuers = 0;

    const updateOrder = [...this.enemies].sort((a, b) => {
      const chasePriority = Number(b.isActivelyChasing()) - Number(a.isActivelyChasing());
      if (chasePriority !== 0) {
        return chasePriority;
      }
      return distanceToPlayer(a, playerPosition) - distanceToPlayer(b, playerPosition);
    });

    for (const enemy of updateOrder) {
      enemy.progressionSpeedMultiplier = 1 + this.directorProgress * (enemy.config.progressionSpeedGain ?? 0.1);
      enemy.progressionDetectionMultiplier = 1 + this.directorProgress * (enemy.config.progressionDetectionGain ?? 0.08);
      const canStartChase = enemy.isActivelyChasing() || activePursuers < pursuitBudget;
      enemy.update(deltaTime, { ...playerState, canStartChase });
      if (enemy.isActivelyChasing()) {
        activePursuers += 1;
      }
      if (enemy.lastDetectionEvent) {
        detectionEvents.push(enemy.lastDetectionEvent);
      }
      threat = Math.max(threat, enemy.getThreatAmount(playerPosition));
      caught ||= enemy.caughtPlayer;
    }

    this.hud.setThreat(threat);
    return {
      caught,
      threat,
      detectionEvents,
      pursuitBudget,
      activePursuers,
      progress: this.directorProgress,
    };
  }

  setTestSafeMode(enabled) {
    if (!enabled) {
      return;
    }

    for (const enemy of this.enemies) {
      enemy.beginWander();
      enemy.caughtPlayer = false;
      enemy.cabinetTarget = null;
      enemy.chasePath = [];
      enemy.chasePathTimer = 0;
      enemy.chasePathGoal = null;
      enemy.lastDetectionEvent = null;
      enemy.playAction("patrol");
    }
  }

  getClosestChasingEnemy(position) {
    let closestEnemy = null;
    let closestDistance = Infinity;

    for (const enemy of this.enemies) {
      if (!enemy.isActivelyChasing()) {
        continue;
      }

      const distance = Math.hypot(enemy.group.position.x - position.x, enemy.group.position.z - position.z);
      if (distance < closestDistance) {
        closestEnemy = enemy;
        closestDistance = distance;
      }
    }

    return closestEnemy;
  }

  endCabinetInvestigations() {
    for (const enemy of this.enemies) {
      if (enemy.state === "investigateCabinet") {
        enemy.endCabinetInvestigation();
      }
    }
  }

  reset(doors = null) {
    if (doors) {
      this.doors = doors;
    }

    for (const enemy of this.enemies.filter((entry) => entry.isDynamic)) {
      this.scene.remove(enemy.group);
      enemy.dispose();
    }
    this.enemies = this.enemies.filter((entry) => !entry.isDynamic);

    for (const enemy of this.enemies) {
      if (doors) {
        enemy.doors = doors;
      }
      enemy.group.position.set(...enemy.config.spawn);
      
      if (enemy.config.type === "baby") {
        enemy.state = "crying";
        enemy.babyAwake = false;
      } else {
        enemy.state = "patrol";
      }

      enemy.currentWaypoint = 0;
      enemy.memoryTimer = 0;
      enemy.lastKnownPlayerPosition = null;
      enemy.caughtPlayer = false;
      enemy.cabinetTarget = null;
      enemy.chasePath = null;
      enemy.chasePathTimer = 0;
      enemy.chasePathGoal = null;
      enemy.patrolPath = null;
      enemy.patrolPathTimer = 0;
      enemy.patrolPathGoal = null;
      enemy.waitTimer = 0;
      enemy.waitTurnDirection = 1;
      enemy.stuckTimer = 0;
      enemy.lastUnstuckTarget = null;
      enemy.debugPathTarget = null;
      enemy.lastDetectionEvent = null;
      enemy.sightExposure = 0;
      enemy.hasVisualContact = false;
      enemy.lostSightTimer = 0;
      enemy.searchTimer = 0;
      enemy.searchTarget = null;
      enemy.investigationTimer = 0;
      enemy.investigationTarget = null;
      enemy.recentWanderTargets = [];
      enemy.progressionSpeedMultiplier = 1;
      enemy.progressionDetectionMultiplier = 1;
      enemy.wanderTarget = null;
      enemy.wanderRetargetTimer = 0;
      enemy.wanderStuckCount = 0;
      enemy.group.position.y = this.collisionWorld.getGroundY(enemy.group.position);
      enemy.resumeAnimatedPose();
      
      if (enemy.config.type === "baby") {
        enemy.playAction("crying", 0);
      } else {
        enemy.playAction("patrol", 0);
      }

      enemy.snapModelToGround(false);
    }
    this.directorProgress = 0;
    this.lastNoiseResponseCount = 0;
  }

  notifyNoiseEvent(position, radius = 28.0, options = {}) {
    let responseCount = 0;
    const soundPosition = position.clone?.() || { ...position };
    const playerPosition = window.__happyToy?.player?.position;
    if (
      playerPosition
      && Math.abs((soundPosition.y ?? 0) - (playerPosition.y ?? 0)) > 1.8
      && Math.hypot(soundPosition.x - playerPosition.x, soundPosition.z - playerPosition.z) <= radius
    ) {
      // FirecrackerProjectiles currently settle on y=0; retain the thrower's floor for AI hearing.
      soundPosition.y = playerPosition.y;
    }
    for (const enemy of this.enemies) {
      if (enemy.notifyNoise(soundPosition, radius, options)) {
        responseCount += 1;
      }
    }
    this.lastNoiseResponseCount = responseCount;
    if (responseCount > 0 && options.silentFeedback !== true) {
      this.hud?.setStatus(
        responseCount === 1
          ? "폭음이 가라앉자, 발소리 하나가 그쪽으로 꺾입니다."
          : `폭음 쪽으로 ${responseCount}개의 발소리가 흩어집니다.`,
        1800,
      );
    }
    return responseCount;
  }
}

function distanceToPlayer(enemy, position) {
  return Math.hypot(
    enemy.group.position.x - position.x,
    enemy.group.position.z - position.z,
  );
}

