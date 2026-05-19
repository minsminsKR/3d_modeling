// 2층 액자방에서 hwacat 등장 컷신과 몬스터 변신을 담당합니다.
// 플레이어 입력을 잠깐 잠그고 카메라를 연한 붉은 얼룩으로 돌린 뒤, 연출 종료 후 기존 Enemy AI에 편입시킵니다.

import * as THREE from "three";
import { HWACAT_ANGRY_ENEMY_CONFIG, HWACAT_EVENT_CONFIG } from "../config/gameConfig.js";
import { CharacterLoader } from "../loaders/CharacterLoader.js";
import { distance2D, yawFromDirection } from "../utils/math.js";

export class MirrorHwacatEvent {
  constructor(config, dependencies) {
    this.config = config;
    this.scene = dependencies.scene;
    this.camera = dependencies.camera;
    this.player = dependencies.player;
    this.enemyManager = dependencies.enemyManager;
    this.hud = dependencies.hud;
    this.revealKeyById = dependencies.revealKeyById;
    this.loader = new CharacterLoader();
    this.group = null;
    this.modelRoot = null;
    this.paintingObject = null;
    this.paintingStartPosition = new THREE.Vector3();
    this.paintingStartRotation = new THREE.Euler();
    this.paintingTargetPosition = new THREE.Vector3();
    this.paintingTargetRotation = new THREE.Euler();
    this.pendingRemovalGroup = null;
    this.mixer = null;
    this.actions = {};
    this.currentActionName = null;
    this.state = "idle";
    this.timer = 0;
    this.hasTriggered = false;
    this.isControlLocked = false;
    this.controlPauseTimer = 0;
    this.isTransforming = false;
    this.startCameraPosition = new THREE.Vector3();
    this.targetCameraPosition = new THREE.Vector3();
    this.returnCameraStartPosition = new THREE.Vector3();
    this.returnCameraEndPosition = new THREE.Vector3();
    this.cameraReturnTimer = 0;
    this.cameraReturnDuration = 0;
    this.startYaw = 0;
    this.startPitch = 0;
    this.targetYaw = 0;
    this.targetPitch = 0;
    this.lookTarget = new THREE.Vector3(...this.config.lookAtPosition);
  }

  get isActive() {
    return this.state !== "idle" && this.state !== "done";
  }

  get blocksPlayerControl() {
    return this.state === "loading" || this.isControlLocked || this.controlPauseTimer > 0;
  }

  update(deltaTime) {
    if (this.state === "idle") {
      this.tryTrigger();
      return;
    }

    this.timer += deltaTime;
    this.controlPauseTimer = Math.max(0, this.controlPauseTimer - deltaTime);
    this.updateCameraReturn(deltaTime);
    this.mixer?.update(deltaTime);
    if (this.modelRoot) {
      this.snapModelToGround(this.modelRoot);
    }
    this.facePlayer();

    if (this.state === "paintingDrop") {
      this.updatePaintingDrop();
      if (this.timer >= (this.config.paintingDropSeconds ?? 0.7)) {
        this.beginHwacatSequence();
      }
      return;
    }

    if (this.state === "standUp") {
      if (this.isControlLocked) {
        this.updateCameraPan();
      }
      if (this.isControlLocked && this.timer >= this.config.cameraDuration) {
        this.releaseControl();
      }
      const standUpSeconds = Math.max(
        this.config.cameraDuration,
        this.getActionDuration("standUp", this.config.standUpSeconds ?? 2.8),
      );
      if (this.timer >= standUpSeconds) {
        this.playAction("dance", 0.12);
        this.state = "dance";
        this.timer = 0;
        this.hud.setStatus("hwacat이 천천히 플레이어를 바라봅니다.", 1800);
      }
      return;
    }

    if (this.state === "dance" && this.timer >= this.config.danceSeconds) {
      this.playAction("patrol", 0.18);
      this.state = "idleStand";
      this.timer = 0;
      return;
    }

    const idleSeconds = this.config.idleSeconds ?? this.config.standSeconds ?? 5;
    if (this.state === "idleStand" && this.timer >= idleSeconds && !this.isTransforming) {
      this.isTransforming = true;
      this.transformToEnemy();
      return;
    }

    if (this.state === "transformOverlap" && this.timer >= (this.config.transformOverlapSeconds ?? 0.5)) {
      this.removeHwacatModel();
      this.state = "done";
    }
  }

