import { soundManager } from "../audio/SoundManager.js";

// Game-time driven: pausing and cinematics never consume a warning or recovery window.
export class DreadDirector {
  constructor(game) {
    this.game = game;
    this.reset();
  }

  reset() {
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

    // A rare answering step after a quiet interval. No repeated pop-up exposition.
    this.echoTimer -= dt;
    if (this.echoTimer <= 0 && this.phase === "quiet" && !refuge && context.nearestDistance > 20) {
      if (player.isMoving && soundManager.initialized) {
        soundManager.playStepTransient({ pan: this.stepSide * 0.65, bodyFrequency: 54,
          gritFrequency: 420, gain: 0.045, water: false });
        this.stepSide *= -1;
      }
      this.echoTimer = 28 + Math.random() * 22;
    }
  }

  updateFootsteps(dt, context) {
    this.stepTimer -= dt;
    if (this.stepTimer > 0 || !soundManager.initialized) return;
    const g = this.game;
    const p = g.player.position;
    let closest = null;
    let distance = 19;
    for (const enemy of g.enemyManager.enemies) {
      if (enemy.isDormant || !enemy.group.visible || !enemy.isSameLevelAs(p)) continue;
      if (!["chase", "patrol", "wander", "investigateNoise", "search"].includes(enemy.state)) continue;
      const d = Math.hypot(enemy.group.position.x - p.x, enemy.group.position.z - p.z);
      if (d < distance) { closest = enemy; distance = d; }
    }
    if (!closest) { this.stepTimer = 0.2; return; }
    const dx = closest.group.position.x - p.x;
    const dz = closest.group.position.z - p.z;
    const pan = (dx * Math.cos(g.player.yaw) - dz * Math.sin(g.player.yaw)) / Math.max(1, distance);
    const visible = g.collisionWorld.hasLineOfSight(closest.group.position, p);
    soundManager.playStepTransient({ pan, bodyFrequency: 48,
      gritFrequency: visible ? 560 : 190,
      gain: (1 - distance / 20) * (visible ? 0.16 : 0.07), water: false });
    this.stepTimer = closest.state === "chase" ? 0.34 : 0.68;
  }
}
