import { soundManager } from "../audio/SoundManager.js";

export class MenuSystem {
  constructor(game) {
    this.game = game;
    this.container = document.createElement("div");
    this.container.id = "menu-system-root";
    this.container.style.pointerEvents = "auto";
    document.body.appendChild(this.container);

    this.currentMode = "normal"; // normal, nightmare, hardcore
    this.highScores = this.loadHighScores();
    this.screen = "title";
    this.assetsReady = Boolean(game?.assetsReady);
    this.pendingStart = false;

    this.onTitleKeyDown = (event) => this.handleTitleKeyDown(event);
    window.addEventListener("keydown", this.onTitleKeyDown);

    this.renderTitleScreen();
  }

  loadHighScores() {
    try {
      const data = localStorage.getItem("happy_toy_high_scores");
      return data ? JSON.parse(data) : [];
    } catch (e) {
      return [];
    }
  }

  saveHighScore(timeSeconds, keyCount) {
    const newRecord = {
      date: new Date().toLocaleDateString("ko-KR"),
      time: timeSeconds.toFixed(1),
      keys: keyCount,
      mode: this.currentMode,
    };
    this.highScores.push(newRecord);
    this.highScores.sort((a, b) => parseFloat(a.time) - parseFloat(b.time));
    this.highScores = this.highScores.slice(0, 5); // top 5
    try {
      localStorage.setItem("happy_toy_high_scores", JSON.stringify(this.highScores));
    } catch (e) {}
  }

  showTitleScreen() {
    this.container.style.display = "block";
    this.renderTitleScreen();
  }

  hideMenu() {
    this.screen = "hidden";
    this.pendingStart = false;
    this.container.style.display = "none";
  }

  isTitleVisible() {
    return this.screen === "title" && this.container.style.display !== "none";
  }

  setAssetsReady(ready) {
    this.assetsReady = Boolean(ready);
    this.syncStartButton();
    if (this.assetsReady && this.pendingStart && this.isTitleVisible()) {
      this.pendingStart = false;
      this.tryStart();
    }
  }

  syncStartButton() {
    const btn = document.getElementById("btn-start-game");
    const status = document.getElementById("menu-status");
    if (btn) {
      btn.disabled = false;
      btn.classList.toggle("is-loading", !this.assetsReady);
      btn.textContent = this.assetsReady ? "게임 시작" : "불러오는 중...";
    }
    if (status) {
      if (this.pendingStart && !this.assetsReady) {
        status.textContent = "불러오는 대로 바로 시작합니다...";
      } else if (this.assetsReady) {
        status.textContent = "클릭하거나 아무 키나 누르면 시작합니다.";
      } else {
        status.textContent = "복도와 몬스터를 불러오는 중입니다.";
      }
    }
  }

  tryStart() {
    if (!this.game) return false;
    if (this.game.isStarted && !this.game.isPaused) {
      this.hideMenu();
      this.game.input?.requestPointerLock();
      return true;
    }
    if (!this.game.assetsReady) {
      this.pendingStart = true;
      this.syncStartButton();
      return false;
    }

    try {
      soundManager.init();
      soundManager.resume();
    } catch (_) {}

    this.hideMenu();
    this.game.start();
    return true;
  }

  handleTitleKeyDown(event) {
    if (!this.isTitleVisible()) return;
    if (event.repeat || event.metaKey || event.ctrlKey || event.altKey) return;
    if (event.target && typeof event.target.closest === "function" && event.target.closest("input, textarea")) {
      return;
    }
    const ignore = new Set(["Escape", "Tab", "F5", "F12", "Shift", "Control", "Alt", "Meta"]);
    if (ignore.has(event.key) || ignore.has(event.code)) return;
    event.preventDefault();
    this.tryStart();
  }

