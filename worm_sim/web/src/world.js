// 시뮬레이션 가상공간: 페트리 접시 형태의 원형 무대, 조명, 장애물, 먹이.
import * as THREE from "three";

export const DISH_RADIUS = 26;

export function createWorld(scene) {
  scene.background = new THREE.Color(0x0b1020);
  scene.fog = new THREE.Fog(0x0b1020, 60, 140);

  const hemi = new THREE.HemisphereLight(0xbdd4ff, 0x1a2338, 0.9);
  scene.add(hemi);

  const sun = new THREE.DirectionalLight(0xffffff, 1.6);
  sun.position.set(18, 30, 12);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -35;
  sun.shadow.camera.right = 35;
  sun.shadow.camera.top = 35;
  sun.shadow.camera.bottom = -35;
  scene.add(sun);

  // 접시 바닥
  const floor = new THREE.Mesh(
    new THREE.CircleGeometry(DISH_RADIUS, 96),
    new THREE.MeshStandardMaterial({ color: 0x1c2b45, roughness: 0.9 }),
  );
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  scene.add(floor);

  // 미묘한 동심원 무늬 (한천 배지 느낌)
  for (let r = 6; r < DISH_RADIUS; r += 6) {
    const ring = new THREE.Mesh(
      new THREE.RingGeometry(r - 0.06, r + 0.06, 96),
      new THREE.MeshBasicMaterial({
        color: 0x2c406a,
        transparent: true,
        opacity: 0.35,
        side: THREE.DoubleSide,
      }),
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.01;
    scene.add(ring);
  }

  // 접시 테두리 벽
  const rim = new THREE.Mesh(
    new THREE.CylinderGeometry(DISH_RADIUS + 0.6, DISH_RADIUS + 0.6, 2.4, 96, 1, true),
    new THREE.MeshStandardMaterial({
      color: 0x37527f,
      roughness: 0.5,
      transparent: true,
      opacity: 0.55,
      side: THREE.DoubleSide,
    }),
  );
  rim.position.y = 1.2;
  scene.add(rim);

  // 장애물 바위 (코 촉각 자극원)
  const obstacles = [];
  const rockMaterial = new THREE.MeshStandardMaterial({ color: 0x54627e, roughness: 0.85 });
  const rockSpecs = [
    { x: -9, z: -6, r: 1.9 },
    { x: 8, z: 9, r: 1.5 },
    { x: 12, z: -10, r: 2.3 },
    { x: -13, z: 10, r: 1.7 },
  ];
  for (const spec of rockSpecs) {
    const rock = new THREE.Mesh(new THREE.IcosahedronGeometry(spec.r, 1), rockMaterial);
    rock.position.set(spec.x, spec.r * 0.55, spec.z);
    rock.castShadow = true;
    rock.receiveShadow = true;
    scene.add(rock);
    obstacles.push({ position: new THREE.Vector3(spec.x, 0, spec.z), radius: spec.r });
  }

  return { obstacles };
}

const foodGeometry = new THREE.SphereGeometry(0.5, 20, 16);
const foodMaterial = new THREE.MeshStandardMaterial({
  color: 0x5fe08a,
  emissive: 0x2b8f4e,
  emissiveIntensity: 0.9,
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
