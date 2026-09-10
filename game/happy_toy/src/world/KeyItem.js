// 수집 가능한 열쇠 하나를 표현하는 모듈입니다.
// 렌더링, E키 프롬프트, 수집 상태를 함께 보유합니다.

import * as THREE from "three";
import { WORLD_CONFIG } from "../config/gameConfig.js";

export class KeyItem {
  constructor(config) {
    this.id = config.id;
    this.label = {
      'key-workshop': '요람에 남은 혼', 'key-playroom': '인형이 지키던 혼',
      'key-storage': '선반에 갇힌 혼', 'key-hwacat': '액자 뒤의 혼',
    }[config.id] || config.label;
    this.position = new THREE.Vector3(...config.position);
    this.initiallyVisible = config.initiallyVisible ?? true;
    this.isAvailable = this.initiallyVisible;
    this.isCollected = false;
    this.floatOffset = (this.position.x + this.position.z) * 0.37;

    this.group = new THREE.Group();
    this.group.name = config.id;
    this.group.position.set(this.position.x, this.position.y + 0.72, this.position.z);
    this.group.visible = this.initiallyVisible;

    const material = new THREE.MeshStandardMaterial({
      color: WORLD_CONFIG.keyColor,
      emissive: 0x000000,
      metalness: 0.35,
      roughness: 0.38,
    });

    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.18, 0.035, 12, 22), material);
    ring.rotation.x = Math.PI / 2;
    ring.castShadow = true;
    ring.receiveShadow = true;
    this.group.add(ring);

    const shaft = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.06, 0.48), material);
    shaft.position.z = 0.34;
    shaft.castShadow = true;
    shaft.receiveShadow = true;
    this.group.add(shaft);

    const toothA = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.06, 0.08), material);
    toothA.position.set(0.03, 0, 0.58);
    toothA.castShadow = true;
    toothA.receiveShadow = true;
    this.group.add(toothA);

    const toothB = new THREE.Mesh(new THREE.BoxGeometry(0.11, 0.06, 0.08), material);
    toothB.position.set(-0.05, 0, 0.46);
    toothB.castShadow = true;
    toothB.receiveShadow = true;
    this.group.add(toothB);
    // The old key remains as the physical keepsake inside a restrained soul glow.
    this.soulMaterial = new THREE.MeshStandardMaterial({
      color: 0xc8e3d3, emissive: 0x9dbda7, emissiveIntensity: 1.1,
      transparent: true, opacity: .28, roughness: .25, depthWrite: false,
    });
    this.soul = new THREE.Mesh(new THREE.SphereGeometry(.13, 16, 12), this.soulMaterial);
    this.soul.position.set(0,.15,.18); this.soul.scale.set(.75,1.35,.75);this.group.add(this.soul);
    this.orbit = new THREE.Group();this.group.add(this.orbit);
    for(let i=0;i<5;i++) {
      const mote = new THREE.Mesh(new THREE.SphereGeometry(.012,5,4),new THREE.MeshStandardMaterial({color:0xe3ce96,emissive:0xc2a16a,emissiveIntensity:1.3}));
      mote.position.set(Math.cos(i*1.256)*.27, i*.065-.05, .18+Math.sin(i*1.256)*.27);
      this.orbit.add(mote);
    }
  }

  update(deltaTime, elapsedTime) {
    if (!this.isAvailable || this.isCollected) {
      return;
    }

    this.group.rotation.y += deltaTime * .45;
    this.orbit.rotation.y -= deltaTime * .8;
    this.soulMaterial.opacity = .26 + Math.sin(elapsedTime*1.7 + this.floatOffset)*.06;
    this.group.position.y = this.position.y + 0.72 + Math.sin(elapsedTime * 2.2 + this.floatOffset) * 0.06;
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  isInteractable() {
    return this.isAvailable && !this.isCollected;
  }

  getPrompt(context) {
    const collected = context.getKeyCount?.() ?? 0;
    const total = context.getTotalKeys?.() ?? 3;
    return `E - ${this.label} 줍기 (${collected}/${total}) · 소리가 퍼집니다`;
  }

  interact(context) {
    context.collectKey?.(this);
  }

  collect() {
    this.isCollected = true;
    this.group.visible = false;
  }

  revealAt(position) {
    this.position.set(position[0], position[1], position[2]);
    this.floatOffset = (this.position.x + this.position.z) * 0.37;
    this.group.position.set(this.position.x, this.position.y + 0.72, this.position.z);
    this.isAvailable = true;
    this.isCollected = false;
    this.group.visible = true;
  }

  reset() {
    this.isAvailable = this.initiallyVisible;
    this.isCollected = false;
    this.group.visible = this.initiallyVisible;
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
      }
    });
  }
}
