import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { Door } from "./Door.js";
import { Cabinet } from "./Cabinet.js";
import { KeyItem } from "./KeyItem.js";
import { FinalExit } from "./FinalExit.js";
import { SafeLight } from "./SafeLight.js";
import { LIGHTING_CONFIG, LOVELY_DOLL_CONFIG } from "../config/gameConfig.js";
import { LovelyDoll } from "../entities/LovelyDoll.js";
import { CharacterLoader } from "../loaders/CharacterLoader.js";

const HORROR_PROP_ASSETS = {
  wrappedBody: {
    kind: "wrapped-body",
    assetUrl: "/assets/props/placeholder-wrapped-body-1f/model.glb",
    size: [0.72, 1.42, 0.5],
    align: "floor",
  },
  watchingMask: {
    kind: "watching-mask",
    assetUrl: "/assets/props/placeholder-watching-mask-1f/model.glb",
    size: [0.72, 0.95, 0.16],
    align: "center",
  },
  hangingBundle: {
    kind: "hanging-bundle",
    assetUrl: "/assets/props/placeholder-hanging-bundle-stair/model.glb",
    size: [0.48, 1.95, 0.4],
    align: "ceiling",
  },
  brokenDollPile: {
    kind: "broken-doll-pile",
    assetUrl: "/assets/props/placeholder-broken-doll-pile-2f/model.glb",
    size: [1.2, 0.34, 0.92],
    align: "floor",
  },
  mannequinA: {
    kind: "silent-mannequin",
    assetUrl: "/assets/props/silent-mannequin-1f/model.glb",
    size: [0.62, 1.72, 0.36],
    align: "floor",
  },
  mannequinB: {
    kind: "silent-mannequin",
    assetUrl: "/assets/props/silent-mannequin-2f/model.glb",
    size: [0.62, 1.72, 0.36],
    align: "floor",
  },
  barredWindow: {
    kind: "barred-window",
    assetUrl: "/assets/props/barred-window/model.glb",
    size: [1.28, 1.05, 0.08],
    align: "center",
  },
  corridorWire: {
    kind: "corridor-wire",
    assetUrl: "/assets/props/corridor-wire/model.glb",
    size: [0.35, 0.2, 2.8],
    align: "ceiling",
  },
  cicadaShells: {
    kind: "cicada-shells",
    assetUrl: "/assets/props/cicada-shells/model.glb",
    size: [0.75, 0.14, 0.54],
    align: "floor",
  },
  barricade: {
    kind: "barricade",
    assetUrl: "/assets/props/barricade/model.glb",
    size: [1.35, 1.0, 0.58],
    align: "floor",
  },
};

const FLOOR_HORROR_PROPS = [
  HORROR_PROP_ASSETS.wrappedBody,
  HORROR_PROP_ASSETS.brokenDollPile,
  HORROR_PROP_ASSETS.mannequinA,
  HORROR_PROP_ASSETS.mannequinB,
  HORROR_PROP_ASSETS.cicadaShells,
  HORROR_PROP_ASSETS.barricade,
];

const WALL_HORROR_PROPS = [
  HORROR_PROP_ASSETS.watchingMask,
  HORROR_PROP_ASSETS.barredWindow,
];

const CEILING_HORROR_PROPS = [
  HORROR_PROP_ASSETS.hangingBundle,
  HORROR_PROP_ASSETS.corridorWire,
];

const SAFE_LIGHT_VARIANTS = ["wall-switch", "floor-lamp", "ceiling-switch", "toy-lamp"];

const SAFE_LIGHT_LABELS = {
  "wall-switch": "벽 스위치",
  "floor-lamp": "낡은 스탠드",
  "ceiling-switch": "형광등 스위치",
  "toy-lamp": "장난감 램프",
};

