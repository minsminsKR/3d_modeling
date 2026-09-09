import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { Door } from "./Door.js";
import { Cabinet } from "./Cabinet.js";
import { KeyItem } from "./KeyItem.js";
import { FinalExit } from "./FinalExit.js";
import { SafeLight } from "./SafeLight.js";
import { LoreNote } from "./LoreNote.js";
import { LIGHTING_CONFIG, LOVELY_DOLL_CONFIG } from "../config/gameConfig.js";
import { LovelyDoll } from "../entities/LovelyDoll.js";
import { CharacterLoader } from "../loaders/CharacterLoader.js";
import {
  CORE_EDGES,
  CORE_RADIUS,
  MANSION_EDGES,
  PLAYABLE_RADIUS,
  SCHOOL_RING_EDGES,
  getGraphOpenings,
  getRingHallType,
  isPlayableCell,
} from "./schoolMaze.js";

export {
  CORE_EDGES,
  CORE_RADIUS,
  MANSION_EDGES,
  PLAYABLE_RADIUS,
  SCHOOL_RING_EDGES,
  getRingHallType,
};

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
  "tatami_room",
  "wide_room",
  "flicker_room",
  "omen_room",
  "static_room",
  "workshop",
  "playroom",
  "storage",
  "event",
  "archive",
  "classroom",
  "nurse_office",
  "music_room",
  "faculty_office",
  "science_lab",
  "stairs_2f",
  "stairs_b1",
]);

