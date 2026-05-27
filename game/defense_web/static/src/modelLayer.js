import * as THREE from "three";
import { ASSETS, LIMITS } from "./config.js";
import { cloneCharacter } from "./characterLoader.js";

const DEFAULT_ROTATION_Y = Math.PI;
const BASIC_SCALE = 1.0;
const MID_BOSS_SCALE = 1.0;
const BIG_BOSS_SCALE = 1.08;

export class ModelDecoratorSystem {
  constructor(scene, loader, options = {}) {
    this.scene = scene;
    this.loader = loader;
    this.maxSlots = options.maxSlots ?? LIMITS.modelDecorators;
    this.rotationY = options.rotationY ?? DEFAULT_ROTATION_Y;
    this.templates = {};
    this.slots = [];
    this.decoratedEnemies = new WeakSet();
    this.ready = false;
    this.loadError = null;
    this.visibleCount = 0;
    this.load();
  }

  async load() {
    try {
      const [cyclopse, hwacat, hwacatAngry] = await Promise.all([
        this.loader.load(ASSETS.cyclopse),
        this.loader.load(ASSETS.hwacat),
        this.loader.load(ASSETS.hwacatAngry),
      ]);
      this.templates = { cyclopse, hwacat, hwacatAngry };

      for (const type of createSlotPlan(this.maxSlots)) {
        const root = cloneCharacter(this.templates[type]);
        root.userData.baseScale = root.userData.baseScale?.clone?.() ?? root.scale.clone();
        root.visible = false;
        this.scene.add(root);
        
        let mixer = null;
        let action = null;
        if (root.animations && root.animations.length > 0) {
          mixer = new THREE.AnimationMixer(root);
          action = mixer.clipAction(root.animations[0]);
          action.play();
        }
        this.slots.push({ type, root, mixer, action });
      }

      this.ready = true;
      console.info("[ModelDecoratorSystem] ready", {
        slots: this.slots.map((slot) => slot.type),
      });
    } catch (error) {
      this.loadError = error;
      console.error("[ModelDecoratorSystem] failed to initialize model decorators", error);
    }
  }

  update(enemies, player, dt = 0.016) {
    this.decoratedEnemies = new WeakSet();
    this.visibleCount = 0;
    if (!this.ready) return;

    for (const slot of this.slots) {
      slot.root.visible = false;
    }

    const used = new Set();
    const active = enemies
      .filter((enemy) => enemy.active)
      .sort((a, b) => importance(b, player) - importance(a, player));

    for (const enemy of active) {
      const slotIndex = this.findSlotIndex(enemy, used);
      if (slotIndex < 0) continue;

      const slot = this.slots[slotIndex];
      used.add(slotIndex);
      this.decoratedEnemies.add(enemy);
      this.applyEnemyTransform(slot.root, enemy);
      this.visibleCount += 1;
    }

    // Update active animations
    for (const slot of this.slots) {
      if (slot.root.visible && slot.mixer) {
        slot.mixer.update(dt);
      }
    }
  }

  isDecorated(enemy) {
    return this.decoratedEnemies.has(enemy);
  }

  findSlotIndex(enemy, used) {
    const exact = this.slots.findIndex((slot, index) => {
      return !used.has(index) && slot.type === enemy.modelType;
    });
    if (exact >= 0) return exact;

    if (enemy.type === "bigBoss" || enemy.type === "midBoss") {
      console.warn("[ModelDecoratorSystem] boss has no matching FBX slot", {
        enemyType: enemy.type,
        modelType: enemy.modelType,
      });
    }
    return -1;
  }

  applyEnemyTransform(root, enemy) {
    root.visible = true;
    root.position.set(enemy.x, 0, enemy.z);
    root.rotation.set(0, this.rotationY, 0);

    const scale = enemy.type === "bigBoss"
      ? BIG_BOSS_SCALE * (enemy.scale / 2.12)
      : enemy.type === "midBoss"
        ? MID_BOSS_SCALE * (enemy.scale / 1.5)
        : enemy.type === "tank"
          ? 1.16
          : enemy.type === "fast"
            ? 0.86
            : BASIC_SCALE;
    root.scale.copy(root.userData.baseScale).multiplyScalar(scale);
  }

  getStatus() {
    return {
      ready: this.ready,
      visibleCount: this.visibleCount,
      loadError: this.loadError ? String(this.loadError?.message || this.loadError) : null,
      slots: this.slots.map((slot) => slot.type),
    };
  }
}

export function createSlotPlan(maxSlots = LIMITS.modelDecorators) {
  const safeSlots = Math.max(3, maxSlots);
  const plan = ["hwacatAngry", "hwacat"];
  if (safeSlots >= 6) {
    plan.push("hwacatAngry", "hwacat");
  }
  while (plan.length < safeSlots) {
    plan.push("cyclopse");
  }
  return plan.slice(0, safeSlots);
}

export function importance(enemy, player) {
  const boss = enemy.type === "bigBoss" ? 100000 : enemy.type === "midBoss" ? 80000 : 0;
  const dz = Math.abs(enemy.z - player.z);
  const dx = Math.abs(enemy.x - player.x);
  const forwardBias = enemy.z >= player.z - 2 ? 200 : 0;
  const visibleRange = THREE.MathUtils.clamp(90 - dz, 0, 90);
  return boss + forwardBias + visibleRange * 12 - dz * 10 - dx * 2;
}
