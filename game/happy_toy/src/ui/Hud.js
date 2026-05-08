// 화면 위 HUD 텍스트와 공포 연출 레이어를 관리하는 모듈입니다.
// 게임 로직은 DOM을 직접 만지지 않고 Hud 메서드로 상태만 전달합니다.

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
    this.startButton = document.querySelector("#start-button");
    this.restartButton = document.querySelector("#restart-button");
    this.clearRestartButton = document.querySelector("#clear-restart-button");
    this.resumeButton = document.querySelector("#resume-button");
    this.pauseRestartButton = document.querySelector("#pause-restart-button");
    this.quitButton = document.querySelector("#quit-button");
    this.mouseSensitivityInput = document.querySelector("#mouse-sensitivity");
    this.mouseSensitivityValue = document.querySelector("#mouse-sensitivity-value");
    this.statusTimer = null;
  }

  setPrompt(text) {
    this.promptElement.textContent = text;
  }

  setStatus(text, timeout = 0) {
    window.clearTimeout(this.statusTimer);
    this.statusElement.textContent = text;
    if (timeout > 0) {
      this.statusTimer = window.setTimeout(() => {
        this.statusElement.textContent = "복도 어딘가에서 발소리가 들립니다.";
      }, timeout);
    }
  }

  setThreat(amount) {
    const clamped = Math.max(0, Math.min(1, amount));
    this.threatElement.style.opacity = String(clamped);
  }

  setFlashlightEnabled(enabled) {
    document.body.classList.toggle("flashlight-off", !enabled);
  }

  hideStart() {
    this.startScreen.classList.add("hidden");
  }

  showStart() {
    this.startScreen.classList.remove("hidden");
  }

  showCaught(message = "발소리가 바로 뒤에서 멈췄습니다.") {
    const title = this.caughtScreen.querySelector("h2");
    if (title) {
      title.textContent = message;
    }
    this.caughtScreen.classList.remove("hidden");
    this.setThreat(1);
  }

  hideCaught() {
    this.caughtScreen.classList.add("hidden");
    this.setThreat(0);
  }

  showClear() {
    this.clearScreen.classList.remove("hidden");
    this.setThreat(0);
    this.setStatus("장난감 상자가 열리고 복도의 소리가 사라졌습니다.");
  }

  hideClear() {
    this.clearScreen.classList.add("hidden");
  }

  showPause() {
    this.pauseScreen.classList.remove("hidden");
    this.setPrompt("");
  }

  hidePause() {
    this.pauseScreen.classList.add("hidden");
  }

  setMouseSensitivityDisplay(value) {
    this.mouseSensitivityValue.textContent = value.toFixed(2);
  }

  setFloorDebug(debug) {
    if (!this.floorDebugElement || !debug) {
      return;
    }

    const drop = debug.lastDropAttempt;
    const dropText = drop
      ? `${drop.status} | ${drop.reason || "none"} | targetFloor=${drop.targetFloor ?? "none"} | landing=${drop.targetLandingId ?? "none"}`
      : "none";
    const monsterText = (debug.monsters || [])
      .map((monster) => {
        const target = monster.pathTarget
          ? `${monster.pathTarget.type}:${monster.pathTarget.x.toFixed(1)},${monster.pathTarget.z.toFixed(1)}`
          : "none";
        return `${monster.label} f=${monster.floor} ${monster.state} path=${monster.chasePathLength || monster.patrolPathLength} target=${target} stuck=${monster.stuckTimer.toFixed(2)}`;
      })
      .join("\n");
    const waypointText = (debug.transitionWaypoints || [])
      .map((waypoint) => `${waypoint.id} f=${waypoint.floor}->${waypoint.links.join(",") || "none"}`)
      .join(" | ");
    const door = debug.nearestDoor;
    const doorText = door
      ? `${door.id} room=${door.connectedRoomId ?? "none"} locked=${door.locked} blocked=${door.blocked} d=${door.distance.toFixed(1)}`
      : "none";
    const area = debug.areaCounts;
    const areaText = area
      ? `walk=${area.walkable} room=${area.room} blocked=${area.blocked} void=${area.void} stair=${area.stair} door=${area.door}`
      : "none";
    this.floorDebugElement.textContent = [
      `floor: ${debug.floor}`,
      `x/z: ${debug.x.toFixed(2)} / ${debug.z.toFixed(2)}`,
      `tile: ${debug.tileType} (${debug.tileId})`,
      `below valid: ${debug.belowValidLanding} floor=${debug.belowFloor ?? "none"} tile=${debug.belowTileType}`,
      `drop: ${dropText}`,
      `areas: ${areaText}`,
      `door: ${doorText}`,
      `stair wp: ${waypointText || "none"}`,
      `monsters:\n${monsterText || "none"}`,
    ].join("\n");
  }
}
