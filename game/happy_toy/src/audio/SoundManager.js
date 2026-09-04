// Web Audio API 기반의 자족형 호러 오디오 엔진
// 브라우저 외부 음원 파일 의존 없이 합성음 및 앰비언스, 발소리, 심장소리, 몬스터 괴성, 상호작용 SFX를 재생합니다.

export class SoundManager {
  constructor() {
    this.ctx = null;
    this.masterGain = null;
    this.bgmGain = null;
    this.sfxGain = null;
    
    this.volumes = {
      master: 0.8,
      bgm: 0.6,
      sfx: 0.9,
    };

    this.initialized = false;
    this.isMuted = false;
    
    // 심장소리 타이머
    this.heartbeatTimer = 0;
    this.heartbeatInterval = 1.2; // 몬스터 거리에 따라 단축 (0.3 ~ 1.2초)
    this.monsterDistanceRatio = 1.0; // 0 (아주 가까움) ~ 1 (멀음)

    // 앰비언스 노드 참조
    this.ambientOsc1 = null;
    this.ambientOsc2 = null;
    this.ambientNoiseNode = null;
    
    // 발소리 쿨다운
    this.lastFootstepTime = 0;
  }

  init() {
    if (this.initialized) return;
    try {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (!AudioCtx) return;
      this.ctx = new AudioCtx();

      this.masterGain = this.ctx.createGain();
      this.bgmGain = this.ctx.createGain();
      this.sfxGain = this.ctx.createGain();

      this.masterGain.gain.value = this.volumes.master;
      this.bgmGain.gain.value = this.volumes.bgm;
      this.sfxGain.gain.value = this.volumes.sfx;

      this.bgmGain.connect(this.masterGain);
      this.sfxGain.connect(this.masterGain);
      this.masterGain.connect(this.ctx.destination);

      this.initialized = true;
      this.startAmbientDrone();
    } catch (e) {
      console.warn("SoundManager init failed:", e);
    }
  }

  resume() {
    if (this.ctx && this.ctx.state === "suspended") {
      this.ctx.resume();
    }
  }

  setMasterVolume(val) {
    this.volumes.master = Math.max(0, Math.min(1, val));
    if (this.masterGain && this.ctx) {
      this.masterGain.gain.setValueAtTime(this.volumes.master, this.ctx.currentTime);
    }
  }

  setBGMVolume(val) {
    this.volumes.bgm = Math.max(0, Math.min(1, val));
    if (this.bgmGain && this.ctx) {
      this.bgmGain.gain.setValueAtTime(this.volumes.bgm, this.ctx.currentTime);
    }
  }

  setSFXVolume(val) {
    this.volumes.sfx = Math.max(0, Math.min(1, val));
    if (this.sfxGain && this.ctx) {
      this.sfxGain.gain.setValueAtTime(this.volumes.sfx, this.ctx.currentTime);
    }
  }

  // 백그라운드 공포 드론 & 형광등 웅웅거리는 소리 합성
  startAmbientDrone() {
    if (!this.initialized || !this.ctx) return;

    // Sub bass low frequency osc
    const osc1 = this.ctx.createOscillator();
    const osc1Gain = this.ctx.createGain();
    osc1.type = "sine";
    osc1.frequency.setValueAtTime(55, this.ctx.currentTime); // A1 note low rumble
    osc1Gain.gain.setValueAtTime(0.25, this.ctx.currentTime);
    osc1.connect(osc1Gain);
    osc1Gain.connect(this.bgmGain);
    osc1.start();
    this.ambientOsc1 = osc1;

    // Deep drone 2 (dissonant detuned)
    const osc2 = this.ctx.createOscillator();
    const osc2Gain = this.ctx.createGain();
    osc2.type = "sawtooth";
    osc2.frequency.setValueAtTime(58.2, this.ctx.currentTime); // Low detuned Bb1
    
    // Low pass filter for dark tone
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.setValueAtTime(140, this.ctx.currentTime);

    osc2Gain.gain.setValueAtTime(0.08, this.ctx.currentTime);
    osc2.connect(filter);
    filter.connect(osc2Gain);
    osc2Gain.connect(this.bgmGain);
    osc2.start();
    this.ambientOsc2 = osc2;

    // Wind / Fluorescent hum noise generator
    const bufferSize = 2 * this.ctx.sampleRate;
    const noiseBuffer = this.ctx.createBuffer(1, bufferSize, this.ctx.sampleRate);
    const output = noiseBuffer.getChannelData(0);
    for (let i = 0; i < bufferSize; i++) {
      output[i] = Math.random() * 2 - 1;
    }

    const whiteNoise = this.ctx.createBufferSource();
    whiteNoise.buffer = noiseBuffer;
    whiteNoise.loop = true;

    const noiseFilter = this.ctx.createBiquadFilter();
    noiseFilter.type = "bandpass";
    noiseFilter.frequency.setValueAtTime(220, this.ctx.currentTime);
    noiseFilter.Q.setValueAtTime(3.0, this.ctx.currentTime);

    const noiseGain = this.ctx.createGain();
    noiseGain.gain.setValueAtTime(0.03, this.ctx.currentTime);

    whiteNoise.connect(noiseFilter);
    noiseFilter.connect(noiseGain);
    noiseGain.connect(this.bgmGain);
    whiteNoise.start();
    this.ambientNoiseNode = whiteNoise;

    // Whispering noise generator for Weeping Angel proximity
    const whisperNoise = this.ctx.createBufferSource();
    whisperNoise.buffer = noiseBuffer;
    whisperNoise.loop = true;

    const whisperFilter = this.ctx.createBiquadFilter();
    whisperFilter.type = "bandpass";
    whisperFilter.frequency.setValueAtTime(480, this.ctx.currentTime);
    whisperFilter.Q.setValueAtTime(5.0, this.ctx.currentTime);

    const whisperGain = this.ctx.createGain();
    whisperGain.gain.setValueAtTime(0, this.ctx.currentTime);

    whisperNoise.connect(whisperFilter);
    whisperFilter.connect(whisperGain);
    whisperGain.connect(this.sfxGain);
    whisperNoise.start();
    this.whisperGain = whisperGain;
  }

