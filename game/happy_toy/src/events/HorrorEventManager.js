// 위치와 시간에 따라 공포 이벤트를 조절합니다.
// 빛 깜빡임, 반복 복도 느낌의 상태 메시지와 scripted scare를 담당합니다.

import { distance2D } from "../utils/math.js";

export class HorrorEventManager {
  constructor(scene, player, doors, hud, lights = []) {
    this.scene = scene;
    this.player = player;
    this.doors = doors;
    this.hud = hud;
    this.lights = lights;
    this.triggeredEvents = new Set();
    this.flickerTimer = 0;

    for (const light of this.lights) {
      light.userData.baseIntensity = light.intensity;
    }
  }

  update(deltaTime) {
    if (!this.player) {
      return;
    }

    this.flickerTimer += deltaTime;
    const tension = this.getTension();
    this.updateLights(tension);
    this.updateScriptedEvents(tension);
  }

  reset() {
    this.triggeredEvents.clear();
    this.flickerTimer = 0;
    for (const light of this.lights) {
      light.intensity = light.userData.baseIntensity ?? light.intensity;
    }
  }

  getTension() {
    const position = this.player.position;
    let tension = 0.18;

    if (position.y > 2.5) {
      tension += 0.16;
    }
    if (position.z < -18) {
      tension += 0.22;
    }
    if (position.z < -28) {
      tension += 0.2;
    }
    if (distance2D(position, { x: 0, z: -36 }) < 8) {
      tension += 0.18;
    }

    return Math.max(0, Math.min(1, tension));
  }

  updateLights(tension) {
    const flicker = (
      Math.sin(this.flickerTimer * 17.3)
      + Math.sin(this.flickerTimer * 31.7 + 1.1)
      + Math.sin(this.flickerTimer * 8.9 + 2.4)
    ) / 3;
    const dip = Math.max(0, flicker) * tension * 0.38;

    for (const light of this.lights) {
      const base = light.userData.baseIntensity ?? light.intensity;
      const localJitter = Math.sin(this.flickerTimer * 5.7 + light.position.x * 0.31 + light.position.z * 0.13) * 0.04;
      light.intensity = Math.max(0.05, base * (1 - dip + localJitter * tension));
    }
  }

  updateScriptedEvents(tension) {
    const position = this.player.position;

    if (position.y <= 1.5 && position.z < -23 && !this.triggeredEvents.has("first-cicada-wall")) {
      this.triggeredEvents.add("first-cicada-wall");
      this.hud?.setStatus("벽 안쪽 어딘가가 마른 껍질처럼 바스락거립니다.", 2400);
    }

    if (position.y > 2.5 && position.z < -18 && !this.triggeredEvents.has("upper-repeat-sign")) {
      this.triggeredEvents.add("upper-repeat-sign");
      this.hud?.setStatus("방금 지나온 문패가 복도 끝에 다시 걸려 있습니다.", 2600);
      this.closeNearbyUpperDoor();
    }

    if (tension > 0.72 && !this.triggeredEvents.has("deep-corridor-hum")) {
      this.triggeredEvents.add("deep-corridor-hum");
      this.hud?.setStatus("후레쉬 빛 바깥에서 복도가 조금씩 접히는 것 같습니다.", 2600);
    }
  }

  closeNearbyUpperDoor() {
    const door = this.doors.find((entry) => (
      entry.position.y > 2.5
      && entry.isOpen
      && !entry.isLocked
      && !entry.isBlocked
      && entry.distanceTo(this.player.position) < 7
    ));

    if (!door) {
      return;
    }

    door.isOpen = false;
  }
}
