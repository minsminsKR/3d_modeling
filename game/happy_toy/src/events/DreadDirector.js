import { soundManager } from "../audio/SoundManager.js";

// Game-time driven: pausing and cinematics never consume a warning or recovery window.
export class DreadDirector {
  constructor(game) {
    this.game = game;
    this.reset();
  }

  reset() {
    this.restoreBetrayedLights();
    this.phase = "quiet";
    this.timer = 0;
    this.fear = 0;
    this.lightScale = 1;
    this.pending = null;
    this.anchor = null;
    this.noiseCooldown = 0;
    this.stepTimer = 0;
    this.stepSide = 1;
    this.activeTime = 0;
    this.echoTimer = 24;
    this.lastCount = 0;
    this.ritualActive = false;
    this.ritualProgress = 0;
    this.phantomQueue = [];
    this.phantomDelay = 0;
    this.cabinetScrapeTimer = 5 + Math.random() * 4;
    this.wasHidden = false;
    this.safeBetrayCooldown = 3.5 + Math.random() * 3;
    this.betrayedLights = [];
    this.quietScareTimer = 14 + Math.random() * 10;
    this.huntRadioDelay = 0;
    this.huntRadioPlayed = true;
    this.game.hud?.setDread?.(0, "", "quiet");
  }

  onRelic(position, count, total) {
    this.pending = { position: position.clone(), final: count >= total };
    this.lastCount = count;
    // A second pickup doesn't erase a hunt; it queues one response after recovery.
    if (this.phase === "quiet") this.beginWarning();
  }

  beginWarning() {
    if (!this.pending) return;
    this.anchor = this.pending.position;
    this.finalReturn = this.pending.final;
    this.pending = null;
    this.phase = "warning";
    this.timer = 6;
    this.phantomQueue.length = 0;
    this.bell(146);
  }

  bell(frequency) {
    if (soundManager.initialized) soundManager.playBell(frequency, 0);
  }

  beginRitual() {
    if (this.ritualActive) return;
    this.ritualActive = true;
    this.ritualProgress = 0;
    this.game.hud.setStatus("봉인이 시작됩니다. 제단 곁에서 6초를 버티십시오. 멀어지면 중단됩니다.", 5000);
    this.game.voiceAnnouncer?.announce("ritual", "제단이 문을 삼키려 합니다. 여섯을 세십시오.");
    this.bell(110);
    if (!(this.game.isInvincible ?? this.game.testSafeMode)) this.game.enemyManager.notifyNoiseEvent(this.game.finalExit.position, 24,
      { duration: 8, source: "altar", silentFeedback: true });
  }

