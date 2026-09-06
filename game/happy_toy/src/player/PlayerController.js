// 1인칭 플레이어 이동, 스태미나, 발소리, 아이템 투척/사용, 시점 제어를 담당하는 모듈입니다.

import * as THREE from "three";
import { PLAYER_CONFIG } from "../config/gameConfig.js";
import { clamp, direction2D, smoothStep } from "../utils/math.js";
import { soundManager } from "../audio/SoundManager.js";

export class PlayerController {
  constructor(camera, input, collisionWorld, hud) {
    this.camera = camera;
    this.input = input;
    this.collisionWorld = collisionWorld;
    this.hud = hud;
    this.position = new THREE.Vector3();
    this.yaw = 0;
    this.pitch = 0;
    this.interactables = [];
    this.interactionContext = {};
    this.currentInteractable = null;
    this.isHidden = false;
    this.hiddenCabinet = null;
    this.isMoving = false;
    this.isSprinting = false;
    this.cameraY = PLAYER_CONFIG.height;
    this.mouseSensitivity = PLAYER_CONFIG.mouseSensitivity;
    this.stuckTimer = 0;
    this.noclip = false;

    // Stamina & Speed Boost
    this.stamina = 1.0; // 0.0 ~ 1.0
    this.staminaExhausted = false;
    this.speedBoostTimer = 0;
    this.speedBoostMultiplier = 1.0;

    // View bobbing & Camera tilt
    this.bobTimer = 0;
    this.currentBobSpeed = 8;
    this.currentBobAmount = 0.02;
    this.tiltAngle = 0;
    this.bobBlend = 0;
    this.breathPhase = 0;
    this.cameraLateralOffset = 0;
    this.baseFov = camera.fov;
    this.currentFov = camera.fov;
    this.inventoryHudInitialized = false;
  }

  setPosition(position) {
    this.position.copy(position);
    this.collisionWorld.snapToValidSurface(this.position, { actorId: "player" });
    this.cameraY = this.position.y + PLAYER_CONFIG.height;
    this.camera.position.set(this.position.x, this.position.y + PLAYER_CONFIG.height, this.position.z);
    this.stamina = 1;
    this.staminaExhausted = false;
    this.speedBoostTimer = 0;
    this.speedBoostMultiplier = 1;
    this.bobBlend = 0;
    this.hud?.setStamina(1);
  }

  resetLook(yaw = 0, pitch = 0) {
    this.yaw = yaw;
    this.pitch = pitch;
  }

  setLookAt(target) {
    const direction = new THREE.Vector3().subVectors(target, this.camera.position);
    if (direction.lengthSq() <= 0.0001) {
      return;
    }
    direction.normalize();
    this.yaw = Math.atan2(-direction.x, -direction.z);
    this.pitch = Math.asin(direction.y);
    this.camera.rotation.set(this.pitch, this.yaw, 0, "YXZ");
  }

  setInteractables(interactables, context) {
    this.interactables = interactables;
    this.interactionContext = context;
    const inventory = context?.game?.itemSystem?.inventory;
    if (inventory) {
      this.hud?.updateInventory(inventory);
      this.inventoryHudInitialized = true;
    }
  }

  setMouseSensitivity(value) {
    this.mouseSensitivity = value;
  }

  restoreStamina(amount) {
    this.stamina = Math.min(1.0, this.stamina + amount);
    if (this.stamina > 0.2) {
      this.staminaExhausted = false;
    }
    if (this.hud) this.hud.setStamina(this.stamina);
  }

  applySpeedBoost(duration, multiplier) {
    this.speedBoostTimer = duration;
    this.speedBoostMultiplier = multiplier;
  }

  getPosition() {
    return this.position;
  }

  getForwardVector() {
    const dir = new THREE.Vector3(0, 0, -1);
    dir.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
    return dir;
  }

  enterCabinet(cabinet) {
    this.isHidden = true;
    this.hiddenCabinet = cabinet;
    this.input.consumePointerDelta();
    this.applyCabinetView();
    soundManager.playSFX("cabinet_enter");
  }

  exitCabinet() {
    const cabinet = this.hiddenCabinet;
    this.isHidden = false;
    this.hiddenCabinet = null;

    if (cabinet) {
      const forward = cabinet.getForwardDirection();
      this.position.copy(cabinet.getExitPosition());
      this.collisionWorld.snapToValidSurface(this.position, { actorId: "player" });
      this.yaw = yawForCameraForward(forward);
      this.pitch = 0;
    }

    this.cameraY = this.position.y + PLAYER_CONFIG.height;
    this.camera.position.set(this.position.x, this.cameraY, this.position.z);
    this.camera.rotation.set(this.pitch, this.yaw, 0, "YXZ");
    if (cabinet) {
      soundManager.playSFX("door_open");
    }
  }

