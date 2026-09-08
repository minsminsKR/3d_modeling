// 시뮬레이션 가상공간: 잔디와 흙이 있는 자연 초원, 나무와 바위, 먹이.
import * as THREE from "three";

export const WORLD_RADIUS = 26;

// 잔디 + 흙 패치를 그린 캔버스 텍스처 생성
function createGroundTexture() {
  const size = 1024;
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d");

  // 기본 잔디색
  ctx.fillStyle = "#4d7a3a";
  ctx.fillRect(0, 0, size, size);

  // 잔디 명암 얼룩
  for (let i = 0; i < 900; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const r = 8 + Math.random() * 30;
    const shade = Math.random();
    ctx.fillStyle = shade < 0.5
      ? `rgba(${60 + Math.random() * 20}, ${105 + Math.random() * 30}, ${45 + Math.random() * 15}, 0.25)`
      : `rgba(${40 + Math.random() * 15}, ${80 + Math.random() * 20}, ${35 + Math.random() * 10}, 0.25)`;
    ctx.beginPath();
    ctx.ellipse(x, y, r, r * (0.5 + Math.random() * 0.5), Math.random() * Math.PI, 0, Math.PI * 2);
    ctx.fill();
  }

  // 흙 패치
  for (let i = 0; i < 26; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const r = 26 + Math.random() * 70;
    const gradient = ctx.createRadialGradient(x, y, r * 0.2, x, y, r);
    gradient.addColorStop(0, "rgba(122, 90, 58, 0.9)");
    gradient.addColorStop(0.7, "rgba(110, 82, 52, 0.55)");
    gradient.addColorStop(1, "rgba(110, 82, 52, 0)");
    ctx.fillStyle = gradient;
    ctx.beginPath();
    ctx.ellipse(x, y, r, r * (0.55 + Math.random() * 0.4), Math.random() * Math.PI, 0, Math.PI * 2);
    ctx.fill();
  }

  // 흙 위 잔돌 점
  for (let i = 0; i < 500; i++) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    ctx.fillStyle = `rgba(${140 + Math.random() * 50}, ${120 + Math.random() * 40}, ${90 + Math.random() * 30}, ${0.15 + Math.random() * 0.2})`;
    ctx.fillRect(x, y, 2 + Math.random() * 3, 2 + Math.random() * 3);
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  return texture;
}

function createTree(scale = 1) {
  const tree = new THREE.Group();

  const trunkHeight = 2.6 * scale;
  const trunk = new THREE.Mesh(
    new THREE.CylinderGeometry(0.22 * scale, 0.34 * scale, trunkHeight, 8),
    new THREE.MeshLambertMaterial({ color: 0x6d4c33 }),
  );
  trunk.position.y = trunkHeight / 2;
  trunk.castShadow = true;
  tree.add(trunk);

  const foliageColors = [0x3e7d32, 0x4c8f3a, 0x35702c];
  const blobs = 3;
  for (let i = 0; i < blobs; i++) {
    const r = (1.1 + Math.random() * 0.5) * scale;
    const foliage = new THREE.Mesh(
      new THREE.IcosahedronGeometry(r, 1),
      new THREE.MeshLambertMaterial({ color: foliageColors[i % foliageColors.length] }),
    );
    foliage.position.set(
      (Math.random() - 0.5) * 0.9 * scale,
      trunkHeight + (i * 0.55 + 0.2) * scale,
      (Math.random() - 0.5) * 0.9 * scale,
    );
    foliage.castShadow = true;
    tree.add(foliage);
  }
  return tree;
}

