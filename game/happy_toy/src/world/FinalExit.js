// 시작 홀의 제단함. 네 이름을 돌려놓는 봉인 장치입니다.

import * as THREE from "three";

export class FinalExit {
  constructor(config) {
    this.id = config.id;
    this.label = config.label;
    this.position = new THREE.Vector3(...config.position);

    this.group = new THREE.Group();
    this.group.name = config.id;
    this.group.position.copy(this.position);

    const loader = new THREE.TextureLoader();
    const woodMap = loader.load("/assets/textures/doors/doors.png");
    woodMap.colorSpace = THREE.SRGBColorSpace;
    woodMap.wrapS = THREE.RepeatWrapping;
    woodMap.wrapT = THREE.RepeatWrapping;
    woodMap.repeat.set(1.4, 0.8);

    const canvas = document.createElement('canvas');
    canvas.width = 128; canvas.height = 256;
    const ink = canvas.getContext('2d');
    ink.fillStyle = '#cbb88d'; ink.fillRect(0, 0, 128, 256);
    ink.strokeStyle = '#853e31'; ink.lineWidth = 3; ink.strokeRect(12, 12, 104, 232);
    ink.fillStyle = '#553a2d'; ink.font = '28px Batang, serif'; ink.textAlign = 'center';
    ['귀', '환', '봉', '인'].forEach((letter, i) => ink.fillText(letter, 64, 58 + i * 48));
    const paperMap = new THREE.CanvasTexture(canvas);
    paperMap.colorSpace = THREE.SRGBColorSpace;

    const wood = new THREE.MeshStandardMaterial({
      map: woodMap,
      color: 0x5a4030,
      roughness: 0.82,
      metalness: 0.04,
      emissive: 0x1a0c08,
      emissiveIntensity: 0.08,
    });
    const lacquer = new THREE.MeshStandardMaterial({
      color: 0x3a120e,
      roughness: 0.45,
      metalness: 0.08,
      emissive: 0x220806,
      emissiveIntensity: 0.12,
    });
    const brass = new THREE.MeshStandardMaterial({
      color: 0x8a6a38,
      roughness: 0.48,
      metalness: 0.35,
      emissive: 0x2a1c08,
      emissiveIntensity: 0.1,
    });
    const paper = new THREE.MeshStandardMaterial({
      map: paperMap,
      color: 0xe8dcc4,
      roughness: 0.9,
      side: THREE.DoubleSide,
      emissive: 0x2a1810,
      emissiveIntensity: 0.08,
    });
    const flame = new THREE.MeshStandardMaterial({
      color: 0xffc878,
      emissive: 0xff8a3a,
      emissiveIntensity: 1.4,
    });

    const table = new THREE.Mesh(new THREE.BoxGeometry(1.72, 0.12, 0.98), wood);
    table.position.y = 0.58;
    table.castShadow = true;
    table.receiveShadow = true;
    this.group.add(table);

    const apron = new THREE.Mesh(new THREE.BoxGeometry(1.78, 0.08, 1.04), lacquer);
    apron.position.y = 0.64;
    this.group.add(apron);

    for (const x of [-0.72, 0.72]) {
      for (const z of [-0.36, 0.36]) {
        const leg = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.58, 0.1), wood);
        leg.position.set(x, 0.29, z);
        this.group.add(leg);
      }
    }

    this.soulSeals = [];
    for (let i = 0; i < 4; i += 1) {
      const slot = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.04, 0.36), lacquer);
      slot.position.set(-0.54 + i * 0.36, 0.7, 0.08);
      this.group.add(slot);
      const plate = new THREE.Mesh(new THREE.PlaneGeometry(0.22, 0.3), paper);
      plate.position.set(-0.54 + i * 0.36, 0.78, 0.08);
      plate.rotation.x = -Math.PI / 2.6;
      this.group.add(plate);
      const token = new THREE.Mesh(new THREE.SphereGeometry(0.04, 12, 8), new THREE.MeshStandardMaterial({color:0x514837, emissive:0xe7b563, emissiveIntensity:0}));
      token.position.set(-0.54 + i * 0.36, 0.8, -0.13);
      this.group.add(token); this.soulSeals.push(token);
    }

    const seal = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.28, 0.06), brass);
    seal.position.set(0, 0.52, -0.52);
    this.group.add(seal);

    const strip = new THREE.Mesh(new THREE.PlaneGeometry(0.34, 0.7), paper);
    strip.position.set(0.62, 1.12, 0.02);
    this.group.add(strip);

    for (const x of [-0.58, 0.58]) {
      const stick = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.02, 0.28, 8), wood);
      stick.position.set(x, 0.86, -0.28);
      this.group.add(stick);
      const wick = new THREE.Mesh(new THREE.SphereGeometry(0.035, 8, 8), flame);
      wick.position.set(x, 1.02, -0.28);
      this.group.add(wick);
    }

    this.glow = new THREE.PointLight(0xffb068, 1.35, 4.8, 1.7);
    this.glow.position.set(0, 1.15, 0);
    this.group.add(this.glow);
    this.flicker = 0;
  }

  update(deltaTime) {
    this.flicker += deltaTime * 2.4;
    if (this.glow) {
      this.glow.intensity = 1.15 + Math.sin(this.flicker) * 0.18 + Math.sin(this.flicker * 3.1) * 0.08;
    }
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  setProgress(count) {
    this.soulSeals.forEach((token, index) => {
      token.material.emissiveIntensity = index < count ? 1.6 : 0;
    });
  }

  isInteractable(context) {
    return !context.isCleared?.();
  }

  getPrompt(context) {
    const collected = context.getKeyCount?.() ?? 0;
    const total = context.getTotalKeys?.() ?? 3;
    return collected >= total
      ? "E - 봉인 시작 · 제단함 곁에서 6초 버티기"
      : `E - ${this.label}에 이름을 건네기 (${collected}/${total})`;
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
