// 위치, 진행도, 적의 경계 상태를 합쳐 공포의 밀도와 휴지기를 조절합니다.
// 조명/문/HUD만 사용하며 실제 추격 중에는 추가 점프 스케어를 얹지 않습니다.

import { HORROR_PACING_CONFIG } from "../config/gameConfig.js";
import { distance2D } from "../utils/math.js";

const AMBIENT_MESSAGES = [
  "한 칸 뒤에서 발소리가 멎었습니다.",
  "켜 둔 불빛 사이로 어둠이 자리를 옮깁니다.",
  "복도 끝의 문이 방금보다 가까워 보입니다.",
  "천장 위에서 무언가가 같은 속도로 따라옵니다.",
  "문틈 너머의 그림자가 한 박자 늦게 움직입니다.",
];

export class HorrorEventManager {
  constructor(scene, player, doors, hud, lights = []) {
    this.scene = scene;
    this.player = player;
    this.doors = doors;
    this.hud = hud;
    this.lights = lights;
    this.triggeredEvents = new Set();
    this.flickerTimer = 0;
    this.elapsed = 0;
    this.smoothedTension = 0.16;
    this.ambientCooldown = randomRange(HORROR_PACING_CONFIG.ambientCooldownRange);
    this.lastEventAt = -Infinity;
    this.lastZoneKey = null;
    this.zoneTransitions = 0;
    this.ambientEventIndex = 0;
    this.recentMessageIndices = [];
    this.lastProgress = 0;
    this.pendingProgressScare = null;
    this.flickerBurstTimer = 0;
    this.flickerBurstDuration = 0;
    this.flickerBurstStrength = 0;

    for (const light of this.lights) {
      light.userData.baseIntensity = light.intensity;
    }
  }

  update(deltaTime) {
    if (!this.player) {
      return;
    }

    this.elapsed += deltaTime;
    this.flickerTimer += deltaTime;
    this.ambientCooldown -= deltaTime;
    this.trackPlayerZone();

    const context = this.getThreatContext();
    const targetTension = this.getTension(context);
    const smoothing = 1 - Math.exp(-deltaTime * (targetTension > this.smoothedTension ? 2.8 : 0.72));
    this.smoothedTension += (targetTension - this.smoothedTension) * smoothing;

    this.updateDirector(deltaTime, this.smoothedTension, context);
    this.updateLights(this.smoothedTension);
    this.updateScriptedEvents(this.smoothedTension, context);
  }

  reset() {
    this.triggeredEvents.clear();
    this.flickerTimer = 0;
    this.elapsed = 0;
    this.smoothedTension = 0.16;
    this.ambientCooldown = randomRange(HORROR_PACING_CONFIG.ambientCooldownRange);
    this.lastEventAt = -Infinity;
    this.lastZoneKey = null;
    this.zoneTransitions = 0;
    this.ambientEventIndex = 0;
    this.recentMessageIndices = [];
    this.lastProgress = 0;
    this.pendingProgressScare = null;
    this.flickerBurstTimer = 0;
    this.flickerBurstDuration = 0;
    this.flickerBurstStrength = 0;
    for (const light of this.lights) {
      light.intensity = light.userData.baseIntensity ?? light.intensity;
    }
  }

  getThreatContext() {
    const game = window.__happyToy;
    const enemies = game?.enemyManager?.enemies || [];
    let nearestDistance = Infinity;
    let chasingCount = 0;
    let searchingCount = 0;

    for (const enemy of enemies) {
      if (enemy.isDormant || !enemy.group?.visible || !enemy.isSameLevelAs?.(this.player.position)) {
        continue;
      }
      nearestDistance = Math.min(nearestDistance, distance2D(enemy.group.position, this.player.position));
      if (enemy.state === "chase" || enemy.state === "investigateCabinet") {
        chasingCount += 1;
      } else if (enemy.state === "search" || enemy.state === "investigateNoise") {
        searchingCount += 1;
      }
    }

    const totalKeys = Math.max(1, game?.keys?.length || 4);
    const progress = Math.min(1, Math.max(0, (game?.keyCount || 0) / totalKeys));
    const nearSafeLight = (game?.safeLights || []).some((light) => (
      light.isOn && distance2D(light.position, this.player.position) <= 7.5
    ));
    return {
      game,
      progress,
      chasingCount,
      searchingCount,
      nearestDistance,
      nearSafeLight,
    };
  }

