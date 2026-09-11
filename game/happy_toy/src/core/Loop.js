// requestAnimationFrame 기반의 게임 루프 모듈입니다.
// 렌더링과 업데이트 타이밍을 Game 클래스 밖으로 분리해 테스트와 교체를 쉽게 합니다.

import * as THREE from "three";

export class Loop {
  constructor(update) {
    this.clock = new THREE.Clock();
    this.update = update;
    this.running = false;
    this.frameId = null;
    this.backgroundTimer = new Worker(new URL('./BackgroundTick.js', import.meta.url));
    this.backgroundTimer.onmessage = () => {
      if (this.running && (document.hidden || !document.hasFocus())) this.tick();
    };
    const reschedule = () => {
      if (!this.running) return;
      if (this.frameId !== null) cancelAnimationFrame(this.frameId);
      this.frameId = null;
      this.backgroundTimer.postMessage((document.hidden || !document.hasFocus()));
      this.tick();
    };
    document.addEventListener('visibilitychange', reschedule);
    window.addEventListener('blur', reschedule);
    window.addEventListener('focus', reschedule);
  }

  start() {
    if (this.running) {
      return;
    }
    this.running = true;
    this.clock.start();
    this.backgroundTimer.postMessage((document.hidden || !document.hasFocus()));
    this.tick();
  }

  stop() {
    this.running = false;
    this.backgroundTimer.postMessage(false);
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
    const elapsed = this.clock.getDelta();
    if ((document.hidden || !document.hasFocus())) {
      // Keep AI/timers moving with small collision-safe steps, without drawing
      // an invisible frame. Bound recovery after OS sleep to avoid a long stall.
      let remaining = Math.min(elapsed, 5);
      while (remaining > 0.00001) {
        const step = Math.min(remaining, 0.05);
        this.update(step, {skipRender: true});
        remaining -= step;
      }
    } else {
      this.update(Math.min(elapsed, 0.05));
    }
    const dt = performance.now() - t0;

    if (typeof window !== "undefined" && window.__happyToy?.debugEnabled) {
      if (!window.__happyToyFrameTimes) {
        window.__happyToyFrameTimes = [];
      }
      window.__happyToyFrameTimes.push(dt);
      if (window.__happyToyFrameTimes.length > 3600) window.__happyToyFrameTimes.splice(0, 600);
    }

    if (dt > 100 && window.__happyToy?.debugEnabled) {
      console.warn(`[PERF] Frame took ${dt.toFixed(2)}ms (Spike!)`);
    }

    this.frameId = (document.hidden || !document.hasFocus()) ? null : requestAnimationFrame(() => this.tick());
  }
}