  setWhisperIntensity(intensity) {
    if (!this.initialized || !this.ctx || !this.whisperGain) return;
    const clamped = Math.max(0, Math.min(1, intensity));
    const now = this.ctx.currentTime;
    this.whisperGain.gain.setTargetAtTime(clamped * 0.45, now, 0.1);
  }

  // 지하 침수층 물 튀기는 발소리 합성 (water splash footstep)
  playWaterStep(isSprinting = false) {
    if (!this.initialized || !this.ctx) return;
    const now = performance.now() / 1000;
    const interval = isSprinting ? 0.30 : 0.46;
    if (now - this.lastFootstepTime < interval) return;
    this.lastFootstepTime = now;

    // High splash noise
    const bufferSize = Math.floor(this.ctx.sampleRate * 0.12);
    const buffer = this.ctx.createBuffer(1, bufferSize, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < bufferSize; i++) {
      data[i] = (Math.random() * 2 - 1) * Math.exp(-i / (bufferSize * 0.25));
    }
    const noise = this.ctx.createBufferSource();
    noise.buffer = buffer;

    const filter = this.ctx.createBiquadFilter();
    filter.type = "bandpass";
    filter.frequency.setValueAtTime(1200 + Math.random() * 400, this.ctx.currentTime);
    filter.Q.setValueAtTime(4.0, this.ctx.currentTime);

    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0.25 * this.volumes.sfx, this.ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, this.ctx.currentTime + 0.12);

    noise.connect(filter);
    filter.connect(gain);
    gain.connect(this.sfxGain);
    noise.start();

    // Low water pop bass
    const osc = this.ctx.createOscillator();
    const oscGain = this.ctx.createGain();
    osc.type = "sine";
    osc.frequency.setValueAtTime(140 + Math.random() * 30, this.ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(50, this.ctx.currentTime + 0.1);
    oscGain.gain.setValueAtTime(0.2 * this.volumes.sfx, this.ctx.currentTime);
    oscGain.gain.exponentialRampToValueAtTime(0.001, this.ctx.currentTime + 0.1);

    osc.connect(oscGain);
    oscGain.connect(this.sfxGain);
    osc.start();
    osc.stop(this.ctx.currentTime + 0.1);
  }

  // 발소리 재생 (isSprinting에 따라 피치와 볼륨 변동)

