import { soundManager } from "../audio/SoundManager.js";

export class MenuSystem {
  constructor(game) {
    this.game = game;
    this.container = document.createElement("div");
    this.container.id = "menu-system-root";
    document.body.appendChild(this.container);

    this.currentMode = "normal"; // normal, nightmare, hardcore
    this.highScores = this.loadHighScores();

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

  markMenuOpen() {
    document.body.classList.add("menu-open");
    this.container.style.display = "block";
    this.container.style.pointerEvents = "auto";
  }

  showTitleScreen() {
    this.markMenuOpen();
    this.renderTitleScreen();
  }

  hideMenu() {
    document.body.classList.remove("menu-open");
    this.container.style.display = "none";
    this.container.style.pointerEvents = "none";
  }

  renderTitleScreen() {
    this.markMenuOpen();
    const modeLabel =
      this.currentMode === "nightmare" ? "악몽" : this.currentMode === "hardcore" ? "하드코어" : "보통";
    this.container.innerHTML = `
      <div class="menu-overlay title-screen-bg">
        <div class="title-box">
          <p class="title-eyebrow">SHADOW CORRIDOR</p>
          <h1 class="title-heading">그림자복도</h1>
          <p class="title-kana" lang="ja">廃校の夏 · 影の廊下</p>
          <p class="title-sub">손전등을 끄면 복도만 남습니다. 신발장에 숨고, 이름을 제단함에 돌려놓으십시오.<br>출석이 끝나기 전에.</p>

          <div class="menu-buttons">
            <button id="btn-start-game" class="menu-btn primary-btn">복도로 들어가기</button>
            <button id="btn-difficulty" class="menu-btn secondary-btn">난이도 · <span id="diff-label">${modeLabel}</span></button>
            <button id="btn-settings" class="menu-btn secondary-btn">환경 설정</button>
            <button id="btn-records" class="menu-btn secondary-btn">탈출 기록</button>
          </div>

          <p class="controls-quiet">WASD 이동 · SHIFT 질주 · F 손전등 · E 열기·숨기 · Q 유인 · ESC 멈춤</p>
          <p class="title-footnote"><kbd>\`</kbd> 유령</p>
        </div>
      </div>
    `;

    document.getElementById("btn-start-game")?.addEventListener("click", () => {
      soundManager.init();
      soundManager.resume();
      this.hideMenu();
      this.game?.start();
    });

    this.container.querySelector(".menu-overlay")?.addEventListener("pointerdown", (event) => {
      if (event.target instanceof Element && event.target.closest("button, a, input, select")) {
        return;
      }
      soundManager.init();
      soundManager.resume();
      this.hideMenu();
      this.game?.start();
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
    this.markMenuOpen();
    this.container.innerHTML = `
      <div class="menu-overlay">
        <div class="settings-card">
          <p class="title-eyebrow">SHADOW CORRIDOR</p>
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
    this.markMenuOpen();
    const listHtml = this.highScores.length
      ? this.highScores
          .map(
            (r, i) => `
        <div class="record-row">
          <span>#${i + 1} [${r.mode.toUpperCase()}] ${r.date}</span>
          <span><b>${r.time}초</b> (이름 ${r.keys})</span>
        </div>
      `,
          )
          .join("")
      : '<div class="no-records">아직 봉인 기록이 없습니다.</div>';

    this.container.innerHTML = `
      <div class="menu-overlay">
        <div class="records-card">
          <p class="title-eyebrow">SHADOW CORRIDOR</p>
          <h2>최단 봉인 기록</h2>
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
    this.markMenuOpen();
    this.container.innerHTML = `
      <div class="screamer-overlay">
        <div class="screamer-content">
          <p class="title-eyebrow">출석</p>
          <h1 class="screamer-title">복도가 당신의 이름을 외웠습니다.</h1>
          <p class="screamer-sub">신발장 너머에서 출석이 끝났습니다. 복도는 당신의 이름을 잊지 않습니다.</p>
          <button id="btn-retry-game" class="menu-btn primary-btn big-btn">다시 걷기</button>
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

    this.markMenuOpen();
    this.container.innerHTML = `
      <div class="victory-overlay">
        <div class="victory-card">
          <div class="victory-banner">봉인</div>
          <h1 class="victory-title">제단이 문을 삼켰습니다.</h1>
          <p class="victory-linger">손전등이 꺼져도 복도는 남습니다.</p>

          <div class="victory-stats">
            <div class="stat-item">
              <span class="label">봉인까지</span>
              <span class="value">${timeSeconds.toFixed(1)}초</span>
            </div>
            <div class="stat-item">
              <span class="label">최종 랭크</span>
              <span class="value rank-${rank}">${rank}</span>
            </div>
          </div>

          <div class="victory-actions">
            <button id="btn-victory-replay" class="menu-btn primary-btn">다시 걷기</button>
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
