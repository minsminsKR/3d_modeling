import * as THREE from "three";

const hiddenMatrix = new THREE.Matrix4().makeScale(0, 0, 0);

export class InstancedPool {
  constructor(scene, { capacity, geometry, material, castShadow = false, receiveShadow = false }) {
    this.capacity = capacity;
    this.mesh = new THREE.InstancedMesh(geometry, material, capacity);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.mesh.castShadow = castShadow;
    this.mesh.receiveShadow = receiveShadow;
    this.mesh.frustumCulled = false;
    this.mesh.count = capacity;
    scene.add(this.mesh);

    this.dummy = new THREE.Object3D();
    this.color = new THREE.Color();
    for (let i = 0; i < capacity; i += 1) {
      this.mesh.setMatrixAt(i, hiddenMatrix);
    }
    this.mesh.instanceMatrix.needsUpdate = true;
  }

  set(index, position, scale, color = null, yaw = 0) {
    this.dummy.position.copy(position);
    this.dummy.rotation.set(0, yaw, 0);
    this.dummy.scale.copy(scale);
    this.dummy.updateMatrix();
    this.mesh.setMatrixAt(index, this.dummy.matrix);
    if (color !== null) {
      this.mesh.setColorAt(index, this.color.setHex(color));
      this.mesh.instanceColor.needsUpdate = true;
    }
  }

  hide(index) {
    this.mesh.setMatrixAt(index, hiddenMatrix);
  }

  flush() {
    this.mesh.instanceMatrix.needsUpdate = true;
  }
}

export class AudioBus {
  constructor() {
    this.last = new Map();
  }

  play(key, minGap = 0.04) {
    const now = performance.now() / 1000;
    if ((this.last.get(key) || 0) + minGap > now) {
      return false;
    }
    this.last.set(key, now);
    return true;
  }
}
