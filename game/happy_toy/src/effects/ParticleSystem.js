import * as THREE from "three";

export class ParticleSystem {
  constructor(scene) {
    this.scene = scene;
    this.particleCount = 150;
    this.geometry = new THREE.BufferGeometry();
    this.positions = new Float32Array(this.particleCount * 3);
    this.speeds = new Float32Array(this.particleCount * 3);
    this.driftPhases = new Float32Array(this.particleCount);
    this.lastPlayerPosition = null;
    let seed = 0x51f15e;
    const random = () => {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      return seed / 4294967296;
    };

    const range = 18;
    for (let i = 0; i < this.particleCount; i++) {
      this.positions[i * 3] = (random() - 0.5) * range;
      this.positions[i * 3 + 1] = -0.3 + random() * 4.2;
      this.positions[i * 3 + 2] = (random() - 0.5) * range;

      this.speeds[i * 3] = (random() - 0.5) * 0.045;
      this.speeds[i * 3 + 1] = 0.008 + random() * 0.022;
      this.speeds[i * 3 + 2] = (random() - 0.5) * 0.045;
      this.driftPhases[i] = random() * Math.PI * 2;
    }

    this.geometry.setAttribute("position", new THREE.BufferAttribute(this.positions, 3));

    // Particle texture canvas
    const canvas = document.createElement("canvas");
    canvas.width = 16;
    canvas.height = 16;
    const ctx = canvas.getContext("2d");
    const grad = ctx.createRadialGradient(8, 8, 0, 8, 8, 8);
    grad.addColorStop(0, "rgba(255, 244, 218, 0.9)");
    grad.addColorStop(0.28, "rgba(218, 205, 179, 0.42)");
    grad.addColorStop(1, "rgba(190, 180, 162, 0.0)");
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, 16, 16);

    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;

    this.material = new THREE.PointsMaterial({
      size: 0.075,
      map: texture,
      transparent: true,
      depthWrite: false,
      depthTest: true,
      blending: THREE.NormalBlending,
      opacity: 0.28,
      color: 0xc8baa3,
      sizeAttenuation: true,
      toneMapped: false,
    });

    this.points = new THREE.Points(this.geometry, this.material);
    // The particle cloud follows the player in world space, so its original
    // geometry bounds quickly become stale. Keeping this tiny cloud uncullable
    // prevents dust popping out after a floor transition.
    this.points.frustumCulled = false;
    this.points.renderOrder = 2;
    this.scene.add(this.points);
  }

  update(deltaTime, playerPosition) {
    if (!playerPosition) return;

    const safeDelta = Math.min(deltaTime, 0.05);
    const posAttr = this.geometry.attributes.position;
    const positions = posAttr.array;
    const range = 16;
    const verticalMin = playerPosition.y - 0.35;
    const verticalMax = playerPosition.y + 3.5;
    const driftTime = performance.now() * 0.00018;

    if (!this.lastPlayerPosition) {
      for (let i = 0; i < this.particleCount; i++) {
        positions[i * 3] += playerPosition.x;
        positions[i * 3 + 1] += playerPosition.y;
        positions[i * 3 + 2] += playerPosition.z;
      }
      this.lastPlayerPosition = playerPosition.clone();
    }

    for (let i = 0; i < this.particleCount; i++) {
      const phase = this.driftPhases[i] + driftTime;
      positions[i * 3] += (this.speeds[i * 3] + Math.sin(phase) * 0.012) * safeDelta;
      positions[i * 3 + 1] += this.speeds[i * 3 + 1] * safeDelta;
      positions[i * 3 + 2] += (this.speeds[i * 3 + 2] + Math.cos(phase * 0.83) * 0.012) * safeDelta;

      // Wrap around player position
      if (Math.abs(positions[i * 3] - playerPosition.x) > range * 0.5) {
        positions[i * 3] = playerPosition.x + (Math.random() - 0.5) * range;
      }
      if (positions[i * 3 + 1] < verticalMin || positions[i * 3 + 1] > verticalMax) {
        positions[i * 3 + 1] = verticalMin + Math.random() * (verticalMax - verticalMin);
      }
      if (Math.abs(positions[i * 3 + 2] - playerPosition.z) > range * 0.5) {
        positions[i * 3 + 2] = playerPosition.z + (Math.random() - 0.5) * range;
      }
    }

    this.lastPlayerPosition.copy(playerPosition);
    posAttr.needsUpdate = true;
  }
}
