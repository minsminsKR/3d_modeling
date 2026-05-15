// 적에게 발각되는 순간의 화면 지지직, 잔류 노이즈, 저주파 hum을 관리합니다.
// Game은 감지 이벤트와 threat 값만 넘기고, DOM/CSS 변수 조작은 이 모듈에 모아 둡니다.

export class GlitchController {
  constructor() {
    this.overlay = document.querySelector("#glitch-overlay");
    this.body = document.body;
    this.burstTimer = 0;
    this.burstDuration = 0.36;
    this.burstStrength = 0;
    this.residual = 0;
    this.frameBreakTimer = 0;
    this.impactTimer = 0;
    this.impactDuration = 0.72;
    this.impactStrength = 0;
    this.heartbeatTimer = 0;
    this.heartbeatPhase = 0;
    this.audioContext = null;
    this.humOscillator = null;
    this.humGain = null;
    this.noiseOscillator = null;
    this.noiseGain = null;
  }

  primeAudio() {
    if (this.audioContext) {
      this.resumeAudio();
      return;
    }

    const AudioContextClass = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextClass) {
      return;
    }

    this.audioContext = new AudioContextClass();
    this.humOscillator = this.audioContext.createOscillator();
    this.humGain = this.audioContext.createGain();
    this.noiseOscillator = this.audioContext.createOscillator();
    this.noiseGain = this.audioContext.createGain();

    this.humOscillator.type = "sawtooth";
    this.humOscillator.frequency.value = 47;
    this.humGain.gain.value = 0;
    this.humOscillator.connect(this.humGain).connect(this.audioContext.destination);
    this.humOscillator.start();

