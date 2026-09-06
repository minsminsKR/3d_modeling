// 적 하나의 상태 머신을 담당하는 모듈입니다.
// 순찰, 플레이어 발견, 추적, 캐비넷 앞 대기, 포획 거리 판정을 처리합니다.

import * as THREE from "three";
import { direction2D, distance2D, vectorFromArray, yawFromDirection } from "../utils/math.js";

export class Enemy {
  constructor(config, loadedAsset, collisionWorld, doors) {
    this.config = config;
    this.collisionWorld = collisionWorld;
    this.doors = doors;
    this.group = new THREE.Group();
    this.group.name = config.label;
    this.group.position.copy(vectorFromArray(config.spawn));
    this.collisionWorld.snapToValidSurface(this.group.position, { actorId: config.id });
    this.collisionWorld.resolveCircle(this.group.position, config.radius);
    this.modelRoot = loadedAsset.root;
    this.group.add(loadedAsset.root);
    this.shadowMesh = addShadowBlob(this.group, config.radius);

    this.mixer = loadedAsset.animations.length
      ? new THREE.AnimationMixer(loadedAsset.root)
      : null;
    this.actions = this.createActions(loadedAsset.actions || {});
    this.currentActionName = null;
    this.isIdlePose = false;
    
    this.isBaby = config.type === "baby";
    this.babyAwake = false;

    if (this.isBaby) {
      this.state = "crying";
      this.playAction("crying", 0);
    } else {
      this.state = "wander";
      this.playAction("patrol", 0);
    }
    
    this.snapModelToGround(false);

    this.currentWaypoint = 0;
    this.lastKnownPlayerPosition = null;
    this.memoryTimer = 0;
    this.caughtPlayer = false;
    this.cabinetTarget = null;
    this.chasePath = null;
    this.chasePathTimer = 0;
    this.chasePathGoal = null;
    this.patrolPath = null;
    this.patrolPathTimer = 0;
    this.patrolPathGoal = null;
    this.waitTimer = 0;
    this.waitTurnDirection = 1;
    this.stuckTimer = 0;
    this.lastUnstuckTarget = null;
    this.debugPathTarget = null;
    this.lastDetectionEvent = null;
    this.sightExposure = 0;
    this.hasVisualContact = false;
    this.lostSightTimer = 0;
    this.searchTimer = 0;
    this.searchTarget = null;
    this.investigationTimer = 0;
    this.investigationTarget = null;
    this.recentWanderTargets = [];
    this.progressionSpeedMultiplier = 1;
    this.progressionDetectionMultiplier = 1;
    // Waypoint-based long-distance wander state
    this.wanderTarget = null;          // current far waypoint goal
    this.wanderRetargetTimer = 0;      // countdown until we pick a new waypoint
    this.wanderStuckCount = 0;         // how many times we got stuck on this waypoint
    // Dormant state: monsters remain hidden & still until their intro cutscene triggers
    this.isDormant = true;
    this.group.visible = false;
  }

  setDormant(dormant = true) {
    this.isDormant = Boolean(dormant);
    this.group.visible = !this.isDormant;
    if (this.isDormant) {
      this.mixer?.stopAllAction();
      this.currentActionName = null;
    } else {
      if (this.isBaby) {
        this.playAction("crying", 0.2);
      } else if (this.state === "chase") {
        this.playAction("chase", 0.2);
      } else {
        this.playAction("patrol", 0.2);
      }
    }
  }

