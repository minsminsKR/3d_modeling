import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";

// Floor-bound lurker for B1 / 2F. Uncat stays on 1F; this silhouette hunts the
// other two maps so hiding is required without breaking floor-separation tests.
export class FloorHuntDirector {
  constructor(game) {
    this.game = game;
    this.silhouette = null;
    this.reset();
  }

  reset() {
    this.floorTime = 0;
    this.lastFloor = 1;
    this.stepTimer = 0.4;
    this.approach = 0;
    this.angle = Math.PI * 0.28;
    this.firedClose = false;
    this.hide();
  }

  ensureSilhouette() {
    if (this.silhouette) return this.silhouette;
    const group = new THREE.Group();
    group.name = "floor-hunt-silhouette";
    const mat = new THREE.MeshStandardMaterial({
      color: 0x07040a,
      roughness: 1,
      metalness: 0,
      emissive: 0x14060c,
      emissiveIntensity: 0.16,
    });
    const torso = new THREE.Mesh(new THREE.BoxGeometry(0.44, 0.92, 0.3), mat);
    torso.position.y = 0.96;
    const head = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.34, 0.26), mat);
    head.position.y = 1.54;
    const legs = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.7, 0.24), mat);
    legs.position.y = 0.36;
    group.add(torso, head, legs);
    group.visible = false;
    this.game.scene.add(group);
    this.silhouette = group;
    return group;
  }

  hide() {
    if (this.silhouette) this.silhouette.visible = false;
  }

  update(dt) {
    const game = this.game;
    if (!game?.isStarted || game.gameOver || game.gameCleared || game.isPaused) return;
    if (game.cutsceneEvent || game.monsterIntroManager?.blocksPlayerControl) {
      this.hide();
      return;
    }
    const player = game.player;
    if (!player) return;

    if (player.isHidden) {
      this.approach = Math.max(0, this.approach - dt * 0.7);
      this.hide();
      return;
    }

    const { x, y, z } = player.position;
    const floor = y < -2.2 ? -1 : y > 3.2 ? 2 : 1;
    if (floor !== this.lastFloor) {
      this.lastFloor = floor;
      this.floorTime = 0;
      this.approach = 0;
      this.firedClose = false;
    }
    this.floorTime += dt;
    if (floor === 1 || this.floorTime < 2.2) {
      this.hide();
      return;
    }

    if (player.isSprinting) this.approach = Math.min(1, this.approach + dt * 0.28);
    this.angle += dt * (0.38 + this.approach * 0.42);
    const radius = Math.max(0.82, 12.4 - this.approach * 11.5);
    const hx = x + Math.sin(this.angle) * radius;
    const hz = z + Math.cos(this.angle) * radius;
    const hy = floor === -1 ? -5 : 5;
    const sil = this.ensureSilhouette();
    sil.position.set(hx, hy, hz);
    sil.lookAt(x, hy + 1.15, z);
    sil.visible = this.approach > 0.08 || this.floorTime > 4;

    this.stepTimer -= dt;
    if (this.stepTimer <= 0) {
      this.stepTimer = Math.max(0.28, 0.62 - this.approach * 0.28);
      soundManager.playSFX(floor === -1 ? "drip" : "blood_drip");
    }

    if (this.isPlayerLookingAt(sil.position)) {
      this.approach = Math.max(0, this.approach - dt * 0.62);
    } else {
      this.approach = Math.min(1, this.approach + dt * 0.14);
    }

    const dist = Math.hypot(hx - x, hz - z);
    if (dist < 6.4 && !this.firedClose) {
      this.firedClose = true;
      game.storyDirector?.fire(
        floor === -1 ? "b1hunt" : "f2hunt",
        "hide",
        "발소리가 꺾인 복도에서 돌아옵니다. 벽장으로.",
        floor === -1 ? "drip" : "blood_drip",
      );
    }

    if (!game.isInvincible && dist < 1.02 && this.approach > 0.78 && this.floorTime > 9) {
      game.handleCaught(floor === -1 ? "basement-lurk" : "gallery-lurk");
    }
  }

  isPlayerLookingAt(pos) {
    const player = this.game.player;
    const dx = pos.x - player.position.x;
    const dz = pos.z - player.position.z;
    const len = Math.hypot(dx, dz) || 1;
    const fx = -Math.sin(player.yaw);
    const fz = -Math.cos(player.yaw);
    return (fx * dx + fz * dz) / len > 0.52;
  }
}
