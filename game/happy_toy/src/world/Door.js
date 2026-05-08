// 열고 닫을 수 있는 문 하나를 표현하는 모듈입니다.
// 렌더링 메시, 상호작용 거리, 닫힌 상태 충돌체를 함께 보유합니다.

import * as THREE from "three";
import { makeAabbFromCenter, smoothStep } from "../utils/math.js";

export class Door {
  constructor(config, material) {
    this.id = config.id;
    this.label = config.label;
    this.position = new THREE.Vector3(...config.position);
    this.size = config.size;
    this.openDirection = config.openDirection ?? 1;
    this.connectedRoomId = config.connectedRoomId || null;
    this.isLocked = Boolean(config.locked);
    this.isBlocked = Boolean(config.blocked);
    this.blockedReason = config.blockedReason || "문이 단단히 막혀 있습니다.";
    this.isOpen = false;
    this.openAmount = 0;

    this.group = new THREE.Group();
    this.group.position.copy(this.position);

    const geometry = new THREE.BoxGeometry(this.size[0], this.size[1], this.size[2]);
    this.mesh = new THREE.Mesh(geometry, material);
    this.mesh.position.set(0, this.size[1] / 2, 0);
    this.mesh.castShadow = true;
    this.mesh.receiveShadow = true;
    this.group.add(this.mesh);

    const handle = new THREE.Mesh(
      new THREE.SphereGeometry(0.06, 12, 12),
      new THREE.MeshStandardMaterial({ color: 0xd4b24a, roughness: 0.55 }),
    );
    handle.position.set(-0.13, 1.22, this.size[2] * 0.28);
    this.group.add(handle);
  }

  toggle() {
    if (this.isLocked || this.isBlocked) {
      return false;
    }
    this.isOpen = !this.isOpen;
    return true;
  }

  getPrompt() {
    if (this.isLocked || this.isBlocked) {
      return `E - ${this.label} 확인`;
    }
    const action = this.isOpen ? "닫기" : "열기";
    return `E - ${this.label} ${action}`;
  }

  interact(context) {
    if (!this.toggle()) {
      context.hud?.setStatus(this.blockedReason, 1500);
      console.warn(`[Door] ${this.id} is locked/blocked. connectedRoom=${this.connectedRoomId ?? "none"}`);
      return;
    }
    const action = this.isOpen ? "열었습니다" : "닫았습니다";
    context.hud?.setStatus(`${this.label} 문을 ${action}.`, 1400);
  }

  update(deltaTime) {
    const target = this.isOpen ? 1 : 0;
    this.openAmount = smoothStep(this.openAmount, target, 7.5, deltaTime);
    this.group.rotation.y = this.openAmount * this.openDirection * Math.PI * 0.52;
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  isBlocking() {
    return this.isLocked || this.isBlocked || (!this.isOpen && this.openAmount < 0.72);
  }

  getAabb() {
    return makeAabbFromCenter([this.position.x, this.position.y + this.size[1] / 2, this.position.z], this.size);
  }

  getDebugInfo() {
    return {
      id: this.id,
      label: this.label,
      connectedRoomId: this.connectedRoomId,
      locked: this.isLocked,
      blocked: this.isBlocked,
      open: this.isOpen,
    };
  }
}