  tryTrigger() {
    if (this.hasTriggered || this.player.isHidden) {
      return;
    }

    const triggerPosition = new THREE.Vector3(...this.config.triggerPosition);
    const verticalDistance = Math.abs(this.player.position.y - triggerPosition.y);
    if (verticalDistance > 0.9 || distance2D(this.player.position, triggerPosition) > this.config.triggerRadius) {
      return;
    }

    this.hasTriggered = true;
    this.startPaintingDrop();
  }

  startPaintingDrop() {
    this.paintingObject = this.scene.getObjectByName(this.config.paintingId || "upper-hwa-painting") || null;
    this.timer = 0;
    this.state = "paintingDrop";
    this.hud.setStatus("벽에 걸린 액자가 아래로 떨어집니다.", 1500);

    if (!this.paintingObject) {
      console.warn(`[MirrorHwacatEvent] painting object not found: ${this.config.paintingId}`);
      this.beginHwacatSequence();
      return;
    }

    this.paintingStartPosition.copy(this.paintingObject.position);
    this.paintingStartRotation.copy(this.paintingObject.rotation);
    this.paintingTargetPosition.fromArray(
      this.config.paintingDropTargetPosition || [
        this.paintingStartPosition.x,
        Math.max(0.08, this.config.triggerPosition[1] + 0.06),
        this.paintingStartPosition.z,
      ],
    );
    const targetRotation = this.config.paintingDropTargetRotation || [
      Math.PI / 2,
      this.paintingStartRotation.y,
      this.paintingStartRotation.z,
    ];
    this.paintingTargetRotation.set(targetRotation[0], targetRotation[1], targetRotation[2], "XYZ");
  }

  updatePaintingDrop() {
    if (!this.paintingObject) {
      return;
    }

    const duration = Math.max(0.001, this.config.paintingDropSeconds ?? 0.7);
    const rawT = Math.min(1, this.timer / duration);
    const t = rawT * rawT;
    const bounce = rawT >= 0.82 ? Math.sin((rawT - 0.82) / 0.18 * Math.PI) * 0.035 : 0;
    this.paintingObject.position.lerpVectors(this.paintingStartPosition, this.paintingTargetPosition, t);
    this.paintingObject.position.y += bounce;
    this.paintingObject.rotation.set(
      THREE.MathUtils.lerp(this.paintingStartRotation.x, this.paintingTargetRotation.x, t),
      THREE.MathUtils.lerp(this.paintingStartRotation.y, this.paintingTargetRotation.y, t),
      THREE.MathUtils.lerp(this.paintingStartRotation.z, this.paintingTargetRotation.z, t),
      "XYZ",
    );
  }

  async beginHwacatSequence() {
    if (this.state === "loading") {
      return;
    }

    this.state = "loading";
    this.timer = 0;
    this.hud.setStatus("액자 뒤에서 붉은 기척이 번집니다.", 1800);
    await this.spawnHwacat();
    this.lockControl();
    this.playAction("standUp", 0);
    this.state = "standUp";
    this.timer = 0;
  }

