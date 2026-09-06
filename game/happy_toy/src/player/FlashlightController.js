// 플레이어 손전등의 on/off 상태, 배터리 잔량, 깜빡임 연출을 관리하는 모듈입니다.

import { soundManager } from "../audio/SoundManager.js";

export class FlashlightController {
  constructor(spotLight, input, hud, game = null) {
    this.spotLight = spotLight;
    this.input = input;
    this.hud = hud;
    this.game = game;
    this.enabled = false;
    this.defaultIntensity = spotLight.intensity;
    this.currentIntensity = 0;
    this.batteryLevel = 1.0; // 0.0 ~ 1.0
    this.flickerTimer = 0;
    this.voltageNoise = 0;
    this.nextVoltageDip = 2.4 + Math.random() * 4;
    this.voltageDipTimer = 0;
    this.lowBatteryWarned = false;
    this.applyState(false);
  }

  update(deltaTime = 0.016) {
    if (this.input.consumePressed("f")) {
      this.toggle();
    }

    if (this.enabled) {
      // Slow battery drain
      const drainMult = this.drainMultiplier || 1.0;
      this.batteryLevel = Math.max(0, this.batteryLevel - 0.003 * deltaTime * drainMult);

      // Low battery and nearby danger create slow voltage instability. This is
      // intentionally a sag, not a high-frequency strobe.
      const playerPos = this.game?.player?.position;
      const monsterDist = this.game ? this.game.getMinMonsterDistance(playerPos) : Infinity;
      const proximity = Math.max(0, Math.min(1, 1 - monsterDist / 10));
      const lowBattery = Math.max(0, Math.min(1, (0.24 - this.batteryLevel) / 0.24));
      const reducedMotion = document.body.classList.contains("reduced-motion");
      this.flickerTimer += deltaTime * (1.35 + proximity * 1.4);
      this.nextVoltageDip -= deltaTime;
      if (!reducedMotion && this.nextVoltageDip <= 0 && (lowBattery > 0.05 || proximity > 0.15)) {
        this.voltageDipTimer = 0.08 + Math.random() * 0.11;
        this.nextVoltageDip = 2.1 + Math.random() * 4.8 - proximity * 1.2;
      }
      this.voltageDipTimer = Math.max(0, this.voltageDipTimer - deltaTime);
      const slowWaver = reducedMotion ? 1 : 0.965 + Math.sin(this.flickerTimer) * (0.012 + proximity * 0.025 + lowBattery * 0.03);
      const dip = this.voltageDipTimer > 0 ? 0.68 - proximity * 0.08 : 1;
      const batteryOutput = 0.72 + Math.sqrt(this.batteryLevel) * 0.28;
      const targetIntensity = this.defaultIntensity * batteryOutput * slowWaver * dip;
      const blend = 1 - Math.exp(-deltaTime * (this.voltageDipTimer > 0 ? 20 : 9));
      this.currentIntensity += (targetIntensity - this.currentIntensity) * blend;
      this.spotLight.intensity = this.currentIntensity;

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
    }
    this.hud.setFlashlightBattery?.(this.batteryLevel, this.enabled);
  }

  rechargeBattery(amount = 1.0) {
    this.batteryLevel = Math.min(1.0, this.batteryLevel + amount);
    this.lowBatteryWarned = false;
    if (!this.enabled && this.batteryLevel > 0) {
      this.enabled = true;
    }
    this.applyState(true);
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
    this.lowBatteryWarned = false;
    this.applyState(false);
  }

  applyState(showMessage) {
    this.spotLight.visible = true;
    this.currentIntensity = this.enabled ? this.defaultIntensity * (0.72 + Math.sqrt(this.batteryLevel) * 0.28) : 0;
    this.spotLight.intensity = this.currentIntensity;
    this.hud.setFlashlightEnabled(this.enabled);
    this.hud.setFlashlightBattery?.(this.batteryLevel, this.enabled);

    if (!showMessage) {
      return;
    }

    const message = this.enabled ? "후레쉬를 켰습니다." : "후레쉬를 껐습니다.";
    this.hud.setStatus(message, 1200);
  }
}