  update(deltaTime, playerState) {
    if (this.isDormant) {
      if (this.isBaby && !this.babyAwake) {
        const playerPosition = playerState.position || playerState;
        const distance = distance2D(this.group.position, playerPosition);
        const verticalDist = Math.abs((this.group.position.y ?? -5.0) - (playerPosition.y ?? 0));
        const sameFloor = verticalDist <= 2.0;
        const tooClose = sameFloor && distance <= 1.8;
        const sprintNearby = sameFloor && playerState.isSprinting && distance <= 6.0;
        let flashlightAlert = false;
        const game = window.__happyToy;
        const flashlightOn = game?.flashlightController?.enabled;
        if (flashlightOn && distance <= 8.0) {
          const babyPoint = new THREE.Vector3(
            this.group.position.x,
            this.group.position.y + this.config.height * 0.5,
            this.group.position.z
          );
          const eyePos = new THREE.Vector3(
            playerPosition.x,
            (playerPosition.y ?? 0) + 1.7,
            playerPosition.z
          );
          const toBaby = new THREE.Vector3().subVectors(babyPoint, eyePos);
          const distToBaby = toBaby.length();
          if (distToBaby > 0.001) {
            toBaby.normalize();
            const cameraDirection = new THREE.Vector3();
            game.camera.getWorldDirection(cameraDirection);
            const dot = cameraDirection.dot(toBaby);
            const hasLos = this.collisionWorld.hasLineOfSight(eyePos, babyPoint);
            if (dot >= 0.94 && hasLos) {
              flashlightAlert = true;
            }
          }
        }
        if (tooClose || sprintNearby || flashlightAlert) {
          this.setDormant(false);
          this.babyAwake = true;
          this.state = "chase";
          this.memoryTimer = this.config.memorySeconds;
          this.lastKnownPlayerPosition = playerPosition.clone();
          this.playAction("chase");
          if (game?.hud) {
            game.hud.setStatus("아기가 깨어났습니다! 울음소리가 멈췄습니다!", 2200);
          }
          return;
        }
      }
      this.group.visible = false;
      return;
    }

    const playerPosition = playerState.position || playerState;
    if (this.state !== "chase" && this.state !== "flee") {
      const distance = distance2D(this.group.position, playerPosition);
      if (distance > 45) {
        this.group.visible = false;
        this.mixer?.stopAllAction();
        this.currentActionName = null;
        return;
      }
    }
    this.group.visible = true;


    const isPlayerHidden = Boolean(playerState.isHidden || playerState.isUndetectable);
    this.lastDetectionEvent = null;

    if (!this.isIdlePose) {
      this.mixer?.update(deltaTime);
    }
    this.collisionWorld.snapToValidSurface(this.group.position, { actorId: this.config.id });

    // Baby Crying / Awakening check
    if (this.isBaby && !this.babyAwake) {
      const distance = distance2D(this.group.position, playerPosition);
      const verticalDist = Math.abs((this.group.position.y ?? -5.0) - (playerPosition.y ?? 0));
      const sameFloor = verticalDist <= 2.0;
      
      const tooClose = sameFloor && distance <= 1.8;
      const sprintNearby = sameFloor && playerState.isSprinting && distance <= 6.0;
      
      let flashlightAlert = false;
      const game = window.__happyToy;
      const flashlightOn = game?.flashlightController?.enabled;
      
      if (flashlightOn && distance <= 8.0) {
        const babyPoint = new THREE.Vector3(
          this.group.position.x,
          this.group.position.y + this.config.height * 0.5,
          this.group.position.z
        );
        const eyePos = new THREE.Vector3(
          playerPosition.x,
          (playerPosition.y ?? 0) + 1.7,
          playerPosition.z
        );
        const toBaby = new THREE.Vector3().subVectors(babyPoint, eyePos);
        const distToBaby = toBaby.length();
        
        if (distToBaby > 0.001) {
          toBaby.normalize();
          
          const cameraDirection = new THREE.Vector3();
          game.camera.getWorldDirection(cameraDirection);
          
          const dot = cameraDirection.dot(toBaby);
          const hasLos = this.collisionWorld.hasLineOfSight(eyePos, babyPoint);
          
          if (dot >= 0.94 && hasLos) {
            flashlightAlert = true;
          }
        }
      }
      
      if (tooClose || sprintNearby || flashlightAlert) {
        this.babyAwake = true;
        this.state = "chase";
        this.memoryTimer = this.config.memorySeconds;
        this.lastKnownPlayerPosition = playerPosition.clone();
        this.playAction("chase");
        
        if (game?.hud) {
          game.hud.setStatus("아기가 깨어났습니다! 울음소리가 멈췄습니다!", 2200);
        }
      } else {
        // Remain in crying state
        this.playAction("crying");
        this.snapModelToGround(false);
        this.caughtPlayer = false;
        return;
      }
    }

    if (this.state === "cutscene") {
      this.caughtPlayer = false;
      this.snapModelToGround(false);
      return;
    }

    if (this.state === "investigateCabinet") {
      this.updateCabinetInvestigation(deltaTime);
      this.caughtPlayer = false;
      return;
    }

    this.updatePerception(playerPosition, deltaTime, playerState);
    const target = this.getTarget(playerPosition, deltaTime);
    if (target || this.state === "chase" || this.state === "flee") {
      this.playAction(this.state === "chase" || this.state === "flee" ? "chase" : "patrol");
    } else {
      this.playIdlePose();
    }
    this.snapModelToGround(this.shouldAllowAirborneMotion());

    if (target) {
      const isAlertMovement = this.state === "chase" || this.state === "flee";
      const investigationMultiplier = (this.state === "search" || this.state === "investigateNoise")
        ? (this.config.investigationSpeedMultiplier ?? 1.12)
        : 1;
      const baseSpeed = (isAlertMovement ? this.config.chaseSpeed : this.config.patrolSpeed)
        * investigationMultiplier;
      const speed = baseSpeed
        * (this.speedMultiplier || 1.0)
        * (this.progressionSpeedMultiplier || 1.0);

      const beforeMove = this.group.position.clone();
      this.moveToward(target, speed, deltaTime);
      this.updateStuckState(deltaTime, target, beforeMove);
    } else {
      this.stuckTimer = 0;
      this.debugPathTarget = null;
    }

    const isPlayerSprinting = Boolean(playerState.isSprinting);
    const canReactAtCloseRange = this.state === "chase" || this.isPlayerInFront(playerPosition) || isPlayerSprinting;
    this.caughtPlayer = !isPlayerHidden
      && this.state !== "flee"
      && this.isSameLevelAs(playerPosition)
      && canReactAtCloseRange
      && distance2D(this.group.position, playerPosition) <= this.config.catchDistance;
  }

  createActions(clips) {
    if (!this.mixer) {
      return {};
    }

    const actions = {};
    for (const [name, clip] of Object.entries(clips)) {
      if (!clip) {
        continue;
      }
      const action = this.mixer.clipAction(clip);
      action.enabled = true;
      actions[name] = action;
    }
    return actions;
  }

  playAction(name, fadeSeconds = 0.18) {
    const nextAction = this.actions[name] || this.actions.patrol;
    if (!nextAction || this.currentActionName === name) {
      return;
    }

    this.resumeAnimatedPose();
    const previousAction = this.currentActionName ? this.actions[this.currentActionName] : null;
    nextAction.reset();
    nextAction.play();
    nextAction.fadeIn(fadeSeconds);
    if (previousAction && previousAction !== nextAction) {
      previousAction.fadeOut(fadeSeconds);
    }
    this.currentActionName = name;
  }

  shouldAllowAirborneMotion() {
    return false;
  }

  getLowestGroundPoint() {
    let currentMinY = null;
    let hasBones = false;
    this.modelRoot.traverse((child) => {
      if (child.isBone) hasBones = true;
    });

    if (hasBones) {
      let minY = Infinity;
      this.modelRoot.traverse((child) => {
        if (child.isBone) {
          const name = child.name.toLowerCase();
          if (name.includes("root") || name.includes("hips") || name.includes("pelvis") || 
              name.includes("spine") || name.includes("chest") || name.includes("neck") || 
              name.includes("head") || name.includes("clavicle") || name.includes("shoulder")) {
            return;
          }
          child.updateMatrixWorld(true);
          const worldPos = new THREE.Vector3();
          child.getWorldPosition(worldPos);
          if (worldPos.y < minY) {
            minY = worldPos.y;
          }
        }
      });
      if (Number.isFinite(minY)) {
        currentMinY = minY;
      }
    }

    if (currentMinY === null) {
      this.modelRoot.updateMatrixWorld(true);
      const bounds = new THREE.Box3().setFromObject(this.modelRoot);
      if (Number.isFinite(bounds.min.y)) {
        currentMinY = bounds.min.y;
      }
    }
    return currentMinY;
  }

