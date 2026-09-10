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
    this.loreNotes = [];
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
    this.loreNotes = [];
    this.finalExit = null;

    // Initial chunk loading around player spawn
    this.updateLoadedChunks(new THREE.Vector3(0, 0, 0));

    return {
      doors: this.doors,
      keys: this.keys,
      cabinets: this.cabinets,
      safeLights: this.safeLights,
      loreNotes: this.loreNotes,
      finalExit: this.finalExit,
      playerStart: new THREE.Vector3(0, 0, 0),
      pendingAssets: this.pendingAssets,
    };
  }

  updateLoadedChunks(playerPosition, chunkChanged = true, extraCenters = []) {
    const centers = [
      { x: playerPosition.x, z: playerPosition.z },
      ...extraCenters.filter((center) => Number.isFinite(center?.x) && Number.isFinite(center?.z)),
    ];
    const chunkOf = (x, z) => [
      Math.floor((x + 8) / 16),
      Math.floor((z + 8) / 16),
    ];
    const inRange = (cellX, cellZ, radius) => centers.some((center) => {
      const [pcx, pcz] = chunkOf(center.x, center.z);
      return Math.abs(cellX - pcx) <= radius && Math.abs(cellZ - pcz) <= radius;
    });
    const activeRadius = 2;
    const disableRadius = 3;
    const PINNED_CHUNKS = new Set(["0,0", "1,2", "-1,-1", "-2,2", "2,-2"]);

    let changed = false;

    // Heavy operations only when crossing chunk boundaries
    if (chunkChanged) {
      // 1. Unload chunks beyond disable radius
      for (const [key, chunk] of this.loadedChunks.entries()) {
        if (PINNED_CHUNKS.has(key)) continue;
        if (!inRange(chunk.cx, chunk.cz, disableRadius)) {
          this.generator.destroyChunk(chunk.cx, chunk.cz);
          this.loadedChunks.delete(key);

          // Remove unloaded items
          this.doors = this.doors.filter((d) => d.chunkId !== chunk.chunkId);
          this.keys = this.keys.filter((k) => k.chunkId !== chunk.chunkId);
          this.cabinets = this.cabinets.filter((c) => c.chunkId !== chunk.chunkId);
          this.safeLights = this.safeLights.filter((l) => l.chunkId !== chunk.chunkId);
          this.loreNotes = this.loreNotes.filter((n) => n.chunkId !== chunk.chunkId);
          changed = true;
        }
      }

      // 2. Filter out pending chunks in queue that are beyond disable radius
      this.loadQueue = this.loadQueue.filter((q) => inRange(q.cx, q.cz, disableRadius));

      // 3. Find missing chunks in active radius and enqueue them
      let anyNewMissing = false;
      const wanted = new Set();
      for (const center of centers) {
        const [pcx, pcz] = chunkOf(center.x, center.z);
        for (let dx = -activeRadius; dx <= activeRadius; dx++) {
          for (let dz = -activeRadius; dz <= activeRadius; dz++) {
            wanted.add(`${pcx + dx},${pcz + dz}`);
          }
        }
      }
      for (const key of wanted) {
        const [ncx, ncz] = key.split(",").map(Number);
        if (!this.loadedChunks.has(key)) {
          const inQueue = this.loadQueue.some((q) => q.cx === ncx && q.cz === ncz);
          if (!inQueue) {
            this.loadQueue.push({ cx: ncx, cz: ncz });
            anyNewMissing = true;
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
          for (const n of chunk.loreNotes || []) this.loreNotes.push(n);
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
        for (const n of chunk.loreNotes || []) this.loreNotes.push(n);
        if (chunk.finalExit) this.finalExit = chunk.finalExit;
        changed = true;
      }
    }

    return changed;
  }

  decorateChunk(chunk) {
    if (!chunk || chunk.atmosphereDecorated) return;
    if (chunk.type === "void") {
      chunk.atmosphereDecorated = true;
      return;
    }
    chunk.atmosphereDecorated = true;

    // Deterministic decoration keeps screenshots and gameplay tests reproducible.
    let seed = ((chunk.cx * 73856093) ^ (chunk.cz * 19349663) ^ 0x5f3759df) >>> 0;
    const random = () => {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      return seed / 4294967296;
    };

    const isStairVoid = chunk.type === "stairs_2f" || chunk.type === "stairs_b1";
    const isNarrowCorridor = chunk.type === "corridor_ns" || chunk.type === "corridor_ew" || chunk.type === "narrow_ns";
        const hasFixture = !isStairVoid && (chunk.type === "start" || isNarrowCorridor || chunk.type === "classroom" || chunk.type === "nurse_office" || chunk.type === "music_room" || chunk.type === "faculty_office" || chunk.type === "science_lab" || chunk.type === "gymnasium" || random() < 0.52);
    if (hasFixture) {
      const isUnstable = chunk.type === "flicker_room" || random() < 0.14;
      const fixtureMaterial = new THREE.MeshStandardMaterial({
        color: isUnstable ? 0x2a1810 : 0x3a2a1c,
        emissive: isUnstable ? 0x3a1408 : 0x1a1008,
        emissiveIntensity: isUnstable ? 0.18 : 0.04,
        roughness: 0.72,
        metalness: 0.12,
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

      const baseIntensity = isUnstable
        ? 1.15
        : 0.55 + random() * 0.35;
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
      const hallLike = chunk.type === "corridor_ns" || chunk.type === "narrow_ns"
        || chunk.type === "corridor_ew" || chunk.type === "t_junction" || chunk.type === "cross_junction"
        || chunk.type === "dead_end";
      if (hallLike) {
        const maze = this.generator?.isMazeHall?.(chunk.cx, chunk.cz);
        if (maze) {
          const along = 2.7 + random() * 1.5;
          if (random() > 0.5) {
            decal.position.set(chunk.center.x + side * along, chunk.floorY + 1.34, chunk.center.z + side * 1.82);
            decal.rotation.y = side < 0 ? 0 : Math.PI;
          } else {
            decal.position.set(chunk.center.x + side * 1.82, chunk.floorY + 1.34, chunk.center.z + side * along);
            decal.rotation.y = side < 0 ? Math.PI / 2 : -Math.PI / 2;
          }
        } else if (this.generator?.isRoofHallChunk?.(chunk.cx, chunk.cz)) {
          const along = 4.6 + random() * 1.6;
          decal.position.set(chunk.center.x + side * along, chunk.floorY + 1.34, chunk.center.z + side * 2.05);
          decal.rotation.y = side < 0 ? 0 : Math.PI;
        } else {
          const along = 4.6 + random() * 1.6;
          if (random() > 0.5) {
            decal.position.set(chunk.center.x + side * 1.19, chunk.floorY + 1.34, chunk.center.z + side * along);
            decal.rotation.y = side < 0 ? Math.PI / 2 : -Math.PI / 2;
          } else {
            decal.position.set(chunk.center.x + side * along, chunk.floorY + 1.34, chunk.center.z + side * 1.19);
            decal.rotation.y = side < 0 ? 0 : Math.PI;
          }
        }
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

    this.dressSchoolCorridor(chunk, random);
  }

  getClassroomDoorMaterial() {
    if (!this.classroomDoorMaterial) {
      this.classroomDoorMaterial = this.textures.createClassroomDoorMaterial();
    }
    return this.classroomDoorMaterial;
  }

  getRoomPlateMaterial(chunk, slot = 0) {
    if (!this.roomPlateCache) this.roomPlateCache = new Map();
    const kind = this.generator?.getHallNookKind?.(chunk.cx, chunk.cz, slot) || "class";
    const n = Math.abs(chunk.cx) * 12 + Math.abs(chunk.cz) * 5 + slot + (chunk.cx >= 4 ? 40 : 0);
    const grade = (n % 3) + 1;
    const klass = (n % 9) + 1;
    const annex = chunk.cx >= 4;
    let label = annex ? `별 ${grade}-${klass}` : `${grade}-${klass} 교실`;
    if (kind === "library") label = annex ? "별 도서실" : "도서실";
    else if (kind === "washroom") label = annex ? "별 화장실" : "화장실";
    else if (kind === "boarded") label = "폐쇄";
    if (chunk.type === "courtyard") label = "중정";
    if (chunk.type === "auditorium") label = "강당";
    if (chunk.type === "foyer") label = "로비";
    if (chunk.type === "art_room") label = "미술실";
    if (chunk.type === "studio") label = "촬영실";
    if (chunk.type === "broadcast") label = "방송실";
    if (chunk.type === "darkroom") label = "암실";
    if (chunk.type === "greenroom") label = "대기실";
    if (chunk.type === "home_ec") label = "가정실";
    if (chunk.type === "club_room") label = "서도부";
    if (chunk.cx === 3 && chunk.cz === 0) label = "연결복도";
    if (chunk.cx === 6 && chunk.cz === 0) label = "기념관";
    if (chunk.cx === 7 && chunk.cz === 0) label = "트로피";
    if (chunk.cx === 4 && chunk.cz === 1) label = "아케이드";
    if (chunk.cx === 7 && chunk.cz === 2) label = "연습실";
    if (chunk.cx === 6 && chunk.cz === -1) label = "표본";
    if (chunk.cx === 6 && chunk.cz === 2) label = "무대";
    if (chunk.cx === 4 && chunk.cz === -1) label = "세탁";
    if (chunk.cx === 5 && chunk.cz === -2) label = "실험";
    if (chunk.type === "flicker_room") label = "시청각";
    if (chunk.type === "wide_room") label = "비품";
    if (chunk.type === "omen_room") label = "상담실";
    if (chunk.type === "static_room") label = "방송창고";
    if (chunk.cx === 1 && chunk.cz === 1) label = "지하";
    if (chunk.cx === 2 && chunk.cz === 1) label = "보육";
    if (chunk.cx === -2 && chunk.cz === 1) label = "인형";
    if (chunk.cx === -2 && chunk.cz === -1) label = "서고";
    if (chunk.cx === 2 && chunk.cz === -1) label = "창고";
    if (chunk.cx === -1 && chunk.cz === 1) label = "다실";
    if (this.roomPlateCache.has(label)) return this.roomPlateCache.get(label);
    const canvas = document.createElement("canvas");
    canvas.width = 256;
    canvas.height = 96;
    const ctx = canvas.getContext("2d");
    ctx.fillStyle = "#1a120c";
    ctx.fillRect(0, 0, 256, 96);
    ctx.fillStyle = "#3a2a18";
    ctx.fillRect(6, 6, 244, 84);
    ctx.fillStyle = kind === "boarded" ? "#8a4030" : "#c4b089";
    ctx.font = label.length > 6 ? "24px serif" : "28px serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(label, 128, 48);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    const mat = new THREE.MeshStandardMaterial({
      map: texture,
      roughness: 0.85,
      metalness: 0.05,
    });
    this.roomPlateCache.set(label, mat);
    return mat;
  }

  dressSchoolCorridor(chunk, random) {
    if (chunk.type === "void" || chunk.type === "stairs_2f" || chunk.type === "stairs_b1") return;

    const maze = this.generator?.isMazeHall?.(chunk.cx, chunk.cz);
    const chicanes = this.generator?.getHallChicanes?.(chunk.cx, chunk.cz) || {};
    if (maze) {
      this.dressMazeHallProps(chunk, random, chicanes);
      return;
    }

    const roofHall = this.generator?.isRoofHallChunk?.(chunk.cx, chunk.cz);
    const hallLike = chunk.type === "start" || chunk.type === "corridor_ns" || chunk.type === "corridor_ew"
      || chunk.type === "t_junction" || chunk.type === "cross_junction" || chunk.type === "narrow_ns"
      || chunk.type === "dead_end";

    // Roof is an L-run, not a plus. Skip the copied ±1.18 classroom kit.
    if (!roofHall) {
      const plate = new THREE.Mesh(new THREE.PlaneGeometry(0.5, 0.18), this.getRoomPlateMaterial(chunk));
      plate.position.set(chunk.center.x - 1.18, chunk.floorY + 2.08, chunk.center.z - 4.6);
      plate.rotation.y = Math.PI / 2;
      plate.name = `${chunk.chunkId}_room_plate`;
      plate.renderOrder = 2;
      this.scene.add(plate);
      chunk.meshes.push(plate);
    }

    if (hallLike && !roofHall) {
      const doorMat = this.getClassroomDoorMaterial();
      const doorOffsets = [-3.35, 3.35];
      for (let i = 0; i < doorOffsets.length; i += 1) {
        const side = i === 0 ? -1 : 1;
        const door = new THREE.Mesh(new THREE.PlaneGeometry(0.92, 2.08), doorMat);
        door.position.set(
          chunk.center.x + side * 1.185,
          chunk.floorY + 1.08,
          chunk.center.z + doorOffsets[i],
        );
        door.rotation.y = side < 0 ? Math.PI / 2 : -Math.PI / 2;
        door.name = `${chunk.chunkId}_class_door_${i}`;
        door.castShadow = false;
        door.receiveShadow = true;
        this.scene.add(door);
        chunk.meshes.push(door);
      }
    }

    if (!roofHall && random() < 0.72) {
      const ofuda = new THREE.Mesh(
        new THREE.PlaneGeometry(0.12, 0.28),
        new THREE.MeshStandardMaterial({
          color: 0xd8c49a,
          roughness: 0.9,
          side: THREE.DoubleSide,
        }),
      );
      ofuda.position.set(chunk.center.x + 1.18, chunk.floorY + 1.85, chunk.center.z + (random() - 0.5) * 6);
      ofuda.rotation.y = -Math.PI / 2;
      ofuda.rotation.z = (random() - 0.5) * 0.18;
      ofuda.name = `${chunk.chunkId}_ofuda`;
      this.scene.add(ofuda);
      chunk.meshes.push(ofuda);
    }

    if (!roofHall && random() < 0.78) {
      const clock = new THREE.Mesh(
        new THREE.CircleGeometry(0.16, 20),
        new THREE.MeshStandardMaterial({
          color: 0xcfc6b0,
          emissive: 0x1a120c,
          emissiveIntensity: 0.06,
          roughness: 0.55,
        }),
      );
      clock.position.set(chunk.center.x - 1.18, chunk.floorY + 2.22, chunk.center.z + (random() - 0.5) * 3.5);
      clock.rotation.y = Math.PI / 2;
      clock.name = `${chunk.chunkId}_stopped_clock`;
      this.scene.add(clock);
      chunk.meshes.push(clock);
    }

    if (hallLike || random() < 0.55) {
      const arrow = new THREE.Mesh(
        new THREE.PlaneGeometry(0.55, 0.18),
        new THREE.MeshStandardMaterial({
          color: 0x6a1810,
          emissive: 0x2a0808,
          emissiveIntensity: 0.15,
          roughness: 0.8,
          side: THREE.DoubleSide,
        }),
      );
      arrow.rotation.x = -Math.PI / 2;
      arrow.rotation.z = random() > 0.5 ? Math.PI / 2 : 0;
      arrow.position.set(
        chunk.center.x + (random() - 0.5) * 1.2,
        chunk.floorY + 0.012,
        chunk.center.z + (random() - 0.5) * 4,
      );
      arrow.name = `${chunk.chunkId}_false_arrow`;
      this.scene.add(arrow);
      chunk.meshes.push(arrow);
    }
  }

  dressMazeHallProps(chunk, random, chicanes) {
    const c = chunk.center;
    const y = chunk.floorY;
    const gen = this.generator;
    const sides = gen.getHallSides?.(chunk.cx, chunk.cz) || { n: 1.7, s: 1.7, e: 1.7, w: 1.7 };
    const mask = gen.getHallNookMask?.(chunk.cx, chunk.cz) || { n: true, s: true, e: true, w: true };
    const doorsOf = (side) => gen.getHallDoorAlong?.(chunk.cx, chunk.cz, side) || [-5.25, 5.25];
    const sideSign = (alongs, i) => gen.hallDoorSideSign?.(alongs, i)
      ?? (alongs.length <= 1 ? ((alongs[0] || 0) < 0 ? 1 : -1) : (i === 0 ? 1 : -1));
    const addPlate = (lx, lz, yaw, name, slot) => {
      const plate = new THREE.Mesh(new THREE.PlaneGeometry(0.55, 0.2), this.getRoomPlateMaterial(chunk, slot));
      plate.position.set(c.x + lx, y + 2.08, c.z + lz);
      plate.rotation.y = yaw;
      plate.name = `${chunk.chunkId}_${name}`;
      plate.renderOrder = 2;
      this.scene.add(plate);
      chunk.meshes.push(plate);
    };
    if (chicanes.ew) {
      if (mask.n) {
        const doors = doorsOf("n");
        doors.forEach((along, i) => {
          addPlate(along + sideSign(doors, i) * 1.32, -(sides.n + 0.08), 0, `room_plate_n_${i}`, i);
        });
      }
      if (mask.s) {
        const doors = doorsOf("s");
        doors.forEach((along, i) => {
          addPlate(along + sideSign(doors, i) * 1.32, sides.s + 0.08, Math.PI, `room_plate_s_${i}`, 2 + i);
        });
      }
    } else if (chicanes.ns) {
      if (mask.w) {
        const doors = doorsOf("w");
        doors.forEach((along, i) => {
          addPlate(-(sides.w + 0.08), along + sideSign(doors, i) * 1.32, Math.PI / 2, `room_plate_w_${i}`, i);
        });
      }
      if (mask.e) {
        const doors = doorsOf("e");
        doors.forEach((along, i) => {
          addPlate(sides.e + 0.08, along + sideSign(doors, i) * 1.32, -Math.PI / 2, `room_plate_e_${i}`, 2 + i);
        });
      }
    }

    const skipCloneKit = gen.isClosedIdentityHall?.(chunk.cx, chunk.cz)
      || gen.isGlassHallChunk?.(chunk.cx, chunk.cz);
    if (!skipCloneKit && random() < 0.8) {
      const ofuda = new THREE.Mesh(
        new THREE.PlaneGeometry(0.12, 0.28),
        new THREE.MeshStandardMaterial({
          color: 0xd8c49a,
          roughness: 0.9,
          side: THREE.DoubleSide,
        }),
      );
      if (chicanes.ew) {
        ofuda.position.set(c.x + 3.35, y + 1.85, c.z - (sides.n + 0.08));
        ofuda.rotation.y = 0;
      } else {
        ofuda.position.set(c.x - (sides.w + 0.08), y + 1.85, c.z + 3.35);
        ofuda.rotation.y = Math.PI / 2;
      }
      ofuda.rotation.z = (random() - 0.5) * 0.18;
      ofuda.name = `${chunk.chunkId}_ofuda`;
      this.scene.add(ofuda);
      chunk.meshes.push(ofuda);
    }

    if (skipCloneKit) return;
    const arrow = new THREE.Mesh(
      new THREE.PlaneGeometry(0.55, 0.18),
      new THREE.MeshStandardMaterial({
        color: 0x6a1810,
        emissive: 0x2a0808,
        emissiveIntensity: 0.15,
        roughness: 0.8,
        side: THREE.DoubleSide,
      }),
    );
    arrow.rotation.x = -Math.PI / 2;
    arrow.rotation.z = chicanes.ns && !chicanes.ew ? Math.PI / 2 : 0;
    arrow.position.set(c.x, y + 0.012, c.z);
    arrow.name = `${chunk.chunkId}_false_arrow`;
    this.scene.add(arrow);
    chunk.meshes.push(arrow);
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
