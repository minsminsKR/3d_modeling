// Lightweight, asset-free Web Audio soundscape. The project currently ships no
// audio files, so every layer is synthesized once and routed through shared buses.

const clamp01 = (value) => Math.max(0, Math.min(1, Number(value) || 0));

export class SoundManager {
  constructor() {
    this.ctx = null;
    this.masterGain = null;
    this.bgmGain = null;
    this.sfxGain = null;
    this.reverbInput = null;
    this.threatGain = null;
    this.ambientFilter = null;
    this.whisperGain = null;
    this.volumes = { master: 0.72, bgm: 0.48, sfx: 0.82 };
    this.initialized = false;
    this.isMuted = false;
    this.lastFootstepTime = 0;
    this.footstepSide = -1;
    this.heartbeatTimer = 0;
    this.monsterDistanceRatio = 1;
    this.distantSoundTimer = 9 + Math.random() * 8;
    this.breathTimer = 0;
    this.wasExhausted = false;
    this.noiseBuffers = {};
    this.ambientNodes = [];
  }

  init() {
    if (this.initialized) return;
    try {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (!AudioCtx) return;
      this.ctx = new AudioCtx({ latencyHint: "interactive" });

      const limiter = this.ctx.createDynamicsCompressor();
      limiter.threshold.value = -8;
      limiter.knee.value = 10;
      limiter.ratio.value = 8;
      limiter.attack.value = 0.004;
      limiter.release.value = 0.18;
      this.masterGain = this.ctx.createGain();
      this.bgmGain = this.ctx.createGain();
      this.sfxGain = this.ctx.createGain();
      this.reverbInput = this.ctx.createGain();
      const reverb = this.ctx.createConvolver();
      const reverbReturn = this.ctx.createGain();

      this.masterGain.gain.value = this.volumes.master;
      this.bgmGain.gain.value = this.volumes.bgm;
      this.sfxGain.gain.value = this.volumes.sfx;
      this.reverbInput.gain.value = 0.32;
      reverbReturn.gain.value = 0.24;
      reverb.buffer = this.createImpulseResponse(1.85, 2.8);

      this.bgmGain.connect(this.masterGain);
      this.sfxGain.connect(this.masterGain);
      this.reverbInput.connect(reverb);
      reverb.connect(reverbReturn);
      reverbReturn.connect(this.masterGain);
      this.masterGain.connect(limiter);
      limiter.connect(this.ctx.destination);

      this.noiseBuffers.short = this.createNoiseBuffer(0.45);
      this.noiseBuffers.long = this.createNoiseBuffer(3.5);
      this.initialized = true;
      this.startAmbientDrone();
    } catch (error) {
      console.warn("SoundManager init failed:", error);
    }
  }

