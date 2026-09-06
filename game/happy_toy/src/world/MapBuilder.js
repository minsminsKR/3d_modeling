import * as THREE from "three";
import { MAP_CONFIG } from "../config/gameConfig.js";
import { TextureLibrary } from "./TextureLibrary.js";
import { BackroomsGenerator } from "./BackroomsGenerator.js";

export class MapBuilder {
  constructor(scene, collisionWorld, options = {}) {
    this.scene = scene;
    this.collisionWorld = collisionWorld;
    this.mapConfig = options.mapConfig || MAP_CONFIG;
    this.debugEnabled = options.debugEnabled ?? false;
    this.doors = [];
    this.keys = [];
    this.cabinets = [];
    this.safeLights = [];
    this.finalExit = null;
    this.game = options.game;
    this.textures = new TextureLibrary();
    this.generator = new BackroomsGenerator(scene, collisionWorld, this.textures, 12345, this.game);
    this.loadedChunks = new Map();
    this.loadQueue = [];
    this.pendingAssets = this.generator.pendingAssets;
    this.fixtureGeometry = new THREE.BoxGeometry(0.72, 0.055, 0.16);
    this.grimeGeometry = new THREE.PlaneGeometry(1, 1);
    this.grimeMaterial = this.createGrimeMaterial();
    this.wallDecayGeometry = new THREE.PlaneGeometry(2.1, 2.5);
    this.wallDecayMaterial = this.textures.createWallDecayDecalMaterial();
  }

  build() {
    this.loadedChunks.clear();
    this.loadQueue = [];
    this.doors = [];
    this.keys = [];
    this.cabinets = [];
    this.safeLights = [];
    this.finalExit = null;

    // Initial chunk loading around player spawn
    this.updateLoadedChunks(new THREE.Vector3(0, 0, 0));

    return {
      doors: this.doors,
      keys: this.keys,
      cabinets: this.cabinets,
      safeLights: this.safeLights,
      finalExit: this.finalExit,
      playerStart: new THREE.Vector3(0, 0, 0),
      pendingAssets: this.pendingAssets,
    };
  }

  updateLoadedChunks(playerPosition, chunkChanged = true) {
    const cx = Math.floor((playerPosition.x + 8) / 16);
    const cz = Math.floor((playerPosition.z + 8) / 16);
    const activeRadius = 3;
    const disableRadius = 5;

    let changed = false;

    // Heavy operations only when crossing chunk boundaries
    if (chunkChanged) {
      // 1. Unload chunks beyond disable radius
      for (const [key, chunk] of this.loadedChunks.entries()) {
        const dx = Math.abs(chunk.cx - cx);
        const dz = Math.abs(chunk.cz - cz);
        if (dx > disableRadius || dz > disableRadius) {
          this.generator.destroyChunk(chunk.cx, chunk.cz);
          this.loadedChunks.delete(key);

          // Remove unloaded items
          this.doors = this.doors.filter((d) => d.chunkId !== chunk.chunkId);
          this.keys = this.keys.filter((k) => k.chunkId !== chunk.chunkId);
          this.cabinets = this.cabinets.filter((c) => c.chunkId !== chunk.chunkId);
          this.safeLights = this.safeLights.filter((l) => l.chunkId !== chunk.chunkId);
          if (this.finalExit && this.finalExit.chunkId === chunk.chunkId) {
            this.finalExit = null;
          }
          changed = true;
        }
      }

      // 2. Filter out pending chunks in queue that are beyond disable radius
      this.loadQueue = this.loadQueue.filter((q) => {
        const dx = Math.abs(q.cx - cx);
        const dz = Math.abs(q.cz - cz);
        return dx <= disableRadius && dz <= disableRadius;
      });

      // 3. Find missing chunks in active radius and enqueue them
      let anyNewMissing = false;
      for (let dx = -activeRadius; dx <= activeRadius; dx++) {
        for (let dz = -activeRadius; dz <= activeRadius; dz++) {
          const ncx = cx + dx;
          const ncz = cz + dz;
          const key = `${ncx},${ncz}`;

          if (!this.loadedChunks.has(key)) {
            const inQueue = this.loadQueue.some((q) => q.cx === ncx && q.cz === ncz);
            if (!inQueue) {
              this.loadQueue.push({ cx: ncx, cz: ncz });
              anyNewMissing = true;
            }
          }
        }
      }

      // Sort queue only when new items were added (not every frame)
      if (anyNewMissing && this.loadQueue.length > 0) {
        const px = playerPosition.x;
        const pz = playerPosition.z;
        this.loadQueue.sort((a, b) => {
          const distA = Math.hypot(a.cx * 16 - px, a.cz * 16 - pz);
          const distB = Math.hypot(b.cx * 16 - px, b.cz * 16 - pz);
          return distA - distB;
        });
      }
    }

    // 4. Initial load: if no chunks loaded yet, load everything synchronously
    if (this.loadedChunks.size === 0) {
      // Drain the queue synchronously for first spawn
      while (this.loadQueue.length > 0) {
        const item = this.loadQueue.shift();
        const key = `${item.cx},${item.cz}`;
        if (!this.loadedChunks.has(key)) {
          const chunk = this.generator.generateChunk(item.cx, item.cz);
          this.decorateChunk(chunk);
          this.loadedChunks.set(key, chunk);
          for (const d of chunk.doors) this.doors.push(d);
          for (const k of chunk.keys) this.keys.push(k);
          for (const c of chunk.cabinets) this.cabinets.push(c);
          for (const l of chunk.safeLights) this.safeLights.push(l);
          if (chunk.finalExit) this.finalExit = chunk.finalExit;
        }
      }
      return true;
    }

    // 5. Process ONE chunk from queue per frame (sliced loading)
    if (this.loadQueue.length > 0) {
      const next = this.loadQueue.shift();
      const key = `${next.cx},${next.cz}`;

      if (!this.loadedChunks.has(key)) {
        const t0 = performance.now();
        const chunk = this.generator.generateChunk(next.cx, next.cz);
        this.decorateChunk(chunk);
        const dt = performance.now() - t0;
        if (dt > 4) {
          console.warn(`[PERF] MapBuilder: generateChunk ${next.cx},${next.cz} took ${dt.toFixed(2)}ms`);
        }
        this.loadedChunks.set(key, chunk);
        for (const d of chunk.doors) this.doors.push(d);
        for (const k of chunk.keys) this.keys.push(k);
        for (const c of chunk.cabinets) this.cabinets.push(c);
        for (const l of chunk.safeLights) this.safeLights.push(l);
        if (chunk.finalExit) this.finalExit = chunk.finalExit;
        changed = true;
      }
    }

    return changed;
  }