  update(dt) {
    const g = this.game;
    if (!g.isStarted || g.isPaused || g.gameOver || g.gameCleared
      || g.cutsceneEvent || g.monsterIntroManager?.blocksPlayerControl) return;
    dt = Math.min(Math.max(dt, 0), 0.1);
    this.activeTime += dt;
    const player = g.player;
    if (this.ritualActive) {
      const altar = g.finalExit.position;
      if (player.isHidden || Math.abs(altar.y - player.position.y) > 1.8
        || Math.hypot(altar.x - player.position.x, altar.z - player.position.z) > 2.8) {
        this.ritualActive = false;
        this.ritualProgress = 0;
        g.hud.setStatus("봉인이 끊겼습니다. 준비가 되면 제단에서 [E]로 다시 시작하십시오.", 3000);
        g.voiceAnnouncer?.announce("ritualFail", "봉인이 끊겼습니다. 제단을 떠나지 마십시오.");
      } else {
        this.ritualProgress += dt;
        if (this.ritualProgress >= 6) {
          this.ritualActive = false;
          g.hud.setDread(0, "", "quiet");
          g.clearGame();
          return;
        }
      }
    }
    const context = g.horrorEventManager.getThreatContext();
    const refuge = context.nearSafeLight || player.isHidden;
    this.timer -= dt;
    this.noiseCooldown -= dt;
    if (this.phase === "warning" && this.timer <= 0) {
      this.phase = "hunt";
      this.timer = this.finalReturn ? 28 : 16 + this.lastCount * 2;
      this.bell(73);
      this.huntRadioDelay = 1.2 + Math.random() * 1.3;
      this.huntRadioPlayed = false;
      this.triggerHuntBlackout();
      if (!(g.isInvincible ?? g.testSafeMode)) g.enemyManager.notifyNoiseEvent(this.anchor, 32, {
        duration: 10, source: "ritual", silentFeedback: true,
      });
    } else if (this.phase === "hunt" && this.timer <= 0) {
      this.phase = "recovery";
      this.timer = 20;
    } else if (this.phase === "recovery" && this.timer <= 0) {
      this.phase = "quiet";
      this.beginWarning();
    }

    if (this.phase === "hunt" && player.isSprinting && !player.isHidden && this.noiseCooldown <= 0) {
      this.noiseCooldown = 4;
      this.bell(330);
      if (!(g.isInvincible ?? g.testSafeMode)) g.enemyManager.notifyNoiseEvent(player.position, 18, {
        duration: 5, source: "relic-rattle", silentFeedback: true,
      });
    }

    const target = Math.min(1, context.chasingCount * 0.6
      + (this.phase === "hunt" ? 0.4 : this.phase === "warning" ? 0.28 : 0.05)
      + this.lastCount * 0.04 + (context.searchingCount ? 0.18 : 0)
      - (refuge ? 0.25 : 0));
    this.fear += (Math.max(0, target) - this.fear) * (1 - Math.exp(-dt * (target > this.fear ? 1.2 : 0.12)));
    // Slow voltage loss, never a blackout; flashlight and player-activated lamps remain usable.
    const targetLight = this.phase === "hunt" ? 0.72 : this.phase === "warning" ? 0.88 : 1;
    this.lightScale += (targetLight - this.lightScale) * (1 - Math.exp(-dt * 0.65));
    const message = this.ritualActive ? `봉인 중 · ${Math.ceil(6 - this.ritualProgress)}초 · 제단 곁을 지키십시오`
      : this.phase === "warning" ? "종이 울립니다 · 숨을 곳을 찾으십시오"
      : this.phase === "hunt" ? "수색 중 · 달리면 품속의 혼이 울립니다"
      : this.phase === "recovery" ? "울림이 잦아듭니다 · 실제 발소리를 확인하십시오" : "";
    g.hud.setDread(this.fear, message, this.phase);
    this.updateFootsteps(dt, context);
    this.updatePhantomEcho(dt, context);
    this.updateQuietPhaseScapes(dt, context);
    this.updateHuntRadio(dt);
    this.updateCabinetDread(dt);
    this.updateSafeLightBetrayal(dt);
  }

  triggerHuntBlackout() {
    const horror = this.game.horrorEventManager;
    horror?.startFlickerBurst?.();
    const closed = Boolean(horror?.closeNearbyDoor?.());
    if (soundManager.initialized) {
      soundManager.playSFX("whisper");
      soundManager.playSFX("heavy_thud");
      if (closed) soundManager.playSFX("door_close");
    }
    this.game.hud?.setStatus?.(
      closed ? "뒤에서 문이 닫혔습니다." : "복도의 불이 한꺼번에 꺼집니다.",
      2400,
    );
  }

  updatePhantomEcho(dt, context) {
    const player = this.game.player;
    const chasing = (context.chasingCount || 0) > 0;
    const blocked = this.phase !== "quiet" || player.isHidden || chasing;
    if (blocked) {
      this.phantomQueue.length = 0;
      this.phantomDelay = 0;
      if (this.phase !== "quiet") this.echoTimer = Math.max(this.echoTimer, 8);
      return;
    }

    if (this.phantomQueue.length > 0) {
      this.phantomDelay -= dt;
      if (this.phantomDelay > 0) return;
      const step = this.phantomQueue.shift();
      if (soundManager.initialized) {
        soundManager.playStepTransient({
          pan: step.pan,
          bodyFrequency: step.bodyFrequency,
          gritFrequency: step.gritFrequency,
          gain: step.gain,
          water: false,
        });
      }
      this.phantomDelay = this.phantomQueue.length > 0 ? step.nextDelay : 0;
      return;
    }

    this.echoTimer -= dt;
    if (this.echoTimer > 0) return;
    this.echoTimer = 28 + Math.random() * 22;
    if (context.nearestDistance <= 20) return;

    const count = 2 + Math.floor(Math.random() * 2);
    this.phantomQueue = [];
    for (let i = 0; i < count; i += 1) {
      const closer = i / Math.max(1, count - 1);
      this.phantomQueue.push({
        pan: -(0.78 - closer * 0.58),
        bodyFrequency: 50 + closer * 14,
        gritFrequency: 280 + closer * 220,
        gain: 0.028 + closer * 0.038,
        nextDelay: 0.38 + Math.random() * 0.16,
      });
    }
    this.phantomDelay = 0;
  }

