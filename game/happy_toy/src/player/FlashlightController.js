// 플레이어 손전등의 on/off 상태, 배터리 잔량, 위협 기반 깜빡임 연출을 관리하는 모듈입니다.

import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { LIGHTING_CONFIG } from "../config/gameConfig.js";

export class FlashlightController {
  constructor(spotLight, input, hud, game = null) {
    this.spotLight = spotLight;
    this.input = input;
    this.hud = hud;
    this.game = game;
    this.enabled = false;
    this.defaultIntensity = spotLight.intensity;
    this.currentIntensity = 0;
    this.batteryLevel = 1.0;
    this.flickerPhase = 0;
    this.strobePhase = 0;
    this.nextEventAt = 1.6 + Math.random() * 2.2;
    this.eventTimer = 0;
    this.eventKind = "none";
    this.outputScale = 0;
    this.smoothThreat = 0;
    this.soundCooldown = 0;
    this.lowBatteryWarned = false;
    this.healthyColor = new THREE.Color(LIGHTING_CONFIG.flashlightColor);
    this.dyingColor = new THREE.Color(0xff7a32);
    this.applyState(false);
  }

  update(deltaTime = 0.016) {
    if (this.input.consumePressed("f")) {
      this.toggle();
    }

    const dt = Math.min(deltaTime, 0.05);
    this.soundCooldown = Math.max(0, this.soundCooldown - dt);

    if (this.enabled) {
      const drainMult = this.drainMultiplier || 1.0;
      this.batteryLevel = Math.max(0, this.batteryLevel - 0.003 * dt * drainMult);

      const playerPos = this.game?.player?.position;
      const rawThreat = this.game?.getMonsterThreat?.(playerPos) ?? 0;
      this.smoothThreat += (rawThreat - this.smoothThreat) * (1 - Math.exp(-dt * 7));
      const lowBattery = Math.max(0, Math.min(1, (0.24 - this.batteryLevel) / 0.24));
      const threat = Math.max(this.smoothThreat, lowBattery * 0.42);
      const reducedMotion = document.body.classList.contains("reduced-motion");
      const scale = reducedMotion
        ? this.computeReducedMotionScale(threat)
        : this.computeHorrorScale(dt, threat);
      this.applyOutput(scale);

      if (this.batteryLevel < 0.15 && !this.lowBatteryWarned) {
        this.lowBatteryWarned = true;
        this.hud.setStatus("손전등 불빛이 약해지고 있습니다.", 1800);
      } else if (this.batteryLevel > 0.25) {
        this.lowBatteryWarned = false;
      }

      if (this.batteryLevel <= 0) {
        this.enabled = false;
        this.applyState(true);
        this.hud.setStatus("손전등 배터리가 방전되었습니다!", 1800);
      }
    } else {
      this.smoothThreat = 0;
      this.applyOutput(0);
    }
    this.hud.setFlashlightBattery?.(this.batteryLevel, this.enabled);
  }

  computeReducedMotionScale(threat) {
    return 1 - threat * 0.18;
  }

  computeHorrorScale(dt, threat) {
    this.flickerPhase += dt * (2.4 + threat * 22);
    this.strobePhase += dt * (9 + threat * 38);
    this.nextEventAt -= dt;
    this.eventTimer = Math.max(0, this.eventTimer - dt);

    if (this.nextEventAt <= 0) {
      this.beginThreatEvent(threat);
    }

    const grain = Math.sin(this.flickerPhase) * 0.55
      + Math.sin(this.flickerPhase * 2.73 + 1.1) * 0.3
      + Math.sin(this.flickerPhase * 6.1 + 0.4) * 0.15;
    const jitter = 1
      - threat * 0.1
      - threat * 0.22 * (0.5 + 0.5 * grain)
      - threat * threat * 0.16 * Math.abs(Math.sin(this.flickerPhase * 3.4));

    let eventScale = 1;
    if (this.eventTimer > 0) {
      if (this.eventKind === "blackout") {
        eventScale = 0.02 + Math.random() * 0.05;
      } else if (this.eventKind === "strobe") {
        const cutoff = 0.18 - threat * 0.22;
        const pulse = Math.sin(this.strobePhase * (1.6 + threat * 2.4));
        eventScale = pulse > cutoff ? 1.08 : (0.04 + Math.random() * 0.07);
      } else if (this.eventKind === "dip") {
        eventScale = 0.38 - threat * 0.18;
      }
    }

    return Math.max(0, jitter * eventScale);
  }