  decorateChunk(chunk) {
    if (!chunk || chunk.atmosphereDecorated) return;
    chunk.atmosphereDecorated = true;

    // Deterministic decoration keeps screenshots and gameplay tests reproducible.
    let seed = ((chunk.cx * 73856093) ^ (chunk.cz * 19349663) ^ 0x5f3759df) >>> 0;
    const random = () => {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      return seed / 4294967296;
    };

    const isStairVoid = chunk.type === "stairs_2f" || chunk.type === "stairs_b1";
    const isNarrowCorridor = chunk.type === "corridor_ns" || chunk.type === "corridor_ew" || chunk.type === "narrow_ns";
    const hasFixture = !isStairVoid && (chunk.type === "start" || isNarrowCorridor || random() < 0.52);
    if (hasFixture) {
      const isUnstable = chunk.type === "flicker_room" || random() < 0.14;
      const fixtureMaterial = new THREE.MeshStandardMaterial({
        color: isUnstable ? 0x7a4c2c : 0xa8794f,
        emissive: isUnstable ? 0x4d1907 : 0x8a4218,
        emissiveIntensity: isUnstable ? 0.46 : 0.78,
        roughness: 0.54,
        metalness: 0.08,
      });
      const fixture = new THREE.Mesh(this.fixtureGeometry, fixtureMaterial);
      const offsetX = (random() - 0.5) * 1.2;
      const offsetZ = (random() - 0.5) * 1.2;
      fixture.position.set(chunk.center.x + offsetX, chunk.floorY + 2.745, chunk.center.z + offsetZ);
      fixture.rotation.y = random() > 0.5 ? 0 : Math.PI / 2;
      fixture.name = `${chunk.chunkId}_ambient_fixture`;
      fixture.castShadow = false;
      fixture.receiveShadow = false;
      this.scene.add(fixture);
      chunk.meshes.push(fixture);

      const baseIntensity = isUnstable ? 5.6 : 8.4 + random() * 1.1;
      chunk.lights.push({
        mesh: fixture,
        localPos: new THREE.Vector3(offsetX, 2.42, offsetZ),
        baseIntensity,
        currentIntensity: baseIntensity,
        isFlickering: isUnstable,
        flickerTimer: 0.6 + random() * 4.0,
        voltagePhase: random() * Math.PI * 2,
        pooledLight: null,
      });
    }

    // Soft, irregular floor grime breaks up repeated 16m tiles without geometry
    // collisions. One instanced draw call per chunk keeps this dressing cheap.
    const flooded = chunk.cz === 1 && (chunk.cx === -1 || chunk.cx === 0 || chunk.cx === 1);
    if (!flooded && random() < 0.72) {
      const stainCount = 2 + Math.floor(random() * 2);
      const stains = new THREE.InstancedMesh(this.grimeGeometry, this.grimeMaterial, stainCount);
      const matrix = new THREE.Matrix4();
      const quaternion = new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0));
      const position = new THREE.Vector3();
      const scale = new THREE.Vector3();
      for (let i = 0; i < stainCount; i++) {
        position.set(
          chunk.center.x + (random() - 0.5) * 11.5,
          chunk.floorY + 0.008,
          chunk.center.z + (random() - 0.5) * 11.5,
        );
        const width = 0.55 + random() * 1.7;
        scale.set(width, width * (0.45 + random() * 0.75), 1);
        matrix.compose(position, quaternion, scale);
        stains.setMatrixAt(i, matrix);
      }
      stains.instanceMatrix.needsUpdate = true;
      stains.computeBoundingSphere();
      stains.name = `${chunk.chunkId}_floor_grime`;
      stains.frustumCulled = true;
      stains.renderOrder = 1;
      this.scene.add(stains);
      chunk.meshes.push(stains);
    }

    if (!isStairVoid && chunk.type !== "start" && random() < 0.27) {
      const decal = new THREE.Mesh(this.wallDecayGeometry, this.wallDecayMaterial);
      const side = random() > 0.5 ? 1 : -1;
      if (chunk.type === "corridor_ns" || chunk.type === "narrow_ns") {
        decal.position.set(chunk.center.x + side * 1.19, chunk.floorY + 1.34, chunk.center.z + (random() - 0.5) * 8.5);
        decal.rotation.y = side < 0 ? Math.PI / 2 : -Math.PI / 2;
      } else if (chunk.type === "corridor_ew") {
        decal.position.set(chunk.center.x + (random() - 0.5) * 8.5, chunk.floorY + 1.34, chunk.center.z + side * 1.19);
        decal.rotation.y = side < 0 ? 0 : Math.PI;
      } else {
        const useSouthWall = chunk.type === "workshop" || chunk.type === "playroom" || random() > 0.7;
        decal.position.set(
          chunk.center.x + (random() - 0.5) * 8.5,
          chunk.floorY + 1.34,
          chunk.center.z + (useSouthWall ? 7.59 : -7.59),
        );
        decal.rotation.y = useSouthWall ? Math.PI : 0;
      }
      const decalScale = 0.78 + random() * 0.42;
      decal.scale.set(decalScale, decalScale, 1);
      decal.name = `${chunk.chunkId}_wall_decay_decal`;
      decal.castShadow = false;
      decal.receiveShadow = true;
      decal.renderOrder = 1;
      this.scene.add(decal);
      chunk.meshes.push(decal);
    }
  }

  createGrimeMaterial() {
    const canvas = document.createElement("canvas");
    canvas.width = 96;
    canvas.height = 96;
    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    let seed = 0xdecafbad;
    const random = () => {
      seed = (Math.imul(seed, 1103515245) + 12345) >>> 0;
      return seed / 4294967296;
    };
    for (let i = 0; i < 9; i++) {
      const x = 20 + random() * 56;
      const y = 18 + random() * 60;
      const radius = 7 + random() * 22;
      const gradient = ctx.createRadialGradient(x, y, 0, x, y, radius);
      gradient.addColorStop(0, `rgba(62, 27, 14, ${0.22 + random() * 0.16})`);
      gradient.addColorStop(0.55, "rgba(45, 25, 15, 0.12)");
      gradient.addColorStop(1, "rgba(25, 16, 12, 0)");
      ctx.fillStyle = gradient;
      ctx.fillRect(x - radius, y - radius, radius * 2, radius * 2);
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.generateMipmaps = true;
    texture.minFilter = THREE.LinearMipmapLinearFilter;
    texture.magFilter = THREE.LinearFilter;
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: 0x5b3020,
      transparent: true,
      opacity: 0.34,
      roughness: 1,
      metalness: 0,
      depthWrite: false,
      polygonOffset: true,
      polygonOffsetFactor: -1,
      polygonOffsetUnits: -1,
    });
  }
}
