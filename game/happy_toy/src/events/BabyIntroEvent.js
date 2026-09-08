import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D, direction2D, yawFromDirection } from "../utils/math.js";

function shortestAngleDelta(from, to) {
  return Math.atan2(Math.sin(to - from), Math.cos(to - from));
}

function easeInOut(t) {
  return t * t * (3 - 2 * t);
}

const PHASES = {
  notice: 1.8,
  watch: 3.7,
  silence: 5.05,
  turn: 6.55,
  stare: 7.25,
  sting: 8.15,
  done: 8.85,
};

export class BabyIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
    this.duration = PHASES.done;

    this.triggerPosition = new THREE.Vector3(14.5, -5.0, 32.0);
    this.triggerRadius = 3.5;
    this.babyHome = new THREE.Vector3(10.5, -5.0, 30.0);
    this.babyLookTarget = new THREE.Vector3(10.5, -4.15, 30.0);

    this.startCameraPos = new THREE.Vector3();
    this.holdCameraPos = new THREE.Vector3();
    this.startYaw = 0;
    this.startPitch = 0;
    this.targetYaw = 0;
    this.targetPitch = 0;
    this.awayYaw = 0;
    this.faceYaw = 0;

    this.cryCut = false;
    this.turned = false;
    this.stingPlayed = false;
    this.ambientCry = false;
  }

  get blocksPlayerControl() {
    return this.isControlLocked;
  }

  getPhase() {
    if (this.state !== "cutscene") return this.state;
    const t = this.timer;
    if (t < PHASES.notice) return "notice";
    if (t < PHASES.watch) return "watch";
    if (t < PHASES.silence) return "silence";
    if (t < PHASES.turn) return "turn";
    if (t < PHASES.stare) return "stare";
    if (t < PHASES.sting) return "sting";
    return "gaslight";
  }

  getEnemy() {
    return this.game.enemyManager?.enemies.find((enemy) => (
      enemy.config.id === "baby-workshop" || enemy.config.id === "baby" || enemy.config.type === "baby"
    )) || null;
  }

  update(deltaTime) {
    if (this.state === "done") return;

    if (this.state === "idle") {
      this.updateDistantCry();
      this.checkTrigger();
      return;
    }

    if (this.state === "cutscene") {
      this.timer += deltaTime;
      this.updateCutscene(deltaTime);
      if (this.timer >= this.duration) {
        this.releaseControl();
        this.state = "done";
      }
    }
  }

  updateDistantCry() {
    const player = this.game.player;
    if (!player) return;
    const inBasement = Math.abs(player.position.y - this.triggerPosition.y) < 1.8;
    const distance = distance2D(player.position, this.babyHome);
    const hear = inBasement && distance < 22 && !player.isHidden;
    if (hear && !this.ambientCry) {
      this.ambientCry = true;
      soundManager.startBabyCry({ gain: 0.018, pan: -0.22 });
    }
    if (this.ambientCry) {
      const proximity = 1 - Math.min(1, Math.max(0, (distance - 3.5) / 18));
      soundManager.setBabyCryGain(hear ? 0.016 + proximity * 0.07 : 0.0001);
    }
    if (!hear && this.ambientCry) {
      this.ambientCry = false;
      soundManager.stopBabyCry({ abrupt: false });
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
    this.cryCut = false;
    this.turned = false;
    this.stingPlayed = false;
    this.game.cinematicLightScale = 0.42;

    const baby = this.getEnemy();
    const player = this.game.player;
    const camera = this.game.camera;

    if (baby) {
      baby.setDormant(false);
      baby.babyAwake = false;
      baby.state = "cutscene";
      baby.group.visible = true;
      baby.group.position.copy(this.babyHome);
      this.game.collisionWorld?.snapToValidSurface(baby.group.position, { actorId: baby.config.id });
      const away = direction2D(player.position, baby.group.position);
      const toward = direction2D(baby.group.position, player.position);
      this.awayYaw = yawFromDirection(away);
      this.faceYaw = yawFromDirection(toward);
      baby.group.rotation.y = this.awayYaw;
      baby.playAction("crying", 0.08);
      if (baby.mixer) baby.mixer.timeScale = 0.72;
    }

    soundManager.startBabyCry({ gain: 0.13, pan: -0.18 });
    this.ambientCry = true;
    this.game.hud?.setStatus("……", 1600);

    player.input.consumePointerDelta();
    this.startCameraPos.copy(camera.position);
    this.startYaw = player.yaw;
    this.startPitch = player.pitch;

    const lookDir = this.babyLookTarget.clone().sub(this.startCameraPos).normalize();
    const rawTargetYaw = Math.atan2(-lookDir.x, -lookDir.z);
    this.targetYaw = this.startYaw + shortestAngleDelta(this.startYaw, rawTargetYaw);
    this.targetPitch = Math.asin(THREE.MathUtils.clamp(lookDir.y, -1, 1));
    this.holdCameraPos.copy(this.startCameraPos).addScaledVector(lookDir, 1.15);
  }

  updateCutscene(deltaTime) {
    const camera = this.game.camera;
    const player = this.game.player;
    const baby = this.getEnemy();
    const phase = this.getPhase();
    const t = this.timer;

    if (baby && baby.state !== "cutscene") {
      baby.state = "cutscene";
      baby.babyAwake = false;
    }

    if (phase === "notice") {
      const rawT = Math.min(1, t / PHASES.notice);
      const eased = easeInOut(rawT);
      camera.position.lerpVectors(this.startCameraPos, this.holdCameraPos, eased);
      const yaw = THREE.MathUtils.lerp(this.startYaw, this.targetYaw, eased);
      const pitch = THREE.MathUtils.lerp(this.startPitch, this.targetPitch, eased);
      camera.rotation.set(pitch, yaw, 0, "YXZ");
      player.resetLook(yaw, pitch);
      this.game.cinematicLightScale = 0.42 - eased * 0.12;
      soundManager.setBabyCryGain(0.12 + eased * 0.05);
      if (baby) {
        baby.group.position.copy(this.babyHome);
        baby.group.rotation.y = this.awayYaw;
        if (baby.mixer) baby.mixer.timeScale = 0.7;
      }
      return;
    }

    camera.position.copy(this.holdCameraPos);
    camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
    player.resetLook(this.targetYaw, this.targetPitch);

    if (phase === "watch") {
      const local = (t - PHASES.notice) / (PHASES.watch - PHASES.notice);
      const breathe = Math.sin(t * 1.15) * 0.012;
      camera.position.x += breathe;
      camera.position.z += Math.cos(t * 0.9) * 0.008;
      this.game.cinematicLightScale = 0.28 + Math.max(0, Math.sin(t * 11.5)) * 0.05;
      soundManager.setBabyCryGain(0.16);
      if (baby?.mixer) baby.mixer.timeScale = 0.55;
      if (local > 0.55 && Math.random() < 0.02) {
        this.game.cinematicLightScale = 0.08;
      }
      return;
    }

    if (phase === "silence") {
      if (!this.cryCut) {
        this.cryCut = true;
        this.ambientCry = false;
        soundManager.stopBabyCry({ abrupt: true });
        soundManager.playSFX("baby_cry_cut");
        this.game.hud?.setStatus("", 0);
      }
      this.game.cinematicLightScale = 0.16;
      if (baby) {
        baby.group.rotation.y = this.awayYaw;
        if (baby.mixer) baby.mixer.timeScale = 0;
      }
      return;
    }

    if (phase === "turn") {
      const rawT = Math.min(1, (t - PHASES.silence) / (PHASES.turn - PHASES.silence));
      const eased = rawT * rawT * (3 - 2 * rawT);
      this.turned = true;
      this.game.cinematicLightScale = 0.14;
      if (baby) {
        baby.group.position.copy(this.babyHome);
        baby.group.rotation.y = this.awayYaw + shortestAngleDelta(this.awayYaw, this.faceYaw) * eased;
        if (baby.mixer) baby.mixer.timeScale = 0.08;
      }
      if (rawT > 0.35) {
        soundManager.setBabyCryGain(0);
      }
      return;
    }

    if (phase === "stare") {
      this.game.cinematicLightScale = 0.11 + Math.sin(t * 28) * 0.03;
      camera.position.add(new THREE.Vector3(
        (Math.random() - 0.5) * 0.01,
        (Math.random() - 0.5) * 0.008,
        (Math.random() - 0.5) * 0.01,
      ));
      if (baby) {
        baby.group.rotation.y = this.faceYaw;
        if (baby.mixer) baby.mixer.timeScale = 0;
      }
      return;
    }

    if (phase === "sting") {
      if (!this.stingPlayed) {
        this.stingPlayed = true;
        this.game.cinematicLightScale = 0;
        soundManager.playSFX("baby_wrong");
        soundManager.playMonsterRoar("baby");
        this.game.glitchController?.trigger({ strength: 1.7, full: true, firstDetection: true });
      }
      this.game.cinematicLightScale = t < PHASES.sting - 0.35 ? 0 : 0.08;
      const toward = direction2D(this.babyHome, player.position);
      if (baby) {
        baby.group.position.set(
          this.babyHome.x + toward.x * 1.45,
          this.babyHome.y,
          this.babyHome.z + toward.z * 1.45,
        );
        baby.group.rotation.y = this.faceYaw;
        if (baby.mixer) baby.mixer.timeScale = 1.6;
        baby.playAction("chase", 0.04);
      }
      camera.position.add(new THREE.Vector3(
        (Math.random() - 0.5) * 0.09,
        (Math.random() - 0.5) * 0.05,
        (Math.random() - 0.5) * 0.09,
      ));
      camera.rotation.set(this.targetPitch, this.targetYaw, 0, "YXZ");
      return;
    }

    // gaslight: it never happened
    this.game.cinematicLightScale = 0.55;
    if (baby) {
      baby.group.position.copy(this.babyHome);
      this.game.collisionWorld?.snapToValidSurface(baby.group.position, { actorId: baby.config.id });
      baby.group.rotation.y = this.awayYaw;
      baby.babyAwake = false;
      baby.state = "cutscene";
      baby.playAction("crying", 0.12);
      if (baby.mixer) baby.mixer.timeScale = 0.62;
    }
    if (!this.ambientCry) {
      this.ambientCry = true;
      soundManager.startBabyCry({ gain: 0.045, pan: -0.2 });
    }
    const returnT = Math.min(1, (t - PHASES.sting) / Math.max(0.01, this.duration - PHASES.sting));
    camera.position.lerpVectors(this.holdCameraPos, this.startCameraPos, easeInOut(returnT));
    const yaw = this.targetYaw + shortestAngleDelta(this.targetYaw, this.startYaw) * easeInOut(returnT);
    const pitch = THREE.MathUtils.lerp(this.targetPitch, this.startPitch, easeInOut(returnT));
    camera.rotation.set(pitch, yaw, 0, "YXZ");
    player.resetLook(yaw, pitch);
  }

  restoreBabyAfterIntro() {
    const baby = this.getEnemy();
    if (!baby) return;
    baby.group.position.copy(this.babyHome);
    this.game.collisionWorld?.snapToValidSurface(baby.group.position, { actorId: baby.config.id });
    baby.group.rotation.y = this.awayYaw || baby.group.rotation.y;
    baby.babyAwake = false;
    baby.caughtPlayer = false;
    baby.state = "crying";
    baby.setDormant(false);
    baby.playAction("crying", 0.2);
    if (baby.mixer) baby.mixer.timeScale = 1;
  }

  releaseControl() {
    this.isControlLocked = false;
    this.game.cinematicLightScale = 1;
    this.restoreBabyAfterIntro();
    soundManager.startBabyCry({ gain: 0.05, pan: -0.18 });
    this.ambientCry = true;
    this.game.player.input.consumePointerDelta();
    this.game.player.resetLook(this.startYaw, this.startPitch);
    this.game.camera.rotation.set(this.startPitch, this.startYaw, 0, "YXZ");
    this.game.hud?.setStatus("울음이 다시 들립니다. 손전등을 비추거나 뛰면 깨어납니다.", 4200);
  }

  reset() {
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
    this.cryCut = false;
    this.turned = false;
    this.stingPlayed = false;
    this.ambientCry = false;
    this.game.cinematicLightScale = 1;
    soundManager.stopBabyCry({ abrupt: true });
  }
}