  snapModelToGround(allowAirborne = false) {
    const currentMinY = this.getLowestGroundPoint();
    if (currentMinY === null) {
      return;
    }

    // Target ground level: group.position.y (set by collisionWorld)
    // minus visualGroundSink (sinks model slightly into floor for realism)
    // plus footOffset (per-model correction for models whose pivot ≠ foot sole)
    const footOffset = this.config.footOffset ?? 0;
    const groundY = this.group.position.y - (this.config.visualGroundSink ?? 0) + footOffset;

    if (allowAirborne && currentMinY >= groundY) {
      return;
    }

    const offset = groundY - currentMinY;
    if (Math.abs(offset) > 0.001) {
      this.modelRoot.position.y += offset;
      this.modelRoot.updateMatrixWorld(true);
    }
  }

  getModelGroundOffset() {
    const currentMinY = this.getLowestGroundPoint();
    return currentMinY !== null ? currentMinY - this.group.position.y : null;
  }

  matchesFloor(y) {
    const allowed = this.config.allowedFloor;
    if (allowed === undefined || allowed === null) return true;
    if (allowed === 1) return Math.abs(y - 0.0) < 2.0;
    if (allowed === 2) return Math.abs(y - 5.0) < 2.0;
    if (allowed === -1) return Math.abs(y - (-5.0)) < 2.0;
    return true;
  }

  updatePerception(playerPosition, deltaTime, playerState = {}) {
    const isPlayerHidden = Boolean(playerState.isHidden || playerState.isUndetectable);
    const isPlayerSprinting = Boolean(playerState.isSprinting);
    const wasChasing = this.state === "chase" || this.state === "flee";
    const wasAware = wasChasing || this.state === "search" || this.state === "investigateNoise";
    const canStartChase = playerState.canStartChase !== false || wasChasing;

    if (isPlayerHidden) {
      this.hasVisualContact = false;
      this.sightExposure = 0;
      if (this.state === "chase" || this.state === "flee") {
        this.memoryTimer -= deltaTime;
        if (this.memoryTimer <= 0) {
          this.beginSearch(this.lastKnownPlayerPosition, this.config.hiddenSearchSeconds ?? 2.2);
        }
      } else if (this.state === "search") {
        this.searchTimer -= deltaTime;
        if (this.searchTimer <= 0) {
          this.beginWander();
        }
      } else if (this.state === "investigateNoise") {
        this.investigationTimer -= deltaTime;
        if (this.investigationTimer <= 0) {
          this.beginSearch(this.investigationTarget, this.config.postNoiseSearchSeconds ?? 2.2);
        }
      }
      return;
    }

    // Floor Isolation Enforcer: Monsters NEVER cross or react to players outside their assigned floor
    if (this.config.allowedFloor !== undefined && !this.config.allowInterFloorPatrol) {
      const playerFloor = (playerPosition.y ?? 0) >= 3.0 ? 2 : ((playerPosition.y ?? 0) <= -3.0 ? -1 : 1);
      if (playerFloor !== this.config.allowedFloor) {
        if (this.state === "chase" || this.state === "flee") {
          this.beginWander();
        }
        this.memoryTimer = 0;
        this.lastKnownPlayerPosition = null;
        this.sightExposure = 0;
        this.hasVisualContact = false;
        return;
      }
    }

    const distance = distance2D(this.group.position, playerPosition);
    const sameLevel = this.isSameLevelAs(playerPosition);
    const currentSurface = this.collisionWorld.getSurfaceAt(this.group.position, { allowAnyFloor: true });
    const usesTransitionRoute = !sameLevel || currentSurface.type === "stair/transition";
    const canNavigateAcrossFloors = usesTransitionRoute && this.collisionWorld.canNavigateBetween(this.group.position, playerPosition);
    const giveUpRange = usesTransitionRoute
      ? (this.config.interFloorGiveUpRange ?? this.config.giveUpRange * 1.8)
      : this.config.giveUpRange;

    if ((this.state === "chase" || this.state === "flee") && (distance > giveUpRange || (usesTransitionRoute && !canNavigateAcrossFloors))) {
      this.beginWander();
      return;
    }

    if ((this.state === "chase" || this.state === "flee") && canNavigateAcrossFloors) {
      this.memoryTimer = this.config.memorySeconds;
      this.lastKnownPlayerPosition = playerPosition.clone();
      return;
    }

    if (!sameLevel) {
      this.memoryTimer = 0;
      this.lastKnownPlayerPosition = null;
      this.sightExposure = 0;
      this.hasVisualContact = false;
      return;
    }

    const directorDetection = this.progressionDetectionMultiplier || 1.0;
    const hearing = this.config.hearingRange * (this.detectionMultiplier || 1.0) * directorDetection;
    const detection = this.config.detectionRange * (this.detectionMultiplier || 1.0) * directorDetection;
    const canHear = isPlayerSprinting && distance <= hearing;
    const canSee = distance <= detection
      && this.isPlayerInFront(playerPosition)
      && this.collisionWorld.hasLineOfSight(this.group.position, playerPosition);
    this.hasVisualContact = canSee;

    const sightConfirmSeconds = Math.max(0.05, this.config.sightConfirmSeconds ?? 0.32);
    const closeRange = this.config.closeRangeAutoDetect ?? Math.min(2.25, detection * 0.32);
    if (canSee) {
      const proximity = 1 - Math.min(1, distance / Math.max(detection, 0.001));
      const exposureRate = 1 + proximity * 1.6 + (isPlayerSprinting ? 0.9 : 0);
      this.sightExposure = Math.min(sightConfirmSeconds, this.sightExposure + deltaTime * exposureRate);
      this.lostSightTimer = 0;
    } else {
      this.sightExposure = Math.max(0, this.sightExposure - deltaTime * 1.8);
    }

    const sightConfirmed = canSee
      && (wasChasing || distance <= closeRange || this.sightExposure >= sightConfirmSeconds);

    if (canHear || sightConfirmed) {
      const wasAlert = this.state === "chase" || this.state === "flee";
      if (canStartChase) {
        this.state = "chase";
        this.searchTarget = null;
        this.investigationTarget = null;
      } else {
        this.beginSearch(playerPosition, this.config.blockedPursuitSearchSeconds ?? 3.2);
      }
      this.memoryTimer = this.config.memorySeconds;
      this.lastKnownPlayerPosition = clonePoint(playerPosition);
      if (!wasAware) {
        const range = Math.max(detection, 0.001);
        const proximity = 1 - Math.min(1, distance / range);
        this.lastDetectionEvent = {
          enemyId: this.config.id,
          label: this.config.label,
          mode: sightConfirmed ? "sight" : "hearing",
          full: sightConfirmed && canStartChase,
          distance,
          strength: Math.min(1, (sightConfirmed ? 0.76 : 0.5) + proximity * 0.3),
        };
      }
      return;
    }

    if (wasChasing) {
      this.lostSightTimer += deltaTime;
      const graceSeconds = this.config.lostSightGraceSeconds ?? 0.65;
      if (this.lostSightTimer <= graceSeconds) {
        return;
      }
      this.memoryTimer -= deltaTime;
      if (this.memoryTimer <= 0) {
        this.beginSearch(this.lastKnownPlayerPosition, this.config.searchSeconds ?? 5.2);
      }
      return;
    }

    if (this.state === "search") {
      this.searchTimer -= deltaTime;
      if (this.searchTimer <= 0) {
        this.beginWander();
      }
      return;
    }

    if (this.state === "investigateNoise") {
      this.investigationTimer -= deltaTime;
      if (this.investigationTimer <= 0) {
        this.beginSearch(this.investigationTarget, this.config.postNoiseSearchSeconds ?? 2.2);
      }
    }
  }