const ROOM_LIKE_CHUNK_TYPES = new Set([
  "dead_end",
  "pillar_room",
  "wide_room",
  "flicker_room",
  "workshop",
  "playroom",
  "storage",
  "event",
]);

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
  constructor(scene, collisionWorld, textureLibrary, baseSeed = 12345, game = null) {
    this.scene = scene;
    this.collisionWorld = collisionWorld;
    this.textures = textureLibrary;
    this.baseSeed = baseSeed;
    this.game = game;
    this.chunksData = new Map();
    this.geometryCache = new Map();
    this.gltfLoader = new GLTFLoader();
    this.propAssetCache = new Map();
    this.propAssetPromises = new Map();
    this.pendingAssets = [];

    // Load Lovely Doll Asset
    this.characterLoader = new CharacterLoader();
    this.lovelyDollAsset = null;
    const dollLoadTask = this.characterLoader.load(LOVELY_DOLL_CONFIG)
      .then((asset) => {
        this.lovelyDollAsset = asset;
      })
      .catch((err) => {
        console.warn("[BackroomsGenerator] Failed to load Lovely Doll:", err);
      });
    this.pendingAssets.push(dollLoadTask);
    dollLoadTask.finally(() => {
      const idx = this.pendingAssets.indexOf(dollLoadTask);
      if (idx !== -1) {
        this.pendingAssets.splice(idx, 1);
      }
    });

    this.lightPanelGeo = new THREE.BoxGeometry(1.2, 0.05, 0.6);
    this.unitBoxGeo = new THREE.BoxGeometry(1, 1, 1);
    this.lightPanelMat = new THREE.MeshStandardMaterial({
      color: LIGHTING_CONFIG.ceilingPanelOnColor,
      emissive: LIGHTING_CONFIG.ceilingPanelOnColor,
      emissiveIntensity: LIGHTING_CONFIG.ceilingPanelOnEmissiveIntensity,
      roughness: 0.7,
      metalness: 0.0,
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
      safeLights: [],
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

    // Spawning Lovely Dolls in specific chunks (For testing: 1 doll in start chunk (0,0))
    const DOLL_SPAWN_CHUNKS = [
      { cx: 0, cz: 0, id: "lovely_doll_1" },
    ];
    const dollSpawn = DOLL_SPAWN_CHUNKS.find(info => info.cx === cx && info.cz === cz);
    if (dollSpawn) {
      const dollId = dollSpawn.id;
      if (this.game && this.game.spawnedDollIds && !this.game.spawnedDollIds.has(dollId)) {
        let spawnPos = center.clone();
        if (chunk.waypoints && chunk.waypoints.length > 0) {
          const nonCenter = chunk.waypoints.filter(w => Math.hypot(w[0] - center.x, w[2] - center.z) > 1.0);
          const chosenWp = nonCenter.length > 0 ? nonCenter[Math.floor(rand() * nonCenter.length)] : chunk.waypoints[0];
          spawnPos.set(chosenWp[0], chosenWp[1], chosenWp[2]);
        }
        
        const doll = new LovelyDoll(dollId, this.lovelyDollAsset, this.collisionWorld, this.game);
        doll.group.position.copy(spawnPos);
        doll.group.position.y = this.collisionWorld.getGroundY(doll.group.position);
        doll.snapModelToGround();
        
        this.scene.add(doll.group);
        if (this.game.lovelyDolls) {
          this.game.lovelyDolls.push(doll);
        }
        chunk.dollId = dollId;
      }
    }

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
      trimInst.receiveShadow = true;

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
      mesh.receiveShadow = false;
      mesh.castShadow = false;
      this.scene.add(mesh);
      chunk.meshes.push(mesh);

      const isFlickering = type === "flicker_room"
        ? rand() < LIGHTING_CONFIG.flickerRoomLightChance
        : rand() < LIGHTING_CONFIG.corridorFlickerChance;
      const lightData = {
        mesh,
        localPos: new THREE.Vector3(localX, 2.6, localZ),
        pointLight: null,
        isFlickering,
        flickerTimer: rand() * 5,
        baseIntensity: LIGHTING_CONFIG.ceilingLightIntensity,
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
      }, { bodyMaterial: cabinetMaterial });
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
      this.scene.add(key.group);
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

    if (chunk.keys.length > 0 && this.game && this.game.collectedKeyIds) {
      const lastKey = chunk.keys[chunk.keys.length - 1];
      if (this.game.collectedKeyIds.has(lastKey.id)) {
        lastKey.collect();
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
      this.scene.add(exit.group);
      chunk.finalExit = exit;
    }

    // 5. Lights that the player can turn on as a visited-place marker.
    this.buildSafeLights(chunk, type, center, chunkId, rand);

    // 6. Spawning custom glb props
    this.buildProps(chunk, type, center, chunkId, rand);
  }

  buildSafeLights(chunk, type, center, chunkId, rand) {
    if (type === "start") {
      return;
    }

    const isRoomLike = ROOM_LIKE_CHUNK_TYPES.has(type);
    const spawnChance = isRoomLike ? 0.86 : 0.48;
    if (rand() > spawnChance) {
      return;
    }

    const variants = isRoomLike
      ? SAFE_LIGHT_VARIANTS
      : ["wall-switch", "ceiling-switch", "toy-lamp"];
    const variant = this.pickFrom(variants, rand);
    const placement = this.pickSafeLightPlacement(variant, type, center, rand);
    const localId = `safe_${variant.replaceAll("-", "_")}_main`;
    const safeLight = new SafeLight({
      id: `${chunkId}_${localId}`,
      stateKey: `${chunk.cx},${chunk.cz}:${localId}`,
      label: SAFE_LIGHT_LABELS[variant],
      variant,
      position: placement.position,
      yaw: placement.yaw,
    });
    safeLight.chunkId = chunkId;
    this.scene.add(safeLight.group);
    chunk.safeLights.push(safeLight);
  }

  pickSafeLightPlacement(variant, type, center, rand) {
    if (variant === "wall-switch") {
      const wall = this.pickWallPlacement(type, center, rand);
      return {
        position: [wall.x, 1.08, wall.z],
        yaw: wall.yaw,
      };
    }

    if (variant === "ceiling-switch") {
      const ceiling = this.pickCeilingPlacement(type, center, rand);
      return {
        position: [ceiling.x, 2.35, ceiling.z],
        yaw: ceiling.yaw,
      };
    }

    const floor = this.pickFloorPlacement(type, center, rand);
    return {
      position: [floor.x, 0, floor.z],
      yaw: floor.yaw,
    };
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
      toy.receiveShadow = true;
      toy.name = `${chunkId}_toy_prop`;
      this.scene.add(toy);
      chunk.meshes.push(toy);
      // Small collision box
      this.collisionWorld.addStaticBox(toy.name, toy.position, new THREE.Vector3(0.8, 0.5, 0.8), chunkId);
    }

    this.buildHorrorAtmosphereProps(chunk, type, center, chunkId, rand);
  }

  buildHorrorAtmosphereProps(chunk, type, center, chunkId, rand) {
    const isRoomLike = ROOM_LIKE_CHUNK_TYPES.has(type);
    const wallChance = isRoomLike ? 0.72 : 0.46;
    const floorChance = isRoomLike ? 0.82 : 0.28;
    const ceilingChance = type === "flicker_room" ? 0.82 : 0.38;

    if (rand() < wallChance) {
      const definition = this.pickFrom(WALL_HORROR_PROPS, rand);
      const wall = this.pickWallPlacement(type, center, rand);
      this.spawnAssetProp(chunk, {
        ...definition,
        id: `${chunkId}_${definition.kind}_wall`,
        position: [wall.x, 1.65 + rand() * 0.38, wall.z],
        rotation: [0, wall.yaw, 0],
      });
    }

    if (rand() < floorChance) {
      const definition = this.pickFrom(FLOOR_HORROR_PROPS, rand);
      const floor = this.pickFloorPlacement(type, center, rand);
      this.spawnAssetProp(chunk, {
        ...definition,
        id: `${chunkId}_${definition.kind}_floor`,
        position: [floor.x, 0, floor.z],
        rotation: [0, floor.yaw, 0],
      });
    }

    if (rand() < ceilingChance) {
      const definition = this.pickFrom(CEILING_HORROR_PROPS, rand);
      const ceiling = this.pickCeilingPlacement(type, center, rand);
      this.spawnAssetProp(chunk, {
        ...definition,
        id: `${chunkId}_${definition.kind}_ceiling`,
        position: [ceiling.x, 2.76, ceiling.z],
        rotation: [0, ceiling.yaw, 0],
      });
    }

    if (isRoomLike && rand() < 0.38) {
      const secondDefinition = rand() < 0.5
        ? HORROR_PROP_ASSETS.cicadaShells
        : HORROR_PROP_ASSETS.brokenDollPile;
      const scatter = this.pickFloorPlacement(type, center, rand);
      this.spawnAssetProp(chunk, {
        ...secondDefinition,
        id: `${chunkId}_${secondDefinition.kind}_scatter`,
        position: [scatter.x, 0, scatter.z],
        rotation: [0, scatter.yaw, 0],
      });
    }
  }

  pickFrom(entries, rand) {
    return entries[Math.floor(rand() * entries.length) % entries.length];
  }

  pickWallPlacement(type, center, rand) {
    const preferredWalls = this.getPreferredWalls(type);
    const wall = this.pickFrom(preferredWalls, rand);
    const closedWalls = this.getClosedWalls(type);
    const alongClosedWall = closedWalls.has(wall);
    const segmentOffset = alongClosedWall
      ? (rand() - 0.5) * 10.6
      : (rand() < 0.5 ? -5 : 5) + (rand() - 0.5) * 2.1;

    if (wall === "north") {
      return { x: center.x + segmentOffset, z: center.z - 7.52, yaw: Math.PI };
    }
    if (wall === "south") {
      return { x: center.x + segmentOffset, z: center.z + 7.52, yaw: 0 };
    }
    if (wall === "east") {
      return { x: center.x + 7.52, z: center.z + segmentOffset, yaw: -Math.PI / 2 };
    }
    return { x: center.x - 7.52, z: center.z + segmentOffset, yaw: Math.PI / 2 };
  }

  getPreferredWalls(type) {
    const closed = [...this.getClosedWalls(type)];
    if (closed.length > 0) {
      return closed;
    }
    return ["north", "south", "east", "west"];
  }

  getClosedWalls(type) {
    if (type === "corridor_ns" || type === "narrow_ns") return new Set(["east", "west"]);
    if (type === "corridor_ew") return new Set(["north", "south"]);
    if (type === "t_junction") return new Set(["west"]);
    if (type === "corner") return new Set(["north", "west"]);
    if (type === "dead_end") return new Set(["north", "east", "west"]);
    if (type === "workshop") return new Set(["south", "west"]);
    if (type === "playroom") return new Set(["south", "east"]);
    if (type === "storage") return new Set(["north", "west"]);
    if (type === "event") return new Set(["north", "east"]);
    return new Set();
  }

  pickFloorPlacement(type, center, rand) {
    let localX = (rand() - 0.5) * 9.2;
    let localZ = (rand() - 0.5) * 9.2;

    if (type === "corridor_ns") {
      localX = rand() < 0.5 ? -5.65 : 5.65;
      localZ = (rand() - 0.5) * 8.5;
    } else if (type === "corridor_ew") {
      localX = (rand() - 0.5) * 8.5;
      localZ = rand() < 0.5 ? -5.65 : 5.65;
    } else if (type === "narrow_ns") {
      localX = rand() < 0.5 ? -1.1 : 1.1;
      localZ = (rand() - 0.5) * 7.0;
    } else if (type === "corner") {
      localX = 2.6 + rand() * 3.6;
      localZ = 2.6 + rand() * 3.6;
    } else if (type === "event") {
      localX = 1.0 + rand() * 5.4;
      localZ = (rand() - 0.5) * 6.8;
    }

    return {
      x: center.x + localX,
      z: center.z + localZ,
      yaw: rand() * Math.PI * 2,
    };
  }

  pickCeilingPlacement(type, center, rand) {
    if (type === "corridor_ns" || type === "narrow_ns") {
      return {
        x: center.x + (rand() - 0.5) * 1.2,
        z: center.z + (rand() - 0.5) * 8.8,
        yaw: rand() < 0.5 ? 0 : Math.PI,
      };
    }
    if (type === "corridor_ew") {
      return {
        x: center.x + (rand() - 0.5) * 8.8,
        z: center.z + (rand() - 0.5) * 1.2,
        yaw: Math.PI / 2,
      };
    }
    return {
      x: center.x + (rand() - 0.5) * 8.4,
      z: center.z + (rand() - 0.5) * 8.4,
      yaw: rand() * Math.PI * 2,
    };
  }

  spawnAssetProp(chunk, definition) {
    const anchor = new THREE.Group();
    anchor.name = definition.id;
    anchor.userData.horrorProp = true;
    anchor.userData.propKind = definition.kind;
    anchor.userData.assetUrl = definition.assetUrl;
    anchor.position.set(definition.position[0], definition.position[1], definition.position[2]);
    anchor.rotation.set(
      definition.rotation?.[0] ?? 0,
      definition.rotation?.[1] ?? 0,
      definition.rotation?.[2] ?? 0,
    );
    this.scene.add(anchor);
    chunk.meshes.push(anchor);

    // Set weeping angel flags if it's a silent mannequin!
    if (definition.kind === "silent-mannequin") {
      let makeWeepingAngel = false;
      let targetId = definition.id;
      
      if (this.game) {
        if (definition.assetUrl.includes("silent-mannequin-1f") && !this.game.spawnedWeepingAngel1F) {
          makeWeepingAngel = true;
          targetId = "silent-mannequin-1f";
          this.game.spawnedWeepingAngel1F = true;
        } else if (definition.assetUrl.includes("silent-mannequin-2f") && !this.game.spawnedWeepingAngel2F) {
          makeWeepingAngel = true;
          targetId = "silent-mannequin-2f";
          this.game.spawnedWeepingAngel2F = true;
        }
      } else {
        // Fallback for tests or setups without a game instance reference
        makeWeepingAngel = true;
      }
      
      if (makeWeepingAngel) {
        anchor.name = targetId;
        anchor.userData.isWeepingAngel = true;
        anchor.userData.weepingAngelState = {
          id: targetId,
          speed: 1.3, // slow pursuit speed (m/s)
          catchDistance: 1.05,
          radius: 0.38,
          size: definition.size,
          loaded: false,
          path: null,
          pathTimer: 0,
        };
      }
    }

    const loadTask = this.loadPropAsset(definition.assetUrl)
      .then((source) => {
        if (!anchor.parent) {
          return;
        }
        const instance = source.clone(true);
        const content = new THREE.Group();
        content.add(instance);
        content.rotation.set(
          definition.assetRotation?.[0] ?? 0,
          definition.assetRotation?.[1] ?? 0,
          definition.assetRotation?.[2] ?? 0,
        );
        this.prepareHorrorPropInstance(content);
        this.fitPropToTarget(content, definition);
        this.alignPropContent(content, definition.align ?? "floor");
        anchor.add(content);
        anchor.userData.horrorPropLoaded = true;
        if (anchor.userData.isWeepingAngel) {
          anchor.userData.weepingAngelState.loaded = true;
        }
      })
      .catch((error) => {
        console.warn(`[BackroomsGenerator] Failed to load horror prop ${definition.assetUrl}`, error);
      });

    this.pendingAssets.push(loadTask);
    loadTask.finally(() => {
      const idx = this.pendingAssets.indexOf(loadTask);
      if (idx !== -1) {
        this.pendingAssets.splice(idx, 1);
      }
    });
  }

  loadPropAsset(url) {
    if (this.propAssetCache.has(url)) {
      return Promise.resolve(this.propAssetCache.get(url));
    }
    if (this.propAssetPromises.has(url)) {
      return this.propAssetPromises.get(url);
    }

    const promise = new Promise((resolve, reject) => {
      this.gltfLoader.load(
        url,
        (gltf) => {
          this.propAssetCache.set(url, gltf.scene);
          resolve(gltf.scene);
        },
        undefined,
        reject,
      );
    });
    this.propAssetPromises.set(url, promise);
    return promise;
  }

  prepareHorrorPropInstance(root) {
    root.traverse((child) => {
      if (!child.isMesh && !child.isSkinnedMesh) {
        return;
      }
      const geometry = child.geometry;
      if (geometry?.attributes?.position && !geometry.attributes.normal) {
        geometry.computeVertexNormals();
      }
      if (geometry?.attributes?.normal) {
        geometry.attributes.normal.needsUpdate = true;
      }
      child.castShadow = true;
      child.receiveShadow = true;
      child.material = this.createLitPropMaterial(child.material);
    });
  }

  createLitPropMaterial(material) {
    if (Array.isArray(material)) {
      return material.map((entry) => this.createLitPropMaterial(entry));
    }

    const source = material || {};
    if (source.map) {
      source.map.colorSpace = THREE.SRGBColorSpace;
      source.map.needsUpdate = true;
    }

    if (source.isMeshStandardMaterial || source.isMeshPhysicalMaterial) {
      const cloned = source.clone();
      cloned.roughness = cloned.roughness ?? 0.88;
      cloned.metalness = cloned.metalness ?? 0.0;
      if (cloned.emissive) {
        cloned.emissive.setHex(0x000000);
        cloned.emissiveIntensity = 0;
      }
      return cloned;
    }

    return new THREE.MeshStandardMaterial({
      map: source.map ?? null,
      color: source.color?.clone?.() ?? new THREE.Color(0xffffff),
      transparent: source.transparent ?? false,
      opacity: source.opacity ?? 1,
      alphaTest: source.alphaTest ?? 0.02,
      side: source.side ?? THREE.FrontSide,
      roughness: 0.88,
      metalness: 0.0,
    });
  }

  fitPropToTarget(content, definition) {
    content.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(content);
    const size = box.getSize(new THREE.Vector3());
    const target = new THREE.Vector3(...definition.size);
    if (size.x <= 0.0001 || size.y <= 0.0001 || size.z <= 0.0001) {
      return;
    }

    const uniformScale = Math.min(
      target.x / size.x,
      target.y / size.y,
      target.z / size.z,
    ) * (definition.assetScale ?? 1);
    content.scale.multiplyScalar(uniformScale);
  }

  alignPropContent(content, alignMode) {
    content.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(content);
    const center = box.getCenter(new THREE.Vector3());
    content.position.x -= center.x;
    content.position.z -= center.z;

    if (alignMode === "center") {
      content.position.y -= center.y;
    } else if (alignMode === "ceiling") {
      content.position.y -= box.max.y;
    } else {
      content.position.y -= box.min.y;
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

    // 1. Remove meshes from scene and dispose of custom horror prop materials
    for (const mesh of chunk.meshes) {
      this.scene.remove(mesh);
      if (mesh.userData && mesh.userData.isWeepingAngel) {
        if (mesh.name === "silent-mannequin-1f" && this.game) {
          this.game.spawnedWeepingAngel1F = false;
        } else if (mesh.name === "silent-mannequin-2f" && this.game) {
          this.game.spawnedWeepingAngel2F = false;
        }
      }
      if (mesh.userData && mesh.userData.horrorProp) {
        mesh.traverse((child) => {
          if (child.isMesh || child.isSkinnedMesh) {
            // Geometries are cached/shared with the GLTF cache source, do NOT dispose them!
            // But materials are cloned/created per instance, so we must dispose them.
            if (child.material) {
              if (Array.isArray(child.material)) {
                child.material.forEach((mat) => {
                  if (mat && typeof mat.dispose === "function") {
                    mat.dispose();
                  }
                });
              } else if (typeof child.material.dispose === "function") {
                child.material.dispose();
              }
            }
          }
        });
      }
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

    // 6. Remove player-activated safe lights
    for (const safeLight of chunk.safeLights || []) {
      this.scene.remove(safeLight.group);
      safeLight.dispose();
    }

    // 7. Remove final exit
    if (chunk.finalExit && chunk.finalExit.group) {
      this.scene.remove(chunk.finalExit.group);
      chunk.finalExit.dispose();
    }

    // Remove unactivated Lovely Dolls in chunk
    if (chunk.dollId && this.game && this.game.lovelyDolls) {
      const dollId = chunk.dollId;
      const doll = this.game.lovelyDolls.find(d => d.id === dollId);
      if (doll && !doll.isActivated) {
        this.scene.remove(doll.group);
        doll.dispose();
        this.game.lovelyDolls = this.game.lovelyDolls.filter(d => d !== doll);
      }
    }

    // 8. Clear collision world data
    this.collisionWorld.clearChunkData(chunk.chunkId);

    const dtTotal = performance.now() - tStart;
    if (dtTotal > 1.0) {
      console.warn(`[PERF] destroyChunk (${chunk.type} at ${cx},${cz}) took ${dtTotal.toFixed(2)}ms`);
    }

    this.chunksData.delete(key);
  }
}
