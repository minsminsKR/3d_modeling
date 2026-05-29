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
}
