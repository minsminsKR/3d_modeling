// 브라우저 진입점입니다.
// DOM 루트만 찾아 Game을 시작하고, 실제 게임 기능은 core/Game.js가 총괄합니다.

import * as THREE from "three";
import { Game } from "./core/Game.js";

window.THREE = THREE;

function isWebGLAvailable() {
  try {
    const canvas = document.createElement("canvas");
    return !!(window.WebGLRenderingContext && (canvas.getContext("webgl") || canvas.getContext("experimental-webgl")));
  } catch (e) {
    return false;
  }
}

function isWebGL2Available() {
  try {
    const canvas = document.createElement("canvas");
    return !!(window.WebGL2RenderingContext && canvas.getContext("webgl2"));
  } catch (e) {
    return false;
  }
}

try {
  if (!isWebGLAvailable()) {
    throw new Error("WebGL을 사용할 수 없습니다. 브라우저 설정(설정 > 시스템 > 가능한 경우 하드웨어 가속 사용)에서 하드웨어 가속이 켜져 있는지 확인해 주세요.");
  }
  if (!isWebGL2Available()) {
    throw new Error("WebGL 2를 사용할 수 없습니다. 브라우저 설정(설정 > 시스템 > 가능한 경우 하드웨어 가속 사용)에서 하드웨어 가속이 켜져 있는지 확인해 주세요.");
  }

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

  // 화면의 .start-copy 컨테이너 안에 오류 디버그 영역을 동적으로 붙여 유저가 오류를 볼 수 있도록 합니다.
  const startCopy = document.querySelector(".start-copy");
  if (startCopy) {
    let errDiv = document.querySelector("#init-error-display");
    if (!errDiv) {
      errDiv = document.createElement("div");
      errDiv.id = "init-error-display";
      errDiv.style.color = "#ff6b6b";
      errDiv.style.marginTop = "20px";
      errDiv.style.padding = "10px";
      errDiv.style.border = "1px solid #ff6b6b";
      errDiv.style.borderRadius = "6px";
      errDiv.style.background = "rgba(255, 107, 107, 0.1)";
      errDiv.style.fontSize = "13px";
      errDiv.style.whiteSpace = "pre-wrap";
      errDiv.style.textAlign = "left";
      startCopy.appendChild(errDiv);
    }
    errDiv.textContent = `[초기화 오류]\n메시지: ${error.message}\n\n스택:\n${error.stack || ""}`;
  }
}

