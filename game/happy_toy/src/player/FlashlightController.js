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
    this.batteryLevel = 1.0; // 0.0 ~ 1.0
    this.flickerTimer = 0;
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

      // Low battery or monster proximity tension
      const playerPos = this.game?.player?.position;
      const monsterDist = this.game ? this.game.getMinMonsterDistance(playerPos) : Infinity;
      if (this.batteryLevel < 0.15 || monsterDist < 9.0) {
        let freq = 2.5;
        let minDim = 0.55;
        if (monsterDist < 9.0) {
          // Close monster creates tense voltage sag without epileptic strobing
          const proximity = Math.min(1.0, Math.max(0.0, 1.0 - (monsterDist / 9.0)));
          freq = 1.8 + proximity * 2.8; // 1.8Hz to 4.6Hz natural waver
          minDim = 0.65 - proximity * 0.15; // gentle dip down to ~0.50
        }
        this.flickerTimer += deltaTime * freq;
        const wave = Math.sin(this.flickerTimer) * 0.5 + Math.sin(this.flickerTimer * 1.7) * 0.3;
        const normWave = (wave + 0.8) / 1.6; // ~[0, 1]
        const flicker = minDim + (1.0 - minDim) * THREE.MathUtils.clamp(normWave, 0, 1);
        this.spotLight.intensity = this.defaultIntensity * Math.min(this.batteryLevel, 1.0) * flicker;
      } else {
        this.spotLight.intensity = this.defaultIntensity * Math.min(this.batteryLevel, 1.0);
      }

      if (this.batteryLevel <= 0) {
        this.enabled = false;
        this.applyState(true);
        this.hud.setStatus("손전등 배터리가 방전되었습니다!", 1800);
      }
    }
  }

  rechargeBattery(amount = 1.0) {
    this.batteryLevel = Math.min(1.0, this.batteryLevel + amount);
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
    this.applyState(false);
  }

  applyState(showMessage) {
    this.spotLight.visible = true;
    this.spotLight.intensity = this.enabled ? this.defaultIntensity : 0;
    this.hud.setFlashlightEnabled(this.enabled);

    if (!showMessage) {
      return;
    }

    const message = this.enabled ? "후레쉬를 켰습니다." : "후레쉬를 껐습니다.";
    this.hud.setStatus(message, 1200);
  }
}