  renderTitleScreen() {
    this.screen = "title";
    this.container.style.display = "block";
    const diffLabel = this.currentMode === "normal"
      ? "보통"
      : this.currentMode === "nightmare"
        ? "악몽"
        : "하드코어";
    this.container.innerHTML = `
      <div class="menu-overlay title-screen-bg">
        <div class="title-box">
          <div class="title-eyebrow">FIRST-PERSON HORROR</div>
          <h1 class="title-heading">HAPPY TOY</h1>
          <div class="title-sub">그림자 복도 · 낡은 여름 회랑</div>
          
          <div class="menu-buttons">
            <button id="btn-start-game" class="menu-btn primary-btn" type="button">게임 시작</button>
            <button id="btn-difficulty" class="menu-btn secondary-btn" type="button">난이도 · <span id="diff-label">${diffLabel}</span></button>
            <button id="btn-settings" class="menu-btn secondary-btn" type="button">환경 설정</button>
            <button id="btn-records" class="menu-btn secondary-btn" type="button">탈출 기록</button>
          </div>
          <p id="menu-status" class="menu-status" role="status"></p>

          <div class="controls-hint-box">
            <div class="hint-title">조작</div>
            <div class="hint-grid">
              <span><b>WASD</b> 이동</span>
              <span><b>Shift</b> 달리기</span>
              <span><b>Mouse</b> 시점</span>
              <span><b>F</b> 손전등 ON/OFF</span>
              <span><b>E</b> 문/열쇠/캐비넷/아이템 상호작용</span>
              <span><b>Q / 1~4</b> 폭죽 투척 & 아이템 사용</span>
              <span><b>Esc</b> 일시정지 메뉴</span>
            </div>
          </div>
        </div>
      </div>
    `;

    this.syncStartButton();

    document.getElementById("btn-start-game")?.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      this.tryStart();
    });

    this.container.querySelector(".menu-overlay")?.addEventListener("click", (event) => {
      const button = event.target.closest("button");
      if (button && button.id !== "btn-start-game") {
        return;
      }
      this.tryStart();
    });


    document.getElementById("btn-difficulty")?.addEventListener("click", () => {
      if (this.currentMode === "normal") {
        this.currentMode = "nightmare";
      } else if (this.currentMode === "nightmare") {
        this.currentMode = "hardcore";
      } else {
        this.currentMode = "normal";
      }
      const label = document.getElementById("diff-label");
      if (label) {
        label.innerText =
          this.currentMode === "normal"
            ? "보통"
            : this.currentMode === "nightmare"
            ? "악몽"
            : "하드코어";
      }
    });

    document.getElementById("btn-settings")?.addEventListener("click", () => {
      this.renderSettingsScreen();
    });

    document.getElementById("btn-records")?.addEventListener("click", () => {
      this.renderRecordsScreen();
    });
  }

  renderSettingsScreen() {
    this.screen = "settings";
    this.container.innerHTML = `
      <div class="menu-overlay">
        <div class="settings-card">
          <h2>환경 설정</h2>
          <div class="setting-row">
            <label>마스터 음량</label>
            <input type="range" id="vol-master" min="0" max="1" step="0.05" value="${soundManager.volumes.master}">
          </div>
          <div class="setting-row">
            <label>배경음 음량</label>
            <input type="range" id="vol-bgm" min="0" max="1" step="0.05" value="${soundManager.volumes.bgm}">
          </div>
          <div class="setting-row">
            <label>효과음 음량</label>
            <input type="range" id="vol-sfx" min="0" max="1" step="0.05" value="${soundManager.volumes.sfx}">
          </div>
          <div class="setting-row">
            <label>마우스 감도</label>
            <input type="range" id="mouse-sens" min="0.0005" max="0.005" step="0.0005" value="${
              this.game?.player?.mouseSensitivity || 0.0022
            }">
          </div>
          <button id="btn-back-settings" class="menu-btn primary-btn mt-4">뒤로 가기</button>
        </div>
      </div>
    `;

    document.getElementById("vol-master")?.addEventListener("input", (e) => {
      soundManager.setMasterVolume(parseFloat(e.target.value));
    });
    document.getElementById("vol-bgm")?.addEventListener("input", (e) => {
      soundManager.setBGMVolume(parseFloat(e.target.value));
    });
    document.getElementById("vol-sfx")?.addEventListener("input", (e) => {
      soundManager.setSFXVolume(parseFloat(e.target.value));
    });
    document.getElementById("mouse-sens")?.addEventListener("input", (e) => {
      if (this.game?.player) {
        this.game.player.setMouseSensitivity(parseFloat(e.target.value));
      }
    });

    document.getElementById("btn-back-settings")?.addEventListener("click", () => {
      this.renderTitleScreen();
    });
  }

  renderRecordsScreen() {
    this.screen = "records";
    const listHtml = this.highScores.length
      ? this.highScores
          .map(
            (r, i) => `
        <div class="record-row">
          <span>#${i + 1} [${r.mode.toUpperCase()}] ${r.date}</span>
          <span><b>${r.time}초</b> (열쇠 ${r.keys}개)</span>
        </div>
      `,
          )
          .join("")
      : '<div class="no-records">아직 탈출 기록이 없습니다.</div>';

    this.container.innerHTML = `
      <div class="menu-overlay">
        <div class="records-card">
          <h2>최단 탈출 기록</h2>
          <div class="records-list">${listHtml}</div>
          <button id="btn-back-records" class="menu-btn primary-btn mt-4">뒤로 가기</button>
        </div>
      </div>
    `;

    document.getElementById("btn-back-records")?.addEventListener("click", () => {
      this.renderTitleScreen();
    });
  }

  showGameOverScreamer() {
    soundManager.playSFX("screamer_jumpscare");
    this.container.style.display = "block";
    this.container.innerHTML = `
      <div class="screamer-overlay">
        <div class="screamer-content">
          <h1 class="screamer-title">사망하셨습니다</h1>
          <p class="screamer-sub">복도의 기괴한 존재에게 포획되었습니다...</p>
          <button id="btn-retry-game" class="menu-btn primary-btn big-btn">다시 도전하기</button>
        </div>
      </div>
    `;

    document.getElementById("btn-retry-game")?.addEventListener("click", () => {
      this.hideMenu();
      if (this.game) {
        this.game.restart();
      }
    });
  }

  showVictoryClear(timeSeconds) {
    this.saveHighScore(timeSeconds, 4);
    soundManager.playSFX("key_pickup");

    let rank = "C";
    if (timeSeconds < 90) rank = "S";
    else if (timeSeconds < 180) rank = "A";
    else if (timeSeconds < 300) rank = "B";

    this.container.style.display = "block";
    this.container.innerHTML = `
      <div class="victory-overlay">
        <div class="victory-card">
          <div class="victory-banner">ESCAPE SUCCESS!</div>
          <h1 class="victory-title">무사히 탈출했습니다!</h1>
          
          <div class="victory-stats">
            <div class="stat-item">
              <span class="label">탈출 시간</span>
              <span class="value">${timeSeconds.toFixed(1)}초</span>
            </div>
            <div class="stat-item">
              <span class="label">최종 랭크</span>
              <span class="value rank-${rank}">${rank} 랭크</span>
            </div>
          </div>

          <div class="victory-actions">
            <button id="btn-victory-replay" class="menu-btn primary-btn">다시 하기</button>
            <button id="btn-victory-title" class="menu-btn secondary-btn">타이틀 화면</button>
          </div>
        </div>
      </div>
    `;

    document.getElementById("btn-victory-replay")?.addEventListener("click", () => {
      this.hideMenu();
      if (this.game) {
        this.game.restart();
      }
    });

    document.getElementById("btn-victory-title")?.addEventListener("click", () => {
      this.renderTitleScreen();
    });
  }
}
