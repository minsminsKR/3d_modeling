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
    this.objectiveLine = document.querySelector("#objective-line");
    this.objectiveProgress = document.querySelector("#objective-progress");
    this.staminaContainer = document.querySelector("#stamina-vital");
    this.flashlightBatteryFill = document.querySelector("#flashlight-bar-fill");
    this.flashlightBatteryState = document.querySelector("#flashlight-state");
    this.reduceMotionInput = document.querySelector("#reduce-motion");
    this.highContrastInput = document.querySelector("#high-contrast");

    this.qtyBattery = document.querySelector("#qty-battery");
    this.qtyDrink = document.querySelector("#qty-drink");
    this.qtyFirecracker = document.querySelector("#qty-firecracker");
    this.qtyCompass = document.querySelector("#qty-compass");
    this.ghostBadge = document.querySelector("#ghost-badge");
    this.clickToPlayButton = document.querySelector("#click-to-play");

    this.statusTimer = null;
    this.compassActive = false;
    this.lastKeyCount = -1;
    this.setupAccessibilityPreferences();
  }

  setPrompt(text) {
    if (this.promptElement) {
      this.promptElement.textContent = text;
      this.promptElement.classList.toggle("is-visible", Boolean(text));
    }
    document.body.classList.toggle("interaction-ready", Boolean(text));
  }

  setGhostMode(enabled) {
    document.body.classList.toggle("ghost-mode", Boolean(enabled));
    if (this.ghostBadge) {
      this.ghostBadge.hidden = !enabled;
    }
  }

  setDread(amount, message, phase) {
    document.documentElement.style.setProperty("--dread", Math.max(0, Math.min(1, amount)).toFixed(3));
    const label = document.querySelector("#dread-state");
    if (label && label.textContent !== message) label.textContent = message;
    document.body.dataset.dreadPhase = phase;
  }

  setStatus(text, timeout = 0) {
    window.clearTimeout(this.statusTimer);
    if (this.statusElement) {
      this.statusElement.textContent = text;
      this.statusElement.classList.toggle("is-visible", Boolean(text));
      if (timeout > 0) {
        this.statusTimer = window.setTimeout(() => {
          this.statusElement.textContent = "";
          this.statusElement.classList.remove("is-visible");
        }, timeout);
      }
    }
  }

  setThreat(amount) {
    const clamped = Math.max(0, Math.min(1, amount));
    if (this.threatElement) {
      this.threatElement.style.opacity = String(clamped);
    }
    document.documentElement.style.setProperty("--threat", clamped.toFixed(3));
    document.body.classList.toggle("danger-close", clamped > 0.46);
  }

  setStamina(ratio) {
    if (this.staminaFill) {
      const pct = Math.max(0, Math.min(100, ratio * 100));
      this.staminaFill.style.width = `${pct}%`;
      this.staminaFill.setAttribute("aria-valuenow", String(Math.round(pct)));
      this.staminaFill.classList.toggle("low", ratio < 0.22);
      this.staminaContainer?.classList.toggle("is-active", ratio < 0.985);
    }
    document.documentElement.style.setProperty("--fatigue", String(1 - Math.max(0, Math.min(1, ratio))));
    document.body.classList.toggle("player-exhausted", ratio < 0.12);
  }

  setKeyCount(collected, total = 4) {
    if (this.keyCountText) {
      this.keyCountText.textContent = `${collected} / ${total}`;
    }
    if (this.objectiveLine) {
      this.objectiveLine.textContent = collected >= total
        ? "첫 복도의 제단함에 이름을 바치십시오"
        : `흩어진 이름을 찾으십시오 · ${total - collected}개 남음`;
    }
    if (this.objectiveProgress) {
      this.objectiveProgress.style.width = `${total > 0 ? (collected / total) * 100 : 0}%`;
    }
    if (collected !== this.lastKeyCount) {
      document.querySelector("#key-counter")?.classList.add("just-updated");
      window.setTimeout(() => document.querySelector("#key-counter")?.classList.remove("just-updated"), 900);
      this.lastKeyCount = collected;
    }
    document.body.classList.toggle("objective-complete", total > 0 && collected >= total);
    document.body.classList.toggle("names-returning", total > 0 && collected >= total);
  }

  updateInventory(inv) {
    if (!inv) return;
    const values = {
      battery: inv.battery || 0,
      drink: inv.energy_drink || 0,
      firecracker: inv.firecracker || 0,
      compass: inv.compass || 0,
    };
    if (this.qtyBattery) this.qtyBattery.textContent = values.battery;
    if (this.qtyDrink) this.qtyDrink.textContent = values.drink;
    if (this.qtyFirecracker) this.qtyFirecracker.textContent = values.firecracker;
    if (this.qtyCompass) this.qtyCompass.textContent = values.compass;
    document.querySelectorAll(".hotbar-slot").forEach((slot) => {
      const item = slot.dataset.item;
      const count = values[item] || 0;
      slot.classList.toggle("is-empty", count <= 0);
      slot.setAttribute("aria-label", `${slot.querySelector(".item-name")?.textContent || item} ${count}개`);
    });
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

  updateCompass(playerPos, targetPos, playerYaw, label = "이름") {
    if (!this.compassActive || !this.compassNeedle || !playerPos) return;
    this.compassNeedle.style.visibility = targetPos ? "visible" : "hidden";
    if (!targetPos) {
      if (this.compassText) this.compassText.textContent = "기척이 끊겼습니다 · 아직 열지 않은 교실을 살피십시오";
      return;
    }

    const dx = targetPos.x - playerPos.x;
    const dz = targetPos.z - playerPos.z;
    const angleToTarget = Math.atan2(dx, -dz);
    const relativeAngle = angleToTarget + playerYaw;

    const deg = (relativeAngle * 180) / Math.PI;
    this.compassNeedle.style.transform = `rotate(${deg}deg)`;

    const dist = Math.hypot(dx, dz);
    if (this.compassText) {
      const height = targetPos.y - playerPos.y;
      const floor = height > 1.8 ? "위층 ↑ · 계단참을 찾으십시오" : height < -1.8 ? "아래층 ↓ · 계단참을 찾으십시오" : "같은 층";
      this.compassText.textContent = `${label} · ${dist.toFixed(0)}m · ${floor}`;
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

  setFlashlightBattery(ratio, enabled) {
    this.setFlashlightEnabled(enabled);
    const clamped = Math.max(0, Math.min(1, ratio));
    if (this.flashlightBatteryFill) {
      this.flashlightBatteryFill.style.width = `${clamped * 100}%`;
      this.flashlightBatteryFill.classList.toggle("low", clamped < 0.2);
      this.flashlightBatteryFill.setAttribute("aria-valuenow", String(Math.round(clamped * 100)));
    }
    if (this.flashlightBatteryState) {
      this.flashlightBatteryState.textContent = enabled ? "켜짐" : "꺼짐";
    }
  }

  setupAccessibilityPreferences() {
    let savedReduceMotion = null;
    let savedHighContrast = null;
    try {
      savedReduceMotion = localStorage.getItem("happy_toy_reduce_motion");
      savedHighContrast = localStorage.getItem("happy_toy_high_contrast");
    } catch (_) {}
    const reduceMotion = savedReduceMotion === null
      ? window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true
      : savedReduceMotion === "1";
    const highContrast = savedHighContrast === "1";
    const apply = () => {
      document.body.classList.toggle("reduced-motion", Boolean(this.reduceMotionInput?.checked));
      document.body.classList.toggle("high-contrast", Boolean(this.highContrastInput?.checked));
    };
    if (this.reduceMotionInput) {
      this.reduceMotionInput.checked = reduceMotion;
      this.reduceMotionInput.addEventListener("change", () => {
        apply();
        try { localStorage.setItem("happy_toy_reduce_motion", this.reduceMotionInput.checked ? "1" : "0"); } catch (_) {}
      });
    }
    if (this.highContrastInput) {
      this.highContrastInput.checked = highContrast;
      this.highContrastInput.addEventListener("change", () => {
        apply();
        try { localStorage.setItem("happy_toy_high_contrast", this.highContrastInput.checked ? "1" : "0"); } catch (_) {}
      });
    }
    apply();
  }

  hideStart() {
    if (this.startScreen) this.startScreen.classList.add("hidden");
  }

  showClickToPlay(label = "클릭하면 손전등이 켜집니다.") {
    if (!this.clickToPlayButton) {
      return;
    }
    this.clickToPlayButton.hidden = false;
    this.clickToPlayButton.textContent = label;
  }

  hideClickToPlay() {
    if (this.clickToPlayButton) {
      this.clickToPlayButton.hidden = true;
    }
  }

  showStart() {
    if (this.startScreen) this.startScreen.classList.remove("hidden");
  }

  showCaught(message = "복도가 당신의 이름을 외웠습니다.") {
    if (this.caughtScreen) {
      const title = this.caughtScreen.querySelector("h2");
      if (title) {
        title.textContent = message;
      }
      this.caughtScreen.classList.add("is-caught");
      this.caughtScreen.classList.remove("hidden");
    }
    document.body.classList.add("caught-open");
    this.setThreat(1);
  }

  hideCaught() {
    if (this.caughtScreen) {
      this.caughtScreen.classList.add("hidden");
      this.caughtScreen.classList.remove("is-caught");
    }
    document.body.classList.remove("caught-open");
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
      this.startButton.textContent = `${chapter.title || "그림자복도"} 시작`;
    }
  }

  showClear(options = {}) {
    if (this.clearScreen) {
      const title = this.clearScreen.querySelector("h2");
      const text = this.clearScreen.querySelector("p:not(.eyebrow)");
      if (title) {
        title.textContent = options.title || "제단이 문을 삼켰습니다.";
      }
      if (text) {
        text.textContent = options.description || options.message || "손전등이 꺼져도 복도는 남습니다.";
      }
      this.clearScreen.classList.add("is-clear");
      this.clearScreen.classList.remove("hidden");
    }
    document.body.classList.add("clear-open");
  }

  hideClear() {
    if (this.clearScreen) {
      this.clearScreen.classList.add("hidden");
      this.clearScreen.classList.remove("is-clear");
    }
    document.body.classList.remove("clear-open");
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