  createNoiseBuffer(seconds) {
    const length = Math.max(1, Math.floor(this.ctx.sampleRate * seconds));
    const buffer = this.ctx.createBuffer(1, length, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    let previous = 0;
    for (let i = 0; i < length; i += 1) {
      const white = Math.random() * 2 - 1;
      previous = previous * 0.84 + white * 0.16;
      data[i] = previous * 0.82 + white * 0.18;
    }
    return buffer;
  }

  createImpulseResponse(seconds, decay) {
    const length = Math.floor(this.ctx.sampleRate * seconds);
    const impulse = this.ctx.createBuffer(2, length, this.ctx.sampleRate);
    for (let channel = 0; channel < 2; channel += 1) {
      const data = impulse.getChannelData(channel);
      for (let i = 0; i < length; i += 1) {
        const envelope = Math.pow(1 - i / length, decay);
        data[i] = (Math.random() * 2 - 1) * envelope * (channel ? 0.88 : 1);
      }
    }
    return impulse;
  }

  resume() {
    if (this.ctx?.state === "suspended") this.ctx.resume().catch(() => {});
  }

  setMasterVolume(value) {
    this.volumes.master = clamp01(value);
    this.rampParam(this.masterGain?.gain, this.isMuted ? 0 : this.volumes.master, 0.04);
  }

  setBGMVolume(value) {
    this.volumes.bgm = clamp01(value);
    this.rampParam(this.bgmGain?.gain, this.volumes.bgm, 0.06);
  }

  setSFXVolume(value) {
    this.volumes.sfx = clamp01(value);
    this.rampParam(this.sfxGain?.gain, this.volumes.sfx, 0.04);
  }

  rampParam(param, value, timeConstant = 0.04) {
    if (!param || !this.ctx) return;
    param.cancelScheduledValues(this.ctx.currentTime);
    param.setTargetAtTime(value, this.ctx.currentTime, timeConstant);
  }

  startAmbientDrone() {
    if (!this.ctx || !this.initialized || this.ambientNodes.length) return;
    const now = this.ctx.currentTime;
    this.ambientFilter = this.ctx.createBiquadFilter();
    this.ambientFilter.type = "lowpass";
    this.ambientFilter.frequency.value = 720;
    this.ambientFilter.Q.value = 0.65;

    const droneGain = this.ctx.createGain();
    droneGain.gain.value = 0.035;
    const base = this.ctx.createOscillator();
    const beating = this.ctx.createOscillator();
    base.type = "sine";
    beating.type = "triangle";
    base.frequency.value = 43.2;
    beating.frequency.value = 44.05;
    base.connect(droneGain);
    beating.connect(droneGain);
    droneGain.connect(this.ambientFilter);

    const air = this.ctx.createBufferSource();
    const airBand = this.ctx.createBiquadFilter();
    const airGain = this.ctx.createGain();
    air.buffer = this.noiseBuffers.long;
    air.loop = true;
    airBand.type = "bandpass";
    airBand.frequency.value = 510;
    airBand.Q.value = 0.55;
    airGain.gain.value = 0.038;
    air.connect(airBand);
    airBand.connect(airGain);
    airGain.connect(this.ambientFilter);

    const hum = this.ctx.createOscillator();
    const humGain = this.ctx.createGain();
    hum.type = "sine";
    hum.frequency.value = 60;
    humGain.gain.value = 0.014;
    hum.connect(humGain);
    humGain.connect(this.ambientFilter);

    this.threatGain = this.ctx.createGain();
    this.threatGain.gain.value = 0.7;
    this.ambientFilter.connect(this.threatGain);
    this.threatGain.connect(this.bgmGain);
    this.threatGain.connect(this.reverbInput);

    const whisper = this.ctx.createBufferSource();
    const whisperBand = this.ctx.createBiquadFilter();
    this.whisperGain = this.ctx.createGain();
    whisper.buffer = this.noiseBuffers.long;
    whisper.loop = true;
    whisperBand.type = "bandpass";
    whisperBand.frequency.value = 1350;
    whisperBand.Q.value = 5.4;
    this.whisperGain.gain.value = 0;
    whisper.connect(whisperBand);
    whisperBand.connect(this.whisperGain);
    this.whisperGain.connect(this.sfxGain);
    this.whisperGain.connect(this.reverbInput);

    base.start(now);
    beating.start(now);
    hum.start(now);
    air.start(now);
    whisper.start(now);
    this.ambientNodes.push(base, beating, hum, air, whisper);
  }

  setWhisperIntensity(intensity) {
    this.rampParam(this.whisperGain?.gain, clamp01(intensity) * 0.17, 0.12);
  }

  updateHeartbeat(deltaTime, nearestMonsterDistance) {
    if (!this.initialized || !this.ctx) return;
    const distance = Number.isFinite(nearestMonsterDistance) ? nearestMonsterDistance : 999;
    const danger = clamp01(1 - (distance - 3) / 25);
    this.monsterDistanceRatio = 1 - danger;
    this.rampParam(this.threatGain?.gain, 0.68 + danger * 0.26, 0.25);
    if (this.ambientFilter) {
      this.ambientFilter.frequency.setTargetAtTime(680 + danger * 280, this.ctx.currentTime, 0.3);
    }
    this.distantSoundTimer -= Math.min(deltaTime, 0.1);
    if (this.distantSoundTimer <= 0 && danger < 0.52) {
      this.playDistantKnock();
      this.distantSoundTimer = 10 + Math.random() * 18;
    }
    if (danger <= 0.02) {
      this.heartbeatTimer = Math.min(this.heartbeatTimer, 0.5);
      return;
    }
    this.heartbeatTimer += deltaTime;
    const interval = 1.12 - danger * 0.72;
    if (this.heartbeatTimer >= interval) {
      this.heartbeatTimer %= interval;
      this.playHeartbeatSound(danger);
    }
  }

  updatePlayerState(deltaTime, state = {}) {
    if (!this.initialized || !this.ctx) return;
    const stamina = clamp01(state.stamina ?? 1);
    const fatigue = 1 - stamina;
    const exertion = state.isSprinting ? 1 : state.isMoving ? 0.2 : 0;
    const hidden = Boolean(state.isHidden);
    this.breathTimer -= Math.min(deltaTime, 0.1);
    const shouldBreathe = fatigue > 0.28 || exertion > 0.5 || hidden;
    if (shouldBreathe && this.breathTimer <= 0) {
      this.playBreath(Math.max(fatigue, exertion * 0.75), hidden);
      this.breathTimer = Math.max(0.58, 1.55 - fatigue * 0.72 - exertion * 0.3);
    }
    if (fatigue > 0.88 && !this.wasExhausted) this.playSFX("exhausted");
    this.wasExhausted = fatigue > 0.68;
  }

  playFootstep(isSprinting = false) {
    if (!this.initialized || !this.ctx) return;
    const wallTime = performance.now() / 1000;
    const interval = isSprinting ? 0.29 : 0.43;
    if (wallTime - this.lastFootstepTime < interval) return;
    this.lastFootstepTime = wallTime;
    this.footstepSide *= -1;
    this.playStepTransient({ pan: this.footstepSide * 0.14, bodyFrequency: isSprinting ? 92 : 72, gritFrequency: isSprinting ? 760 : 610, gain: isSprinting ? 0.2 : 0.13, water: false });
  }

  playWaterStep(isSprinting = false) {
    if (!this.initialized || !this.ctx) return;
    const wallTime = performance.now() / 1000;
    const interval = isSprinting ? 0.3 : 0.45;
    if (wallTime - this.lastFootstepTime < interval) return;
    this.lastFootstepTime = wallTime;
    this.footstepSide *= -1;
    this.playStepTransient({ pan: this.footstepSide * 0.2, bodyFrequency: isSprinting ? 118 : 96, gritFrequency: isSprinting ? 1750 : 1380, gain: isSprinting ? 0.24 : 0.16, water: true });
  }

  playStepTransient({ pan, bodyFrequency, gritFrequency, gain, water }) {
    const now = this.ctx.currentTime;
    const output = this.createPannedOutput(pan);
    const noise = this.ctx.createBufferSource();
    const band = this.ctx.createBiquadFilter();
    const noiseGain = this.ctx.createGain();
    noise.buffer = this.noiseBuffers.short;
    band.type = water ? "bandpass" : "highpass";
    band.frequency.value = gritFrequency * (0.88 + Math.random() * 0.24);
    band.Q.value = water ? 1.4 : 0.7;
    noiseGain.gain.setValueAtTime(gain * (water ? 0.9 : 0.42), now);
    noiseGain.gain.exponentialRampToValueAtTime(0.0001, now + (water ? 0.16 : 0.085));
    noise.connect(band);
    band.connect(noiseGain);
    noiseGain.connect(output);

    const body = this.ctx.createOscillator();
    const bodyGain = this.ctx.createGain();
    body.type = "sine";
    body.frequency.setValueAtTime(bodyFrequency * (0.93 + Math.random() * 0.12), now);
    body.frequency.exponentialRampToValueAtTime(34, now + 0.085);
    bodyGain.gain.setValueAtTime(gain, now);
    bodyGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.105);
    body.connect(bodyGain);
    bodyGain.connect(output);
    noise.start(now);
    noise.stop(now + 0.2);
    body.start(now);
    body.stop(now + 0.12);
  }

