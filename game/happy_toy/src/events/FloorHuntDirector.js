import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";
import { CharacterLoader } from "../loaders/CharacterLoader.js";
import { ENEMY_CONFIGS } from "../config/gameConfig.js";

// Floor-bound lurker for B1 / 2F. Uncat stays on 1F; this figure hunts the
// other two maps along walkable corridors so hiding is required.
export class FloorHuntDirector {
  constructor(game) {
    this.game = game;
    this.silhouette = null;
    this.mixer = null;
    this.modelReady = false;
    this.loadStarted = false;
    this.reset();
  }

  reset() {
    this.floorTime = 0;
    this.lastFloor = 1;
    this.stepTimer = 0.4;
    this.approach = 0;
    this.path = [];
    this.pathIndex = 0;
    this.repathAt = 0;
    this.firedClose = false;
    this.frozen = false;
    this.hide();
  }

  makeBoxFigure() {
    const group = new THREE.Group();
    group.name = "floor-hunt-silhouette";
    const mat = new THREE.MeshStandardMaterial({
      color: 0x040208,
      roughness: 1,
      metalness: 0,
      emissive: 0x18060c,
      emissiveIntensity: 0.28,
    });
    const cloak = new THREE.Mesh(new THREE.BoxGeometry(0.62, 1.55, 0.38), mat);
    cloak.position.y = 0.92;
    cloak.rotation.x = 0.08;
    const torso = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.72, 0.24), mat);
    torso.position.set(0, 1.12, 0.04);
    const head = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.28, 0.22), mat);
    head.position.set(0, 1.68, 0.06);
    const hips = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.26, 0.2), mat);
    hips.position.y = 0.52;
    const legL = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.58, 0.15), mat);
    legL.position.set(-0.11, 0.28, 0.02);
    const legR = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.58, 0.15), mat);
    legR.position.set(0.11, 0.28, -0.02);
    const armL = new THREE.Mesh(new THREE.BoxGeometry(0.11, 0.72, 0.11), mat);
    armL.position.set(-0.34, 1.05, 0.08);
    armL.rotation.z = 0.28;
    const armR = new THREE.Mesh(new THREE.BoxGeometry(0.11, 0.72, 0.11), mat);
    armR.position.set(0.34, 1.05, 0.08);
    armR.rotation.z = -0.28;
    group.add(cloak, torso, head, hips, legL, legR, armL, armR);
    group.visible = false;
    return group;
  }

  ensureSilhouette() {
    if (!this.silhouette) {
      this.silhouette = this.makeBoxFigure();
      this.game.scene.add(this.silhouette);
    }
    this.loadModel();
    return this.silhouette;
  }

  blacken(root) {
    root.traverse((child) => {
      if (!child.isMesh) return;
      const materials = Array.isArray(child.material) ? child.material : [child.material];
      for (const material of materials) {
        if (!material) continue;
        if (material.color) material.color.setHex(0x060308);
        if (material.emissive) {
          material.emissive.setHex(0x14060c);
          material.emissiveIntensity = 0.22;
        }
        material.map = null;
        material.roughness = 1;
        material.metalness = 0;
        material.needsUpdate = true;
      }
    });
  }

  installRoot(root, clip) {
    const group = new THREE.Group();
    group.name = "floor-hunt-silhouette";
    group.add(root);
    if (this.silhouette) {
      group.position.copy(this.silhouette.position);
      group.visible = this.silhouette.visible;
      this.game.scene.remove(this.silhouette);
    }
    this.game.scene.add(group);
    this.silhouette = group;
    this.modelReady = true;
    if (clip) {
      this.mixer = new THREE.AnimationMixer(root);
      const action = this.mixer.clipAction(clip);
      action.play();
    }
  }

  loadModel() {
    if (this.modelReady) return;
    if (this.loadStarted) return;
    this.loadStarted = true;
    const config = ENEMY_CONFIGS.find((item) => item.id === "uncat") || ENEMY_CONFIGS[0];
    const loader = new CharacterLoader();
    loader.load({ ...config, height: 1.78 }).then((loaded) => {
      if (!loaded?.root) {
        this.loadStarted = false;
        return;
      }
      this.blacken(loaded.root);
      const clip = loaded.actions?.chase || loaded.actions?.patrol || loaded.animations?.[0];
      this.installRoot(loaded.root, clip);
    }).catch(() => {
      this.loadStarted = false;
    });
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
      this.approach = Math.max(0, this.approach - dt * 0.8);
      this.frozen = false;
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
      this.path = [];
      this.pathIndex = 0;
    }
    this.floorTime += dt;
    if (floor === 1 || this.floorTime < 1.8) {
      this.hide();
      return;
    }

    const sil = this.ensureSilhouette();
    const hy = floor === -1 ? -5 : 5;
    if (!sil.visible) {
      const spawn = this.pickSpawn(x, z, hy);
      sil.position.set(spawn.x, hy, spawn.z);
      sil.visible = true;
    }

    const looking = this.isPlayerLookingAt(sil.position);
    const los = game.collisionWorld?.hasLineOfSight?.(player.position, sil.position) !== false;
    this.frozen = Boolean(looking && los && game.flashlightController?.enabled);
    if (this.mixer) this.mixer.timeScale = this.frozen ? 0 : 1;
    if (this.frozen) {
      this.approach = Math.max(0.12, this.approach - dt * 0.85);
    } else {
      if (player.isSprinting) this.approach = Math.min(1, this.approach + dt * 0.32);
      this.approach = Math.min(1, this.approach + dt * 0.13);
      this.advanceAlongPath(dt, player.position, hy);
    }

    sil.lookAt(x, hy + 1.1, z);
    this.mixer?.update(dt);
    this.stepTimer -= dt;
    if (this.stepTimer <= 0 && !this.frozen) {
      this.stepTimer = Math.max(0.26, 0.58 - this.approach * 0.26);
      soundManager.playSFX(floor === -1 ? "drip" : "blood_drip");
    }

    const dist = Math.hypot(sil.position.x - x, sil.position.z - z);
    if (dist < 6.5 && !this.firedClose) {
      this.firedClose = true;
      game.storyDirector?.fire(
        floor === -1 ? "b1hunt" : "f2hunt",
        "hide",
        "발소리가 꺾인 복도에서 돌아옵니다. 벽장으로.",
        floor === -1 ? "drip" : "blood_drip",
      );
    }

    if (!game.isInvincible && dist < 1.05 && this.approach > 0.72 && this.floorTime > 8 && !this.frozen) {
      game.handleCaught(floor === -1 ? "basement-lurk" : "gallery-lurk");
    }
  }

  pickSpawn(px, pz, hy) {
    const waypoints = this.game.collisionWorld?.transitionWaypoints || [];
    const floor = this.lastFloor;
    let best = null;
    let bestScore = -Infinity;
    for (const waypoint of waypoints) {
      if (waypoint.floor !== floor) continue;
      const pos = Array.isArray(waypoint.position)
        ? { x: waypoint.position[0], z: waypoint.position[2] }
        : waypoint.position;
      if (!pos) continue;
      const dist = Math.hypot(pos.x - px, pos.z - pz);
      if (dist < 7 || dist > 28) continue;
      const score = dist;
      if (score > bestScore) {
        bestScore = score;
        best = pos;
      }
    }
    if (best) return { x: best.x, z: best.z };
    return { x: px + 10, z: pz + 8 };
  }

  advanceAlongPath(dt, playerPos, hy) {
    const sil = this.silhouette;
    const world = this.game.collisionWorld;
    this.repathAt -= dt;
    if (this.repathAt <= 0 || this.pathIndex >= this.path.length) {
      this.repathAt = 0.45;
      const path = world?.findPath?.(
        sil.position,
        playerPos,
        0.34,
        { allowInterFloor: false, maxIterations: 5000, cellSize: 0.55 },
      ) || [];
      this.path = path;
      this.pathIndex = path.length > 1 ? 1 : 0;
    }
    if (!this.path.length) return;
    const speed = 1.22 + this.approach * 1.65;
    let remain = speed * dt;
    while (remain > 0 && this.pathIndex < this.path.length) {
      const goal = this.path[this.pathIndex];
      const dx = goal.x - sil.position.x;
      const dz = goal.z - sil.position.z;
      const dist = Math.hypot(dx, dz);
      if (dist <= 0.18) {
        this.pathIndex += 1;
        continue;
      }
      const step = Math.min(remain, dist);
      sil.position.x += (dx / dist) * step;
      sil.position.z += (dz / dist) * step;
      sil.position.y = hy;
      remain -= step;
      if (step === dist) this.pathIndex += 1;
    }
  }

  isPlayerLookingAt(pos) {
    const player = this.game.player;
    const dx = pos.x - player.position.x;
    const dz = pos.z - player.position.z;
    const len = Math.hypot(dx, dz) || 1;
    const fx = -Math.sin(player.yaw);
    const fz = -Math.cos(player.yaw);
    return (fx * dx + fz * dz) / len > 0.48;
  }
}