  beginSearch(position, seconds = 5.2) {
    this.state = "search";
    this.searchTarget = position ? clonePoint(position) : this.group.position.clone();
    this.searchTimer = Math.max(0.1, seconds);
    this.lastKnownPlayerPosition = null;
    this.memoryTimer = 0;
    this.lostSightTimer = 0;
    this.chasePath = [];
    this.chasePathGoal = null;
    this.patrolPath = [];
    this.patrolPathGoal = null;
    this.waitTurnDirection = Math.random() < 0.5 ? -1 : 1;
  }

  beginWander() {
    this.state = "wander";
    this.memoryTimer = 0;
    this.lastKnownPlayerPosition = null;
    this.searchTarget = null;
    this.searchTimer = 0;
    this.investigationTarget = null;
    this.investigationTimer = 0;
    this.sightExposure = 0;
    this.hasVisualContact = false;
    this.lostSightTimer = 0;
    this.wanderTarget = null;
    this.wanderRetargetTimer = this.config.postAlertCalmSeconds ?? 0.8;
    this.chasePath = [];
    this.chasePathGoal = null;
    this.patrolPath = [];
    this.patrolPathGoal = null;
  }

  getTarget(playerPosition, deltaTime = 0) {
    if (this.state === "chase") {
      return this.getChaseTarget(this.lastKnownPlayerPosition || playerPosition, deltaTime);
    }

    if (this.state === "flee") {
      return this.getFleeTarget(playerPosition, deltaTime);
    }

    if (this.state === "search") {
      return this.getInvestigationTarget(this.searchTarget, deltaTime, "search");
    }

    if (this.state === "investigateNoise") {
      return this.getInvestigationTarget(this.investigationTarget, deltaTime, "noise");
    }

    if (this.state === "idle_short") {
      this.waitTimer -= deltaTime;
      this.group.rotation.y += deltaTime * (this.config.lookAroundTurnSpeed ?? 0.36) * this.waitTurnDirection;
      if (this.waitTimer <= 0) {
        this.state = "wander";
      }
      return null;
    }

    if (this.state === "investigateCabinet") {
      return null;
    }

    return this.getWanderTarget(deltaTime);
  }

  getInvestigationTarget(target, deltaTime, mode) {
    if (!target) {
      this.beginWander();
      return null;
    }

    const arrivalDistance = this.config.searchArrivalDistance ?? 0.9;
    if (distance2D(this.group.position, target) > arrivalDistance) {
      return this.getPathTarget(target, deltaTime, mode);
    }

    this.patrolPath = [];
    this.patrolPathGoal = null;
    this.debugPathTarget = { mode, type: "look-around", x: target.x, y: target.y, z: target.z };
    const pulse = Math.sin((mode === "noise" ? this.investigationTimer : this.searchTimer) * 1.7);
    const direction = pulse >= 0 ? this.waitTurnDirection : -this.waitTurnDirection;
    this.group.rotation.y += deltaTime * (this.config.searchTurnSpeed ?? 0.62) * direction;
    return null;
  }

  getWanderTarget(deltaTime) {
    const pos = this.group.position;
    const minDist = this.config.wanderMinDistance ?? 10;
    const maxDist = this.config.wanderMaxDistance ?? 40;
    const chunkRadius = this.config.wanderChunkRadius ?? 3;
    const retargetRange = this.config.wanderRetargetSeconds ?? [6, 12];

    // 1. Countdown — when timer hits zero (or we have no target), pick a new waypoint
    this.wanderRetargetTimer -= deltaTime;
    const needNewTarget = this.wanderRetargetTimer <= 0 || !this.wanderTarget;

    if (needNewTarget) {
      this.pickNextWaypointTarget(minDist, maxDist, chunkRadius);
      const span = retargetRange[1] - retargetRange[0];
      this.wanderRetargetTimer = retargetRange[0] + Math.random() * span;
    }

    // 2. If we have a valid far waypoint, use pathfinding to move toward it
    if (this.wanderTarget) {
      const distToTarget = distance2D(pos, this.wanderTarget);

      // Arrived — pick a new target next frame
      if (distToTarget < 1.2) {
        this.rememberWanderTarget(this.wanderTarget);
        this.wanderTarget = null;
        this.wanderRetargetTimer = this.getNextPatrolWait();
        this.patrolPath = [];
        this.patrolPathGoal = null;
        this.wanderStuckCount = 0;
        this.state = "idle_short";
        this.waitTimer = this.getNextPatrolWait();
        this.waitTurnDirection = Math.random() < 0.5 ? -1 : 1;
        return null;
      }

      return this.getPathTarget(this.wanderTarget, deltaTime, "wander");
    }

    return null;
  }