  playFootstep(isSprinting = false) {
    if (!this.initialized || !this.ctx) return;
    const now = performance.now() / 1000;
    const interval = isSprinting ? 0.32 : 0.48;
    if (now - this.lastFootstepTime < interval) return;
    this.lastFootstepTime = now;

    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    const filter = this.ctx.createBiquadFilter();

    osc.type = "sine";
    const startFreq = isSprinting ? 120 + Math.random() * 20 : 90 + Math.random() * 15;
    osc.frequency.setValueAtTime(startFreq, this.ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(30, this.ctx.currentTime + 0.08);

    filter.type = "lowpass";
    filter.frequency.setValueAtTime(250, this.ctx.currentTime);

    const vol = isSprinting ? 0.35 : 0.2;
    gain.gain.setValueAtTime(vol, this.ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, this.ctx.currentTime + 0.09);

    osc.connect(filter);
    filter.connect(gain);
    gain.connect(this.sfxGain);

    osc.start();
    osc.stop(this.ctx.currentTime + 0.1);
  }

  // 심장 박동음 (몬스터가 근처에 있거나 플레이어가 발견되었을 때)
  updateHeartbeat(deltaTime, nearestMonsterDistance) {
    if (!this.initialized || !this.ctx) return;

    const maxDist = 28;
    const minDist = 3;
    const clampedDist = Math.max(minDist, Math.min(maxDist, nearestMonsterDistance));
    this.monsterDistanceRatio = (clampedDist - minDist) / (maxDist - minDist); // 0 (가까움) ~ 1 (멀음)

    if (nearestMonsterDistance > maxDist) return; // 멀리 있으면 안 뜀

    this.heartbeatTimer += deltaTime;
    // 거리가 가까울수록 심장 박동 주기가 빨라짐 (0.35초 ~ 1.1초)
    const targetInterval = 0.35 + this.monsterDistanceRatio * 0.75;

    if (this.heartbeatTimer >= targetInterval) {
      this.heartbeatTimer = 0;
      this.playHeartbeatSound();
    }
  }

  playHeartbeatSound() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    
    // Thump 1
    const osc1 = this.ctx.createOscillator();
    const gain1 = this.ctx.createGain();
    osc1.type = "sine";
    osc1.frequency.setValueAtTime(75, now);
    osc1.frequency.exponentialRampToValueAtTime(30, now + 0.12);

    const vol = 0.6 * (1.0 - this.monsterDistanceRatio * 0.6);
    gain1.gain.setValueAtTime(vol, now);
    gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.14);

    osc1.connect(gain1);
    gain1.connect(this.sfxGain);
    osc1.start(now);
    osc1.stop(now + 0.15);

    // Thump 2 (부드러운 더블 박동)
    const osc2 = this.ctx.createOscillator();
    const gain2 = this.ctx.createGain();
    osc2.type = "sine";
    osc2.frequency.setValueAtTime(60, now + 0.14);
    osc2.frequency.exponentialRampToValueAtTime(25, now + 0.24);