  update(deltaTime) {
    if (this.isHidden) {
      this.isMoving = false;
      this.isSprinting = false;

      // Resting inside cabinet steadily recovers stamina
      const regenMult = this.staminaRegenMultiplier || 1.0;
      this.stamina = Math.min(1.0, this.stamina + 0.25 * deltaTime * regenMult);
      if (this.stamina > 0.2) {
        this.staminaExhausted = false;
      }
      if (this.hud) {
        this.hud.setStamina(this.stamina);
      }
      soundManager.updatePlayerState(deltaTime, {
        stamina: this.stamina,
        isHidden: true,
      });

      this.updateHiddenInteraction();
      this.applyCabinetView();
      return;
    }

    this.updateLook();
    this.updateStaminaAndItemHotkeys(deltaTime);
    this.updateMovement(deltaTime);
    this.updateInteraction();
    this.updateCamera(deltaTime);
    soundManager.updatePlayerState(deltaTime, {
      stamina: this.stamina,
      isMoving: this.isMoving,
      isSprinting: this.isSprinting,
    });
  }

  updateStaminaAndItemHotkeys(deltaTime) {
    // Speed boost timer count
    if (this.speedBoostTimer > 0) {
      this.speedBoostTimer -= deltaTime;
      if (this.speedBoostTimer <= 0) {
        this.speedBoostMultiplier = 1.0;
      }
    }

    // Item Quick Hotkeys
    const itemSystem = this.interactionContext?.game?.itemSystem;
    if (itemSystem) {
      if (!this.inventoryHudInitialized) {
        this.hud?.updateInventory(itemSystem.inventory);
        this.inventoryHudInitialized = true;
      }
      if (this.input.consumePressed("q")) {
        itemSystem.useItem("firecracker", this, this.interactionContext?.game?.flashlightController);
      } else if (this.input.consumePressed("1")) {
        itemSystem.useItem("battery", this, this.interactionContext?.game?.flashlightController);
      } else if (this.input.consumePressed("2")) {
        itemSystem.useItem("energy_drink", this, this.interactionContext?.game?.flashlightController);
      } else if (this.input.consumePressed("3")) {
        itemSystem.useItem("firecracker", this, this.interactionContext?.game?.flashlightController);
      } else if (this.input.consumePressed("4")) {
        itemSystem.useItem("compass", this, this.interactionContext?.game?.flashlightController);
      }
    }

    // Stamina logic
    const regenMult = this.staminaRegenMultiplier || 1.0;
    const wantsSprint = this.input.isDown("shift");

    if (this.isSprinting && this.isMoving) {
      this.stamina = Math.max(0, this.stamina - 0.22 * deltaTime);
      if (this.stamina <= 0) {
        this.staminaExhausted = true;
      }
    } else {
      this.stamina = Math.min(1.0, this.stamina + 0.18 * deltaTime * regenMult);
      // To prevent jittery stutter when holding shift with exhausted stamina,
      // require either releasing shift or recovering substantial stamina.
      if (!wantsSprint && this.stamina > 0.15) {
        this.staminaExhausted = false;
      } else if (this.stamina > 0.45) {
        this.staminaExhausted = false;
      }
    }

    if (this.hud) {
      this.hud.setStamina(this.stamina);
    }
  }

