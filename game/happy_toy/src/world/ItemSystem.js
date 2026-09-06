import * as THREE from "three";
import { soundManager } from "../audio/SoundManager.js";

export const ITEM_TYPES = {
  BATTERY: {
    id: "battery",
    name: "건전지",
    description: "손전등의 배터리를 100% 충전합니다.",
    color: 0x33cc33,
  },
  ENERGY_DRINK: {
    id: "energy_drink",
    name: "에너지 드링크",
    description: "스태미나를 즉시 회복하고 5초간 이동 속도가 증가합니다.",
    color: 0xffaa00,
  },
  FIRECRACKER: {
    id: "firecracker",
    name: "폭죽",
    description: "던지면 폭발 소리로 근처 몬스터의 시선을 끕니다. (Q 키로 즉시 투척)",
    color: 0xff3333,
  },
  COMPASS: {
    id: "compass",
    name: "영혼의 나침반",
    description: "가장 가까운 열쇠/장난감의 위치를 나침반 바늘로 가리킵니다.",
    color: 0x3399ff,
  },
};

export class ItemPickup {
  constructor(id, type, position) {
    this.id = id;
    this.type = type; // key of ITEM_TYPES
    this.position = new THREE.Vector3().copy(position);
    this.group = new THREE.Group();
    this.group.position.copy(this.position);
    this.collected = false;

    const info = ITEM_TYPES[type.toUpperCase()] || ITEM_TYPES.BATTERY;
    this.mesh = createPickupVisual(type, info);
    this.mesh.position.y = 0.22;
    this.group.add(this.mesh);

    // A very small locator glow keeps supplies readable without turning them
    // into arcade pickups or consuming a meaningful part of the light budget.
    this.light = new THREE.PointLight(info.color, 0.28, 1.45, 2);
    this.light.position.y = 0.28;
    this.group.add(this.light);

    const haloMaterial = new THREE.MeshStandardMaterial({
      color: info.color,
      emissive: info.color,
      emissiveIntensity: 0.12,
      transparent: true,
      opacity: 0.12,
      roughness: 0.95,
      metalness: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.halo = new THREE.Mesh(new THREE.RingGeometry(0.16, 0.29, 24), haloMaterial);
    this.halo.rotation.x = -Math.PI / 2;
    this.halo.position.y = 0.012;
    this.group.add(this.halo);

    this.initialY = this.position.y + 0.2;
    this.floatTimer = Math.random() * Math.PI * 2;
  }

  update(deltaTime) {
    if (this.collected) return;
    this.floatTimer += deltaTime * 1.65;
    this.mesh.rotation.y += deltaTime * 0.55;
    this.mesh.position.y = 0.22 + Math.sin(this.floatTimer) * 0.025;
    this.halo.material.opacity = 0.085 + (Math.sin(this.floatTimer * 1.4) + 1) * 0.022;
  }

  getPrompt() {
    const info = ITEM_TYPES[this.type.toUpperCase()];
    return `[E] ${info ? info.name : "아이템"} 획득`;
  }

  interact(context) {
    if (this.collected) return false;
    this.collected = true;
    this.group.visible = false;
    soundManager.playSFX("key_pickup");

    if (context && context.itemSystem) {
      context.itemSystem.addItemToInventory(this.type);
    }
    return true;
  }
}

export class FirecrackerProjectile {
  constructor(scene, startPos, direction, enemyManager) {
    this.scene = scene;
    this.position = new THREE.Vector3().copy(startPos);
    this.velocity = new THREE.Vector3().copy(direction).multiplyScalar(14);
    this.velocity.y += 3.5; // 약간의 곡사포
    this.floorY = startPos.y - 1.5 + 0.08;
    this.enemyManager = enemyManager;
    this.alive = true;
    this.exploded = false;
    this.lifeTimer = 0;
    this.fuseTime = 1.2; // 1.2초 후 폭발
    this.postTimer = 0;
    this.bounceCount = 0;

    // Visual Mesh
    const geom = new THREE.CylinderGeometry(0.04, 0.04, 0.2, 8);
    const mat = new THREE.MeshStandardMaterial({ color: 0xff2222, emissive: 0x880000, roughness: 0.4 });
    this.mesh = new THREE.Mesh(geom, mat);

    this.mesh.position.copy(this.position);
    this.scene.add(this.mesh);

    // Fuse Sparks Light
    this.light = new THREE.PointLight(0xffaa00, 1.4, 3.2, 2);
    this.mesh.add(this.light);

    soundManager.playSFX("firecracker_fuse");
  }

  update(deltaTime) {
    if (!this.alive) return;
    if (this.exploded) {
      this.postTimer += deltaTime;
      const progress = Math.min(1, this.postTimer / 0.32);
      this.flashLight.intensity = 18 * Math.pow(1 - progress, 2);
      this.shockwave.scale.setScalar(1 + progress * 8);
      this.shockwave.material.opacity = 0.38 * (1 - progress);
      if (progress >= 1) {
        this.dispose();
        this.alive = false;
      }
      return;
    }
    this.lifeTimer += deltaTime;

    // Gravity & Translation
    this.velocity.y -= 9.8 * deltaTime;
    this.position.addScaledVector(this.velocity, deltaTime);

    // Floor collision
    if (this.position.y <= this.floorY) {
      this.position.y = this.floorY;
      if (this.bounceCount < 1 && Math.abs(this.velocity.y) > 1.2) {
        this.velocity.y = Math.abs(this.velocity.y) * 0.24;
        this.velocity.x *= 0.48;
        this.velocity.z *= 0.48;
        this.bounceCount += 1;
      } else {
        this.velocity.set(0, 0, 0);
      }
    }

    this.mesh.position.copy(this.position);
    this.mesh.rotation.x += deltaTime * 8;
    this.light.intensity = 0.65 + Math.random() * 1.25;

    if (this.lifeTimer >= this.fuseTime) {
      this.explode();
    }
  }

  explode() {
    if (this.exploded) return;
    this.exploded = true;
    this.mesh.visible = false;
    soundManager.playSFX("firecracker_explode");

    this.flashLight = new THREE.PointLight(0xff6a20, 18, 18, 2);
    this.flashLight.position.copy(this.position).add(new THREE.Vector3(0, 0.25, 0));
    this.scene.add(this.flashLight);

    this.shockwave = new THREE.Mesh(
      new THREE.RingGeometry(0.12, 0.2, 28),
      new THREE.MeshStandardMaterial({
        color: 0xffc06a,
        emissive: 0xff6a20,
        emissiveIntensity: 0.7,
        transparent: true,
        opacity: 0.38,
        roughness: 0.8,
        metalness: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      }),
    );
    this.shockwave.rotation.x = -Math.PI / 2;
    this.shockwave.position.copy(this.position).add(new THREE.Vector3(0, 0.018, 0));
    this.scene.add(this.shockwave);

    // Distract nearby enemies
    if (this.enemyManager) {
      this.enemyManager.notifyNoiseEvent(this.position, 28.0, {
        duration: 7.5,
        source: "firecracker",
      });
    }
  }

  dispose() {
    this.scene.remove(this.mesh, this.flashLight, this.shockwave);
    this.mesh.traverse((child) => {
      child.geometry?.dispose();
      if (Array.isArray(child.material)) child.material.forEach((material) => material.dispose?.());
      else child.material?.dispose?.();
    });
    this.shockwave?.geometry?.dispose();
    this.shockwave?.material?.dispose();
  }
}

export class ItemSystem {
  constructor(scene, enemyManager, hud) {
    this.scene = scene;
    this.enemyManager = enemyManager;
    this.hud = hud;
    this.pickups = [];
    this.projectiles = [];
    this.inventory = {
      battery: 2,
      energy_drink: 1,
      firecracker: 2,
      compass: 1,
    };
    this.activeBoostTimer = 0;
  }

  spawnPickups(spawnPoints) {
    const itemPool = ["battery", "energy_drink", "firecracker", "battery", "firecracker"];
    spawnPoints.forEach((pt, idx) => {
      const type = itemPool[idx % itemPool.length];
      const pickup = new ItemPickup(`item_${idx}`, type, new THREE.Vector3(pt[0], pt[1], pt[2]));
      this.pickups.push(pickup);
      this.scene.add(pickup.group);
    });
  }

  addItemToInventory(type) {
    const key = type.toLowerCase();
    if (this.inventory[key] !== undefined) {
      this.inventory[key] += 1;
    } else {
      this.inventory[key] = 1;
    }
    if (this.hud) {
      this.hud.updateInventory(this.inventory);
      const info = ITEM_TYPES[type.toUpperCase()];
      this.hud.setStatus(`${info ? info.name : type}을(를) 획득했습니다!`);
    }
  }

  useItem(type, player, flashlight) {
    const key = type.toLowerCase();
    if (!this.inventory[key] || this.inventory[key] <= 0) {
      if (this.hud) this.hud.setStatus(`${ITEM_TYPES[type.toUpperCase()]?.name || type} 이(가) 없습니다!`);
      return false;
    }

    switch (key) {
      case "battery": {
        if (flashlight) {
          if (flashlight.batteryLevel >= 0.92) {
            this.hud?.setStatus("아직 건전지를 교체할 필요가 없습니다.", 1400);
            return false;
          }
          flashlight.rechargeBattery(1.0); // 100% 충전
          this.consumeItem("battery");
          soundManager.playSFX("item_use");
          if (this.hud) this.hud.setStatus("손전등 배터리를 완전 충전했습니다.");
          return true;
        }
        break;
      }
      case "energy_drink": {
        if (player) {
          if (player.stamina >= 0.94 && player.speedBoostTimer > 0.5) {
            this.hud?.setStatus("이미 몸이 충분히 각성되어 있습니다.", 1400);
            return false;
          }
          player.restoreStamina(1.0); // 100% 회복
          player.applySpeedBoost(5.0, 1.4); // 5초간 1.4배 속도
          this.consumeItem("energy_drink");
          soundManager.playSFX("item_use");
          if (this.hud) this.hud.setStatus("에너지 드링크! 스태미나 회복 & 이동속도 증가!");
          return true;
        }
        break;
      }
      case "firecracker": {
        if (player) {
          this.consumeItem("firecracker");
          this.throwFirecracker(player);
          return true;
        }
        break;
      }
      case "compass": {
        if (this.hud) {
          this.hud.toggleCompass();
          this.hud.setStatus("영혼의 나침반을 확인합니다.");
          return true;
        }
        break;
      }
    }
    return false;
  }

  throwFirecracker(player) {
    const eyePos = player.getPosition().clone().add(new THREE.Vector3(0, 1.45, 0));
    const dir = player.getForwardVector();
    const proj = new FirecrackerProjectile(this.scene, eyePos, dir, this.enemyManager);
    this.projectiles.push(proj);
  }

  consumeItem(key) {
    this.inventory[key] = Math.max(0, (this.inventory[key] || 0) - 1);
    this.hud?.updateInventory(this.inventory);
  }

  reset() {
    for (const projectile of this.projectiles) projectile.dispose();
    this.projectiles.length = 0;
    this.inventory = { battery: 2, energy_drink: 1, firecracker: 2, compass: 1 };
    for (const pickup of this.pickups) {
      pickup.collected = false;
      pickup.group.visible = true;
    }
    this.hud?.updateInventory(this.inventory);
    if (this.hud?.compassActive) this.hud.toggleCompass();
  }

  update(deltaTime) {
    // Pickups float update
    for (const pickup of this.pickups) {
      pickup.update(deltaTime);
    }

    // Projectiles flight update
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const proj = this.projectiles[i];
      proj.update(deltaTime);
      if (!proj.alive) {
        this.projectiles.splice(i, 1);
      }
    }
  }

  getInteractables() {
    return this.pickups.filter((p) => !p.collected);
  }
}

function createPickupVisual(type, info) {
  const group = new THREE.Group();
  const casing = new THREE.MeshStandardMaterial({
    color: info.color,
    emissive: info.color,
    emissiveIntensity: 0.08,
    roughness: 0.48,
    metalness: 0.35,
  });
  const dark = new THREE.MeshStandardMaterial({ color: 0x171512, roughness: 0.8, metalness: 0.18 });
  const pale = new THREE.MeshStandardMaterial({ color: 0xd6c9a5, roughness: 0.72, metalness: 0.06 });
  const key = type.toLowerCase();

  if (key === "battery") {
    const body = new THREE.Mesh(new THREE.CylinderGeometry(0.065, 0.065, 0.24, 12), casing);
    body.rotation.z = Math.PI / 2;
    group.add(body);
    const cap = new THREE.Mesh(new THREE.CylinderGeometry(0.038, 0.038, 0.018, 10), pale);
    cap.rotation.z = Math.PI / 2;
    cap.position.x = 0.129;
    group.add(cap);
  } else if (key === "energy_drink") {
    const can = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, 0.24, 16), casing);
    group.add(can);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(0.059, 0.006, 5, 16), pale);
    rim.rotation.x = Math.PI / 2;
    rim.position.y = 0.121;
    group.add(rim);
  } else if (key === "firecracker") {
    for (let i = -1; i <= 1; i += 1) {
      const stick = new THREE.Mesh(new THREE.CylinderGeometry(0.027, 0.027, 0.22, 8), casing);
      stick.position.x = i * 0.055;
      stick.rotation.z = i * 0.06;
      group.add(stick);
    }
    const fuse = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, 0.12, 6), dark);
    fuse.position.y = 0.16;
    fuse.rotation.z = 0.22;
    group.add(fuse);
  } else {
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.105, 0.018, 8, 24), casing);
    ring.rotation.x = Math.PI / 2;
    group.add(ring);
    const needle = new THREE.Mesh(new THREE.ConeGeometry(0.035, 0.16, 6), pale);
    needle.rotation.z = -Math.PI / 2;
    needle.position.x = 0.035;
    group.add(needle);
  }
  group.traverse((child) => {
    if (child.isMesh) {
      child.castShadow = true;
      child.receiveShadow = true;
    }
  });
  return group;
}