    gain2.gain.setValueAtTime(vol * 0.7, now + 0.14);
    gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.26);

    osc2.connect(gain2);
    gain2.connect(this.sfxGain);
    osc2.start(now + 0.14);
    osc2.stop(now + 0.28);
  }

  // 상호작용 SFX (문, 열쇠, 스위치 등)
  playSFX(type) {
    if (!this.initialized || !this.ctx) return;
    this.resume();

    const now = this.ctx.currentTime;

    switch (type) {
      case "door_open": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "triangle";
        osc.frequency.setValueAtTime(120, now);
        osc.frequency.linearRampToValueAtTime(180, now + 0.25);
        gain.gain.setValueAtTime(0.3, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.3);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.3);
        break;
      }
      case "door_close": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sine";
        osc.frequency.setValueAtTime(160, now);
        osc.frequency.exponentialRampToValueAtTime(40, now + 0.18);
        gain.gain.setValueAtTime(0.4, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.2);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.2);
        break;
      }
      case "key_pickup": {
        // 밝은 금속성 벨 소리 (아르페지오)
        [523.25, 659.25, 783.99, 1046.5].forEach((freq, idx) => {
          const osc = this.ctx.createOscillator();
          const gain = this.ctx.createGain();
          osc.type = "sine";
          osc.frequency.setValueAtTime(freq, now + idx * 0.06);
          gain.gain.setValueAtTime(0.25, now + idx * 0.06);
          gain.gain.exponentialRampToValueAtTime(0.001, now + idx * 0.06 + 0.35);
          osc.connect(gain);
          gain.connect(this.sfxGain);
          osc.start(now + idx * 0.06);
          osc.stop(now + idx * 0.06 + 0.35);
        });
        break;
      }
      case "flashlight_toggle": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "square";
        osc.frequency.setValueAtTime(800, now);
        gain.gain.setValueAtTime(0.15, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.04);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.04);
        break;
      }
      case "firecracker_fuse": {
        // 치직치직 지포 라이터/폭죽 치직 소리
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(900, now);
        gain.gain.setValueAtTime(0.2, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.15);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.15);
        break;
      }
      case "firecracker_explode": {
        // 폭죽 폭발음 (쿵!)
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(150, now);
        osc.frequency.exponentialRampToValueAtTime(30, now + 0.4);
        gain.gain.setValueAtTime(0.8, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.45);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.45);
        break;
      }
      case "cabinet_enter": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "triangle";
        osc.frequency.setValueAtTime(100, now);
        osc.frequency.exponentialRampToValueAtTime(50, now + 0.2);
        gain.gain.setValueAtTime(0.3, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.22);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.22);
        break;
      }
      case "item_use": {
        // 아이템 마시기 / 적용 소리
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sine";
        osc.frequency.setValueAtTime(300, now);
        osc.frequency.linearRampToValueAtTime(600, now + 0.2);
        gain.gain.setValueAtTime(0.3, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.25);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.25);
        break;
      }
      case "heavy_thud": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sine";
        osc.frequency.setValueAtTime(90, now);
        osc.frequency.exponentialRampToValueAtTime(25, now + 0.35);
        gain.gain.setValueAtTime(0.75, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.4);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.4);
        break;
      }
      case "cat_eerie": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(450, now);
        osc.frequency.linearRampToValueAtTime(750, now + 0.25);
        osc.frequency.linearRampToValueAtTime(350, now + 0.6);
        gain.gain.setValueAtTime(0.4, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.65);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.65);
        break;
      }
      case "musicbox": {
        [659.25, 783.99, 987.77, 1318.5].forEach((freq, idx) => {
          const osc = this.ctx.createOscillator();
          const gain = this.ctx.createGain();
          osc.type = "sine";
          osc.frequency.setValueAtTime(freq, now + idx * 0.12);
          gain.gain.setValueAtTime(0.2, now + idx * 0.12);
          gain.gain.exponentialRampToValueAtTime(0.001, now + idx * 0.12 + 0.4);
          osc.connect(gain);
          gain.connect(this.sfxGain);
          osc.start(now + idx * 0.12);
          osc.stop(now + idx * 0.12 + 0.4);
        });
        break;
      }
      case "mannequin_creak": {
        const osc = this.ctx.createOscillator();
        const gain = this.ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(320, now);
        osc.frequency.linearRampToValueAtTime(140, now + 0.35);
        gain.gain.setValueAtTime(0.5, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.38);
        osc.connect(gain);
        gain.connect(this.sfxGain);
        osc.start(now);
        osc.stop(now + 0.38);
        break;
      }

      case "screamer_jumpscare": {

        // 피치 급강하 공포 점프스케어 스티어링 (불협화음 + 날카로운 톱니파)
        const osc1 = this.ctx.createOscillator();
        const osc2 = this.ctx.createOscillator();
        const gain = this.ctx.createGain();

        osc1.type = "sawtooth";
        osc2.type = "sawtooth";

        osc1.frequency.setValueAtTime(900, now);
        osc1.frequency.exponentialRampToValueAtTime(90, now + 0.8);

        osc2.frequency.setValueAtTime(945, now); // Detuned
        osc2.frequency.exponentialRampToValueAtTime(95, now + 0.8);

        gain.gain.setValueAtTime(0.9, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.9);

        osc1.connect(gain);
        osc2.connect(gain);
        gain.connect(this.sfxGain);

        osc1.start(now);
        osc2.start(now);
        osc1.stop(now + 0.9);
        osc2.stop(now + 0.9);
        break;
      }
    }
  }

  // 몬스터 괴성 (추격 시작 시)
  playMonsterRoar(enemyType = "uncat") {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;

    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    const filter = this.ctx.createBiquadFilter();

    filter.type = "bandpass";

    if (enemyType === "baby") {
      // 아기 울음 소리 기반 기괴한 고음 울음
      osc.type = "sawtooth";
      osc.frequency.setValueAtTime(450, now);
      osc.frequency.linearRampToValueAtTime(650, now + 0.25);
      osc.frequency.linearRampToValueAtTime(350, now + 0.5);
      filter.frequency.setValueAtTime(700, now);
    } else {
      // 거대한 몬스터 포효
      osc.type = "sawtooth";
      osc.frequency.setValueAtTime(140, now);
      osc.frequency.exponentialRampToValueAtTime(60, now + 0.6);
      filter.frequency.setValueAtTime(300, now);
    }

    gain.gain.setValueAtTime(0.65, now);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.65);

    osc.connect(filter);
    filter.connect(gain);
    gain.connect(this.sfxGain);

    osc.start(now);
    osc.stop(now + 0.65);
  }
}

export const soundManager = new SoundManager();