  createPannedOutput(pan = 0, reverb = 0.12) {
    const panner = this.ctx.createStereoPanner?.();
    if (panner) {
      panner.pan.value = Math.max(-1, Math.min(1, pan));
      panner.connect(this.sfxGain);
      if (reverb > 0) {
        const send = this.ctx.createGain();
        send.gain.value = reverb;
        panner.connect(send);
        send.connect(this.reverbInput);
      }
      return panner;
    }
    return this.sfxGain;
  }

  playHeartbeatSound(danger = 1 - this.monsterDistanceRatio) {
    const now = this.ctx.currentTime;
    const volume = 0.13 + clamp01(danger) * 0.22;
    [0, 0.16].forEach((delay, index) => {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      const filter = this.ctx.createBiquadFilter();
      osc.type = "sine";
      osc.frequency.setValueAtTime(index ? 58 : 72, now + delay);
      osc.frequency.exponentialRampToValueAtTime(29, now + delay + 0.12);
      filter.type = "lowpass";
      filter.frequency.value = 145;
      gain.gain.setValueAtTime(volume * (index ? 0.66 : 1), now + delay);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + delay + 0.14);
      osc.connect(filter);
      filter.connect(gain);
      gain.connect(this.sfxGain);
      osc.start(now + delay);
      osc.stop(now + delay + 0.15);
    });
  }

  playBreath(intensity, hidden) {
    const now = this.ctx.currentTime;
    const noise = this.ctx.createBufferSource();
    const band = this.ctx.createBiquadFilter();
    const gain = this.ctx.createGain();
    noise.buffer = this.noiseBuffers.short;
    band.type = "bandpass";
    band.frequency.value = hidden ? 1050 : 820;
    band.Q.value = 1.1;
    const volume = (hidden ? 0.055 : 0.035) + clamp01(intensity) * 0.055;
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(volume, now + 0.12);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.42);
    noise.connect(band);
    band.connect(gain);
    gain.connect(this.createPannedOutput((Math.random() - 0.5) * 0.1, 0.04));
    noise.start(now);
    noise.stop(now + 0.44);
  }

  playDistantKnock() {
    const now = this.ctx.currentTime;
    const pan = (Math.random() < 0.5 ? -1 : 1) * (0.48 + Math.random() * 0.42);
    const output = this.createPannedOutput(pan, 0.55);
    const count = Math.random() < 0.35 ? 2 : 1;
    for (let i = 0; i < count; i += 1) {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      const t = now + i * 0.22;
      osc.type = "triangle";
      osc.frequency.setValueAtTime(64 + Math.random() * 18, t);
      osc.frequency.exponentialRampToValueAtTime(29, t + 0.2);
      gain.gain.setValueAtTime(0.075, t);
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.28);
      osc.connect(gain);
      gain.connect(output);
      osc.start(t);
      osc.stop(t + 0.3);
    }
  }

  playSFX(type) {
    if (!this.initialized || !this.ctx) return;
    this.resume();
    const now = this.ctx.currentTime;
    const tone = (start, end, duration, volume, wave = "sine", pan = 0, reverb = 0.15) => {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = wave;
      osc.frequency.setValueAtTime(start, now);
      osc.frequency.exponentialRampToValueAtTime(Math.max(1, end), now + duration);
      gain.gain.setValueAtTime(volume, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
      osc.connect(gain);
      gain.connect(this.createPannedOutput(pan, reverb));
      osc.start(now);
      osc.stop(now + duration + 0.01);
    };

    switch (type) {
      case "door_open":
        tone(105, 52, 0.42, 0.18, "triangle", -0.08, 0.42);
        tone(420, 150, 0.24, 0.035, "sawtooth", 0.1, 0.32);
        break;
      case "door_close":
        tone(132, 35, 0.18, 0.29, "sine", 0.08, 0.48);
        tone(590, 180, 0.08, 0.055, "square", -0.06, 0.2);
        break;
      case "key_pickup":
        [0, 0.07, 0.16].forEach((delay, index) => {
          const osc = this.ctx.createOscillator();
          const gain = this.ctx.createGain();
          const t = now + delay;
          osc.type = "sine";
          osc.frequency.value = [740, 988, 1318][index];
          gain.gain.setValueAtTime(0.1, t);
          gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.5);
          osc.connect(gain);
          gain.connect(this.createPannedOutput(0, 0.65));
          osc.start(t);
          osc.stop(t + 0.52);
        });
        break;
      case "flashlight_toggle":
        tone(780, 390, 0.035, 0.075, "square", 0.16, 0.03);
        break;
      case "cabinet_enter":
        tone(96, 42, 0.28, 0.2, "triangle", 0, 0.3);
        break;
      case "item_use":
        tone(290, 560, 0.2, 0.12, "sine", 0, 0.24);
        break;
      case "firecracker_fuse":
        tone(920, 480, 0.14, 0.09, "sawtooth", 0.25, 0.12);
        break;
      case "firecracker_explode":
        tone(165, 24, 0.5, 0.62, "sawtooth", 0, 0.62);
        break;
      case "heavy_thud":
        tone(88, 23, 0.42, 0.48, "sine", (Math.random() - 0.5) * 0.8, 0.62);
        break;
      case "exhausted":
        tone(128, 74, 0.22, 0.08, "triangle", 0, 0.02);
        break;
      case "musicbox":
        [659, 784, 988, 784].forEach((frequency, index) => {
          window.setTimeout(() => this.playBell(frequency, (index - 1.5) * 0.12), index * 120);
        });
        break;
      case "mannequin_creak":
        tone(330, 118, 0.46, 0.22, "sawtooth", -0.28, 0.5);
        break;
      case "cat_eerie":
        tone(510, 215, 0.68, 0.22, "sawtooth", 0.34, 0.55);
        break;
      case "screamer_jumpscare":
        tone(980, 58, 0.88, 0.68, "sawtooth", -0.12, 0.15);
        tone(1040, 63, 0.84, 0.48, "square", 0.12, 0.12);
        break;
      default:
        break;
    }
  }

  playBell(frequency, pan) {
    if (!this.ctx || this.ctx.state === "closed") return;
    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = "sine";
    osc.frequency.value = frequency;
    gain.gain.setValueAtTime(0.075, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.55);
    osc.connect(gain);
    gain.connect(this.createPannedOutput(pan, 0.72));
    osc.start(now);
    osc.stop(now + 0.56);
  }

  playMonsterRoar(enemyType = "uncat") {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const high = enemyType === "baby";
    const output = this.createPannedOutput(0, 0.64);
    [0, 1].forEach((index) => {
      const osc = this.ctx.createOscillator();
      const filter = this.ctx.createBiquadFilter();
      const gain = this.ctx.createGain();
      osc.type = "sawtooth";
      osc.frequency.setValueAtTime((high ? 470 : 136) + index * (high ? 29 : 7), now);
      osc.frequency.exponentialRampToValueAtTime(high ? 240 : 48, now + 0.72);
      filter.type = "bandpass";
      filter.frequency.value = high ? 760 : 245;
      filter.Q.value = 0.85;
      gain.gain.setValueAtTime(index ? 0.15 : 0.23, now);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.76);
      osc.connect(filter);
      filter.connect(gain);
      gain.connect(output);
      osc.start(now);
      osc.stop(now + 0.78);
    });
  }
}

export const soundManager = new SoundManager();
