// 화면 위 HUD 텍스트와 공포 연출 레이어, 스태미나, 인벤토리, 나침반을 관리하는 모듈입니다.

export class Hud {
  constructor() {
    this.promptElement = document.querySelector("#interaction-prompt");
    this.statusElement = document.querySelector("#status-line");
    this.threatElement = document.querySelector("#threat-meter");
    this.floorDebugElement = document.querySelector("#floor-debug");
    this.startScreen = document.querySelector("#start-screen");
    this.caughtScreen = document.querySelector("#caught-screen");
    this.clearScreen = document.querySelector("#clear-screen");
    this.pauseScreen = document.querySelector("#pause-screen");
    this.startEyebrow = document.querySelector("#start-eyebrow");
    this.startTitle = document.querySelector("#start-title");
    this.startDescription = document.querySelector("#start-description");
    this.startButton = document.querySelector("#start-button");
    this.restartButton = document.querySelector("#restart-button");
    this.clearRestartButton = document.querySelector("#clear-restart-button");
    this.resumeButton = document.querySelector("#resume-button");
    this.pauseRestartButton = document.querySelector("#pause-restart-button");
    this.quitButton = document.querySelector("#quit-button");
    this.mouseSensitivityInput = document.querySelector("#mouse-sensitivity");
    this.mouseSensitivityValue = document.querySelector("#mouse-sensitivity-value");

    // New HUD elements
    this.staminaFill = document.querySelector("#stamina-bar-fill");
    this.keyCountText = document.querySelector("#key-count-text");
    this.compassWidget = document.querySelector("#compass-widget");
    this.compassNeedle = document.querySelector("#compass-needle");
    this.compassText = document.querySelector("#compass-target-text");

    this.qtyBattery = document.querySelector("#qty-battery");
    this.qtyDrink = document.querySelector("#qty-drink");
    this.qtyFirecracker = document.querySelector("#qty-firecracker");
    this.qtyCompass = document.querySelector("#qty-compass");

    this.statusTimer = null;
    this.compassActive = false;
  }

  setPrompt(text) {
    if (this.promptElement) {
      this.promptElement.textContent = text;
    }
  }

  setStatus(text, timeout = 0) {
    window.clearTimeout(this.statusTimer);
    if (this.statusElement) {
      this.statusElement.textContent = text;
      if (timeout > 0) {
        this.statusTimer = window.setTimeout(() => {
          this.statusElement.textContent = "복도 어딘가에서 발소리가 들립니다.";
        }, timeout);
      }
    }
  }

  setThreat(amount) {
    const clamped = Math.max(0, Math.min(1, amount));
    if (this.threatElement) {
      this.threatElement.style.opacity = String(clamped);
    }
  }

  setStamina(ratio) {
    if (this.staminaFill) {
      const pct = Math.max(0, Math.min(100, ratio * 100));
      this.staminaFill.style.width = `${pct}%`;
      if (ratio < 0.2) {
        this.staminaFill.classList.add("low");
      } else {
        this.staminaFill.classList.remove("low");
      }
    }
  }

  setKeyCount(collected, total = 4) {
    if (this.keyCountText) {
      this.keyCountText.textContent = `${collected} / ${total}`;
    }
  }

  updateInventory(inv) {
    if (!inv) return;
    if (this.qtyBattery) this.qtyBattery.textContent = `x${inv.battery || 0}`;
    if (this.qtyDrink) this.qtyDrink.textContent = `x${inv.energy_drink || 0}`;
    if (this.qtyFirecracker) this.qtyFirecracker.textContent = `x${inv.firecracker || 0}`;
    if (this.qtyCompass) this.qtyCompass.textContent = `x${inv.compass || 0}`;
  }

  toggleCompass() {
    this.compassActive = !this.compassActive;
    if (this.compassWidget) {
      if (this.compassActive) {
        this.compassWidget.classList.remove("hidden");
      } else {
        this.compassWidget.classList.add("hidden");
      }
    }
  }

  updateCompass(playerPos, targetPos, playerYaw) {
    if (!this.compassActive || !this.compassNeedle || !playerPos || !targetPos) return;

    const dx = targetPos.x - playerPos.x;
    const dz = targetPos.z - playerPos.z;
    const angleToTarget = Math.atan2(dx, -dz);
    const relativeAngle = angleToTarget - playerYaw;

    const deg = (relativeAngle * 180) / Math.PI;
    this.compassNeedle.style.transform = `rotate(${deg}deg)`;

    const dist = Math.hypot(dx, dz);
    if (this.compassText) {
      this.compassText.textContent = `열쇠 감지: ${dist.toFixed(0)}m`;
    }
  }

  setStartEnabled(enabled, label = null) {
    if (!this.startButton) {
      return;
    }
    this.startButton.disabled = !enabled;
    if (label) {
      this.startButton.textContent = label;
    }
  }

  setFlashlightEnabled(enabled) {
    document.body.classList.toggle("flashlight-off", !enabled);
  }

  hideStart() {
    if (this.startScreen) this.startScreen.classList.add("hidden");
  }

  showStart() {
    if (this.startScreen) this.startScreen.classList.remove("hidden");
  }

  showCaught(message = "발소리가 바로 뒤에서 멈췄습니다.") {
    if (this.caughtScreen) {
      const title = this.caughtScreen.querySelector("h2");
      if (title) {
        title.textContent = message;
      }
      this.caughtScreen.classList.remove("hidden");
    }
    this.setThreat(1);
  }

  hideCaught() {
    if (this.caughtScreen) this.caughtScreen.classList.add("hidden");
    this.setThreat(0);
  }

  setChapterInfo(chapter) {
    if (this.startEyebrow) {
      this.startEyebrow.textContent = chapter.eyebrow || `Chapter ${chapter.id}`;
    }
    if (this.startTitle) {
      this.startTitle.textContent = chapter.title;
    }
    if (this.startDescription) {
      this.startDescription.textContent = chapter.description || "";
    }
    if (this.startButton) {
      this.startButton.textContent = `${chapter.eyebrow || `Chapter ${chapter.id}`} 시작`;
    }
  }

  showClear(options = {}) {
    if (this.clearScreen) {
      const title = this.clearScreen.querySelector("h2");
      const text = this.clearScreen.querySelector("p:not(.eyebrow)");
      if (title && options.title) {
        title.textContent = options.title;
      }
      if (text && options.description) {
        text.textContent = options.description;
      }
      this.clearScreen.classList.remove("hidden");
    }
  }

  hideClear() {
    if (this.clearScreen) this.clearScreen.classList.add("hidden");
  }

  showPause() {
    if (this.pauseScreen) this.pauseScreen.classList.remove("hidden");
  }

  hidePause() {
    if (this.pauseScreen) this.pauseScreen.classList.add("hidden");
  }

  setMouseSensitivityDisplay(value) {
    if (this.mouseSensitivityValue) {
      this.mouseSensitivityValue.textContent = Number(value).toFixed(2);
    }
  }

  setDebugEnabled(enabled) {

    if (this.floorDebugElement) {
      this.floorDebugElement.style.display = enabled ? "block" : "none";
    }
  }

  updateDebug(text) {
    if (this.floorDebugElement) {
      this.floorDebugElement.textContent = text;
    }
  }
}
