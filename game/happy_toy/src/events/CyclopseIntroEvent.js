import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D } from "../utils/math.js";

function shortestAngleDelta(from, to) {
  return Math.atan2(Math.sin(to - from), Math.cos(to - from));
}

function easeInOut(t) {
  return t * t * (3 - 2 * t);
}

export class CyclopseIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle"; // "idle", "cutscene", "done"
    this.timer = 0;
    this.isControlLocked = false;

    // Trigger position in East corridor leading to Chunk (1, 0) / (2, 0)
    this.triggerPosition = new THREE.Vector3(14.0, 0.0, 0.0);
    this.triggerRadius = 3.5;

    // Cinematic targets & positions
    this.hiddenMonsterPos = new THREE.Vector3(27.5, 0.0, -4.5);
    this.corridorMonsterPos = new THREE.Vector3(27.5, 0.0, 0.0);
    this.glideEndCameraPos = new THREE.Vector3(23.5, 1.4, 0.0);
    this.cornerFramingTarget = new THREE.Vector3(27.5, 1.4, 0.0);

    this.startCameraPos = new THREE.Vector3();
    this.startYaw = 0;
    this.startPitch = 0;
    this.targetYaw = 0;
    this.targetPitch = 0;

    this.footstepThudPlayed1 = false;
    this.footstepThudPlayed2 = false;
    this.footstepThudPlayed3 = false;
    this.roarPlayed = false;
    this.monsterInitialized = false;
  }

  get blocksPlayerControl() {
    return this.isControlLocked;
  }

  getEnemy() {
    if (!this.game.enemyManager?.enemies) return null;
    return this.game.enemyManager.enemies.find(e => e.config.id === "cyclopse") || null;
  }

  initMonsterPosition() {
    if (this.monsterInitialized) return;
    const cyclopse = this.getEnemy();
    if (cyclopse) {
      cyclopse.group.position.copy(this.hiddenMonsterPos);
      cyclopse.group.rotation.y = 0;
      cyclopse.setDormant(true);
      this.monsterInitialized = true;
    }
  }

  update(deltaTime) {
    if (this.state === "done") return;

    if (this.state === "idle") {
      this.initMonsterPosition();
      this.checkTrigger();
      return;
    }

    if (this.state === "cutscene") {
      this.timer += deltaTime;
      this.updateCutscene(deltaTime);

      if (this.timer >= 3.45) {
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
    this.footstepThudPlayed1 = false;
    this.footstepThudPlayed2 = false;
    this.footstepThudPlayed3 = false;
    this.roarPlayed = false;

    this.game.hud?.setStatus("거대한 외눈박이 괴물이 코너 뒤에서 다가옵니다! 캐비넷으로 숨으세요!", 3200);

    // Awaken Cyclopse behind the corner wall
    const cyclopse = this.getEnemy();
    if (cyclopse) {
      cyclopse.setDormant(false);
      cyclopse.group.visible = true;
      cyclopse.group.position.copy(this.hiddenMonsterPos);
      cyclopse.group.rotation.y = 0;
      cyclopse.state = "cutscene";
      cyclopse.playAction("patrol", 0.1);
    }

    // Capture starting camera & player state
    const camera = this.game.camera;
    const player = this.game.player;
    player.input.consumePointerDelta();
    this.startCameraPos.copy(camera.position);

    this.startYaw = player.yaw;
    this.startPitch = player.pitch;

    // Calculate target yaw looking from glideEndCameraPos to cornerFramingTarget
    const lookDir = this.cornerFramingTarget.clone().sub(this.glideEndCameraPos).normalize();
    const rawTargetYaw = Math.atan2(-lookDir.x, -lookDir.z);
    this.targetYaw = this.startYaw + shortestAngleDelta(this.startYaw, rawTargetYaw);
    this.targetPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));
  }

  updateCutscene(deltaTime) {
    const camera = this.game.camera;
    const player = this.game.player;
    const cyclopse = this.getEnemy();

    // -------------------------------------------------------------
    // Phase 1: Camera Glide down the corridor (0.0s -> 0.85s)
    // -------------------------------------------------------------
    if (this.timer < 0.85) {
      const rawT = Math.min(1, this.timer / 0.85);
      const t = easeInOut(rawT);

      const curPos = new THREE.Vector3().lerpVectors(this.startCameraPos, this.glideEndCameraPos, t);
      camera.position.copy(curPos);

      const curYaw = THREE.MathUtils.lerp(this.startYaw, this.targetYaw, t);
      const curPitch = THREE.MathUtils.lerp(this.startPitch, this.targetPitch, t);
      camera.rotation.set(curPitch, curYaw, 0, "YXZ");
      player.resetLook(curYaw, curPitch);

      // Keep cyclopse hidden behind the corner
      if (cyclopse) {
        cyclopse.group.position.copy(this.hiddenMonsterPos);
        cyclopse.group.rotation.y = 0;
      }
      return;
    }

    // -------------------------------------------------------------
    // Phase 2: Corner Emergence (0.85s -> 2.15s)
    // -------------------------------------------------------------
    if (this.timer >= 0.85 && this.timer < 2.15) {
      const phaseTimer = this.timer - 0.85;
      const rawT = Math.min(1, phaseTimer / 1.30);
      const t = easeInOut(rawT);

      // Camera holds steady at corridor end framing corner
      camera.position.copy(this.glideEndCameraPos);
      camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
      player.resetLook(this.targetYaw, this.targetPitch);

      // Footstep sound triggers
      if (!this.footstepThudPlayed1 && phaseTimer >= 0.0) {
        this.footstepThudPlayed1 = true;
        soundManager.playSFX("heavy_thud");
      }
      if (!this.footstepThudPlayed2 && phaseTimer >= 0.50) {
        this.footstepThudPlayed2 = true;
        soundManager.playSFX("heavy_thud");
      }
      if (!this.footstepThudPlayed3 && phaseTimer >= 1.05) {
        this.footstepThudPlayed3 = true;
        soundManager.playSFX("heavy_thud");
      }

      // Cyclopse steps out into corridor center
      if (cyclopse) {
        cyclopse.group.position.lerpVectors(this.hiddenMonsterPos, this.corridorMonsterPos, t);

        // Turn to face west (yaw = -Math.PI / 2) as it arrives in corridor center
        const turnProgress = Math.max(0, Math.min(1, (rawT - 0.35) / 0.65));
        const turnT = easeInOut(turnProgress);
        cyclopse.group.rotation.y = THREE.MathUtils.lerp(0, -Math.PI / 2, turnT);
      }
      return;
    }

    // -------------------------------------------------------------
    // Phase 3: Shock & Smooth Camera Return (2.15s -> 3.45s)
    // -------------------------------------------------------------
    if (this.timer >= 2.15) {
      const phaseTimer = this.timer - 2.15;
      const rawT = Math.min(1, phaseTimer / 1.30);
      const t = easeInOut(rawT);

      // Shock trigger
      if (!this.roarPlayed) {
        this.roarPlayed = true;
        soundManager.playMonsterRoar("cyclopse");
        soundManager.playSFX("heavy_thud");
        if (cyclopse) {
          cyclopse.playAction("chase", 0.15);
        }
      }

      // Cyclopse remains at corridor center facing west
      if (cyclopse) {
        cyclopse.group.position.copy(this.corridorMonsterPos);
        cyclopse.group.rotation.y = -Math.PI / 2;
      }

      // Camera shake (decays over first 0.55s of Phase 3)
      let shakeX = 0;
      let shakeY = 0;
      if (phaseTimer < 0.55) {
        const shakeDecay = (1 - phaseTimer / 0.55) * 0.055;
        shakeX = Math.sin(phaseTimer * 48.0) * shakeDecay;
        shakeY = Math.cos(phaseTimer * 38.0) * shakeDecay;
      }

      // Camera smoothly glides back to player's eye position
      const returnPos = new THREE.Vector3().lerpVectors(this.glideEndCameraPos, this.startCameraPos, t);
      returnPos.x += shakeX;
      returnPos.y += shakeY;
      camera.position.copy(returnPos);

      // Frame the monster from the moving camera position
      const lookDir = this.cornerFramingTarget.clone().sub(returnPos).normalize();
      const rawCurYaw = Math.atan2(-lookDir.x, -lookDir.z);
      const curYaw = this.targetYaw + shortestAngleDelta(this.targetYaw, rawCurYaw);
      const curPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));

      camera.rotation.set(curPitch, curYaw, 0, "YXZ");
      player.resetLook(curYaw, curPitch);
    }
  }

  releaseControl() {
    this.isControlLocked = false;
    const cyclopse = this.getEnemy();
    if (cyclopse) {
      cyclopse.state = "chase";
      cyclopse.lastKnownPlayerPosition = this.game.player.position.clone();
      cyclopse.memoryTimer = cyclopse.config.memorySeconds;
      cyclopse.playAction("chase", 0.2);
    }

    this.game.player.input.consumePointerDelta();
    // Ensure camera is cleanly aligned with player's eyes
    const player = this.game.player;
    cameraPosFromPlayer(this.game.camera, player);
  }

  reset() {
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
    this.footstepThudPlayed1 = false;
    this.footstepThudPlayed2 = false;
    this.footstepThudPlayed3 = false;
    this.roarPlayed = false;
    this.monsterInitialized = false;

    const cyclopse = this.getEnemy();
    if (cyclopse) {
      cyclopse.group.position.copy(this.hiddenMonsterPos);
      cyclopse.group.rotation.y = 0;
      cyclopse.setDormant(true);
    }
  }
}

function cameraPosFromPlayer(camera, player) {
  if (!player || !camera) return;
  const eyeY = player.cameraY || (player.position.y + 1.68);
  camera.position.set(player.position.x, eyeY, player.position.z);
  camera.rotation.set(player.pitch, player.yaw, 0, "YXZ");
}