    this.noiseOscillator.type = "square";
    this.noiseOscillator.frequency.value = 24;
    this.noiseGain.gain.value = 0;
    this.noiseOscillator.connect(this.noiseGain).connect(this.audioContext.destination);
    this.noiseOscillator.start();
  }

  resumeAudio() {
    if (this.audioContext?.state === "suspended") {
      this.audioContext.resume();
    }
  }

  trigger({ strength = 0.8, full = false, firstDetection = false, distance = null } = {}) {
    const proximity = distance === null ? Math.max(0, strength - 0.58) : Math.max(0, 1 - Math.min(1, distance / 8));
    const scareBoost = firstDetection ? 0.42 + proximity * 0.24 : 0;
    const boosted = Math.max(this.burstStrength, Math.min(1.85, strength + (full ? 0.24 : 0) + scareBoost));
    this.burstStrength = boosted;
    this.burstTimer = firstDetection ? 0.56 : this.burstDuration;
    this.frameBreakTimer = firstDetection ? 0.16 + proximity * 0.04 : (full ? 0.08 : 0.045);
    this.impactTimer = Math.max(this.impactTimer, firstDetection ? this.impactDuration : 0.16);
    this.impactStrength = Math.max(this.impactStrength, firstDetection ? Math.min(1.35, boosted) : 0.45);
    this.heartbeatTimer = Math.max(this.heartbeatTimer, firstDetection ? 2.4 + proximity * 0.85 : 0.9);
    this.residual = Math.max(this.residual, firstDetection ? 0.76 + proximity * 0.18 : (full ? 0.5 : 0.34));
    this.body.classList.add("glitch-active", "glitch-snap");
    if (firstDetection) {
      this.body.classList.add("glitch-no-signal", "glitch-impact");
    }
    window.setTimeout(() => this.body.classList.remove("glitch-snap", "glitch-no-signal"), firstDetection ? 170 : 90);
    window.setTimeout(() => this.body.classList.remove("glitch-impact"), firstDetection ? 720 : 180);
    this.resumeAudio();
    this.playImpact(boosted, proximity, firstDetection);
  }

  update(deltaTime, { threat = 0, healthStress = 0 } = {}) {
    const safeDelta = Math.min(deltaTime, 0.05);
    this.burstTimer = Math.max(0, this.burstTimer - safeDelta);
    this.frameBreakTimer = Math.max(0, this.frameBreakTimer - safeDelta);
    this.impactTimer = Math.max(0, this.impactTimer - safeDelta);
    this.heartbeatTimer = Math.max(0, this.heartbeatTimer - safeDelta);
    this.heartbeatPhase += safeDelta * (this.heartbeatTimer > 0 ? 4.8 : 1);

    const threatResidual = Math.max(0, threat - 0.22) * 0.58;
    const targetResidual = Math.min(0.75, threatResidual + healthStress * 0.24);
    this.residual += (targetResidual - this.residual) * Math.min(1, safeDelta * 4.4);

    const burstRatio = this.burstDuration > 0 ? this.burstTimer / this.burstDuration : 0;
    const burst = burstRatio * this.burstStrength;
    const frameBreak = this.frameBreakTimer > 0 ? 0.42 : 0;
    const impactRatio = this.impactDuration > 0 ? this.impactTimer / this.impactDuration : 0;
    const heartbeat = this.heartbeatTimer > 0
      ? Math.max(0, Math.sin(this.heartbeatPhase * Math.PI) ** 8) * Math.min(0.28, this.heartbeatTimer * 0.08)
      : 0;
    const impact = impactRatio * this.impactStrength;
    const intensity = Math.max(0, Math.min(1.85, this.residual + burst + frameBreak + impact * 0.45 + heartbeat));
    this.applyVisuals(intensity, burstRatio, impactRatio, heartbeat);
    this.updateHum(intensity);
  }

  reset() {
    this.burstTimer = 0;
    this.burstStrength = 0;
    this.residual = 0;
    this.frameBreakTimer = 0;
    this.impactTimer = 0;
    this.impactStrength = 0;
    this.heartbeatTimer = 0;
    this.heartbeatPhase = 0;
    this.body.classList.remove("glitch-active", "glitch-snap", "glitch-no-signal", "glitch-impact");
    this.applyVisuals(0, 0);
    this.updateHum(0);
  }

  applyVisuals(intensity, burstRatio, impactRatio = 0, heartbeat = 0) {
    if (intensity <= 0.015) {
      this.body.classList.remove("glitch-active");
      this.setVar("--glitch-opacity", "0");
      this.setVar("--glitch-x", "0px");
      this.setVar("--glitch-y", "0px");
      this.setVar("--glitch-rgb", "0px");
      this.setVar("--glitch-brightness", "1");
      this.setVar("--glitch-contrast", "1");
      this.setVar("--glitch-tear", "0px");
      this.setVar("--glitch-darkness", "0");
      this.setVar("--glitch-vignette", "0");
      this.setVar("--glitch-static", "0");
      return;
    }

    this.body.classList.add("glitch-active");
    const hit = Math.max(burstRatio, impactRatio);
    const jitter = (Math.random() - 0.5) * (intensity + heartbeat * 2.6);
    const tear = Math.round((Math.random() - 0.5) * 68 * intensity * (hit > 0 ? 1.7 : 0.45));
    this.setVar("--glitch-opacity", Math.min(0.94, 0.1 + intensity * 0.44 + impactRatio * 0.26).toFixed(3));
    this.setVar("--glitch-x", `${(jitter * (16 + impactRatio * 24)).toFixed(2)}px`);
    this.setVar("--glitch-y", `${((Math.random() - 0.5) * intensity * (5 + impactRatio * 10)).toFixed(2)}px`);
    this.setVar("--glitch-rgb", `${Math.min(16, intensity * 7.2 + impactRatio * 7).toFixed(2)}px`);
    this.setVar("--glitch-brightness", (1.2 + burstRatio * 0.36 - impactRatio * 0.34 + intensity * 0.04).toFixed(3));
    this.setVar("--glitch-contrast", (1.08 + intensity * 0.48 + impactRatio * 0.52).toFixed(3));
    this.setVar("--glitch-tear", `${tear}px`);
    this.setVar("--glitch-scan-speed", `${Math.max(0.055, 0.42 - intensity * 0.18 - impactRatio * 0.12).toFixed(2)}s`);
    this.setVar("--glitch-darkness", Math.min(0.64, impactRatio * 0.55 + intensity * 0.08).toFixed(3));
    this.setVar("--glitch-vignette", Math.min(0.9, 0.28 + intensity * 0.24 + impactRatio * 0.42).toFixed(3));
    this.setVar("--glitch-static", Math.min(1, intensity * 0.42 + impactRatio * 0.76).toFixed(3));
  }

  updateHum(intensity) {
    if (!this.audioContext || !this.humGain || !this.noiseGain) {
      return;
    }
    const now = this.audioContext.currentTime;
    const hum = Math.min(0.08, intensity * 0.045);
    const buzz = Math.min(0.035, Math.max(0, intensity - 0.5) * 0.028);
    this.humOscillator.frequency.setTargetAtTime(43 + intensity * 18, now, 0.04);
    this.noiseOscillator.frequency.setTargetAtTime(18 + intensity * 34, now, 0.025);
    this.humGain.gain.setTargetAtTime(hum, now, 0.035);
    this.noiseGain.gain.setTargetAtTime(buzz, now, 0.025);
  }

  playImpact(strength, proximity, firstDetection) {
    if (!this.audioContext) {
      return;
    }

    const now = this.audioContext.currentTime;
    const amount = Math.min(1.2, strength * (firstDetection ? 1 : 0.55));
    this.playBassDrop(now, amount, proximity);
    this.playMetalHit(now, amount);
    if (firstDetection) {
      this.playStaticSnap(now, amount);
    }
  }

  playBassDrop(now, amount, proximity) {
    const oscillator = this.audioContext.createOscillator();
    const gain = this.audioContext.createGain();
    oscillator.type = "triangle";
    oscillator.frequency.setValueAtTime(42 + proximity * 8, now);
    oscillator.frequency.exponentialRampToValueAtTime(24, now + 0.34);
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(0.24 * amount, now + 0.018);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.62);
    oscillator.connect(gain).connect(this.audioContext.destination);
    oscillator.start(now);
    oscillator.stop(now + 0.68);
  }

  playMetalHit(now, amount) {
    for (const [frequency, detune] of [[91, -7], [137, 11], [182, 0]]) {
      const oscillator = this.audioContext.createOscillator();
      const gain = this.audioContext.createGain();
      const filter = this.audioContext.createBiquadFilter();
      oscillator.type = "sawtooth";
      oscillator.frequency.value = frequency;
      oscillator.detune.value = detune;
      filter.type = "bandpass";
      filter.frequency.value = frequency * 2.15;
      filter.Q.value = 9;
      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.exponentialRampToValueAtTime(0.06 * amount, now + 0.012);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.38);
      oscillator.connect(filter).connect(gain).connect(this.audioContext.destination);
      oscillator.start(now);
      oscillator.stop(now + 0.42);
    }
  }

  playStaticSnap(now, amount) {
    const sampleRate = this.audioContext.sampleRate;
    const length = Math.floor(sampleRate * 0.18);
    const buffer = this.audioContext.createBuffer(1, length, sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < length; i += 1) {
      const envelope = 1 - i / length;
      data[i] = (Math.random() * 2 - 1) * envelope * envelope;
    }

    const source = this.audioContext.createBufferSource();
    const filter = this.audioContext.createBiquadFilter();
    const gain = this.audioContext.createGain();
    filter.type = "highpass";
    filter.frequency.value = 720;
    gain.gain.setValueAtTime(0.18 * amount, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.18);
    source.buffer = buffer;
    source.connect(filter).connect(gain).connect(this.audioContext.destination);
    source.start(now);
  }

  setVar(name, value) {
    document.documentElement.style.setProperty(name, value);
  }
}
