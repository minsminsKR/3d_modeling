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
  "workshop",
  "playroom",
  "storage",
  "event",
  "archive",
  "stairs_2f",
  "stairs_b1",
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
    };

    const key = `${cx},${cz}`;
    if (FIXED_MAP_LAYOUT[key]) {
      return FIXED_MAP_LAYOUT[key];
    }

    const seed = getChunkSeed(this.baseSeed, cx, cz);
    const rand = createRandom(seed)();
    if (rand < 0.3) return "corridor_ns";
    if (rand < 0.6) return "corridor_ew";
    return "corner";
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

    // Soul Gathering (영혼집합소) Flooded Canals: Add shallow dark reflective water plane in South canal corridor
    const isFlooded = cz === 1 && (cx === 0 || cx === -1 || cx === 1);
    if (isFlooded) {
      const waterGeo = this.getPlaneGeometry(16, 16);
      const waterMat = new THREE.MeshStandardMaterial({
        color: 0x061e27,
        roughness: 0.1,
        metalness: 0.85,
        transparent: true,
        opacity: 0.82,
      });
      const waterMesh = new THREE.Mesh(waterGeo, waterMat);
      waterMesh.rotation.x = -Math.PI / 2;
      waterMesh.position.set(center.x, floorY + 0.08, center.z);
      waterMesh.name = `${chunkId}_water_surface`;
      this.scene.add(waterMesh);
      chunk.meshes.push(waterMesh);
    }

    const dtFloor = performance.now() - tFloor0;

    // 2. Build Walls based on template
    const tWalls0 = performance.now();
    this.buildTemplateWalls(chunk, type, center, chunkId, rand, floorY);
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
      // Spans x from -23.8 to -14.8, z from -23.8 to -8.5 at Y = 5.0
      // ====================================================
      const galMinX = -23.8;
      const galMaxX = -14.8;
      const galMinZ = -23.8;
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

      // Perimeter enclosing walls:
      // North Wall: at z = -23.8 spanning full width from x = -23.8 to -14.8 (9.0m wide)
      const northWallWidth = galMaxX - galMinX;
      const northWallCenterX = (galMinX + galMaxX) / 2;
      const galNorthGeo = this.getBoxGeometry(northWallWidth + 0.4, 2.8, 0.4);
      const galNorthMesh = new THREE.Mesh(galNorthGeo, stairMat);
      galNorthMesh.position.set(northWallCenterX, floorY + 6.4, galMinZ - 0.15);
      galNorthMesh.castShadow = true;
      galNorthMesh.name = `${chunkId}_gallery_2f_wall_n`;
      this.scene.add(galNorthMesh);
      chunk.meshes.push(galNorthMesh);
      this.collisionWorld.addStaticBox(galNorthMesh.name, galNorthMesh.position, new THREE.Vector3(northWallWidth + 0.4, 2.8, 0.4), chunkId);

      // West Wall: at x = -23.8 spanning from z = -23.8 to -8.5 (15.3m long)
      const galWestGeo = this.getBoxGeometry(0.4, 2.8, galLength + 0.4);
      const galWestMesh = new THREE.Mesh(galWestGeo, stairMat);
      galWestMesh.position.set(galMinX - 0.15, floorY + 6.4, galCenterZ);
      galWestMesh.castShadow = true;
      galWestMesh.name = `${chunkId}_gallery_2f_wall_w`;
      this.scene.add(galWestMesh);
      chunk.meshes.push(galWestMesh);
      this.collisionWorld.addStaticBox(galWestMesh.name, galWestMesh.position, new THREE.Vector3(0.4, 2.8, galLength + 0.4), chunkId);

      // South Wall: at z = -8.5 spanning from x = -23.8 to -17.2 (6.6m wide)
      const galSouthGeo = this.getBoxGeometry(galWestWingWidth + 0.4, 2.8, 0.4);
      const galSouthMesh = new THREE.Mesh(galSouthGeo, stairMat);
      galSouthMesh.position.set(galWestWingCenterX, floorY + 6.4, galMaxZ + 0.15);
      galSouthMesh.castShadow = true;
      galSouthMesh.name = `${chunkId}_gallery_2f_wall_s`;
      this.scene.add(galSouthMesh);
      chunk.meshes.push(galSouthMesh);
      this.collisionWorld.addStaticBox(galSouthMesh.name, galSouthMesh.position, new THREE.Vector3(galWestWingWidth + 0.4, 2.8, 0.4), chunkId);

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
      // West wall at [-23.6, 6.6, -16.0] facing east (+X)
      // ====================================================
      const paintFrameGeo = this.getBoxGeometry(1.85, 2.15, 0.08);
      const paintFrameMesh = new THREE.Mesh(paintFrameGeo, this.propMaterial);
      paintFrameMesh.position.set(-23.68, floorY + 6.6, -16.0);
      paintFrameMesh.rotation.y = Math.PI / 2;
      paintFrameMesh.castShadow = true;
      paintFrameMesh.name = `${chunkId}_gallery_2f_painting_frame`;
      this.scene.add(paintFrameMesh);
      chunk.meshes.push(paintFrameMesh);

      const paintGeo = this.getBoxGeometry(1.65, 1.95, 0.08);
      const paintMat = this.textures.createHwaPaintMaterial();
      const painting = new THREE.Mesh(paintGeo, paintMat);
      painting.name = "upper-hwa-painting";
      painting.position.set(-23.6, floorY + 6.6, -16.0);
      painting.rotation.y = Math.PI / 2;
      painting.castShadow = true;
      painting.receiveShadow = true;
      this.scene.add(painting);
      chunk.meshes.push(painting);

      // Shrine Altar Table against the west wall below the painting
      const altarGeo = this.getBoxGeometry(0.7, 0.85, 2.0);
      const altarMesh = new THREE.Mesh(altarGeo, this.propMaterial);
      altarMesh.position.set(-23.15, floorY + 5.0 + 0.425, -16.0);
      altarMesh.castShadow = true;
      altarMesh.receiveShadow = true;
      altarMesh.name = `${chunkId}_gallery_2f_altar`;
      this.scene.add(altarMesh);
      chunk.meshes.push(altarMesh);
      this.collisionWorld.addStaticBox(altarMesh.name, altarMesh.position, new THREE.Vector3(0.7, 0.85, 2.0), chunkId);

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

      // Register CollisionWorld.addFloorArea for 2F gallery at Y = 5.0 (minX: -23.8, maxX: -14.8, minZ: -23.8, maxZ: -8.5)
      this.collisionWorld.addFloorArea({
        id: "gallery_2f_mirror",
        floor: 2,
        type: "walkable",
        y: floorY + 5.0,
        minX: -23.8,
        maxX: -14.8,
        minZ: -23.8,
        maxZ: -8.5,
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
        position: [-20.5, floorY + 5.0, -16.0],
        floor: 2,
        links: ["tw_2f_gallery_doorway", "tw_2f_gallery_altar", "tw_2f_gallery_south"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_altar",
        position: [-21.2, floorY + 5.0, -16.0],
        floor: 2,
        links: ["tw_2f_gallery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_2f_gallery_south",
        position: [-20.5, floorY + 5.0, -11.0],
        floor: 2,
        links: ["tw_2f_gallery_center"],
      }, chunkId);
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
        color: 0x5c5042,
        roughness: 0.88,
        metalness: 0.04,
      });
      const b1FloorMat = new THREE.MeshStandardMaterial({
        map: this.textures.load("floor"),
        color: 0x483e34,
        roughness: 0.92,
        metalness: 0.02,
      });
      const b1CeilMat = new THREE.MeshStandardMaterial({
        map: this.textures.load("ceiling"),
        color: 0x362c22,
        roughness: 0.96,
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
      // Spans x from 8.2 to 23.8, z from 24.2 to 39.8 at Y = -5.0
      // ====================================================
      const b1MinX = 8.2;
      const b1MaxX = 23.8;
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

      // South Wall: at z = 39.8, spanning full width from x = 8.2 to 23.8 (15.6m)
      const b1SouthGeo = this.getBoxGeometry(b1TotalWidth + 0.4, 2.8, 0.4);
      const b1SouthMesh = new THREE.Mesh(b1SouthGeo, b1WallMat);
      b1SouthMesh.position.set(b1CenterX, floorY - 3.6, b1MaxZ + 0.15);
      b1SouthMesh.castShadow = true;
      b1SouthMesh.name = `${chunkId}_cellar_b1_wall_s`;
      this.scene.add(b1SouthMesh);
      chunk.meshes.push(b1SouthMesh);
      this.collisionWorld.addStaticBox(b1SouthMesh.name, b1SouthMesh.position, new THREE.Vector3(b1TotalWidth + 0.4, 2.8, 0.4), chunkId);

      // West Wall: at x = 8.2, spanning from z = 24.2 to 39.8 (15.6m)
      const b1WestGeo = this.getBoxGeometry(0.4, 2.8, b1TotalLength + 0.4);
      const b1WestMesh = new THREE.Mesh(b1WestGeo, b1WallMat);
      b1WestMesh.position.set(b1MinX - 0.15, floorY - 3.6, b1CenterZ);
      b1WestMesh.castShadow = true;
      b1WestMesh.name = `${chunkId}_cellar_b1_wall_w`;
      this.scene.add(b1WestMesh);
      chunk.meshes.push(b1WestMesh);
      this.collisionWorld.addStaticBox(b1WestMesh.name, b1WestMesh.position, new THREE.Vector3(0.4, 2.8, b1TotalLength + 0.4), chunkId);

      // East Wall: at x = 23.8, spanning from z = 24.2 to 39.8 (15.6m)
      const b1EastGeo = this.getBoxGeometry(0.4, 2.8, b1TotalLength + 0.4);
      const b1EastMesh = new THREE.Mesh(b1EastGeo, b1WallMat);
      b1EastMesh.position.set(b1MaxX + 0.15, floorY - 3.6, b1CenterZ);
      b1EastMesh.castShadow = true;
      b1EastMesh.name = `${chunkId}_cellar_b1_wall_e`;
      this.scene.add(b1EastMesh);
      chunk.meshes.push(b1EastMesh);
      this.collisionWorld.addStaticBox(b1EastMesh.name, b1EastMesh.position, new THREE.Vector3(0.4, 2.8, b1TotalLength + 0.4), chunkId);

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

      // Full-height East enclosing wall along x = 17.2: from z = 24.2 to 39.8 (15.6m, height 7.8m, center floorY - 1.1)
      const eastDivGeo = this.getBoxGeometry(0.3, 7.8, b1TotalLength);
      const eastDivMesh = new THREE.Mesh(eastDivGeo, b1WallMat);
      eastDivMesh.position.set(b1StartX + (rampHalfWidth + 0.15), floorY - 1.1, b1CenterZ);
      eastDivMesh.castShadow = true;
      eastDivMesh.name = `${chunkId}_cellar_b1_wall_e_div`;
      this.scene.add(eastDivMesh);
      chunk.meshes.push(eastDivMesh);
      this.collisionWorld.addStaticBox(eastDivMesh.name, eastDivMesh.position, new THREE.Vector3(0.3, 7.8, b1TotalLength), chunkId);

      // Vertical header wall under 1F floor at z = 29.5: from Y = -5.0 to Y = 0.0
      const headerGeo = this.getBoxGeometry(rampHalfWidth * 2, 5.0, 0.2);
      const headerMesh = new THREE.Mesh(headerGeo, stairMat);
      headerMesh.position.set(b1StartX, floorY - 2.5, b1StartZ);
      headerMesh.name = `${chunkId}_stair_b1_floor_header`;
      this.scene.add(headerMesh);
      chunk.meshes.push(headerMesh);

      // ====================================================
      // B1 Tatami & Nursery Area in the corner ([10.5, -5.0, 30.0])
      // Torn mats, blood stains, and broken toys
      // ====================================================
      const tatamiMat = new THREE.MeshStandardMaterial({
        color: 0x5a5438,
        roughness: 0.9,
        metalness: 0.02,
      });
      // Tatami mat 1
      const mat1Geo = this.getBoxGeometry(1.8, 0.06, 1.0);
      const mat1Mesh = new THREE.Mesh(mat1Geo, tatamiMat);
      mat1Mesh.position.set(10.5, floorY - 5.0 + 0.03, 29.3);
      mat1Mesh.receiveShadow = true;
      mat1Mesh.name = `${chunkId}_b1_tatami_1`;
      this.scene.add(mat1Mesh);
      chunk.meshes.push(mat1Mesh);

      // Tatami mat 2 (torn / askew)
      const mat2Geo = this.getBoxGeometry(1.8, 0.06, 1.0);
      const mat2Mesh = new THREE.Mesh(mat2Geo, tatamiMat);
      mat2Mesh.position.set(10.6, floorY - 5.0 + 0.035, 30.4);
      mat2Mesh.rotation.y = 0.06;
      mat2Mesh.receiveShadow = true;
      mat2Mesh.name = `${chunkId}_b1_tatami_2`;
      this.scene.add(mat2Mesh);
      chunk.meshes.push(mat2Mesh);

      // Blood stain decal on mats and floor
      const bloodGeo = this.getPlaneGeometry(1.8, 1.5);
      const bloodMat = new THREE.MeshStandardMaterial({
        color: 0x3b0202,
        roughness: 0.35,
        transparent: true,
        opacity: 0.88,
        depthWrite: false,
      });
      const bloodMesh = new THREE.Mesh(bloodGeo, bloodMat);
      bloodMesh.rotation.x = -Math.PI / 2;
      bloodMesh.position.set(10.5, floorY - 5.0 + 0.07, 29.9);
      bloodMesh.name = `${chunkId}_b1_blood_stain`;
      this.scene.add(bloodMesh);
      chunk.meshes.push(bloodMesh);

      // Broken doll pile prop
      this.spawnAssetProp(chunk, {
        ...HORROR_PROP_ASSETS.brokenDollPile,
        id: `${chunkId}_b1_nursery_dolls`,
        position: [10.5, floorY - 5.0, 30.0],
        rotation: [0, Math.PI / 4, 0],
      });

      // Scattered broken wooden toy blocks
      const toyBlock1 = new THREE.Mesh(this.getBoxGeometry(0.38, 0.3, 0.38), this.propMaterial);
      toyBlock1.position.set(11.3, floorY - 5.0 + 0.15, 29.2);
      toyBlock1.rotation.y = 0.4;
      toyBlock1.castShadow = true;
      toyBlock1.name = `${chunkId}_b1_toy_1`;
      this.scene.add(toyBlock1);
      chunk.meshes.push(toyBlock1);

      const toyBlock2 = new THREE.Mesh(this.getBoxGeometry(0.3, 0.24, 0.3), this.propMaterial);
      toyBlock2.position.set(9.7, floorY - 5.0 + 0.12, 30.8);
      toyBlock2.rotation.y = -0.3;
      toyBlock2.castShadow = true;
      toyBlock2.name = `${chunkId}_b1_toy_2`;
      this.scene.add(toyBlock2);
      chunk.meshes.push(toyBlock2);

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

      // Register CollisionWorld.addFloorArea for B1 cellar/nursery at Y = -5.0 (minX: 8.2, maxX: 23.8, minZ: 24.2, maxZ: 39.8)
      this.collisionWorld.addFloorArea({
        id: "cellar_b1_nursery",
        floor: -1,
        type: "walkable",
        y: floorY - 5.0,
        minX: 8.2,
        maxX: 23.8,
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
        links: ["tw_b1_bottom_from_1f", "tw_b1_cellar_doorway"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_cellar_doorway",
        position: [14.8, floorY - 5.0, 38.0],
        floor: -1,
        links: ["tw_b1_landing", "tw_b1_cellar_hall"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_cellar_hall",
        position: [11.5, floorY - 5.0, 38.0],
        floor: -1,
        links: ["tw_b1_cellar_doorway", "tw_b1_cabinet", "tw_b1_nursery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_cabinet",
        position: [9.5, floorY - 5.0, 34.0],
        floor: -1,
        links: ["tw_b1_cellar_hall", "tw_b1_nursery_center"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_nursery_center",
        position: [11.0, floorY - 5.0, 30.0],
        floor: -1,
        links: ["tw_b1_cellar_hall", "tw_b1_cabinet", "tw_b1_nursery_corner"],
      }, chunkId);
      this.collisionWorld.addTransitionWaypoint({
        id: "tw_b1_nursery_corner",
        position: [10.0, floorY - 5.0, 26.5],
        floor: -1,
        links: ["tw_b1_nursery_center"],
      }, chunkId);
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


    // Connections: N, S, E, W (true = open, false = closed)
    let N = true, S = true, E = true, W = true;

    if (Math.abs(chunk.cx) > 2 || Math.abs(chunk.cz) > 2) {
      // Complete solid boundary closure for outer regions
      N = false; S = false; E = false; W = false;
    } else {
      // 5x5 Narrative Maze Outer Borders
      if (chunk.cz === -2) N = false;
      if (chunk.cz === 2) S = false;
      if (chunk.cx === -2) W = false;
      if (chunk.cx === 2) E = false;

      // Type-specific connection closures
      if (type === "corridor_ns" || type === "narrow_ns") {
        E = false; W = false;
      } else if (type === "corridor_ew") {
        const connectsStairs2F = (chunk.cx === -1 && chunk.cz === 0);
        const connectsStairsB1 = (chunk.cx === 1 && chunk.cz === 1);
        const connectsTatami = (chunk.cx === -1 && chunk.cz === 1);
        if (!connectsStairs2F) N = false;
        if (!connectsStairsB1 && !connectsTatami) S = false;
      } else if (type === "t_junction") {
        if (chunk.cx === 2) {
          E = false;
        } else {
          W = false;
        }
      } else if (type === "corner") {
        N = false; W = false;
      } else if (type === "dead_end") {
        N = false; E = false; W = false;
      } else if (type === "workshop" || type === "playroom" || type === "tatami_room" || type === "pillar_room") {
        S = false; E = false; W = false;
      } else if (type === "storage" || type === "event" || type === "archive" || type === "wide_room" || type === "stairs_2f") {
        N = false; E = false; W = false;
      } else if (type === "stairs_b1") {
        S = false; E = false; W = false;
      }
    }

    // Build Single-Tile Boundary Openings (2.4m corridor opening)
    // North Border (z = -7.8)
    if (N) {
      addWallSegment(-4.6, -7.8, 6.8, 0.4, "n_left");
      addWallSegment(4.6, -7.8, 6.8, 0.4, "n_right");
    } else {
      addWallSegment(0.0, -7.8, 16.0, 0.4, "n_solid");
    }

    // South Border (z = 7.8)
    if (S) {
      addWallSegment(-4.6, 7.8, 6.8, 0.4, "s_left");
      addWallSegment(4.6, 7.8, 6.8, 0.4, "s_right");
    } else {
      addWallSegment(0.0, 7.8, 16.0, 0.4, "s_solid");
    }

    // West Border (x = -7.8)
    if (W) {
      addWallSegment(-7.8, -4.6, 0.4, 6.8, "w_top");
      addWallSegment(-7.8, 4.6, 0.4, 6.8, "w_bottom");
    } else {
      addWallSegment(-7.8, 0.0, 0.4, 16.0, "w_solid");
    }

    // East Border (x = 7.8)
    if (E) {
      addWallSegment(7.8, -4.6, 0.4, 6.8, "e_top");
      addWallSegment(7.8, 4.6, 0.4, 6.8, "e_bottom");
    } else {
      addWallSegment(7.8, 0.0, 0.4, 16.0, "e_solid");
    }

    // Build Single-Tile Corridor Interior Guide Walls (Uniform 2.4m corridor width)
    if (type === "corridor_ns" || type === "narrow_ns") {
      addWallSegment(-1.4, 0.0, 0.4, 16.0, "corridor_w_guide");
      addWallSegment(1.4, 0.0, 0.4, 16.0, "corridor_e_guide");
    } else if (type === "corridor_ew") {
      const connectsStairs2F = (chunk.cx === -1 && chunk.cz === 0);
      const connectsStairsB1 = (chunk.cx === 1 && chunk.cz === 1);
      const connectsTatami = (chunk.cx === -1 && chunk.cz === 1);

      if (connectsStairs2F) {
        addWallSegment(-4.6, -1.4, 6.8, 0.4, "corridor_n_guide_w");
        addWallSegment(4.6, -1.4, 6.8, 0.4, "corridor_n_guide_e");
        addWallSegment(-1.4, -4.6, 0.4, 6.0, "corridor_branch_w");
        addWallSegment(1.4, -4.6, 0.4, 6.0, "corridor_branch_e");
      } else {
        addWallSegment(0.0, -1.4, 16.0, 0.4, "corridor_n_guide");
      }

      if (connectsStairsB1 || connectsTatami) {
        addWallSegment(-4.6, 1.4, 6.8, 0.4, "corridor_s_guide_w");
        addWallSegment(4.6, 1.4, 6.8, 0.4, "corridor_s_guide_e");
        addWallSegment(-1.4, 4.6, 0.4, 6.0, "corridor_branch_w");
        addWallSegment(1.4, 4.6, 0.4, 6.0, "corridor_branch_e");
      } else {
        addWallSegment(0.0, 1.4, 16.0, 0.4, "corridor_s_guide");
      }
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
    } else if (type === "t_junction") {
      if (chunk.cx === 2) {
        addWallSegment(-1.4, -4.6, 0.4, 6.8, "tjunc_nw_ns");
        addWallSegment(-4.6, -1.4, 6.8, 0.4, "tjunc_nw_ew");
        addWallSegment(-1.4, 4.6, 0.4, 6.8, "tjunc_sw_ns");
        addWallSegment(-4.6, 1.4, 6.8, 0.4, "tjunc_sw_ew");
        addWallSegment(1.4, 0.0, 0.4, 16.0, "tjunc_e_guide");
      } else {
        addWallSegment(1.4, -4.6, 0.4, 6.8, "tjunc_ne_ns");
        addWallSegment(4.6, -1.4, 6.8, 0.4, "tjunc_ne_ew");
        addWallSegment(1.4, 4.6, 0.4, 6.8, "tjunc_se_ns");
        addWallSegment(4.6, 1.4, 6.8, 0.4, "tjunc_se_ew");
        addWallSegment(-1.4, 0.0, 0.4, 16.0, "tjunc_w_guide");
      }
    } else if (type === "cross_junction") {
      addWallSegment(-1.4, -4.6, 0.4, 6.8, "cross_nw_ns");
      addWallSegment(-4.6, -1.4, 6.8, 0.4, "cross_nw_ew");
      addWallSegment(1.4, -4.6, 0.4, 6.8, "cross_ne_ns");
      addWallSegment(4.6, -1.4, 6.8, 0.4, "cross_ne_ew");
      addWallSegment(-1.4, 4.6, 0.4, 6.8, "cross_sw_ns");
      addWallSegment(-4.6, 1.4, 6.8, 0.4, "cross_sw_ew");
      addWallSegment(1.4, 4.6, 0.4, 6.8, "cross_se_ns");
      addWallSegment(4.6, 1.4, 6.8, 0.4, "cross_se_ew");
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
    } else if (type === "wide_room" || type === "flicker_room") {
      addWallSegment(-5.0, -5.0, 1.2, 1.2, "wide_corner_nw");
      addWallSegment(5.0, -5.0, 1.2, 1.2, "wide_corner_ne");
      addWallSegment(-5.0, 5.0, 1.2, 1.2, "wide_corner_sw");
      addWallSegment(5.0, 5.0, 1.2, 1.2, "wide_corner_se");
    } else if (type === "start") {
      // Central shrine foyer with 2.4m corridor wings extending cleanly to North, South, East, and West doors
      addWallSegment(-1.4, -5.4, 0.4, 4.8, "start_nw_corridor");
      addWallSegment(1.4, -5.4, 0.4, 4.8, "start_ne_corridor");
      addWallSegment(-1.4, 5.4, 0.4, 4.8, "start_sw_corridor");
      addWallSegment(1.4, 5.4, 0.4, 4.8, "start_se_corridor");
      addWallSegment(-5.4, -1.4, 4.8, 0.4, "start_wn_corridor");
      addWallSegment(-5.4, 1.4, 4.8, 0.4, "start_ws_corridor");
      addWallSegment(5.4, -1.4, 4.8, 0.4, "start_en_corridor");
      addWallSegment(5.4, 1.4, 4.8, 0.4, "start_es_corridor");

      // Foyer corner enclosures
      addWallSegment(-2.2, -3.0, 1.6, 0.4, "start_nw_foyer_n");
      addWallSegment(-3.0, -2.2, 0.4, 1.6, "start_nw_foyer_w");
      addWallSegment(2.2, -3.0, 1.6, 0.4, "start_ne_foyer_n");
      addWallSegment(3.0, -2.2, 0.4, 1.6, "start_ne_foyer_e");
      addWallSegment(-2.2, 3.0, 1.6, 0.4, "start_sw_foyer_s");
      addWallSegment(-3.0, 2.2, 0.4, 1.6, "start_sw_foyer_w");
      addWallSegment(2.2, 3.0, 1.6, 0.4, "start_se_foyer_s");
      addWallSegment(3.0, 2.2, 0.4, 1.6, "start_se_foyer_e");
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
    } else if (type === "stairs_2f") {
      addDynamicDoor("door-stairs-2f", "2층 계단실", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
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
      addDynamicCabinet("cabinet-workshop", "작업방 캐비넷", [-5.0, 0.0, -5.0], -Math.PI / 2);
    } else if (isPlayroom) {
      addDynamicCabinet("cabinet-playroom", "놀이방 캐비넷", [5.0, 0.0, -5.0], Math.PI / 2);
    } else if (isStorage) {
      addDynamicCabinet("cabinet-storage", "보관실 캐비넷", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (isArchive) {
      addDynamicCabinet("cabinet-archive", "서고 벽장", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (chunk.cx === 1 && chunk.cz === 0) {
      addDynamicCabinet("cabinet_chokepoint_1_0", "복도 입구 캐비넷", [0.0, 0.0, 0.85], 0);
    } else if (chunk.cx === 0 && chunk.cz === 1) {
      addDynamicCabinet("cabinet_junction_0_1", "교차로 캐비넷", [-5.0, 0.0, 0.85], 0);
    } else if (type === "stairs_2f") {
      addDynamicCabinet("cabinet_stairs_2f_attic", "2층 갤러리 벽장", [-7.4, 5.0, 5.0], -Math.PI / 2);
    } else if (type === "stairs_b1") {
      addDynamicCabinet("cabinet_b1_cellar", "지하 보육실 벽장", [-7.4, -5.0, 2.0], -Math.PI / 2);
    } else if (type === "tatami_room" || type === "pillar_room") {
      addDynamicCabinet("cabinet-tatami-room", "다실 벽장", [7.1, 0.0, 0.0], -Math.PI / 2);
    } else if (!isStart && !isEvent && !isArchive && !type.includes("stairs") && (type.includes("room") || type.includes("storage")) && rand() < 0.4) {
      addDynamicCabinet(`cabinet_${chunk.cx}_${chunk.cz}`, "복도 구석 캐비넷", [-5.0, 0.0, 0.0], -Math.PI / 2);
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
      addDynamicKey("key-workshop", "녹슨 열쇠", [-5.5, -5.0, -4.5]); // B1 Nursery: [10.5, -5.0, 27.5]
    } else if (isWorkshop) {
      // Key moved to B1 nursery
    } else if (isPlayroom) {
      addDynamicKey("key-playroom", "놀이방 열쇠", [0.0, 0.0, 0.0]);
    } else if (isStorage) {
      addDynamicKey("key-storage", "도자기 열쇠", [0.0, 0.0, 0.0]);
    } else if (type === "stairs_2f") {
      addDynamicKey("key-hwacat", "뒤틀린 열쇠", [-6.5, 5.0, 0.0]); // 2F Gallery: [-22.5, 5.0, -16.0]
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
        position: [center.x, floorY, center.z],
      }, this.scene);
      exit.chunkId = chunkId;
      this.scene.add(exit.group);
      chunk.finalExit = exit;
    }

    // 5. Lights that the player can turn on as a visited-place marker.
    this.buildSafeLights(chunk, type, center, chunkId, rand, floorY);

    // 6. Spawning custom glb props
    this.buildProps(chunk, type, center, chunkId, rand, floorY);
  }

  buildSafeLights(chunk, type, center, chunkId, rand, floorY = 0) {
    let lightIdx = 0;
    const spawnSafeLight = (variant, localX, localY, localZ, yaw, labelOverride = null) => {
      const localId = `safe_${variant.replaceAll("-", "_")}_${lightIdx++}`;
      const stateKey = `${chunk.cx},${chunk.cz}:${localId}`;
      const isOn = this.game?.activatedSafeLightKeys?.has(stateKey) || false;
      const safeLight = new SafeLight({
        id: `${chunkId}_${localId}`,
        stateKey,
        label: labelOverride || SAFE_LIGHT_LABELS[variant] || "조명",
        variant,
        position: [center.x + localX, floorY + localY, center.z + localZ],
        yaw,
        isOn,
      });
      safeLight.chunkId = chunkId;
      this.scene.add(safeLight.group);
      chunk.safeLights.push(safeLight);
    };

    const wallH = 1.08;
    const ceilingH = 2.35;
    const floorH = 0.0;

    if (type === "corridor_ns" || type === "narrow_ns") {
      // Corridors: Flush against West (x = -1.18) and East (x = 1.18) inner wall faces
      spawnSafeLight("wall-switch", -1.18, wallH, -5.2, Math.PI / 2, "벽 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, 0.0, -Math.PI / 2, "벽 스위치");
      spawnSafeLight("wall-switch", -1.18, wallH, 5.2, Math.PI / 2, "벽 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "형광등 스위치");
    } else if (type === "corridor_ew") {
      // Corridors EW: Flush against North (z = -1.18) and South (z = 1.18) inner wall faces
      spawnSafeLight("wall-switch", -4.5, wallH, -1.18, Math.PI, "벽 스위치");
      spawnSafeLight("wall-switch", -3.5, wallH, 1.18, 0, "벽 스위치");
      spawnSafeLight("wall-switch", 4.5, wallH, -1.18, Math.PI, "벽 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, Math.PI / 2, "형광등 스위치");
    } else if (type === "cross_junction") {
      // 4-way cross junction: Flush on corner pillar faces (x = ±1.18)
      spawnSafeLight("wall-switch", -1.18, wallH, -2.5, Math.PI / 2, "교차로 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, -2.5, -Math.PI / 2, "교차로 스위치");
      spawnSafeLight("wall-switch", -1.18, wallH, 2.5, Math.PI / 2, "교차로 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, 2.5, -Math.PI / 2, "교차로 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "교차로 천장 스위치");
    } else if (type === "t_junction") {
      // T-junction: Flush on guide walls (x = ±1.18)
      if (chunk.cx === 2) {
        spawnSafeLight("wall-switch", 1.18, wallH, -4.5, -Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", 1.18, wallH, 0.0, -Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", 1.18, wallH, 4.5, -Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", -1.18, wallH, -2.5, Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", -1.18, wallH, 2.5, Math.PI / 2, "갈림길 스위치");
      } else {
        spawnSafeLight("wall-switch", -1.18, wallH, -4.5, Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", -1.18, wallH, 0.0, Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", -1.18, wallH, 4.5, Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", 1.18, wallH, -2.5, -Math.PI / 2, "갈림길 스위치");
        spawnSafeLight("wall-switch", 1.18, wallH, 2.5, -Math.PI / 2, "갈림길 스위치");
      }
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "갈림길 천장 스위치");
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
    } else if (type === "start") {
      // Start shrine: Sconces next to exit wings and foyer
      spawnSafeLight("wall-switch", -1.18, wallH, -5.5, Math.PI / 2, "북쪽 출구 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, 5.5, -Math.PI / 2, "남쪽 출구 스위치");
      spawnSafeLight("wall-switch", 5.5, wallH, 1.18, 0, "동쪽 출구 스위치");
      spawnSafeLight("wall-switch", -5.5, wallH, -1.18, Math.PI, "서쪽 출구 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH, -3.5, -Math.PI / 2, "북쪽 복도 스위치");
      spawnSafeLight("wall-switch", -1.18, wallH, 3.5, Math.PI / 2, "남쪽 복도 스위치");
      spawnSafeLight("floor-lamp", -3.2, floorH, -3.2, Math.PI * 0.25, "신당 낡은 스탠드");
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
      spawnSafeLight("wall-switch", -7.58, wallH + 5.0, -2.0, Math.PI / 2, "갤러리 벽 스위치");
      spawnSafeLight("floor-lamp", -5.5, floorH + 5.0, 4.5, Math.PI / 4, "갤러리 낡은 스탠드");
      spawnSafeLight("toy-lamp", -6.5, floorH + 5.0 + 0.85, 0.0, 0, "사당 제단 촛대");
    } else if (type === "stairs_b1") {
      // B1 Stairwell & Cellar/Nursery: Flush on north entrance wall, stairwell wall, landing, and nursery west wall
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "계단실 입구 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH - 2.5, 0.0, -Math.PI / 2, "지하 계단 스위치");
      spawnSafeLight("wall-switch", 1.18, wallH - 5.0, 6.0, -Math.PI / 2, "지하 착지점 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH - 5.0, -4.5, Math.PI / 2, "지하 보육실 벽 스위치");
      spawnSafeLight("floor-lamp", -4.0, floorH - 5.0, -1.0, Math.PI / 4, "보육실 낡은 스탠드");
      spawnSafeLight("floor-lamp", -5.5, floorH - 5.0, 4.5, -Math.PI / 4, "지하 창고 스탠드");
    } else if (type === "wide_room") {
      // Wide room: Flush on south entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "출구방 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "출구방 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "출구방 벽 스위치");
      spawnSafeLight("floor-lamp", 4.0, floorH, -4.0, -Math.PI / 4, "출구방 낡은 스탠드");
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
    const wallChance = isRoomLike ? 0.72 : 0.46;
    const floorChance = isRoomLike ? 0.82 : 0.28;
    const ceilingChance = type === "flicker_room" ? 0.82 : 0.38;

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
        [cx - 2.5, 5.0, cz - 5],
        [cx - 6.5, 5.0, cz + 0],
        [cx - 5.5, 5.0, cz - 5],
        [cx - 5.5, 5.0, cz + 5],
      ];
    } else if (type === "stairs_b1") {
      chunk.waypoints = [
        [cx + 0, 0.0, cz - 5],
        [cx + 0, -2.5, cz + 0],
        [cx + 0, -5.0, cz + 5],
        [cx - 1.5, -5.0, cz + 0],
        [cx - 5.5, -5.0, cz - 2],
        [cx - 5.5, -5.0, cz - 4.5],
        [cx - 5.5, -5.0, cz + 5],
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
