import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D } from "../utils/math.js";

function shortestAngleDelta(from, to) {
  return Math.atan2(Math.sin(to - from), Math.cos(to - from));
}

function easeInOut(t) {
  return t * t * (3 - 2 * t);
}

export class UncatIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle"; // "idle", "cutscene", "done"
    this.timer = 0;
    this.isControlLocked = false;

    // Trigger in South corridor leading towards Chunk (0, 1) cross junction
    this.triggerPosition = new THREE.Vector3(0.0, 0.0, 14.0);
    this.triggerRadius = 3.5;

    // Cinematic targets & positions
    this.hiddenMonsterPos = new THREE.Vector3(4.5, 0.0, 25.0);
    this.corridorMonsterPos = new THREE.Vector3(0.0, 0.0, 25.0);
    this.glideEndCameraPos = new THREE.Vector3(0.0, 1.4, 19.5);
    this.intersectionFramingTarget = new THREE.Vector3(0.0, 1.4, 25.0);

    this.startCameraPos = new THREE.Vector3();
    this.startYaw = 0;
    this.startPitch = 0;
    this.targetYaw = 0;
    this.targetPitch = 0;

    this.originalFlashlightIntensity = 11.5;
    this.catSfxPlayed = false;
    this.blackoutPlayed = false;
    this.monsterInitialized = false;
  }

  get blocksPlayerControl() {
    return this.isControlLocked;
  }

  getEnemy() {
    if (!this.game.enemyManager?.enemies) return null;
    return this.game.enemyManager.enemies.find(e => e.config.id === "uncat") || null;
  }

  initMonsterPosition() {
    if (this.monsterInitialized) return;
    const uncat = this.getEnemy();
    if (uncat) {
      uncat.group.position.copy(this.hiddenMonsterPos);
      uncat.group.rotation.y = -Math.PI / 2; // Facing West towards intersection
      uncat.setDormant(true);
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

      if (this.timer >= 3.55) {
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
    this.catSfxPlayed = false;
    this.blackoutPlayed = false;

    this.originalFlashlightIntensity = this.game.flashlight?.intensity ?? 11.5;

    this.game.hud?.setStatus("기괴한 고양이 괴물이 복도의 조명을 삼키며 나타났습니다!", 3200);

    // Awaken Uncat behind the cross wall
    const uncat = this.getEnemy();
    if (uncat) {
      uncat.setDormant(false);
      uncat.group.visible = true;
      uncat.group.position.copy(this.hiddenMonsterPos);
      uncat.group.rotation.y = -Math.PI / 2; // Facing West
      uncat.state = "cutscene";
      uncat.playAction("patrol", 0.1);
    }

    // Capture starting camera & player state
    const camera = this.game.camera;
    const player = this.game.player;
    player.input.consumePointerDelta();
    this.startCameraPos.copy(camera.position);

    this.startYaw = player.yaw;
    this.startPitch = player.pitch;

    // Calculate target yaw looking from glideEndCameraPos to intersectionFramingTarget (South, +Z)
    const lookDir = this.intersectionFramingTarget.clone().sub(this.glideEndCameraPos).normalize();
    const rawTargetYaw = Math.atan2(-lookDir.x, -lookDir.z); // Math.PI for South (+Z)
    this.targetYaw = this.startYaw + shortestAngleDelta(this.startYaw, rawTargetYaw);
    this.targetPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));
  }

  updateCutscene(deltaTime) {
    const camera = this.game.camera;
    const player = this.game.player;
    const uncat = this.getEnemy();

    // -------------------------------------------------------------
    // Phase 1: Camera Glide down South corridor (0.0s -> 0.85s)
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

      // Keep uncat hidden behind the cross wall
      if (uncat) {
        uncat.group.position.copy(this.hiddenMonsterPos);
        uncat.group.rotation.y = -Math.PI / 2;
      }
      return;
    }

    // -------------------------------------------------------------
    // Phase 2: Corner Emergence, Contortion & Rapid Light Flicker (0.85s -> 2.25s)
    // -------------------------------------------------------------
    if (this.timer >= 0.85 && this.timer < 2.25) {
      const phaseTimer = this.timer - 0.85;
      const rawT = Math.min(1, phaseTimer / 1.40);
      const t = easeInOut(rawT);

      // Camera holds steady at corridor end framing intersection
      camera.position.copy(this.glideEndCameraPos);
      camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
      player.resetLook(this.targetYaw, this.targetPitch);

      // Distorted eerie cat SFX
      if (!this.catSfxPlayed) {
        this.catSfxPlayed = true;
        soundManager.playSFX("cat_eerie");
      }

      // Rapid erratic lighting flicker
      const flickerNoise = Math.sin(phaseTimer * 42.0) + Math.sin(phaseTimer * 78.0) * 0.6;
      const isDim = flickerNoise < -0.15;
      const flickerMult = isDim ? (0.06 + Math.random() * 0.12) : (0.75 + Math.random() * 0.30);
      this.applyLightingFlicker(flickerMult);

      // Uncat creeps out from around the corner into intersection center
      if (uncat) {
        uncat.group.position.lerpVectors(this.hiddenMonsterPos, this.corridorMonsterPos, t);

        // Contort and turn to face North towards camera (yaw = -Math.PI)
        const turnProgress = Math.max(0, Math.min(1, (rawT - 0.30) / 0.70));
        const turnT = easeInOut(turnProgress);
        uncat.group.rotation.y = THREE.MathUtils.lerp(-Math.PI / 2, -Math.PI, turnT);

        // Uncanny twitch & contortion
        if (uncat.modelRoot) {
          uncat.modelRoot.rotation.z = Math.sin(phaseTimer * 14.0) * 0.16;
          uncat.modelRoot.rotation.x = Math.cos(phaseTimer * 10.0) * 0.12;
        }
        if (uncat.mixer) {
          uncat.mixer.timeScale = 1.3;
        }
      }
      return;
    }

    // -------------------------------------------------------------
    // Phase 3: Blackout Sting & Smooth Camera Return (2.25s -> 3.55s)
    // -------------------------------------------------------------
    if (this.timer >= 2.25) {
      const phaseTimer = this.timer - 2.25;
      const rawT = Math.min(1, phaseTimer / 1.30);
      const t = easeInOut(rawT);

      // Quick blackout sting at start of phase 3
      if (!this.blackoutPlayed) {
        this.blackoutPlayed = true;
        soundManager.playSFX("screamer_jumpscare");
        soundManager.playMonsterRoar("uncat");
        this.game.glitchController?.trigger({ strength: 1.6, full: true, firstDetection: true });
      }

      // Complete blackout for the first 0.22s of phase 3, then restore lights
      if (phaseTimer < 0.22) {
        this.applyLightingFlicker(0.0);
      } else {
        const recoverT = Math.min(1, (phaseTimer - 0.22) / 0.4);
        const residualFlicker = (0.7 + Math.random() * 0.3) * recoverT;
        this.applyLightingFlicker(residualFlicker);
      }

      // Normalize Uncat contortion pose
      if (uncat) {
        uncat.group.position.copy(this.corridorMonsterPos);
        uncat.group.rotation.y = -Math.PI; // North facing
        if (uncat.modelRoot) {
          uncat.modelRoot.rotation.set(0, 0, 0);
        }
        if (uncat.mixer) {
          uncat.mixer.timeScale = 1.0;
        }
      }

      // Camera smoothly glides back to player's eye position
      const returnPos = new THREE.Vector3().lerpVectors(this.glideEndCameraPos, this.startCameraPos, t);
      camera.position.copy(returnPos);

      // Maintain framing on intersection from moving camera
      const lookDir = this.intersectionFramingTarget.clone().sub(returnPos).normalize();
      const rawCurYaw = Math.atan2(-lookDir.x, -lookDir.z);
      const curYaw = this.targetYaw + shortestAngleDelta(this.targetYaw, rawCurYaw);
      const curPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));

      camera.rotation.set(curPitch, curYaw, 0, "YXZ");
      player.resetLook(curYaw, curPitch);
    }
  }

  applyLightingFlicker(multiplier) {
    // Flashlight flicker
    if (this.game.flashlight) {
      this.game.flashlight.intensity = this.originalFlashlightIntensity * multiplier;
    }

    // SafeLights in map
    for (const safeLight of this.game.safeLights || []) {
      safeLight.setFlickerState?.(multiplier);
    }

    // Ceiling point lights in pool
    if (this.game._pointLightPool) {
      for (const pl of this.game._pointLightPool) {
        if (pl && pl.position.y > -9000) {
          pl.intensity = Math.max(0, pl.intensity * multiplier);
        }
      }
    }
  }

  restoreLighting() {
    if (this.game.flashlight) {
      this.game.flashlight.intensity = this.originalFlashlightIntensity;
    }
    for (const safeLight of this.game.safeLights || []) {
      safeLight.setFlickerState?.(1.0);
    }
  }

  releaseControl() {
    this.isControlLocked = false;
    this.restoreLighting();

    const uncat = this.getEnemy();
    if (uncat) {
      uncat.state = "wander";
      uncat.playAction("patrol", 0.2);
      if (uncat.modelRoot) {
        uncat.modelRoot.rotation.set(0, 0, 0);
      }
      if (uncat.mixer) {
        uncat.mixer.timeScale = 1.0;
      }
    }

    this.game.player.input.consumePointerDelta();
    // Cleanly re-align camera with player eye position
    const player = this.game.player;
    cameraPosFromPlayer(this.game.camera, player);
  }

  reset() {
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
    this.catSfxPlayed = false;
    this.blackoutPlayed = false;
    this.monsterInitialized = false;

    this.restoreLighting();

    const uncat = this.getEnemy();
    if (uncat) {
      uncat.group.position.copy(this.hiddenMonsterPos);
      uncat.group.rotation.y = -Math.PI / 2;
      if (uncat.modelRoot) {
        uncat.modelRoot.rotation.set(0, 0, 0);
      }
      if (uncat.mixer) {
        uncat.mixer.timeScale = 1.0;
      }
      uncat.setDormant(true);
    }
  }
}

function cameraPosFromPlayer(camera, player) {
  if (!player || !camera) return;
  const eyeY = player.cameraY || (player.position.y + 1.68);
  camera.position.set(player.position.x, eyeY, player.position.z);
  camera.rotation.set(player.pitch, player.yaw, 0, "YXZ");
}
