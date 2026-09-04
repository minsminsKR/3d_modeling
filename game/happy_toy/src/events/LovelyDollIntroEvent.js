import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D } from "../utils/math.js";

export class LovelyDollIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;

    this.triggerPosition = new THREE.Vector3(-32.0, 0.0, 24.5);
    this.triggerRadius = 3.5;
    this.dollLookTarget = new THREE.Vector3(-32.0, 0.8, 32.0);


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
    if (distance2D(playerPos, this.triggerPosition) <= this.triggerRadius) {
      this.triggerEvent();
    }
  }

  triggerEvent() {
    this.hasTriggered = true;
    this.state = "cutscene";
    this.timer = 0;
    this.isControlLocked = true;

    soundManager.playSFX("musicbox");
    this.game.hud?.setStatus("붉은 놀이방 한가운데 조명 아래에서 기괴한 인형이 춤을 추고 있습니다...", 3200);

    if (this.game.lovelyDolls) {
      const doll = this.game.lovelyDolls.find(d => d.id === "lovely_doll_playroom" || d.id === "lovely_doll_1");
      if (doll) {
        doll.wakeUp?.();
      }
    }
    if (this.game.enemyManager) {
      const doll = this.game.enemyManager.enemies.find(e => e.config.id === "lovely_doll" || e.config.id === "lovelyDoll");
      if (doll) {
        doll.setDormant(false);
      }
    }


    const camera = this.game.camera;
    const player = this.game.player;

    player.input.consumePointerDelta();
    this.startCameraPos.copy(camera.position);

    const lookDir = this.dollLookTarget.clone().sub(this.startCameraPos).normalize();
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