  getTension(context = this.getThreatContext()) {
    const position = this.player.position;
    const radialDepth = Math.min(1, Math.hypot(position.x, position.z) / 52);
    let tension = 0.12 + context.progress * 0.2 + radialDepth * 0.1;

    if (position.y > 2.5 || position.y < -2.5) {
      tension += 0.1;
    }
    if (position.z < -23) {
      tension += 0.1;
    }
    if (context.searchingCount > 0) {
      tension += Math.min(0.2, 0.1 + context.searchingCount * 0.04);
    }
    if (context.chasingCount > 0) {
      tension += Math.min(0.56, 0.42 + context.chasingCount * 0.08);
    }
    if (Number.isFinite(context.nearestDistance)) {
      tension += Math.max(0, 1 - context.nearestDistance / 18) * 0.2;
    }
    if (context.nearSafeLight) {
      tension -= 0.13;
    }
    if (this.player.isHidden) {
      tension -= 0.18;
    }

    return Math.max(0.06, Math.min(1, tension));
  }

  updateDirector(deltaTime, tension, context) {
    if (this.flickerBurstTimer > 0) {
      this.flickerBurstTimer = Math.max(0, this.flickerBurstTimer - deltaTime);
    }

    const keyCount = context.game?.keyCount || 0;
    if (keyCount > this.lastProgress) {
      this.lastProgress = keyCount;
      this.pendingProgressScare = {
        keyCount,
        timer: HORROR_PACING_CONFIG.progressScareDelaySeconds,
      };
    }

    if (this.pendingProgressScare) {
      this.pendingProgressScare.timer -= deltaTime;
      if (this.pendingProgressScare.timer <= 0 && context.chasingCount === 0) {
        const progressEvent = this.pendingProgressScare;
        const triggered = this.triggerEvent(
          `key-response-${progressEvent.keyCount}`,
          progressEvent.keyCount >= 3
            ? "열쇠가 맞부딪히자, 멀리 있던 발소리들이 동시에 멎습니다."
            : "주머니 속 열쇠가 저절로 한 번 울립니다.",
          { duration: 2100, flicker: true },
        );
        if (triggered) {
          this.pendingProgressScare = null;
        }
      }
    }

    if (context.chasingCount > 0) {
      this.ambientCooldown = Math.max(
        this.ambientCooldown,
        HORROR_PACING_CONFIG.chaseEventLockoutSeconds,
      );
      return;
    }

    if (
      this.elapsed >= HORROR_PACING_CONFIG.initialGraceSeconds
      && this.zoneTransitions > 0
      && this.ambientCooldown <= 0
      && this.elapsed - this.lastEventAt > 8
    ) {
      this.triggerAmbientEvent(tension, context);
    }
  }

  updateLights(tension) {
    const burstProgress = this.flickerBurstDuration > 0
      ? 1 - this.flickerBurstTimer / this.flickerBurstDuration
      : 1;
    const burstEnvelope = this.flickerBurstTimer > 0
      ? Math.sin(Math.min(1, Math.max(0, burstProgress)) * Math.PI)
      : 0;
    const strobe = Math.max(0,
      Math.sin(this.flickerTimer * 31.7 + 0.8)
      + Math.sin(this.flickerTimer * 67.1) * 0.55,
    ) / 1.55;
    const burstDip = burstEnvelope * strobe * this.flickerBurstStrength;

    for (const light of this.lights) {
      const base = light.userData.baseIntensity ?? light.intensity;
      const localDistance = distance2D(light.position, this.player.position);
      const localWeight = 1 - Math.min(1, localDistance / HORROR_PACING_CONFIG.nearLightRadius);
      const breathing = Math.sin(
        this.flickerTimer * 1.7 + light.position.x * 0.19 + light.position.z * 0.11,
      ) * 0.018 * tension;
      const distantUnease = Math.max(0, tension - 0.55) * (1 - localWeight) * 0.08;
      const dip = burstDip * (0.28 + localWeight * 0.72) + distantUnease;
      light.intensity = Math.max(0.04, base * (1 - dip + breathing));
    }
  }

  updateScriptedEvents(tension, context = this.getThreatContext()) {
    const position = this.player.position;

    if (context.chasingCount > 0) {
      return;
    }

    if (position.y <= 1.5 && position.z < -23 && !this.triggeredEvents.has("first-cicada-wall")) {
      this.triggerEvent(
        "first-cicada-wall",
        "벽 안쪽 어딘가가 마른 껍질처럼 바스락거립니다.",
        { duration: 2400, flicker: true },
      );
    }

    if (position.y > 2.5 && position.z < -18 && !this.triggeredEvents.has("upper-repeat-sign")) {
      this.triggerEvent(
        "upper-repeat-sign",
        "방금 지나온 문패가 복도 끝에 다시 걸려 있습니다.",
        { duration: 2600, flicker: true, closeDoor: true, upperOnly: true },
      );
    }

    if (tension > 0.72 && !this.triggeredEvents.has("deep-corridor-hum")) {
      this.triggerEvent(
        "deep-corridor-hum",
        "후레쉬 빛 바깥에서 복도가 조금씩 접히는 것 같습니다.",
        { duration: 2600 },
      );
    }
  }

