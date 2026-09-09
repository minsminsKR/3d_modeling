// Web Audio soundscape. SFX and ambience are synthesized; Korean PA lines
// load from /assets/voice/*.ogg with speechSynthesis as a fallback.

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
    this.dripTimer = 7 + Math.random() * 9;
    this.noiseBuffers = {};
    this.ambientNodes = [];
    this.babyCryNodes = null;
    this.voiceBuffers = {};
    this.voiceSource = null;
    this.voicesLoading = false;
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
      this.reverbInput.gain.value = 0.38;
      reverbReturn.gain.value = 0.3;
      reverb.buffer = this.createImpulseResponse(2.85, 3.35);

      this.bgmGain.connect(this.masterGain);
      this.sfxGain.connect(this.masterGain);
      this.reverbInput.connect(reverb);
      reverb.connect(reverbReturn);
      reverbReturn.connect(this.masterGain);
      this.masterGain.connect(limiter);
      limiter.connect(this.ctx.destination);

      this.noiseBuffers.short = this.createNoiseBuffer(0.45);
      this.noiseBuffers.long = this.createNoiseBuffer(4.2);
      this.initialized = true;
      this.startAmbientDrone();
      this.preloadVoiceClips();
    } catch (error) {
      console.warn("SoundManager init failed:", error);
    }
  }

  preloadVoiceClips() {
    if (this.voicesLoading || !this.ctx) return;
    this.voicesLoading = true;
    const keys = [
      "start", "hunt", "pa", "f1b", "b1", "f2", "nurse", "music", "faculty", "science",
      "key", "key2", "key3", "keysDone", "ritual", "ritualFail", "death", "clear",
      "hide", "stairWait", "leaveStart", "nursery", "shrine", "stairB1", "stairF2",
      "b1deep", "f2deep", "b1east", "f2south", "f1maze", "f1ring",
      "library", "washroom", "boarded", "throughClass", "gym",
      "windowHall", "skybridge", "courtyard", "memorial", "auditorium", "foyer", "atrium",
      "trophy", "arcade", "art", "practice", "studio", "broadcast",
      "darkroom", "greenroom", "homeec", "club",
      "specimen", "stagewing", "laundry", "lablink",
      "av", "supply", "counsel", "staticset", "stairhall", "nurseryhall", "dollhall",
      "archivehall", "storagehall", "teahall",
    ];
    Promise.all(keys.map(async (key) => {
      try {
        const response = await fetch(`/assets/voice/${key}.ogg`, { cache: "force-cache" });
        if (!response.ok) return;
        const bytes = await response.arrayBuffer();
        this.voiceBuffers[key] = await this.ctx.decodeAudioData(bytes.slice(0));
      } catch {
        // Browser speechSynthesis remains the fallback.
      }
    })).catch(() => {});
  }

  playVoiceLine(key) {
    if (!this.initialized || !this.ctx) return false;
    const buffer = this.voiceBuffers[key];
    if (!buffer) return false;
    try {
      if (this.voiceSource) {
        try { this.voiceSource.stop(); } catch { /* already stopped */ }
      }
      const source = this.ctx.createBufferSource();
      const gain = this.ctx.createGain();
      const filter = this.ctx.createBiquadFilter();
      source.buffer = buffer;
      filter.type = "lowpass";
      filter.frequency.value = 2800;
      gain.gain.value = 0.92;
      source.connect(filter);
      filter.connect(gain);
      gain.connect(this.sfxGain || this.masterGain);
      source.start();
      this.voiceSource = source;
      return true;
    } catch {
      return false;
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
    if (!this.ctx || this.ambientNodes.length || !this.noiseBuffers.long) return;
    try {
      const now = this.ctx.currentTime;
      this.ambientFilter = this.ctx.createBiquadFilter();
      this.ambientFilter.type = "lowpass";
      this.ambientFilter.frequency.value = 640;
      this.ambientFilter.Q.value = 0.52;

      this.threatGain = this.ctx.createGain();
      this.threatGain.gain.value = 0.7;
      this.ambientFilter.connect(this.threatGain);
      this.threatGain.connect(this.bgmGain);
      this.threatGain.connect(this.reverbInput);

      // Oppressive 40–60 Hz hallway bed (sub + slow beating + 50 Hz school wiring).
      const droneGain = this.ctx.createGain();
      droneGain.gain.value = 0.04;
      const base = this.ctx.createOscillator();
      const beating = this.ctx.createOscillator();
      const bed = this.ctx.createOscillator();
      const bedGain = this.ctx.createGain();
      base.type = "sine";
      beating.type = "sine";
      bed.type = "triangle";
      base.frequency.value = 41.6;
      beating.frequency.value = 43.02;
      bed.frequency.value = 52.4;
      bedGain.gain.value = 0.016;
      base.connect(droneGain);
      beating.connect(droneGain);
      droneGain.connect(this.ambientFilter);
      bed.connect(bedGain);
      bedGain.connect(this.ambientFilter);

      const mains = this.ctx.createOscillator();
      const mainsGain = this.ctx.createGain();
      mains.type = "sine";
      mains.frequency.value = 50;
      mainsGain.gain.value = 0.015;
      mains.connect(mainsGain);
      mainsGain.connect(this.ambientFilter);

      const hum = this.ctx.createOscillator();
      const humGain = this.ctx.createGain();
      hum.type = "sine";
      hum.frequency.value = 60;
      humGain.gain.value = 0.01;
      hum.connect(humGain);
      humGain.connect(this.ambientFilter);

      // Dying fluorescent: 100 Hz buzz with a slow flicker and tube hiss.
      const buzz = this.ctx.createOscillator();
      const buzzFilter = this.ctx.createBiquadFilter();
      const buzzGain = this.ctx.createGain();
      const buzzLfo = this.ctx.createOscillator();
      const buzzLfoDepth = this.ctx.createGain();
      buzz.type = "square";
      buzz.frequency.value = 100;
      buzzFilter.type = "bandpass";
      buzzFilter.frequency.value = 100;
      buzzFilter.Q.value = 9.2;
      buzzGain.gain.value = 0.0065;
      buzzLfo.type = "sine";
      buzzLfo.frequency.value = 0.11;
      buzzLfoDepth.gain.value = 0.0028;
      buzzLfo.connect(buzzLfoDepth);
      buzzLfoDepth.connect(buzzGain.gain);
      buzz.connect(buzzFilter);
      buzzFilter.connect(buzzGain);
      buzzGain.connect(this.ambientFilter);

      const tubeHiss = this.ctx.createBufferSource();
      const tubeBand = this.ctx.createBiquadFilter();
      const tubeGain = this.ctx.createGain();
      tubeHiss.buffer = this.noiseBuffers.long;
      tubeHiss.loop = true;
      tubeBand.type = "bandpass";
      tubeBand.frequency.value = 3850;
      tubeBand.Q.value = 0.75;
      tubeGain.gain.value = 0.007;
      tubeHiss.connect(tubeBand);
      tubeBand.connect(tubeGain);
      tubeGain.connect(this.ambientFilter);

      // Low corridor wind through vents, wet with hallway reverb.
      const wind = this.ctx.createBufferSource();
      const windHp = this.ctx.createBiquadFilter();
      const windLp = this.ctx.createBiquadFilter();
      const windGain = this.ctx.createGain();
      const windLfo = this.ctx.createOscillator();
      const windLfoDepth = this.ctx.createGain();
      const windSend = this.ctx.createGain();
      wind.buffer = this.noiseBuffers.long;
      wind.loop = true;
      wind.playbackRate.value = 0.72;
      windHp.type = "highpass";
      windHp.frequency.value = 62;
      windLp.type = "lowpass";
      windLp.frequency.value = 265;
      windLp.Q.value = 0.38;
      windGain.gain.value = 0.048;
      windLfo.type = "sine";
      windLfo.frequency.value = 0.068;
      windLfoDepth.gain.value = 0.016;
      windSend.gain.value = 0.58;
      windLfo.connect(windLfoDepth);
      windLfoDepth.connect(windGain.gain);
      wind.connect(windHp);
      windHp.connect(windLp);
      windLp.connect(windGain);
      windGain.connect(this.ambientFilter);
      windGain.connect(windSend);
      windSend.connect(this.reverbInput);

      const air = this.ctx.createBufferSource();
      const airBand = this.ctx.createBiquadFilter();
      const airGain = this.ctx.createGain();
      air.buffer = this.noiseBuffers.long;
      air.loop = true;
      airBand.type = "bandpass";
      airBand.frequency.value = 470;
      airBand.Q.value = 0.5;
      airGain.gain.value = 0.03;
      air.connect(airBand);
      airBand.connect(airGain);
      airGain.connect(this.ambientFilter);

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
      bed.start(now);
      mains.start(now);
      hum.start(now);
      buzz.start(now);
      buzzLfo.start(now);
      windLfo.start(now);
      tubeHiss.start(now);
      wind.start(now);
      air.start(now);
      whisper.start(now);
      this.ambientNodes.push(
        base, beating, bed, mains, hum, buzz, buzzLfo, windLfo,
        tubeHiss, wind, air, whisper,
      );
    } catch (error) {
      console.warn("Ambient drone failed:", error);
    }
  }

  setWhisperIntensity(intensity) {
    this.rampParam(this.whisperGain?.gain, clamp01(intensity) * 0.17, 0.12);
  }

  setChaseActive(active) {
    if (!this.initialized || !this.ctx) return;
    if (active) this.startChaseBed();
    else this.stopChaseBed();
  }

  startChaseBed() {
    if (!this.ctx || this.chaseBedOn) return;
    this.chaseBedOn = true;
    if (!this.chaseGain) {
      this.chaseGain = this.ctx.createGain();
      this.chaseGain.gain.value = 0;
      const low = this.ctx.createOscillator();
      low.type = "triangle";
      low.frequency.value = 46;
      const high = this.ctx.createOscillator();
      high.type = "sine";
      high.frequency.value = 93;
      const highGain = this.ctx.createGain();
      highGain.gain.value = 0.32;
      low.connect(this.chaseGain);
      high.connect(highGain);
      highGain.connect(this.chaseGain);
      this.chaseGain.connect(this.bgmGain);
      const now = this.ctx.currentTime;
      low.start(now);
      high.start(now);
      this.chaseNodes = [low, high];
    }
    this.rampParam(this.chaseGain.gain, 0.058, 0.16);
  }

  stopChaseBed() {
    this.chaseBedOn = false;
    this.rampParam(this.chaseGain?.gain, 0.0001, 0.32);
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
    this.dripTimer -= Math.min(deltaTime, 0.1);
    if (this.distantSoundTimer <= 0 && danger < 0.52) {
      const roll = Math.random();
      if (roll < 0.46) this.playFarMetallicHit();
      else if (roll < 0.72) this.playFarHallwaySteps();
      else if (roll < 0.88) this.playSFX("wet_drip");
      else this.playSFX("school_chime");
      this.distantSoundTimer = 8 + Math.random() * 12;
    }
    if (this.dripTimer <= 0 && danger < 0.45) {
      this.playSparseDrip();
      this.dripTimer = 9 + Math.random() * 14;
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

  playStepTransient({ pan, bodyFrequency, gritFrequency, gain, water, when }) {
    const now = when ?? this.ctx.currentTime;
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
    this.playFarMetallicHit();
  }

  playFarMetallicHit() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const pan = (Math.random() < 0.5 ? -1 : 1) * (0.52 + Math.random() * 0.4);
    const output = this.createPannedOutput(pan, 0.86);
    const muffler = this.ctx.createBiquadFilter();
    muffler.type = "lowpass";
    muffler.frequency.value = 720;
    muffler.Q.value = 0.5;
    muffler.connect(output);

    const body = this.ctx.createOscillator();
    const bodyGain = this.ctx.createGain();
    body.type = "triangle";
    body.frequency.setValueAtTime(78 + Math.random() * 22, now);
    body.frequency.exponentialRampToValueAtTime(28, now + 0.32);
    bodyGain.gain.setValueAtTime(0.07, now);
    bodyGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.4);
    body.connect(bodyGain);
    bodyGain.connect(muffler);
    body.start(now);
    body.stop(now + 0.42);

    [188, 412, 640].forEach((frequency, index) => {
      const ring = this.ctx.createOscillator();
      const ringFilter = this.ctx.createBiquadFilter();
      const ringGain = this.ctx.createGain();
      ring.type = index ? "sine" : "triangle";
      ring.frequency.setValueAtTime(frequency * (0.97 + Math.random() * 0.05), now);
      ringFilter.type = "bandpass";
      ringFilter.frequency.value = frequency;
      ringFilter.Q.value = 7.5;
      ringGain.gain.setValueAtTime(0.028 / (index + 1), now);
      ringGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.55 + index * 0.12);
      ring.connect(ringFilter);
      ringFilter.connect(ringGain);
      ringGain.connect(muffler);
      ring.start(now);
      ring.stop(now + 0.7);
    });

    const scrape = this.ctx.createBufferSource();
    const scrapeBand = this.ctx.createBiquadFilter();
    const scrapeGain = this.ctx.createGain();
    scrape.buffer = this.noiseBuffers.short;
    scrapeBand.type = "bandpass";
    scrapeBand.frequency.value = 980;
    scrapeBand.Q.value = 1.3;
    scrapeGain.gain.setValueAtTime(0.04, now);
    scrapeGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.18);
    scrape.connect(scrapeBand);
    scrapeBand.connect(scrapeGain);
    scrapeGain.connect(muffler);
    scrape.start(now);
    scrape.stop(now + 0.2);
  }

  playFarHallwaySteps() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const count = 2 + Math.floor(Math.random() * 3);
    const side = Math.random() < 0.5 ? -1 : 1;
    for (let i = 0; i < count; i += 1) {
      const closer = i / Math.max(1, count - 1);
      this.playStepTransient({
        pan: side * (0.72 - closer * 0.22),
        bodyFrequency: 46 + closer * 16,
        gritFrequency: 240 + closer * 160,
        gain: 0.026 + closer * 0.018,
        water: false,
        when: now + i * (0.44 + Math.random() * 0.1),
      });
    }
  }

  playSparseDrip() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const pan = (Math.random() < 0.5 ? -1 : 1) * (0.28 + Math.random() * 0.55);
    const output = this.createPannedOutput(pan, 0.78);
    const drop = this.ctx.createOscillator();
    const dropFilter = this.ctx.createBiquadFilter();
    const dropGain = this.ctx.createGain();
    drop.type = "sine";
    drop.frequency.setValueAtTime(190 + Math.random() * 80, now);
    drop.frequency.exponentialRampToValueAtTime(58, now + 0.22);
    dropFilter.type = "lowpass";
    dropFilter.frequency.value = 420;
    dropGain.gain.setValueAtTime(0.038, now);
    dropGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.28);
    drop.connect(dropFilter);
    dropFilter.connect(dropGain);
    dropGain.connect(output);
    drop.start(now);
    drop.stop(now + 0.3);
  }

  playSchoolChime() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const pan = (Math.random() < 0.5 ? -1 : 1) * (0.18 + Math.random() * 0.22);
    const output = this.createPannedOutput(pan, 0.96);
    const muffler = this.ctx.createBiquadFilter();
    muffler.type = "lowpass";
    muffler.frequency.value = 1650;
    muffler.Q.value = 0.45;
    muffler.connect(output);

    const notes = [783.99, 659.25, 698.46, 523.25];
    const broken = Math.floor(Math.random() * 4);
    let t = now + 0.04;
    notes.forEach((frequency, index) => {
      const stagger = index === 2 ? 0.78 : 0.58;
      const start = t;
      t += stagger + (Math.random() * 0.08 - 0.02);
      const detune = index === broken ? 0.94 : 1 + (Math.random() * 0.008 - 0.004);
      const volume = (index === broken ? 0.007 : 0.018) * (index === 3 ? 0.7 : 1);
      const osc = this.ctx.createOscillator();
      const partial = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      const partialGain = this.ctx.createGain();
      osc.type = "sine";
      osc.frequency.setValueAtTime(frequency * detune, start);
      partial.type = "triangle";
      partial.frequency.setValueAtTime(frequency * detune * (index === broken ? 1.97 : 2.005), start);
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(volume, start + 0.03);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + 1.85);
      partialGain.gain.setValueAtTime(0.0001, start);
      partialGain.gain.exponentialRampToValueAtTime(volume * 0.12, start + 0.02);
      partialGain.gain.exponentialRampToValueAtTime(0.0001, start + 0.9);
      osc.connect(gain);
      gain.connect(muffler);
      partial.connect(partialGain);
      partialGain.connect(muffler);
      osc.start(start);
      partial.start(start);
      osc.stop(start + 1.9);
      partial.stop(start + 0.95);
    });
  }

  playLockerCreak() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const pan = (Math.random() < 0.5 ? -1 : 1) * (0.22 + Math.random() * 0.46);
    const output = this.createPannedOutput(pan, 0.48);
    const duration = 0.62 + Math.random() * 0.28;

    const scrape = this.ctx.createBufferSource();
    const scrapeBand = this.ctx.createBiquadFilter();
    const scrapeHp = this.ctx.createBiquadFilter();
    const scrapeGain = this.ctx.createGain();
    scrape.buffer = this.noiseBuffers.short;
    scrape.loop = true;
    scrape.playbackRate.value = 0.55 + Math.random() * 0.2;
    scrapeBand.type = "bandpass";
    scrapeBand.frequency.setValueAtTime(920, now);
    scrapeBand.frequency.exponentialRampToValueAtTime(240, now + duration);
    scrapeBand.Q.value = 1.35;
    scrapeHp.type = "highpass";
    scrapeHp.frequency.value = 180;
    scrapeGain.gain.setValueAtTime(0.0001, now);
    scrapeGain.gain.exponentialRampToValueAtTime(0.12, now + 0.05);
    scrapeGain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    scrape.connect(scrapeHp);
    scrapeHp.connect(scrapeBand);
    scrapeBand.connect(scrapeGain);
    scrapeGain.connect(output);
    scrape.start(now);
    scrape.stop(now + duration + 0.02);

    const hinge = this.ctx.createOscillator();
    const hingeGain = this.ctx.createGain();
    hinge.type = "sawtooth";
    hinge.frequency.setValueAtTime(210 + Math.random() * 40, now);
    hinge.frequency.exponentialRampToValueAtTime(62, now + duration);
    hingeGain.gain.setValueAtTime(0.07, now);
    hingeGain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    const hingeFilter = this.ctx.createBiquadFilter();
    hingeFilter.type = "lowpass";
    hingeFilter.frequency.value = 540;
    hinge.connect(hingeFilter);
    hingeFilter.connect(hingeGain);
    hingeGain.connect(output);
    hinge.start(now);
    hinge.stop(now + duration + 0.02);

    [176, 348, 890].forEach((frequency, index) => {
      const panel = this.ctx.createOscillator();
      const panelFilter = this.ctx.createBiquadFilter();
      const panelGain = this.ctx.createGain();
      panel.type = "triangle";
      panel.frequency.setValueAtTime(frequency * (0.98 + Math.random() * 0.04), now);
      panelFilter.type = "bandpass";
      panelFilter.frequency.value = frequency;
      panelFilter.Q.value = 11;
      panelGain.gain.setValueAtTime(0.045 / (index + 1), now);
      panelGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.42 + index * 0.08);
      panel.connect(panelFilter);
      panelFilter.connect(panelGain);
      panelGain.connect(output);
      panel.start(now);
      panel.stop(now + 0.55);
    });

    const latch = this.ctx.createOscillator();
    const latchGain = this.ctx.createGain();
    const latchAt = now + duration * 0.72;
    latch.type = "square";
    latch.frequency.setValueAtTime(1480, latchAt);
    latch.frequency.exponentialRampToValueAtTime(220, latchAt + 0.04);
    latchGain.gain.setValueAtTime(0.03, latchAt);
    latchGain.gain.exponentialRampToValueAtTime(0.0001, latchAt + 0.05);
    latch.connect(latchGain);
    latchGain.connect(output);
    latch.start(latchAt);
    latch.stop(latchAt + 0.06);
  }

  playLockerKnock() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const pan = (Math.random() < 0.5 ? -1 : 1) * (0.12 + Math.random() * 0.28);
    const output = this.createPannedOutput(pan, 0.35);
    [0, 0.16 + Math.random() * 0.05].forEach((delay, index) => {
      const t = now + delay;
      const thud = this.ctx.createOscillator();
      const thudGain = this.ctx.createGain();
      const filter = this.ctx.createBiquadFilter();
      thud.type = "triangle";
      thud.frequency.setValueAtTime(index === 0 ? 92 : 78, t);
      thud.frequency.exponentialRampToValueAtTime(32, t + 0.12);
      filter.type = "lowpass";
      filter.frequency.value = 420;
      thudGain.gain.setValueAtTime(0.16, t);
      thudGain.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
      thud.connect(filter);
      filter.connect(thudGain);
      thudGain.connect(output);
      thud.start(t);
      thud.stop(t + 0.2);
    });
  }

  playDeathBreath() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const output = this.createPannedOutput(0, 0.7);
    const noise = this.ctx.createBufferSource();
    const hp = this.ctx.createBiquadFilter();
    const lp = this.ctx.createBiquadFilter();
    const gain = this.ctx.createGain();
    noise.buffer = this.noiseBuffers.long;
    noise.playbackRate.value = 0.42;
    hp.type = "highpass";
    hp.frequency.value = 90;
    lp.type = "lowpass";
    lp.frequency.value = 280;
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(0.22, now + 0.28);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 1.8);
    noise.connect(hp);
    hp.connect(lp);
    lp.connect(gain);
    gain.connect(output);
    noise.start(now);
    noise.stop(now + 1.85);
    const heart = this.ctx.createOscillator();
    const heartGain = this.ctx.createGain();
    heart.type = "sine";
    heart.frequency.value = 48;
    heartGain.gain.setValueAtTime(0.18, now);
    heartGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.55);
    heart.connect(heartGain);
    heartGain.connect(output);
    heart.start(now);
    heart.stop(now + 0.6);
  }

  playCorridorWind() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const pan = (Math.random() - 0.5) * 0.7;
    const output = this.createPannedOutput(pan, 0.88);
    const duration = 2.1 + Math.random() * 0.7;
    const gust = this.ctx.createBufferSource();
    const hp = this.ctx.createBiquadFilter();
    const lp = this.ctx.createBiquadFilter();
    const band = this.ctx.createBiquadFilter();
    const gain = this.ctx.createGain();
    gust.buffer = this.noiseBuffers.long;
    gust.playbackRate.value = 0.62 + Math.random() * 0.18;
    hp.type = "highpass";
    hp.frequency.value = 55;
    lp.type = "lowpass";
    lp.frequency.value = 340;
    band.type = "bandpass";
    band.frequency.value = 190;
    band.Q.value = 0.45;
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(0.11, now + 0.45);
    gain.gain.exponentialRampToValueAtTime(0.05, now + duration * 0.55);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    gust.connect(hp);
    hp.connect(lp);
    lp.connect(band);
    band.connect(gain);
    gain.connect(output);
    gust.start(now);
    gust.stop(now + duration + 0.02);
  }

  playDrip() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const output = this.createPannedOutput((Math.random() - 0.5) * 0.8, 0.9);
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    const lp = this.ctx.createBiquadFilter();
    osc.type = "sine";
    osc.frequency.setValueAtTime(1180 + Math.random() * 220, now);
    osc.frequency.exponentialRampToValueAtTime(180, now + 0.16);
    lp.type = "lowpass";
    lp.frequency.value = 2400;
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(0.07, now + 0.012);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.22);
    osc.connect(lp);
    lp.connect(gain);
    gain.connect(output);
    osc.start(now);
    osc.stop(now + 0.24);
  }

  playBloodDrip() {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const output = this.createPannedOutput((Math.random() - 0.5) * 0.7, 0.86);
    const osc = this.ctx.createOscillator();
    const noise = this.ctx.createBufferSource();
    const gain = this.ctx.createGain();
    const lp = this.ctx.createBiquadFilter();
    osc.type = "triangle";
    osc.frequency.setValueAtTime(220 + Math.random() * 40, now);
    osc.frequency.exponentialRampToValueAtTime(70, now + 0.28);
    noise.buffer = this.noiseBuffers.short;
    lp.type = "lowpass";
    lp.frequency.value = 520;
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(0.06, now + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.34);
    osc.connect(lp);
    noise.connect(lp);
    lp.connect(gain);
    gain.connect(output);
    osc.start(now);
    noise.start(now);
    osc.stop(now + 0.36);
    noise.stop(now + 0.36);
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
      case "flashlight_flicker":
        tone(68, 22, 0.07, 0.14, "square", 0, 0.05);
        tone(1480, 220, 0.03, 0.04, "square", 0.18, 0.02);
        break;
      case "cabinet_enter":
        tone(96, 42, 0.28, 0.2, "triangle", 0, 0.3);
        break;
      case "cabinet_scrape": {
        const scrapePan = (Math.random() < 0.5 ? -1 : 1) * (0.38 + Math.random() * 0.4);
        const scrapeNoise = this.ctx.createBufferSource();
        const scrapeBand = this.ctx.createBiquadFilter();
        const scrapeGain = this.ctx.createGain();
        scrapeNoise.buffer = this.noiseBuffers.short;
        scrapeBand.type = "bandpass";
        scrapeBand.frequency.value = 260;
        scrapeBand.Q.value = 1.05;
        scrapeGain.gain.setValueAtTime(0.1, now);
        scrapeGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.44);
        scrapeNoise.connect(scrapeBand);
        scrapeBand.connect(scrapeGain);
        scrapeGain.connect(this.createPannedOutput(scrapePan, 0.2));
        scrapeNoise.start(now);
        scrapeNoise.stop(now + 0.46);
        tone(148, 46, 0.36, 0.13, "sawtooth", scrapePan, 0.28);
        tone(72, 28, 0.22, 0.09, "sine", scrapePan * 0.55, 0.16);
        break;
      }
      case "whisper": {
        const whisperPan = (Math.random() < 0.5 ? -1 : 1) * (0.48 + Math.random() * 0.38);
        const whisperNoise = this.ctx.createBufferSource();
        const whisperBand = this.ctx.createBiquadFilter();
        const whisperBurst = this.ctx.createGain();
        whisperNoise.buffer = this.noiseBuffers.short;
        whisperBand.type = "bandpass";
        whisperBand.frequency.value = 1680;
        whisperBand.Q.value = 4.8;
        whisperBurst.gain.setValueAtTime(0.0001, now);
        whisperBurst.gain.exponentialRampToValueAtTime(0.085, now + 0.07);
        whisperBurst.gain.exponentialRampToValueAtTime(0.0001, now + 0.52);
        whisperNoise.connect(whisperBand);
        whisperBand.connect(whisperBurst);
        whisperBurst.connect(this.createPannedOutput(whisperPan, 0.68));
        whisperNoise.start(now);
        whisperNoise.stop(now + 0.54);
        tone(920, 240, 0.42, 0.028, "triangle", whisperPan * 0.7, 0.55);
        break;
      }
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
      case "baby_cry_cut":
        tone(740, 90, 0.07, 0.22, "square", -0.2, 0.08);
        tone(180, 40, 0.12, 0.16, "sine", 0.1, 0.4);
        break;
      case "baby_wrong":
        tone(1240, 70, 0.92, 0.58, "sawtooth", -0.16, 0.2);
        tone(880, 46, 0.8, 0.42, "square", 0.18, 0.16);
        tone(190, 28, 0.7, 0.3, "sine", 0, 0.55);
        break;
      case "radio_static": {
        const staticPan = (Math.random() < 0.5 ? -1 : 1) * (0.22 + Math.random() * 0.52);
        const output = this.createPannedOutput(staticPan, 0.22);
        const noise = this.ctx.createBufferSource();
        const highpass = this.ctx.createBiquadFilter();
        const band = this.ctx.createBiquadFilter();
        const notch = this.ctx.createBiquadFilter();
        const burst = this.ctx.createGain();
        noise.buffer = this.noiseBuffers.short;
        highpass.type = "highpass";
        highpass.frequency.value = 720;
        highpass.Q.value = 0.72;
        band.type = "bandpass";
        band.frequency.value = 1980;
        band.Q.value = 1.45;
        notch.type = "peaking";
        notch.frequency.value = 2650;
        notch.Q.value = 2.8;
        notch.gain.value = 6.5;
        const duration = 0.34 + Math.random() * 0.14;
        burst.gain.setValueAtTime(0.0001, now);
        burst.gain.exponentialRampToValueAtTime(0.19, now + 0.012);
        burst.gain.exponentialRampToValueAtTime(0.11, now + 0.07);
        burst.gain.exponentialRampToValueAtTime(0.0001, now + duration);
        noise.connect(highpass);
        highpass.connect(band);
        band.connect(notch);
        notch.connect(burst);
        burst.connect(output);
        noise.start(now);
        noise.stop(now + duration + 0.02);
        tone(2480, 880, 0.055, 0.045, "square", staticPan, 0.06);
        tone(160, 58, 0.11, 0.055, "sawtooth", staticPan * 0.4, 0.1);
        break;
      }
      case "wet_drip": {
        const dripPan = (Math.random() < 0.5 ? -1 : 1) * (0.16 + Math.random() * 0.5);
        const output = this.createPannedOutput(dripPan, 0.58);
        const drop = this.ctx.createOscillator();
        const dropFilter = this.ctx.createBiquadFilter();
        const dropGain = this.ctx.createGain();
        drop.type = "sine";
        drop.frequency.setValueAtTime(210 + Math.random() * 70, now);
        drop.frequency.exponentialRampToValueAtTime(64, now + 0.2);
        dropFilter.type = "lowpass";
        dropFilter.frequency.value = 480;
        dropFilter.Q.value = 0.8;
        dropGain.gain.setValueAtTime(0.085, now);
        dropGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.24);
        drop.connect(dropFilter);
        dropFilter.connect(dropGain);
        dropGain.connect(output);
        drop.start(now);
        drop.stop(now + 0.26);
        const splash = this.ctx.createBufferSource();
        const splashBand = this.ctx.createBiquadFilter();
        const splashGain = this.ctx.createGain();
        splash.buffer = this.noiseBuffers.short;
        splashBand.type = "bandpass";
        splashBand.frequency.value = 1380;
        splashBand.Q.value = 1.1;
        splashGain.gain.setValueAtTime(0.028, now);
        splashGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.09);
        splash.connect(splashBand);
        splashBand.connect(splashGain);
        splashGain.connect(output);
        splash.start(now);
        splash.stop(now + 0.12);
        break;
      }
      case "distant_cry": {
        const cryPan = (Math.random() < 0.5 ? -1 : 1) * (0.52 + Math.random() * 0.38);
        const output = this.createPannedOutput(cryPan, 0.82);
        const wail = this.ctx.createOscillator();
        const sob = this.ctx.createOscillator();
        const band = this.ctx.createBiquadFilter();
        const muffler = this.ctx.createBiquadFilter();
        const wailGain = this.ctx.createGain();
        const sobGain = this.ctx.createGain();
        wail.type = "sawtooth";
        wail.frequency.setValueAtTime(760, now);
        wail.frequency.exponentialRampToValueAtTime(940, now + 0.16);
        wail.frequency.exponentialRampToValueAtTime(490, now + 0.58);
        sob.type = "triangle";
        sob.frequency.setValueAtTime(1180, now);
        sob.frequency.exponentialRampToValueAtTime(620, now + 0.5);
        band.type = "bandpass";
        band.frequency.value = 1080;
        band.Q.value = 3.6;
        muffler.type = "lowpass";
        muffler.frequency.value = 920;
        muffler.Q.value = 0.55;
        wailGain.gain.setValueAtTime(0.0001, now);
        wailGain.gain.exponentialRampToValueAtTime(0.062, now + 0.07);
        wailGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.64);
        sobGain.gain.setValueAtTime(0.0001, now);
        sobGain.gain.exponentialRampToValueAtTime(0.018, now + 0.09);
        sobGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.48);
        wail.connect(band);
        band.connect(muffler);
        muffler.connect(wailGain);
        wailGain.connect(output);
        sob.connect(sobGain);
        sobGain.connect(muffler);
        wail.start(now);
        sob.start(now);
        wail.stop(now + 0.68);
        sob.stop(now + 0.52);
        break;
      }
      case "school_chime":
        this.playSchoolChime();
        break;
      case "locker_creak":
        this.playLockerCreak();
        break;
      case "locker_knock":
        this.playLockerKnock();
        break;
      case "death_breath":
        this.playDeathBreath();
        break;
      case "corridor_wind":
        this.playCorridorWind();
        break;
      case "drip":
        this.playDrip();
        break;
      case "blood_drip":
        this.playBloodDrip();
        break;
      default:
        break;
    }
  }

  startBabyCry({ gain = 0.1, pan = 0 } = {}) {
    this.stopBabyCry({ abrupt: true });
    if (!this.initialized || !this.ctx) return;
    this.resume();
    const output = this.createPannedOutput(pan, 0.82);
    const cryGain = this.ctx.createGain();
    cryGain.gain.value = Math.max(0.0001, gain);

    const osc = this.ctx.createOscillator();
    osc.type = "sawtooth";
    osc.frequency.value = 640;
    const lfo = this.ctx.createOscillator();
    lfo.type = "sine";
    lfo.frequency.value = 4.6;
    const lfoGain = this.ctx.createGain();
    lfoGain.gain.value = 92;
    lfo.connect(lfoGain);
    lfoGain.connect(osc.frequency);

    const wobble = this.ctx.createOscillator();
    wobble.frequency.value = 0.35;
    const wobbleGain = this.ctx.createGain();
    wobbleGain.gain.value = 0.35;
    const sob = this.ctx.createOscillator();
    sob.type = "triangle";
    sob.frequency.value = 1180;
    wobble.connect(wobbleGain);
    wobbleGain.connect(sob.frequency);

    const filter = this.ctx.createBiquadFilter();
    filter.type = "bandpass";
    filter.frequency.value = 1480;
    filter.Q.value = 2.4;
    const sobGain = this.ctx.createGain();
    sobGain.gain.value = 0.22;

    osc.connect(filter);
    filter.connect(cryGain);
    sob.connect(sobGain);
    sobGain.connect(cryGain);
    cryGain.connect(output);
    osc.start();
    lfo.start();
    wobble.start();
    sob.start();
    this.babyCryNodes = { osc, lfo, wobble, sob, cryGain };
  }

  setBabyCryGain(value) {
    if (!this.babyCryNodes?.cryGain || !this.ctx) return;
    const next = Math.max(0.0001, value);
    this.babyCryNodes.cryGain.gain.setTargetAtTime(next, this.ctx.currentTime, 0.06);
  }

  stopBabyCry({ abrupt = false } = {}) {
    if (!this.babyCryNodes) return;
    const { osc, lfo, wobble, sob, cryGain } = this.babyCryNodes;
    const now = this.ctx?.currentTime ?? 0;
    try {
      if (abrupt) {
        cryGain.gain.setValueAtTime(0.0001, now);
        osc.stop(now + 0.03);
        lfo.stop(now + 0.03);
        wobble.stop(now + 0.03);
        sob.stop(now + 0.03);
      } else {
        cryGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);
        osc.stop(now + 0.38);
        lfo.stop(now + 0.38);
        wobble.stop(now + 0.38);
        sob.stop(now + 0.38);
      }
    } catch (_error) {
      // already stopped
    }
    this.babyCryNodes = null;
  }

  playVoiceCadence(text) {
    if (!this.initialized || !this.ctx) return;
    const now = this.ctx.currentTime;
    const syllables = Math.max(4, Math.min(18, String(text || "").replace(/\s+/g, "").length / 2));
    const pan = (Math.random() < 0.5 ? -1 : 1) * 0.18;
    const output = this.createPannedOutput(pan, 0.7);
    for (let i = 0; i < syllables; i += 1) {
      const t = now + i * 0.11;
      const formant = this.ctx.createOscillator();
      const band = this.ctx.createBiquadFilter();
      const gain = this.ctx.createGain();
      formant.type = "sawtooth";
      formant.frequency.value = 118 + (i % 5) * 16 + Math.random() * 8;
      band.type = "bandpass";
      band.frequency.value = 620 + (i % 3) * 140;
      band.Q.value = 4.2;
      gain.gain.setValueAtTime(0.0001, t);
      gain.gain.exponentialRampToValueAtTime(0.045, t + 0.03);
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.09);
      formant.connect(band);
      band.connect(gain);
      gain.connect(output);
      formant.start(t);
      formant.stop(t + 0.1);
    }
    const breath = this.ctx.createBufferSource();
    const breathGain = this.ctx.createGain();
    const breathBand = this.ctx.createBiquadFilter();
    breath.buffer = this.noiseBuffers.short;
    breathBand.type = "bandpass";
    breathBand.frequency.value = 1700;
    breathBand.Q.value = 2.4;
    breathGain.gain.setValueAtTime(0.04, now);
    breathGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.7);
    breath.connect(breathBand);
    breathBand.connect(breathGain);
    breathGain.connect(output);
    breath.start(now);
    breath.stop(now + 0.72);
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
