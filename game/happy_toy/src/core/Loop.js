// requestAnimationFrame 기반의 게임 루프 모듈입니다.
// 렌더링과 업데이트 타이밍을 Game 클래스 밖으로 분리해 테스트와 교체를 쉽게 합니다.

import * as THREE from "three";
import { PERF_CONFIG } from "../config/gameConfig.js";

const FRAME_RING_SIZE = PERF_CONFIG.frameTimeRingSize ?? 7200;

export class Loop {
  constructor(update) {
    this.clock = new THREE.Clock();
    this.update = update;
    this.running = false;
    this.frameId = null;
    this._lastPerfWarn = 0;
  }

  start() {
    if (this.running) {
      return;
    }
    this.running = true;
    this.clock.start();
    // Defer the first tick so Game.init() can finish even if a frame throws.
    this.frameId = requestAnimationFrame(() => this.tick());
  }

  stop() {
    this.running = false;
    if (this.frameId !== null) {
      cancelAnimationFrame(this.frameId);
      this.frameId = null;
    }
  }

  tick() {
    if (!this.running) {
      return;
    }
    const t0 = performance.now();
    const deltaTime = Math.min(this.clock.getDelta(), 0.05);
    try {
      this.update(deltaTime);
    } catch (error) {
      if (t0 - this._lastPerfWarn > 1000) {
        this._lastPerfWarn = t0;
        console.error("[Loop] frame error — continuing so the game does not freeze", error);
      }
    }
    const dt = performance.now() - t0;

    if (typeof window !== "undefined") {
      if (!window.__happyToyFrameTimes) {
        window.__happyToyFrameTimes = [];
      }
      const times = window.__happyToyFrameTimes;
      times.push(dt);
      if (times.length > FRAME_RING_SIZE) {
        times.splice(0, times.length - Math.floor(FRAME_RING_SIZE * 0.75));
      }
    }

    if (dt > 33.3 && t0 - this._lastPerfWarn > 1000) {
      this._lastPerfWarn = t0;
      console.warn(`[PERF] Frame took ${dt.toFixed(2)}ms (Spike!)`);
    }

    this.frameId = requestAnimationFrame(() => this.tick());
  }
}
