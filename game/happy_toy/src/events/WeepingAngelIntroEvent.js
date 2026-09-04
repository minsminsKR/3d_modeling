import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D } from "../utils/math.js";

function easeInOut(t) {
  return t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
}

function shortestAngleDelta(from, to) {
  return Math.atan2(Math.sin(to - from), Math.cos(to - from));
}

export class WeepingAngelIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
    this.turnSoundPlayed = false;

    this.triggerPosition = new THREE.Vector3(-14.0, 0.0, 0.0);
    this.triggerRadius = 3.5;
    this.mannequinLookTarget = new THREE.Vector3(-22.0, 1.2, 0.0);
    this.glideEndCameraPos = new THREE.Vector3(-18.2, 1.4, 0.0);

    this.startCameraPos = new THREE.Vector3();
    this.startYaw = 0;
    this.startPitch = 0;
    this.targetYaw = 0;
    this.targetPitch = 0;

    this.mannequinMesh = null;
    this.mannequinStartYaw = -Math.PI / 2; // Facing west (back to player)
    this.mannequinTargetYaw = Math.PI / 2;  // Facing east (directly looking at player)
  }

  get blocksPlayerControl() {
    return this.isControlLocked;
  }

  getMannequinMesh() {
    if (this.mannequinMesh && this.mannequinMesh.parent) {
      return this.mannequinMesh;
    }
    const direct = this.game.scene.getObjectByName("silent-mannequin-1f")
      || this.game.scene.getObjectByName("chunk_-1_0_silent_mannequin_intro");
    if (direct) {
      this.mannequinMesh = direct;
      return direct;
    }

    if (this.game.mapBuilder) {
      for (const chunk of this.game.mapBuilder.loadedChunks.values()) {
        for (const mesh of chunk.meshes) {
          if (mesh.userData && mesh.userData.isWeepingAngel && mesh.userData.weepingAngelState) {
            if (mesh.name.includes("1f") || mesh.position.x < -18.0) {
              this.mannequinMesh = mesh;
              return mesh;
            }
          }
        }
      }
    }
    return null;
  }

  update(deltaTime) {
    if (this.state === "done") return;

    if (this.state === "idle") {
      this.checkTrigger();
      return;
    }

    if (this.state === "cutscene") {
      this.timer += deltaTime;
      this.updateCutscene(deltaTime);
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
    this.turnSoundPlayed = false;

    const mannequin = this.getMannequinMesh();
    if (mannequin) {
      mannequin.rotation.y = this.mannequinStartYaw; // Ensure starting with back facing player
    }

    this.game.hud?.setStatus("복도 끝 조명 아래 누군가 등을 돌리고 서 있습니다...", 2200);

    const camera = this.game.camera;
    const player = this.game.player;

    player.input.consumePointerDelta();
    this.startCameraPos.copy(camera.position);
    this.startYaw = player.yaw;
    this.startPitch = player.pitch;

    // Calculate target yaw/pitch to frame mannequin
    const lookDir = this.mannequinLookTarget.clone().sub(this.glideEndCameraPos).normalize();
    const rawTargetYaw = Math.atan2(-lookDir.x, -lookDir.z);
    this.targetYaw = this.startYaw + shortestAngleDelta(this.startYaw, rawTargetYaw);
    this.targetPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));
  }

  updateCutscene(deltaTime) {
    const camera = this.game.camera;
    const player = this.game.player;
    const mannequin = this.getMannequinMesh();

    const glideDuration = 0.65;
    const turnStart = 0.65;
    const turnDuration = 0.85;
    const turnEnd = turnStart + turnDuration; // 1.50s
    const holdEnd = 1.70;
    const totalDuration = 2.20;

    // Phase 1: Camera Glides Forward to zoom in on the mannequin's back (0.0s -> 0.65s)
    if (this.timer <= glideDuration) {
      const t = easeInOut(Math.min(1, this.timer / glideDuration));
      camera.position.lerpVectors(this.startCameraPos, this.glideEndCameraPos, t);

      const curYaw = THREE.MathUtils.lerp(this.startYaw, this.targetYaw, t);
      const curPitch = THREE.MathUtils.lerp(this.startPitch, this.targetPitch, t);
      camera.rotation.set(curPitch, curYaw, 0, "YXZ");
      player.resetLook(curYaw, curPitch);

      if (mannequin) {
        mannequin.rotation.y = this.mannequinStartYaw;
      }
      return;
    }

    // Phase 2: Dramatic 180-degree Body/Head Turn to face the player (0.65s -> 1.50s)
    if (this.timer <= turnEnd) {
      camera.position.copy(this.glideEndCameraPos);
      camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
      player.resetLook(this.targetYaw, this.targetPitch);

      if (!this.turnSoundPlayed) {
        this.turnSoundPlayed = true;
        soundManager.playSFX("mannequin_creak");
        this.game.hud?.setStatus("마네킹이 천천히 고개를 돌려 당신을 응시합니다... 시선을 떼지 마세요!", 3500);
      }

      const turnT = easeInOut(Math.min(1, (this.timer - turnStart) / turnDuration));
      if (mannequin) {
        mannequin.rotation.y = THREE.MathUtils.lerp(this.mannequinStartYaw, this.mannequinTargetYaw, turnT);
      }

      // Slight camera tremor as the uncanny turn reaches completion
      if (turnT > 0.7) {
        const shake = (Math.random() - 0.5) * 0.012 * (1 - turnT);
        camera.position.y += shake;
      }
      return;
    }

    // Phase 2b: Dramatic Eye-Contact Freeze (1.50s -> 1.70s)
    if (this.timer <= holdEnd) {
      camera.position.copy(this.glideEndCameraPos);
      camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
      player.resetLook(this.targetYaw, this.targetPitch);

      if (mannequin) {
        mannequin.rotation.y = this.mannequinTargetYaw;
      }
      return;
    }

    // Phase 3: Smooth Camera Glide back to player (1.70s -> 2.20s)
    if (this.timer <= totalDuration) {
      const retT = easeInOut(Math.min(1, (this.timer - holdEnd) / (totalDuration - holdEnd)));
      camera.position.lerpVectors(this.glideEndCameraPos, this.startCameraPos, retT);

      camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
      player.resetLook(this.targetYaw, this.targetPitch);

      if (mannequin) {
        mannequin.rotation.y = this.mannequinTargetYaw;
      }
      return;
    }

    // Cutscene Complete: Unlock control and activate Weeping Angel behavior
    this.releaseControl();
    this.state = "done";
  }

  releaseControl() {
    this.isControlLocked = false;
    const mannequin = this.getMannequinMesh();
    if (mannequin) {
      mannequin.rotation.y = this.mannequinTargetYaw;
      if (mannequin.userData && mannequin.userData.weepingAngelState) {
        mannequin.userData.weepingAngelState.active = true;
      }
    }

    const camera = this.game.camera;
    const player = this.game.player;
    camera.position.copy(this.startCameraPos);
    player.input.consumePointerDelta();
    player.resetLook(this.targetYaw, this.targetPitch);
    camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
  }

  reset() {
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
    this.turnSoundPlayed = false;
    const mannequin = this.getMannequinMesh();
    if (mannequin) {
      mannequin.rotation.y = this.mannequinStartYaw;
    }
  }
}
