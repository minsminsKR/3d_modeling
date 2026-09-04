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

    // Visual Mesh
    const info = ITEM_TYPES[type.toUpperCase()] || ITEM_TYPES.BATTERY;
    const geometry = new THREE.CylinderGeometry(0.12, 0.12, 0.22, 12);
    const material = new THREE.MeshStandardMaterial({
      color: info.color,
      emissive: info.color,
      emissiveIntensity: 0.4,
      roughness: 0.3,
      metalness: 0.5,
    });

    this.mesh = new THREE.Mesh(geometry, material);
    this.mesh.position.y = 0.2;
    this.group.add(this.mesh);

    // Floating Light
    this.light = new THREE.PointLight(info.color, 1.2, 3.5);
    this.light.position.y = 0.4;
    this.group.add(this.light);

    this.initialY = this.position.y + 0.2;
    this.floatTimer = Math.random() * Math.PI * 2;
  }

  update(deltaTime) {
    if (this.collected) return;
    this.floatTimer += deltaTime * 2.5;
    this.mesh.rotation.y += deltaTime * 1.8;
    this.mesh.position.y = 0.2 + Math.sin(this.floatTimer) * 0.06;
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
    this.enemyManager = enemyManager;
    this.alive = true;
    this.lifeTimer = 0;
    this.fuseTime = 1.2; // 1.2초 후 폭발

    // Visual Mesh
    const geom = new THREE.CylinderGeometry(0.04, 0.04, 0.2, 8);
    const mat = new THREE.MeshStandardMaterial({ color: 0xff2222, emissive: 0x880000, roughness: 0.4 });
    this.mesh = new THREE.Mesh(geom, mat);

    this.mesh.position.copy(this.position);
    this.scene.add(this.mesh);

    // Fuse Sparks Light
    this.light = new THREE.PointLight(0xffaa00, 2.0, 4.0);
    this.mesh.add(this.light);

    soundManager.playSFX("firecracker_fuse");
  }

  update(deltaTime) {
    if (!this.alive) return;
    this.lifeTimer += deltaTime;

    // Gravity & Translation
    this.velocity.y -= 9.8 * deltaTime;
    this.position.addScaledVector(this.velocity, deltaTime);

    // Floor collision
    if (this.position.y <= 0.1) {
      this.position.y = 0.1;
      this.velocity.set(0, 0, 0);
    }

    this.mesh.position.copy(this.position);

    if (this.lifeTimer >= this.fuseTime) {
      this.explode();
    }
  }

  explode() {
    this.alive = false;
    this.scene.remove(this.mesh);
    soundManager.playSFX("firecracker_explode");

    // Flash light explosion effect
    const flashLight = new THREE.PointLight(0xff6600, 15.0, 18.0);
    flashLight.position.copy(this.position);
    this.scene.add(flashLight);

    setTimeout(() => {
      this.scene.remove(flashLight);
    }, 250);

    // Distract nearby enemies
    if (this.enemyManager) {
      this.enemyManager.notifyNoiseEvent(this.position, 28.0); // 28m 감지 범위
    }
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
          flashlight.rechargeBattery(1.0); // 100% 충전
          this.inventory.battery -= 1;
          soundManager.playSFX("item_use");
          if (this.hud) this.hud.setStatus("손전등 배터리를 완전 충전했습니다.");
          return true;
        }
        break;
      }
      case "energy_drink": {
        if (player) {
          player.restoreStamina(1.0); // 100% 회복
          player.applySpeedBoost(5.0, 1.4); // 5초간 1.4배 속도
          this.inventory.energy_drink -= 1;
          soundManager.playSFX("item_use");
          if (this.hud) this.hud.setStatus("에너지 드링크! 스태미나 회복 & 이동속도 증가!");
          return true;
        }
        break;
      }
      case "firecracker": {
        if (player) {
          this.throwFirecracker(player);
          this.inventory.firecracker -= 1;
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
    const eyePos = player.getPosition().clone().add(new THREE.Vector3(0, 1.5, 0));
    const dir = player.getForwardVector();
    const proj = new FirecrackerProjectile(this.scene, eyePos, dir, this.enemyManager);
    this.projectiles.push(proj);
    if (this.hud) this.hud.updateInventory(this.inventory);
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
