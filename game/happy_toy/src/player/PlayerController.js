// 1인칭 플레이어 이동과 상호작용을 담당하는 모듈입니다.
// WASD 이동, 마우스 회전, Shift 달리기, E 상호작용, 캐비넷 내부 시점을 처리합니다.

import * as THREE from "three";
import { PLAYER_CONFIG } from "../config/gameConfig.js";
import { clamp, direction2D, smoothStep } from "../utils/math.js";

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
  }

  setPosition(position) {
    this.position.copy(position);
    this.collisionWorld.snapToValidSurface(this.position, { actorId: "player" });
    this.cameraY = this.position.y + PLAYER_CONFIG.height;
    this.camera.position.set(this.position.x, this.position.y + PLAYER_CONFIG.height, this.position.z);
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
  }

  setMouseSensitivity(value) {
    this.mouseSensitivity = value;
  }

  enterCabinet(cabinet) {
    this.isHidden = true;
    this.hiddenCabinet = cabinet;
    this.input.consumePointerDelta();
    this.applyCabinetView();
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
  }

  update(deltaTime) {
    if (this.isHidden) {
      this.isMoving = false;
      this.isSprinting = false;
      this.updateHiddenInteraction();
      this.applyCabinetView();
      return;
    }

    this.updateLook();
    this.updateMovement(deltaTime);
    this.updateInteraction();
    this.updateCamera(deltaTime);
    this.camera.rotation.set(this.pitch, this.yaw, 0, "YXZ");
  }

  updateCamera(deltaTime) {
    const targetY = this.position.y + PLAYER_CONFIG.height;
    this.cameraY = deltaTime > 0
      ? smoothStep(this.cameraY, targetY, PLAYER_CONFIG.verticalCameraSmoothness, deltaTime)
      : targetY;
    this.camera.position.set(this.position.x, this.cameraY, this.position.z);
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
    const forward = Number(this.input.isDown("w", "arrowup")) - Number(this.input.isDown("s", "arrowdown"));
    const strafe = Number(this.input.isDown("d", "arrowright")) - Number(this.input.isDown("a", "arrowleft"));
    this.isMoving = forward !== 0 || strafe !== 0;
    this.isSprinting = this.isMoving && this.input.isDown("shift");

    if (!this.isMoving) {
      return;
    }

    const move = new THREE.Vector3(strafe, 0, -forward);
    move.normalize();
    move.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);

    const speed = this.isSprinting ? PLAYER_CONFIG.sprintSpeed : PLAYER_CONFIG.walkSpeed;
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

    // Stuck check and unstuck teleport logic
    if (!this.noclip) {
      const movedDist = previousPosition.distanceTo(this.position);
      const expectedDist = speed * deltaTime;
      if (this.isMoving && movedDist < expectedDist * 0.1) {
        this.stuckTimer += deltaTime;
        if (this.stuckTimer > 1.2) {
          // Player is stuck! Teleport to center of current chunk
          const cx = Math.floor((this.position.x + 8) / 16);
          const cz = Math.floor((this.position.z + 8) / 16);
          this.position.set(cx * 16, 0, cz * 16);
          this.collisionWorld.snapToValidSurface(this.position, { actorId: "player-unstuck" });
          this.stuckTimer = 0;
          this.hud.setStatus("끼임 방지: 복도 중앙으로 복귀했습니다.", 1800);
          console.warn(`[PlayerController] Player stuck detected. Teleported to chunk center (${cx}, ${cz}).`);
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