  pickNextWaypointTarget(minDist = 10, maxDist = 40, chunkRadius = 3) {
    const pos = this.group.position;
    const cx = Math.floor((pos.x + 8) / 16);
    const cz = Math.floor((pos.z + 8) / 16);

    // Collect waypoints from all chunks within chunkRadius, plus config waypoints
    const candidates = [];
    
    // 1. Check specific config waypoints if configured
    if (this.config.waypoints && this.config.waypoints.length > 0) {
      for (const wp of this.config.waypoints) {
        const wpY = typeof wp[1] === "number" ? wp[1] : (this.group.position.y ?? 0);
        if (!this.matchesFloor(wpY)) continue;
        const wpVec = new THREE.Vector3(wp[0], wpY, wp[2]);
        const dist = distance2D(pos, wpVec);
        if (dist >= Math.min(minDist, 4) && dist <= maxDist) {
          candidates.push(wpVec);
        }
      }
    }

    // 2. Collect loaded chunk waypoints matching this floor
    const mapBuilder = window.__happyToy?.mapBuilder;
    if (mapBuilder) {
      for (let dx = -chunkRadius; dx <= chunkRadius; dx++) {
        for (let dz = -chunkRadius; dz <= chunkRadius; dz++) {
          const key = `${cx + dx},${cz + dz}`;
          const chunk = mapBuilder.loadedChunks.get(key);
          if (chunk?.waypoints) {
            for (const wp of chunk.waypoints) {
              const wpY = typeof wp[1] === "number" ? wp[1] : (this.group.position.y ?? 0);
              if (!this.matchesFloor(wpY)) continue;
              const wpVec = new THREE.Vector3(wp[0], wpY, wp[2]);
              const dist = distance2D(pos, wpVec);
              // Only consider waypoints in the desired distance band
              if (dist >= minDist && dist <= maxDist) {
                candidates.push(wpVec);
              }
            }
          }
        }
      }
    }

    // Prefer useful mid/far routes, but retain enough variance that patrols cannot be memorized.
    if (candidates.length > 0) {
      const chosen = this.chooseWeightedWanderCandidate(candidates, pos, minDist, maxDist);
      this.wanderTarget = chosen;
      this.wanderStuckCount = 0;
      this.patrolPath = [];
      this.patrolPathGoal = null;
      return;
    }

    // Fallback: if no distant waypoints exist, pick any nearby waypoint on this floor
    const fallbackCandidates = [];
    if (this.config.waypoints && this.config.waypoints.length > 0) {
      for (const wp of this.config.waypoints) {
        const wpY = typeof wp[1] === "number" ? wp[1] : (this.group.position.y ?? 0);
        if (!this.matchesFloor(wpY)) continue;
        const wpVec = new THREE.Vector3(wp[0], wpY, wp[2]);
        if (distance2D(pos, wpVec) > 1.2) {
          fallbackCandidates.push(wpVec);
        }
      }
    }
    if (mapBuilder) {
      for (let dx = -1; dx <= 1; dx++) {
        for (let dz = -1; dz <= 1; dz++) {
          const key = `${cx + dx},${cz + dz}`;
          const chunk = mapBuilder.loadedChunks.get(key);
          if (chunk?.waypoints) {
            for (const wp of chunk.waypoints) {
              const wpY = typeof wp[1] === "number" ? wp[1] : (this.group.position.y ?? 0);
              if (!this.matchesFloor(wpY)) continue;
              const wpVec = new THREE.Vector3(wp[0], wpY, wp[2]);
              if (distance2D(pos, wpVec) > 1.5) {
                fallbackCandidates.push(wpVec);
              }
            }
          }
        }
      }
    }

    if (fallbackCandidates.length > 0) {
      const chosen = this.chooseWeightedWanderCandidate(fallbackCandidates, pos, 1.5, Math.max(8, maxDist));
      this.wanderTarget = chosen;
      return;
    }

    // Last resort: random direction 5m ahead on same floor level
    const theta = Math.random() * Math.PI * 2;
    this.wanderTarget = new THREE.Vector3(
      pos.x + Math.cos(theta) * 5,
      pos.y,
      pos.z + Math.sin(theta) * 5,
    );
  }

  chooseWeightedWanderCandidate(candidates, position, minDistance, maxDistance) {
    const unique = [];
    const seen = new Set();
    for (const candidate of candidates) {
      const key = wanderTargetKey(candidate);
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);
      unique.push(candidate);
    }