export function createWorld(scene) {
  scene.background = new THREE.Color(0x9fc7ea);
  scene.fog = new THREE.Fog(0x9fc7ea, 55, 150);

  const hemi = new THREE.HemisphereLight(0xcfe6ff, 0x3f5a2e, 1.0);
  scene.add(hemi);

  const sun = new THREE.DirectionalLight(0xfff2d8, 1.9);
  sun.position.set(20, 32, 14);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -40;
  sun.shadow.camera.right = 40;
  sun.shadow.camera.top = 40;
  sun.shadow.camera.bottom = -40;
  scene.add(sun);

  // 초원 바닥 (활동 영역보다 넓게 깔아 자연스럽게)
  const ground = new THREE.Mesh(
    new THREE.CircleGeometry(WORLD_RADIUS * 2.2, 96),
    new THREE.MeshLambertMaterial({ map: createGroundTexture() }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  // 잔디 다발 (인스턴싱)
  const tuftCount = 700;
  const tuftGeometry = new THREE.ConeGeometry(0.09, 0.55, 4);
  const tuftMaterial = new THREE.MeshLambertMaterial({ color: 0x5c9445 });
  const tufts = new THREE.InstancedMesh(tuftGeometry, tuftMaterial, tuftCount);
  const dummy = new THREE.Object3D();
  for (let i = 0; i < tuftCount; i++) {
    const angle = Math.random() * Math.PI * 2;
    const r = Math.sqrt(Math.random()) * (WORLD_RADIUS * 1.6);
    dummy.position.set(Math.sin(angle) * r, 0.22, Math.cos(angle) * r);
    dummy.rotation.y = Math.random() * Math.PI;
    dummy.rotation.z = (Math.random() - 0.5) * 0.25;
    const s = 0.7 + Math.random() * 0.9;
    dummy.scale.set(s, s, s);
    dummy.updateMatrix();
    tufts.setMatrixAt(i, dummy.matrix);
  }
  scene.add(tufts);

  const obstacles = [];

  // 활동 영역 안쪽 나무 (장애물 = 코 촉각 자극원)
  const innerTreeSpecs = [
    { x: -9, z: -6, s: 1.1 },
    { x: 8, z: 9, s: 0.9 },
    { x: 12, z: -10, s: 1.3 },
    { x: -13, z: 10, s: 1.0 },
    { x: 2, z: 15, s: 0.85 },
  ];
  for (const spec of innerTreeSpecs) {
    const tree = createTree(spec.s);
    tree.position.set(spec.x, 0, spec.z);
    scene.add(tree);
    obstacles.push({ position: new THREE.Vector3(spec.x, 0, spec.z), radius: 1.0 * spec.s });
  }

  // 경계 숲: 활동 영역 바깥 둘레에 나무 링
  for (let i = 0; i < 30; i++) {
    const angle = (i / 30) * Math.PI * 2 + Math.random() * 0.15;
    const r = WORLD_RADIUS + 2.5 + Math.random() * 6;
    const tree = createTree(0.9 + Math.random() * 0.8);
    tree.position.set(Math.sin(angle) * r, 0, Math.cos(angle) * r);
    tree.rotation.y = Math.random() * Math.PI;
    scene.add(tree);
  }

  // 바위
  const rockMaterial = new THREE.MeshLambertMaterial({ color: 0x8b8f96 });
  const rockSpecs = [
    { x: -4, z: -14, r: 1.6 },
    { x: 15, z: 3, r: 1.2 },
    { x: -17, z: -2, r: 1.4 },
  ];
  for (const spec of rockSpecs) {
    const rock = new THREE.Mesh(new THREE.IcosahedronGeometry(spec.r, 1), rockMaterial);
    rock.position.set(spec.x, spec.r * 0.45, spec.z);
    rock.scale.y = 0.7;
    rock.castShadow = true;
    rock.receiveShadow = true;
    scene.add(rock);
    obstacles.push({ position: new THREE.Vector3(spec.x, 0, spec.z), radius: spec.r });
  }

  return { obstacles };
}

const foodGeometry = new THREE.SphereGeometry(0.5, 20, 16);
const foodMaterial = new THREE.MeshStandardMaterial({
  color: 0xff8c42,
  emissive: 0xc2571f,
  emissiveIntensity: 0.85,
  roughness: 0.35,
});

export class Food {
  constructor(scene, position, amount = 40) {
    this.amount = amount;
    this.initialAmount = amount;
    this.mesh = new THREE.Mesh(foodGeometry, foodMaterial.clone());
    this.mesh.castShadow = true;
    this.mesh.position.copy(position);
    this.mesh.position.y = 0.45;
    this.mesh.userData.food = this;
    scene.add(this.mesh);
  }

  get position() {
    return this.mesh.position;
  }

  // amount만큼 소모하고 실제 소모량을 반환
  consume(requested) {
    const eaten = Math.min(this.amount, requested);
    this.amount -= eaten;
    const s = Math.max(0.25, this.amount / this.initialAmount);
    this.mesh.scale.setScalar(s);
    return eaten;
  }

  get depleted() {
    return this.amount <= 0;
  }

  dispose(scene) {
    scene.remove(this.mesh);
    this.mesh.material.dispose();
  }
}