  beginThreatEvent(threat) {
    const roll = Math.random();
    if (threat < 0.12) {
      this.eventKind = "none";
      this.eventTimer = 0;
      this.nextEventAt = 1.8 + Math.random() * 3.2;
      return;
    }

    if (threat > 0.78) {
      this.eventKind = roll < 0.42 ? "blackout" : roll < 0.82 ? "strobe" : "dip";
    } else if (threat > 0.42) {
      this.eventKind = roll < 0.22 ? "blackout" : roll < 0.72 ? "strobe" : "dip";
    } else {
      this.eventKind = roll < 0.18 ? "strobe" : "dip";
    }

    if (this.eventKind === "blackout") {
      this.eventTimer = 0.07 + Math.random() * (0.08 + threat * 0.18);
    } else if (this.eventKind === "strobe") {
      this.eventTimer = 0.16 + Math.random() * (0.18 + threat * 0.38);
    } else {
      this.eventTimer = 0.09 + Math.random() * 0.14;
    }

    const gap = (1.7 - threat * 1.45) + Math.random() * (1.1 - threat * 0.95);
    this.nextEventAt = this.eventTimer + Math.max(0.05, gap);
    this.playFlickerSound(threat);
  }

  playFlickerSound(threat) {
    if (this.soundCooldown > 0 || this.eventKind === "none") {
      return;
    }
    this.soundCooldown = Math.max(0.05, 0.28 - threat * 0.22);
    soundManager.playSFX("flashlight_flicker");
  }

  applyOutput(scale) {
    this.outputScale = scale;
    const batteryOutput = 0.72 + Math.sqrt(this.batteryLevel) * 0.28;
    const cinematic = this.game?.cinematicLightScale ?? 1;
    this.currentIntensity = this.enabled
      ? this.defaultIntensity * batteryOutput * scale
      : 0;
    this.spotLight.intensity = this.currentIntensity * cinematic;
    if (this.game?.flashlightFill) {
      const fillBase = LIGHTING_CONFIG.flashlightFillIntensity ?? 4.6;
      this.game.flashlightFill.intensity = this.enabled
        ? fillBase * batteryOutput * scale * cinematic
        : 0;
    }
    const dying = this.enabled ? Math.max(0, 1 - scale) : 0;
    this.spotLight.color.copy(this.healthyColor).lerp(this.dyingColor, Math.min(1, dying * 1.35));
  }

  rechargeBattery(amount = 1.0) {
    this.batteryLevel = Math.min(1.0, this.batteryLevel + amount);
    this.lowBatteryWarned = false;
    if (!this.enabled && this.batteryLevel > 0) {
      this.enabled = true;
    }
    this.applyState(true);
  }

  setEnabled(enabled, showMessage = false) {
    if (enabled && this.batteryLevel <= 0) {
      return false;
    }
    this.enabled = Boolean(enabled);
    this.applyState(showMessage);
    return this.enabled;
  }

  toggle() {
    if (this.batteryLevel <= 0 && !this.enabled) {
      this.hud.setStatus("배터리가 없어 손전등을 켤 수 없습니다. (건전지 필요)", 1500);
      soundManager.playSFX("flashlight_toggle");
      return;
    }

    this.enabled = !this.enabled;
    soundManager.playSFX("flashlight_toggle");
    this.applyState(true);
  }

  reset() {
    this.enabled = false;
    this.batteryLevel = 1.0;
    this.currentIntensity = 0;
    this.outputScale = 0;
    this.smoothThreat = 0;
    this.eventTimer = 0;
    this.eventKind = "none";
    this.lowBatteryWarned = false;
    this.applyState(false);
  }

  applyState(showMessage) {
    this.spotLight.visible = true;
    this.applyOutput(this.enabled ? 1 : 0);
    this.hud.setFlashlightEnabled(this.enabled);
    this.hud.setFlashlightBattery?.(this.batteryLevel, this.enabled);

    if (!showMessage) {
      return;
    }

    const message = this.enabled ? "후레쉬를 켰습니다." : "후레쉬를 껐습니다.";
    this.hud.setStatus(message, 1200);
  }
}
