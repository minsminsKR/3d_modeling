import * as THREE from "three";

export class ParticleSystem {
  constructor(scene) {
    this.scene = scene;
    this.particleCount = 120;
    this.geometry = new THREE.BufferGeometry();
    this.positions = new Float32Array(this.particleCount * 3);
    this.speeds = new Float32Array(this.particleCount * 3);

    const range = 18;
    for (let i = 0; i < this.particleCount; i++) {
      this.positions[i * 3] = (Math.random() - 0.5) * range;
      this.positions[i * 3 + 1] = Math.random() * 4.5;
      this.positions[i * 3 + 2] = (Math.random() - 0.5) * range;

      this.speeds[i * 3] = (Math.random() - 0.5) * 0.15;
      this.speeds[i * 3 + 1] = (Math.random() - 0.5) * 0.08;
      this.speeds[i * 3 + 2] = (Math.random() - 0.5) * 0.15;
    }

    this.geometry.setAttribute("position", new THREE.BufferAttribute(this.positions, 3));

    // Particle texture canvas
    const canvas = document.createElement("canvas");
    canvas.width = 16;
    canvas.height = 16;
    const ctx = canvas.getContext("2d");
    const grad = ctx.createRadialGradient(8, 8, 0, 8, 8, 8);
    grad.addColorStop(0, "rgba(255, 235, 200, 0.75)");
    grad.addColorStop(1, "rgba(255, 235, 200, 0.0)");
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, 16, 16);

    const texture = new THREE.CanvasTexture(canvas);

    this.material = new THREE.PointsMaterial({
      size: 0.18,
      map: texture,
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      opacity: 0.45,
    });

    this.points = new THREE.Points(this.geometry, this.material);
    this.scene.add(this.points);
  }

  update(deltaTime, playerPosition) {
    if (!playerPosition) return;

    // Follow player position box
    const posAttr = this.geometry.attributes.position;
    const positions = posAttr.array;
    const range = 16;

    for (let i = 0; i < this.particleCount; i++) {
      positions[i * 3] += this.speeds[i * 3] * deltaTime;
      positions[i * 3 + 1] += this.speeds[i * 3 + 1] * deltaTime;
      positions[i * 3 + 2] += this.speeds[i * 3 + 2] * deltaTime;

      // Wrap around player position
      if (Math.abs(positions[i * 3] - playerPosition.x) > range * 0.5) {
        positions[i * 3] = playerPosition.x + (Math.random() - 0.5) * range;
      }
      if (positions[i * 3 + 1] < 0 || positions[i * 3 + 1] > 6.0) {
        positions[i * 3 + 1] = Math.random() * 5.0;
      }
      if (Math.abs(positions[i * 3 + 2] - playerPosition.z) > range * 0.5) {
        positions[i * 3 + 2] = playerPosition.z + (Math.random() - 0.5) * range;
      }
    }

    posAttr.needsUpdate = true;
  }
}