  updateCamera(deltaTime) {
    const targetY = this.position.y + PLAYER_CONFIG.height;
    this.cameraY = deltaTime > 0
      ? smoothStep(this.cameraY, targetY, PLAYER_CONFIG.verticalCameraSmoothness, deltaTime)
      : targetY;

    // Weighty but restrained head movement. Fatigue adds breathing sway instead
    // of a screen shake, keeping the game comfortable over long sessions.
    const reducedMotion = document.body.classList.contains("reduced-motion");
    const targetBlend = this.isMoving && !reducedMotion ? 1 : 0;
    this.bobBlend = smoothStep(this.bobBlend, targetBlend, targetBlend ? 9 : 6, deltaTime);
    const fatigue = 1 - this.stamina;
    const targetBobSpeed = this.isSprinting ? 13.2 : 8.6;
    const targetBobAmount = reducedMotion ? 0 : this.isSprinting ? 0.034 : 0.018;
    this.currentBobSpeed = smoothStep(this.currentBobSpeed, targetBobSpeed, 7, deltaTime);
    this.currentBobAmount = smoothStep(this.currentBobAmount, targetBobAmount, 7, deltaTime);
    this.bobTimer += deltaTime * this.currentBobSpeed * Math.max(0.15, this.bobBlend);
    this.breathPhase += deltaTime * (1.25 + fatigue * 1.5);

    const stepLift = Math.abs(Math.sin(this.bobTimer)) * this.currentBobAmount * this.bobBlend;
    const footDrop = -this.currentBobAmount * 0.38 * this.bobBlend;
    const breathing = reducedMotion ? 0 : Math.sin(this.breathPhase) * (0.0015 + fatigue * 0.0045);
    const lateralTarget = reducedMotion ? 0 : Math.sin(this.bobTimer * 0.5) * this.currentBobAmount * 0.42 * this.bobBlend;
    this.cameraLateralOffset = smoothStep(this.cameraLateralOffset, lateralTarget, 12, deltaTime);
    const rightX = Math.cos(this.yaw);
    const rightZ = -Math.sin(this.yaw);
    this.camera.position.set(
      this.position.x + rightX * this.cameraLateralOffset,
      this.cameraY + stepLift + footDrop + breathing,
      this.position.z + rightZ * this.cameraLateralOffset,
    );

    // Small roll and FOV expansion communicate speed without covering the HUD.
    const strafe = Number(this.input.isDown("d", "arrowright")) - Number(this.input.isDown("a", "arrowleft"));
    const headRoll = reducedMotion ? 0 : Math.sin(this.bobTimer * 0.5) * 0.007 * this.bobBlend;
    const targetTilt = reducedMotion ? 0 : -strafe * 0.018 + headRoll;
    this.tiltAngle = smoothStep(this.tiltAngle, targetTilt, 12, deltaTime);
    this.camera.rotation.set(this.pitch, this.yaw, this.tiltAngle, "YXZ");

    const targetFov = this.baseFov + (this.isSprinting && !reducedMotion ? 2.4 : 0) + (this.speedBoostMultiplier > 1 && !reducedMotion ? 1.2 : 0);
    this.currentFov = smoothStep(this.currentFov, targetFov, 6, deltaTime);
    if (Math.abs(this.camera.fov - this.currentFov) > 0.01) {
      this.camera.fov = this.currentFov;
      this.camera.updateProjectionMatrix();
    }
  }

  applyCabinetView() {
    if (!this.hiddenCabinet) {
      return;
    }

    const view = this.hiddenCabinet.getInsideView();
    this.camera.position.copy(view.position);
    this.camera.lookAt(view.lookAt);
  }

  updateLook() {
    const delta = this.input.consumePointerDelta();
    this.yaw -= delta.x * this.mouseSensitivity;
    this.pitch = clamp(
      this.pitch - delta.y * this.mouseSensitivity,
      -Math.PI * 0.46,
      Math.PI * 0.46,
    );
  }

  updateMovement(deltaTime) {
    const game = window.__happyToy;
    const isSafeMode = !!(game && game.testSafeMode);

    if (this.noclip && !isSafeMode) {
      this.noclip = false;
      this.position.y = this.collisionWorld.getGroundY(this.position, { allowAnyFloor: true });
      this.cameraY = this.position.y + PLAYER_CONFIG.height;
    }
    this.noclip = isSafeMode;

    if (this.noclip) {
      const up = this.input.isDown(" ", "space") ? 1 : 0;
      const down = this.input.isDown("control", "ctrl", "c") ? 1 : 0;
      const verticalMove = up - down;
      if (verticalMove !== 0) {
        const verticalSpeed = this.input.isDown("shift") ? PLAYER_CONFIG.sprintSpeed : PLAYER_CONFIG.walkSpeed;
        this.position.y += verticalMove * verticalSpeed * deltaTime;
      }
    }

    const forward = Number(this.input.isDown("w", "arrowup")) - Number(this.input.isDown("s", "arrowdown"));
    const strafe = Number(this.input.isDown("d", "arrowright")) - Number(this.input.isDown("a", "arrowleft"));
    this.isMoving = forward !== 0 || strafe !== 0;

    // Sprinting check with stamina exhaustion
    const wantsSprint = this.input.isDown("shift");
    this.isSprinting = this.isMoving && wantsSprint && !this.staminaExhausted && this.stamina > 0.05;

    if (!this.isMoving) {
      return;
    }

    const move = new THREE.Vector3(strafe, 0, -forward);
    move.normalize();
    move.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);

