// 수집 가능한 열쇠 하나를 표현하는 모듈입니다.
// 렌더링, E키 프롬프트, 수집 상태를 함께 보유합니다.

import * as THREE from "three";
import { WORLD_CONFIG } from "../config/gameConfig.js";

export class KeyItem {
  constructor(config) {
    this.id = config.id;
    this.label = config.label;
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
      emissive: 0x2c2208,
      metalness: 0.35,
      roughness: 0.38,
    });

    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.18, 0.035, 12, 22), material);
    ring.rotation.x = Math.PI / 2;
    this.group.add(ring);

    const shaft = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.06, 0.48), material);
    shaft.position.z = 0.34;
    this.group.add(shaft);

    const toothA = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.06, 0.08), material);
    toothA.position.set(0.03, 0, 0.58);
    this.group.add(toothA);

    const toothB = new THREE.Mesh(new THREE.BoxGeometry(0.11, 0.06, 0.08), material);
    toothB.position.set(-0.05, 0, 0.46);
    this.group.add(toothB);
  }

  update(deltaTime, elapsedTime) {
    if (!this.isAvailable || this.isCollected) {
      return;
    }

    this.group.rotation.y += deltaTime * 1.6;
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
    return `E - ${this.label} 줍기 (${collected}/${total})`;
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
}
