// 플레이어가 E키로 한 번 켜 둘 수 있는 방문 표시용 고정 조명입니다.
// 실제 PointLight는 Game의 제한된 풀에서 가까운 조명에만 할당합니다.

import * as THREE from "three";
import { SAFE_LIGHT_CONFIG } from "../config/gameConfig.js";

const VARIANT_LABELS = {
  "wall-switch": "벽 스위치",
  "floor-lamp": "낡은 스탠드",
  "ceiling-switch": "형광등 스위치",
  "toy-lamp": "장난감 램프",
};

export class SafeLight {
  constructor(config) {
    this.id = config.id;
    this.stateKey = config.stateKey || config.id;
    this.label = config.label || VARIANT_LABELS[config.variant] || "조명";
    this.variant = config.variant || "wall-switch";
    this.position = new THREE.Vector3(...config.position);
    this.yaw = config.yaw ?? 0;
    this.isOn = Boolean(config.isOn);

    this.group = new THREE.Group();
    this.group.name = this.id;
    this.group.userData.safeLight = true;
    this.group.userData.safeLightKey = this.stateKey;
    this.group.userData.safeLightVariant = this.variant;
    this.group.position.copy(this.position);
    this.group.rotation.y = this.yaw;

    this.materials = this.createMaterials();
    this.lightAnchor = new THREE.Object3D();
    this.group.add(this.lightAnchor);
    this.createVariantMesh();
    this.setActivated(this.isOn);
  }

  createMaterials() {
    return {
      body: new THREE.MeshStandardMaterial({ color: 0x2b241b, roughness: 0.82, metalness: 0.04 }),
      dark: new THREE.MeshStandardMaterial({ color: 0x0f0c09, roughness: 0.9 }),
      brass: new THREE.MeshStandardMaterial({ color: 0x6f5531, roughness: 0.62, metalness: 0.18 }),
      glow: new THREE.MeshStandardMaterial({
        color: 0x4a3219,
        emissive: 0x1f1308,
        emissiveIntensity: 0.03,
        roughness: 0.58,
        metalness: 0.02,
      }),
      shade: new THREE.MeshStandardMaterial({
        color: 0x4a2e1c,
        emissive: 0x120904,
        emissiveIntensity: 0.02,
        roughness: 0.76,
        metalness: 0.02,
        side: THREE.DoubleSide,
      }),
    };
  }

  createVariantMesh() {
    if (this.variant === "floor-lamp") {
      this.createFloorLamp();
    } else if (this.variant === "ceiling-switch") {
      this.createCeilingSwitch();
    } else if (this.variant === "toy-lamp") {
      this.createToyLamp();
    } else {
      this.createWallSwitch();
    }
  }

  createWallSwitch() {
    this.lightAnchor.position.set(0, 0.18, -0.34);

    // Antique dark wood wall-mounting backplate
    const woodBack = this.addMesh(new THREE.BoxGeometry(0.42, 0.56, 0.03), this.materials.dark);
    woodBack.position.set(0, 0.18, 0.015);

    const plate = this.addMesh(new THREE.BoxGeometry(0.34, 0.46, 0.055), this.materials.body);
    plate.position.set(0, 0.18, -0.02);

    const toggle = this.addMesh(new THREE.BoxGeometry(0.08, 0.23, 0.045), this.materials.brass);
    toggle.name = `${this.id}-toggle`;
    toggle.position.set(0, 0.18, -0.065);
    toggle.rotation.x = -0.18;

    const indicator = this.addMesh(new THREE.SphereGeometry(0.065, 12, 12), this.materials.glow);
    indicator.name = `${this.id}-indicator`;
    indicator.position.set(0, 0.48, -0.075);
    this.glowMeshes = [indicator];
  }

