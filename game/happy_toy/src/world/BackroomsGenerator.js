import * as THREE from "three";
import { Door } from "./Door.js";
import { Cabinet } from "./Cabinet.js";
import { KeyItem } from "./KeyItem.js";
import { FinalExit } from "./FinalExit.js";

// Deterministic seed-based random generator (Mulberry32)
export function createRandom(seed) {
  return function() {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function getChunkSeed(baseSeed, cx, cz) {
  let h = baseSeed ^ (cx * 73856093) ^ (cz * 19349663);
  return h >>> 0;
}

export class BackroomsGenerator {
  constructor(scene, collisionWorld, textureLibrary, baseSeed = 12345) {
    this.scene = scene;
    this.collisionWorld = collisionWorld;
    this.textures = textureLibrary;
    this.baseSeed = baseSeed;
    this.chunksData = new Map();
    this.geometryCache = new Map();
    this.lightPanelGeo = new THREE.BoxGeometry(1.2, 0.05, 0.6);
    this.unitBoxGeo = new THREE.BoxGeometry(1, 1, 1);
    this.lightPanelMat = new THREE.MeshBasicMaterial({
      color: 0xfffee4,
      toneMapped: false,
    });
    this.trimMaterial = new THREE.MeshStandardMaterial({
      color: 0x161510,
      roughness: 0.8,
    });
    this.propMaterial = new THREE.MeshStandardMaterial({
      color: 0x5a7d9a,
      roughness: 0.8,
    });
  }

  getBoxGeometry(x, y, z) {
    const key = `box_${x.toFixed(2)}_${y.toFixed(2)}_${z.toFixed(2)}`;
    if (this.geometryCache.has(key)) {
      return this.geometryCache.get(key);
    }
    const geo = new THREE.BoxGeometry(x, y, z);
    this.geometryCache.set(key, geo);
    return geo;
  }

  getPlaneGeometry(w, h) {
    const key = `plane_${w.toFixed(2)}_${h.toFixed(2)}`;
    if (this.geometryCache.has(key)) {
      return this.geometryCache.get(key);
    }
    const geo = new THREE.PlaneGeometry(w, h);
    this.geometryCache.set(key, geo);
    return geo;
  }

  getChunkKey(cx, cz) {
    return `${cx},${cz}`;
  }

  getChunkType(cx, cz) {
    if (cx === 0 && cz === 0) return "start";
    if (cx === 2 && cz === 2) return "workshop";
    if (cx === -2 && cz === 2) return "playroom";
    if (cx === 2 && cz === -2) return "storage";
    if (cx === -2 && cz === -2) return "event";

    const seed = getChunkSeed(this.baseSeed, cx, cz);
    const rand = createRandom(seed)();

    if (rand < 0.05) return "flicker_room";
    if (rand < 0.15) return "pillar_room";
    if (rand < 0.28) return "wide_room";
    if (rand < 0.40) return "corridor_ns";
    if (rand < 0.52) return "corridor_ew";
    if (rand < 0.62) return "narrow_ns";
    if (rand < 0.72) return "t_junction";
    if (rand < 0.84) return "corner";
    if (rand < 0.94) return "dead_end";
    return "cross_junction";
  }

  generateChunk(cx, cz) {
    const key = this.getChunkKey(cx, cz);
    if (this.chunksData.has(key)) {
      return this.chunksData.get(key);
    }

    const tStart = performance.now();

    const type = this.getChunkType(cx, cz);
    const center = new THREE.Vector3(cx * 16, 0, cz * 16);
    const chunkId = `chunk_${cx}_${cz}`;
    const seed = getChunkSeed(this.baseSeed, cx, cz);
    const rand = createRandom(seed);

    const chunk = {
      cx,
      cz,
      type,
      center,
      chunkId,
      meshes: [],
      lights: [],
      doors: [],
      keys: [],
      cabinets: [],
      finalExit: null,
      waypoints: [],
    };

    // 1. Create floor and ceiling
    const tFloor0 = performance.now();
    const floorGeo = this.getPlaneGeometry(16, 16);
    const floorMat = this.textures.createFloorMaterial(16, 16);
    const floor = new THREE.Mesh(floorGeo, floorMat);
    floor.rotation.x = -Math.PI / 2;
    floor.position.set(center.x, 0, center.z);
    floor.receiveShadow = true;
    floor.name = `${chunkId}_floor`;
    this.scene.add(floor);
    chunk.meshes.push(floor);

    const ceilingGeo = this.getPlaneGeometry(16, 16);
    const ceilingMat = this.textures.createCeilingMaterial(16, 16);
    const ceiling = new THREE.Mesh(ceilingGeo, ceilingMat);
    ceiling.rotation.x = Math.PI / 2;
    ceiling.position.set(center.x, 2.8, center.z);
    ceiling.receiveShadow = true;
    ceiling.name = `${chunkId}_ceiling`;
    this.scene.add(ceiling);
    chunk.meshes.push(ceiling);

    // Register floor area in CollisionWorld
    this.collisionWorld.addFloorArea({
      id: `${chunkId}_walkable`,
      floor: 1,
      type: "walkable",
      y: 0,
      minX: center.x - 8,
      maxX: center.x + 8,
      minZ: center.z - 8,
      maxZ: center.z + 8,
    }, chunkId);
    const dtFloor = performance.now() - tFloor0;

    // 2. Build Walls based on template
    const tWalls0 = performance.now();
    this.buildTemplateWalls(chunk, type, center, chunkId, rand);
    const dtWalls = performance.now() - tWalls0;

    // 3. Add light panels (emissive mesh)
    const tLights0 = performance.now();
    this.buildCeilingLights(chunk, type, center, chunkId, rand);
    const dtLights = performance.now() - tLights0;

    // 4. Place special interactables (doors, cabinets, keys, final exit)
    const tInteract0 = performance.now();
    this.buildInteractables(chunk, type, center, chunkId, rand);
    const dtInteract = performance.now() - tInteract0;

    // 5. Build waypoints for AI patrolling
    const tWaypoints0 = performance.now();
    this.buildWaypoints(chunk, type, center);
    const dtWaypoints = performance.now() - tWaypoints0;

    const dtTotal = performance.now() - tStart;
    if (dtTotal > 1.0) {
      console.warn(`[PERF] generateChunk (${type} at ${cx},${cz}) took ${dtTotal.toFixed(2)}ms: floor=${dtFloor.toFixed(2)}ms, walls=${dtWalls.toFixed(2)}ms, lights=${dtLights.toFixed(2)}ms, interactables=${dtInteract.toFixed(2)}ms, waypoints=${dtWaypoints.toFixed(2)}ms`);
    }

    this.chunksData.set(key, chunk);
    return chunk;
  }

  buildTemplateWalls(chunk, type, center, chunkId, rand) {
    const wallMaterial = this.textures.createWallMaterial();
    const trimMaterial = this.trimMaterial;

    const wallsData = [];

    const addWallSegment = (localX, localZ, sizeX, sizeZ, name) => {
      wallsData.push({ localX, localZ, sizeX, sizeZ, name });
      const meshName = `${chunkId}_wall_${name}`;
      const globalPos = new THREE.Vector3(center.x + localX, 1.4, center.z + localZ);
      this.collisionWorld.addStaticBox(meshName, globalPos, new THREE.Vector3(sizeX, 2.8, sizeZ), chunkId);
    };

    // Connections: N, S, E, W (true = open, false = closed)
    let N = true, S = true, E = true, W = true;

    if (type === "corridor_ns" || type === "narrow_ns") {
      E = false; W = false;
    } else if (type === "corridor_ew") {
      N = false; S = false;
    } else if (type === "t_junction") {
      W = false; // Closed on West
    } else if (type === "corner") {
      N = false; W = false; // Closed North and West (South-East bend)
    } else if (type === "dead_end") {
      N = false; E = false; W = false; // Only South is open
    } else if (type === "workshop" || type === "playroom" || type === "storage" || type === "event") {
      // Special rooms: keep some openings, close others
      if (type === "workshop") { S = false; W = false; }
      if (type === "playroom") { S = false; E = false; }
      if (type === "storage") { N = false; W = false; }
      if (type === "event") { N = false; E = false; }
    }

    // Build Boundary Walls
    // North Border (z = -7.8)
    if (N) {
      addWallSegment(-5.0, -7.8, 6.0, 0.4, "n_left");
      addWallSegment(5.0, -7.8, 6.0, 0.4, "n_right");
    } else {
      addWallSegment(0.0, -7.8, 16.0, 0.4, "n_solid");
    }

    // South Border (z = 7.8)
    if (S) {
      addWallSegment(-5.0, 7.8, 6.0, 0.4, "s_left");
      addWallSegment(5.0, 7.8, 6.0, 0.4, "s_right");
    } else {
      addWallSegment(0.0, 7.8, 16.0, 0.4, "s_solid");
    }

    // West Border (x = -7.8)
    if (W) {
      addWallSegment(-7.8, -5.0, 0.4, 6.0, "w_top");
      addWallSegment(-7.8, 5.0, 0.4, 6.0, "w_bottom");
    } else {
      addWallSegment(-7.8, 0.0, 0.4, 16.0, "w_solid");
    }

    // East Border (x = 7.8)
    if (E) {
      addWallSegment(7.8, -5.0, 0.4, 6.0, "e_top");
      addWallSegment(7.8, 5.0, 0.4, 6.0, "e_bottom");
    } else {
      addWallSegment(7.8, 0.0, 0.4, 16.0, "e_solid");
    }

    // Build Interior Walls/Pillars
    if (type === "narrow_ns") {
      // Narrowing panels inside the corridor
      addWallSegment(-2.2, 0.0, 0.4, 6.0, "narrow_w");
      addWallSegment(2.2, 0.0, 0.4, 6.0, "narrow_e");
    } else if (type === "pillar_room") {
      // 4 large square pillars
      addWallSegment(-3.5, -3.5, 1.5, 1.5, "pillar_nw");
      addWallSegment(3.5, -3.5, 1.5, 1.5, "pillar_ne");
      addWallSegment(-3.5, 3.5, 1.5, 1.5, "pillar_sw");
      addWallSegment(3.5, 3.5, 1.5, 1.5, "pillar_se");
    } else if (type === "corner") {
      // Corner inner block to enforce L-turn
      addWallSegment(-3.0, -3.0, 10.0, 10.0, "corner_inner");
    } else if (type === "toy_storage" || type === "storage" || type === "workshop" || type === "playroom") {
      // Place small partitioning dividers
      if (rand() < 0.5) {
        addWallSegment(-4.0, 0.0, 4.0, 0.4, "divider_w");
      } else {
        addWallSegment(4.0, 0.0, 4.0, 0.4, "divider_e");
      }
    } else if (type === "event") {
      // Transformation event room partitioning wall for painting
      addWallSegment(-5.0, 0.0, 0.4, 8.0, "event_partition");
    }

    // Build Instanced Meshes
    if (wallsData.length > 0) {
      const count = wallsData.length;

      const wallInst = new THREE.InstancedMesh(this.unitBoxGeo, wallMaterial, count);
      wallInst.name = `${chunkId}_walls_inst`;
      wallInst.castShadow = true;
      wallInst.receiveShadow = true;

      const trimInst = new THREE.InstancedMesh(this.unitBoxGeo, trimMaterial, count);
      trimInst.name = `${chunkId}_trims_inst`;
      trimInst.castShadow = true;

      const matrix = new THREE.Matrix4();
      const position = new THREE.Vector3();
      const rotation = new THREE.Quaternion(); // No rotation (0,0,0,1)
      const scale = new THREE.Vector3();

      wallsData.forEach((w, index) => {
        // Wall Matrix
        position.set(center.x + w.localX, 1.4, center.z + w.localZ);
        scale.set(w.sizeX, 2.8, w.sizeZ);
        matrix.compose(position, rotation, scale);
        wallInst.setMatrixAt(index, matrix);

        // Trim Matrix
        position.set(center.x + w.localX, 0.04, center.z + w.localZ);
        scale.set(w.sizeX + 0.02, 0.08, w.sizeZ + 0.02);
        matrix.compose(position, rotation, scale);
        trimInst.setMatrixAt(index, matrix);
      });

      wallInst.instanceMatrix.needsUpdate = true;
      trimInst.instanceMatrix.needsUpdate = true;

      wallInst.computeBoundingSphere();
      trimInst.computeBoundingSphere();

      this.scene.add(wallInst);
      this.scene.add(trimInst);

      chunk.meshes.push(wallInst);
      chunk.meshes.push(trimInst);
    }
  }

  buildCeilingLights(chunk, type, center, chunkId, rand) {
    const spawnLightPanel = (localX, localZ) => {
      const mesh = new THREE.Mesh(this.lightPanelGeo, this.lightPanelMat.clone());
      mesh.position.set(center.x + localX, 2.78, center.z + localZ);
      mesh.name = `${chunkId}_light`;
      this.scene.add(mesh);
      chunk.meshes.push(mesh);

      const isFlickering = type === "flicker_room" || rand() < 0.15;
      const lightData = {
        mesh,
        localPos: new THREE.Vector3(localX, 2.6, localZ),
        pointLight: null,
        isFlickering,
        flickerTimer: rand() * 5,
        baseIntensity: 3.5,
      };
      chunk.lights.push(lightData);
    };

    if (type === "corridor_ns" || type === "narrow_ns") {
      spawnLightPanel(0.0, -4.0);
      spawnLightPanel(0.0, 4.0);
    } else if (type === "corridor_ew") {
      spawnLightPanel(-4.0, 0.0);
      spawnLightPanel(4.0, 0.0);
    } else if (type === "cross_junction" || type === "t_junction" || type === "corner" || type === "dead_end") {
      spawnLightPanel(0.0, 0.0);
    } else {
      // Room chunks: grid of 4 lights
      spawnLightPanel(-3.5, -3.5);
      spawnLightPanel(3.5, -3.5);
      spawnLightPanel(-3.5, 3.5);
      spawnLightPanel(3.5, 3.5);
    }
  }

  buildInteractables(chunk, type, center, chunkId, rand) {
    const isStart = type === "start";
    const isWorkshop = type === "workshop";
    const isPlayroom = type === "playroom";
    const isStorage = type === "storage";
    const isEvent = type === "event";

    // 1. Spawning doors
    const doorMaterial = this.textures.createDoorMaterial();
    const addDynamicDoor = (id, label, localPos, size, isLocked = false, isBlocked = false, reason = "") => {
      const globalPos = [center.x + localPos[0], localPos[1], center.z + localPos[2]];
      const door = new Door({
        id,
        label,
        position: globalPos,
        size,
        openDirection: 1,
        locked: isLocked,
        blocked: isBlocked,
        blockedReason: reason,
      }, doorMaterial);
      door.chunkId = chunkId;
      this.scene.add(door.group);
      chunk.doors.push(door);
      this.collisionWorld.addDoor(door, chunkId);
    };

    // Doors for special rooms
    if (isStart) {
      addDynamicDoor(`${chunkId}_door_n`, "북쪽 복도문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_door_s`, "남쪽 복도문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_door_e`, "동쪽 복도문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_door_w`, "서쪽 복도문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
    } else if (isWorkshop) {
      addDynamicDoor("door-left-workshop", "낡은 작업방", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
    } else if (isPlayroom) {
      addDynamicDoor("door-right-playroom", "붉은 놀이방", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
    } else if (isStorage) {
      addDynamicDoor("door-left-storage", "삐걱대는 보관실", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
    } else if (isEvent) {
      addDynamicDoor("door-upper-mirror", "뒤틀린 거울방", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);

      const paintGeo = this.getBoxGeometry(1.65, 1.95, 0.08);
      const paintMat = this.textures.createHwaPaintMaterial();
      const painting = new THREE.Mesh(paintGeo, paintMat);
      painting.name = "upper-hwa-painting";
      painting.position.set(center.x - 4.78, 1.6, center.z);
      painting.rotation.y = -Math.PI / 2;
      painting.castShadow = true;
      this.scene.add(painting);
      chunk.meshes.push(painting);
    }

    // 2. Cabinets
    const cabinetMaterial = this.textures.createCabinetMaterial();
    const addDynamicCabinet = (id, label, localPos, yaw) => {
      const globalPos = [center.x + localPos[0], localPos[1], center.z + localPos[2]];
      const cabinet = new Cabinet({
        id,
        label,
        position: globalPos,
        yaw,
      }, cabinetMaterial);
      cabinet.chunkId = chunkId;
      this.scene.add(cabinet.group);
      chunk.cabinets.push(cabinet);
    };

    if (isWorkshop) {
      addDynamicCabinet("cabinet-workshop", "작업방 캐비넷", [-5.0, 0.0, -5.0], -Math.PI / 2);
    } else if (isPlayroom) {
      addDynamicCabinet("cabinet-playroom", "놀이방 캐비넷", [5.0, 0.0, -5.0], Math.PI / 2);
    } else if (isStorage) {
      addDynamicCabinet("cabinet-storage", "보관실 캐비넷", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (!isStart && !isEvent && (type.includes("room") || type.includes("storage")) && rand() < 0.4) {
      addDynamicCabinet(`cabinet_${chunk.cx}_${chunk.cz}`, "복도 구석 캐비넷", [-5.0, 0.0, 0.0], -Math.PI / 2);
    }

    // 3. Keys
    const addDynamicKey = (id, label, localPos) => {
      const globalPos = [center.x + localPos[0], localPos[1], center.z + localPos[2]];
      const key = new KeyItem({
        id,
        label,
        position: globalPos,
      }, this.scene);
      key.chunkId = chunkId;
      chunk.keys.push(key);
    };

    if (isWorkshop) {
      addDynamicKey("key-workshop", "녹슨 열쇠", [0.0, 0.0, 0.0]);
    } else if (isPlayroom) {
      addDynamicKey("key-playroom", "놀이방 열쇠", [0.0, 0.0, 0.0]);
    } else if (isStorage) {
      addDynamicKey("key-storage", "도자기 열쇠", [0.0, 0.0, 0.0]);
    } else if (isEvent) {
      addDynamicKey("key-hwacat", "뒤틀린 열쇠", [-0.7, 0.0, -1.0]);
      // The key is hidden initially and will be revealed by the MirrorHwacatEvent
      const keyObj = chunk.keys[chunk.keys.length - 1];
      if (keyObj) {
        keyObj.initiallyVisible = false;
        if (keyObj.group) keyObj.group.visible = false;
      }
    }

    // 4. Final Exit (Toy Box) at Start Room
    if (isStart) {
      const exit = new FinalExit({
        id: "final-offering",
        label: "장난감 상자",
        position: [center.x, 0.0, center.z],
      }, this.scene);
      exit.chunkId = chunkId;
      chunk.finalExit = exit;
    }

    // 5. Spawning custom glb props
    this.buildProps(chunk, type, center, chunkId, rand);
  }

  buildProps(chunk, type, center, chunkId, rand) {
    // If it's a start room, only exit is allowed
    if (type === "start") return;

    const propMaterial = this.propMaterial;

    // Toy Box prop spawn with small chance
    if (rand() < 0.25) {
      const toyGeo = this.getBoxGeometry(0.8, 0.5, 0.8);
      const toy = new THREE.Mesh(toyGeo, propMaterial);
      const localX = (rand() - 0.5) * 8;
      const localZ = (rand() - 0.5) * 8;
      toy.position.set(center.x + localX, 0.25, center.z + localZ);
      toy.castShadow = true;
      toy.name = `${chunkId}_toy_prop`;
      this.scene.add(toy);
      chunk.meshes.push(toy);
      // Small collision box
      this.collisionWorld.addStaticBox(toy.name, toy.position, new THREE.Vector3(0.8, 0.5, 0.8), chunkId);
    }
  }

  buildWaypoints(chunk, type, center) {
    // All waypoints are [worldX, 0, worldZ] tuples.
    // Coverage spans the full walkable interior so monsters always have
    // distant, reachable targets without cutting through walls.
    const cx = center.x;
    const cz = center.z;
    const wp = (lx, lz) => [cx + lx, 0, cz + lz];

    if (type === "start") {
      chunk.waypoints = [wp(0,0), wp(0,-5), wp(0,5), wp(-5,0), wp(5,0)];
    } else if (type === "corridor_ns" || type === "narrow_ns") {
      chunk.waypoints = [wp(0,-6), wp(0,-3), wp(0,0), wp(0,3), wp(0,6)];
    } else if (type === "corridor_ew") {
      chunk.waypoints = [wp(-6,0), wp(-3,0), wp(0,0), wp(3,0), wp(6,0)];
    } else if (type === "cross_junction") {
      chunk.waypoints = [wp(0,0), wp(0,-5), wp(0,5), wp(-5,0), wp(5,0)];
    } else if (type === "t_junction") {
      chunk.waypoints = [wp(0,0), wp(0,-5), wp(0,5), wp(5,0)];
    } else if (type === "corner") {
      // SE corner — open quadrant only
      chunk.waypoints = [wp(3,3), wp(5,3), wp(3,5), wp(5,0), wp(0,5)];
    } else if (type === "dead_end") {
      chunk.waypoints = [wp(0,5), wp(-3,0), wp(3,0), wp(0,0), wp(0,-4)];
    } else if (type === "pillar_room") {
      chunk.waypoints = [
        wp(0,0), wp(0,-6), wp(0,6), wp(-6,0), wp(6,0),
        wp(-6,-6), wp(6,-6), wp(-6,6), wp(6,6),
      ];
    } else if (type === "wide_room" || type === "flicker_room") {
      chunk.waypoints = [
        wp(0,0),
        wp(-5,-5), wp(0,-5), wp(5,-5),
        wp(-5, 0),           wp(5, 0),
        wp(-5, 5), wp(0, 5), wp(5, 5),
      ];
    } else if (type === "workshop") {
      chunk.waypoints = [wp(0,0), wp(0,-5), wp(5,0), wp(-3,-3), wp(3,-3), wp(3,3)];
    } else if (type === "playroom") {
      chunk.waypoints = [wp(0,0), wp(-5,0), wp(0,-5), wp(-3,-3), wp(-3,3), wp(3,-3)];
    } else if (type === "storage") {
      chunk.waypoints = [wp(0,0), wp(5,0), wp(0,5), wp(3,3), wp(-3,3), wp(3,-3)];
    } else if (type === "event") {
      // Partition wall at x=-5 local → stay east side
      chunk.waypoints = [wp(2,0), wp(2,-4), wp(2,4), wp(4,-3), wp(4,3), wp(0,5)];
    } else {
      chunk.waypoints = [wp(0,0), wp(-4,-4), wp(4,-4), wp(-4,4), wp(4,4)];
    }
  }

  destroyChunk(cx, cz) {
    const key = this.getChunkKey(cx, cz);
    const chunk = this.chunksData.get(key);
    if (!chunk) return;

    const tStart = performance.now();

    // 1. Remove meshes from scene
    for (const mesh of chunk.meshes) {
      this.scene.remove(mesh);
    }

    // 2. Remove lights and their PointLights
    for (const light of chunk.lights) {
      if (light.pointLight) {
        this.scene.remove(light.pointLight);
        light.pointLight.dispose();
      }
      this.scene.remove(light.mesh);
      light.mesh.material?.dispose();
    }

    // 3. Remove doors
    for (const door of chunk.doors) {
      this.scene.remove(door.group);
      door.dispose();
    }

    // 4. Remove cabinets
    for (const cabinet of chunk.cabinets) {
      this.scene.remove(cabinet.group);
      cabinet.dispose();
    }

    // 5. Remove keys
    for (const keyObj of chunk.keys) {
      if (keyObj.group) {
        this.scene.remove(keyObj.group);
      }
      keyObj.dispose();
    }

    // 6. Remove final exit
    if (chunk.finalExit && chunk.finalExit.group) {
      this.scene.remove(chunk.finalExit.group);
      chunk.finalExit.dispose();
    }

    // 7. Clear collision world data
    this.collisionWorld.clearChunkData(chunk.chunkId);

    const dtTotal = performance.now() - tStart;
    if (dtTotal > 1.0) {
      console.warn(`[PERF] destroyChunk (${chunk.type} at ${cx},${cz}) took ${dtTotal.toFixed(2)}ms`);
    }

    this.chunksData.delete(key);
  }
}
