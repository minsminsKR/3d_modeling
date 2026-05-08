// 브라우저 진입점입니다.
// DOM 루트만 찾아 Game을 시작하고, 실제 게임 기능은 core/Game.js가 총괄합니다.

import { Game } from "./core/Game.js";

try {
  const rootElement = document.querySelector("#game-root");
  const game = new Game(rootElement);
  window.__happyToy = game;
  await game.init();
} catch (error) {
  console.error(error);
  const status = document.querySelector("#status-line");
  const startButton = document.querySelector("#start-button");
  if (status) {
    status.textContent = `게임 초기화 실패: ${error.message}`;
  }
  if (startButton) {
    startButton.disabled = true;
    startButton.textContent = "초기화 실패";
  }
}
