import * as THREE from 'three';

// Source lights keep their authored transforms and animation. Only this fixed
// set reaches WebGL, so streamed rooms and temporary effects cannot alter the
// shader light layout. Layer 31 is reserved for non-rendered light sources.
export class StablePointLights {
  constructor(scene, count = 12) {
    this.scene = scene;
    this.position = new THREE.Vector3();
    this.candidates = [];
    this.pool = Array.from({ length: count }, () => {
      const light = new THREE.PointLight(0xffffff, 0);
      light.userData.renderLight = true;
      scene.add(light);
      return light;
    });
  }

  update(camera) {
    this.scene.updateMatrixWorld(true);
    camera.getWorldPosition(this.position);
    const candidates = this.candidates;
    candidates.length = 0;
    this.scene.traverseVisible(source => {
      if (!source.isPointLight || source.userData.renderLight) return;
      source.layers.set(31);
      if (source.intensity <= 0) return;
      const position = source.userData.renderPosition ||= new THREE.Vector3();
      position.setFromMatrixPosition(source.matrixWorld);
      const distance = position.distanceTo(this.position);
      if (source.distance > 0 && distance > source.distance + 10) return;
      source.userData.renderScore = source.intensity / (1 + distance * distance);
      candidates.push(source);
    });
    candidates.sort((a,b) => b.userData.renderScore - a.userData.renderScore);
    for (let i=0; i<this.pool.length; i++) {
      const target = this.pool[i], source = candidates[i];
      target.intensity = source?.intensity || 0;
      if (!source) continue;
      target.position.copy(source.userData.renderPosition);
      target.color.copy(source.color);
      target.distance = source.distance;
      target.decay = source.decay;
    }
  }
}
