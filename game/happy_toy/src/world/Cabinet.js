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
    this.guardDistance = config.guardDistance ?? 1.28;
    this.exitDistance = config.exitDistance ?? 1.05;
    this.occupied = false;
    this.interiorActive = false;

    this.group = new THREE.Group();
    this.group.name = config.id;
    this.group.position.copy(this.position);
    this.group.rotation.y = this.yaw;

    this.bodyMaterial = materials.bodyMaterial ?? new THREE.MeshStandardMaterial({
      color: WORLD_CONFIG.cabinetColor,
      roughness: 0.82,
      metalness: 0.08,
    });
    this.isBodyMaterialShared = Boolean(materials.bodyMaterial);
    const bodyMaterial = this.bodyMaterial;
    const darkMaterial = new THREE.MeshStandardMaterial({ color: 0x15100e, roughness: 0.9 });
    const trimMaterial = new THREE.MeshStandardMaterial({ color: 0x1d1714, roughness: 0.8 });
    const innerMaterial = new THREE.MeshStandardMaterial({
      color: 0x1a1410,
      roughness: 0.92,
      metalness: 0.04,
    });
    const slatMaterial = new THREE.MeshStandardMaterial({
      color: 0x0c0907,
      roughness: 0.62,
      metalness: 0.18,
    });

    this.bodyMesh = new THREE.Mesh(new THREE.BoxGeometry(...this.size), bodyMaterial);
    this.bodyMesh.name = `${this.id}-body`;
    this.bodyMesh.position.y = this.size[1] / 2;
    this.bodyMesh.castShadow = true;
    this.bodyMesh.receiveShadow = true;
    this.group.add(this.bodyMesh);

    this.exteriorGroup = new THREE.Group();
    this.exteriorGroup.name = `${this.id}-exterior`;
    const doorGap = new THREE.Mesh(new THREE.BoxGeometry(0.035, this.size[1] * 0.92, 0.03), darkMaterial);
    doorGap.position.set(0, this.size[1] / 2, -this.size[2] / 2 - 0.02);
    this.exteriorGroup.add(doorGap);

    for (let i = 0; i < 4; i += 1) {
      const slit = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.035, 0.035), darkMaterial);
      slit.position.set(0, 1.15 + i * 0.13, -this.size[2] / 2 - 0.035);
      this.exteriorGroup.add(slit);
    }

    const handleLeft = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.24, 0.04), trimMaterial);
    handleLeft.position.set(-0.11, 1.05, -this.size[2] / 2 - 0.055);
    this.exteriorGroup.add(handleLeft);

    const handleRight = handleLeft.clone();
    handleRight.position.x = 0.11;
    this.exteriorGroup.add(handleRight);
    this.group.add(this.exteriorGroup);

    this.interiorGroup = this.buildInterior(innerMaterial, slatMaterial);
    this.interiorGroup.visible = false;
    this.group.add(this.interiorGroup);
  }

  buildInterior(innerMaterial, slatMaterial) {
    const [w, h, d] = this.size;
    const group = new THREE.Group();
    group.name = `${this.id}-interior`;

    const back = new THREE.Mesh(new THREE.BoxGeometry(w - 0.06, h - 0.06, 0.05), innerMaterial);
    back.position.set(0, h / 2, d / 2 - 0.04);
    group.add(back);

    const left = new THREE.Mesh(new THREE.BoxGeometry(0.05, h - 0.06, d - 0.1), innerMaterial);
    left.position.set(-w / 2 + 0.04, h / 2, 0.02);
    group.add(left);

    const right = left.clone();
    right.position.x = w / 2 - 0.04;
    group.add(right);

    const ceiling = new THREE.Mesh(new THREE.BoxGeometry(w - 0.1, 0.04, d - 0.1), innerMaterial);
    ceiling.position.set(0, h - 0.05, 0.02);
    group.add(ceiling);

    const floor = new THREE.Mesh(new THREE.BoxGeometry(w - 0.1, 0.04, d - 0.1), innerMaterial);
    floor.position.set(0, 0.04, 0.02);
    group.add(floor);

    const doorZ = -d / 2 + 0.02;
    const frameW = 0.08;
    const openingW = 0.78;
    const openingH = 1.62;
    const openingY = 1.28;
    const frameLeft = new THREE.Mesh(new THREE.BoxGeometry(frameW, h - 0.08, 0.05), slatMaterial);
    frameLeft.position.set(-openingW / 2 - frameW / 2, h / 2, doorZ);
    group.add(frameLeft);
    const frameRight = frameLeft.clone();
    frameRight.position.x = openingW / 2 + frameW / 2;
    group.add(frameRight);
    const frameTop = new THREE.Mesh(new THREE.BoxGeometry(openingW + frameW * 2, 0.1, 0.05), slatMaterial);
    frameTop.position.set(0, openingY + openingH / 2 + 0.04, doorZ);
    group.add(frameTop);
    const frameBottom = new THREE.Mesh(new THREE.BoxGeometry(openingW + frameW * 2, 0.12, 0.05), slatMaterial);
    frameBottom.position.set(0, openingY - openingH / 2 - 0.05, doorZ);
    group.add(frameBottom);

    const slatCount = 7;
    const slatW = 0.038;
    const span = openingW - 0.04;
    for (let i = 0; i < slatCount; i += 1) {
      const t = slatCount === 1 ? 0 : i / (slatCount - 1) - 0.5;
      const slat = new THREE.Mesh(new THREE.BoxGeometry(slatW, openingH, 0.045), slatMaterial);
      slat.name = `${this.id}-slat-${i}`;
      slat.position.set(t * span, openingY, doorZ - 0.01);
      group.add(slat);
    }

    this.interiorLight = new THREE.PointLight(0x1c120c, 0, 1.35, 2.0);
    this.interiorLight.position.set(0, 1.55, 0.08);
    this.interiorLight.name = `${this.id}-interior-light`;
    group.add(this.interiorLight);
    return group;
  }

  setOccupied(occupied) {
    this.occupied = Boolean(occupied);
    this.interiorActive = this.occupied;
    if (this.bodyMesh) this.bodyMesh.visible = !this.occupied;
    if (this.exteriorGroup) this.exteriorGroup.visible = !this.occupied;
    if (this.interiorGroup) this.interiorGroup.visible = this.occupied;
    if (this.interiorLight) this.interiorLight.intensity = this.occupied ? 0.28 : 0;
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

  getInsideView(peekYaw = 0, peekPitch = 0) {
    const forward = this.getForwardDirection();
    const position = this.position.clone().addScaledVector(forward, -0.05);
    position.y = this.position.y + 1.42;

    const look = forward.clone();
    look.applyAxisAngle(UP, peekYaw);
    look.y = Math.sin(peekPitch);
    look.normalize();
    const lookAt = position.clone().addScaledVector(look, 4.2);
    return { position, lookAt };
  }

  getGuardPosition() {
    return this.position.clone().addScaledVector(this.getForwardDirection(), this.guardDistance);
  }

  getExitPosition() {
    const exitPosition = this.position.clone().addScaledVector(this.getForwardDirection(), this.exitDistance);
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
    this.setOccupied(false);
  }

  dispose() {
    this.group.traverse((child) => {
      if (child.isMesh) {
        child.geometry?.dispose();
        if (child.material) {
          if (Array.isArray(child.material)) {
            child.material.forEach((m) => {
              if (m !== this.bodyMaterial || !this.isBodyMaterialShared) {
                m.dispose();
              }
            });
          } else if (child.material !== this.bodyMaterial || !this.isBodyMaterialShared) {
            child.material.dispose();
          }
        }
      }
    });
  }
}