  updateQuietPhaseScapes(dt, context) {
    const player = this.game.player;
    const chasing = (context.chasingCount || 0) > 0;
    if (this.phase !== "quiet" || player.isHidden || chasing) return;

    this.quietScareTimer -= dt;
    if (this.quietScareTimer > 0) return;
    this.quietScareTimer = 22 + Math.random() * 20;
    if (!soundManager.initialized) return;

    const cry = Math.random();
    const sfx = cry < 0.34 ? "distant_cry" : cry < 0.67 ? "radio_static" : "school_chime";
    soundManager.playSFX(sfx);
    this.game.hud?.setStatus?.(
      sfx === "distant_cry"
        ? "멀리서 짧은 울음이 벽을 타고 옵니다."
        : sfx === "radio_static"
          ? "죽은 수신기가 잡음을 토해냅니다."
          : "종소리가 한 음 모자란 채로 복도를 지나갑니다.",
      2200,
    );
  }

  updateHuntRadio(dt) {
    if (this.phase !== "hunt" || this.huntRadioPlayed) return;
    this.huntRadioDelay -= dt;
    if (this.huntRadioDelay > 0) return;
    this.huntRadioPlayed = true;
    if (soundManager.initialized) {
      soundManager.playSFX("radio_static");
      soundManager.playSFX("school_chime");
    }
    this.game.hud?.setStatus?.("방송: 복도에서 기다리십시오. 하교하지 않습니다.", 3200);
    this.game.voiceAnnouncer?.announce("pa", "방송입니다. 하교하지 않습니다. 복도에서 기다리십시오.");
  }

  updateCabinetDread(dt) {
    const hidden = Boolean(this.game.player?.isHidden);
    if (!hidden) {
      this.wasHidden = false;
      return;
    }
    if (!this.wasHidden) {
      this.wasHidden = true;
      this.cabinetScrapeTimer = 3.6 + Math.random() * 3.2;
    }
    this.cabinetScrapeTimer -= dt;
    if (this.cabinetScrapeTimer > 0) return;
    this.cabinetScrapeTimer = 2.2 + Math.random() * 2.4;
    if (!soundManager.initialized) return;
    soundManager.playSFX(Math.random() < 0.55 ? "locker_knock" : "cabinet_scrape");
    soundManager.playStepTransient({
      pan: (Math.random() < 0.5 ? -1 : 1) * (0.42 + Math.random() * 0.28),
      bodyFrequency: 40,
      gritFrequency: 150,
      gain: 0.03,
      water: false,
    });
  }

  updateSafeLightBetrayal(dt) {
    for (let i = this.betrayedLights.length - 1; i >= 0; i -= 1) {
      const entry = this.betrayedLights[i];
      entry.timer -= dt;
      if (entry.timer <= 0) {
        this.restoreBetrayedLight(entry.light);
        this.betrayedLights.splice(i, 1);
      } else {
        this.holdBetrayedLight(entry.light);
      }
    }

    if (this.phase !== "hunt" && this.phase !== "warning") {
      this.safeBetrayCooldown = Math.max(this.safeBetrayCooldown, 2.5);
      return;
    }

    this.safeBetrayCooldown -= dt;
    if (this.safeBetrayCooldown > 0) return;
    this.safeBetrayCooldown = 7 + Math.random() * 8;
    this.betrayNearbySafeLight();
  }