  async spawnHwacat() {
    const asset = await this.loader.load(HWACAT_EVENT_CONFIG);
    this.group = new THREE.Group();
    this.group.name = "Hwacat Mirror Event";
    this.group.position.set(...this.config.spawnPosition);
    this.group.rotation.y = this.config.spawnYaw ?? 0;
    this.modelRoot = asset.root;
    this.group.add(asset.root);
    this.scene.add(this.group);

    this.mixer = asset.animations.length ? new THREE.AnimationMixer(asset.root) : null;
    this.actions = {};
    if (this.mixer) {
      for (const [name, clip] of Object.entries(asset.actions || {})) {
        if (!clip) {
          continue;
        }
        const action = this.mixer.clipAction(clip);
        action.enabled = true;
        if (name === "standUp") {
          action.setLoop(THREE.LoopOnce, 1);
          action.clampWhenFinished = true;
        } else {
          action.setLoop(THREE.LoopRepeat, Infinity);
          action.clampWhenFinished = false;
        }
        this.actions[name] = action;
      }
    }
    this.snapModelToGround(asset.root);
  }

  lockControl() {
    this.isControlLocked = true;
    this.controlPauseTimer = 0;
    this.player.input.consumePointerDelta();
    this.startCameraPosition.copy(this.camera.position);
    const retreatDirection = this.camera.position.clone().sub(this.lookTarget);
    if (retreatDirection.lengthSq() <= 0.0001) {
      retreatDirection.set(0, 0, 1);
    }
    retreatDirection.normalize();
    this.targetCameraPosition
      .copy(this.startCameraPosition)
      .addScaledVector(retreatDirection, this.config.cameraBackStep ?? 0.45);
    this.targetCameraPosition.y += this.config.cameraLift ?? 0.04;
    this.startYaw = this.player.yaw;
    this.startPitch = this.player.pitch;
    const targetDirection = this.lookTarget.clone().sub(this.targetCameraPosition).normalize();
    const rawTargetYaw = Math.atan2(-targetDirection.x, -targetDirection.z);
    this.targetYaw = this.startYaw + shortestAngleDelta(this.startYaw, rawTargetYaw);
    this.targetPitch = Math.asin(THREE.MathUtils.clamp(targetDirection.y, -1, 1));
  }

  releaseControl() {
    if (!this.isControlLocked) {
      return;
    }
    this.isControlLocked = false;
    this.cameraReturnDuration = this.config.cameraReturnDuration ?? 0.25;
    this.cameraReturnTimer = this.cameraReturnDuration;
    this.controlPauseTimer = Math.max(this.cameraReturnDuration, this.config.safePauseSeconds ?? 0.12);
    this.returnCameraStartPosition.copy(this.camera.position);
    this.returnCameraEndPosition.copy(this.startCameraPosition);
    this.camera.position.copy(this.targetCameraPosition);
    this.player.resetLook(this.targetYaw, this.targetPitch);
    this.camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
    this.player.input.consumePointerDelta();
  }

  updateCameraPan() {
    const duration = Math.max(0.001, this.config.cameraDuration);
    const rawT = Math.min(1, this.timer / duration);
    const t = rawT * rawT * rawT * (rawT * (rawT * 6 - 15) + 10);
    const yaw = THREE.MathUtils.lerp(this.startYaw, this.targetYaw, t);
    const pitch = THREE.MathUtils.lerp(this.startPitch, this.targetPitch, t);
    this.camera.position.lerpVectors(this.startCameraPosition, this.targetCameraPosition, t);
    this.player.resetLook(yaw, pitch);
    this.camera.rotation.set(pitch, yaw, 0, "YXZ");
  }

  updateCameraReturn(deltaTime) {
    if (this.cameraReturnTimer <= 0) {
      return;
    }

    this.cameraReturnTimer = Math.max(0, this.cameraReturnTimer - deltaTime);
    const duration = Math.max(0.001, this.cameraReturnDuration);
    const rawT = 1 - this.cameraReturnTimer / duration;
    const t = rawT * rawT * (3 - 2 * rawT);
    this.camera.position.lerpVectors(this.returnCameraStartPosition, this.returnCameraEndPosition, t);
    this.camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
  }