  triggerAmbientEvent(tension, context) {
    const messageIndex = this.pickAmbientMessageIndex();
    const shouldCloseDoor = context.progress >= 0.25 && Math.random() < 0.34;
    this.triggerEvent(
      `ambient-${this.ambientEventIndex += 1}`,
      AMBIENT_MESSAGES[messageIndex],
      {
        duration: 2100 + Math.round(tension * 500),
        flicker: true,
        closeDoor: shouldCloseDoor,
      },
    );
  }

  triggerEvent(id, message, options = {}) {
    if (id && this.triggeredEvents.has(id)) {
      return false;
    }
    if (options.ignoreSpacing !== true && this.elapsed - this.lastEventAt < 2.8) {
      return false;
    }
    if (id) {
      this.triggeredEvents.add(id);
    }
    if (message) {
      this.hud?.setStatus(message, options.duration ?? 2300);
    }
    if (options.flicker) {
      this.startFlickerBurst();
    }
    if (options.closeDoor) {
      this.closeNearbyDoor({ upperOnly: Boolean(options.upperOnly) });
    }
    this.lastEventAt = this.elapsed;
    this.resetAmbientCooldown(this.smoothedTension);
    return true;
  }

  startFlickerBurst() {
    this.flickerBurstDuration = randomRange(HORROR_PACING_CONFIG.flickerBurstDurationRange);
    this.flickerBurstTimer = this.flickerBurstDuration;
    this.flickerBurstStrength = randomRange(HORROR_PACING_CONFIG.flickerBurstStrengthRange);
  }

  resetAmbientCooldown(tension = 0) {
    const scale = 1 - Math.max(0, tension - 0.45)
      * (1 - HORROR_PACING_CONFIG.highTensionCooldownScale);
    this.ambientCooldown = randomRange(HORROR_PACING_CONFIG.ambientCooldownRange) * scale;
  }

  trackPlayerZone() {
    const zoneSize = HORROR_PACING_CONFIG.zoneSize;
    const position = this.player.position;
    const floor = position.y > 2.5 ? 2 : (position.y < -2.5 ? -1 : 1);
    const zoneKey = `${floor}:${Math.floor(position.x / zoneSize)}:${Math.floor(position.z / zoneSize)}`;
    if (this.lastZoneKey === null) {
      this.lastZoneKey = zoneKey;
      return;
    }
    if (zoneKey !== this.lastZoneKey) {
      this.lastZoneKey = zoneKey;
      this.zoneTransitions += 1;
      this.ambientCooldown -= Math.min(2.5, this.zoneTransitions * 0.25);
    }
  }

  pickAmbientMessageIndex() {
    const candidates = AMBIENT_MESSAGES
      .map((_, index) => index)
      .filter((index) => !this.recentMessageIndices.includes(index));
    const pool = candidates.length > 0 ? candidates : AMBIENT_MESSAGES.map((_, index) => index);
    const chosen = pool[Math.floor(Math.random() * pool.length)];
    this.recentMessageIndices.push(chosen);
    if (this.recentMessageIndices.length > 3) {
      this.recentMessageIndices.shift();
    }
    return chosen;
  }

  closeNearbyDoor(options = {}) {
    const sameFloorTolerance = 1.8;
    const playerYaw = Number.isFinite(this.player.yaw) ? this.player.yaw : null;
    const candidates = this.doors.filter((entry) => {
      if (!entry.isOpen || entry.isLocked || entry.isBlocked || entry === this.player.currentInteractable) {
        return false;
      }
      if (options.upperOnly && entry.position.y <= 2.5) {
        return false;
      }
      if (Math.abs((entry.position.y ?? 0) - (this.player.position.y ?? 0)) > sameFloorTolerance) {
        return false;
      }
      const distance = entry.distanceTo(this.player.position);
      return distance >= 2.6 && distance <= 8.5;
    });

    candidates.sort((a, b) => {
      if (playerYaw !== null) {
        const forwardX = -Math.sin(playerYaw);
        const forwardZ = -Math.cos(playerYaw);
        const aLength = Math.max(0.001, distance2D(a.position, this.player.position));
        const bLength = Math.max(0.001, distance2D(b.position, this.player.position));
        const aDot = ((a.position.x - this.player.position.x) * forwardX
          + (a.position.z - this.player.position.z) * forwardZ) / aLength;
        const bDot = ((b.position.x - this.player.position.x) * forwardX
          + (b.position.z - this.player.position.z) * forwardZ) / bLength;
        if (Math.abs(aDot - bDot) > 0.05) {
          return aDot - bDot;
        }
      }
      return a.distanceTo(this.player.position) - b.distanceTo(this.player.position);
    });

    const door = candidates[0];
    if (!door) {
      return false;
    }
    door.isOpen = false;
    return true;
  }

  closeNearbyUpperDoor() {
    return this.closeNearbyDoor({ upperOnly: true });
  }
}

function randomRange(range) {
  return range[0] + Math.random() * (range[1] - range[0]);
}
