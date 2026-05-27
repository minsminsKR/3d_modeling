// requestAnimationFrame 기반의 게임 루프 모듈입니다.
// 렌더링과 업데이트 타이밍을 Game 클래스 밖으로 분리해 테스트와 교체를 쉽게 합니다.

import * as THREE from "three";

export class Loop {
  constructor(update) {
    this.clock = new THREE.Clock();
    this.update = update;
    this.running = false;
    this.frameId = null;
  }

  start() {
    if (this.running) {
      return;
    }
    this.running = true;
    this.clock.start();
    this.tick();
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
    this.update(deltaTime);
    const dt = performance.now() - t0;

    if (typeof window !== "undefined") {
      if (!window.__happyToyFrameTimes) {
        window.__happyToyFrameTimes = [];
      }
      window.__happyToyFrameTimes.push(dt);
    }

    if (dt > 16.7) {
      console.warn(`[PERF] Frame took ${dt.toFixed(2)}ms (Spike!)`);
    }

    this.frameId = requestAnimationFrame(() => this.tick());
  }
}