    let speed = this.isSprinting ? PLAYER_CONFIG.sprintSpeed : PLAYER_CONFIG.walkSpeed;
    speed *= this.speedBoostMultiplier;

    // Soul Gathering (영혼집합소) flooded canal check (South sector z: [6, 26], x: [-24, 24])
    const inFloodedCanal = this.position.y <= 0.25 && this.position.z >= 6.0 && this.position.z <= 26.0 && Math.abs(this.position.x) <= 24.0;
    if (inFloodedCanal) {
      speed *= 0.88; // subtle water drag
    }

    const previousPosition = this.position.clone();
    this.position.addScaledVector(move, speed * deltaTime);

    if (this.noclip) {
      this.stuckTimer = 0;
    } else {
      this.collisionWorld.resolveCircle(this.position, PLAYER_CONFIG.radius);
      const result = this.collisionWorld.resolveActorPosition(
        previousPosition,
        this.position,
        PLAYER_CONFIG.radius,
        { actorId: "player" },
      );
      if (!result.allowed) {
        this.isMoving = false;
      }
    }

    // Play footstep audio (Water splash in flooded canals or when Y < -0.5)
    if (inFloodedCanal || this.position.y < -0.5) {
      soundManager.playWaterStep(this.isSprinting);
    } else {
      soundManager.playFootstep(this.isSprinting);
    }


    // Stuck check and unstuck teleport logic
    if (!this.noclip) {
      const movedDist = previousPosition.distanceTo(this.position);
      const expectedDist = speed * deltaTime;
      if (this.isMoving && movedDist < expectedDist * 0.1) {
        this.stuckTimer += deltaTime;
        if (this.stuckTimer > 1.2) {
          const cx = Math.floor((this.position.x + 8) / 16);
          const cz = Math.floor((this.position.z + 8) / 16);
          const groundY = this.collisionWorld.getGroundY(
            { x: cx * 16, y: this.position.y, z: cz * 16 },
            { allowAnyFloor: true }
          );
          this.position.set(cx * 16, groundY, cz * 16);
          this.collisionWorld.snapToValidSurface(this.position, { actorId: "player-unstuck" });
          this.stuckTimer = 0;
          this.hud.setStatus("끼임 방지: 복도 중앙으로 복귀했습니다.", 1800);
        }
      } else {
        this.stuckTimer = 0;
      }
    }
  }

  updateInteraction() {
    this.currentInteractable = this.findInteractable();
    if (this.currentInteractable) {
      this.hud.setPrompt(this.currentInteractable.getPrompt(this.interactionContext));
    } else {
      this.hud.setPrompt("");
    }

    if (this.currentInteractable && this.input.consumePressed("e")) {
      this.currentInteractable.interact(this.interactionContext);
    }
  }

  updateHiddenInteraction() {
    const prompt = this.interactionContext.getHiddenPrompt?.(this.hiddenCabinet) || "E - 캐비넷에서 나오기";
    this.hud.setPrompt(prompt);

    if (!this.input.consumePressed("e")) {
      return;
    }

    if (this.interactionContext.canExitCabinet?.() === false) {
      this.hud.setStatus("문틈 너머에서 숨소리가 지나가길 기다립니다.", 1000);
      return;
    }

    this.interactionContext.exitCabinet?.();
  }

  findInteractable() {
    const lookDirection = new THREE.Vector3(0, 0, -1).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
    let bestInteractable = null;
    let bestDistance = Infinity;

    for (const interactable of this.interactables) {
      if (interactable.isInteractable?.(this.interactionContext) === false) {
        continue;
      }

      const distance = interactable.distanceTo
        ? interactable.distanceTo(this.position)
        : Math.hypot(interactable.position.x - this.position.x, interactable.position.z - this.position.z);
      if (distance > PLAYER_CONFIG.interactDistance || distance >= bestDistance) {
        continue;
      }

      const direction = direction2D(this.position, interactable.position);
      const facingTarget = lookDirection.dot(direction) > 0.12;
      if (!facingTarget) {
        continue;
      }

      bestInteractable = interactable;
      bestDistance = distance;
    }

    return bestInteractable;
  }
}

function yawForCameraForward(direction) {
  return Math.atan2(-direction.x, -direction.z);
}