    const preferredDistance = minDistance + (maxDistance - minDistance) * (0.42 + Math.random() * 0.3);
    const recent = new Set(this.recentWanderTargets);
    const weighted = unique.map((candidate) => {
      const distance = distance2D(position, candidate);
      const distanceFit = 1 / (1 + Math.abs(distance - preferredDistance) * 0.18);
      const novelty = recent.has(wanderTargetKey(candidate)) ? 0.08 : 1;
      return { candidate, weight: Math.max(0.01, distanceFit * novelty) };
    });
    let roll = Math.random() * weighted.reduce((sum, entry) => sum + entry.weight, 0);
    for (const entry of weighted) {
      roll -= entry.weight;
      if (roll <= 0) {
        return entry.candidate.clone();
      }
    }
    return weighted[weighted.length - 1].candidate.clone();
  }

  rememberWanderTarget(target) {
    if (!target) {
      return;
    }
    this.recentWanderTargets.push(wanderTargetKey(target));
    const historySize = this.config.wanderHistorySize ?? 4;
    if (this.recentWanderTargets.length > historySize) {
      this.recentWanderTargets.splice(0, this.recentWanderTargets.length - historySize);
    }
  }

  getFleeTarget(playerPosition, deltaTime) {
    if (!playerPosition || typeof playerPosition.x !== "number") {
      return null;
    }
    this.chasePathTimer -= deltaTime;
    // Calculate opposite direction vector from player to enemy
    const dir = new THREE.Vector3().subVectors(this.group.position, playerPosition).normalize();
    // Project a point 20–30 m away in the opposite direction for long-distance fleeing
    const fleeDistance = 20 + Math.random() * 10;
    const goal = new THREE.Vector3().copy(this.group.position).addScaledVector(dir, fleeDistance);

    // Prefer the farthest valid waypoint in the flee direction
    const waypoints = this.getActivePatrolWaypoints();
    let bestWp = null;
    let bestScore = -Infinity;
    for (const wp of waypoints) {
      const wpPoint = vectorFromArray(wp);
      const distFromPlayer = distance2D(wpPoint, playerPosition);
      const alignment = dir.dot(
        new THREE.Vector3().subVectors(wpPoint, this.group.position).normalize(),
      );
      // Score: distance from player + bonus for aligning with flee direction
      const score = distFromPlayer + alignment * 8;
      if (score > bestScore) {
        bestScore = score;
        bestWp = wpPoint;
      }
    }

    if (bestWp && distance2D(bestWp, playerPosition) > distance2D(this.group.position, playerPosition)) {
      return this.getPathTarget(bestWp, deltaTime, "flee");
    }

    // Fallback: straight-line flee goal
    const surface = this.collisionWorld.getSurfaceAt(goal, { allowAnyFloor: true });
    if (surface.walkable) {
      return this.getPathTarget(goal, deltaTime, "flee");
    }
    return null;
  }

  getChaseTarget(playerPosition, deltaTime) {
    if (!playerPosition || typeof playerPosition.x !== "number") {
      return null;
    }
    this.chasePathTimer -= deltaTime;
    const goalMoved = !this.chasePathGoal || distance2D(this.chasePathGoal, playerPosition) > 0.9;
    const canMoveDirect = this.isSameLevelAs(playerPosition)
      && this.collisionWorld.hasLineOfSight(this.group.position, playerPosition);

    if (canMoveDirect) {
      this.chasePath = [];
      this.chasePathGoal = playerPosition.clone();
      this.chasePathTimer = 0;
      return playerPosition;
    }

    return this.getPathTarget(playerPosition, deltaTime, "chase", goalMoved);
  }

  getPathTarget(goal, deltaTime, mode, forceRefresh = false) {
    if (!goal || typeof goal.x !== "number") {
      return null;
    }
    const isAlert = mode === "chase" || mode === "flee";
    const pathKey = isAlert ? "chasePath" : "patrolPath";
    const timerKey = isAlert ? "chasePathTimer" : "patrolPathTimer";
    const goalKey = isAlert ? "chasePathGoal" : "patrolPathGoal";
    this[timerKey] -= deltaTime;

    const canMoveDirect = this.isSameLevelAs(goal)
      && this.collisionWorld.hasLineOfSight(this.group.position, goal);
    if (canMoveDirect) {
      this[pathKey] = [];
      this[goalKey] = goal.clone?.() || vectorFromArray([goal.x, goal.y, goal.z]);
      this[timerKey] = 0;
      this.debugPathTarget = { mode, type: "direct", x: goal.x, y: goal.y, z: goal.z };
      return goal;
    }

    const goalMoved = forceRefresh || !this[goalKey] || distance2D(this[goalKey], goal) > 0.9;
    if (this[pathKey] === null || this[timerKey] <= 0 || goalMoved) {
      this[pathKey] = this.collisionWorld.findPath(this.group.position, goal, this.config.radius, {
        cellSize: this.config.pathCellSize ?? 0.85,
        allowInterFloor: mode === "chase" || mode === "flee" || (mode === "wander" && Boolean(this.config.allowInterFloorPatrol)),
      });
      this[goalKey] = goal.clone?.() || vectorFromArray([goal.x, goal.y, goal.z]);
      this[timerKey] = this.config.pathRefreshSeconds ?? 0.28;
      if (this[pathKey].length === 0) {
        if (mode === "wander") {
          // Pathfinding to wanderTarget failed — clear it so the next frame picks a new one.
          // Broaden the search by temporarily lowering minDist requirements.
          this.wanderTarget = null;
          this.wanderRetargetTimer = 0;
          this.wanderStuckCount = (this.wanderStuckCount ?? 0) + 1;
          // If persistently failing, try nearest waypoint as fallback
          if (this.wanderStuckCount >= 2) {
            this.pickNextWaypointTarget(2, 40, this.config.wanderChunkRadius ?? 3);
            this.wanderStuckCount = 0;
          }
          return null;
        }
        console.warn(`[Enemy:${this.config.id}] pathfinding failed in ${mode} mode.`);
      }
    }

    while (this[pathKey] && this[pathKey].length > 1 && distance2D(this.group.position, this[pathKey][1]) < 0.4) {
      this[pathKey].shift();
    }

    const nextTarget = (this[pathKey] && (this[pathKey][1] || this[pathKey][0])) || goal;
    this.debugPathTarget = {
      mode,
      type: (this[pathKey] && this[pathKey].length > 0) ? "path" : "fallback",
      pathLength: this[pathKey] ? this[pathKey].length : 0,
      x: nextTarget.x,
      y: nextTarget.y,
      z: nextTarget.z,
    };
    return nextTarget;
  }

  moveToward(target, speed, deltaTime) {
    const direction = direction2D(this.group.position, target);
    if (direction.lengthSq() <= 0.0001) {
      return;
    }

    this.openDoorOnPath(direction);
    const previousPosition = this.group.position.clone();
    this.group.position.addScaledVector(direction, speed * deltaTime);
    this.collisionWorld.resolveCircle(this.group.position, this.config.radius);
    this.collisionWorld.resolveActorPosition(
      previousPosition,
      this.group.position,
      this.config.radius,
      { actorId: this.config.id },
    );
    this.group.rotation.y = yawFromDirection(direction);
  }

  openDoorOnPath(direction) {
    for (const door of this.doors) {
      if (door.isOpen || door.isLocked || door.isBlocked || door.distanceTo(this.group.position) > 1.75) {
        continue;
      }

      const doorDirection = direction2D(this.group.position, door.position);
      if (direction.dot(doorDirection) > 0.05) {
        door.isOpen = true;
      }
    }
  }

  updateStuckState(deltaTime, target, beforeMove) {
    const moved = distance2D(beforeMove, this.group.position);
    const targetDistance = distance2D(this.group.position, target);
    if (targetDistance > 0.7 && moved < 0.025) {
      this.stuckTimer += deltaTime;
    } else {
      this.stuckTimer = Math.max(0, this.stuckTimer - deltaTime * 2);
    }

    if (this.stuckTimer < 1.15) {
      return;
    }

    this.tryUnstuck(target);
    this.stuckTimer = 0;
  }

  tryUnstuck(target) {
    if (this.state === "wander" || this.state === "idle_short") {
      // Force a new far waypoint immediately instead of just advancing one step
      this.wanderStuckCount = (this.wanderStuckCount ?? 0) + 1;
      this.wanderTarget = null;
      this.wanderRetargetTimer = 0;
      this.patrolPath = [];
      this.patrolPathGoal = null;

      if (this.wanderStuckCount >= 3) {
        // Repeatedly stuck — snap to nearest valid waypoint
        const waypoints = this.getActivePatrolWaypoints();
        if (waypoints.length > 0) {
          const chosen = waypoints[Math.floor(Math.random() * waypoints.length)];
          const candidate = vectorFromArray(chosen);
          const surface = this.collisionWorld.getSurfaceAt(candidate, { allowAnyFloor: true });
          if (surface.walkable) {
            this.group.position.set(candidate.x, surface.y, candidate.z);
            this.collisionWorld.snapToValidSurface(this.group.position, { actorId: `${this.config.id}-unstuck` });
            this.lastUnstuckTarget = this.group.position.clone();
            this.wanderStuckCount = 0;
          }
        }
      }
      return;
    }

    const path = (this.state === "chase" || this.state === "flee") ? this.chasePath : this.patrolPath;
    const candidate = path.find((point) => distance2D(this.group.position, point) > 0.55)
      || pointFromWaypoint(this.collisionWorld.findNearestTransitionWaypoint(this.group.position))
      || target;
    const surface = this.collisionWorld.getSurfaceAt(candidate, { allowAnyFloor: true });
    if (!surface.walkable) {
      console.warn(`[Enemy:${this.config.id}] unstuck cancelled because target surface is ${surface.type}.`);
      return;
    }

    this.group.position.set(candidate.x, surface.y, candidate.z);
    this.collisionWorld.snapToValidSurface(this.group.position, { actorId: `${this.config.id}-unstuck` });
    this.lastUnstuckTarget = this.group.position.clone();
  }

  beginCabinetInvestigation(cabinet) {
    this.state = "investigateCabinet";
    this.cabinetTarget = cabinet;
    this.memoryTimer = 0;
    this.lastKnownPlayerPosition = null;
    this.chasePath = [];
    this.chasePathTimer = 0;
    this.chasePathGoal = null;
    this.playAction("chase");
  }

  updateCabinetInvestigation(deltaTime) {
    if (!this.cabinetTarget) {
      this.state = "wander";
      return;
    }

    const guardPosition = this.cabinetTarget.getGuardPosition();
    if (distance2D(this.group.position, guardPosition) > 0.42) {
      const target = this.getPathTarget(guardPosition, deltaTime, "chase") || guardPosition;
      const speed = this.config.cabinetInvestigateSpeed ?? Math.max(this.config.patrolSpeed * 1.35, this.config.chaseSpeed * 0.82);
      this.playAction("chase");
      this.moveToward(target, speed, deltaTime);
      this.snapModelToGround(false);
      return;
    }

    this.playAction("chase");
    this.snapModelToGround(false);
    const faceDirection = direction2D(this.group.position, this.cabinetTarget.position);
    if (faceDirection.lengthSq() > 0.0001) {
      this.group.rotation.y = yawFromDirection(faceDirection);
    }
  }

  endCabinetInvestigation() {
    this.beginWander();
    this.cabinetTarget = null;
    this.resumeAnimatedPose();
    this.caughtPlayer = false;
    this.waitTimer = this.config.postCabinetWaitSeconds ?? 0.5;
    this.waitTurnDirection = Math.random() < 0.5 ? -1 : 1;
    this.chooseNearestWaypoint();
  }

  resumeChaseFromCabinet(playerPosition) {
    this.state = "chase";
    this.cabinetTarget = null;
    this.memoryTimer = this.config.memorySeconds;
    this.lastKnownPlayerPosition = playerPosition.clone?.() || vectorFromArray([playerPosition.x, playerPosition.y, playerPosition.z]);
    this.caughtPlayer = false;
    this.chasePath = [];
    this.chasePathTimer = 0;
    this.chasePathGoal = null;
    this.patrolPath = [];
    this.patrolPathGoal = null;
    this.waitTimer = 0;
    this.resumeAnimatedPose();
    this.playAction("chase");
  }

  isActivelyChasing() {
    return this.state === "chase";
  }

  isSameLevelAs(position) {
    return Math.abs((this.group.position.y ?? 0) - (position.y ?? 0)) <= (this.config.floorAwarenessHeight ?? 1.8);
  }

  isPlayerInFront(playerPosition) {
    const directionToPlayer = direction2D(this.group.position, playerPosition);
    if (directionToPlayer.lengthSq() <= 0.0001) {
      return true;
    }
    const facingDirection = new THREE.Vector3(
      Math.sin(this.group.rotation.y),
      0,
      Math.cos(this.group.rotation.y),
    );
    return facingDirection.dot(directionToPlayer) >= (this.config.frontAwarenessDot ?? 0.08);
  }

  playIdlePose() {
    const action = this.actions.idle || this.actions.patrol;
    if (!action) {
      return;
    }

    if (this.currentActionName !== "idlePose") {
      const previousAction = this.currentActionName ? this.actions[this.currentActionName] : null;
      action.reset();
      action.play();
      action.time = action.getClip().duration * (this.config.idlePoseRatio ?? 0.08);
      action.paused = true;
      if (previousAction && previousAction !== action) {
        previousAction.stop();
      }
      this.currentActionName = "idlePose";
    }

    this.isIdlePose = true;
    action.paused = true;
  }

  resumeAnimatedPose() {
    if (!this.isIdlePose) {
      return;
    }

    for (const action of Object.values(this.actions)) {
      action.paused = false;
    }
    this.isIdlePose = false;
    this.currentActionName = null;
  }

  chooseNearestWaypoint() {
    const waypoints = this.getActivePatrolWaypoints();
    let bestIndex = 0;
    let bestDistance = Infinity;
    for (let index = 0; index < waypoints.length; index += 1) {
      const waypoint = vectorFromArray(waypoints[index]);
      const distance = distance2D(this.group.position, waypoint);
      if (distance < bestDistance) {
        bestIndex = index;
        bestDistance = distance;
      }
    }
    this.currentWaypoint = bestIndex;
    this.patrolPath = [];
    this.patrolPathTimer = 0;
    this.patrolPathGoal = null;
  }

  advancePatrolWaypoint(length = null) {
    const count = length ?? this.getActivePatrolWaypoints().length;
    this.currentWaypoint = count > 0 ? (this.currentWaypoint + 1) % count : 0;
  }

  getActivePatrolWaypoints() {
    if (this.config.waypoints && this.config.waypoints.length > 0) {
      return this.config.waypoints;
    }
    const waypoints = [];
    const mapBuilder = window.__happyToy?.mapBuilder;
    if (mapBuilder && mapBuilder.loadedChunks) {
      const ecx = Math.floor((this.group.position.x + 8) / 16);
      const ecz = Math.floor((this.group.position.z + 8) / 16);
      const radius = 2;
      for (let dx = -radius; dx <= radius; dx++) {
        for (let dz = -radius; dz <= radius; dz++) {
          const key = `${ecx + dx},${ecz + dz}`;
          const chunk = mapBuilder.loadedChunks.get(key);
          if (chunk && chunk.waypoints) {
            for (const wp of chunk.waypoints) {
              const wpY = typeof wp[1] === "number" ? wp[1] : (this.group.position.y ?? 0);
              if (this.matchesFloor(wpY)) {
                waypoints.push(wp);
              }
            }
          }
        }
      }
    }

    if (waypoints.length > 0) {
      return waypoints;
    }
    return [[this.group.position.x, this.group.position.y, this.group.position.z]];
  }


  getNextPatrolWait() {
    const range = this.config.patrolWaitRange || [0.15, 0.75];
    return range[0] + Math.random() * (range[1] - range[0]);
  }

  getThreatAmount(playerPosition) {
    if (!this.isSameLevelAs(playerPosition)) {
      return 0;
    }
    const distance = distance2D(this.group.position, playerPosition);
    const distanceThreat = 1 - Math.min(1, distance / this.config.detectionRange);
    const stateBoost = this.state === "chase" || this.state === "investigateCabinet"
      ? 0.35
      : (this.state === "search" || this.state === "investigateNoise" ? 0.16 : 0);
    return Math.min(1, Math.max(0, distanceThreat + stateBoost));
  }

  notifyNoise(position, radius, options = {}) {
    if (this.isDormant || !position || !this.isSameLevelAs(position)) {
      return false;
    }
    const dist = distance2D(this.group.position, position);
    if (dist > radius || (this.state === "chase" && this.hasVisualContact)) {
      return false;
    }

    if (this.isBaby) {
      this.babyAwake = true;
    }
    this.investigationTarget = clonePoint(position);
    this.investigationTimer = options.duration ?? this.config.noiseInvestigationSeconds ?? 6.5;
    this.searchTarget = null;
    this.searchTimer = 0;
    this.lastKnownPlayerPosition = null;
    this.memoryTimer = 0;
    this.chasePath = [];
    this.chasePathGoal = null;
    this.patrolPath = [];
    this.patrolPathGoal = null;
    this.state = "investigateNoise";
    this.playAction("patrol");
    return true;
  }

  getDebugState() {
    const surface = this.collisionWorld.getSurfaceAt(this.group.position, { allowAnyFloor: true });

    const groundY = this.collisionWorld.getGroundY?.(this.group.position) ?? this.group.position.y;
    const cx = Math.floor((this.group.position.x + 8) / 16);
    const cz = Math.floor((this.group.position.z + 8) / 16);
    return {
      id: this.config.id,
      label: this.config.label,
      state: this.state,
      floor: surface.floor ?? "?",
      tileId: surface.id,
      tileType: surface.type,
      x: this.group.position.x,
      y: this.group.position.y,
      z: this.group.position.z,
      groundY,
      footOffset: this.config.footOffset ?? 0,
      visualGroundSink: this.config.visualGroundSink ?? 0,
      currentChunk: `${cx},${cz}`,
      wanderTarget: this.wanderTarget
        ? { x: this.wanderTarget.x.toFixed(1), z: this.wanderTarget.z.toFixed(1) }
        : null,
      wanderRetargetTimer: this.wanderRetargetTimer?.toFixed(2) ?? null,
      wanderStuckCount: this.wanderStuckCount ?? 0,
      hasVisualContact: this.hasVisualContact,
      sightExposure: this.sightExposure,
      searchTimer: this.searchTimer,
      investigationTimer: this.investigationTimer,
      pathTarget: this.debugPathTarget,
      chasePathLength: this.chasePath ? this.chasePath.length : 0,
      patrolPathLength: this.patrolPath ? this.patrolPath.length : 0,
      stuckTimer: this.stuckTimer,
      lastUnstuckTarget: this.lastUnstuckTarget,
    };
  }

  dispose() {
    this.mixer?.stopAllAction();
    if (this.mixer && this.modelRoot) {
      this.mixer.uncacheRoot(this.modelRoot);
    }
    this.mixer = null;
    if (this.shadowMesh) {
      this.shadowMesh.geometry?.dispose();
      if (this.shadowMesh.material) {
        this.shadowMesh.material.map?.dispose();
        this.shadowMesh.material.dispose();
      }
    }
  }
}

