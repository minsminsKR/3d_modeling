// 최종 방의 클리어 장치를 표현하는 모듈입니다.
// 필요한 열쇠를 모두 모은 뒤 E키로 상호작용하면 Game이 클리어 처리를 합니다.

import * as THREE from "three";
import { WORLD_CONFIG } from "../config/gameConfig.js";

export class FinalExit {
  constructor(config) {
    this.id = config.id;
    this.label = config.label;
    this.position = new THREE.Vector3(...config.position);

    this.group = new THREE.Group();
    this.group.name = config.id;
    this.group.position.copy(this.position);

    const chestMaterial = new THREE.MeshStandardMaterial({
      color: WORLD_CONFIG.finalColor,
      roughness: 0.72,
      metalness: 0.08,
    });
    const trimMaterial = new THREE.MeshStandardMaterial({
      color: WORLD_CONFIG.keyColor,
      emissive: 0x000000,
      metalness: 0.3,
      roughness: 0.42,
    });

    const base = new THREE.Mesh(new THREE.BoxGeometry(1.45, 0.58, 0.92), chestMaterial);
    base.position.y = 0.29;
    base.castShadow = true;
    base.receiveShadow = true;
    this.group.add(base);

    const lid = new THREE.Mesh(new THREE.BoxGeometry(1.55, 0.18, 0.98), chestMaterial);
    lid.position.y = 0.69;
    lid.castShadow = true;
    this.group.add(lid);

    const lock = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.22, 0.08), trimMaterial);
    lock.position.set(0, 0.44, -0.51);
    lock.castShadow = true;
    lock.receiveShadow = true;
    this.group.add(lock);

    const glow = new THREE.PointLight(WORLD_CONFIG.keyColor, 0.18, 3.2, 1.6);
    glow.position.set(0, 1.1, 0);
    glow.castShadow = false;
    this.group.add(glow);
  }

  update(deltaTime) {
    this.group.rotation.y += deltaTime * 0.12;
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  isInteractable(context) {
    return !context.isCleared?.();
  }

  getPrompt(context) {
    const collected = context.getKeyCount?.() ?? 0;
    const total = context.getTotalKeys?.() ?? 3;
    return `E - ${this.label}에 열쇠 건네기 (${collected}/${total})`;
  }

  interact(context) {
    context.tryClearFinal?.(this);
  }

  dispose() {
    this.group.traverse((child) => {
      if (child.isMesh) {
        child.geometry?.dispose();
        if (Array.isArray(child.material)) {
          child.material.forEach((m) => m.dispose());
        } else {
          child.material?.dispose();
        }
      } else if (child.isPointLight) {
        child.dispose();
      }
    });
  }
}
