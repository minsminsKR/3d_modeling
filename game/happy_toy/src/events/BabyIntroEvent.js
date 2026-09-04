import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D } from "../utils/math.js";

export class BabyIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;

    this.triggerPosition = new THREE.Vector3(14.5, -5.0, 32.0);
    this.triggerRadius = 3.5;
    this.babyLookTarget = new THREE.Vector3(10.5, -4.5, 30.0);

    this.startCameraPos = new THREE.Vector3();
    this.targetCameraPos = new THREE.Vector3();
    this.startYaw = 0;
    this.startPitch = 0;
    this.targetYaw = 0;
    this.targetPitch = 0;
  }

  get blocksPlayerControl() {
    return this.isControlLocked;
  }

  update(deltaTime) {
    if (this.state === "done") return;

    if (this.state === "idle") {
      this.checkTrigger();
      return;
    }

    if (this.state === "cutscene") {
      this.timer += deltaTime;

      this.updateCameraPan(deltaTime);

      if (this.timer >= 1.5) {
        this.releaseControl();
        this.state = "done";
      }
    }
  }

  checkTrigger() {
    if (this.hasTriggered || !this.game.player || this.game.player.isHidden) return;

    const playerPos = this.game.player.position;
    const verticalDist = Math.abs(playerPos.y - this.triggerPosition.y);
    if (verticalDist < 1.5 && distance2D(playerPos, this.triggerPosition) <= this.triggerRadius) {
      this.triggerEvent();
    }
  }

  triggerEvent() {
    this.hasTriggered = true;
    this.state = "cutscene";
    this.timer = 0;
    this.isControlLocked = true;

    this.game.hud?.setStatus("지하 보육실 안쪽 구석에서 서럽게 우는 아기의 뒷모습이 보입니다... 소리를 내면 깨어납니다.", 3200);

    if (this.game.enemyManager) {
      const baby = this.game.enemyManager.enemies.find(e => e.config.id === "baby-workshop" || e.config.id === "baby" || e.config.type === "baby");
      if (baby) {
        baby.setDormant(false);
        baby.group.position.set(10.5, -5.0, 30.0);
        this.game.collisionWorld?.snapToValidSurface(baby.group.position, { actorId: baby.config.id });
      }
    }


    const camera = this.game.camera;
    const player = this.game.player;

    player.input.consumePointerDelta();
    this.startCameraPos.copy(camera.position);

    const lookDir = this.babyLookTarget.clone().sub(this.startCameraPos).normalize();
    const rawTargetYaw = Math.atan2(-lookDir.x, -lookDir.z);

    this.startYaw = player.yaw;
    this.startPitch = player.pitch;
    this.targetYaw = this.startYaw + Math.atan2(Math.sin(rawTargetYaw - this.startYaw), Math.cos(rawTargetYaw - this.startYaw));
    this.targetPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));

    this.targetCameraPos.copy(this.startCameraPos).addScaledVector(lookDir, 0.4);
  }

  updateCameraPan(deltaTime) {
    const duration = 1.5;
    const rawT = Math.min(1, this.timer / duration);
    const t = rawT * rawT * (3 - 2 * rawT);

    const yaw = THREE.MathUtils.lerp(this.startYaw, this.targetYaw, t);
    const pitch = THREE.MathUtils.lerp(this.startPitch, this.targetPitch, t);

    this.game.camera.position.lerpVectors(this.startCameraPos, this.targetCameraPos, t);
    this.game.player.resetLook(yaw, pitch);
    this.game.camera.rotation.set(pitch, yaw, 0, "YXZ");
  }

  releaseControl() {
    this.isControlLocked = false;
    this.game.player.input.consumePointerDelta();
    this.game.player.resetLook(this.targetYaw, this.targetPitch);
  }

  reset() {
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
  }
}
