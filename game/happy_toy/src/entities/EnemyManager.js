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

    for (const enemy of this.enemies) {
      enemy.update(deltaTime, playerState);
      if (enemy.lastDetectionEvent) {
        detectionEvents.push(enemy.lastDetectionEvent);
      }
      threat = Math.max(threat, enemy.getThreatAmount(playerPosition));
      caught ||= enemy.caughtPlayer;
    }

    this.hud.setThreat(threat);
    return { caught, threat, detectionEvents };
  }

  setTestSafeMode(enabled) {
    if (!enabled) {
      return;
    }

    for (const enemy of this.enemies) {
      enemy.state = "wander";
      enemy.memoryTimer = 0;
      enemy.lastKnownPlayerPosition = null;
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
      enemy.state = "patrol";
      enemy.currentWaypoint = 0;
      enemy.memoryTimer = 0;
      enemy.lastKnownPlayerPosition = null;
      enemy.caughtPlayer = false;
      enemy.cabinetTarget = null;
      enemy.chasePath = [];
      enemy.chasePathTimer = 0;
      enemy.chasePathGoal = null;
      enemy.patrolPath = [];
      enemy.patrolPathTimer = 0;
      enemy.patrolPathGoal = null;
      enemy.waitTimer = 0;
      enemy.waitTurnDirection = 1;
      enemy.stuckTimer = 0;
      enemy.lastUnstuckTarget = null;
      enemy.debugPathTarget = null;
      enemy.lastDetectionEvent = null;
      enemy.wanderTarget = null;
      enemy.wanderRetargetTimer = 0;
      enemy.wanderStuckCount = 0;
      enemy.group.position.y = this.collisionWorld.getGroundY(enemy.group.position);
      enemy.resumeAnimatedPose();
      enemy.playAction("patrol", 0);
      enemy.snapModelToGround(false);
    }
  }
}
