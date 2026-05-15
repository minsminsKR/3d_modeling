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
    this.modelRoot = loadedAsset.root;
    this.group.add(loadedAsset.root);

    this.mixer = loadedAsset.animations.length
      ? new THREE.AnimationMixer(loadedAsset.root)
      : null;
    this.actions = this.createActions(loadedAsset.actions || {});
    this.currentActionName = null;
    this.isIdlePose = false;
    this.playAction("patrol", 0);
    this.snapModelToGround(false);

    this.state = "patrol";
    this.currentWaypoint = 0;
    this.lastKnownPlayerPosition = null;
    this.memoryTimer = 0;
    this.caughtPlayer = false;
    this.cabinetTarget = null;
    this.chasePath = [];
    this.chasePathTimer = 0;
    this.chasePathGoal = null;
    this.patrolPath = [];
    this.patrolPathTimer = 0;
    this.patrolPathGoal = null;
    this.waitTimer = 0;
    this.waitTurnDirection = 1;
    this.stuckTimer = 0;
    this.lastUnstuckTarget = null;
    this.debugPathTarget = null;
    this.lastDetectionEvent = null;
  }

  update(deltaTime, playerState) {
    const playerPosition = playerState.position || playerState;
    const isPlayerHidden = Boolean(playerState.isHidden || playerState.isUndetectable);
    this.lastDetectionEvent = null;

    if (!this.isIdlePose) {
      this.mixer?.update(deltaTime);
    }
    this.collisionWorld.snapToValidSurface(this.group.position, { actorId: this.config.id });

    if (this.state === "investigateCabinet") {
      this.updateCabinetInvestigation(deltaTime);
      this.caughtPlayer = false;
      return;
    }

    this.updatePerception(playerPosition, deltaTime, playerState);
    const target = this.getTarget(playerPosition, deltaTime);
    if (target || this.state === "chase") {
      this.playAction(this.state === "chase" ? "chase" : "patrol");
    } else {
      this.playIdlePose();
    }
    this.snapModelToGround(this.shouldAllowAirborneMotion());

    if (target) {
      const speed = this.state === "chase" ? this.config.chaseSpeed : this.config.patrolSpeed;
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

  snapModelToGround(allowAirborne = false) {
    this.modelRoot.updateMatrixWorld(true);
    const bounds = new THREE.Box3().setFromObject(this.modelRoot);
    if (!Number.isFinite(bounds.min.y)) {
      return;
    }

    const groundY = this.group.position.y - (this.config.visualGroundSink ?? 0);
    if (allowAirborne && bounds.min.y >= groundY) {
      return;
    }

    const offset = groundY - bounds.min.y;
    if (Math.abs(offset) > 0.002) {
      this.modelRoot.position.y += offset;
      this.modelRoot.updateMatrixWorld(true);
    }
  }

  getModelGroundOffset() {
    this.modelRoot.updateMatrixWorld(true);
    const bounds = new THREE.Box3().setFromObject(this.modelRoot);
    return Number.isFinite(bounds.min.y) ? bounds.min.y - this.group.position.y : null;
  }

  updatePerception(playerPosition, deltaTime, playerState = {}) {
    const isPlayerHidden = Boolean(playerState.isHidden || playerState.isUndetectable);
    const isPlayerSprinting = Boolean(playerState.isSprinting);
    const wasChasing = this.state === "chase";
    if (isPlayerHidden) {
      if (this.state === "chase" && this.memoryTimer > 0) {
        this.memoryTimer -= deltaTime;
        if (this.memoryTimer <= 0) {
          this.state = "patrol";
          this.lastKnownPlayerPosition = null;
        }
      }
      return;
    }

    const distance = distance2D(this.group.position, playerPosition);
    const sameLevel = this.isSameLevelAs(playerPosition);
    const currentSurface = this.collisionWorld.getSurfaceAt(this.group.position, { allowAnyFloor: true });
    const usesTransitionRoute = !sameLevel || currentSurface.type === "stair/transition";
    const canNavigateAcrossFloors = usesTransitionRoute && this.collisionWorld.canNavigateBetween(this.group.position, playerPosition);
    const giveUpRange = usesTransitionRoute
      ? (this.config.interFloorGiveUpRange ?? this.config.giveUpRange * 1.8)
      : this.config.giveUpRange;

    if (this.state === "chase" && (distance > giveUpRange || (usesTransitionRoute && !canNavigateAcrossFloors))) {
      this.state = "patrol";
      this.memoryTimer = 0;
      this.lastKnownPlayerPosition = null;
      return;
    }

    if (this.state === "chase" && canNavigateAcrossFloors) {
      this.memoryTimer = this.config.memorySeconds;
      this.lastKnownPlayerPosition = playerPosition.clone();
      return;
    }

    if (!sameLevel) {
      this.memoryTimer = 0;
      this.lastKnownPlayerPosition = null;
      return;
    }

    const canHear = isPlayerSprinting && distance <= this.config.hearingRange;
    const canSee = distance <= this.config.detectionRange
      && this.isPlayerInFront(playerPosition)
      && this.collisionWorld.hasLineOfSight(this.group.position, playerPosition);

    if (canHear || canSee) {
      this.state = "chase";
      this.memoryTimer = this.config.memorySeconds;
      this.lastKnownPlayerPosition = playerPosition.clone();
      if (!wasChasing) {
        const range = Math.max(this.config.detectionRange, 0.001);
        const proximity = 1 - Math.min(1, distance / range);
        this.lastDetectionEvent = {
          enemyId: this.config.id,
          label: this.config.label,
          mode: canSee ? "sight" : "hearing",
          full: canSee,
          distance,
          strength: Math.min(1, (canSee ? 0.78 : 0.55) + proximity * 0.35),
        };
      }
      return;
    }

    if (this.memoryTimer > 0) {
      this.memoryTimer -= deltaTime;
      return;
    }

    this.state = "patrol";
    this.lastKnownPlayerPosition = null;
  }

  getTarget(playerPosition, deltaTime = 0) {
    if (this.state === "chase") {
      return this.getChaseTarget(this.lastKnownPlayerPosition || playerPosition, deltaTime);
    }

    if (this.state === "investigateCabinet") {
      return null;
    }

    return this.getPatrolTarget(deltaTime);
  }

  getPatrolTarget(deltaTime) {
    if (this.waitTimer > 0) {
      this.waitTimer -= deltaTime;
      this.group.rotation.y += deltaTime * (this.config.lookAroundTurnSpeed ?? 0.42) * this.waitTurnDirection;
      return null;
    }

    const waypoints = this.getActivePatrolWaypoints();
    if (this.currentWaypoint >= waypoints.length) {
      this.currentWaypoint = 0;
    }
    const waypoint = vectorFromArray(waypoints[this.currentWaypoint]);
    if (distance2D(this.group.position, waypoint) < 0.48) {
      this.advancePatrolWaypoint(waypoints.length);
      this.patrolPath = [];
      this.patrolPathGoal = null;
      this.waitTimer = this.getNextPatrolWait();
      this.waitTurnDirection = Math.random() < 0.5 ? -1 : 1;
      return null;
    }

    return this.getPathTarget(waypoint, deltaTime, "patrol");
  }

  getChaseTarget(playerPosition, deltaTime) {
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
    const pathKey = mode === "chase" ? "chasePath" : "patrolPath";
    const timerKey = mode === "chase" ? "chasePathTimer" : "patrolPathTimer";
    const goalKey = mode === "chase" ? "chasePathGoal" : "patrolPathGoal";
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
    if (this[timerKey] <= 0 || goalMoved || this[pathKey].length === 0) {
      this[pathKey] = this.collisionWorld.findPath(this.group.position, goal, this.config.radius, {
        cellSize: this.config.pathCellSize ?? 0.85,
        allowInterFloor: mode === "chase" || (mode === "patrol" && Boolean(this.config.allowInterFloorPatrol)),
      });
      this[goalKey] = goal.clone?.() || vectorFromArray([goal.x, goal.y, goal.z]);
      this[timerKey] = this.config.pathRefreshSeconds ?? 0.28;
      if (this[pathKey].length === 0) {
        console.warn(`[Enemy:${this.config.id}] pathfinding failed in ${mode} mode.`);
        if (mode === "patrol") {
          this.advancePatrolWaypoint();
          return null;
        }
      }
    }

    while (this[pathKey].length > 1 && distance2D(this.group.position, this[pathKey][1]) < 0.4) {
      this[pathKey].shift();
    }

    const nextTarget = this[pathKey][1] || this[pathKey][0] || goal;
    this.debugPathTarget = {
      mode,
      type: this[pathKey].length > 0 ? "path" : "fallback",
      pathLength: this[pathKey].length,
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
    if (this.state !== "chase") {
      this.advancePatrolWaypoint();
      this.patrolPath = [];
      this.patrolPathGoal = null;
      console.warn(`[Enemy:${this.config.id}] patrol unstuck skipped teleport and advanced waypoint.`);
      return;
    }

    const path = this.state === "chase" ? this.chasePath : this.patrolPath;
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
    console.warn(`[Enemy:${this.config.id}] unstuck to x=${this.group.position.x.toFixed(2)}, y=${this.group.position.y.toFixed(2)}, z=${this.group.position.z.toFixed(2)}.`);
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
      this.state = "patrol";
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
    this.state = "patrol";
    this.cabinetTarget = null;
    this.resumeAnimatedPose();
    this.memoryTimer = 0;
    this.lastKnownPlayerPosition = null;
    this.caughtPlayer = false;
    this.chasePath = [];
    this.chasePathTimer = 0;
    this.chasePathGoal = null;
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
    const byFloor = this.config.patrolWaypointsByFloor;
    if (!byFloor) {
      return this.config.waypoints;
    }

    const surface = this.collisionWorld.getSurfaceAt(this.group.position, { allowAnyFloor: true });
    const floorKey = String(surface.floor ?? 1);
    return byFloor[floorKey] || this.config.waypoints;
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
    const stateBoost = this.state === "chase" || this.state === "investigateCabinet" ? 0.35 : 0;
    return Math.min(1, Math.max(0, distanceThreat + stateBoost));
  }

  getDebugState() {
    const surface = this.collisionWorld.getSurfaceAt(this.group.position, { allowAnyFloor: true });
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
      pathTarget: this.debugPathTarget,
      chasePathLength: this.chasePath.length,
      patrolPathLength: this.patrolPath.length,
      stuckTimer: this.stuckTimer,
      lastUnstuckTarget: this.lastUnstuckTarget,
    };
  }
}

function pointFromWaypoint(waypoint) {
  if (!waypoint?.position) {
    return null;
  }
  return new THREE.Vector3(waypoint.position[0], waypoint.position[1], waypoint.position[2]);
}