function addShadowBlob(group, radius) {
  const canvas = document.createElement("canvas");
  canvas.width = 64;
  canvas.height = 64;
  const ctx = canvas.getContext("2d");
  
  const gradient = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
  gradient.addColorStop(0, "rgba(0, 0, 0, 0.72)");
  gradient.addColorStop(0.4, "rgba(0, 0, 0, 0.45)");
  gradient.addColorStop(1, "rgba(0, 0, 0, 0.0)");
  
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, 64, 64);
  
  const texture = new THREE.CanvasTexture(canvas);
  const geometry = new THREE.PlaneGeometry(radius * 3.6, radius * 3.6);
  const material = new THREE.MeshStandardMaterial({
    map: texture,
    transparent: true,
    depthWrite: false,
    color: 0x000000,
    roughness: 1.0,
    metalness: 0.0,
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  mesh.rotation.x = -Math.PI / 2;
  mesh.position.y = 0.015; // slightly above ground to prevent z-fighting

  group.add(mesh);
  return mesh;
}

function pointFromWaypoint(waypoint) {
  if (!waypoint?.position) {
    return null;
  }
  return new THREE.Vector3(waypoint.position[0], waypoint.position[1], waypoint.position[2]);
}

function clonePoint(position) {
  if (position?.clone) {
    return position.clone();
  }
  return new THREE.Vector3(position?.x ?? 0, position?.y ?? 0, position?.z ?? 0);
}

function wanderTargetKey(position) {
  return `${Math.round(position.x * 2) / 2}:${Math.round((position.y ?? 0) * 2) / 2}:${Math.round(position.z * 2) / 2}`;
}