  createFloorLamp() {
    this.lightAnchor.position.set(0, 1.18, 0);

    const base = this.addMesh(new THREE.CylinderGeometry(0.18, 0.24, 0.08, 16), this.materials.brass);
    base.position.y = 0.04;

    const pole = this.addMesh(new THREE.CylinderGeometry(0.025, 0.035, 1.05, 10), this.materials.brass);
    pole.position.y = 0.58;

    const shade = this.addMesh(new THREE.ConeGeometry(0.34, 0.42, 18, 1, true), this.materials.shade);
    shade.position.y = 1.22;
    shade.rotation.x = Math.PI;

    const bulb = this.addMesh(new THREE.SphereGeometry(0.11, 14, 14), this.materials.glow);
    bulb.name = `${this.id}-bulb`;
    bulb.position.y = 1.12;
    this.glowMeshes = [bulb, shade];
  }

  createCeilingSwitch() {
    this.lightAnchor.position.set(0, 0.2, -0.22);

    const box = this.addMesh(new THREE.BoxGeometry(0.42, 0.26, 0.07), this.materials.body);
    box.position.y = 0.1;

    const pull = this.addMesh(new THREE.CylinderGeometry(0.012, 0.012, 0.38, 8), this.materials.brass);
    pull.position.set(0, -0.13, -0.035);

    const cap = this.addMesh(new THREE.SphereGeometry(0.055, 10, 10), this.materials.glow);
    cap.name = `${this.id}-cap`;
    cap.position.set(0, -0.34, -0.035);
    this.glowMeshes = [cap];
  }

  createToyLamp() {
    this.lightAnchor.position.set(0, 0.52, 0);

    const base = this.addMesh(new THREE.BoxGeometry(0.42, 0.22, 0.42), this.materials.body);
    base.position.y = 0.11;

    const ears = [
      [-0.16, 0.35, 0],
      [0.16, 0.35, 0],
    ];
    for (const [x, y, z] of ears) {
      const ear = this.addMesh(new THREE.SphereGeometry(0.11, 10, 10), this.materials.brass);
      ear.position.set(x, y, z);
    }

    const head = this.addMesh(new THREE.SphereGeometry(0.22, 16, 16), this.materials.glow);
    head.name = `${this.id}-head`;
    head.position.y = 0.34;
    this.glowMeshes = [head];
  }

  addMesh(geometry, material) {
    const mesh = new THREE.Mesh(geometry, material);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    this.group.add(mesh);
    return mesh;
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.z - point.z);
  }

  isInteractable(context) {
    return !this.isOn && !context?.isSafeLightActivated?.(this.stateKey);
  }

  getPrompt() {
    return `Press E to turn on light - ${this.label}`;
  }

  interact(context) {
    context.activateSafeLight?.(this);
  }

  setActivated(isOn) {
    this.isOn = Boolean(isOn);
    this.group.userData.safeLightOn = this.isOn;
    const color = this.isOn ? SAFE_LIGHT_CONFIG.emissiveColor : 0x8a4c18;
    const intensity = this.isOn ? 1.05 : 0.35;
    for (const mesh of this.glowMeshes || []) {
      if (mesh.material?.emissive) {
        mesh.material.color.setHex(this.isOn ? 0xffc47a : 0x9c6b38);
        mesh.material.emissive.setHex(color);
        mesh.material.emissiveIntensity = intensity;
      }
    }
  }

  setFlickerState(flickerMult = 1.0) {
    if (!this.isOn) return;
    const baseIntensity = 1.05;
    for (const mesh of this.glowMeshes || []) {
      if (mesh.material?.emissive) {
        mesh.material.emissiveIntensity = baseIntensity * Math.max(0.04, flickerMult);
      }
    }
  }

  getLightWorldPosition(target = new THREE.Vector3()) {
    if (this.lightAnchor) {
      this.lightAnchor.updateMatrixWorld(true);
      return this.lightAnchor.getWorldPosition(target);
    }
    return target.copy(this.position);
  }

  dispose() {
    this.group.traverse((child) => {
      if (!child.isMesh) {
        return;
      }
      child.geometry?.dispose();
    });
    for (const material of Object.values(this.materials)) {
      material.dispose();
    }
  }
}