function isClassroomType(type) {
  return type === "classroom" || type === "nurse_office" || type === "music_room"
    || type === "faculty_office" || type === "science_lab";
}

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

    this.lightPanelGeo = new THREE.BoxGeometry(0.14, 0.24, 0.14);
    this.unitBoxGeo = new THREE.BoxGeometry(1, 1, 1);
    this.lightPanelMat = new THREE.MeshStandardMaterial({
      color: 0xffa855,
      emissive: 0xff6611,
      emissiveIntensity: 0.9,
      roughness: 0.45,
      metalness: 0.05,
    });
    this.trimMaterial = new THREE.MeshStandardMaterial({
      color: 0x161510,
      roughness: 0.8,
    });
    this.propMaterial = new THREE.MeshStandardMaterial({
      color: 0x4a3221,
      roughness: 0.7,
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
    // Fixed Narrative Map Grid for Chapter 1
    const FIXED_MAP_LAYOUT = {
      "0,0": "start",
      "0,-2": "wide_room", // Final Exit Lock Room
      "0,-1": "corridor_ns",

      "1,0": "corridor_ew",   // Cyclopse Intro Corner
      "2,0": "t_junction",
      "2,-1": "corridor_ns",
      "2,-2": "storage",       // Key 1 Storage Room

      "1,1": "corridor_ew",
      "2,1": "corridor_ns",
      "2,2": "workshop",       // Key 3 Sleeping Baby Room

      "0,1": "cross_junction", // Uncat Blackout Reveal
      "-1,1": "corridor_ew",
      "-2,1": "corridor_ns",
      "-2,2": "playroom",      // Key 2 LovelyDoll Room

      "-1,0": "corridor_ew",   // Weeping Angel Mannequin Intro
      "-2,0": "t_junction",
      "-2,-1": "corridor_ns",
      "-2,-2": "archive",      // Japanese Antique Archive (고서 보관소)

      "1,-1": "flicker_room",
      "1,2": "stairs_b1",
      "-1,-1": "stairs_2f",
      "-1,2": "tatami_room",
      "0,2": "corridor_ns",
      "-1,-2": "omen_room",
      "1,-2": "static_room",
      "5,-1": "nurse_office",
      "8,2": "music_room",
      "6,1": "faculty_office",
      "6,-2": "science_lab",
    };

    const key = `${cx},${cz}`;
    if (FIXED_MAP_LAYOUT[key]) {
      return FIXED_MAP_LAYOUT[key];
    }
    if (!this.isPlayableChunk(cx, cz)) {
      return "void";
    }
    return getRingHallType(cx, cz);
  }

  getRingHallType(cx, cz) {
    return getRingHallType(cx, cz);
  }

  isPlayableChunk(cx, cz) {
    return isPlayableCell(cx, cz);
  }

  getOpenings(cx, cz) {
    return getGraphOpenings(cx, cz);
  }

    // Long school-hall tiles: EW through-halls off the start meridian, and
    // NS through-halls off the start parallel. Start (0,0), Uncat south
    // (0,1), and the weeping-angel west tile (-1,0) stay plus spines.
  getHallChicanes(cx, cz) {
    const openings = this.getOpenings(cx, cz);
    const skipUncatSouth = cx === 0 && cz === 1;
    const skipAngelWest = cx === -1 && cz === 0;
    return {
      openings,
      ew: Boolean(openings.E && openings.W && cx !== 0 && !skipAngelWest),
      ns: Boolean(openings.N && openings.S && cz !== 0 && !skipUncatSouth),
    };
  }

  isHallChunk(cx, cz) {
    const type = this.getChunkType(cx, cz);
    return type === "corridor_ns" || type === "narrow_ns" || type === "corridor_ew"
      || type === "t_junction" || type === "cross_junction" || type === "start" || type === "dead_end";
  }

  isMazeHall(cx, cz) {
    if (!this.isPlayableChunk(cx, cz) || !this.isHallChunk(cx, cz)) return false;
    const chicanes = this.getHallChicanes(cx, cz);
    return Boolean(chicanes.ew || chicanes.ns);
  }

  generateExteriorHull(cx, cz) {
    const key = this.getChunkKey(cx, cz);
    const center = new THREE.Vector3(cx * 16, 0, cz * 16);
    const chunkId = `chunk_${cx}_${cz}`;
    const chunk = {
      cx,
      cz,
      cy: 0,
      floorY: 0,
      type: "void",
      center,
      chunkId,
      meshes: [],
      lights: [],
      doors: [],
      keys: [],
      cabinets: [],
      safeLights: [],
      loreNotes: [],
      finalExit: null,
      waypoints: [],
    };
    this.collisionWorld.addVoidArea({
      id: `${chunkId}_void`,
      minX: center.x - 8,
      maxX: center.x + 8,
      minZ: center.z - 8,
      maxZ: center.z + 8,
      y: 0,
    }, chunkId);
    this.chunksData.set(key, chunk);
    return chunk;
  }

  getChunkElevation(cx, cz) {
    // Seamless Shadow Corridor Labyrinth: all chunks on unified ground plane (Y = 0.0)
    return 0;
  }

  generateChunk(cx, cz) {
    const key = this.getChunkKey(cx, cz);
    if (this.chunksData.has(key)) {
      return this.chunksData.get(key);
    }

    const tStart = performance.now();

    if (!this.isPlayableChunk(cx, cz)) {
      return this.generateExteriorHull(cx, cz);
    }

    const type = this.getChunkType(cx, cz);
    const cy = this.getChunkElevation(cx, cz);
    const floorY = cy * 5.0;
    const center = new THREE.Vector3(cx * 16, floorY, cz * 16);
    const chunkId = `chunk_${cx}_${cz}`;
    const seed = getChunkSeed(this.baseSeed, cx, cz);
    const rand = createRandom(seed);

    const chunk = {
      cx,
      cz,
      cy,
      floorY,
      type,
      center,
      chunkId,
      meshes: [],
      lights: [],
      doors: [],
      keys: [],
      cabinets: [],
      safeLights: [],
      loreNotes: [],
      finalExit: null,
      waypoints: [],
    };

    // 1. Create floor and ceiling
    const tFloor0 = performance.now();
    const floorMat = this.textures.createFloorMaterial(16, 16);
    const ceilingMat = this.textures.createCeilingMaterial(16, 16);

    if (type === "stairs_b1") {
      // For B1 stairwell: leave opening for descending stairs (x in [-1.2, 1.2], z in [-2.5, 8])
      const vestGeo = this.getPlaneGeometry(16, 5.5);
      const vestMesh = new THREE.Mesh(vestGeo, floorMat);
      vestMesh.rotation.x = -Math.PI / 2;
      vestMesh.position.set(center.x, floorY, center.z - 5.25);
      vestMesh.receiveShadow = true;
      vestMesh.name = `${chunkId}_floor_vest`;
      this.scene.add(vestMesh);
      chunk.meshes.push(vestMesh);

      const leftGeo = this.getPlaneGeometry(6.8, 10.5);
      const leftMesh = new THREE.Mesh(leftGeo, floorMat);
      leftMesh.rotation.x = -Math.PI / 2;
      leftMesh.position.set(center.x - 4.6, floorY, center.z + 2.75);
      leftMesh.receiveShadow = true;
      leftMesh.name = `${chunkId}_floor_l`;
      this.scene.add(leftMesh);
      chunk.meshes.push(leftMesh);

      const rightGeo = this.getPlaneGeometry(6.8, 10.5);
      const rightMesh = new THREE.Mesh(rightGeo, floorMat);
      rightMesh.rotation.x = -Math.PI / 2;
      rightMesh.position.set(center.x + 4.6, floorY, center.z + 2.75);
      rightMesh.receiveShadow = true;
      rightMesh.name = `${chunkId}_floor_r`;
      this.scene.add(rightMesh);
      chunk.meshes.push(rightMesh);

      const ceilingGeo = this.getPlaneGeometry(16, 16);
      const ceiling = new THREE.Mesh(ceilingGeo, ceilingMat);
      ceiling.rotation.x = Math.PI / 2;
      ceiling.position.set(center.x, floorY + 2.8, center.z);
      ceiling.receiveShadow = true;
      ceiling.name = `${chunkId}_ceiling`;
      this.scene.add(ceiling);
      chunk.meshes.push(ceiling);
    } else if (type === "stairs_2f") {
      // For 2F stairwell: floor is solid 1F plane; ceiling leaves opening for ascending stairs (x in [-1.2, 1.2], z in [-8, 2.5])
      const floorGeo = this.getPlaneGeometry(16, 16);
      const floor = new THREE.Mesh(floorGeo, floorMat);
      floor.rotation.x = -Math.PI / 2;
      floor.position.set(center.x, floorY, center.z);
      floor.receiveShadow = true;
      floor.name = `${chunkId}_floor`;
      this.scene.add(floor);
      chunk.meshes.push(floor);

      const vestCeilGeo = this.getPlaneGeometry(16, 5.5);
      const vestCeilMesh = new THREE.Mesh(vestCeilGeo, ceilingMat);
      vestCeilMesh.rotation.x = Math.PI / 2;
      vestCeilMesh.position.set(center.x, floorY + 2.8, center.z + 5.25);
      vestCeilMesh.receiveShadow = true;
      vestCeilMesh.name = `${chunkId}_ceiling_vest`;
      this.scene.add(vestCeilMesh);
      chunk.meshes.push(vestCeilMesh);

      const leftCeilGeo = this.getPlaneGeometry(6.8, 10.5);
      const leftCeilMesh = new THREE.Mesh(leftCeilGeo, ceilingMat);
      leftCeilMesh.rotation.x = Math.PI / 2;
      leftCeilMesh.position.set(center.x - 4.6, floorY + 2.8, center.z - 2.75);
      leftCeilMesh.receiveShadow = true;
      leftCeilMesh.name = `${chunkId}_ceiling_l`;
      this.scene.add(leftCeilMesh);
      chunk.meshes.push(leftCeilMesh);

      const rightCeilGeo = this.getPlaneGeometry(6.8, 10.5);
      const rightCeilMesh = new THREE.Mesh(rightCeilGeo, ceilingMat);
      rightCeilMesh.rotation.x = Math.PI / 2;
      rightCeilMesh.position.set(center.x + 4.6, floorY + 2.8, center.z - 2.75);
      rightCeilMesh.receiveShadow = true;
      rightCeilMesh.name = `${chunkId}_ceiling_r`;
      this.scene.add(rightCeilMesh);
      chunk.meshes.push(rightCeilMesh);
    } else {
      const floorGeo = this.getPlaneGeometry(16, 16);
      const floor = new THREE.Mesh(floorGeo, floorMat);
      floor.rotation.x = -Math.PI / 2;
      floor.position.set(center.x, floorY, center.z);
      floor.receiveShadow = true;
      floor.name = `${chunkId}_floor`;
      this.scene.add(floor);
      chunk.meshes.push(floor);

      const ceilingGeo = this.getPlaneGeometry(16, 16);
      const ceiling = new THREE.Mesh(ceilingGeo, ceilingMat);
      ceiling.rotation.x = Math.PI / 2;
      ceiling.position.set(center.x, floorY + 2.8, center.z);
      ceiling.receiveShadow = true;
      ceiling.name = `${chunkId}_ceiling`;
      this.scene.add(ceiling);
      chunk.meshes.push(ceiling);
    }

    // Register floor area in CollisionWorld
    this.collisionWorld.addFloorArea({
      id: `${chunkId}_walkable`,
      floor: 1,
      type: "walkable",
      y: floorY,
      minX: center.x - 8,
      maxX: center.x + 8,
      minZ: center.z - 8,
      maxZ: center.z + 8,
    }, chunkId);

    // 1F stays a dry school. Standing water belongs to the B1 cellar map.

    const dtFloor = performance.now() - tFloor0;

    // 2. Build Walls based on template
    const tWalls0 = performance.now();
    this.buildTemplateWalls(chunk, type, center, chunkId, rand, floorY);
    this.dressHallLabyrinth(chunk, type, center, chunkId, floorY);
    const dtWalls = performance.now() - tWalls0;

    // 2b. Build Staircases if chunk is a stairwell
    if (type === "stairs_2f" || type === "stairs_b1") {
      this.buildStaircases(chunk, type, center, chunkId, floorY);
    }


    // 3. Add light panels (emissive mesh)
    const tLights0 = performance.now();
    this.buildCeilingLights(chunk, type, center, chunkId, rand, floorY);
    const dtLights = performance.now() - tLights0;

    // 4. Place special interactables (doors, cabinets, keys, final exit)
    const tInteract0 = performance.now();
    this.buildInteractables(chunk, type, center, chunkId, rand, floorY);
    const dtInteract = performance.now() - tInteract0;

    // 5. Build waypoints for AI patrolling
    const tWaypoints0 = performance.now();
    this.buildWaypoints(chunk, type, center, floorY);
    const dtWaypoints = performance.now() - tWaypoints0;

    // Spawning Lovely Dolls in specific chunks (Start chunk 0,0 and Playroom -2,2)
    const DOLL_SPAWN_CHUNKS = [
      { cx: 0, cz: 0, id: "lovely_doll_1" },
      { cx: -2, cz: 2, id: "lovely_doll_playroom" },
    ];
    const dollSpawn = DOLL_SPAWN_CHUNKS.find(info => info.cx === cx && info.cz === cz);
    if (dollSpawn) {
      const dollId = dollSpawn.id;
      if (this.game && this.game.spawnedDollIds && !this.game.spawnedDollIds.has(dollId)) {
        let spawnPos = center.clone();
        if (dollId !== "lovely_doll_playroom" && chunk.waypoints && chunk.waypoints.length > 0) {
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

    // Deterministic Weeping Angel Mannequin in West corridor chunk (-1, 0)
    if (cx === -1 && cz === 0) {
      if (!this.game || !this.game.spawnedWeepingAngel1F) {
        const mannequinPos = new THREE.Vector3(-22.0, floorY, 0.0);
        this.spawnAssetProp(chunk, {
          ...HORROR_PROP_ASSETS.mannequinA,
          id: `${chunkId}_silent_mannequin_intro`,
          position: [mannequinPos.x, mannequinPos.y, mannequinPos.z],
          rotation: [0, -Math.PI / 2, 0], // Initially facing west (showing back to player approaching from east)
        });
        
        // Spotlight right above the mannequin so it stands clearly under the light
        const spotLight = new THREE.PointLight(0xffdfaa, 28.0, 16.0, 1.0);
        spotLight.position.set(mannequinPos.x, floorY + 2.5, mannequinPos.z);
        this.scene.add(spotLight);
        chunk.meshes.push(spotLight);

        // Glowing ceiling lamp fixture directly above the mannequin
        const fixtureMesh = new THREE.Mesh(this.getBoxGeometry(0.35, 0.12, 0.35), this.trimMaterial);
        fixtureMesh.position.set(mannequinPos.x, floorY + 2.74, mannequinPos.z);
        this.scene.add(fixtureMesh);
        chunk.meshes.push(fixtureMesh);

        const bulbMat = new THREE.MeshStandardMaterial({
          color: 0xffffff,
          emissive: 0xffaa44,
          emissiveIntensity: 3.5,
          roughness: 0.2,
        });
        const bulbMesh = new THREE.Mesh(new THREE.SphereGeometry(0.14, 12, 12), bulbMat);
        bulbMesh.position.set(mannequinPos.x, floorY + 2.6, mannequinPos.z);
        this.scene.add(bulbMesh);
        chunk.meshes.push(bulbMesh);
      }
    }

    const dtTotal = performance.now() - tStart;
    if (dtTotal > 1.0) {
      console.warn(`[PERF] generateChunk (${type} at ${cx},${cz}) took ${dtTotal.toFixed(2)}ms: floor=${dtFloor.toFixed(2)}ms, walls=${dtWalls.toFixed(2)}ms, lights=${dtLights.toFixed(2)}ms, interactables=${dtInteract.toFixed(2)}ms, waypoints=${dtWaypoints.toFixed(2)}ms`);
    }

    this.chunksData.set(key, chunk);
    return chunk;
  }

  buildStaircases(chunk, type, center, chunkId, floorY = 0) {
    const stairMat = this.textures.createWallMaterial();
    const ceilingMat = this.textures.createCeilingMaterial(16, 16);
    const STEP_COUNT = 16;
    const rampHalfWidth = 1.2; // 2.4m wide staircase strictly matching 2.4m corridor width

    if (type === "stairs_2f") {
      // 1F → 2F Staircase (rising from Y = 0.0 to Y = 5.0 towards North, inside chunk (-1, -1))
      // Entrance is from South (z = 7.8)
      // Vestibule at Y = 0.0 from z = 7.8 to z = 2.5
      // Staircase runs from z = 2.5 (Y = 0.0) to z = -4.5 (Y = 5.0)
      // 2F Landing from z = -4.5 to z = -7.8 at Y = 5.0
      const f2StartX = center.x;
      const f2StartZ = center.z + 2.5;
      const f2EndZ = center.z - 4.5;
      const stepDepth = Math.abs(f2EndZ - f2StartZ) / STEP_COUNT;
      const stepHeight = 5.0 / STEP_COUNT;

      // Visual stair steps
      for (let i = 0; i < STEP_COUNT; i++) {
        const progress = i / STEP_COUNT;
        const stepGeo = this.getBoxGeometry(rampHalfWidth * 2, stepHeight + 0.06, stepDepth);
        const stepMesh = new THREE.Mesh(stepGeo, stairMat);
        stepMesh.position.set(
          f2StartX,
          floorY + progress * 5.0 + stepHeight / 2,
          f2StartZ - progress * Math.abs(f2EndZ - f2StartZ) - stepDepth / 2
        );
        stepMesh.castShadow = true;
        stepMesh.receiveShadow = true;
        stepMesh.name = `${chunkId}_stair_2f_step_${i}`;
        this.scene.add(stepMesh);
        chunk.meshes.push(stepMesh);
      }

      // 2F Landing platform floor (Y = 5.0, from z = -20.5 to -23.8)
      const landingMinZ = center.z - 7.8;
      const landingMaxZ = f2EndZ;
      const landingLength = landingMaxZ - landingMinZ;
      const landingGeo = this.getBoxGeometry(rampHalfWidth * 2, 0.2, landingLength);
      const landingMesh = new THREE.Mesh(landingGeo, stairMat);
      landingMesh.position.set(
        f2StartX,
        floorY + 5.0 - 0.1,
        (landingMinZ + landingMaxZ) / 2
      );
      landingMesh.receiveShadow = true;
      landingMesh.name = `${chunkId}_stair_2f_landing_mesh`;
      this.scene.add(landingMesh);
      chunk.meshes.push(landingMesh);

      // ====================================================
      // 2F Mirror & Painting Gallery (액자 사당 갤러리)
      // Expanded hall: west shrine room + outer gallery, not a closet.
      // Spans x from -38.0 to -14.8, z from -36.0 to -8.5 at Y = 5.0
      // ====================================================
      const galMinX = -38.0;
      const galMaxX = -14.8;
      const galMinZ = -36.0;
      const galMaxZ = -8.5;
      const galWestWingWidth = (f2StartX - rampHalfWidth) - galMinX; // 6.6m
      const galWestWingCenterX = (galMinX + (f2StartX - rampHalfWidth)) / 2; // -20.5
      const galLength = galMaxZ - galMinZ; // 15.3m
      const galCenterZ = (galMinZ + galMaxZ) / 2; // -16.15

      // Solid 2F Gallery Floor (Y = 5.0)
      const galFloorGeo = this.getPlaneGeometry(galWestWingWidth, galLength);
      const galFloorMesh = new THREE.Mesh(galFloorGeo, this.textures.createFloorMaterial(galWestWingWidth, galLength));
      galFloorMesh.rotation.x = -Math.PI / 2;
      galFloorMesh.position.set(galWestWingCenterX, floorY + 5.0, galCenterZ);
      galFloorMesh.receiveShadow = true;
      galFloorMesh.name = `${chunkId}_gallery_2f_floor`;
      galFloorMesh.material.color.setHex(0x4a181c);
      galFloorMesh.material.emissive = new THREE.Color(0x28060a);
      galFloorMesh.material.emissiveIntensity = 0.14;
      galFloorMesh.material.roughness = 0.38;
      this.scene.add(galFloorMesh);
      chunk.meshes.push(galFloorMesh);

      // Solid 2F Gallery Ceiling (Y = 7.8, height 2.8m)
      const galCeilGeo = this.getPlaneGeometry(galWestWingWidth, galLength);
      const galCeilMesh = new THREE.Mesh(galCeilGeo, ceilingMat);
      galCeilMesh.rotation.x = Math.PI / 2;
      galCeilMesh.position.set(galWestWingCenterX, floorY + 7.8, galCenterZ);
      galCeilMesh.receiveShadow = true;
      galCeilMesh.name = `${chunkId}_gallery_2f_ceiling`;
      this.scene.add(galCeilMesh);
      chunk.meshes.push(galCeilMesh);

      // North / west walls keep the painting wall solid at z≈-22 and open maze gates.
      this.placeGappedWall(chunk, chunkId, "gallery_2f_wall_n", {
        axis: "x",
        pos: galMinZ - 0.15,
        min: galMinX,
        max: galMaxX,
        wallY: floorY + 6.4,
        height: 2.8,
        thickness: 0.4,
        material: stairMat,
        gaps: [{ center: -32.5, width: 2.7 }, { center: -22.5, width: 2.7 }],
      });
      this.placeGappedWall(chunk, chunkId, "gallery_2f_wall_w", {
        axis: "z",
        pos: galMinX - 0.15,
        min: galMinZ,
        max: galMaxZ,
        wallY: floorY + 6.4,
        height: 2.8,
        thickness: 0.4,
        material: stairMat,
        gaps: [{ center: -12.0, width: 2.7 }, { center: -32.0, width: 2.7 }],
      });

      // Inner shrine partition at x = -27.5 (doorway toward the painting at z ≈ -22)
      const galInnerX = -27.5;
      const galDoorMinZ = -23.2;
      const galDoorMaxZ = -20.8;
      const galInnerJambNLen = galDoorMinZ - galMinZ;
      const galInnerJambNCenterZ = (galMinZ + galDoorMinZ) / 2;
      const galInnerJambNGeo = this.getBoxGeometry(0.3, 2.8, galInnerJambNLen);
      const galInnerJambN = new THREE.Mesh(galInnerJambNGeo, stairMat);
      galInnerJambN.position.set(galInnerX, floorY + 6.4, galInnerJambNCenterZ);
      galInnerJambN.castShadow = true;
      galInnerJambN.name = `${chunkId}_gallery_2f_inner_jamb_n`;
      this.scene.add(galInnerJambN);
      chunk.meshes.push(galInnerJambN);
      this.collisionWorld.addStaticBox(galInnerJambN.name, galInnerJambN.position, new THREE.Vector3(0.3, 2.8, galInnerJambNLen), chunkId);

      const galInnerJambSLen = galMaxZ - galDoorMaxZ;
      const galInnerJambSCenterZ = (galDoorMaxZ + galMaxZ) / 2;
      const galInnerJambSGeo = this.getBoxGeometry(0.3, 2.8, galInnerJambSLen);
      const galInnerJambS = new THREE.Mesh(galInnerJambSGeo, stairMat);
      galInnerJambS.position.set(galInnerX, floorY + 6.4, galInnerJambSCenterZ);
      galInnerJambS.castShadow = true;
      galInnerJambS.name = `${chunkId}_gallery_2f_inner_jamb_s`;
      this.scene.add(galInnerJambS);
      chunk.meshes.push(galInnerJambS);
      this.collisionWorld.addStaticBox(galInnerJambS.name, galInnerJambS.position, new THREE.Vector3(0.3, 2.8, galInnerJambSLen), chunkId);

      const galInnerLintelGeo = this.getBoxGeometry(0.3, 0.6, 2.4);
      const galInnerLintel = new THREE.Mesh(galInnerLintelGeo, stairMat);
      galInnerLintel.position.set(galInnerX, floorY + 7.5, (galDoorMinZ + galDoorMaxZ) / 2);
      galInnerLintel.name = `${chunkId}_gallery_2f_inner_lintel`;
      this.scene.add(galInnerLintel);
      chunk.meshes.push(galInnerLintel);

      // South wall opens into the blood-soaked south labyrinth.
      this.placeGappedWall(chunk, chunkId, "gallery_2f_wall_s", {
        axis: "x",
        pos: galMaxZ + 0.15,
        min: galMinX,
        max: f2StartX - rampHalfWidth,
        wallY: floorY + 6.4,
        height: 2.8,
        thickness: 0.4,
        material: stairMat,
        gaps: [{ center: -22.5, width: 2.7 }, { center: -32.5, width: 2.7 }],
      });

      // Dividing Wall along x = -17.2 between Gallery and Stairs/Vestibule:
      // Doorway: 2.4m opening connecting landing to gallery (z from -23.0 to -20.6)
      // North doorway jamb (z from -23.8 to -23.0, length 0.8m)
      const jambNGeo = this.getBoxGeometry(0.3, 2.8, 0.8);
      const jambNMesh = new THREE.Mesh(jambNGeo, stairMat);
      jambNMesh.position.set(f2StartX - (rampHalfWidth + 0.15), floorY + 6.4, -23.4);
      jambNMesh.castShadow = true;
      jambNMesh.name = `${chunkId}_stair_2f_wall_l_jamb_n`;
      this.scene.add(jambNMesh);
      chunk.meshes.push(jambNMesh);
      this.collisionWorld.addStaticBox(jambNMesh.name, jambNMesh.position, new THREE.Vector3(0.3, 2.8, 0.8), chunkId);

      // Doorway header lintel over 2.4m doorway opening (height 0.6, from Y = 7.2 to 7.8)
      const lintelGeo = this.getBoxGeometry(0.3, 0.6, 2.4);
      const lintelMesh = new THREE.Mesh(lintelGeo, stairMat);
      lintelMesh.position.set(f2StartX - (rampHalfWidth + 0.15), floorY + 7.5, -21.8);
      lintelMesh.name = `${chunkId}_stair_2f_door_lintel`;
      this.scene.add(lintelMesh);
      chunk.meshes.push(lintelMesh);

      // South dividing wall (z from -20.6 to -8.5, length 12.1m)
      const divLength = (-8.5) - (-20.6);
      const divCenterZ = (-20.6 + -8.5) / 2;
      const divWallGeo = this.getBoxGeometry(0.3, 2.8, divLength);
      const divWallMesh = new THREE.Mesh(divWallGeo, stairMat);
      divWallMesh.position.set(f2StartX - (rampHalfWidth + 0.15), floorY + 6.4, divCenterZ);
      divWallMesh.castShadow = true;
      divWallMesh.name = `${chunkId}_gallery_2f_wall_e_div`;
      this.scene.add(divWallMesh);
      chunk.meshes.push(divWallMesh);
      this.collisionWorld.addStaticBox(divWallMesh.name, divWallMesh.position, new THREE.Vector3(0.3, 2.8, divLength), chunkId);

      // Lower wall below 2F floor along stairs (from Y = 0 to 4.6, along stair rise from f2StartZ to f2EndZ only)
      const stairRiseZ = Math.abs(f2StartZ - f2EndZ); // from -13.5 to -20.6 (7.1m)
      const stairRiseCenterZ = (f2StartZ + f2EndZ) / 2; // -17.05
      const westLowerGeo = this.getBoxGeometry(0.3, 4.6, stairRiseZ);
      const westLowerMesh = new THREE.Mesh(westLowerGeo, stairMat);
      westLowerMesh.position.set(f2StartX - (rampHalfWidth + 0.15), floorY + 2.3, stairRiseCenterZ);
      westLowerMesh.castShadow = true;
      westLowerMesh.name = `${chunkId}_stair_2f_wall_l_lower`;
      this.scene.add(westLowerMesh);
      chunk.meshes.push(westLowerMesh);
      this.collisionWorld.addStaticBox(westLowerMesh.name, westLowerMesh.position, new THREE.Vector3(0.3, 4.6, stairRiseZ), chunkId);

      // East enclosing wall for 2F stairs: from Y = 0 up to Y = 7.8 (solid)
      const stairTotalZ = Math.abs((center.z - 7.8) - f2StartZ);
      const stairCenterZ = ((center.z - 7.8) + f2StartZ) / 2;
      const eastWallGeo = this.getBoxGeometry(0.3, 7.8, stairTotalZ);
      const eastWallMesh = new THREE.Mesh(eastWallGeo, stairMat);
      eastWallMesh.position.set(f2StartX + (rampHalfWidth + 0.15), floorY + 3.9, stairCenterZ);
      eastWallMesh.castShadow = true;
      eastWallMesh.name = `${chunkId}_stair_2f_wall_r`;
      this.scene.add(eastWallMesh);
      chunk.meshes.push(eastWallMesh);
      this.collisionWorld.addStaticBox(eastWallMesh.name, eastWallMesh.position, new THREE.Vector3(0.3, 7.8, stairTotalZ), chunkId);

      // 1F Vestibule side walls (from z = 2.5 to z = 7.8, length 5.3)
      const vestLength = (center.z + 7.8) - f2StartZ;
      const vestCenterZ = (f2StartZ + (center.z + 7.8)) / 2;
      for (const side of [-1, 1]) {
        const vestWallGeo = this.getBoxGeometry(0.3, 2.8, vestLength);
        const vestWallMesh = new THREE.Mesh(vestWallGeo, stairMat);
        vestWallMesh.position.set(
          f2StartX + side * (rampHalfWidth + 0.15),
          floorY + 1.4,
          vestCenterZ
        );
        vestWallMesh.castShadow = true;
        vestWallMesh.name = `${chunkId}_stair_2f_vest_wall_${side > 0 ? "r" : "l"}`;
        this.scene.add(vestWallMesh);
        chunk.meshes.push(vestWallMesh);
        this.collisionWorld.addStaticBox(
          vestWallMesh.name,
          vestWallMesh.position,
          new THREE.Vector3(0.3, 2.8, vestLength),
          chunkId
        );
      }

      // Upper ceiling over 2F landing & stairs: from z = -7.8 to z = 2.5 at Y = 7.8
      const ceil2FGeo = this.getPlaneGeometry(rampHalfWidth * 2, stairTotalZ);
      const ceil2FMesh = new THREE.Mesh(ceil2FGeo, ceilingMat);
      ceil2FMesh.rotation.x = Math.PI / 2;
      ceil2FMesh.position.set(f2StartX, floorY + 7.8, stairCenterZ);
      ceil2FMesh.receiveShadow = true;
      ceil2FMesh.name = `${chunkId}_stair_2f_ceiling_upper`;
      this.scene.add(ceil2FMesh);
      chunk.meshes.push(ceil2FMesh);

      // Vertical header wall at z = 2.5 closing 1F ceiling (2.8) to 2F ceiling (7.8)
      const headerGeo = this.getBoxGeometry(rampHalfWidth * 2, 5.0, 0.2);
      const headerMesh = new THREE.Mesh(headerGeo, stairMat);
      headerMesh.position.set(f2StartX, floorY + 5.3, f2StartZ);
      headerMesh.name = `${chunkId}_stair_2f_ceiling_header`;
      this.scene.add(headerMesh);
      chunk.meshes.push(headerMesh);

      // ====================================================
      // 2F Hwacat Painting & Shrine Altar Table
      // West wall of the inner shrine, facing east (+X)
      // ====================================================
      const paintFrameGeo = this.getBoxGeometry(1.85, 2.15, 0.08);
      const paintFrameMesh = new THREE.Mesh(paintFrameGeo, this.propMaterial);
      paintFrameMesh.position.set(-37.68, floorY + 6.6, -22.0);
      paintFrameMesh.rotation.y = Math.PI / 2;
      paintFrameMesh.castShadow = true;
      paintFrameMesh.name = `${chunkId}_gallery_2f_painting_frame`;
      this.scene.add(paintFrameMesh);
      chunk.meshes.push(paintFrameMesh);

      const paintGeo = this.getBoxGeometry(1.65, 1.95, 0.08);
      const paintMat = this.textures.createHwaPaintMaterial();
      const painting = new THREE.Mesh(paintGeo, paintMat);
      painting.name = "upper-hwa-painting";
      painting.position.set(-37.6, floorY + 6.6, -22.0);
      painting.rotation.y = Math.PI / 2;
      painting.castShadow = true;
      painting.receiveShadow = true;
      this.scene.add(painting);
      chunk.meshes.push(painting);

      const altarGeo = this.getBoxGeometry(0.7, 0.85, 2.0);
      const altarMesh = new THREE.Mesh(altarGeo, this.propMaterial);
      altarMesh.position.set(-37.15, floorY + 5.0 + 0.425, -22.0);
      altarMesh.castShadow = true;
      altarMesh.receiveShadow = true;
      altarMesh.name = `${chunkId}_gallery_2f_altar`;
      this.scene.add(altarMesh);
      chunk.meshes.push(altarMesh);
      this.collisionWorld.addStaticBox(altarMesh.name, altarMesh.position, new THREE.Vector3(0.7, 0.85, 2.0), chunkId);

      const galleryBenchGeo = this.getBoxGeometry(0.55, 0.46, 2.4);
      const galleryBench = new THREE.Mesh(galleryBenchGeo, this.propMaterial);
      galleryBench.position.set(-22.4, floorY + 5.23, -12.2);
      galleryBench.castShadow = true;
      galleryBench.receiveShadow = true;
      galleryBench.name = `${chunkId}_gallery_2f_bench`;
      this.scene.add(galleryBench);
      chunk.meshes.push(galleryBench);
      this.collisionWorld.addStaticBox(galleryBench.name, galleryBench.position, new THREE.Vector3(0.55, 0.46, 2.4), chunkId);

      const sideFrameGeo = this.getBoxGeometry(1.2, 1.45, 0.07);
      const sideFrame = new THREE.Mesh(sideFrameGeo, this.propMaterial);
      sideFrame.position.set(-37.72, floorY + 6.35, -30.5);
      sideFrame.rotation.y = Math.PI / 2;
      sideFrame.castShadow = true;
      sideFrame.name = `${chunkId}_gallery_2f_side_frame`;
      this.scene.add(sideFrame);
      chunk.meshes.push(sideFrame);

      this.dressBloodGallery(chunk, chunkId, floorY, {
        galMinX,
        galMaxX,
        galMinZ,
        galMaxZ,
        galWestWingCenterX,
        galWestWingWidth,
        galLength,
        galCenterZ,
        f2StartX,
        rampHalfWidth,
      });

      // CollisionWorld Ramp for 2F
      this.collisionWorld.addRamp({
        id: "stairs_1f_to_2f",
        axis: "z",
        startY: floorY + 0.0,
        endY: floorY + 5.0,
        startZ: f2StartZ,
        endZ: f2EndZ,
        minX: f2StartX - rampHalfWidth,
        maxX: f2StartX + rampHalfWidth,
        minZ: Math.min(f2StartZ, f2EndZ),
        maxZ: Math.max(f2StartZ, f2EndZ),
        startFloor: 1,
        endFloor: 2,
        chunkId: chunkId,
      });

      this.collisionWorld.addFloorArea({
        id: "gallery_2f_mirror",
        floor: 2,
        type: "walkable",
        y: floorY + 5.0,
        minX: -38.0,
        maxX: -17.2,
        minZ: -36.0,
        maxZ: -8.5,
      }, chunkId);
      this.collisionWorld.addFloorArea({
        id: "landing_2f_gallery",
        floor: 2,
        type: "walkable",
        y: floorY + 5.0,
        minX: f2StartX - rampHalfWidth,
        maxX: f2StartX + rampHalfWidth,
        minZ: Math.min(f2EndZ, center.z - 7.8),
        maxZ: Math.max(f2EndZ, center.z - 7.8),
      }, chunkId);

      // Transition waypoints for 2F
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_1f_top_to_2f",
        position: [f2StartX, floorY + 0.0, f2StartZ + 0.5],
        floor: 1,
        links: ["tw_2f_bottom_from_1f"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_bottom_from_1f",
        position: [f2StartX, floorY + 5.0, f2EndZ - 0.5],
        floor: 2,
        links: ["tw_1f_top_to_2f", "tw_2f_landing"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_landing",
        position: [f2StartX, floorY + 5.0, -22.0],
        floor: 2,
        links: ["tw_2f_bottom_from_1f", "tw_2f_gallery_doorway"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_doorway",
        position: [-17.2, floorY + 5.0, -21.8],
        floor: 2,
        links: ["tw_2f_landing", "tw_2f_gallery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_center",
        position: [-22.5, floorY + 5.0, -22.0],
        floor: 2,
        links: ["tw_2f_gallery_doorway", "tw_2f_gallery_inner_door", "tw_2f_gallery_south"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_inner_door",
        position: [-27.5, floorY + 5.0, -22.0],
        floor: 2,
        links: ["tw_2f_gallery_center", "tw_2f_gallery_altar"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_altar",
        position: [-35.5, floorY + 5.0, -22.0],
        floor: 2,
        links: ["tw_2f_gallery_inner_door", "tw_2f_gallery_north"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_south",
        position: [-22.5, floorY + 5.0, -12.0],
        floor: 2,
        links: ["tw_2f_gallery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_north",
        position: [-32.5, floorY + 5.0, -31.0],
        floor: 2,
        links: ["tw_2f_gallery_altar"],
      }, chunkId);

      this.dressGalleryMaze(chunk, chunkId, floorY, {
        galMinX,
        galMaxX,
        galMinZ,
        galMaxZ,
      });
      this.dressGalleryLabyrinth(chunk, chunkId, floorY, stairMat, ceilingMat);
    } else if (type === "stairs_b1") {
      // 1F → B1 Staircase (descending from Y = 0.0 to Y = -5.0 towards South, inside chunk (1, 2))
      // Entrance is from North (z = -7.8)
      // Vestibule at Y = 0.0 from z = -7.8 to z = -2.5
      // Staircase runs from z = -2.5 (Y = 0.0) to z = 4.5 (Y = -5.0)
      // B1 Landing from z = 4.5 to z = 7.8 at Y = -5.0
      const b1StartX = center.x;
      const b1StartZ = center.z - 2.5;
      const b1EndZ = center.z + 4.5;
      const stepDepth = Math.abs(b1EndZ - b1StartZ) / STEP_COUNT;
      const stepHeight = 5.0 / STEP_COUNT;

      // Weathered stone and damp wood materials for B1
      const b1WallMat = new THREE.MeshStandardMaterial({
        map: this.textures.load("wall"),
        color: 0x4a5a52,
        roughness: 0.88,
        metalness: 0.04,
        emissive: 0x0a2218,
        emissiveIntensity: 0.1,
      });
      const b1FloorMat = new THREE.MeshStandardMaterial({
        map: this.textures.load("floor"),
        color: 0x3a4840,
        roughness: 0.55,
        metalness: 0.06,
        emissive: 0x081810,
        emissiveIntensity: 0.08,
      });
      const b1CeilMat = new THREE.MeshStandardMaterial({
        map: this.textures.load("ceiling"),
        color: 0x24322c,
        roughness: 0.94,
        emissive: 0x061410,
        emissiveIntensity: 0.06,
      });

      // Visual stair steps
      for (let i = 0; i < STEP_COUNT; i++) {
        const progress = i / STEP_COUNT;
        const stepGeo = this.getBoxGeometry(rampHalfWidth * 2, stepHeight + 0.06, stepDepth);
        const stepMesh = new THREE.Mesh(stepGeo, b1WallMat);
        stepMesh.position.set(
          b1StartX,
          floorY - progress * 5.0 - stepHeight / 2,
          b1StartZ + progress * Math.abs(b1EndZ - b1StartZ) + stepDepth / 2
        );
        stepMesh.castShadow = true;
        stepMesh.receiveShadow = true;
        stepMesh.name = `${chunkId}_stair_b1_step_${i}`;
        this.scene.add(stepMesh);
        chunk.meshes.push(stepMesh);
      }

      // B1 Landing platform floor (Y = -5.0, from z = 36.5 to 39.8)
      const landingMinZ = b1EndZ;
      const landingMaxZ = center.z + 7.8;
      const landingLength = landingMaxZ - landingMinZ;
      const landingGeo = this.getBoxGeometry(rampHalfWidth * 2, 0.2, landingLength);
      const landingMesh = new THREE.Mesh(landingGeo, b1FloorMat);
      landingMesh.position.set(
        b1StartX,
        floorY - 5.0 - 0.1,
        (landingMinZ + landingMaxZ) / 2
      );
      landingMesh.receiveShadow = true;
      landingMesh.name = `${chunkId}_stair_b1_landing_mesh`;
      this.scene.add(landingMesh);
      chunk.meshes.push(landingMesh);

      // ====================================================
      // B1 Underground Cellar & Nursery (지하 음습한 보육실 & 복도)
      // Expanded west nursery + inner crib room + east storage, not a closet.
      // Spans x from -8.0 to 26.0, z from 24.2 to 39.8 at Y = -5.0
      // ====================================================
      const b1MinX = -8.0;
      const b1MaxX = 26.0;
      const b1MinZ = 24.2;
      const b1MaxZ = 39.8;
      const b1TotalWidth = b1MaxX - b1MinX; // 15.6m
      const b1TotalLength = b1MaxZ - b1MinZ; // 15.6m
      const b1CenterX = (b1MinX + b1MaxX) / 2; // 16.0
      const b1CenterZ = (b1MinZ + b1MaxZ) / 2; // 32.0

      const westWingWidth = (b1StartX - rampHalfWidth) - b1MinX; // 6.6m
      const westWingCenterX = (b1MinX + (b1StartX - rampHalfWidth)) / 2; // 11.5
      const eastWingWidth = b1MaxX - (b1StartX + rampHalfWidth); // 6.6m
      const eastWingCenterX = ((b1StartX + rampHalfWidth) + b1MaxX) / 2; // 20.5

      // Solid B1 Floor planes (Y = -5.0)
      const westFloorGeo = this.getPlaneGeometry(westWingWidth, b1TotalLength);
      const westFloorMesh = new THREE.Mesh(westFloorGeo, b1FloorMat);
      westFloorMesh.rotation.x = -Math.PI / 2;
      westFloorMesh.position.set(westWingCenterX, floorY - 5.0, b1CenterZ);
      westFloorMesh.receiveShadow = true;
      westFloorMesh.name = `${chunkId}_cellar_b1_floor_w`;
      this.scene.add(westFloorMesh);
      chunk.meshes.push(westFloorMesh);

      const eastFloorGeo = this.getPlaneGeometry(eastWingWidth, b1TotalLength);
      const eastFloorMesh = new THREE.Mesh(eastFloorGeo, b1FloorMat);
      eastFloorMesh.rotation.x = -Math.PI / 2;
      eastFloorMesh.position.set(eastWingCenterX, floorY - 5.0, b1CenterZ);
      eastFloorMesh.receiveShadow = true;
      eastFloorMesh.name = `${chunkId}_cellar_b1_floor_e`;
      this.scene.add(eastFloorMesh);
      chunk.meshes.push(eastFloorMesh);

      // Solid B1 Ceiling planes (Y = -2.2, height 2.8m)
      const westCeilGeo = this.getPlaneGeometry(westWingWidth, b1TotalLength);
      const westCeilMesh = new THREE.Mesh(westCeilGeo, b1CeilMat);
      westCeilMesh.rotation.x = Math.PI / 2;
      westCeilMesh.position.set(westWingCenterX, floorY - 2.2, b1CenterZ);
      westCeilMesh.receiveShadow = true;
      westCeilMesh.name = `${chunkId}_cellar_b1_ceiling_w`;
      this.scene.add(westCeilMesh);
      chunk.meshes.push(westCeilMesh);

      const eastCeilGeo = this.getPlaneGeometry(eastWingWidth, b1TotalLength);
      const eastCeilMesh = new THREE.Mesh(eastCeilGeo, b1CeilMat);
      eastCeilMesh.rotation.x = Math.PI / 2;
      eastCeilMesh.position.set(eastWingCenterX, floorY - 2.2, b1CenterZ);
      eastCeilMesh.receiveShadow = true;
      eastCeilMesh.name = `${chunkId}_cellar_b1_ceiling_e`;
      this.scene.add(eastCeilMesh);
      chunk.meshes.push(eastCeilMesh);

      // Landing ceiling at Y = -2.2
      const landCeilGeo = this.getPlaneGeometry(rampHalfWidth * 2, landingLength);
      const landCeilMesh = new THREE.Mesh(landCeilGeo, b1CeilMat);
      landCeilMesh.rotation.x = Math.PI / 2;
      landCeilMesh.position.set(b1StartX, floorY - 2.2, (landingMinZ + landingMaxZ) / 2);
      landCeilMesh.receiveShadow = true;
      landCeilMesh.name = `${chunkId}_cellar_b1_ceiling_landing`;
      this.scene.add(landCeilMesh);
      chunk.meshes.push(landCeilMesh);

      // Vertical header wall at z = 36.5 closing B1 ceiling (Y = -2.2) to 1F ceiling (Y = 2.8) over stair opening
      const b1HeaderGeo = this.getBoxGeometry(rampHalfWidth * 2, 5.0, 0.2);
      const b1HeaderMesh = new THREE.Mesh(b1HeaderGeo, b1WallMat);
      b1HeaderMesh.position.set(b1StartX, floorY + 0.3, b1EndZ);
      b1HeaderMesh.name = `${chunkId}_cellar_b1_ceiling_header`;
      this.scene.add(b1HeaderMesh);
      chunk.meshes.push(b1HeaderMesh);

      // Perimeter enclosing walls for B1:
      // North Wall: at z = 24.2, spanning full width from x = 8.2 to 23.8 (15.6m)
      const b1NorthGeo = this.getBoxGeometry(b1TotalWidth + 0.4, 2.8, 0.4);
      const b1NorthMesh = new THREE.Mesh(b1NorthGeo, b1WallMat);
      b1NorthMesh.position.set(b1CenterX, floorY - 3.6, b1MinZ - 0.15);
      b1NorthMesh.castShadow = true;
      b1NorthMesh.name = `${chunkId}_cellar_b1_wall_n`;
      this.scene.add(b1NorthMesh);
      chunk.meshes.push(b1NorthMesh);
      this.collisionWorld.addStaticBox(b1NorthMesh.name, b1NorthMesh.position, new THREE.Vector3(b1TotalWidth + 0.4, 2.8, 0.4), chunkId);

      // South wall keeps the stair landing boxed in, with maze gates at x=8.4 and x=20.5.
      this.placeGappedWall(chunk, chunkId, "cellar_b1_wall_s", {
        axis: "x",
        pos: b1MaxZ + 0.15,
        min: b1MinX,
        max: b1MaxX,
        wallY: floorY - 3.6,
        height: 2.8,
        thickness: 0.4,
        material: b1WallMat,
        gaps: [{ center: 8.4, width: 2.7 }, { center: 20.5, width: 2.7 }],
      });

      // West wall opens into the flooded west labyrinth at the crib hall and north hall.
      this.placeGappedWall(chunk, chunkId, "cellar_b1_wall_w", {
        axis: "z",
        pos: b1MinX - 0.15,
        min: b1MinZ,
        max: b1MaxZ,
        wallY: floorY - 3.6,
        height: 2.8,
        thickness: 0.4,
        material: b1WallMat,
        gaps: [{ center: 30.0, width: 2.7 }, { center: 38.0, width: 2.7 }],
      });

      // Inner crib-room partition at x = 1.2 with a 2.4m doorway facing the baby
      const b1InnerX = 1.2;
      const b1DoorMinZ = 28.8;
      const b1DoorMaxZ = 31.2;
      const b1InnerJambNLen = b1DoorMinZ - b1MinZ;
      const b1InnerJambNGeo = this.getBoxGeometry(0.3, 2.8, b1InnerJambNLen);
      const b1InnerJambN = new THREE.Mesh(b1InnerJambNGeo, b1WallMat);
      b1InnerJambN.position.set(b1InnerX, floorY - 3.6, (b1MinZ + b1DoorMinZ) / 2);
      b1InnerJambN.castShadow = true;
      b1InnerJambN.name = `${chunkId}_cellar_b1_inner_jamb_n`;
      this.scene.add(b1InnerJambN);
      chunk.meshes.push(b1InnerJambN);
      this.collisionWorld.addStaticBox(b1InnerJambN.name, b1InnerJambN.position, new THREE.Vector3(0.3, 2.8, b1InnerJambNLen), chunkId);

      const b1InnerJambSLen = b1MaxZ - b1DoorMaxZ;
      const b1InnerJambSGeo = this.getBoxGeometry(0.3, 2.8, b1InnerJambSLen);
      const b1InnerJambS = new THREE.Mesh(b1InnerJambSGeo, b1WallMat);
      b1InnerJambS.position.set(b1InnerX, floorY - 3.6, (b1DoorMaxZ + b1MaxZ) / 2);
      b1InnerJambS.castShadow = true;
      b1InnerJambS.name = `${chunkId}_cellar_b1_inner_jamb_s`;
      this.scene.add(b1InnerJambS);
      chunk.meshes.push(b1InnerJambS);
      this.collisionWorld.addStaticBox(b1InnerJambS.name, b1InnerJambS.position, new THREE.Vector3(0.3, 2.8, b1InnerJambSLen), chunkId);

      const b1InnerLintelGeo = this.getBoxGeometry(0.3, 0.6, 2.4);
      const b1InnerLintel = new THREE.Mesh(b1InnerLintelGeo, b1WallMat);
      b1InnerLintel.position.set(b1InnerX, floorY - 2.5, (b1DoorMinZ + b1DoorMaxZ) / 2);
      b1InnerLintel.name = `${chunkId}_cellar_b1_inner_lintel`;
      this.scene.add(b1InnerLintel);
      chunk.meshes.push(b1InnerLintel);

      this.placeGappedWall(chunk, chunkId, "cellar_b1_wall_e", {
        axis: "z",
        pos: b1MaxX + 0.15,
        min: b1MinZ,
        max: b1MaxZ,
        wallY: floorY - 3.6,
        height: 2.8,
        thickness: 0.4,
        material: b1WallMat,
        gaps: [{ center: 28.0, width: 2.7 }, { center: 38.0, width: 2.7 }],
      });

      // Dividing Walls around stairs and doorway to landing:
      // Dividing Walls around stairs and doorway to landing:
      // Full-height West enclosing wall along x = 14.8: spans Y = -5.0 to Y = 2.8 (height 7.8m, center floorY - 1.1)
      const rampDivLen = b1EndZ - b1MinZ; // from z = 24.2 to 36.5 (12.3m)
      const rampDivCenterZ = (b1MinZ + b1EndZ) / 2;
      const rampDivGeo = this.getBoxGeometry(0.3, 7.8, rampDivLen);
      const rampDivMesh = new THREE.Mesh(rampDivGeo, b1WallMat);
      rampDivMesh.position.set(b1StartX - (rampHalfWidth + 0.15), floorY - 1.1, rampDivCenterZ);
      rampDivMesh.castShadow = true;
      rampDivMesh.name = `${chunkId}_cellar_b1_wall_ramp_div`;
      this.scene.add(rampDivMesh);
      chunk.meshes.push(rampDivMesh);
      this.collisionWorld.addStaticBox(rampDivMesh.name, rampDivMesh.position, new THREE.Vector3(0.3, 7.8, rampDivLen), chunkId);

      // Doorway: 2.4m opening at x = 14.8 connecting landing to cellar (z from 36.8 to 39.2)
      // North jamb of doorway (z from 36.5 to 36.8, length 0.3m, height 2.8m from Y = -5.0 to -2.2)
      const b1JambNGeo = this.getBoxGeometry(0.3, 2.8, 0.3);
      const b1JambNMesh = new THREE.Mesh(b1JambNGeo, b1WallMat);
      b1JambNMesh.position.set(b1StartX - (rampHalfWidth + 0.15), floorY - 3.6, 36.65);
      b1JambNMesh.castShadow = true;
      b1JambNMesh.name = `${chunkId}_cellar_b1_jamb_n`;
      this.scene.add(b1JambNMesh);
      chunk.meshes.push(b1JambNMesh);
      this.collisionWorld.addStaticBox(b1JambNMesh.name, b1JambNMesh.position, new THREE.Vector3(0.3, 2.8, 0.3), chunkId);

      // South jamb of doorway (z from 39.2 to 39.8, length 0.6m, height 2.8m from Y = -5.0 to -2.2)
      const b1JambSGeo = this.getBoxGeometry(0.3, 2.8, 0.6);
      const b1JambSMesh = new THREE.Mesh(b1JambSGeo, b1WallMat);
      b1JambSMesh.position.set(b1StartX - (rampHalfWidth + 0.15), floorY - 3.6, 39.5);
      b1JambSMesh.castShadow = true;
      b1JambSMesh.name = `${chunkId}_cellar_b1_jamb_s`;
      this.scene.add(b1JambSMesh);
      chunk.meshes.push(b1JambSMesh);
      this.collisionWorld.addStaticBox(b1JambSMesh.name, b1JambSMesh.position, new THREE.Vector3(0.3, 2.8, 0.6), chunkId);

      // Doorway header lintel over 2.4m doorway opening (height 0.6, from Y = -2.8 to -2.2)
      const b1LintelGeo = this.getBoxGeometry(0.3, 0.6, 2.4);
      const b1LintelMesh = new THREE.Mesh(b1LintelGeo, b1WallMat);
      b1LintelMesh.position.set(b1StartX - (rampHalfWidth + 0.15), floorY - 2.5, 38.0);
      b1LintelMesh.name = `${chunkId}_cellar_b1_door_lintel`;
      this.scene.add(b1LintelMesh);
      chunk.meshes.push(b1LintelMesh);

      // Upper wall over landing doorway: from Y = -2.2 to Y = 2.8 (height 5.0m, center floorY + 0.3, z from 36.5 to 39.8)
      const doorwayUpperGeo = this.getBoxGeometry(0.3, 5.0, 3.3);
      const doorwayUpperMesh = new THREE.Mesh(doorwayUpperGeo, b1WallMat);
      doorwayUpperMesh.position.set(b1StartX - (rampHalfWidth + 0.15), floorY + 0.3, 38.15);
      doorwayUpperMesh.name = `${chunkId}_cellar_b1_door_upper`;
      this.scene.add(doorwayUpperMesh);
      chunk.meshes.push(doorwayUpperMesh);
      this.collisionWorld.addStaticBox(doorwayUpperMesh.name, doorwayUpperMesh.position, new THREE.Vector3(0.3, 5.0, 3.3), chunkId);

      // East stair enclosure along x = 17.2 (solid beside the ramp, doorway on the landing)
      const eastRampDivLen = b1EndZ - b1MinZ;
      const eastRampDivGeo = this.getBoxGeometry(0.3, 7.8, eastRampDivLen);
      const eastRampDivMesh = new THREE.Mesh(eastRampDivGeo, b1WallMat);
      eastRampDivMesh.position.set(b1StartX + (rampHalfWidth + 0.15), floorY - 1.1, (b1MinZ + b1EndZ) / 2);
      eastRampDivMesh.castShadow = true;
      eastRampDivMesh.name = `${chunkId}_cellar_b1_wall_e_div`;
      this.scene.add(eastRampDivMesh);
      chunk.meshes.push(eastRampDivMesh);
      this.collisionWorld.addStaticBox(eastRampDivMesh.name, eastRampDivMesh.position, new THREE.Vector3(0.3, 7.8, eastRampDivLen), chunkId);

      const b1EastJambNGeo = this.getBoxGeometry(0.3, 2.8, 0.3);
      const b1EastJambN = new THREE.Mesh(b1EastJambNGeo, b1WallMat);
      b1EastJambN.position.set(b1StartX + (rampHalfWidth + 0.15), floorY - 3.6, 36.65);
      b1EastJambN.castShadow = true;
      b1EastJambN.name = `${chunkId}_cellar_b1_east_jamb_n`;
      this.scene.add(b1EastJambN);
      chunk.meshes.push(b1EastJambN);
      this.collisionWorld.addStaticBox(b1EastJambN.name, b1EastJambN.position, new THREE.Vector3(0.3, 2.8, 0.3), chunkId);

      const b1EastJambSGeo = this.getBoxGeometry(0.3, 2.8, 0.6);
      const b1EastJambS = new THREE.Mesh(b1EastJambSGeo, b1WallMat);
      b1EastJambS.position.set(b1StartX + (rampHalfWidth + 0.15), floorY - 3.6, 39.5);
      b1EastJambS.castShadow = true;
      b1EastJambS.name = `${chunkId}_cellar_b1_east_jamb_s`;
      this.scene.add(b1EastJambS);
      chunk.meshes.push(b1EastJambS);
      this.collisionWorld.addStaticBox(b1EastJambS.name, b1EastJambS.position, new THREE.Vector3(0.3, 2.8, 0.6), chunkId);

      const b1EastLintelGeo = this.getBoxGeometry(0.3, 0.6, 2.4);
      const b1EastLintel = new THREE.Mesh(b1EastLintelGeo, b1WallMat);
      b1EastLintel.position.set(b1StartX + (rampHalfWidth + 0.15), floorY - 2.5, 38.0);
      b1EastLintel.name = `${chunkId}_cellar_b1_east_door_lintel`;
      this.scene.add(b1EastLintel);
      chunk.meshes.push(b1EastLintel);

      const eastDoorUpperGeo = this.getBoxGeometry(0.3, 5.0, 3.3);
      const eastDoorUpper = new THREE.Mesh(eastDoorUpperGeo, b1WallMat);
      eastDoorUpper.position.set(b1StartX + (rampHalfWidth + 0.15), floorY + 0.3, 38.15);
      eastDoorUpper.name = `${chunkId}_cellar_b1_east_door_upper`;
      this.scene.add(eastDoorUpper);
      chunk.meshes.push(eastDoorUpper);
      this.collisionWorld.addStaticBox(eastDoorUpper.name, eastDoorUpper.position, new THREE.Vector3(0.3, 5.0, 3.3), chunkId);

      // Vertical header wall under 1F floor at z = 29.5: from Y = -5.0 to Y = 0.0
      const headerGeo = this.getBoxGeometry(rampHalfWidth * 2, 5.0, 0.2);
      const headerMesh = new THREE.Mesh(headerGeo, stairMat);
      headerMesh.position.set(b1StartX, floorY - 2.5, b1StartZ);
      headerMesh.name = `${chunkId}_stair_b1_floor_header`;
      this.scene.add(headerMesh);
      chunk.meshes.push(headerMesh);

      // ====================================================
      // B1 Tatami & Nursery Area in the inner crib room
      // Torn mats, blood stains, and broken toys around the baby
      // ====================================================
      const tatamiMat = new THREE.MeshStandardMaterial({
        color: 0x5a5438,
        roughness: 0.9,
        metalness: 0.02,
      });
      const mat1Geo = this.getBoxGeometry(1.8, 0.06, 1.0);
      const mat1Mesh = new THREE.Mesh(mat1Geo, tatamiMat);
      mat1Mesh.position.set(-3.5, floorY - 5.0 + 0.03, 27.8);
      mat1Mesh.receiveShadow = true;
      mat1Mesh.name = `${chunkId}_b1_tatami_1`;
      this.scene.add(mat1Mesh);
      chunk.meshes.push(mat1Mesh);

      const mat2Geo = this.getBoxGeometry(1.8, 0.06, 1.0);
      const mat2Mesh = new THREE.Mesh(mat2Geo, tatamiMat);
      mat2Mesh.position.set(-3.4, floorY - 5.0 + 0.035, 28.9);
      mat2Mesh.rotation.y = 0.06;
      mat2Mesh.receiveShadow = true;
      mat2Mesh.name = `${chunkId}_b1_tatami_2`;
      this.scene.add(mat2Mesh);
      chunk.meshes.push(mat2Mesh);

      const bloodGeo = this.getPlaneGeometry(2.2, 1.8);
      const bloodMat = new THREE.MeshStandardMaterial({
        color: 0x14241c,
        roughness: 0.18,
        metalness: 0.35,
        transparent: true,
        opacity: 0.7,
        depthWrite: false,
      });
      const bloodMesh = new THREE.Mesh(bloodGeo, bloodMat);
      bloodMesh.rotation.x = -Math.PI / 2;
      bloodMesh.position.set(-3.5, floorY - 5.0 + 0.19, 28.4);
      bloodMesh.name = `${chunkId}_b1_scum_slick`;
      this.scene.add(bloodMesh);
      chunk.meshes.push(bloodMesh);

      this.spawnAssetProp(chunk, {
        ...HORROR_PROP_ASSETS.brokenDollPile,
        id: `${chunkId}_b1_nursery_dolls`,
        position: [-3.5, floorY - 5.0, 28.5],
        rotation: [0, Math.PI / 4, 0],
      });

      const toyBlock1 = new THREE.Mesh(this.getBoxGeometry(0.38, 0.3, 0.38), this.propMaterial);
      toyBlock1.position.set(-2.7, floorY - 5.0 + 0.15, 27.7);
      toyBlock1.rotation.y = 0.4;
      toyBlock1.castShadow = true;
      toyBlock1.name = `${chunkId}_b1_toy_1`;
      this.scene.add(toyBlock1);
      chunk.meshes.push(toyBlock1);

      const toyBlock2 = new THREE.Mesh(this.getBoxGeometry(0.3, 0.24, 0.3), this.propMaterial);
      toyBlock2.position.set(-4.3, floorY - 5.0 + 0.12, 29.3);
      toyBlock2.rotation.y = -0.3;
      toyBlock2.castShadow = true;
      toyBlock2.name = `${chunkId}_b1_toy_2`;
      this.scene.add(toyBlock2);
      chunk.meshes.push(toyBlock2);

      const hallCrate = new THREE.Mesh(this.getBoxGeometry(0.9, 0.7, 0.9), this.propMaterial);
      hallCrate.position.set(8.6, floorY - 5.0 + 0.35, 26.4);
      hallCrate.castShadow = true;
      hallCrate.receiveShadow = true;
      hallCrate.name = `${chunkId}_b1_hall_crate`;
      this.scene.add(hallCrate);
      chunk.meshes.push(hallCrate);
      this.collisionWorld.addStaticBox(hallCrate.name, hallCrate.position, new THREE.Vector3(0.9, 0.7, 0.9), chunkId);

      const eastShelf = new THREE.Mesh(this.getBoxGeometry(0.46, 1.4, 2.2), this.propMaterial);
      eastShelf.position.set(25.2, floorY - 5.0 + 0.7, 32.0);
      eastShelf.castShadow = true;
      eastShelf.receiveShadow = true;
      eastShelf.name = `${chunkId}_b1_east_shelf`;
      this.scene.add(eastShelf);
      chunk.meshes.push(eastShelf);
      this.collisionWorld.addStaticBox(eastShelf.name, eastShelf.position, new THREE.Vector3(0.46, 1.4, 2.2), chunkId);

      this.dressBasementFlood(chunk, chunkId, floorY, {
        westWingWidth,
        westWingCenterX,
        eastWingWidth,
        eastWingCenterX,
        b1TotalLength,
        b1CenterX,
        b1CenterZ,
        b1StartX,
        rampHalfWidth,
        landingLength,
        landingMinZ,
        landingMaxZ,
        b1MinX,
        b1MaxX,
        b1MinZ,
        b1MaxZ,
      });

      // CollisionWorld Ramp for B1
      this.collisionWorld.addRamp({
        id: "stairs_1f_to_b1",
        axis: "z",
        startY: floorY + 0.0,
        endY: floorY - 5.0,
        startZ: b1StartZ,
        endZ: b1EndZ,
        minX: b1StartX - rampHalfWidth,
        maxX: b1StartX + rampHalfWidth,
        minZ: Math.min(b1StartZ, b1EndZ),
        maxZ: Math.max(b1StartZ, b1EndZ),
        startFloor: 1,
        endFloor: -1,
        chunkId: chunkId,
      });

      this.collisionWorld.addFloorArea({
        id: "cellar_b1_west",
        floor: -1,
        type: "walkable",
        y: floorY - 5.0,
        minX: -8.0,
        maxX: b1StartX - rampHalfWidth,
        minZ: 24.2,
        maxZ: 39.8,
      }, chunkId);
      this.collisionWorld.addFloorArea({
        id: "cellar_b1_east",
        floor: -1,
        type: "walkable",
        y: floorY - 5.0,
        minX: b1StartX + rampHalfWidth,
        maxX: 26.0,
        minZ: 24.2,
        maxZ: 39.8,
      }, chunkId);

      // Landing floor area at bottom of B1 stair
      this.collisionWorld.addFloorArea({
        id: "landing_b1_from_stairs",
        floor: -1,
        type: "walkable",
        y: floorY - 5.0,
        minX: b1StartX - rampHalfWidth,
        maxX: b1StartX + rampHalfWidth,
        minZ: b1EndZ,
        maxZ: center.z + 7.8,
      }, chunkId);

      // Transition waypoints for B1
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_1f_top_to_b1",
        position: [b1StartX, floorY + 0.0, b1StartZ - 0.5],
        floor: 1,
        links: ["tw_b1_bottom_from_1f"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_bottom_from_1f",
        position: [b1StartX, floorY - 5.0, b1EndZ + 0.5],
        floor: -1,
        links: ["tw_1f_top_to_b1", "tw_b1_landing"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_landing",
        position: [b1StartX, floorY - 5.0, 38.0],
        floor: -1,
        links: ["tw_b1_bottom_from_1f", "tw_b1_cellar_doorway", "tw_b1_east_door"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_cellar_doorway",
        position: [14.8, floorY - 5.0, 38.0],
        floor: -1,
        links: ["tw_b1_landing", "tw_b1_cellar_hall"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_east_door",
        position: [17.2, floorY - 5.0, 38.0],
        floor: -1,
        links: ["tw_b1_landing", "tw_b1_east_store"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_cellar_hall",
        position: [8.0, floorY - 5.0, 34.0],
        floor: -1,
        links: ["tw_b1_cellar_doorway", "tw_b1_inner_door"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_cabinet",
        position: [-6.5, floorY - 5.0, 34.0],
        floor: -1,
        links: ["tw_b1_inner_door", "tw_b1_nursery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_inner_door",
        position: [1.2, floorY - 5.0, 30.0],
        floor: -1,
        links: ["tw_b1_cellar_hall", "tw_b1_nursery_center", "tw_b1_cabinet"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_nursery_center",
        position: [-3.5, floorY - 5.0, 28.5],
        floor: -1,
        links: ["tw_b1_inner_door", "tw_b1_cabinet", "tw_b1_nursery_corner"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_nursery_corner",
        position: [-2.5, floorY - 5.0, 26.5],
        floor: -1,
        links: ["tw_b1_nursery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_east_store",
        position: [20.5, floorY - 5.0, 32.0],
        floor: -1,
        links: ["tw_b1_east_door"],
      }, chunkId);

      this.dressBasementMaze(chunk, chunkId, floorY, {
        b1MinX,
        b1MaxX,
        b1MinZ,
        b1MaxZ,
        b1StartX,
        rampHalfWidth,
      });
      this.dressBasementLabyrinth(chunk, chunkId, floorY, b1WallMat, b1FloorMat, b1CeilMat);
    }
  }

  buildTemplateWalls(chunk, type, center, chunkId, rand, floorY = 0) {
    const wallMaterial = this.textures.createWallMaterial();
    const trimMaterial = this.trimMaterial;

    const wallsData = [];

    const addWallSegment = (localX, localZ, sizeX, sizeZ, name) => {
      wallsData.push({ localX, localZ, sizeX, sizeZ, name });
      const meshName = `${chunkId}_wall_${name}`;
      const globalPos = new THREE.Vector3(center.x + localX, floorY + 1.4, center.z + localZ);
      this.collisionWorld.addStaticBox(meshName, globalPos, new THREE.Vector3(sizeX, 2.8, sizeZ), chunkId);
    };


    // Connections come from the mansion graph so neighboring chunks always agree.
    const openings = this.getOpenings(chunk.cx, chunk.cz);
    const N = openings.N;
    const S = openings.S;
    const E = openings.E;
    const W = openings.W;

    // Cardinal doors: 2.4m into special plus tiles, 3.4m maze-to-maze so the
    // school corridor can run through tile centers without ring gates.
    const hallN = N && this.isMazeHall(chunk.cx, chunk.cz) && this.isMazeHall(chunk.cx, chunk.cz - 1);
    const hallS = S && this.isMazeHall(chunk.cx, chunk.cz) && this.isMazeHall(chunk.cx, chunk.cz + 1);
    const hallW = W && this.isMazeHall(chunk.cx, chunk.cz) && this.isMazeHall(chunk.cx - 1, chunk.cz);
    const hallE = E && this.isMazeHall(chunk.cx, chunk.cz) && this.isMazeHall(chunk.cx + 1, chunk.cz);
    const doorSpan = (wide) => {
      const half = wide ? 1.7 : 1.2;
      return {
        left: (-8.0 - half) / 2,
        right: (8.0 + half) / 2,
        size: 8.0 - half,
      };
    };

    if (N) {
      const span = doorSpan(hallN);
      addWallSegment(span.left, -7.8, span.size, 0.4, "n_left");
      addWallSegment(span.right, -7.8, span.size, 0.4, "n_right");
    } else {
      addWallSegment(0.0, -7.8, 16.0, 0.4, "n_solid");
    }

    if (S) {
      const span = doorSpan(hallS);
      addWallSegment(span.left, 7.8, span.size, 0.4, "s_left");
      addWallSegment(span.right, 7.8, span.size, 0.4, "s_right");
    } else {
      addWallSegment(0.0, 7.8, 16.0, 0.4, "s_solid");
    }

    if (W) {
      const span = doorSpan(hallW);
      addWallSegment(-7.8, span.left, 0.4, span.size, "w_top");
      addWallSegment(-7.8, span.right, 0.4, span.size, "w_bottom");
    } else {
      addWallSegment(-7.8, 0.0, 0.4, 16.0, "w_solid");
    }

    if (E) {
      const span = doorSpan(hallE);
      addWallSegment(7.8, span.left, 0.4, span.size, "e_top");
      addWallSegment(7.8, span.right, 0.4, span.size, "e_bottom");
    } else {
      addWallSegment(7.8, 0.0, 0.4, 16.0, "e_solid");
    }

    // Plus-shaped halls keep four enterable corner alcoves. Maze halls skip
    // these separators so a 3.4m school corridor can run through the center.
    const hallLike = type === "corridor_ns" || type === "narrow_ns" || type === "corridor_ew"
      || type === "t_junction" || type === "cross_junction" || type === "start" || type === "dead_end";
    const chicanes = this.getHallChicanes(chunk.cx, chunk.cz);
    const ewChicane = hallLike && chicanes.ew;
    const nsChicane = hallLike && chicanes.ns;
    const maze = ewChicane || nsChicane;
    if (hallLike && !maze) {
      addWallSegment(-1.4, -5.6, 0.4, 4.8, "alcove_nw_ns");
      addWallSegment(-5.6, -1.4, 4.8, 0.4, "alcove_nw_ew");
      addWallSegment(1.4, -5.6, 0.4, 4.8, "alcove_ne_ns");
      addWallSegment(5.6, -1.4, 4.8, 0.4, "alcove_ne_ew");
      addWallSegment(-1.4, 5.6, 0.4, 4.8, "alcove_sw_ns");
      addWallSegment(-5.6, 1.4, 4.8, 0.4, "alcove_sw_ew");
      addWallSegment(1.4, 5.6, 0.4, 4.8, "alcove_se_ns");
      addWallSegment(5.6, 1.4, 4.8, 0.4, "alcove_se_ew");
    } else if (type === "tatami_room" || type === "pillar_room") {
      // Traditional Japanese Tatami Room: Architectural corner posts & alcove wall
      addWallSegment(-7.2, -7.2, 0.8, 0.8, "tatami_post_nw");
      addWallSegment(7.2, -7.2, 0.8, 0.8, "tatami_post_ne");
      addWallSegment(-7.2, 7.2, 0.8, 0.8, "tatami_post_sw");
      addWallSegment(7.2, 7.2, 0.8, 0.8, "tatami_post_se");
      addWallSegment(-7.3, 0.0, 0.6, 0.8, "tatami_post_w");
      addWallSegment(7.3, 0.0, 0.6, 0.8, "tatami_post_e");
      addWallSegment(0.0, 6.8, 4.0, 0.4, "tatami_tokonoma_wall");
    } else if (type === "corner") {
      addWallSegment(1.4, 4.6, 0.4, 6.8, "corner_inner_s");
      addWallSegment(4.6, 1.4, 6.8, 0.4, "corner_inner_e");
      addWallSegment(-1.4, 3.3, 0.4, 9.4, "corner_outer_w");
      addWallSegment(3.3, -1.4, 9.4, 0.4, "corner_outer_n");
    } else if (type === "toy_storage" || type === "storage") {
      addWallSegment(-3.5, -2.0, 4.5, 0.4, "shelf_partition_nw");
      addWallSegment(-3.5, 2.0, 4.5, 0.4, "shelf_partition_sw");
      addWallSegment(3.5, 0.0, 4.5, 0.4, "shelf_partition_e");
    } else if (type === "archive") {
      addWallSegment(-3.6, -2.2, 5.0, 0.6, "archive_shelf_w");
      addWallSegment(3.6, -2.2, 5.0, 0.6, "archive_shelf_e");
      addWallSegment(0.0, 1.5, 2.4, 1.2, "archive_desk");
    } else if (type === "workshop") {
      addWallSegment(-5.0, -2.0, 0.4, 6.0, "nursery_partition_w");
      addWallSegment(5.0, 2.0, 0.4, 6.0, "nursery_partition_e");
    } else if (type === "playroom") {
      addWallSegment(-4.0, -3.0, 0.4, 4.0, "playroom_divider_w");
      addWallSegment(4.0, 3.0, 0.4, 4.0, "playroom_divider_e");
    } else if (type === "event") {
      addWallSegment(-4.8, 0.0, 0.4, 8.0, "event_partition");
    } else if (type === "wide_room" || type === "flicker_room" || type === "omen_room" || type === "static_room" || isClassroomType(type)) {
      addWallSegment(-5.0, -5.0, 1.2, 1.2, "wide_corner_nw");
      addWallSegment(5.0, -5.0, 1.2, 1.2, "wide_corner_ne");
      addWallSegment(-5.0, 5.0, 1.2, 1.2, "wide_corner_sw");
      addWallSegment(5.0, 5.0, 1.2, 1.2, "wide_corner_se");
      if (isClassroomType(type)) {
        addWallSegment(-3.4, -1.8, 2.4, 0.7, "desk_row_w");
        addWallSegment(3.4, 1.6, 2.4, 0.7, "desk_row_e");
      }
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
        position.set(center.x + w.localX, floorY + 1.4, center.z + w.localZ);
        scale.set(w.sizeX, 2.8, w.sizeZ);
        matrix.compose(position, rotation, scale);
        wallInst.setMatrixAt(index, matrix);

        // Trim Matrix
        position.set(center.x + w.localX, floorY + 0.04, center.z + w.localZ);
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

  dressHallLabyrinth(chunk, type, center, chunkId, floorY = 0) {
    const hallLike = type === "corridor_ns" || type === "narrow_ns" || type === "corridor_ew"
      || type === "t_junction" || type === "cross_junction" || type === "start" || type === "dead_end";
    if (!hallLike) return;
    const openings = this.getOpenings(chunk.cx, chunk.cz);
    const wallMat = this.textures.createWallMaterial().clone();
    wallMat.color.setHex(0x2a221c);
    wallMat.emissive = new THREE.Color(0x080604);
    wallMat.emissiveIntensity = 0.04;
    const y = floorY + 1.4;
    const h = 2.8;
    const t = 0.32;
    const skipNW = chunk.cx === 0 && chunk.cz === 0;
    const cabinetSE = (chunk.cx + chunk.cz) % 2 === 0;
    const chicanes = this.getHallChicanes(chunk.cx, chunk.cz);
    const ewChicane = chicanes.ew;
    const nsChicane = chicanes.ns;
    const maze = ewChicane || nsChicane;
    const walls = [];
    const add = (name, x, z, sx, sz, skip = false) => {
      if (!skip) walls.push([name, x, z, sx, sz]);
    };
    if (!maze) {
      add("hall_maze_nw_h", -6.0, -4.6, 1.8, t, skipNW);
      add("hall_maze_nw_v", -6.2, -6.2, t, 1.8, skipNW);
      add("hall_maze_ne_h", 6.0, -4.6, 1.8, t);
      add("hall_maze_ne_v", 6.2, -6.2, t, 1.8);
      add("hall_maze_sw_h", -6.0, 4.6, 1.8, t, !cabinetSE);
      add("hall_maze_sw_v", -6.2, 6.2, t, 1.8, !cabinetSE);
      add("hall_maze_se_h", 6.0, 4.6, 1.8, t, cabinetSE);
      add("hall_maze_se_v", 6.2, 6.2, t, 1.8, cabinetSE);
      if (openings.E && openings.W && !ewChicane) {
        add("hall_maze_teeth_w", -4.2, 0.92, 1.8, t);
        add("hall_maze_teeth_e", 4.2, -0.92, 1.8, t);
      }
      if (openings.N && openings.S && !nsChicane) {
        add("hall_maze_teeth_n", -0.92, -4.2, t, 1.8);
        add("hall_maze_teeth_s", 0.92, 4.2, t, 1.8);
      }
    }
    for (const [name, x, z, sx, sz] of walls) {
      this.placeDressedBox(chunk, chunkId, name, center.x + x, y, center.z + z, sx, h, sz, wallMat);
    }
    if (maze) {
      this.dressHallLockerBanks(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane);
      this.dressSchoolCorridor(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane);
    }
    const gloom = new THREE.PointLight(0x4a3020, 1.15, 5.4, 2.0);
    gloom.position.set(
      center.x + (cabinetSE ? -5.4 : 5.4),
      floorY + 2.05,
      center.z + (skipNW ? 5.2 : -5.2),
    );
    gloom.name = `${chunkId}_hall_school_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    if (maze) {
      const gloom2 = new THREE.PointLight(0x2a1814, 0.82, 4.6, 2.0);
      gloom2.position.set(
        center.x + (cabinetSE ? 5.1 : -5.1),
        floorY + 2.05,
        center.z + (skipNW ? -5.1 : 5.1),
      );
      gloom2.name = `${chunkId}_hall_school_gloom2`;
      this.scene.add(gloom2);
      chunk.meshes.push(gloom2);
    }
  }

  dressOmenRoom(chunk, center, chunkId, floorY) {
    const altarGeo = this.getBoxGeometry(1.35, 0.82, 1.35);
    const altar = new THREE.Mesh(altarGeo, this.propMaterial);
    altar.position.set(center.x, floorY + 0.41, center.z);
    altar.castShadow = true;
    altar.receiveShadow = true;
    altar.name = `${chunkId}_omen_altar`;
    this.scene.add(altar);
    chunk.meshes.push(altar);
    this.collisionWorld.addStaticBox(altar.name, altar.position, new THREE.Vector3(1.35, 0.82, 1.35), chunkId);

    const clothGeo = this.getBoxGeometry(1.5, 0.04, 1.5);
    const clothMat = new THREE.MeshStandardMaterial({
      color: 0x1a090c,
      roughness: 0.92,
      metalness: 0.02,
    });
    const cloth = new THREE.Mesh(clothGeo, clothMat);
    cloth.position.set(center.x, floorY + 0.84, center.z);
    cloth.name = `${chunkId}_omen_cloth`;
    this.scene.add(cloth);
    chunk.meshes.push(cloth);

    const candleMat = new THREE.MeshStandardMaterial({
      color: 0x3a2a18,
      emissive: 0x140804,
      emissiveIntensity: 0.12,
      roughness: 0.7,
    });
    for (let i = 0; i < 6; i += 1) {
      const angle = (i / 6) * Math.PI * 2;
      const candle = new THREE.Mesh(this.getBoxGeometry(0.08, 0.28, 0.08), candleMat);
      candle.position.set(
        center.x + Math.cos(angle) * 0.42,
        floorY + 0.98,
        center.z + Math.sin(angle) * 0.42,
      );
      candle.name = `${chunkId}_omen_candle_${i}`;
      this.scene.add(candle);
      chunk.meshes.push(candle);
    }

    const veilGeo = this.getBoxGeometry(0.06, 2.2, 2.4);
    const veilMat = new THREE.MeshStandardMaterial({
      color: 0x12080a,
      roughness: 0.95,
      transparent: true,
      opacity: 0.55,
    });
    const veil = new THREE.Mesh(veilGeo, veilMat);
    veil.position.set(center.x - 3.6, floorY + 1.15, center.z - 2.4);
    veil.name = `${chunkId}_omen_veil`;
    this.scene.add(veil);
    chunk.meshes.push(veil);
  }

  dressStaticRoom(chunk, center, chunkId, floorY) {
    const setGeo = this.getBoxGeometry(1.1, 0.72, 0.55);
    const setMesh = new THREE.Mesh(setGeo, this.propMaterial);
    setMesh.position.set(center.x, floorY + 0.36, center.z - 4.6);
    setMesh.castShadow = true;
    setMesh.receiveShadow = true;
    setMesh.name = `${chunkId}_static_set`;
    this.scene.add(setMesh);
    chunk.meshes.push(setMesh);
    this.collisionWorld.addStaticBox(setMesh.name, setMesh.position, new THREE.Vector3(1.1, 0.72, 0.55), chunkId);

    const screenMat = new THREE.MeshStandardMaterial({
      color: 0x0b1014,
      emissive: 0x3a5a72,
      emissiveIntensity: 0.55,
      roughness: 0.35,
    });
    const screen = new THREE.Mesh(this.getBoxGeometry(0.82, 0.48, 0.04), screenMat);
    screen.position.set(center.x, floorY + 0.92, center.z - 4.34);
    screen.name = `${chunkId}_static_screen`;
    this.scene.add(screen);
    chunk.meshes.push(screen);
    chunk.lights.push({
      mesh: screen,
      localPos: new THREE.Vector3(0, 0.92, -4.34),
      baseIntensity: 2.4,
      currentIntensity: 2.4,
      isFlickering: true,
      flickerTimer: 0.2,
      voltagePhase: 1.7,
      pooledLight: null,
    });

    const chairGeo = this.getBoxGeometry(0.48, 0.62, 0.48);
    const chair = new THREE.Mesh(chairGeo, this.trimMaterial);
    chair.position.set(center.x + 0.15, floorY + 0.31, center.z - 1.8);
    chair.castShadow = true;
    chair.name = `${chunkId}_static_chair`;
    this.scene.add(chair);
    chunk.meshes.push(chair);
    this.collisionWorld.addStaticBox(chair.name, chair.position, new THREE.Vector3(0.48, 0.62, 0.48), chunkId);
  }

  dressHallLockerBanks(chunk, center, chunkId, floorY, _openings, ewChicane, nsChicane) {
    // Locker rows sit on the inner faces of the 3.4m school corridor.
    // Keep the T-spur (center ±1.7) and alcove doors (x/z ±5.25) clear.
    const cabinetMat = this.textures.createCabinetMaterial();
    if (!this.lockerSlitMaterial) {
      this.lockerSlitMaterial = new THREE.MeshStandardMaterial({
        color: 0x1a1410,
        roughness: 0.92,
        metalness: 0.04,
      });
    }
    const slitMat = this.lockerSlitMaterial;
    const y = floorY + 1.08;
    const h = 2.16;
    const depth = 0.22;
    const bank = 1.7;
    const clear = 1.7;
    const face = clear - depth / 2;
    const along = 2.9;
    const placeBank = (name, lx, lz, sx, sz) => {
      this.placeDressedBox(
        chunk,
        chunkId,
        name,
        center.x + lx,
        y,
        center.z + lz,
        sx,
        h,
        sz,
        cabinetMat,
      );
      const alongWall = sx >= sz ? sx : sz;
      const slits = 4;
      for (let i = 0; i < slits; i += 1) {
        const t = (i + 1) / (slits + 1) - 0.5;
        const slitX = sx >= sz ? lx + t * alongWall : lx;
        const slitZ = sx >= sz ? lz : lz + t * alongWall;
        const slitSx = sx >= sz ? 0.03 : depth + 0.02;
        const slitSz = sx >= sz ? depth + 0.02 : 0.03;
        this.placeDressedBox(
          chunk,
          chunkId,
          `${name}_slit_${i}`,
          center.x + slitX,
          y,
          center.z + slitZ,
          slitSx,
          h * 0.92,
          slitSz,
          slitMat,
          false,
        );
      }
    };
    if (ewChicane) {
      placeBank("hall_lockers_n_w", -along, -face, bank, depth);
      placeBank("hall_lockers_n_e", along, -face, bank, depth);
      placeBank("hall_lockers_s_w", -along, face, bank, depth);
      placeBank("hall_lockers_s_e", along, face, bank, depth);
    }
    if (nsChicane) {
      placeBank("hall_lockers_w_n", -face, -along, depth, bank);
      placeBank("hall_lockers_w_s", -face, along, depth, bank);
      placeBank("hall_lockers_e_n", face, -along, depth, bank);
      placeBank("hall_lockers_e_s", face, along, depth, bank);
    }
  }

  ensureSchoolCorridorMaterials() {
    if (this.schoolClassWallMat) return;
    this.schoolClassWallMat = this.textures.createWallMaterial().clone();
    this.schoolClassWallMat.color.setHex(0x2a221c);
    this.schoolClassDoorMat = this.textures.createClassroomDoorMaterial();
    this.schoolGlassMat = new THREE.MeshStandardMaterial({
      color: 0x07080a,
      roughness: 0.22,
      metalness: 0.18,
      emissive: 0x04060a,
      emissiveIntensity: 0.08,
    });
    this.schoolStripeMat = new THREE.MeshStandardMaterial({
      color: 0x1a120e,
      roughness: 0.95,
      metalness: 0,
    });
    this.schoolFluoroMat = new THREE.MeshStandardMaterial({
      color: 0x3a4038,
      roughness: 0.45,
      metalness: 0.12,
      emissive: 0x1a1810,
      emissiveIntensity: 0.06,
    });
    this.schoolPaperMat = new THREE.MeshStandardMaterial({
      color: 0xc4b090,
      roughness: 0.92,
      metalness: 0,
    });
  }

  dressSchoolCorridor(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane) {
    // 3.4m school corridor through the tile center. Classroom walls at the
    // clear edge, hide-alcove doors at ±5.25, T-spurs when the graph turns.
    this.ensureSchoolCorridorMaterials();
    const clear = 1.7;
    const t = 0.28;
    const wallPos = clear + t / 2;
    const y = floorY + 1.4;
    const h = 2.8;
    const alcove = 5.25;
    const doorW = 2.2;
    const spanMin = -7.45;
    const spanMax = 7.45;
    const stemEnd = 7.52;
    const stemStart = wallPos;
    const stemLen = stemEnd - stemStart;
    const stemMid = (stemStart + stemEnd) / 2;

    const gapped = (name, axis, localPos, localGaps) => {
      if (axis === "x") {
        this.placeGappedWall(chunk, chunkId, name, {
          axis,
          pos: center.z + localPos,
          min: center.x + spanMin,
          max: center.x + spanMax,
          wallY: y,
          height: h,
          thickness: t,
          material: this.schoolClassWallMat,
          gaps: localGaps.map((gap) => ({ center: center.x + gap.center, width: gap.width })),
        });
      } else {
        this.placeGappedWall(chunk, chunkId, name, {
          axis,
          pos: center.x + localPos,
          min: center.z + spanMin,
          max: center.z + spanMax,
          wallY: y,
          height: h,
          thickness: t,
          material: this.schoolClassWallMat,
          gaps: localGaps.map((gap) => ({ center: center.z + gap.center, width: gap.width })),
        });
      }
    };
    const stem = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, y, center.z + z, sx, h, sz,
        this.schoolClassWallMat,
      );
    };

    if (ewChicane) {
      const nGaps = [{ center: -alcove, width: doorW }, { center: alcove, width: doorW }];
      const sGaps = [{ center: -alcove, width: doorW }, { center: alcove, width: doorW }];
      if (nsChicane || openings.N) nGaps.push({ center: 0, width: clear * 2 });
      if (nsChicane || openings.S) sGaps.push({ center: 0, width: clear * 2 });
      gapped("hall_class_n", "x", -wallPos, nGaps);
      gapped("hall_class_s", "x", wallPos, sGaps);
      if (!nsChicane && openings.N) {
        stem("hall_class_stem_n_w", -wallPos, -stemMid, t, stemLen);
        stem("hall_class_stem_n_e", wallPos, -stemMid, t, stemLen);
      }
      if (!nsChicane && openings.S) {
        stem("hall_class_stem_s_w", -wallPos, stemMid, t, stemLen);
        stem("hall_class_stem_s_e", wallPos, stemMid, t, stemLen);
      }
      if (!nsChicane && !openings.N) {
        stem("hall_class_split_n", 0, -stemMid, t, stemLen);
      }
      if (!nsChicane && !openings.S) {
        stem("hall_class_split_s", 0, stemMid, t, stemLen);
      }
    }

    if (nsChicane) {
      const wGaps = [];
      const eGaps = [];
      if (ewChicane || openings.W) wGaps.push({ center: 0, width: clear * 2 });
      if (ewChicane || openings.E) eGaps.push({ center: 0, width: clear * 2 });
      if (!ewChicane) {
        wGaps.push({ center: -alcove, width: doorW }, { center: alcove, width: doorW });
        eGaps.push({ center: -alcove, width: doorW }, { center: alcove, width: doorW });
      }
      gapped("hall_class_w", "z", -wallPos, wGaps);
      gapped("hall_class_e", "z", wallPos, eGaps);
      if (!ewChicane && openings.W) {
        stem("hall_class_stem_w_n", -stemMid, -wallPos, stemLen, t);
        stem("hall_class_stem_w_s", -stemMid, wallPos, stemLen, t);
      }
      if (!ewChicane && openings.E) {
        stem("hall_class_stem_e_n", stemMid, -wallPos, stemLen, t);
        stem("hall_class_stem_e_s", stemMid, wallPos, stemLen, t);
      }
      if (!ewChicane && !openings.W) {
        stem("hall_class_split_w", -stemMid, 0, stemLen, t);
      }
      if (!ewChicane && !openings.E) {
        stem("hall_class_split_e", stemMid, 0, stemLen, t);
      }
    }

    const doorH = 2.15;
    const doorY = floorY + 1.12;
    const panel = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, doorY, center.z + z, sx, doorH, sz,
        this.schoolClassDoorMat, false,
      );
    };
    const glass = (name, x, z, sx, sy, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, floorY + 1.72, center.z + z, sx, sy, sz,
        this.schoolGlassMat, false,
      );
    };
    const winZ = wallPos + 0.03;
    if (ewChicane) {
      panel("hall_class_door_n_w", -alcove, -winZ, 0.9, 0.04);
      panel("hall_class_door_n_e", alcove, -winZ, 0.9, 0.04);
      panel("hall_class_door_s_w", -alcove, winZ, 0.9, 0.04);
      panel("hall_class_door_s_e", alcove, winZ, 0.9, 0.04);
      glass("hall_window_n_w", -2.9, -winZ, 0.28, 0.32, 0.03);
      glass("hall_window_n_e", 2.9, -winZ, 0.28, 0.32, 0.03);
      glass("hall_window_s_w", -2.9, winZ, 0.28, 0.32, 0.03);
      glass("hall_window_s_e", 2.9, winZ, 0.28, 0.32, 0.03);
      this.placeDressedBox(
        chunk, chunkId, "hall_stripe",
        center.x, floorY + 0.012, center.z, 14.6, 0.02, 0.09,
        this.schoolStripeMat, false,
      );
      for (const [name, x] of [["hall_fluoro_w", -4.2], ["hall_fluoro_e", 4.2]]) {
        this.placeDressedBox(
          chunk, chunkId, name,
          center.x + x, floorY + 2.68, center.z, 2.35, 0.05, 0.14,
          this.schoolFluoroMat, false,
        );
      }
      this.placeDressedBox(
        chunk, chunkId, "hall_paper_n",
        center.x + 2.9, floorY + 1.55, center.z - winZ,
        0.42, 0.55, 0.02, this.schoolPaperMat, false,
      );
    }
    if (nsChicane) {
      const winX = wallPos + 0.03;
      if (!ewChicane) {
        panel("hall_class_door_w_n", -winX, -alcove, 0.04, 0.9);
        panel("hall_class_door_w_s", -winX, alcove, 0.04, 0.9);
        panel("hall_class_door_e_n", winX, -alcove, 0.04, 0.9);
        panel("hall_class_door_e_s", winX, alcove, 0.04, 0.9);
      }
      glass("hall_window_w_n", -winX, -2.9, 0.03, 0.32, 0.28);
      glass("hall_window_w_s", -winX, 2.9, 0.03, 0.32, 0.28);
      glass("hall_window_e_n", winX, -2.9, 0.03, 0.32, 0.28);
      glass("hall_window_e_s", winX, 2.9, 0.03, 0.32, 0.28);
      this.placeDressedBox(
        chunk, chunkId, "hall_stripe_ns",
        center.x, floorY + 0.012, center.z, 0.09, 0.02, 14.6,
        this.schoolStripeMat, false,
      );
      for (const [name, z] of [["hall_fluoro_n", -4.2], ["hall_fluoro_s", 4.2]]) {
        this.placeDressedBox(
          chunk, chunkId, name,
          center.x, floorY + 2.68, center.z + z, 0.14, 0.05, 2.35,
          this.schoolFluoroMat, false,
        );
      }
    }
  }

  dressClassroom(chunk, center, chunkId, floorY) {
    const type = this.getChunkType(chunk.cx, chunk.cz);
    if (type === "nurse_office") {
      this.dressNurseOffice(chunk, center, chunkId, floorY);
      return;
    }
    if (type === "music_room") {
      this.dressMusicRoom(chunk, center, chunkId, floorY);
      return;
    }
    if (type === "faculty_office") {
      this.dressFacultyOffice(chunk, center, chunkId, floorY);
      return;
    }
    if (type === "science_lab") {
      this.dressScienceLab(chunk, center, chunkId, floorY);
      return;
    }
    const open = this.getOpenings(chunk.cx, chunk.cz);
    const doorFace = open.N ? "N" : open.S ? "S" : open.E ? "E" : "W";
    const boardMat = new THREE.MeshStandardMaterial({
      color: 0x1a2a1c,
      roughness: 0.88,
      metalness: 0.04,
    });
    const board = new THREE.Mesh(this.getBoxGeometry(3.6, 1.15, 0.08), boardMat);
    const podium = new THREE.Mesh(this.getBoxGeometry(0.9, 0.78, 0.55), this.propMaterial);
    if (doorFace === "S") {
      board.position.set(center.x, floorY + 1.55, center.z - 7.52);
      podium.position.set(center.x - 2.2, floorY + 0.39, center.z - 5.1);
    } else if (doorFace === "N") {
      board.position.set(center.x, floorY + 1.55, center.z + 7.52);
      podium.position.set(center.x + 2.2, floorY + 0.39, center.z + 5.1);
    } else if (doorFace === "E") {
      board.rotation.y = Math.PI / 2;
      board.position.set(center.x - 7.52, floorY + 1.55, center.z);
      podium.position.set(center.x - 5.1, floorY + 0.39, center.z - 2.2);
    } else {
      board.rotation.y = Math.PI / 2;
      board.position.set(center.x + 7.52, floorY + 1.55, center.z);
      podium.position.set(center.x + 5.1, floorY + 0.39, center.z + 2.2);
    }
    board.name = `${chunkId}_chalkboard`;
    this.scene.add(board);
    chunk.meshes.push(board);

    podium.castShadow = true;
    podium.receiveShadow = true;
    podium.name = `${chunkId}_podium`;
    this.scene.add(podium);
    chunk.meshes.push(podium);
    this.collisionWorld.addStaticBox(podium.name, podium.position, new THREE.Vector3(0.9, 0.78, 0.55), chunkId);

    const deskGeo = this.getBoxGeometry(0.62, 0.72, 0.48);
    const chairGeo = this.getBoxGeometry(0.36, 0.46, 0.36);
    let deskIndex = 0;
    for (let row = 0; row < 3; row += 1) {
      for (let col = 0; col < 3; col += 1) {
        const u = (col - 1) * 1.7;
        const v = (row - 0.35) * 1.55;
        let x = center.x + u;
        let z = center.z + v;
        if (doorFace === "N" || doorFace === "S") {
          z = doorFace === "S" ? center.z - 0.4 + row * 1.55 : center.z + 0.4 - row * 1.55;
          x = center.x + u;
        } else {
          x = doorFace === "E" ? center.x - 0.4 + row * 1.55 : center.x + 0.4 - row * 1.55;
          z = center.z + u;
        }
        const desk = new THREE.Mesh(deskGeo, this.propMaterial);
        desk.position.set(x, floorY + 0.36, z);
        desk.castShadow = true;
        desk.receiveShadow = true;
        desk.name = `${chunkId}_desk_${deskIndex}`;
        this.scene.add(desk);
        chunk.meshes.push(desk);
        this.collisionWorld.addStaticBox(desk.name, desk.position, new THREE.Vector3(0.62, 0.72, 0.48), chunkId);

        const chair = new THREE.Mesh(chairGeo, this.trimMaterial);
        const chairBack = doorFace === "S" ? 0.42 : doorFace === "N" ? -0.42 : 0;
        const chairSide = doorFace === "E" ? 0.42 : doorFace === "W" ? -0.42 : 0;
        chair.position.set(x + chairSide, floorY + 0.23, z + chairBack);
        chair.name = `${chunkId}_chair_${deskIndex}`;
        this.scene.add(chair);
        chunk.meshes.push(chair);
        this.collisionWorld.addStaticBox(chair.name, chair.position, new THREE.Vector3(0.36, 0.46, 0.36), chunkId);
        deskIndex += 1;
      }
    }

    const cubby = new THREE.Mesh(this.getBoxGeometry(2.4, 1.15, 0.42), this.trimMaterial);
    cubby.name = `${chunkId}_shoe_cubby`;
    if (doorFace === "S" || doorFace === "N") {
      cubby.position.set(center.x + 6.4, floorY + 0.58, center.z);
      this.collisionWorld.addStaticBox(cubby.name, cubby.position, new THREE.Vector3(2.4, 1.15, 0.42), chunkId);
    } else {
      cubby.rotation.y = Math.PI / 2;
      cubby.position.set(center.x, floorY + 0.58, center.z + 6.4);
      this.collisionWorld.addStaticBox(cubby.name, cubby.position, new THREE.Vector3(0.42, 1.15, 2.4), chunkId);
    }
    this.scene.add(cubby);
    chunk.meshes.push(cubby);
  }

  placeDressedBox(chunk, chunkId, name, x, y, z, sx, sy, sz, material, collide = true) {
    const mesh = new THREE.Mesh(this.getBoxGeometry(sx, sy, sz), material);
    mesh.position.set(x, y, z);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.name = `${chunkId}_${name}`;
    this.scene.add(mesh);
    chunk.meshes.push(mesh);
    if (collide) {
      this.collisionWorld.addStaticBox(mesh.name, mesh.position, new THREE.Vector3(sx, sy, sz), chunkId);
    }
    return mesh;
  }

  dressNurseOffice(chunk, center, chunkId, floorY) {
    const linen = new THREE.MeshStandardMaterial({ color: 0xd8c8b0, roughness: 0.9 });
    const steel = new THREE.MeshStandardMaterial({ color: 0x6a7674, roughness: 0.42, metalness: 0.35 });
    const curtain = new THREE.MeshStandardMaterial({
      color: 0x5a3040,
      roughness: 0.92,
      transparent: true,
      opacity: 0.72,
    });
    this.placeDressedBox(chunk, chunkId, "nurse_bed", center.x + 2.1, floorY + 0.32, center.z + 2.4, 1.9, 0.64, 0.86, this.propMaterial);
    this.placeDressedBox(chunk, chunkId, "nurse_sheet", center.x + 2.1, floorY + 0.66, center.z + 2.4, 1.82, 0.06, 0.8, linen, false);
    this.placeDressedBox(chunk, chunkId, "nurse_cabinet", center.x - 5.6, floorY + 0.7, center.z, 0.42, 1.4, 2.2, steel);
    this.placeDressedBox(chunk, chunkId, "nurse_desk", center.x - 2.4, floorY + 0.38, center.z - 4.4, 1.35, 0.76, 0.7, this.propMaterial);
    this.placeDressedBox(chunk, chunkId, "nurse_curtain", center.x + 0.4, floorY + 1.15, center.z + 1.1, 0.06, 2.2, 2.4, curtain, false);
  }

  dressMusicRoom(chunk, center, chunkId, floorY) {
    const lacquer = new THREE.MeshStandardMaterial({
      color: 0x1a120e,
      roughness: 0.38,
      metalness: 0.08,
      emissive: 0x120806,
      emissiveIntensity: 0.08,
    });
    const ivory = new THREE.MeshStandardMaterial({ color: 0xe8dcc4, roughness: 0.55 });
    this.placeDressedBox(chunk, chunkId, "piano", center.x + 3.4, floorY + 0.46, center.z, 1.55, 0.92, 0.62, lacquer);
    this.placeDressedBox(chunk, chunkId, "piano_lid", center.x + 3.4, floorY + 0.98, center.z - 0.08, 1.48, 0.06, 0.52, lacquer, false);
    this.placeDressedBox(chunk, chunkId, "piano_keys", center.x + 2.72, floorY + 0.72, center.z, 0.22, 0.04, 0.5, ivory, false);
    this.placeDressedBox(chunk, chunkId, "music_stool", center.x + 2.1, floorY + 0.28, center.z, 0.42, 0.56, 0.42, this.trimMaterial);
    this.placeDressedBox(chunk, chunkId, "music_stand_a", center.x - 2.6, floorY + 0.72, center.z - 2.8, 0.12, 1.44, 0.12, this.trimMaterial);
    this.placeDressedBox(chunk, chunkId, "music_stand_b", center.x - 3.4, floorY + 0.72, center.z + 1.6, 0.12, 1.44, 0.12, this.trimMaterial);
  }

  dressFacultyOffice(chunk, center, chunkId, floorY) {
    const paper = new THREE.MeshStandardMaterial({ color: 0xe4d4b8, roughness: 0.92 });
    this.placeDressedBox(chunk, chunkId, "faculty_desk", center.x - 3.2, floorY + 0.38, center.z, 1.7, 0.76, 0.86, this.propMaterial);
    this.placeDressedBox(chunk, chunkId, "faculty_desk_b", center.x + 3.4, floorY + 0.38, center.z + 2.6, 1.55, 0.76, 0.8, this.propMaterial);
    this.placeDressedBox(chunk, chunkId, "faculty_file", center.x + 5.8, floorY + 0.7, center.z - 2.2, 0.46, 1.4, 1.6, this.trimMaterial);
    this.placeDressedBox(chunk, chunkId, "faculty_file_b", center.x - 5.8, floorY + 0.7, center.z + 2.4, 0.46, 1.4, 1.6, this.trimMaterial);
    this.placeDressedBox(chunk, chunkId, "faculty_papers", center.x - 3.2, floorY + 0.8, center.z, 0.62, 0.04, 0.42, paper, false);
  }

  dressScienceLab(chunk, center, chunkId, floorY) {
    const bench = new THREE.MeshStandardMaterial({ color: 0x3a2a1c, roughness: 0.82 });
    const glass = new THREE.MeshStandardMaterial({
      color: 0x8ab8a4,
      roughness: 0.18,
      metalness: 0.12,
      transparent: true,
      opacity: 0.55,
      emissive: 0x143028,
      emissiveIntensity: 0.12,
    });
    const pipe = new THREE.MeshStandardMaterial({ color: 0x2a2420, roughness: 0.55, metalness: 0.4 });
    this.placeDressedBox(chunk, chunkId, "lab_bench", center.x - 3.4, floorY + 0.46, center.z, 2.4, 0.92, 0.72, bench);
    this.placeDressedBox(chunk, chunkId, "lab_bench_b", center.x + 3.4, floorY + 0.46, center.z, 2.4, 0.92, 0.72, bench);
    this.placeDressedBox(chunk, chunkId, "lab_bottle_a", center.x - 3.1, floorY + 1.08, center.z - 0.12, 0.12, 0.28, 0.12, glass, false);
    this.placeDressedBox(chunk, chunkId, "lab_bottle_b", center.x - 2.7, floorY + 1.12, center.z + 0.16, 0.1, 0.36, 0.1, glass, false);
    this.placeDressedBox(chunk, chunkId, "lab_bottle_c", center.x + 3.6, floorY + 1.08, center.z, 0.12, 0.28, 0.12, glass, false);
    this.placeDressedBox(chunk, chunkId, "lab_pipe", center.x, floorY + 2.42, center.z, 8.4, 0.1, 0.1, pipe, false);
  }

  linkTransitionWaypoint(id, extraId) {
    const waypoint = this.collisionWorld.transitionWaypoints.find((item) => item.id === id);
    if (waypoint && Array.isArray(waypoint.links) && !waypoint.links.includes(extraId)) {
      waypoint.links.push(extraId);
    }
  }

  placeGappedWall(chunk, chunkId, name, spec) {
    const { axis, pos, min, max, wallY, height, thickness, material, gaps = [] } = spec;
    const sorted = [...gaps].sort((a, b) => a.center - b.center);
    const segments = [];
    let cursor = min;
    for (const gap of sorted) {
      const g0 = gap.center - gap.width / 2;
      const g1 = gap.center + gap.width / 2;
      if (g0 > cursor + 0.28) segments.push([cursor, Math.min(g0, max)]);
      cursor = Math.max(cursor, g1);
    }
    if (cursor < max - 0.28) segments.push([cursor, max]);
    segments.forEach(([start, end], index) => {
      const len = end - start;
      if (len < 0.34) return;
      const mid = (start + end) / 2;
      if (axis === "x") {
        this.placeDressedBox(chunk, chunkId, `${name}_${index}`, mid, wallY, pos, len, height, thickness, material);
      } else {
        this.placeDressedBox(chunk, chunkId, `${name}_${index}`, pos, wallY, mid, thickness, height, len, material);
      }
    });
  }

  addWingPlane(chunk, chunkId, name, width, length, x, y, z, material, flipCeiling = false) {
    const mesh = new THREE.Mesh(this.getPlaneGeometry(width, length), material);
    mesh.rotation.x = flipCeiling ? Math.PI / 2 : -Math.PI / 2;
    mesh.position.set(x, y, z);
    mesh.receiveShadow = true;
    mesh.name = `${chunkId}_${name}`;
    this.scene.add(mesh);
    chunk.meshes.push(mesh);
    return mesh;
  }

  dressBasementMaze(chunk, chunkId, floorY, _bounds) {
    const wallMat = new THREE.MeshStandardMaterial({
      map: this.textures.load("wall"),
      color: 0x24352e,
      roughness: 0.92,
      metalness: 0.05,
      emissive: 0x071410,
      emissiveIntensity: 0.1,
    });
    const y = floorY - 3.6;
    const h = 2.8;
    const t = 0.32;
    // Keep landing door (x≈14.8, z 36.8–39.2), inner crib door (x=1.2, z 28.8–31.2),
    // nursery/key, and existing hide spots clear. Gaps are 2.6m+ for the player radius.
    const walls = [
      ["b1_maze_west_a", 4.55, 33.2, 4.7, t],
      ["b1_maze_west_b", 11.75, 33.2, 4.1, t],
      ["b1_maze_west_spur", 5.4, 26.05, t, 3.1],
      ["b1_maze_west_n", 11.35, 28.2, 4.3, t],
      ["b1_maze_nursery_a", -5.8, 32.4, 3.2, t],
      ["b1_maze_nursery_b", -5.8, 25.65, t, 2.3],
      ["b1_maze_east_a", 18.6, 34.0, 1.2, t],
      ["b1_maze_east_b", 23.5, 34.0, 3.4, t],
      ["b1_maze_east_spur", 21.2, 26.4, t, 3.6],
      ["b1_maze_mid_n", 5.55, 36.05, 2.3, t],
      ["b1_maze_nook", 3.15, 25.35, 1.9, t],
      ["b1_maze_east_nook", 18.35, 27.15, t, 2.2],
    ];
    for (const [name, x, z, sx, sz] of walls) {
      this.placeDressedBox(chunk, chunkId, name, x, y, z, sx, h, sz, wallMat);
    }

    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_maze_gap",
      position: [8.4, floorY - 5.0, 33.2],
      floor: -1,
      links: ["tw_b1_cellar_hall", "tw_b1_inner_door"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_east_gap",
      position: [20.5, floorY - 5.0, 34.0],
      floor: -1,
      links: ["tw_b1_east_door", "tw_b1_east_store"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_east_boiler",
      position: [22.4, floorY - 5.0, 26.8],
      floor: -1,
      links: ["tw_b1_east_store"],
    }, chunkId);
    this.linkTransitionWaypoint("tw_b1_cellar_hall", "tw_b1_maze_gap");
    this.linkTransitionWaypoint("tw_b1_inner_door", "tw_b1_maze_gap");
    this.linkTransitionWaypoint("tw_b1_east_door", "tw_b1_east_gap");
    this.linkTransitionWaypoint("tw_b1_east_store", "tw_b1_east_gap");
    this.linkTransitionWaypoint("tw_b1_east_store", "tw_b1_east_boiler");
  }

  dressBasementLabyrinth(chunk, chunkId, floorY, wallMat, floorMat, ceilMat) {
    const y = floorY - 3.6;
    const h = 2.8;
    const t = 0.32;
    const westMinX = -24.2;
    const westMaxX = -8.0;
    const southMinZ = 39.8;
    const southMaxZ = 56.4;
    const coreMinZ = 24.2;
    const coreMaxX = 26.0;
    const eastMaxX = 44.0;

    this.addWingPlane(
      chunk, chunkId, "cellar_b1_floor_west_lab",
      westMaxX - westMinX, southMaxZ - coreMinZ,
      (westMinX + westMaxX) / 2, floorY - 5.0, (coreMinZ + southMaxZ) / 2, floorMat,
    );
    this.addWingPlane(
      chunk, chunkId, "cellar_b1_ceil_west_lab",
      westMaxX - westMinX, southMaxZ - coreMinZ,
      (westMinX + westMaxX) / 2, floorY - 2.2, (coreMinZ + southMaxZ) / 2, ceilMat, true,
    );
    this.addWingPlane(
      chunk, chunkId, "cellar_b1_floor_south_lab",
      coreMaxX - westMaxX, southMaxZ - southMinZ,
      (westMaxX + coreMaxX) / 2, floorY - 5.0, (southMinZ + southMaxZ) / 2, floorMat,
    );
    this.addWingPlane(
      chunk, chunkId, "cellar_b1_ceil_south_lab",
      coreMaxX - westMaxX, southMaxZ - southMinZ,
      (westMaxX + coreMaxX) / 2, floorY - 2.2, (southMinZ + southMaxZ) / 2, ceilMat, true,
    );

    this.collisionWorld.addFloorArea({
      id: "cellar_b1_west_lab",
      floor: -1,
      type: "walkable",
      y: floorY - 5.0,
      minX: westMinX,
      maxX: westMaxX,
      minZ: coreMinZ,
      maxZ: southMaxZ,
    }, chunkId);
    this.collisionWorld.addFloorArea({
      id: "cellar_b1_south_lab",
      floor: -1,
      type: "walkable",
      y: floorY - 5.0,
      minX: westMaxX,
      maxX: coreMaxX,
      minZ: southMinZ,
      maxZ: southMaxZ,
    }, chunkId);
    this.addWingPlane(
      chunk, chunkId, "cellar_b1_floor_east_lab",
      eastMaxX - coreMaxX, southMaxZ - coreMinZ,
      (coreMaxX + eastMaxX) / 2, floorY - 5.0, (coreMinZ + southMaxZ) / 2, floorMat,
    );
    this.addWingPlane(
      chunk, chunkId, "cellar_b1_ceil_east_lab",
      eastMaxX - coreMaxX, southMaxZ - coreMinZ,
      (coreMaxX + eastMaxX) / 2, floorY - 2.2, (coreMinZ + southMaxZ) / 2, ceilMat, true,
    );
    this.collisionWorld.addFloorArea({
      id: "cellar_b1_east_lab",
      floor: -1,
      type: "walkable",
      y: floorY - 5.0,
      minX: coreMaxX,
      maxX: eastMaxX,
      minZ: coreMinZ,
      maxZ: southMaxZ,
    }, chunkId);

    this.placeDressedBox(chunk, chunkId, "cellar_b1_wall_lab_w", westMinX - 0.15, y, (coreMinZ + southMaxZ) / 2, 0.4, h, southMaxZ - coreMinZ + 0.4, wallMat);
    this.placeDressedBox(chunk, chunkId, "cellar_b1_wall_lab_n", (westMinX + westMaxX) / 2, y, coreMinZ - 0.15, westMaxX - westMinX + 0.4, h, 0.4, wallMat);
    this.placeDressedBox(chunk, chunkId, "cellar_b1_wall_lab_n_e", (coreMaxX + eastMaxX) / 2, y, coreMinZ - 0.15, eastMaxX - coreMaxX + 0.4, h, 0.4, wallMat);
    this.placeDressedBox(chunk, chunkId, "cellar_b1_wall_lab_s", (westMinX + eastMaxX) / 2, y, southMaxZ + 0.15, eastMaxX - westMinX + 0.4, h, 0.4, wallMat);
    this.placeDressedBox(chunk, chunkId, "cellar_b1_wall_lab_e_outer", eastMaxX + 0.15, y, (coreMinZ + southMaxZ) / 2, 0.4, h, southMaxZ - coreMinZ + 0.4, wallMat);
    this.placeGappedWall(chunk, chunkId, "cellar_b1_wall_lab_e_gate", {
      axis: "z",
      pos: coreMaxX + 0.15,
      min: southMinZ,
      max: southMaxZ,
      wallY: y,
      height: h,
      thickness: 0.4,
      material: wallMat,
      gaps: [{ center: 48.0, width: 2.7 }],
    });

    const walls = [
      ["b1_maze_s_h1w", -0.2, 45.1, 14.8, t],
      ["b1_maze_s_h1e", 14.55, 45.1, 10.3, t],
      ["b1_maze_s_h1ee", 23.55, 45.1, 4.1, t],
      ["b1_maze_s_h2w", -0.2, 51.4, 14.8, t],
      ["b1_maze_s_h2e", 15.85, 51.4, 12.7, t],
      ["b1_maze_s_v_dead", 4.15, 54.4, t, 3.4],
      ["b1_maze_s_v_mid", 12.35, 48.25, t, 5.6],
      ["b1_maze_w_h1a", -19.85, 33.6, 7.9, t],
      ["b1_maze_w_h1b", -10.55, 33.6, 4.7, t],
      ["b1_maze_w_spur", -18.85, 27.05, t, 4.5],
      ["b1_maze_w_h2a", -19.85, 45.1, 7.9, t],
      ["b1_maze_w_h2b", -10.55, 45.1, 4.7, t],
      ["b1_maze_w_south_a", -20.4, 51.4, 6.4, t],
      ["b1_maze_w_south_b", -10.5, 51.4, 5.0, t],
      ["b1_maze_e_h1a", 28.55, 35.8, 4.3, t],
      ["b1_maze_e_h1b", 38.7, 35.8, 10.6, t],
      ["b1_maze_e_h2a", 28.55, 44.6, 4.3, t],
      ["b1_maze_e_h2b", 38.7, 44.6, 10.6, t],
      ["b1_maze_e_h3a", 28.55, 50.8, 4.3, t],
      ["b1_maze_e_h3b", 38.7, 50.8, 10.6, t],
      ["b1_maze_e_spur", 36.4, 27.2, t, 3.8],
      ["b1_maze_e_v_loop", 36.4, 40.2, t, 7.6],
      ["b1_maze_e_v_dead", 40.2, 52.6, t, 5.2],
    ];
    for (const [name, x, z, sx, sz] of walls) {
      this.placeDressedBox(chunk, chunkId, name, x, y, z, sx, h, sz, wallMat);
    }

    const waterY = floorY - 5.0 + 0.15;
    const waterMat = new THREE.MeshStandardMaterial({
      color: 0x2d7a68,
      roughness: 0.32,
      metalness: 0.12,
      transparent: true,
      opacity: 0.82,
      emissive: 0x145a48,
      emissiveIntensity: 0.55,
      depthWrite: false,
    });
    this.addWingPlane(
      chunk, chunkId, "flood_water_west_lab",
      westMaxX - westMinX - 0.4, southMaxZ - coreMinZ - 0.4,
      (westMinX + westMaxX) / 2, waterY, (coreMinZ + southMaxZ) / 2, waterMat,
    );
    this.addWingPlane(
      chunk, chunkId, "flood_water_south_lab",
      coreMaxX - westMaxX - 0.4, southMaxZ - southMinZ - 0.4,
      (westMaxX + coreMaxX) / 2, waterY, (southMinZ + southMaxZ) / 2, waterMat,
    );
    this.addWingPlane(
      chunk, chunkId, "flood_water_east_lab",
      eastMaxX - coreMaxX - 0.4, southMaxZ - coreMinZ - 0.4,
      (coreMaxX + eastMaxX) / 2, waterY, (coreMinZ + southMaxZ) / 2, waterMat,
    );

    const gloom = new THREE.PointLight(0x1c4a3c, 2.4, 8.4, 1.7);
    gloom.position.set(8.4, floorY - 4.05, 48.2);
    gloom.name = `${chunkId}_b1_lab_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    const gloomWest = new THREE.PointLight(0x16382e, 2.1, 7.6, 1.7);
    gloomWest.position.set(-16.2, floorY - 4.05, 30.2);
    gloomWest.name = `${chunkId}_b1_lab_gloom_w`;
    this.scene.add(gloomWest);
    chunk.meshes.push(gloomWest);
    const gloomEast = new THREE.PointLight(0x1a4034, 2.2, 8.0, 1.7);
    gloomEast.position.set(32.0, floorY - 4.05, 38.0);
    gloomEast.name = `${chunkId}_b1_lab_gloom_east`;
    this.scene.add(gloomEast);
    chunk.meshes.push(gloomEast);

    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_south_gate",
      position: [8.4, floorY - 5.0, 40.4],
      floor: -1,
      links: ["tw_b1_maze_gap", "tw_b1_south_end"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_south_end",
      position: [8.4, floorY - 5.0, 54.0],
      floor: -1,
      links: ["tw_b1_south_gate"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_west_gate",
      position: [-8.0, floorY - 5.0, 30.0],
      floor: -1,
      links: ["tw_b1_inner_door", "tw_b1_west_end"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_west_end",
      position: [-20.5, floorY - 5.0, 26.8],
      floor: -1,
      links: ["tw_b1_west_gate"],
    }, chunkId);
    this.linkTransitionWaypoint("tw_b1_maze_gap", "tw_b1_south_gate");
    this.linkTransitionWaypoint("tw_b1_inner_door", "tw_b1_west_gate");
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_east_gate",
      position: [26.2, floorY - 5.0, 38.0],
      floor: -1,
      links: ["tw_b1_east_door", "tw_b1_east_end"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_b1_east_end",
      position: [32.0, floorY - 5.0, 48.0],
      floor: -1,
      links: ["tw_b1_east_gate", "tw_b1_south_end"],
    }, chunkId);
    this.linkTransitionWaypoint("tw_b1_east_door", "tw_b1_east_gate");
  }

  dressGalleryMaze(chunk, chunkId, floorY, _bounds) {
    const wallMat = new THREE.MeshStandardMaterial({
      map: this.textures.load("wall"),
      color: 0x6a2024,
      roughness: 0.82,
      metalness: 0.04,
      emissive: 0x2a0608,
      emissiveIntensity: 0.2,
    });
    const y = floorY + 6.4;
    const h = 2.8;
    const t = 0.32;
    // Keep landing door (x≈-17.2, z -23.0–-20.6), shrine door (x=-27.5, z -23.2–-20.8),
    // shrine path at z≈-22, key, and hide spots clear.
    const walls = [
      ["gallery_maze_south_a", -25.0, -16.2, 2.4, t],
      ["gallery_maze_south_b", -19.8, -16.2, 2.8, t],
      ["gallery_maze_north_spur", -23.6, -32.2, t, 6.8],
      ["gallery_maze_shrine_a", -35.7, -26.6, 3.4, t],
      ["gallery_maze_shrine_b", -29.6, -26.6, 2.8, t],
      ["gallery_maze_shrine_south", -31.6, -15.7, t, 4.6],
      ["gallery_maze_mid_nook", -20.4, -28.4, 2.2, t],
      ["gallery_maze_south_nook", -26.8, -11.2, t, 2.4],
    ];
    for (const [name, x, z, sx, sz] of walls) {
      this.placeDressedBox(chunk, chunkId, name, x, y, z, sx, h, sz, wallMat);
    }

    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_maze_south_gap",
      position: [-22.5, floorY + 5.0, -16.2],
      floor: 2,
      links: ["tw_2f_gallery_center", "tw_2f_gallery_south"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_maze_north_gap",
      position: [-32.5, floorY + 5.0, -26.6],
      floor: 2,
      links: ["tw_2f_gallery_altar", "tw_2f_gallery_north"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_shrine_south",
      position: [-32.5, floorY + 5.0, -11.2],
      floor: 2,
      links: ["tw_2f_gallery_inner_door"],
    }, chunkId);
    this.linkTransitionWaypoint("tw_2f_gallery_center", "tw_2f_maze_south_gap");
    this.linkTransitionWaypoint("tw_2f_gallery_south", "tw_2f_maze_south_gap");
    this.linkTransitionWaypoint("tw_2f_gallery_altar", "tw_2f_maze_north_gap");
    this.linkTransitionWaypoint("tw_2f_gallery_north", "tw_2f_maze_north_gap");
    this.linkTransitionWaypoint("tw_2f_gallery_inner_door", "tw_2f_shrine_south");
  }

  dressGalleryLabyrinth(chunk, chunkId, floorY, wallMat, ceilingMat) {
    const y = floorY + 6.4;
    const h = 2.8;
    const t = 0.32;
    const westMinX = -54.0;
    const westMaxX = -38.0;
    const northMinZ = -54.0;
    const northMaxZ = -36.0;
    const galMaxZ = -8.5;
    const galMaxX = -17.2;
    const floorMat = this.textures.createFloorMaterial(16, 16);
    floorMat.color.setHex(0x4a181c);
    floorMat.emissive = new THREE.Color(0x28060a);
    floorMat.emissiveIntensity = 0.14;
    floorMat.roughness = 0.36;
    const mazeMat = wallMat.clone();
    mazeMat.color.setHex(0x6a2024);
    mazeMat.emissive = new THREE.Color(0x2a0608);
    mazeMat.emissiveIntensity = 0.2;

    this.addWingPlane(
      chunk, chunkId, "gallery_2f_floor_west_lab",
      westMaxX - westMinX, galMaxZ - northMinZ,
      (westMinX + westMaxX) / 2, floorY + 5.0, (northMinZ + galMaxZ) / 2, floorMat,
    );
    this.addWingPlane(
      chunk, chunkId, "gallery_2f_ceil_west_lab",
      westMaxX - westMinX, galMaxZ - northMinZ,
      (westMinX + westMaxX) / 2, floorY + 7.8, (northMinZ + galMaxZ) / 2, ceilingMat, true,
    );
    this.addWingPlane(
      chunk, chunkId, "gallery_2f_floor_north_lab",
      galMaxX - westMaxX, northMaxZ - northMinZ,
      (westMaxX + galMaxX) / 2, floorY + 5.0, (northMinZ + northMaxZ) / 2, floorMat,
    );
    this.addWingPlane(
      chunk, chunkId, "gallery_2f_ceil_north_lab",
      galMaxX - westMaxX, northMaxZ - northMinZ,
      (westMaxX + galMaxX) / 2, floorY + 7.8, (northMinZ + northMaxZ) / 2, ceilingMat, true,
    );

    this.collisionWorld.addFloorArea({
      id: "gallery_2f_west_lab",
      floor: 2,
      type: "walkable",
      y: floorY + 5.0,
      minX: westMinX,
      maxX: westMaxX,
      minZ: northMinZ,
      maxZ: galMaxZ,
    }, chunkId);
    this.collisionWorld.addFloorArea({
      id: "gallery_2f_north_lab",
      floor: 2,
      type: "walkable",
      y: floorY + 5.0,
      minX: westMaxX,
      maxX: galMaxX,
      minZ: northMinZ,
      maxZ: northMaxZ,
    }, chunkId);

    const southMinZ = galMaxZ;
    const southMaxZ = 8.0;

    this.addWingPlane(
      chunk, chunkId, "gallery_2f_floor_south_lab",
      galMaxX - westMinX, southMaxZ - southMinZ,
      (westMinX + galMaxX) / 2, floorY + 5.0, (southMinZ + southMaxZ) / 2, floorMat,
    );
    this.addWingPlane(
      chunk, chunkId, "gallery_2f_ceil_south_lab",
      galMaxX - westMinX, southMaxZ - southMinZ,
      (westMinX + galMaxX) / 2, floorY + 7.8, (southMinZ + southMaxZ) / 2, ceilingMat, true,
    );
    this.collisionWorld.addFloorArea({
      id: "gallery_2f_south_lab",
      floor: 2,
      type: "walkable",
      y: floorY + 5.0,
      minX: westMinX,
      maxX: galMaxX,
      minZ: southMinZ,
      maxZ: southMaxZ,
    }, chunkId);

    this.placeDressedBox(chunk, chunkId, "gallery_2f_wall_lab_w", westMinX - 0.15, y, (northMinZ + galMaxZ) / 2, 0.4, h, galMaxZ - northMinZ + 0.4, mazeMat);
    this.placeDressedBox(chunk, chunkId, "gallery_2f_wall_lab_n", (westMinX + galMaxX) / 2, y, northMinZ - 0.15, galMaxX - westMinX + 0.4, h, 0.4, mazeMat);
    this.placeGappedWall(chunk, chunkId, "gallery_2f_wall_lab_s", {
      axis: "x",
      pos: galMaxZ + 0.15,
      min: westMinX,
      max: westMaxX,
      wallY: y,
      height: h,
      thickness: 0.4,
      material: mazeMat,
      gaps: [{ center: -46.0, width: 2.7 }],
    });
    this.placeDressedBox(chunk, chunkId, "gallery_2f_wall_lab_e", galMaxX + 0.15, y, (northMinZ + northMaxZ) / 2, 0.4, h, northMaxZ - northMinZ + 0.4, mazeMat);
    this.placeDressedBox(chunk, chunkId, "gallery_2f_wall_lab_s_outer", (westMinX + galMaxX) / 2, y, southMaxZ + 0.15, galMaxX - westMinX + 0.4, h, 0.4, mazeMat);
    this.placeDressedBox(chunk, chunkId, "gallery_2f_wall_lab_s_w", westMinX - 0.15, y, (southMinZ + southMaxZ) / 2, 0.4, h, southMaxZ - southMinZ + 0.4, mazeMat);
    this.placeDressedBox(chunk, chunkId, "gallery_2f_wall_lab_s_e", galMaxX + 0.15, y, (southMinZ + southMaxZ) / 2, 0.4, h, southMaxZ - southMinZ + 0.4, mazeMat);

    const walls = [
      ["gallery_maze_n_h1w", -35.7, -40.6, 3.6, t],
      ["gallery_maze_n_h1e", -27.6, -40.6, 7.5, t],
      ["gallery_maze_n_h2w", -35.7, -47.2, 3.6, t],
      ["gallery_maze_n_h2e", -25.15, -47.2, 12.1, t],
      ["gallery_maze_n_v_dead", -28.4, -51.2, t, 4.0],
      ["gallery_maze_w_h1a", -51.65, -22.0, 4.1, t],
      ["gallery_maze_w_h1b", -43.35, -22.0, 6.5, t],
      ["gallery_maze_w_south_dead", -50.6, -10.05, t, 2.6],
      ["gallery_maze_w_v1", -44.15, -26.6, t, 6.4],
      ["gallery_maze_w_v2", -44.15, -16.8, t, 6.4],
      ["gallery_maze_s_h1w", -40.6, -2.4, 26.0, t],
      ["gallery_maze_s_h1e", -19.6, -2.4, 4.0, t],
      ["gallery_maze_s_h2w", -45.0, 4.2, 16.0, t],
      ["gallery_maze_s_h2e", -27.6, 4.2, 7.5, t],
      ["gallery_maze_s_h2ee", -19.4, 4.2, 4.0, t],
      ["gallery_maze_s_v_dead", -46.2, 6.5, t, 2.6],
    ];
    for (const [name, x, z, sx, sz] of walls) {
      this.placeDressedBox(chunk, chunkId, name, x, y, z, sx, h, sz, mazeMat);
    }

    const bloodY = floorY + 5.04;
    const poolMat = new THREE.MeshStandardMaterial({
      color: 0x9a1418,
      roughness: 0.38,
      metalness: 0.06,
      transparent: true,
      opacity: 0.9,
      emissive: 0x5a080c,
      emissiveIntensity: 0.42,
      depthWrite: false,
    });
    this.addWingPlane(chunk, chunkId, "blood_north_lab", 12.4, 8.6, -28.4, bloodY, -45.2, poolMat);
    this.addWingPlane(chunk, chunkId, "blood_west_lab", 9.6, 16.4, -46.2, bloodY, -20.4, poolMat);
    this.addWingPlane(chunk, chunkId, "blood_south_lab", 14.8, 8.4, -32.5, bloodY, 1.6, poolMat);

    const gloom = new THREE.PointLight(0x5a1018, 2.6, 8.8, 1.65);
    gloom.position.set(-22.5, floorY + 6.15, -42.0);
    gloom.name = `${chunkId}_f2_lab_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    const gloomWest = new THREE.PointLight(0x4a0c14, 2.2, 8.2, 1.65);
    gloomWest.position.set(-48.0, floorY + 6.15, -12.0);
    gloomWest.name = `${chunkId}_f2_lab_gloom_w`;
    this.scene.add(gloomWest);
    chunk.meshes.push(gloomWest);
    const gloomSouth = new THREE.PointLight(0x521018, 2.3, 8.4, 1.65);
    gloomSouth.position.set(-22.5, floorY + 6.15, 1.6);
    gloomSouth.name = `${chunkId}_f2_lab_gloom_s`;
    this.scene.add(gloomSouth);
    chunk.meshes.push(gloomSouth);

    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_north_gate",
      position: [-22.5, floorY + 5.0, -36.4],
      floor: 2,
      links: ["tw_2f_gallery_north", "tw_2f_north_end"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_north_end",
      position: [-32.5, floorY + 5.0, -50.0],
      floor: 2,
      links: ["tw_2f_north_gate"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_west_gate",
      position: [-38.4, floorY + 5.0, -12.0],
      floor: 2,
      links: ["tw_2f_gallery_south", "tw_2f_west_end"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_west_end",
      position: [-50.0, floorY + 5.0, -12.0],
      floor: 2,
      links: ["tw_2f_west_gate"],
    }, chunkId);
    this.linkTransitionWaypoint("tw_2f_gallery_north", "tw_2f_north_gate");
    this.linkTransitionWaypoint("tw_2f_gallery_south", "tw_2f_west_gate");
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_south_gate",
      position: [-22.5, floorY + 5.0, -8.2],
      floor: 2,
      links: ["tw_2f_gallery_south", "tw_2f_south_end"],
    }, chunkId);
    this.collisionWorld.addTransitionWaypoint({
      id: "tw_2f_south_end",
      position: [-22.5, floorY + 5.0, 6.0],
      floor: 2,
      links: ["tw_2f_south_gate"],
    }, chunkId);
    this.linkTransitionWaypoint("tw_2f_gallery_south", "tw_2f_south_gate");
  }

  dressBasementFlood(chunk, chunkId, floorY, bounds) {
    const waterY = floorY - 5.0 + 0.15;
    const waterMat = new THREE.MeshStandardMaterial({
      color: 0x2d7a68,
      roughness: 0.32,
      metalness: 0.12,
      transparent: true,
      opacity: 0.82,
      emissive: 0x145a48,
      emissiveIntensity: 0.55,
      depthWrite: false,
    });
    const addWater = (name, width, length, x, z) => {
      const mesh = new THREE.Mesh(this.getPlaneGeometry(width, length), waterMat);
      mesh.rotation.x = -Math.PI / 2;
      mesh.position.set(x, waterY, z);
      mesh.name = `${chunkId}_${name}`;
      mesh.renderOrder = 2;
      this.scene.add(mesh);
      chunk.meshes.push(mesh);
    };
    addWater("flood_water_west", bounds.westWingWidth, bounds.b1TotalLength, bounds.westWingCenterX, bounds.b1CenterZ);
    addWater("flood_water_east", bounds.eastWingWidth, bounds.b1TotalLength, bounds.eastWingCenterX, bounds.b1CenterZ);
    addWater(
      "flood_water_landing",
      bounds.rampHalfWidth * 2,
      bounds.landingLength,
      bounds.b1StartX,
      (bounds.landingMinZ + bounds.landingMaxZ) / 2,
    );

    const foamMat = new THREE.MeshStandardMaterial({
      color: 0x6a8f7e,
      roughness: 0.85,
      transparent: true,
      opacity: 0.48,
      emissive: 0x1a3c30,
      emissiveIntensity: 0.22,
      depthWrite: false,
      side: THREE.DoubleSide,
    });
    const foamSpots = [
      [10.4, 31.6, 1.8, 1.1], [8.6, 28.4, 1.4, 0.9], [12.2, 34.2, 1.6, 1.0],
      [19.6, 32.4, 1.5, 1.0], [22.4, 29.8, 1.3, 0.8], [-3.2, 30.6, 1.2, 0.9],
      [9.4, 36.2, 1.1, 0.7], [16.8, 27.6, 1.4, 0.85],
    ];
    foamSpots.forEach(([x, z, w, l], i) => {
      const foam = new THREE.Mesh(this.getPlaneGeometry(w, l), foamMat);
      foam.rotation.x = -Math.PI / 2;
      foam.rotation.z = i * 0.41;
      foam.position.set(x, waterY + 0.02, z);
      foam.name = `${chunkId}_flood_foam_${i}`;
      foam.renderOrder = 3;
      this.scene.add(foam);
      chunk.meshes.push(foam);
    });

    const lineMat = new THREE.MeshStandardMaterial({
      color: 0x0a1814,
      roughness: 0.35,
      metalness: 0.28,
      emissive: 0x06241c,
      emissiveIntensity: 0.18,
      transparent: true,
      opacity: 0.78,
      side: THREE.DoubleSide,
    });
    const waterline = [
      { x: bounds.b1MinX + 0.21, z: bounds.b1CenterZ, rotY: Math.PI / 2, w: bounds.b1TotalLength * 0.92, h: 0.38 },
      { x: bounds.b1MaxX - 0.21, z: bounds.b1CenterZ, rotY: -Math.PI / 2, w: bounds.b1TotalLength * 0.92, h: 0.38 },
      { x: bounds.b1CenterX, z: bounds.b1MinZ + 0.21, rotY: 0, w: (bounds.b1MaxX - bounds.b1MinX) * 0.9, h: 0.38 },
      { x: bounds.b1CenterX, z: bounds.b1MaxZ - 0.21, rotY: Math.PI, w: (bounds.b1MaxX - bounds.b1MinX) * 0.9, h: 0.38 },
    ];
    waterline.forEach((band, i) => {
      const mesh = new THREE.Mesh(this.getPlaneGeometry(band.w, band.h), lineMat);
      mesh.position.set(band.x, waterY + 0.12, band.z);
      mesh.rotation.y = band.rotY;
      mesh.name = `${chunkId}_flood_waterline_${i}`;
      this.scene.add(mesh);
      chunk.meshes.push(mesh);
    });

    const dripMat = new THREE.MeshStandardMaterial({
      color: 0xa8e0d0,
      roughness: 0.08,
      metalness: 0.25,
      transparent: true,
      opacity: 0.7,
      emissive: 0x1a5a44,
      emissiveIntensity: 0.38,
    });
    const dripSpots = [
      [8.2, 30.4], [12.4, 34.8], [18.6, 27.6], [22.8, 33.2],
      [-2.4, 28.8], [-5.1, 32.6], [4.8, 36.4], [15.8, 38.2],
      [10.6, 26.8], [20.4, 29.5], [1.8, 31.2], [-6.4, 35.0],
      [9.8, 32.0], [14.2, 30.8], [21.2, 36.4],
    ];
    dripSpots.forEach(([x, z], i) => {
      const drip = new THREE.Mesh(this.getBoxGeometry(0.04, 0.7 + (i % 3) * 0.22, 0.04), dripMat);
      drip.position.set(x, floorY - 2.45, z);
      drip.name = `${chunkId}_drip_${i}`;
      this.scene.add(drip);
      chunk.meshes.push(drip);
    });

    const pipeMat = new THREE.MeshStandardMaterial({
      color: 0x1a2420,
      roughness: 0.7,
      metalness: 0.45,
    });
    const pipe = new THREE.Mesh(this.getBoxGeometry(18.5, 0.16, 0.16), pipeMat);
    pipe.position.set(9.0, floorY - 2.38, 33.4);
    pipe.name = `${chunkId}_b1_pipe`;
    this.scene.add(pipe);
    chunk.meshes.push(pipe);
    const pipe2 = new THREE.Mesh(this.getBoxGeometry(0.14, 0.14, 9.4), pipeMat);
    pipe2.position.set(21.6, floorY - 2.42, 31.8);
    pipe2.name = `${chunkId}_b1_pipe_e`;
    this.scene.add(pipe2);
    chunk.meshes.push(pipe2);

    const debrisMat = new THREE.MeshStandardMaterial({ color: 0x2a241c, roughness: 0.9 });
    [[10.1, 31.2, 1.15, 0.08, 0.34], [8.4, 29.6, 0.7, 0.06, 0.22], [12.8, 33.8, 0.85, 0.07, 0.28]].forEach(([x, z, w, h, d], i) => {
      const plank = new THREE.Mesh(this.getBoxGeometry(w, h, d), debrisMat);
      plank.position.set(x, waterY + 0.04, z);
      plank.rotation.y = i * 0.7;
      plank.name = `${chunkId}_flood_debris_${i}`;
      this.scene.add(plank);
      chunk.meshes.push(plank);
    });

    const sheetMat = new THREE.MeshStandardMaterial({
      color: 0x1c2a24,
      roughness: 0.9,
      transparent: true,
      opacity: 0.48,
      side: THREE.DoubleSide,
    });
    [[6.4, 29.2], [19.8, 35.6], [-4.2, 33.8], [11.2, 36.8]].forEach(([x, z], i) => {
      const sheet = new THREE.Mesh(this.getBoxGeometry(0.04, 1.85, 0.85), sheetMat);
      sheet.position.set(x, floorY - 3.85, z);
      sheet.rotation.y = i * 0.7;
      sheet.name = `${chunkId}_b1_sheet_${i}`;
      this.scene.add(sheet);
      chunk.meshes.push(sheet);
    });

    const moldMat = new THREE.MeshStandardMaterial({
      color: 0x0c1812,
      roughness: 1,
      transparent: true,
      opacity: 0.7,
      emissive: 0x05140e,
      emissiveIntensity: 0.16,
      side: THREE.DoubleSide,
    });
    const mold = new THREE.Mesh(this.getPlaneGeometry(3.4, 1.6), moldMat);
    mold.position.set(bounds.b1MinX + 0.22, floorY - 3.4, 31.2);
    mold.rotation.y = Math.PI / 2;
    mold.name = `${chunkId}_b1_mold`;
    this.scene.add(mold);
    chunk.meshes.push(mold);

    const gloom = new THREE.PointLight(0x1c4a3c, 4.8, 10.5, 1.6);
    gloom.position.set(10.4, floorY - 4.05, 31.6);
    gloom.name = `${chunkId}_b1_flood_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    const gloomEast = new THREE.PointLight(0x16382e, 3.6, 9.2, 1.65);
    gloomEast.position.set(20.6, floorY - 4.05, 32.2);
    gloomEast.name = `${chunkId}_b1_flood_gloom_e`;
    this.scene.add(gloomEast);
    chunk.meshes.push(gloomEast);
  }

  dressBloodGallery(chunk, chunkId, floorY, bounds) {
    const bloodY = floorY + 5.04;
    const poolMat = new THREE.MeshStandardMaterial({
      color: 0x9a1418,
      roughness: 0.38,
      metalness: 0.06,
      transparent: true,
      opacity: 0.94,
      emissive: 0x5a080c,
      emissiveIntensity: 0.48,
      depthWrite: false,
    });
    const smearMat = new THREE.MeshStandardMaterial({
      color: 0x5a080c,
      roughness: 0.48,
      transparent: true,
      opacity: 0.88,
      emissive: 0x2a0204,
      emissiveIntensity: 0.28,
      side: THREE.DoubleSide,
    });
    const addPool = (name, w, l, x, z, rot = 0) => {
      const mesh = new THREE.Mesh(this.getPlaneGeometry(w, l), poolMat);
      mesh.rotation.x = -Math.PI / 2;
      mesh.rotation.z = rot;
      mesh.position.set(x, bloodY, z);
      mesh.name = `${chunkId}_${name}`;
      mesh.renderOrder = 2;
      this.scene.add(mesh);
      chunk.meshes.push(mesh);
    };
    addPool("blood_carpet", bounds.galWestWingWidth * 0.94, bounds.galLength * 0.92, bounds.galWestWingCenterX, bounds.galCenterZ);
    addPool("blood_shrine", 5.2, 3.8, -34.2, -22.0, 0.12);
    addPool("blood_landing", 3.4, 5.0, bounds.f2StartX - 1.1, -21.6, -0.08);
    addPool("blood_south", 5.8, 2.6, -24.6, -11.4, 0.2);
    addPool("blood_trail", 1.4, 8.4, -27.8, -20.2, 0.18);
    addPool("blood_near", 2.6, 2.2, -23.4, -18.2, -0.22);

    const wallStreaks = [
      { x: bounds.galMinX + 0.22, y: floorY + 6.15, z: -22.0, rotY: Math.PI / 2, w: 3.2, h: 2.2 },
      { x: bounds.galMinX + 0.22, y: floorY + 6.05, z: -30.4, rotY: Math.PI / 2, w: 2.6, h: 2.0 },
      { x: -22.6, y: floorY + 6.2, z: bounds.galMinZ + 0.22, rotY: 0, w: 3.8, h: 2.1 },
      { x: -28.8, y: floorY + 6.1, z: bounds.galMaxZ - 0.22, rotY: Math.PI, w: 3.0, h: 1.9 },
      { x: -24.2, y: floorY + 6.05, z: bounds.galMaxZ - 0.22, rotY: Math.PI, w: 2.4, h: 1.7 },
      { x: -20.4, y: floorY + 6.25, z: bounds.galMinZ + 0.22, rotY: 0, w: 2.2, h: 1.8 },
    ];
    wallStreaks.forEach((streak, i) => {
      const mesh = new THREE.Mesh(this.getPlaneGeometry(streak.w, streak.h), smearMat);
      mesh.position.set(streak.x, streak.y, streak.z);
      mesh.rotation.y = streak.rotY;
      mesh.name = `${chunkId}_blood_wall_${i}`;
      this.scene.add(mesh);
      chunk.meshes.push(mesh);
    });

    const handMat = new THREE.MeshStandardMaterial({
      color: 0x6a0a10,
      roughness: 0.7,
      emissive: 0x2a0204,
      emissiveIntensity: 0.22,
      side: THREE.DoubleSide,
    });
    [[-37.72, 6.05, -18.6, Math.PI / 2], [-37.72, 5.85, -25.4, Math.PI / 2], [-21.2, 5.9, -8.72, Math.PI]].forEach(([x, y, z, rotY], i) => {
      const hand = new THREE.Mesh(this.getPlaneGeometry(0.38, 0.48), handMat);
      hand.position.set(x, floorY + y, z);
      hand.rotation.y = rotY;
      hand.name = `${chunkId}_blood_hand_${i}`;
      this.scene.add(hand);
      chunk.meshes.push(hand);
    });

    const dripMat = new THREE.MeshStandardMaterial({
      color: 0x6a1014,
      roughness: 0.28,
      metalness: 0.08,
      transparent: true,
      opacity: 0.78,
      emissive: 0x2a0406,
      emissiveIntensity: 0.14,
    });
    const drips = [
      [-20.4, -14.2], [-24.8, -18.6], [-31.2, -22.4], [-35.6, -28.8],
      [-27.4, -12.8], [-22.0, -32.4], [-33.8, -16.2], [-18.6, -21.8],
      [-23.6, -17.8], [-29.2, -24.6], [-26.4, -19.2],
    ];
    drips.forEach(([x, z], i) => {
      const drip = new THREE.Mesh(this.getBoxGeometry(0.04, 0.42 + (i % 3) * 0.12, 0.04), dripMat);
      drip.position.set(x, floorY + 7.52, z);
      drip.name = `${chunkId}_blood_drip_${i}`;
      this.scene.add(drip);
      chunk.meshes.push(drip);
    });

    const visceraMat = new THREE.MeshStandardMaterial({
      color: 0x5a0c10,
      roughness: 0.55,
      metalness: 0.12,
      emissive: 0x2a0406,
      emissiveIntensity: 0.28,
    });
    [[-24.2, -15.1, 1.6, 0.28, 0.9], [-22.4, -19.6, 1.1, 0.22, 0.7], [-26.8, -17.4, 1.35, 0.24, 0.8]].forEach(([x, z, w, h, d], i) => {
      const pile = new THREE.Mesh(this.getBoxGeometry(w, h, d), visceraMat);
      pile.position.set(x, floorY + 5.0 + h / 2, z);
      pile.rotation.y = i * 0.5;
      pile.name = `${chunkId}_blood_pile_${i}`;
      this.scene.add(pile);
      chunk.meshes.push(pile);
    });

    this.spawnAssetProp(chunk, {
      ...HORROR_PROP_ASSETS.wrappedBody,
      id: `${chunkId}_2f_body_a`,
      position: [-24.8, floorY + 5.0, -14.6],
      rotation: [0, 1.1, 0],
    });
    this.spawnAssetProp(chunk, {
      ...HORROR_PROP_ASSETS.wrappedBody,
      id: `${chunkId}_2f_body_b`,
      position: [-32.4, floorY + 5.0, -29.2],
      rotation: [0, -0.6, 0],
    });
    this.spawnAssetProp(chunk, {
      ...HORROR_PROP_ASSETS.wrappedBody,
      id: `${chunkId}_2f_body_c`,
      position: [-21.8, floorY + 5.0, -20.4],
      rotation: [0, 0.35, 0],
    });
    this.spawnAssetProp(chunk, {
      ...HORROR_PROP_ASSETS.hangingBundle,
      id: `${chunkId}_2f_hang`,
      position: [-26.6, floorY + 7.55, -19.4],
      rotation: [0, 0.4, 0],
    });
    this.spawnAssetProp(chunk, {
      ...HORROR_PROP_ASSETS.hangingBundle,
      id: `${chunkId}_2f_hang_b`,
      position: [-30.8, floorY + 7.5, -26.2],
      rotation: [0, -0.55, 0],
    });

    const shrineGlow = new THREE.PointLight(0x6a1018, 4.6, 10.2, 1.55);
    shrineGlow.position.set(-34.0, floorY + 6.1, -22.0);
    shrineGlow.name = `${chunkId}_2f_blood_glow`;
    this.scene.add(shrineGlow);
    chunk.meshes.push(shrineGlow);
    const poolGlow = new THREE.PointLight(0x4a0a10, 3.8, 9.0, 1.6);
    poolGlow.position.set(-23.6, floorY + 5.95, -18.2);
    poolGlow.name = `${chunkId}_2f_blood_glow_near`;
    this.scene.add(poolGlow);
    chunk.meshes.push(poolGlow);
  }

  buildCeilingLights(chunk, type, center, chunkId, rand, floorY = 0) {
    // Dark corridor overhaul: No automatic ceiling or wall lights are spawned.
    // The labyrinth remains dark by default, requiring the player's flashlight (F)
    // or manual SafeLights (E) placed along corridors, corners, and doors.
    chunk.lights = [];
  }

  buildInteractables(chunk, type, center, chunkId, rand, floorY = 0) {
    const isStart = type === "start";
    const isWorkshop = type === "workshop";
    const isPlayroom = type === "playroom";
    const isStorage = type === "storage";
    const isEvent = type === "event";
    const isArchive = type === "archive";

    // 1. Spawning doors
    const doorMaterial = this.textures.createDoorMaterial();
    const addDynamicDoor = (id, label, localPos, size, isLocked = false, isBlocked = false, reason = "") => {
      const globalPos = [center.x + localPos[0], floorY + localPos[1], center.z + localPos[2]];
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
      addDynamicDoor("door-right-playroom", "붉은 놀이방", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
    } else if (isStorage) {
      addDynamicDoor("door-left-storage", "삐걱대는 보관실", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
    } else if (isArchive) {
      addDynamicDoor("door-archive", "고서 보관소", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);

      const deskGeo = this.getBoxGeometry(2.4, 0.72, 1.2);
      const deskMesh = new THREE.Mesh(deskGeo, this.propMaterial);
      deskMesh.position.set(center.x, floorY + 0.36, center.z + 1.5);
      deskMesh.castShadow = true;
      deskMesh.receiveShadow = true;
      deskMesh.name = `${chunkId}_archive_desk`;
      this.scene.add(deskMesh);
      chunk.meshes.push(deskMesh);
      this.collisionWorld.addStaticBox(deskMesh.name, deskMesh.position, new THREE.Vector3(2.4, 0.72, 1.2), chunkId);
    } else if (isEvent) {
      addDynamicDoor("door-upper-mirror", "뒤틀린 거울방", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);

      const paintGeo = this.getBoxGeometry(1.65, 1.95, 0.08);
      const paintMat = this.textures.createHwaPaintMaterial();
      const painting = new THREE.Mesh(paintGeo, paintMat);
      painting.name = "upper-hwa-painting";
      painting.position.set(center.x - 4.78, floorY + 1.6, center.z);
      painting.rotation.y = -Math.PI / 2;
      painting.castShadow = true;
      this.scene.add(painting);
      chunk.meshes.push(painting);
    } else if (type === "wide_room") {
      addDynamicDoor("door-final-lock-room", "봉인된 출구방", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
    } else if (type === "flicker_room") {
      addDynamicDoor("door-flicker-room", "깜빡이는 방", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
    } else if (type === "omen_room") {
      addDynamicDoor("door-omen-room", "서늘한 북실", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      this.dressOmenRoom(chunk, center, chunkId, floorY);
    } else if (type === "static_room") {
      addDynamicDoor("door-static-room", "노이즈가 남는 방", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      this.dressStaticRoom(chunk, center, chunkId, floorY);
    } else if (type === "stairs_2f") {
      addDynamicDoor("door-stairs-2f", "2층 계단실", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor("door-stairs-2f-north", "2층 북실 통로", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor("door-stairs-2f-gallery", "2층 액자방", [-1.35, 5.0, -5.8], [0.22, 2.35, 2.4]);
    } else if (type === "stairs_b1") {
      addDynamicDoor("door-stairs-b1", "지하 계단실", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
    } else if (type === "tatami_room" || type === "pillar_room") {
      addDynamicDoor("door-tatami-room", "고풍스러운 다실", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);

      // Tatami Center Mat Platform (7.6m x 7.6m)
      const tatamiGeo = this.getBoxGeometry(7.6, 0.04, 7.6);
      const tatamiMat = new THREE.MeshStandardMaterial({
        color: 0x685f44,
        roughness: 0.86,
        metalness: 0.02,
      });
      const tatamiMesh = new THREE.Mesh(tatamiGeo, tatamiMat);
      tatamiMesh.position.set(center.x, floorY + 0.02, center.z);
      tatamiMesh.receiveShadow = true;
      tatamiMesh.name = `${chunkId}_tatami_mat`;
      this.scene.add(tatamiMesh);
      chunk.meshes.push(tatamiMesh);

      // Low Tea Table (Chabudai)
      const tableGeo = this.getBoxGeometry(1.6, 0.42, 1.2);
      const tableMesh = new THREE.Mesh(tableGeo, this.propMaterial);
      tableMesh.position.set(center.x, floorY + 0.21, center.z);
      tableMesh.castShadow = true;
      tableMesh.receiveShadow = true;
      tableMesh.name = `${chunkId}_tea_table`;
      this.scene.add(tableMesh);
      chunk.meshes.push(tableMesh);
      this.collisionWorld.addStaticBox(tableMesh.name, tableMesh.position, new THREE.Vector3(1.6, 0.42, 1.2), chunkId);
    } else if (isClassroomType(type)) {
      const classLabel = type === "nurse_office" ? "보건실 문"
        : type === "music_room" ? "음악실 문"
        : type === "faculty_office" ? "교무실 문"
        : type === "science_lab" ? "과학실 문"
        : "교실 문";
      const open = this.getOpenings(chunk.cx, chunk.cz);
      if (open.N) addDynamicDoor(`${chunkId}_class_door`, classLabel, [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      else if (open.S) addDynamicDoor(`${chunkId}_class_door`, classLabel, [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      else if (open.E) addDynamicDoor(`${chunkId}_class_door`, classLabel, [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      else addDynamicDoor(`${chunkId}_class_door`, classLabel, [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressClassroom(chunk, center, chunkId, floorY);
    }

    // 2. Cabinets
    const cabinetMaterial = this.textures.createCabinetMaterial();
    const addDynamicCabinet = (id, label, localPos, yaw) => {
      const globalPos = [center.x + localPos[0], floorY + localPos[1], center.z + localPos[2]];
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
      addDynamicCabinet("cabinet-workshop", "작업실 신발장", [-5.0, 0.0, -5.0], -Math.PI / 2);
    } else if (isPlayroom) {
      addDynamicCabinet("cabinet-playroom", "교실 신발장", [5.0, 0.0, -5.0], Math.PI / 2);
    } else if (isStorage) {
      addDynamicCabinet("cabinet-storage", "창고 신발장", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (isArchive) {
      addDynamicCabinet("cabinet-archive", "서고 신발장", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (chunk.cx === 1 && chunk.cz === 0) {
      // Odd EW loops: SW hide off the south jog, NW hide off the north jog.
      addDynamicCabinet("cabinet_chokepoint_1_0", "복도 신발장", [-5.25, 0.0, 5.25], -Math.PI / 2);
      addDynamicCabinet("cabinet_chokepoint_1_0_n", "복도 신발장", [-5.25, 0.0, -5.25], Math.PI);
    } else if (chunk.cx === 0 && chunk.cz === 1) {
      addDynamicCabinet("cabinet_junction_0_1", "교차로 신발장", [-5.2, 0.0, 5.2], -Math.PI / 2);
    } else if (type === "omen_room") {
      addDynamicCabinet("cabinet-omen-room", "북실 신발장", [5.4, 0.0, -5.4], Math.PI / 2);
    } else if (type === "static_room") {
      addDynamicCabinet("cabinet-static-room", "방송실 신발장", [-5.4, 0.0, -5.4], -Math.PI / 2);
    } else if (type === "stairs_2f") {
      addDynamicCabinet("cabinet_stairs_2f_attic", "2층 갤러리 벽장", [-6.0, 5.0, 4.0], Math.PI / 2);
      addDynamicCabinet("cabinet_stairs_2f_shrine", "2층 사당 벽장", [-18.4, 5.0, -4.2], Math.PI / 2);
      addDynamicCabinet("cabinet_stairs_2f_landing", "2층 계단참 벽장", [0.0, 5.0, -6.2], 0);
      addDynamicCabinet("cabinet_stairs_2f_south", "2층 별실 벽장", [-16.5, 5.0, 4.8], Math.PI);
      addDynamicCabinet("cabinet_stairs_2f_north_lab", "2층 북미로 벽장", [-16.5, 5.0, -34.0], Math.PI / 2);
      addDynamicCabinet("cabinet_stairs_2f_west_lab", "2층 서미로 벽장", [-35.5, 5.0, 4.2], Math.PI);
      addDynamicCabinet("cabinet_stairs_2f_south_lab", "2층 남미로 벽장", [-30.2, 5.0, 22.2], Math.PI);
    } else if (type === "stairs_b1") {
      addDynamicCabinet("cabinet_b1_cellar", "지하 보육실 벽장", [-22.5, -5.0, 2.0], -Math.PI / 2);
      addDynamicCabinet("cabinet_b1_flood", "지하 침수복도 벽장", [-7.4, -5.0, -0.4], Math.PI / 2);
      addDynamicCabinet("cabinet_b1_east", "지하 동쪽 벽장", [8.2, -5.0, 0.2], -Math.PI / 2);
      addDynamicCabinet("cabinet_b1_boiler", "지하 보일러 벽장", [6.4, -5.0, -5.4], Math.PI);
      addDynamicCabinet("cabinet_b1_south_lab", "지하 남미로 벽장", [-7.6, -5.0, 22.2], Math.PI);
      addDynamicCabinet("cabinet_b1_west_lab", "지하 서미로 벽장", [-37.8, -5.0, -5.4], -Math.PI / 2);
      addDynamicCabinet("cabinet_b1_east_lab", "지하 동미로 벽장", [26.5, -5.0, 16.0], Math.PI);
    } else if (type === "tatami_room" || type === "pillar_room") {
      addDynamicCabinet("cabinet-tatami-room", "다실 벽장", [7.1, 0.0, 0.0], -Math.PI / 2);
    } else if (isClassroomType(type)) {
      addDynamicCabinet(`cabinet_class_${chunk.cx}_${chunk.cz}`, type === "nurse_office" ? "보건실 벽장" : type === "music_room" ? "음악실 벽장" : type === "faculty_office" ? "교무실 벽장" : type === "science_lab" ? "과학실 벽장" : "교실 신발장", [5.4, 0.0, 5.4], Math.PI / 2);
    } else if (!isStart && !isEvent && !isArchive && !type.includes("stairs") && (type.includes("room") || type.includes("storage")) && rand() < 0.4) {
      addDynamicCabinet(`cabinet_${chunk.cx}_${chunk.cz}`, "복도 신발장", [-5.2, 0.0, -5.2], -Math.PI / 2);
    }

    const hallLike = type === "start" || type === "corridor_ns" || type === "corridor_ew"
      || type === "t_junction" || type === "cross_junction" || type === "narrow_ns" || type === "dead_end";
    if (hallLike && chunk.cabinets.length === 0) {
      const alcove = (chunk.cx + chunk.cz) % 2 === 0
        ? { pos: [5.25, 0.0, 5.25], yaw: Math.PI / 2 }
        : { pos: [-5.25, 0.0, 5.25], yaw: -Math.PI / 2 };
      addDynamicCabinet(`cabinet_hall_${chunk.cx}_${chunk.cz}`, "신발장", alcove.pos, alcove.yaw);
    }
    const mazeHall = hallLike && (this.getHallChicanes(chunk.cx, chunk.cz).ew || this.getHallChicanes(chunk.cx, chunk.cz).ns);
    if (mazeHall && chunk.cabinets.length === 1 && !(chunk.cx === 0 && chunk.cz === 0)) {
      const first = chunk.cabinets[0];
      const lx = first.position.x - center.x;
      const lz = first.position.z - center.z;
      addDynamicCabinet(
        `cabinet_hall2_${chunk.cx}_${chunk.cz}`,
        "반대편 신발장",
        [lx, 0.0, -lz],
        lz > 0 ? Math.PI : 0,
      );
    }
    const addLoreNote = (id, localPos, yaw, body) => {
      const note = new LoreNote({
        id,
        label: "벽에 꽂힌 종이",
        position: [center.x + localPos[0], floorY + localPos[1], center.z + localPos[2]],
        yaw,
        body,
      });
      note.chunkId = chunkId;
      this.scene.add(note.group);
      chunk.loreNotes.push(note);
    };
    if (type === "start") {
      addLoreNote(`${chunkId}_lore`, [-7.55, 1.35, -3.2], Math.PI / 2,
        "제단함은 이 홀에 있다. 이름을 넷 모으기 전에는 열리지 않는다.");
    } else if (type === "stairs_b1") {
      addLoreNote(`${chunkId}_lore_flood`, [-7.4, -3.55, -2.2], Math.PI / 2,
        "물이 이름을 적고 있다. 요람 쪽 열쇠를 집고 신발장에 숨어서 나오십시오.");
      addLoreNote(`${chunkId}_lore_maze`, [6.2, -3.55, -5.0], Math.PI,
        "보일러실 너머로 요람이 운다. 물이 차도 이름을 집고 벽장에 숨으십시오.");
      addLoreNote(`${chunkId}_lore_south`, [-7.4, -3.55, 16.4], 0,
        "남쪽 물이 두 번 꺾인다. 막다른 벽장 앞에서 호흡을 끊으십시오.");
    } else if (type === "stairs_2f") {
      addLoreNote(`${chunkId}_lore_blood`, [-8.2, 6.35, 1.6], Math.PI / 2,
        "액자 아래는 아직 젖어 있다. 그림이 떨어지면 네 번째 이름이 드러난다.");
      addLoreNote(`${chunkId}_lore_maze`, [-16.2, 6.35, 4.4], Math.PI,
        "피 묻은 꺾인 복도는 액자로 끝난다. 그림이 떨어질 때까지 호흡을 끊으십시오.");
      addLoreNote(`${chunkId}_lore_north`, [-16.4, 6.35, -33.2], 0,
        "북쪽 미로에서 그림자가 손전등을 보면 멈춘다. 보지 않으면 다가온다.");
    } else if (type === "nurse_office") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "보건실 장부에 출석이 끝나지 않은 이름이 넷이다. 별관에서 같은 신발을 두 번 보지 마십시오.");
    } else if (type === "music_room") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "피아노 뚜껑이 열린 채로 녹슬어 있다. 한 음이 모자라면 복도가 당신의 이름을 외운다.");
    } else if (type === "faculty_office") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "교무실 출석부에 네 이름이 지워져 있다. 별관 끝 교실은 한 번만 들어가십시오.");
    } else if (type === "science_lab") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "약품 냄새가 복도까지 샌다. 가스관이 아직 식지 않았다.");
    } else if (type === "omen_room" || type === "static_room" || type === "flicker_room" || type === "classroom") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0);
    } else if (isWorkshop) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "요람은 비어 있다. 젖은 발자국이 남쪽 지하 계단으로 이어진다.");
    } else if (isPlayroom) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "인형이 아직 줍지 않은 이름을 가리킨다. 눈이 마주치면 따라가십시오.");
    } else if (isStorage) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "창고 선반 뒤에 도자기 열쇠가 있다. 문을 닫아 추격을 끊으십시오.");
    } else if (hallLike && (Math.abs(chunk.cx) + Math.abs(chunk.cz)) % 2 === 1) {
      addLoreNote(`${chunkId}_lore`, [-1.18, 1.38, -5.4], Math.PI / 2);
    }

    if (isWorkshop) {
      const crib = new THREE.Mesh(this.getBoxGeometry(1.35, 0.72, 0.78), this.trimMaterial);
      crib.position.set(center.x - 4.2, floorY + 0.36, center.z + 3.4);
      crib.name = `${chunkId}_empty_crib`;
      this.scene.add(crib);
      chunk.meshes.push(crib);
      this.collisionWorld.addStaticBox(crib.name, crib.position, new THREE.Vector3(1.35, 0.72, 0.78), chunkId);
      const stain = new THREE.Mesh(
        this.getPlaneGeometry(1.8, 4.2),
        new THREE.MeshStandardMaterial({
          color: 0x1a3a32,
          roughness: 0.55,
          transparent: true,
          opacity: 0.55,
          emissive: 0x0a241c,
          emissiveIntensity: 0.18,
          depthWrite: false,
        }),
      );
      stain.rotation.x = -Math.PI / 2;
      stain.position.set(center.x - 2.4, floorY + 0.02, center.z + 1.4);
      stain.name = `${chunkId}_crib_wet_trail`;
      this.scene.add(stain);
      chunk.meshes.push(stain);
    }

    // 3. Keys
    const addDynamicKey = (id, label, localPos) => {
      const globalPos = [center.x + localPos[0], floorY + localPos[1], center.z + localPos[2]];
      const key = new KeyItem({
        id,
        label,
        position: globalPos,
      }, this.scene);
      key.chunkId = chunkId;
      this.scene.add(key.group);
      chunk.keys.push(key);
    };

    if (type === "stairs_b1") {
      addDynamicKey("key-workshop", "녹슨 열쇠", [-18.5, -5.0, -5.5]); // B1 crib: [-2.5, -5.0, 26.5]
    } else if (isWorkshop) {
      // Key moved to B1 nursery
    } else if (isPlayroom) {
      addDynamicKey("key-playroom", "놀이방 열쇠", [0.0, 0.0, 0.0]);
    } else if (isStorage) {
      addDynamicKey("key-storage", "도자기 열쇠", [0.0, 0.0, 0.0]);
    } else if (type === "stairs_2f") {
      addDynamicKey("key-hwacat", "뒤틀린 열쇠", [-19.5, 5.0, -6.0]); // 2F shrine: [-35.5, 5.0, -22.0]
      const keyObj = chunk.keys[chunk.keys.length - 1];
      if (keyObj) {
        keyObj.initiallyVisible = false;
        keyObj.isAvailable = false;
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
        label: "제단함",
        position: [center.x, floorY, center.z - 2.05],
      }, this.scene);
      exit.chunkId = chunkId;
      this.scene.add(exit.group);
      chunk.finalExit = exit;
      this.collisionWorld.addStaticBox(
        `${chunkId}_altar_block`,
        new THREE.Vector3(center.x, floorY + 0.4, center.z - 2.05),
        new THREE.Vector3(1.72, 0.8, 0.98),
        chunkId,
      );
    }

    if (chunk.cx === 4 && chunk.cz === 0) {
      this.dressAnnexGate(chunk, center, chunkId, floorY);
    }

    // 5. Lights that the player can turn on as a visited-place marker.
    this.buildSafeLights(chunk, type, center, chunkId, rand, floorY);

    // 6. Spawning custom glb props
    this.buildProps(chunk, type, center, chunkId, rand, floorY);
  }

  buildSafeLights(chunk, type, center, chunkId, rand, floorY = 0) {
    let lightIdx = 0;
    const spawnSafeLight = (variant, localX, localY, localZ, yaw, labelOverride = null) => {
      let facing = yaw;
      let x = localX;
      let z = localZ;
      if (variant === "wall-switch") {
        // Mesh faces local -Z. Historical E/W call sites used the opposite yaw.
        if (Math.abs(Math.abs(yaw) - Math.PI / 2) < 0.02) {
          facing = -yaw;
        }
        const inset = 0.16;
        x += -Math.sin(facing) * inset;
        z += -Math.cos(facing) * inset;
      }
      const localId = `safe_${variant.replaceAll("-", "_")}_${lightIdx++}`;
      const stateKey = `${chunk.cx},${chunk.cz}:${localId}`;
      const isOn = this.game?.activatedSafeLightKeys?.has(stateKey) || false;
      const safeLight = new SafeLight({
        id: `${chunkId}_${localId}`,
        stateKey,
        label: labelOverride || SAFE_LIGHT_LABELS[variant] || "조명",
        variant,
        position: [center.x + x, floorY + localY, center.z + z],
        yaw: facing,
        isOn,
      });
      safeLight.chunkId = chunkId;
      this.scene.add(safeLight.group);
      chunk.safeLights.push(safeLight);
    };

    const wallH = 1.08;
    const ceilingH = 2.35;
    const floorH = 0.0;

    if (type === "corridor_ns" || type === "narrow_ns" || type === "corridor_ew"
      || type === "cross_junction" || type === "t_junction" || type === "start") {
      // Mount on alcove inner faces (x=±1.4 or z=±1.4), never in the 2.4m gap.
      spawnSafeLight("wall-switch", -1.18, wallH, -5.2, Math.PI / 2, "벽 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, 5.2, -Math.PI / 2, "벽 스위치");
      spawnSafeLight("wall-switch", -5.2, wallH, -1.18, Math.PI, "벽 스위치");
      spawnSafeLight("wall-switch", 5.2, wallH, 1.18, 0, "벽 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "형광등 스위치");
      if (type === "start") {
        spawnSafeLight("floor-lamp", -3.2, floorH, -3.2, Math.PI * 0.25, "신당 낡은 스탠드");
      }
    } else if (type === "corner") {
      // Corners: Flush on outer walls
      spawnSafeLight("wall-switch", -1.18, wallH, 3.5, Math.PI / 2, "모퉁이 스위치");
      spawnSafeLight("wall-switch", 3.5, wallH, -1.18, Math.PI, "모퉁이 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "모퉁이 천장 스위치");
      spawnSafeLight("floor-lamp", -0.7, floorH, -0.7, Math.PI * 0.25, "낡은 스탠드");
    } else if (type === "dead_end") {
      // Dead ends: Side walls and end wall
      spawnSafeLight("wall-switch", -1.18, wallH, -3.0, Math.PI / 2, "벽 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, -3.0, -Math.PI / 2, "벽 스위치");
      spawnSafeLight("wall-switch", 0.0, wallH, -7.58, Math.PI, "벽 스위치");
      spawnSafeLight("toy-lamp", -0.6, floorH, -6.5, 0, "장난감 램프");
    } else if (type === "workshop") {
      // Workshop: Flush on north entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", 1.6, wallH, -7.58, Math.PI, "작업방 입구 스위치");
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "작업방 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "작업방 벽 스위치");
      spawnSafeLight("floor-lamp", 4.0, floorH, 2.5, -Math.PI / 4, "작업방 낡은 스탠드");
    } else if (type === "playroom") {
      // Playroom: Flush on north entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "놀이방 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, -7.58, Math.PI, "놀이방 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "놀이방 벽 스위치");
      spawnSafeLight("toy-lamp", 3.2, floorH, 2.8, 0, "놀이방 장난감 램프");
    } else if (type === "storage" || type === "toy_storage") {
      // Storage: Flush on south entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "보관실 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "보관실 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "보관실 벽 스위치");
      spawnSafeLight("floor-lamp", -3.5, floorH, 3.5, 0, "보관실 낡은 스탠드");
    } else if (type === "archive") {
      // Archive: Flush on south entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "서고 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "서고 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "서고 벽 스위치");
      spawnSafeLight("floor-lamp", 3.5, floorH, -3.5, 0, "서고 낡은 스탠드");
    } else if (type === "event") {
      // Mirror event room: Flush on south entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "거울방 입구 스위치");
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "거울방 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "거울방 벽 스위치");
      spawnSafeLight("floor-lamp", 3.5, floorH, -3.5, 0, "거울방 낡은 스탠드");
    } else if (type === "tatami_room" || type === "pillar_room") {
      // Japanese Tatami Room: Flush on north entrance wall, west perimeter wall, SE floor lamp, and table lamp
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "다실 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "다실 벽 스위치");
      spawnSafeLight("floor-lamp", 6.2, floorH, 6.2, -Math.PI / 4, "다실 낡은 스탠드");
      spawnSafeLight("toy-lamp", 0.0, floorH + 0.42, 0.0, 0, "찻상 촛대 램프");
    } else if (type === "stairs_2f") {
      // 2F Stairwell & Gallery: Flush on south entrance wall, stairwell wall, landing, and gallery west wall
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "계단실 입구 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH + 2.5, 0.0, -Math.PI / 2, "2층 계단 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH + 5.0, -6.0, -Math.PI / 2, "2층 착지점 스위치");
      spawnSafeLight("wall-switch", -21.58, wallH + 5.0, -6.0, Math.PI / 2, "사당 벽 스위치");
      spawnSafeLight("floor-lamp", -18.0, floorH + 5.0, -4.0, Math.PI / 4, "갤러리 낡은 스탠드");
      spawnSafeLight("toy-lamp", -21.15, floorH + 5.0 + 0.85, -6.0, 0, "사당 제단 촛대");
    } else if (type === "stairs_b1") {
      // B1 Stairwell & Cellar/Nursery: Flush on north entrance wall, stairwell wall, landing, and nursery west wall
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "계단실 입구 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH - 2.5, 0.0, -Math.PI / 2, "지하 계단 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH - 5.0, 6.0, -Math.PI / 2, "지하 착지점 스위치");
      spawnSafeLight("wall-switch", -23.58, wallH - 5.0, -3.5, Math.PI / 2, "지하 보육실 벽 스위치");
      spawnSafeLight("floor-lamp", -19.5, floorH - 5.0, -3.5, Math.PI / 4, "보육실 낡은 스탠드");
      spawnSafeLight("floor-lamp", -8.0, floorH - 5.0, 6.0, -Math.PI / 4, "지하 복도 스탠드");
    } else if (type === "omen_room") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "북실 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, -2.4, Math.PI / 2, "북실 벽 스위치");
      spawnSafeLight("toy-lamp", 0.0, floorH + 0.86, 0.0, 0, "꺼진 제단 촛대");
    } else if (type === "static_room") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "노이즈방 입구 스위치");
      spawnSafeLight("wall-switch", 7.58, wallH, 0.0, -Math.PI / 2, "노이즈방 벽 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, -2.2, 0, "지지직거리는 형광등");
    } else if (type === "wide_room") {
      // Wide room: Flush on south entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "출구방 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "출구방 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "출구방 벽 스위치");
      spawnSafeLight("floor-lamp", 4.0, floorH, -4.0, -Math.PI / 4, "출구방 낡은 스탠드");
    } else if (type === "classroom") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "교실 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "교실 벽 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "꺼진 형광등");
    } else if (type === "nurse_office") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "보건실 입구 스위치");
      spawnSafeLight("floor-lamp", 4.2, floorH, -3.4, -Math.PI / 5, "보건실 스탠드");
    } else if (type === "music_room") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "음악실 입구 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "음악실 형광등");
    } else if (type === "faculty_office") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "교무실 입구 스위치");
      spawnSafeLight("floor-lamp", -4.0, floorH, 3.2, Math.PI / 5, "교무실 스탠드");
    } else if (type === "science_lab") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "과학실 입구 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "과학실 형광등");
    } else {
      // Generic fallback room (e.g. flicker_room): Flush on perimeter walls
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "벽 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "벽 스위치");
      spawnSafeLight("floor-lamp", 5.0, floorH, 5.0, -Math.PI / 4, "낡은 스탠드");
      if (type === "flicker_room") {
        spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "형광등 스위치");
      }
    }
  }

  createSignMaterial(text) {
    const canvas = typeof document !== "undefined" ? document.createElement("canvas") : null;
    if (!canvas) {
      return new THREE.MeshStandardMaterial({ color: 0x3a2a1c, roughness: 0.86 });
    }
    canvas.width = 768;
    canvas.height = 192;
    const ctx = canvas.getContext("2d");
    ctx.fillStyle = "#24160e";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = "#8a6a38";
    ctx.lineWidth = 8;
    ctx.strokeRect(10, 10, canvas.width - 20, canvas.height - 20);
    ctx.fillStyle = "#e2d2b4";
    ctx.font = "700 92px 'Noto Serif KR', serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(text, canvas.width / 2, canvas.height / 2 + 4);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    return new THREE.MeshStandardMaterial({
      map: texture,
      roughness: 0.78,
      metalness: 0.04,
      emissive: 0x1a1208,
      emissiveIntensity: 0.16,
      side: THREE.DoubleSide,
    });
  }

  dressAnnexGate(chunk, center, chunkId, floorY) {
    const frame = this.placeDressedBox(
      chunk, chunkId, "annex_sign_frame",
      center.x - 0.02, floorY + 2.28, center.z,
      0.08, 0.72, 2.72,
      new THREE.MeshStandardMaterial({ color: 0x3a2418, roughness: 0.86 }),
      false,
    );
    frame.rotation.y = 0;
    const sign = new THREE.Mesh(this.getPlaneGeometry(2.6, 0.62), this.createSignMaterial("별관"));
    sign.position.set(center.x - 0.08, floorY + 2.28, center.z);
    sign.rotation.y = -Math.PI / 2;
    sign.name = `${chunkId}_annex_sign`;
    this.scene.add(sign);
    chunk.meshes.push(sign);

    const paper = new THREE.Mesh(
      this.getPlaneGeometry(0.42, 0.58),
      new THREE.MeshStandardMaterial({
        map: this.textures.load("loreNote"),
        color: 0xe8dcc4,
        roughness: 0.9,
        side: THREE.DoubleSide,
      }),
    );
    paper.position.set(center.x - 1.16, floorY + 1.42, center.z - 4.6);
    paper.rotation.y = -Math.PI / 2;
    paper.name = `${chunkId}_annex_notice`;
    this.scene.add(paper);
    chunk.meshes.push(paper);
  }

  pickSafeLightPlacement(variant, type, center, rand, floorY = 0) {
    if (variant === "wall-switch") {
      const wall = this.pickWallPlacement(type, center, rand);
      return {
        position: [wall.x, floorY + 1.08, wall.z],
        yaw: wall.yaw,
      };
    }

    if (variant === "ceiling-switch") {
      const ceiling = this.pickCeilingPlacement(type, center, rand);
      return {
        position: [ceiling.x, floorY + 2.35, ceiling.z],
        yaw: ceiling.yaw,
      };
    }

    const floor = this.pickFloorPlacement(type, center, rand);
    return {
      position: [floor.x, floorY, floor.z],
      yaw: floor.yaw,
    };
  }

  buildProps(chunk, type, center, chunkId, rand, floorY = 0) {
    const isCorridorOrStair = type === "start" || type === "corridor_ns" || type === "corridor_ew" || type === "narrow_ns" || type === "stairs_2f" || type === "stairs_b1";

    const propMaterial = this.propMaterial;

    // Antique chest prop spawn only in rooms with small chance
    if (!isCorridorOrStair && rand() < 0.25) {
      const toyGeo = this.getBoxGeometry(0.8, 0.5, 0.8);
      const toy = new THREE.Mesh(toyGeo, propMaterial);
      const localX = (rand() - 0.5) * 8;
      const localZ = (rand() - 0.5) * 8;
      toy.position.set(center.x + localX, floorY + 0.25, center.z + localZ);
      toy.castShadow = true;
      toy.receiveShadow = true;
      toy.name = `${chunkId}_toy_prop`;
      this.scene.add(toy);
      chunk.meshes.push(toy);
      // Small collision box
      this.collisionWorld.addStaticBox(toy.name, toy.position, new THREE.Vector3(0.8, 0.5, 0.8), chunkId);
    }

    this.buildHorrorAtmosphereProps(chunk, type, center, chunkId, rand, floorY);
  }

  buildHorrorAtmosphereProps(chunk, type, center, chunkId, rand, floorY = 0) {
    const isRoomLike = ROOM_LIKE_CHUNK_TYPES.has(type);
    const repeatingWing = Math.abs(chunk.cx) > 2 || Math.abs(chunk.cz) > 2;
    const wallChance = isRoomLike ? 0.72 : repeatingWing ? 0.58 : 0.46;
    const floorChance = isRoomLike ? 0.82 : repeatingWing ? 0.42 : 0.28;
    const ceilingChance = type === "flicker_room" ? 0.82 : repeatingWing ? 0.48 : 0.38;

    if (rand() < wallChance) {
      const definition = this.pickFrom(WALL_HORROR_PROPS, rand);
      const wall = this.pickWallPlacement(type, center, rand);
      this.spawnAssetProp(chunk, {
        ...definition,
        id: `${chunkId}_${definition.kind}_wall`,
        position: [wall.x, floorY + 1.65 + rand() * 0.38, wall.z],
        rotation: [0, wall.yaw, 0],
      });
    }

    if (rand() < floorChance) {
      const definition = this.pickFrom(FLOOR_HORROR_PROPS, rand);
      const floor = this.pickFloorPlacement(type, center, rand);
      this.spawnAssetProp(chunk, {
        ...definition,
        id: `${chunkId}_${definition.kind}_floor`,
        position: [floor.x, floorY, floor.z],
        rotation: [0, floor.yaw, 0],
      });
    }

    if (rand() < ceilingChance) {
      const definition = this.pickFrom(CEILING_HORROR_PROPS, rand);
      const ceiling = this.pickCeilingPlacement(type, center, rand, floorY);
      this.spawnAssetProp(chunk, {
        ...definition,
        id: `${chunkId}_${definition.kind}_ceiling`,
        position: [ceiling.x, floorY + 2.76, ceiling.z],
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
        position: [scatter.x, floorY, scatter.z],
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
      return { x: center.x + 7.52, z: center.z + segmentOffset, yaw: Math.PI / 2 };
    }
    return { x: center.x - 7.52, z: center.z + segmentOffset, yaw: -Math.PI / 2 };
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
    if (type === "dead_end" || isClassroomType(type)) return new Set(["north", "east", "west"]);
    if (type === "workshop" || type === "playroom") return new Set(["south", "east", "west"]);
    if (type === "storage" || type === "event" || type === "wide_room" || type === "stairs_2f") return new Set(["north", "east", "west"]);
    if (type === "stairs_b1") return new Set(["south", "east", "west"]);
    return new Set();
  }

  pickFloorPlacement(type, center, rand) {
    let localX = (rand() - 0.5) * 9.2;
    let localZ = (rand() - 0.5) * 9.2;

    if (type === "corridor_ns") {
      localX = rand() < 0.5 ? -0.8 : 0.8;
      localZ = (rand() - 0.5) * 8.5;
    } else if (type === "corridor_ew") {
      localX = (rand() - 0.5) * 8.5;
      localZ = rand() < 0.5 ? -0.8 : 0.8;
    } else if (type === "narrow_ns") {
      localX = rand() < 0.5 ? -0.8 : 0.8;
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
          active: false, // Inactive initially until WeepingAngelIntroEvent activates it
          speed: 1.3, // slow pursuit speed (m/s)
          catchDistance: 1.05,
          radius: 0.38,
          size: definition.size,
          loaded: false,
          path: null,
          pathTimer: 0,
        };
        anchor.userData.shadowMesh = addShadowBlob(anchor, 0.38);
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
      // Retain original PBR parameters without harsh forced roughness
      cloned.roughness = source.roughness !== undefined && source.roughness !== null ? source.roughness : 0.48;
      cloned.metalness = source.metalness !== undefined && source.metalness !== null ? source.metalness : 0.05;
      if (source.map) {
        cloned.map = source.map;
        cloned.color.setHex(0xffffff); // 100% full original texture color
      }
      return cloned;
    }

    return new THREE.MeshStandardMaterial({
      map: source.map ?? null,
      color: source.map ? new THREE.Color(0xffffff) : (source.color?.clone?.() ?? new THREE.Color(0xffffff)),
      transparent: source.transparent ?? false,
      opacity: source.opacity ?? 1,
      alphaTest: source.alphaTest ?? 0.02,
      side: source.side ?? THREE.FrontSide,
      roughness: 0.48,
      metalness: 0.05,
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

  buildWaypoints(chunk, type, center, floorY = 0) {
    // All waypoints are [worldX, floorY, worldZ] tuples.
    // Coverage spans the full walkable interior so monsters always have
    // distant, reachable targets without cutting through walls.
    const cx = center.x;
    const cz = center.z;
    const wp = (lx, lz) => [cx + lx, floorY, cz + lz];

    if (type === "start" || type === "cross_junction" || type === "t_junction"
      || type === "corridor_ns" || type === "narrow_ns" || type === "corridor_ew") {
      const chicanes = this.getHallChicanes(chunk.cx, chunk.cz);
      const hallOpen = this.getOpenings(chunk.cx, chunk.cz);
      chunk.waypoints = [
        wp(0, 0),
        wp(-5.2, -5.2), wp(5.2, -5.2), wp(-5.2, 5.2), wp(5.2, 5.2),
      ];
      if (!chicanes.ew && !chicanes.ns) {
        chunk.waypoints.push(wp(0, -5.4), wp(0, 5.4), wp(-5.4, 0), wp(5.4, 0));
      }
      if (chicanes.ew) {
        chunk.waypoints.push(
          wp(-6.2, 0), wp(-3.2, 0), wp(3.2, 0), wp(6.2, 0),
          wp(-5.25, 5.2), wp(5.25, 5.2), wp(-5.25, -5.2), wp(5.25, -5.2),
        );
        if (chicanes.ns || hallOpen.N) chunk.waypoints.push(wp(0, -5.4), wp(0, -6.5));
        if (chicanes.ns || hallOpen.S) chunk.waypoints.push(wp(0, 5.4), wp(0, 6.5));
      }
      if (chicanes.ns) {
        chunk.waypoints.push(wp(0, -6.2), wp(0, -3.2), wp(0, 3.2), wp(0, 6.2));
        if (chicanes.ew || hallOpen.W) chunk.waypoints.push(wp(-5.4, 0), wp(-6.5, 0));
        if (chicanes.ew || hallOpen.E) chunk.waypoints.push(wp(5.4, 0), wp(6.5, 0));
        if (!chicanes.ew) {
          chunk.waypoints.push(
            wp(-5.25, 5.2), wp(5.25, 5.2), wp(-5.25, -5.2), wp(5.25, -5.2),
          );
        }
      }
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
    } else if (type === "wide_room" || type === "flicker_room" || type === "omen_room" || type === "static_room" || isClassroomType(type)) {
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
    } else if (type === "archive") {
      chunk.waypoints = [wp(0,0), wp(2,-3), wp(2,3), wp(-2,-3), wp(-2,3), wp(0,5)];
    } else if (type === "event") {
      // Partition wall at x=-5 local → stay east side
      chunk.waypoints = [wp(2,0), wp(2,-4), wp(2,4), wp(4,-3), wp(4,3), wp(0,5)];
    } else if (type === "stairs_2f") {
      chunk.waypoints = [
        [cx + 0, 0.0, cz + 5],
        [cx + 0, 2.5, cz + 0],
        [cx + 0, 5.0, cz - 5],
        [-17.2, 5.0, -21.8],
        [-22.5, 5.0, -22.0],
        [-27.5, 5.0, -22.0],
        [-35.5, 5.0, -22.0],
        [-32.5, 5.0, -31.0],
        [-22.5, 5.0, -12.0],
      ];
    } else if (type === "stairs_b1") {
      chunk.waypoints = [
        [cx + 0, 0.0, cz - 5],
        [cx + 0, -2.5, cz + 0],
        [cx + 0, -5.0, cz + 5],
        [14.8, -5.0, 38.0],
        [8.0, -5.0, 34.0],
        [1.2, -5.0, 30.0],
        [-3.5, -5.0, 28.5],
        [-2.5, -5.0, 26.5],
        [-6.5, -5.0, 34.0],
        [20.5, -5.0, 32.0],
      ];
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
        if (mesh.userData.shadowMesh) {
          const shadow = mesh.userData.shadowMesh;
          shadow.geometry?.dispose();
          if (shadow.material) {
            shadow.material.map?.dispose();
            shadow.material.dispose();
          }
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
      safeLight.dispose?.();
    }

    for (const note of chunk.loreNotes || []) {
      this.scene.remove(note.group);
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

function addShadowBlob(group, radius) {
  const canvas = document.createElement("canvas");
  canvas.width = 64;
  canvas.height = 64;
  const ctx = canvas.getContext("2d");
  
  const gradient = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
  gradient.addColorStop(0, "rgba(0, 0, 0, 0.72)");
  gradient.addColorStop(0.4, "rgba(0, 0, 0, 0.45)");
  gradient.addColorStop(1, "rgba(0, 0, 0, 0.0)");
  
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, 64, 64);
  
  const texture = new THREE.CanvasTexture(canvas);
  const geometry = new THREE.PlaneGeometry(radius * 3.6, radius * 3.6);
  const material = new THREE.MeshStandardMaterial({
    map: texture,
    transparent: true,
    depthWrite: false,
    color: 0x000000,
    roughness: 1.0,
    metalness: 0.0,
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  mesh.rotation.x = -Math.PI / 2;
  mesh.position.y = 0.015; // slightly above ground to prevent z-fighting

  group.add(mesh);
  return mesh;
}