  playAction(name, fadeSeconds = 0.15) {
    const nextAction = this.actions[name] || this.actions.standUp || this.actions.patrol;
    if (!nextAction || this.currentActionName === name) {
      return;
    }

    for (const action of Object.values(this.actions)) {
      if (action !== nextAction) {
        action.fadeOut(fadeSeconds);
      }
    }
    nextAction.reset();
    nextAction.play();
    nextAction.fadeIn(fadeSeconds);
    this.currentActionName = name;
  }

  getActionDuration(name, fallback) {
    const action = this.actions[name];
    const duration = action?.getClip?.().duration;
    return Number.isFinite(duration) && duration > 0 ? duration : fallback;
  }

  facePlayer() {
    if (!this.group) {
      return;
    }

    const direction = new THREE.Vector3(
      this.player.position.x - this.group.position.x,
      0,
      this.player.position.z - this.group.position.z,
    );
    if (direction.lengthSq() > 0.0001) {
      direction.normalize();
      this.group.rotation.y = yawFromDirection(direction);
    }
  }

  async transformToEnemy() {
    if (!this.group) {
      return;
    }

    const spawn = this.group.position.toArray();
    const yaw = this.group.rotation.y;

    const enemyConfig = {
      ...HWACAT_ANGRY_ENEMY_CONFIG,
      spawn,
    };
    const enemy = await this.enemyManager.addEnemy(enemyConfig, { spawn, yaw, state: "chase", dynamic: true });
    enemy.state = "chase";
    enemy.lastKnownPlayerPosition = this.player.position.clone();
    enemy.memoryTimer = enemy.config.memorySeconds;
    if (this.config.rewardKeyId) {
      this.revealKeyById?.(this.config.rewardKeyId, spawn);
    }
    this.pendingRemovalGroup = this.group;
    this.state = "transformOverlap";
    this.timer = 0;
    this.hud.setStatus("hwacat이 뒤틀리며 달려듭니다.", 2200);
  }

  removeHwacatModel() {
    if (this.pendingRemovalGroup) {
      this.scene.remove(this.pendingRemovalGroup);
    } else if (this.group) {
      this.scene.remove(this.group);
    }
    this.pendingRemovalGroup = null;
    this.group = null;
    this.modelRoot = null;
  }

  snapModelToGround(root) {
    this.group.updateMatrixWorld(true);
    root.updateMatrixWorld(true);
    const bounds = new THREE.Box3().setFromObject(root);
    if (!Number.isFinite(bounds.min.y)) {
      return;
    }
    const offset = this.group.position.y - bounds.min.y;
    if (Math.abs(offset) > 0.002) {
      root.position.y += offset;
    }
    this.group.updateMatrixWorld(true);
    root.updateMatrixWorld(true);
  }

  reset() {
    this.releaseControl();
    if (this.group) {
      this.scene.remove(this.group);
    }
    if (this.pendingRemovalGroup) {
      this.scene.remove(this.pendingRemovalGroup);
    }
    this.enemyManager.removeEnemyById(HWACAT_ANGRY_ENEMY_CONFIG.id);
    this.group = null;
    this.modelRoot = null;
    this.pendingRemovalGroup = null;
    this.mixer = null;
    this.actions = {};
    this.currentActionName = null;
    this.state = "idle";
    this.timer = 0;
    this.hasTriggered = false;
    this.isControlLocked = false;
    this.controlPauseTimer = 0;
    this.cameraReturnTimer = 0;
    this.cameraReturnDuration = 0;
    this.isTransforming = false;
    if (this.paintingObject) {
      this.paintingObject.position.copy(this.paintingStartPosition);
      this.paintingObject.rotation.copy(this.paintingStartRotation);
    }
    this.paintingObject = null;
  }
}

function shortestAngleDelta(from, to) {
  return Math.atan2(Math.sin(to - from), Math.cos(to - from));
}