  betrayNearbySafeLight() {
    const player = this.game.player;
    const betrayedIds = new Set(this.betrayedLights.map((entry) => entry.light));
    const candidates = (this.game.safeLights || []).filter((light) => {
      if (!light?.isOn || betrayedIds.has(light)) return false;
      if (Math.abs((light.position?.y ?? 0) - (player.position?.y ?? 0)) > 2.4) return false;
      return true;
    });
    if (!candidates.length) return;

    candidates.sort((a, b) => {
      const da = Math.hypot(a.position.x - player.position.x, a.position.z - player.position.z);
      const db = Math.hypot(b.position.x - player.position.x, b.position.z - player.position.z);
      return da - db;
    });
    const pool = candidates.slice(0, Math.min(3, candidates.length));
    const light = pool[Math.floor(Math.random() * pool.length)];
    light.setFlickerState?.(0.05);
    light.isOn = false;
    this.killSafeLightPool(light);
    this.betrayedLights.push({ light, timer: 2 + Math.random() * 2 });
  }

  holdBetrayedLight(light) {
    if (!light) return;
    if (light.isOn) light.setFlickerState?.(0.05);
    light.isOn = false;
    this.killSafeLightPool(light);
  }

  killSafeLightPool(light) {
    const pos = light.getLightWorldPosition?.() || light.position;
    if (!pos) return;
    for (const pointLight of this.game._safeLightPool || []) {
      if (!pointLight || pointLight.position.y < -9000) continue;
      const dx = pointLight.position.x - pos.x;
      const dz = pointLight.position.z - pos.z;
      if (dx * dx + dz * dz < 0.64) pointLight.intensity = 0;
    }
  }

  restoreBetrayedLight(light) {
    if (!light) return;
    light.isOn = true;
    light.setFlickerState?.(1);
  }

  restoreBetrayedLights() {
    for (const entry of this.betrayedLights || []) {
      this.restoreBetrayedLight(entry.light);
    }
    this.betrayedLights = [];
  }

  updateFootsteps(dt, context) {
    this.stepTimer -= dt;
    if (this.stepTimer > 0 || !soundManager.initialized) return;
    const g = this.game;
    const p = g.player.position;
    let closestPos = null;
    let closestState = "patrol";
    let distance = 28;
    for (const enemy of g.enemyManager.enemies) {
      if (enemy.isDormant || !enemy.group.visible || !enemy.isSameLevelAs(p)) continue;
      if (!["chase", "patrol", "wander", "investigateNoise", "search"].includes(enemy.state)) continue;
      const d = Math.hypot(enemy.group.position.x - p.x, enemy.group.position.z - p.z);
      if (d < distance) {
        closestPos = enemy.group.position;
        closestState = enemy.state;
        distance = d;
      }
    }
    const hunt = g.floorHuntDirector?.silhouette;
    if (hunt?.visible && Math.abs((hunt.position?.y || 0) - p.y) < 2.4) {
      const d = Math.hypot(hunt.position.x - p.x, hunt.position.z - p.z);
      if (d < distance) {
        closestPos = hunt.position;
        closestState = "chase";
        distance = d;
      }
    }
    if (!closestPos) { this.stepTimer = 0.2; return; }
    const dx = closestPos.x - p.x;
    const dz = closestPos.z - p.z;
    const pan = (dx * Math.cos(g.player.yaw) - dz * Math.sin(g.player.yaw)) / Math.max(1, distance);
    const visible = g.collisionWorld.hasLineOfSight(closestPos, p);
    const chasing = closestState === "chase";
    const closeness = 1 - distance / 28;
    soundManager.playStepTransient({
      pan,
      bodyFrequency: chasing ? 34 : 50,
      gritFrequency: visible ? 480 : 150,
      gain: closeness * (chasing ? (visible ? 0.62 : 0.42) : (visible ? 0.26 : 0.18)),
      water: p.y < -2.2,
    });
    this.stepTimer = chasing ? 0.2 : 0.54;
  }
}
