import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { distance2D } from "../utils/math.js";

export class LovelyDollIntroEvent {
  constructor(game) {
    this.game = game;
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;

    this.triggerPosition = new THREE.Vector3(-32.0, 0.0, 26.5);
    this.triggerRadius = 4.2;
  }

  get blocksPlayerControl() {
    return false;
  }

  update(deltaTime) {
    if (this.state === "done") return;
    if (this.state === "idle") this.checkTrigger();
  }

  checkTrigger() {
    if (this.hasTriggered || !this.game.player || this.game.player.isHidden) return;
    const playerPos = this.game.player.position;
    if (Math.abs(playerPos.y - this.triggerPosition.y) > 1.6) return;
    if (distance2D(playerPos, this.triggerPosition) <= this.triggerRadius) {
      this.triggerEvent();
    }
  }

  triggerEvent() {
    this.hasTriggered = true;
    this.state = "done";
    this.isControlLocked = false;

    soundManager.playSFX("musicbox");
    this.game.hud?.setStatus("놀이방의 작은 인형이 손을 흔듭니다. 눈을 맞추면 길을 안내합니다.", 4200);

    const doll = this.game.lovelyDolls?.find((entry) => (
      entry.id === "lovely_doll_playroom" || entry.id === "lovely_doll_1"
    ));
    doll?.wakeUp?.();
  }

  reset() {
    this.hasTriggered = false;
    this.state = "idle";
    this.timer = 0;
    this.isControlLocked = false;
  }
}
