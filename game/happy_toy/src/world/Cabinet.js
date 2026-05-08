// 플레이어가 숨을 수 있는 캐비넷 하나를 표현하는 모듈입니다.
// 외형, 충돌 크기, 내부 카메라 시점, 몬스터가 대기할 위치를 제공합니다.

import * as THREE from "three";
import { WORLD_CONFIG } from "../config/gameConfig.js";

const UP = new THREE.Vector3(0, 1, 0);

export class Cabinet {
  constructor(config, materials = {}) {
    this.id = config.id;
    this.label = config.label;
    this.position = new THREE.Vector3(...config.position);
    this.yaw = config.yaw ?? 0;
    this.size = config.size || [1.18, 2.25, 0.72];
    this.occupied = false;

    this.group = new THREE.Group();
    this.group.name = config.id;
    this.group.position.copy(this.position);
    this.group.rotation.y = this.yaw;

    const bodyMaterial = materials.bodyMaterial ?? new THREE.MeshStandardMaterial({
      color: WORLD_CONFIG.cabinetColor,
      roughness: 0.82,
      metalness: 0.08,
    });
    const darkMaterial = new THREE.MeshStandardMaterial({ color: 0x15100e, roughness: 0.9 });
    const trimMaterial = new THREE.MeshStandardMaterial({ color: 0x1d1714, roughness: 0.8 });

    const body = new THREE.Mesh(new THREE.BoxGeometry(...this.size), bodyMaterial);
    body.name = `${this.id}-body`;
    body.position.y = this.size[1] / 2;
    body.castShadow = true;
    body.receiveShadow = true;
    this.group.add(body);

    const doorGap = new THREE.Mesh(new THREE.BoxGeometry(0.035, this.size[1] * 0.92, 0.03), darkMaterial);
    doorGap.position.set(0, this.size[1] / 2, -this.size[2] / 2 - 0.02);
    this.group.add(doorGap);

    for (let i = 0; i < 4; i += 1) {
      const slit = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.035, 0.035), darkMaterial);
      slit.position.set(0, 1.15 + i * 0.13, -this.size[2] / 2 - 0.035);
      this.group.add(slit);
    }

    const handleLeft = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.24, 0.04), trimMaterial);
    handleLeft.position.set(-0.11, 1.05, -this.size[2] / 2 - 0.055);
    this.group.add(handleLeft);

    const handleRight = handleLeft.clone();
    handleRight.position.x = 0.11;
    this.group.add(handleRight);
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  isInteractable() {
    return !this.occupied;
  }

  getPrompt() {
    return `E - ${this.label} 숨기`;
  }

  interact(context) {
    context.enterCabinet?.(this);
  }

  getForwardDirection() {
    return new THREE.Vector3(0, 0, -1).applyAxisAngle(UP, this.yaw);
  }

  getInsideView() {
    const forward = this.getForwardDirection();
    const position = this.position.clone().addScaledVector(forward, 0.16);
    position.y = this.position.y + 1.48;

    const lookAt = this.position.clone().addScaledVector(forward, 3.2);
    lookAt.y = this.position.y + 1.33;

    return { position, lookAt };
  }

  getGuardPosition() {
    return this.position.clone().addScaledVector(this.getForwardDirection(), 1.28);
  }

  getExitPosition() {
    const exitPosition = this.position.clone().addScaledVector(this.getForwardDirection(), 1.05);
    exitPosition.y = this.position.y;
    return exitPosition;
  }

  getAabb() {
    return {
      minX: this.position.x - this.size[0] / 2,
      maxX: this.position.x + this.size[0] / 2,
      minY: this.position.y,
      maxY: this.position.y + this.size[1],
      minZ: this.position.z - this.size[2] / 2,
      maxZ: this.position.z + this.size[2] / 2,
    };
  }

  reset() {
    this.occupied = false;
  }
}
