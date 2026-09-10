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
  "gymnasium",
  "courtyard",
  "auditorium",
  "foyer",
  "art_room",
  "studio",
  "broadcast",
  "darkroom",
  "greenroom",
  "home_ec",
  "club_room",
  "stairs_2f",
  "stairs_b1",
]);

function isClassroomType(type) {
  return type === "classroom" || type === "nurse_office" || type === "music_room"
    || type === "faculty_office" || type === "science_lab" || type === "gymnasium";
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

  getCylinderGeometry(radius, height, segments = 10) {
    const key = `cyl_${radius.toFixed(2)}_${height.toFixed(2)}_${segments}`;
    if (this.geometryCache.has(key)) {
      return this.geometryCache.get(key);
    }
    const geo = new THREE.CylinderGeometry(radius, radius, height, segments);
    this.geometryCache.set(key, geo);
    return geo;
  }

  getConeGeometry(radius, height, segments = 8) {
    const key = `cone_${radius.toFixed(2)}_${height.toFixed(2)}_${segments}`;
    if (this.geometryCache.has(key)) {
      return this.geometryCache.get(key);
    }
    const geo = new THREE.ConeGeometry(radius, height, segments);
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
      "2,-2": "storage",       // 준비물 창고 (key at center)

      "1,1": "corridor_ew",
      "2,1": "corridor_ns",
      "2,2": "workshop",       // 생활관 (empty crib)

      "0,1": "cross_junction", // Uncat Blackout Reveal
      "-1,1": "corridor_ew",
      "-2,1": "corridor_ns",
      "-2,2": "playroom",      // 인형교실 (key + doll at center)

      "-1,0": "corridor_ew",   // Weeping Angel Mannequin Intro
      "-2,0": "t_junction",    // 분실물 복도
      "-2,-1": "corridor_ns",
      "-2,-2": "archive",      // 폐관 도서실

      "1,-1": "flicker_room",
      "1,2": "stairs_b1",
      "-1,-1": "stairs_2f",
      "-1,2": "tatami_room",   // 예절실
      "0,2": "corridor_ns",    // 옥상 복도 (N+W, not a maze chicane)
      "-1,-2": "omen_room",
      "1,-2": "static_room",
      "5,-1": "nurse_office",
      "8,2": "music_room",
      "6,1": "faculty_office",
      "6,-2": "science_lab",
      "8,0": "gymnasium",
      "5,1": "courtyard",
      "8,1": "auditorium",
      "7,1": "foyer",
      "8,-1": "art_room",
      "8,-2": "studio",
      "4,2": "broadcast",
      "7,-2": "darkroom",
      "5,2": "greenroom",
      "4,-2": "home_ec",
      "7,-1": "club_room",
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
    // NS through-halls off the start parallel. Start (0,0) is a 3.4m school
    // cross so the first step east/south/west is the same corridor. The
    // 제단함 sits in the intersection (z=-1.12), inside the plus clear.
  getHallChicanes(cx, cz) {
    const openings = this.getOpenings(cx, cz);
    const isStart = cx === 0 && cz === 0;
    return {
      openings,
      ew: Boolean(openings.E && openings.W && (cx !== 0 || isStart)),
      ns: Boolean(openings.N && openings.S && (cz !== 0 || isStart || this.isLostFoundChunk(cx, cz))),
    };
  }

  // Chase-hide tiles stay a 3.4m school hall. Identity halls get their own
  // half-width so the 16m tile stops reading as one copied corridor shell.
  // T-stems still use this value (graph-centered) so neighbors keep a
  // walkable junction. Long-wall offsets live in getHallSides.
  getHallClear(cx, cz) {
    if ((cx === 0 && cz === 0) || (cx === 1 && cz === 0) || (cx === 0 && cz === 1) || (cx === 0 && cz === -1)) {
      return 1.7;
    }
    if (this.isEastWashChunk(cx, cz)) return 1.7;
    if (this.isTrophyChunk(cx, cz)) return 2.18;
    if (this.isAnnexGateChunk(cx, cz)) return 1.22;
    if (this.isWashFourChunk(cx, cz)) return 1.42;
    if (this.isAngelHallChunk(cx, cz)) return 1.48;
    if (this.isLaundryChunk(cx, cz)) return 1.22;
    if (this.isSpecimenChunk(cx, cz)) return 1.48;
    if (this.isNurseryHallChunk(cx, cz)) return 1.28;
    if (this.isStorageHallChunk(cx, cz)) return 1.38;
    if (this.isDollHallChunk(cx, cz)) return 1.52;
    if (this.isArchiveHallChunk(cx, cz)) return 1.18;
    if (this.isLabLinkChunk(cx, cz)) return 1.32;
    if (this.isTeaHallChunk(cx, cz)) return 1.45;
    if (this.isArcadeChunk(cx, cz)) return 1.58;
    if (this.isStageWingChunk(cx, cz)) return 1.92;
    return 1.7;
  }

  // Inner-face distance from the tile center to each long wall. Chase and
  // the east-wash cut-through stay a centered 3.4m. Everyone else can sit
  // the north wall closer than the south (or west closer than east) so the
  // open band still contains the graph spine at 0.
  getHallSides(cx, cz) {
    const c = this.getHallClear(cx, cz);
    const sides = { n: c, s: c, e: c, w: c };
    const authored = {
      "4,0": { n: 1.52, s: 1.05 },
      "7,0": { n: 1.58, s: 2.18 },
      "5,0": { n: 1.68, s: 1.18 },
      "-1,0": { n: 1.72, s: 1.22 },
      "6,2": { n: 1.55, s: 2.15 },
      "-1,1": { n: 1.68, s: 1.18 },
      "5,-2": { n: 1.12, s: 1.58 },
      "6,0": { n: 1.42, s: 2.12 },
      "3,0": { n: 1.32, s: 2.05 },
      "4,-1": { e: 1.05, w: 1.48 },
      "6,-1": { e: 1.18, w: 1.72 },
      "2,1": { e: 1.18, w: 1.55 },
      "2,-1": { e: 1.15, w: 1.62 },
      "-2,1": { e: 1.22, w: 1.78 },
      "-2,-1": { e: 0.98, w: 1.42 },
      "4,1": { e: 1.28, w: 1.82 },
      "-2,0": { e: 1.22, w: 1.88 },
      "1,1": { n: 1.58, s: 2.02, e: 1.42, w: 1.88 },
      "7,2": { n: 1.48, s: 2.02 },
      // Roof L (N+W): dual inner faces. North aisle width e+w = 3.40,
      // west aisle width n+s = 3.43. Walk |x|<1.6 / |z|<1.4 stays inside
      // with player radius 0.34.
      "0,2": { n: 1.25, s: 2.18, e: 2.08, w: 1.32 },
    };
    Object.assign(sides, authored[`${cx},${cz}`] || {});
    return sides;
  }

  getHallFluoroAlong(cx, cz) {
    const authored = {
      "4,0": [-5.15, 3.55],
      "7,0": [-3.45, 5.55],
      "5,0": [-5.55, 2.85],
      "-1,0": [-3.15, 5.85],
      "6,2": [-5.85, 0.65, 5.15],
      "6,0": [-5.45, -1.15, 4.85],
      "3,0": [-5.85, 0, 5.85],
      "2,1": [-3.65, 5.25],
      "4,-1": [-5.35, 3.15],
      "-2,0": [-5.65, 4.45],
      "1,1": [-3.25, 5.65],
      "4,1": [-5.05, 2.45, 6.15],
      "-2,-1": [-6.05, 3.85],
      "2,-1": [-4.55, 5.85],
      "-2,1": [-5.75, 3.25],
      "6,-1": [-3.95, 5.45],
      "-1,1": [-5.25, 4.15],
      "5,-2": [-6.15, 3.55],
      "7,2": [-5.55, 1.15, 5.85],
    };
    return authored[`${cx},${cz}`] || [-4.2, 4.2];
  }

  // Outer window pane centers along the window wall. East-wash keeps the
  // copied ±5.35 / ±3.15 rhythm so the cut-through classroom still reads.
  // Identity and glass halls shift the set so two neighboring tiles do not
  // share one four-pane cadence. T-spur window walls stay |along| >= 3.
  getHallWindowAlongs(cx, cz) {
    const authored = {
      "4,0": [-4.88, -1.62, 2.55],
      "7,0": [-6.12, -2.55, 0.85, 5.42],
      "5,0": [-4.68, 1.05, 5.28],
      "-1,0": [-5.82, -0.45, 4.15],
      "4,-1": [-5.05, 1.28, 4.72],
      "6,-1": [-4.42, 0.55, 5.18],
      "2,1": [-4.95, -1.22, 2.08],
      "2,-1": [-5.22, 0.92, 4.38],
      "-2,1": [-4.15, 1.55, 5.02],
      "-2,-1": [-5.55, -0.72, 3.65],
      "5,-2": [-3.88, 2.15, 5.55],
      "6,2": [-5.45, -1.05, 3.28],
      "3,0": [-6.05, -2.22, 1.48, 4.92],
      "6,0": [-5.72, -1.85, 2.08, 5.55],
      "7,2": [-4.25, 0.72, 5.85],
      "-1,1": [-5.12, 4.62],
      "4,1": [-4.28, 5.08],
    };
    return authored[`${cx},${cz}`] || [-5.35, -3.15, 3.15, 5.35];
  }

  // Classroom door centers along a wall. Chase tiles keep ±5.25 so hide and
  // the (1,0)→(2,0) cut-through stay on the verified aisle. Identity halls
  // shift the pair so the 16m shell stops using one copied door rhythm.
  getHallDoorAlong(cx, cz, side) {
    const authored = {
      "4,0": { n: [-4.95, 6.35] },
      "7,0": { s: [-6.45, 4.95] },
      "4,-1": { w: [-6.25, 4.95] },
      "6,-1": { w: [-6.35, 4.85] },
      "2,1": { w: [-6.35, 4.95] },
      "2,-1": { w: [-4.95, 6.35] },
      "-2,1": { w: [-6.45, 4.85] },
      "-2,-1": { w: [-4.85, 6.45] },
      "5,-2": { s: [-6.25, 4.95] },
      "-1,1": { n: [-6.15, 4.95] },
      "4,1": { w: [-4.95, 6.35] },
      "6,2": { n: [-6.45, 4.85] },
      "5,0": { n: [-4.95, 6.25] },
      "-1,0": { n: [-6.35, 4.95] },
      "2,0": { n: [-5.25, 6.35] },
      "-2,0": { w: [-6.15, 5.15] },
      "1,1": {
        n: [-4.75, 6.15],
        s: [-6.25, 4.85],
        e: [-5.55, 6.05],
        w: [-6.45, 4.75],
      },
    };
    const row = authored[`${cx},${cz}`];
    if (row && row[side] && row[side].length) return row[side];
    return [-5.25, 5.25];
  }

  hallDoorSideSign(alongs, index) {
    if (!alongs || alongs.length <= 1) return (alongs?.[0] || 0) < 0 ? 1 : -1;
    return index === 0 ? 1 : -1;
  }

  isClosedIdentityHall(cx, cz) {
    return this.isAnnexGateChunk(cx, cz)
      || this.isTrophyChunk(cx, cz)
      || this.isEastWashChunk(cx, cz)
      || this.isWashFourChunk(cx, cz)
      || this.isAngelHallChunk(cx, cz)
      || this.isLaundryChunk(cx, cz)
      || this.isSpecimenChunk(cx, cz)
      || this.isNurseryHallChunk(cx, cz)
      || this.isStorageHallChunk(cx, cz)
      || this.isDollHallChunk(cx, cz)
      || this.isArchiveHallChunk(cx, cz)
      || this.isLabLinkChunk(cx, cz)
      || this.isTeaHallChunk(cx, cz)
      || this.isArcadeChunk(cx, cz)
      || this.isStageWingChunk(cx, cz);
  }

  isSkybridgeChunk(cx, cz) {
    return cx === 3 && cz === 0;
  }

  isMemorialChunk(cx, cz) {
    return cx === 6 && cz === 0;
  }

  isTrophyChunk(cx, cz) {
    return cx === 7 && cz === 0;
  }

  isArcadeChunk(cx, cz) {
    return cx === 4 && cz === 1;
  }

  isSpecimenChunk(cx, cz) {
    return cx === 6 && cz === -1;
  }

  isStageWingChunk(cx, cz) {
    return cx === 6 && cz === 2;
  }

  isLaundryChunk(cx, cz) {
    return cx === 4 && cz === -1;
  }

  isLabLinkChunk(cx, cz) {
    return cx === 5 && cz === -2;
  }

  isStairHallChunk(cx, cz) {
    return cx === 1 && cz === 1;
  }

  isNurseryHallChunk(cx, cz) {
    return cx === 2 && cz === 1;
  }

  isDollHallChunk(cx, cz) {
    return cx === -2 && cz === 1;
  }

  isArchiveHallChunk(cx, cz) {
    return cx === -2 && cz === -1;
  }

  isStorageHallChunk(cx, cz) {
    return cx === 2 && cz === -1;
  }

  isTeaHallChunk(cx, cz) {
    return cx === -1 && cz === 1;
  }

  isLostFoundChunk(cx, cz) {
    return cx === -2 && cz === 0;
  }

  isStartHallChunk(cx, cz) {
    return cx === 0 && cz === 0;
  }

  isClassWingChunk(cx, cz) {
    return cx === 1 && cz === 0;
  }

  isUncatHallChunk(cx, cz) {
    return cx === 0 && cz === 1;
  }

  isNorthHallChunk(cx, cz) {
    return cx === 0 && cz === -1;
  }

  isRoofHallChunk(cx, cz) {
    return cx === 0 && cz === 2;
  }

  isEastWashChunk(cx, cz) {
    return cx === 2 && cz === 0;
  }

  isAngelHallChunk(cx, cz) {
    return cx === -1 && cz === 0;
  }

  isWashFourChunk(cx, cz) {
    return cx === 5 && cz === 0;
  }

  isPracticeChunk(cx, cz) {
    return cx === 7 && cz === 2;
  }

  isAnnexGateChunk(cx, cz) {
    return cx === 4 && cz === 0;
  }

  isGlassHallChunk(cx, cz) {
    return this.isSkybridgeChunk(cx, cz) || this.isMemorialChunk(cx, cz);
  }

  getHallWindowSide(cx, cz) {
    // Verified chase-hide plus tiles keep four classroom nooks.
    if ((cx === 0 && cz === 0) || (cx === 1 && cz === 0) || (cx === 0 && cz === 1) || (cx === 0 && cz === -1)) {
      return null;
    }
    if (this.isGlassHallChunk(cx, cz)) return "both";
    const chicanes = this.getHallChicanes(cx, cz);
    if (chicanes.ew && chicanes.ns) return null;
    if (chicanes.ew) {
      if (cx === 2 && cz === 0) return "s";
      if (cx === -1 && cz === 0) return "s";
      if (cx === 4 && cz === 0) return "s";
      if (cx === 5 && cz === 0) return "s";
      if (cx === 7 && cz === 0) return "n";
      if (cx === 7 && cz === 2) return "n";
      return (cx + cz) % 2 === 0 ? "s" : "n";
    }
    if (chicanes.ns) {
      return (cx + cz * 2) % 2 === 0 ? "e" : "w";
    }
    return null;
  }

  getHallNookMask(cx, cz) {
    const chicanes = this.getHallChicanes(cx, cz);
    const mask = { n: false, s: false, e: false, w: false };
    if (chicanes.ew) {
      mask.n = true;
      mask.s = true;
    }
    if (chicanes.ns && !chicanes.ew) {
      mask.e = true;
      mask.w = true;
    }
    if (chicanes.ew && chicanes.ns) {
      mask.n = mask.s = mask.e = mask.w = true;
    }
    const windowSide = this.getHallWindowSide(cx, cz);
    if (windowSide === "both") {
      mask.n = false;
      mask.s = false;
    } else if (windowSide) {
      mask[windowSide] = false;
    }
    return mask;
  }

  getHallNookKind(cx, cz, idx = 0) {
    // (1,0) and (0,0) keep verified classroom desk rows for chase-hide.
    if (cx === 1 && cz === 0) return "class";
    if (cx === 0 && cz === 0) return "class";
    const authored = {
      "-1,0": ["library", "shrine", "boarded", "music"],
      "2,0": ["class", "washroom", "shoes", "empty"],
      "0,1": ["shoes", "boarded", "music", "library"],
      "0,-1": ["shoes", "class", "washroom", "empty"],
      "4,0": ["library", "shoes", "boarded", "music"],
      "-2,0": ["empty", "shoes", "library", "music"],
      "5,0": ["shoes", "washroom", "science", "boarded"],
      "1,1": ["washroom", "shoes", "science", "empty"],
      "2,1": ["shoes", "library", "music", "empty"],
      "-2,1": ["shrine", "shoes", "library", "empty"],
      "-1,1": ["music", "shoes", "shrine", "empty"],
      "-2,-1": ["library", "shrine", "science", "empty"],
      "2,-1": ["science", "shoes", "empty", "washroom"],
      "4,1": ["shoes", "music", "empty", "library"],
      "4,-1": ["washroom", "shoes", "empty", "science"],
      "6,-1": ["science", "library", "shrine", "empty"],
      "6,2": ["music", "shoes", "empty", "shrine"],
      "5,-2": ["science", "empty", "shoes", "library"],
      "7,0": ["shrine", "library", "music", "empty"],
      "7,2": ["music", "shoes", "empty", "library"],
    };
    const row = authored[`${cx},${cz}`];
    if (row) return row[idx] || "shoes";
    const kinds = ["shoes", "library", "music", "boarded", "science", "washroom", "shrine", "empty"];
    return kinds[Math.abs(cx * 19 + cz * 13 + idx * 7) % kinds.length];
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
        const spotLight = new THREE.PointLight(0xffdfaa, 0.55, 3.6, 2.0);
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
          emissiveIntensity: 0.35,
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

      this.dressStairAtrium(chunk, chunkId, floorY, {
        x: f2StartX,
        halfWidth: rampHalfWidth,
        minZ: f2EndZ,
        maxZ: f2StartZ,
        railY: floorY + 5.0,
        glowY: floorY + 2.35,
        prefix: "atrium",
      });

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

      this.dressStairAtrium(chunk, chunkId, floorY, {
        x: b1StartX,
        halfWidth: rampHalfWidth,
        minZ: b1StartZ,
        maxZ: b1EndZ,
        railY: floorY,
        glowY: floorY - 2.35,
        prefix: "atrium_b1",
      });

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

    const addGappedPerimeter = (axis, sign, wide, prefix) => {
      const wallPos = sign * 7.8;
      const thickness = 0.4;
      const half = wide ? 1.7 : 1.2;
      const throughC = axis === "x" ? 6.65 : 6.9;
      const throughW = axis === "x" ? 1.5 : 1.3;
      const mask = this.getHallNookMask(chunk.cx, chunk.cz);
      const nmask = axis === "x"
        ? this.getHallNookMask(chunk.cx + sign, chunk.cz)
        : this.getHallNookMask(chunk.cx, chunk.cz + sign);
      const throughNeg = Boolean(wide && (axis === "x" ? mask.n && nmask.n : mask.w && nmask.w));
      const throughPos = Boolean(wide && (axis === "x" ? mask.s && nmask.s : mask.e && nmask.e));
      const gaps = [[-half, half]];
      if (throughNeg) gaps.push([-throughC - throughW / 2, -throughC + throughW / 2]);
      if (throughPos) gaps.push([throughC - throughW / 2, throughC + throughW / 2]);
      gaps.sort((a, b) => a[0] - b[0]);
      const merged = [];
      for (const gap of gaps) {
        const last = merged[merged.length - 1];
        if (last && gap[0] <= last[1] + 0.02) last[1] = Math.max(last[1], gap[1]);
        else merged.push([...gap]);
      }
      const ranges = [];
      const pushRange = (a, b, name) => {
        const len = b - a;
        if (len < 0.38) return;
        ranges.push({ mid: (a + b) / 2, len, name });
      };
      let cursor = -8.0;
      let part = 0;
      for (const [g0, g1] of merged) {
        pushRange(cursor, g0, `${prefix}_${part}`);
        part += 1;
        cursor = g1;
      }
      pushRange(cursor, 8.0, `${prefix}_${part}`);
      for (const piece of ranges) {
        if (axis === "z") {
          addWallSegment(piece.mid, wallPos, piece.len, thickness, piece.name);
        } else {
          addWallSegment(wallPos, piece.mid, thickness, piece.len, piece.name);
        }
      }
    };

    if (N) {
      addGappedPerimeter("z", -1, hallN, "n");
    } else {
      addWallSegment(0.0, -7.8, 16.0, 0.4, "n_solid");
    }

    if (S) {
      addGappedPerimeter("z", 1, hallS, "s");
    } else {
      addWallSegment(0.0, 7.8, 16.0, 0.4, "s_solid");
    }

    if (W) {
      addGappedPerimeter("x", -1, hallW, "w");
    } else {
      addWallSegment(-7.8, 0.0, 0.4, 16.0, "w_solid");
    }

    if (E) {
      addGappedPerimeter("x", 1, hallE, "e");
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
    // Roof hall is an L (N+W), not a leftover plus-alcove tile. Authored
    // roofhall_run_* walls in dressRoofHall replace these eight separators.
    if (hallLike && !maze && !this.isRoofHallChunk(chunk.cx, chunk.cz)) {
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
      if (isClassroomType(type) && type !== "gymnasium") {
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
    const isStart = chunk.cx === 0 && chunk.cz === 0;
    const walls = [];
    const add = (name, x, z, sx, sz, skip = false) => {
      if (!skip) walls.push([name, x, z, sx, sz]);
    };
    if (!maze && !isStart && !this.isRoofHallChunk(chunk.cx, chunk.cz)) {
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
    } else if (isStart) {
      this.dressStartFoyer(chunk, center, chunkId, floorY);
    }
    const gloom = new THREE.PointLight(0x4a3020, 0.12, 2.8, 2.2);
    gloom.position.set(
      center.x + (cabinetSE ? -5.4 : 5.4),
      floorY + 2.05,
      center.z + (skipNW ? 5.2 : -5.2),
    );
    gloom.name = `${chunkId}_hall_school_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    if (maze) {
      const gloom2 = new THREE.PointLight(0x2a1814, 0.08, 2.4, 2.2);
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
    this.ensureSchoolCorridorMaterials();
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
    this.addHallNookSign(
      chunk, chunkId, "counsel_sign",
      center.x - 7.52, floorY + 2.14, center.z,
      Math.PI / 2, "상담실",
    );
    const glow = new THREE.PointLight(0x201018, 0.5, 6.4, 2);
    glow.position.set(center.x, floorY + 2.12, center.z + 2.4);
    glow.name = `${chunkId}_counsel_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressStaticRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
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
      emissiveIntensity: 0.08,
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
    this.addHallNookSign(
      chunk, chunkId, "staticset_sign",
      center.x - 7.52, floorY + 2.14, center.z + 3.15,
      Math.PI / 2, "방송창고",
    );
    const glow = new THREE.PointLight(0x182028, 0.52, 6.4, 2);
    glow.position.set(center.x, floorY + 2.08, center.z + 2.2);
    glow.name = `${chunkId}_staticset_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressStartFoyer(chunk, center, chunkId, floorY) {
    // Plus foyer: keep the 제단함 in the north arm, but read as a school
    // crossing — floor stripe, dead fluorescents, no classroom walls.
    this.ensureSchoolCorridorMaterials();
    this.placeDressedBox(
      chunk, chunkId, "hall_stripe_ew",
      center.x, floorY + 0.012, center.z, 14.6, 0.02, 0.09,
      this.schoolStripeMat, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "hall_stripe_ns",
      center.x, floorY + 0.012, center.z, 0.09, 0.02, 14.6,
      this.schoolStripeMat, false,
    );
    for (const [name, x, z, sx, sz] of [
      ["hall_fluoro_e", 4.2, 0, 2.35, 0.14],
      ["hall_fluoro_w", -4.2, 0, 2.35, 0.14],
      ["hall_fluoro_s", 0, 4.2, 0.14, 2.35],
      ["hall_fluoro_n", 0, -4.2, 0.14, 2.35],
    ]) {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, floorY + 2.68, center.z + z, sx, 0.05, sz,
        this.schoolFluoroMat, false,
      );
    }
    this.dressStartHall(chunk, center, chunkId, floorY);
  }

  dressStartHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolRackMat) {
      this.schoolRackMat = new THREE.MeshStandardMaterial({
        color: 0x3a3228,
        roughness: 0.78,
        metalness: 0.08,
        emissive: 0x0c0804,
        emissiveIntensity: 0.05,
      });
    }
    for (const [name, x] of [["w", -4.55], ["e", 4.55]]) {
      this.addShoeRackUnit(
        chunk, chunkId, `starthall_rack_${name}`,
        center.x + x, floorY + 0.48, center.z + 1.52, Math.PI,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "starthall_sign",
      center.x, floorY + 2.12, center.z + 1.72,
      Math.PI, "현관",
    );
    const glow = new THREE.PointLight(0x201808, 0.48, 6.0, 2);
    glow.position.set(center.x + 3.4, floorY + 2.05, center.z + 1.15);
    glow.name = `${chunkId}_starthall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressClassWingHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolCartMat) {
      this.schoolCartMat = new THREE.MeshStandardMaterial({
        color: 0x3a342c,
        roughness: 0.74,
        metalness: 0.12,
        emissive: 0x0c0804,
        emissiveIntensity: 0.05,
      });
    }
    for (const [name, x] of [["w", -2.42], ["e", 2.42]]) {
      this.addHallCartUnit(
        chunk, chunkId, `classwing_cart_${name}`,
        center.x + x, floorY + 0.38, center.z - 1.52, 0,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "classwing_sign",
      center.x, floorY + 2.12, center.z - 1.72,
      0, "교실",
    );
    const glow = new THREE.PointLight(0x201808, 0.48, 6.0, 2);
    glow.position.set(center.x, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_classwing_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressUncatHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    for (const [name, z] of [["n", -5.35], ["s", 5.35]]) {
      this.addCautionTape(
        chunk, chunkId, `uncathall_tape_${name}`,
        center.x - 1.72, floorY + 1.42, center.z + z, Math.PI / 2,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "uncathall_sign",
      center.x - 1.72, floorY + 2.12, center.z,
      Math.PI / 2, "교차",
    );
    const glow = new THREE.PointLight(0x201410, 0.48, 6.0, 2);
    glow.position.set(center.x - 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_uncathall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressNorthHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    for (const [name, z] of [["n", -5.35], ["s", 5.35]]) {
      this.addTrafficConeUnit(
        chunk, chunkId, `northhall_cone_${name}`,
        center.x + 1.52, floorY + 0.52, center.z + z,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "northhall_sign",
      center.x + 1.72, floorY + 2.12, center.z,
      -Math.PI / 2, "북관",
    );
    const glow = new THREE.PointLight(0x201808, 0.48, 6.0, 2);
    glow.position.set(center.x + 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_northhall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressHallLockerBanks(chunk, center, chunkId, floorY, _openings, ewChicane, nsChicane) {
    // Identity halls with closed corner rooms keep their own props instead of
    // the shared 3.4m locker banks. Chase tiles (0,0)/(1,0) still dress lockers.
    if (this.isClosedIdentityHall(chunk.cx, chunk.cz) || this.isGlassHallChunk(chunk.cx, chunk.cz)
      || this.isPracticeChunk(chunk.cx, chunk.cz)) {
      return;
    }
    // Locker rows sit on the inner faces of the 3.4m school corridor.
    // Keep the T-spur (center ±1.7) and alcove doors (x/z ±5.25) clear.
    const cabinetMat = this.textures.createLockerMaterial();
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
    const sides = this.getHallSides(chunk.cx, chunk.cz);
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
    const mask = this.getHallNookMask(chunk.cx, chunk.cz);
    if (ewChicane) {
      if (mask.n) {
        const face = sides.n - depth / 2;
        placeBank("hall_lockers_n_w", -along, -face, bank, depth);
        placeBank("hall_lockers_n_e", along, -face, bank, depth);
      }
      if (mask.s && !this.isPracticeChunk(chunk.cx, chunk.cz)) {
        const face = sides.s - depth / 2;
        placeBank("hall_lockers_s_w", -along, face, bank, depth);
        placeBank("hall_lockers_s_e", along, face, bank, depth);
      }
    }
    if (nsChicane) {
      if (mask.w) {
        const face = sides.w - depth / 2;
        placeBank("hall_lockers_w_n", -face, -along, depth, bank);
        placeBank("hall_lockers_w_s", -face, along, depth, bank);
      }
      if (mask.e) {
        const face = sides.e - depth / 2;
        placeBank("hall_lockers_e_n", face, -along, depth, bank);
        placeBank("hall_lockers_e_s", face, along, depth, bank);
      }
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
    this.schoolPaMat = new THREE.MeshStandardMaterial({
      color: 0x2a2620,
      roughness: 0.68,
      metalness: 0.28,
    });
    this.schoolClockMat = new THREE.MeshStandardMaterial({
      color: 0xcfc6b0,
      roughness: 0.55,
      metalness: 0.08,
      emissive: 0x1a120c,
      emissiveIntensity: 0.08,
    });
    if (this.textures?.createDoorMaterial) {
      this.schoolDeskMat = this.textures.createDoorMaterial().clone();
      this.schoolDeskMat.color.setHex(0x8a6844);
      this.schoolDeskMat.roughness = 0.62;
    }
    this.schoolDeskDark = (this.schoolDeskMat || this.propMaterial).clone();
    this.schoolDeskDark.color.setHex(0x3d2818);
    this.schoolChairMat = (this.schoolDeskMat || this.propMaterial).clone();
    this.schoolChairMat.color.setHex(0x6e4e32);
    this.schoolMetalMat = new THREE.MeshStandardMaterial({
      color: 0x2c3034,
      roughness: 0.42,
      metalness: 0.58,
    });
    this.schoolChalkMat = new THREE.MeshStandardMaterial({
      color: 0xe8e0d0,
      roughness: 0.92,
      metalness: 0,
    });
    this.schoolClockHandMat = new THREE.MeshStandardMaterial({
      color: 0x1a1410,
      roughness: 0.7,
      metalness: 0.08,
    });
  }

  ensureSpecialNookMaterials() {
    this.ensureSchoolCorridorMaterials();
    if (this.schoolBookSpineMat) return;
    this.schoolShelfMat = (this.schoolDeskDark || this.trimMaterial).clone();
    this.schoolShelfMat.color.setHex(0x2c1c12);
    this.schoolBookSpineMat = this.createBookSpineMaterial(1);
    this.schoolPlywoodMat = this.createPlywoodMaterial();
    this.schoolTileMat = this.createWashTileMaterial();
    this.schoolPorcelainMat = new THREE.MeshStandardMaterial({
      color: 0xb8c0bc,
      roughness: 0.38,
      metalness: 0.08,
    });
    this.schoolStallDoorMat = new THREE.MeshStandardMaterial({
      color: 0x3a4238,
      roughness: 0.62,
      metalness: 0.18,
    });
    this.schoolCautionMat = this.createCautionTapeMaterial();
    this.schoolBookMats = [0x4a2018, 0x1a3048, 0x3a2a10, 0x5a3020, 0x243028, 0x2c2438].map((hex) => (
      new THREE.MeshStandardMaterial({
        color: hex,
        roughness: 0.72,
        metalness: 0.04,
        emissive: hex,
        emissiveIntensity: 0.07,
      })
    ));
    this.schoolWetMat = new THREE.MeshStandardMaterial({
      color: 0x1a2422,
      roughness: 0.22,
      metalness: 0.18,
      transparent: true,
      opacity: 0.48,
      depthWrite: false,
    });
    this.schoolChalkLabelMat = this.createChalkLabelMaterial("출석 금지");
  }

  addSchoolProp(chunk, object) {
    this.scene.add(object);
    chunk.meshes.push(object);
    return object;
  }

  addSchoolDeskGroup(chunk, chunkId, name, x, y, z, yaw, collide, kind = "student") {
    this.ensureSchoolCorridorMaterials();
    const wood = this.schoolDeskMat || this.propMaterial;
    const dark = this.schoolDeskDark || this.trimMaterial;
    const metal = this.schoolMetalMat || this.trimMaterial;
    const [cw, ch, cd] = collide;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = name;
    const teacher = kind === "teacher";
    const topW = teacher ? Math.max(cw - 0.04, 0.88) : 0.62;
    const topD = teacher ? Math.max(cd - 0.04, 0.46) : 0.42;
    const topY = ch / 2 - 0.028;
    const top = new THREE.Mesh(this.getBoxGeometry(topW, 0.05, topD), wood);
    top.position.y = topY;
    top.castShadow = true;
    top.receiveShadow = true;
    group.add(top);
    const apron = new THREE.Mesh(this.getBoxGeometry(topW * 0.9, 0.07, 0.04), dark);
    apron.position.set(0, topY - 0.055, topD * 0.4);
    group.add(apron);
    const legH = ch - 0.06;
    const insetX = topW * 0.42;
    const insetZ = topD * 0.38;
    for (const [lx, lz] of [[-insetX, -insetZ], [insetX, -insetZ], [-insetX, insetZ], [insetX, insetZ]]) {
      const leg = new THREE.Mesh(this.getBoxGeometry(0.046, legH, 0.046), metal);
      leg.position.set(lx, -ch / 2 + legH / 2, lz);
      leg.castShadow = true;
      group.add(leg);
    }
    const groove = new THREE.Mesh(this.getBoxGeometry(topW * 0.72, 0.012, 0.032), dark);
    groove.position.set(0, topY + 0.028, -topD * 0.3);
    group.add(groove);
    if (teacher) {
      const drawer = new THREE.Mesh(this.getBoxGeometry(topW * 0.42, 0.12, topD * 0.72), dark);
      drawer.position.set(-topW * 0.18, topY - 0.12, 0);
      group.add(drawer);
    }
    this.addSchoolProp(chunk, group);
    this.collisionWorld.addStaticBox(name, group.position, new THREE.Vector3(cw, ch, cd), chunkId);
    return group;
  }

  addSchoolChairGroup(chunk, chunkId, name, x, y, z, yaw, options = {}) {
    this.ensureSchoolCorridorMaterials();
    const wood = this.schoolChairMat || this.trimMaterial;
    const metal = this.schoolMetalMat || this.trimMaterial;
    const collideH = options.collideH || 0.42;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    if (options.fallen) {
      group.rotation.set(1.15, yaw, 0.2);
    } else {
      group.rotation.y = yaw;
    }
    group.name = name;
    const seat = new THREE.Mesh(this.getBoxGeometry(0.36, 0.04, 0.34), wood);
    seat.position.y = 0.02;
    seat.castShadow = true;
    group.add(seat);
    const back = new THREE.Mesh(this.getBoxGeometry(0.36, 0.38, 0.04), wood);
    back.position.set(0, 0.22, -0.16);
    back.castShadow = true;
    group.add(back);
    const inset = 0.13;
    const legH = Math.max(0.36, collideH - 0.04);
    for (const [lx, lz] of [[-inset, -inset], [inset, -inset], [-inset, inset], [inset, inset]]) {
      const leg = new THREE.Mesh(this.getBoxGeometry(0.036, legH, 0.036), metal);
      leg.position.set(lx, -collideH / 2 + legH / 2, lz);
      group.add(leg);
    }
    this.addSchoolProp(chunk, group);
    if (options.collide) {
      const [cw, ch, cd] = options.collide;
      this.collisionWorld.addStaticBox(name, group.position, new THREE.Vector3(cw, ch, cd), chunkId);
    }
    return group;
  }

  addChalkboardGroup(chunk, chunkId, name, x, y, z, yaw, width = 1.65, height = 0.95) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBoardMat) {
      this.schoolBoardMat = new THREE.MeshStandardMaterial({
        color: 0x1a2a1c,
        roughness: 0.88,
        metalness: 0.04,
      });
    }
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = name;
    const frame = new THREE.Mesh(
      this.getBoxGeometry(width + 0.14, height + 0.16, 0.05),
      this.schoolDeskDark || this.trimMaterial,
    );
    group.add(frame);
    const board = new THREE.Mesh(this.getBoxGeometry(width, height, 0.04), this.schoolBoardMat);
    board.position.z = 0.014;
    group.add(board);
    const tray = new THREE.Mesh(
      this.getBoxGeometry(width + 0.1, 0.045, 0.09),
      this.schoolDeskDark || this.trimMaterial,
    );
    tray.position.set(0, -height * 0.54, 0.03);
    group.add(tray);
    const chalk = new THREE.Mesh(this.getBoxGeometry(0.12, 0.018, 0.018), this.schoolChalkMat || this.schoolPaperMat);
    chalk.position.set(-width * 0.28, -height * 0.52, 0.06);
    group.add(chalk);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addHallClockGroup(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSchoolCorridorMaterials();
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const rim = new THREE.Mesh(this.getBoxGeometry(0.34, 0.34, 0.05), this.schoolClockMat);
    group.add(rim);
    const face = new THREE.Mesh(this.getBoxGeometry(0.26, 0.26, 0.02), this.schoolPaperMat);
    face.position.z = 0.022;
    group.add(face);
    const hub = new THREE.Mesh(this.getBoxGeometry(0.03, 0.03, 0.02), this.schoolClockHandMat);
    hub.position.z = 0.036;
    group.add(hub);
    const hour = new THREE.Mesh(this.getBoxGeometry(0.028, 0.08, 0.012), this.schoolClockHandMat);
    hour.position.set(0.018, 0.022, 0.036);
    hour.rotation.z = 0.7;
    group.add(hour);
    const minute = new THREE.Mesh(this.getBoxGeometry(0.018, 0.11, 0.012), this.schoolClockHandMat);
    minute.position.set(-0.02, 0.03, 0.038);
    minute.rotation.z = -0.35;
    group.add(minute);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addHallPaGroup(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSchoolCorridorMaterials();
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const body = new THREE.Mesh(this.getBoxGeometry(0.28, 0.16, 0.1), this.schoolPaMat);
    group.add(body);
    const grill = new THREE.Mesh(this.getBoxGeometry(0.2, 0.1, 0.02), this.schoolClockMat);
    grill.position.z = 0.052;
    group.add(grill);
    const mount = new THREE.Mesh(this.getBoxGeometry(0.08, 0.05, 0.06), this.schoolMetalMat);
    mount.position.z = -0.07;
    group.add(mount);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addHallNookSign(chunk, chunkId, name, x, y, z, yaw, label) {
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const back = new THREE.Mesh(
      this.getBoxGeometry(0.66, 0.22, 0.03),
      this.schoolDeskDark || this.trimMaterial,
    );
    group.add(back);
    const plate = new THREE.Mesh(this.getPlaneGeometry(0.62, 0.18), this.createSignMaterial(label));
    plate.position.z = 0.02;
    group.add(plate);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addLibraryShelfUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSpecialNookMaterials();
    const width = 0.94;
    const height = 1.66;
    const depth = 0.3;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_hall_books_${name}`;
    const wood = this.schoolShelfMat;
    const back = new THREE.Mesh(this.getBoxGeometry(width, height, 0.035), wood);
    back.position.z = -depth / 2 + 0.018;
    group.add(back);
    for (const sx of [-1, 1]) {
      const side = new THREE.Mesh(this.getBoxGeometry(0.035, height, depth), wood);
      side.position.x = sx * (width / 2 - 0.018);
      group.add(side);
    }
    for (const yy of [height / 2 - 0.018, -height / 2 + 0.018]) {
      const cap = new THREE.Mesh(this.getBoxGeometry(width, 0.035, depth), wood);
      cap.position.y = yy;
      group.add(cap);
    }
    for (let i = 1; i <= 3; i += 1) {
      const board = new THREE.Mesh(this.getBoxGeometry(width - 0.06, 0.028, depth - 0.04), wood);
      board.position.y = -height / 2 + i * (height / 4);
      group.add(board);
    }
    const spines = new THREE.Mesh(
      this.getBoxGeometry(width - 0.1, height - 0.14, 0.05),
      this.schoolBookSpineMat,
    );
    spines.position.z = depth / 2 - 0.04;
    spines.name = `${chunkId}_hall_books_${name}`;
    group.add(spines);
    for (let i = 0; i < 4; i += 1) {
      const book = new THREE.Mesh(
        this.getBoxGeometry(0.04, 0.16 + (i % 3) * 0.03, 0.15),
        this.schoolBookMats[i % this.schoolBookMats.length],
      );
      book.position.set(-0.32 + i * 0.2, -0.18 + (i % 4) * 0.26, depth / 2 + 0.02);
      group.add(book);
    }
    this.addSchoolProp(chunk, group);
    return group;
  }

  addLooseBooks(chunk, x, y, z, yaw, seed = 0) {
    this.ensureSpecialNookMaterials();
    const pile = new THREE.Group();
    pile.position.set(x, y, z);
    pile.rotation.y = yaw;
    pile.name = `${chunk.chunkId}_hall_books_loose_${seed}`;
    for (let i = 0; i < 5; i += 1) {
      const book = new THREE.Mesh(
        this.getBoxGeometry(0.15 + (i % 2) * 0.04, 0.028, 0.21),
        this.schoolBookMats[(i + seed) % this.schoolBookMats.length],
      );
      book.position.set((i % 3) * 0.04 - 0.04, i * 0.03, (i % 2) * 0.03);
      book.rotation.y = 0.08 * i;
      pile.add(book);
    }
    this.addSchoolProp(chunk, pile);
    return pile;
  }

  addShoeRackUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    const wood = this.schoolRackMat || this.schoolDeskDark || this.trimMaterial;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const frame = new THREE.Mesh(this.getBoxGeometry(1.12, 0.96, 0.36), wood);
    group.add(frame);
    for (const yy of [-0.28, 0.02, 0.32]) {
      const shelf = new THREE.Mesh(this.getBoxGeometry(1.02, 0.03, 0.32), wood);
      shelf.position.y = yy;
      group.add(shelf);
    }
    const shoeColors = [0x1a1410, 0x2a1814, 0x3a2418, 0x221810];
    for (let row = 0; row < 3; row += 1) {
      for (let col = 0; col < 4; col += 1) {
        const shoe = new THREE.Mesh(
          this.getBoxGeometry(0.2, 0.08, 0.11),
          new THREE.MeshStandardMaterial({
            color: shoeColors[(row + col) % shoeColors.length],
            roughness: 0.86,
          }),
        );
        shoe.position.set(-0.38 + col * 0.25, -0.22 + row * 0.3, 0.14);
        shoe.name = `${chunkId}_${name}_shoe_${row}${col}`;
        group.add(shoe);
      }
    }
    this.addSchoolProp(chunk, group);
    this.collisionWorld.addStaticBox(group.name, group.position, new THREE.Vector3(1.12, 0.96, 0.38), chunkId);
    return group;
  }

  addHallCartUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSchoolCorridorMaterials();
    const metal = this.schoolMetalMat || this.trimMaterial;
    const dark = this.schoolDeskDark || this.trimMaterial;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const body = new THREE.Mesh(this.getBoxGeometry(0.72, 0.08, 0.48), metal);
    body.position.y = 0.12;
    group.add(body);
    const shelf = new THREE.Mesh(this.getBoxGeometry(0.7, 0.04, 0.46), dark);
    shelf.position.y = -0.18;
    group.add(shelf);
    for (const [lx, lz] of [[-0.28, -0.16], [0.28, -0.16], [-0.28, 0.16], [0.28, 0.16]]) {
      const post = new THREE.Mesh(this.getBoxGeometry(0.04, 0.62, 0.04), metal);
      post.position.set(lx, -0.05, lz);
      group.add(post);
      const wheel = new THREE.Mesh(this.getCylinderGeometry(0.055, 0.04, 8), metal);
      wheel.rotation.z = Math.PI / 2;
      wheel.position.set(lx, -0.34, lz);
      group.add(wheel);
    }
    const crate = new THREE.Mesh(this.getBoxGeometry(0.42, 0.22, 0.28), this.schoolPaperMat || dark);
    crate.position.set(0.04, 0.28, 0);
    crate.name = `${chunkId}_${name}_tray`;
    group.add(crate);
    this.addSchoolProp(chunk, group);
    this.collisionWorld.addStaticBox(group.name, group.position, new THREE.Vector3(0.78, 0.76, 0.52), chunkId);
    return group;
  }

  addTrafficConeUnit(chunk, chunkId, name, x, y, z) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolConeMat) {
      this.schoolConeMat = new THREE.MeshStandardMaterial({
        color: 0xb45a18,
        roughness: 0.55,
        metalness: 0.08,
        emissive: 0x2a1204,
        emissiveIntensity: 0.08,
      });
    }
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.name = `${chunkId}_${name}`;
    const base = new THREE.Mesh(this.getBoxGeometry(0.42, 0.06, 0.42), this.schoolMetalMat || this.trimMaterial);
    base.position.y = -0.48;
    group.add(base);
    const cone = new THREE.Mesh(this.getConeGeometry(0.16, 0.92, 8), this.schoolConeMat);
    cone.position.y = 0.02;
    group.add(cone);
    const stripe = new THREE.Mesh(this.getBoxGeometry(0.22, 0.06, 0.22), this.schoolPaperMat || this.trimMaterial);
    stripe.position.y = -0.08;
    group.add(stripe);
    this.addSchoolProp(chunk, group);
    this.collisionWorld.addStaticBox(group.name, group.position, new THREE.Vector3(0.52, 1.04, 0.52), chunkId);
    return group;
  }

  addBoilerDrumUnit(chunk, chunkId, name, x, y, z) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBoilerMat) {
      this.schoolBoilerMat = new THREE.MeshStandardMaterial({
        color: 0x2a322c,
        roughness: 0.62,
        metalness: 0.28,
        emissive: 0x081410,
        emissiveIntensity: 0.06,
      });
    }
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.name = `${chunkId}_${name}`;
    const drum = new THREE.Mesh(this.getCylinderGeometry(0.36, 1.12, 12), this.schoolBoilerMat);
    group.add(drum);
    const lid = new THREE.Mesh(this.getCylinderGeometry(0.38, 0.06, 12), this.schoolMetalMat || this.schoolBoilerMat);
    lid.position.y = 0.56;
    group.add(lid);
    const valve = new THREE.Mesh(this.getBoxGeometry(0.18, 0.12, 0.18), this.schoolMetalMat || this.trimMaterial);
    valve.position.set(0.28, 0.22, 0);
    group.add(valve);
    const gauge = new THREE.Mesh(this.getCylinderGeometry(0.07, 0.04, 10), this.schoolGlassMat || this.trimMaterial);
    gauge.rotation.z = Math.PI / 2;
    gauge.position.set(0.38, 0.22, 0);
    group.add(gauge);
    this.addSchoolProp(chunk, group);
    this.collisionWorld.addStaticBox(group.name, group.position, new THREE.Vector3(0.78, 1.16, 0.78), chunkId);
    return group;
  }

  addPortraitFrameUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBloodFrameMat) {
      this.schoolBloodFrameMat = new THREE.MeshStandardMaterial({
        color: 0x3a1818,
        roughness: 0.72,
        metalness: 0.08,
        emissive: 0x1a0608,
        emissiveIntensity: 0.08,
      });
    }
    if (!this.schoolPortraitMat) {
      this.schoolPortraitMat = new THREE.MeshStandardMaterial({
        color: 0x1a1210,
        roughness: 0.88,
        metalness: 0,
        emissive: 0x100808,
        emissiveIntensity: 0.06,
      });
    }
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const frame = new THREE.Mesh(this.getBoxGeometry(0.92, 1.28, 0.06), this.schoolBloodFrameMat);
    group.add(frame);
    const canvas = new THREE.Mesh(this.getBoxGeometry(0.72, 1.02, 0.03), this.schoolPortraitMat);
    canvas.position.z = 0.03;
    group.add(canvas);
    const glass = new THREE.Mesh(this.getBoxGeometry(0.74, 1.04, 0.02), this.schoolGlassMat || this.trimMaterial);
    glass.position.z = 0.05;
    group.add(glass);
    this.addSchoolProp(chunk, group);
    return group;
  }

  dressMazePartitionTrim(chunk, chunkId, name, x, y, z, sx, sz, height) {
    this.ensureSchoolCorridorMaterials();
    const longX = sx >= sz;
    const span = longX ? sx : sz;
    if (span < 2.6) return;
    const paneCount = Math.min(5, Math.max(2, Math.floor(span / 2.15)));
    const paneW = Math.min(0.62, span / (paneCount + 1.15));
    const glass = this.schoolGlassMat || this.trimMaterial;
    const rail = this.schoolDeskDark || this.trimMaterial;
    const railMesh = new THREE.Mesh(
      this.getBoxGeometry(longX ? span * 0.96 : 0.06, 0.08, longX ? 0.06 : span * 0.96),
      rail,
    );
    railMesh.position.set(x, y - height * 0.18, z);
    railMesh.name = `${chunkId}_${name}_rail`;
    this.addSchoolProp(chunk, railMesh);
    for (let i = 0; i < paneCount; i += 1) {
      const t = (i + 1) / (paneCount + 1) - 0.5;
      const px = longX ? x + t * span * 0.86 : x;
      const pz = longX ? z : z + t * span * 0.86;
      const pane = new THREE.Mesh(
        this.getBoxGeometry(longX ? paneW : 0.04, 0.42, longX ? 0.04 : paneW),
        glass,
      );
      pane.position.set(px, y + 0.22, pz);
      pane.name = `${chunkId}_${name}_pane_${i}`;
      this.addSchoolProp(chunk, pane);
    }
  }

  addShoeCubbyUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSpecialNookMaterials();
    const wood = this.schoolShelfMat || this.schoolDeskDark || this.trimMaterial;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const back = new THREE.Mesh(this.getBoxGeometry(0.94, 1.55, 0.04), wood);
    back.position.z = -0.16;
    group.add(back);
    for (let row = 0; row < 4; row += 1) {
      const shelf = new THREE.Mesh(this.getBoxGeometry(0.9, 0.03, 0.32), wood);
      shelf.position.y = -0.68 + row * 0.44;
      group.add(shelf);
      for (let col = 0; col < 3; col += 1) {
        const shoe = new THREE.Mesh(
          this.getBoxGeometry(0.2, 0.07, 0.12),
          this.schoolDeskDark || this.trimMaterial,
        );
        shoe.position.set(-0.28 + col * 0.28, -0.58 + row * 0.44, 0.08);
        shoe.name = `${chunkId}_hall_shoe_${name}_${row}${col}`;
        group.add(shoe);
      }
    }
    this.addSchoolProp(chunk, group);
    return group;
  }

  addMusicStandUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSchoolCorridorMaterials();
    const metal = this.schoolMetalMat || this.trimMaterial;
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const pole = new THREE.Mesh(this.getBoxGeometry(0.04, 1.15, 0.04), metal);
    pole.position.y = 0.1;
    group.add(pole);
    const desk = new THREE.Mesh(this.getBoxGeometry(0.42, 0.02, 0.28), metal);
    desk.position.set(0, 0.62, 0.06);
    desk.rotation.x = -0.45;
    group.add(desk);
    const sheet = new THREE.Mesh(this.getBoxGeometry(0.28, 0.01, 0.2), this.schoolPaperMat || this.trimMaterial);
    sheet.position.set(0, 0.64, 0.07);
    sheet.rotation.x = -0.45;
    group.add(sheet);
    this.addSchoolProp(chunk, group);
    this.collisionWorld.addStaticBox(group.name, group.position, new THREE.Vector3(0.42, 1.2, 0.36), chunkId);
    return group;
  }

  addSpecimenJarUnit(chunk, chunkId, name, x, y, z) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolJarMat) {
      this.schoolJarMat = new THREE.MeshStandardMaterial({
        color: 0x243830,
        roughness: 0.22,
        metalness: 0.12,
        transparent: true,
        opacity: 0.55,
        emissive: 0x081410,
        emissiveIntensity: 0.08,
      });
    }
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.name = `${chunkId}_${name}`;
    const jar = new THREE.Mesh(this.getCylinderGeometry(0.09, 0.28, 10), this.schoolJarMat);
    group.add(jar);
    const lid = new THREE.Mesh(this.getCylinderGeometry(0.1, 0.04, 10), this.schoolMetalMat || this.trimMaterial);
    lid.position.y = 0.15;
    group.add(lid);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addWashStallUnit(chunk, chunkId, name, x, y, z, yaw, index) {
    this.ensureSpecialNookMaterials();
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_hall_stall_door_${name}`;
    const divider = new THREE.Mesh(this.getBoxGeometry(0.04, 1.48, 0.84), this.trimMaterial);
    divider.position.x = 0.36;
    group.add(divider);
    if (index === 0) {
      const start = new THREE.Mesh(this.getBoxGeometry(0.04, 1.48, 0.84), this.trimMaterial);
      start.position.x = -0.36;
      group.add(start);
    }
    const door = new THREE.Mesh(this.getBoxGeometry(0.64, 1.22, 0.03), this.schoolStallDoorMat);
    door.position.set(0, -0.02, 0.41);
    door.name = `${chunkId}_hall_stall_door_${name}`;
    group.add(door);
    const gap = new THREE.Mesh(this.getBoxGeometry(0.62, 0.04, 0.02), this.schoolMetalMat);
    gap.position.set(0, -0.66, 0.41);
    group.add(gap);
    const handle = new THREE.Mesh(this.getBoxGeometry(0.04, 0.08, 0.05), this.schoolMetalMat);
    handle.position.set(0.22, 0.04, 0.45);
    group.add(handle);
    const plate = new THREE.Mesh(
      this.getBoxGeometry(0.08, 0.08, 0.02),
      new THREE.MeshStandardMaterial({
        color: index === 1 ? 0x6a1818 : 0x2a4a28,
        roughness: 0.55,
        emissive: index === 1 ? 0x3a0808 : 0x081808,
        emissiveIntensity: 0.2,
      }),
    );
    plate.position.set(-0.18, 0.38, 0.44);
    group.add(plate);
    const tank = new THREE.Mesh(this.getBoxGeometry(0.32, 0.28, 0.16), this.schoolPorcelainMat);
    tank.position.set(0, 0.12, -0.28);
    group.add(tank);
    const bowl = new THREE.Mesh(this.getBoxGeometry(0.28, 0.16, 0.34), this.schoolPorcelainMat);
    bowl.position.set(0, -0.42, -0.12);
    group.add(bowl);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addWashSinkUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSpecialNookMaterials();
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const basin = new THREE.Mesh(this.getBoxGeometry(0.46, 0.08, 0.32), this.schoolPorcelainMat);
    group.add(basin);
    const bowl = new THREE.Mesh(this.getBoxGeometry(0.28, 0.06, 0.18), this.schoolWetMat);
    bowl.position.y = 0.05;
    group.add(bowl);
    const pedestal = new THREE.Mesh(this.getBoxGeometry(0.12, 0.42, 0.12), this.schoolPorcelainMat);
    pedestal.position.y = -0.25;
    group.add(pedestal);
    const faucet = new THREE.Mesh(this.getBoxGeometry(0.04, 0.16, 0.04), this.schoolMetalMat);
    faucet.position.set(0, 0.14, -0.08);
    group.add(faucet);
    const spout = new THREE.Mesh(this.getBoxGeometry(0.04, 0.03, 0.12), this.schoolMetalMat);
    spout.position.set(0, 0.2, -0.02);
    group.add(spout);
    this.addSchoolProp(chunk, group);
    return group;
  }

  addBoardedWindowUnit(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSpecialNookMaterials();
    const group = new THREE.Group();
    group.position.set(x, y, z);
    group.rotation.y = yaw;
    group.name = `${chunkId}_${name}`;
    const glass = new THREE.Mesh(this.getBoxGeometry(0.78, 0.78, 0.03), this.schoolGlassMat);
    glass.position.z = -0.02;
    group.add(glass);
    const planks = [
      [0.82, 0.2, 0.05, 0, 0.22, 0.08],
      [0.82, 0.2, 0.05, 0, -0.06, -0.06],
      [0.82, 0.2, 0.05, 0, -0.28, 0.1],
      [0.22, 0.82, 0.04, -0.12, 0, 0.55],
    ];
    for (const [sx, sy, sz, px, py, rotZ] of planks) {
      const plank = new THREE.Mesh(this.getBoxGeometry(sx, sy, sz), this.schoolPlywoodMat);
      plank.position.set(px, py, 0.03);
      plank.rotation.z = rotZ;
      group.add(plank);
    }
    for (const [nx, ny] of [[-0.28, 0.28], [0.26, -0.22], [0.08, 0.02], [-0.18, -0.3]]) {
      const nail = new THREE.Mesh(this.getBoxGeometry(0.03, 0.03, 0.04), this.schoolMetalMat);
      nail.position.set(nx, ny, 0.06);
      group.add(nail);
    }
    this.addSchoolProp(chunk, group);
    return group;
  }

  addCautionTape(chunk, chunkId, name, x, y, z, yaw) {
    this.ensureSpecialNookMaterials();
    const tape = new THREE.Mesh(this.getBoxGeometry(1.55, 0.07, 0.02), this.schoolCautionMat);
    tape.position.set(x, y, z);
    tape.rotation.set(0.12, yaw, 0.38);
    tape.name = `${chunkId}_${name}`;
    this.addSchoolProp(chunk, tape);
    return tape;
  }

  dressSchoolCorridor(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane) {
    // Authored-width school corridor through the tile center. Classroom walls
    // at the per-side clear edge, hide-alcove doors from getHallDoorAlong,
    // T-spurs on graph turns. Offset N/S or E/W walls so the 16m shell is
    // not a copied ±clear rectangle.
    this.ensureSchoolCorridorMaterials();
    const clear = this.getHallClear(chunk.cx, chunk.cz);
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const t = 0.28;
    const nPos = sides.n + t / 2;
    const sPos = sides.s + t / 2;
    const ePos = sides.e + t / 2;
    const wPos = sides.w + t / 2;
    const stemHalf = clear + t / 2;
    const y = floorY + 1.4;
    const h = 2.8;
    const doorW = 2.2;
    const doorsOf = (side) => this.getHallDoorAlong(chunk.cx, chunk.cz, side);
    const spanMin = -7.45;
    const spanMax = 7.45;
    const stemEnd = 7.52;
    const nsThrough = nsChicane
      ? { center: (sides.e - sides.w) / 2, width: sides.e + sides.w }
      : { center: 0, width: clear * 2 };
    const ewThrough = ewChicane && nsChicane
      ? { center: (sides.s - sides.n) / 2, width: sides.n + sides.s }
      : { center: 0, width: clear * 2 };

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
    const stemRun = (startPos) => {
      const len = stemEnd - startPos;
      return { len, mid: (startPos + stemEnd) / 2 };
    };

    const mask = this.getHallNookMask(chunk.cx, chunk.cz);
    const practice = this.isPracticeChunk(chunk.cx, chunk.cz);

    if (ewChicane) {
      const nGaps = [];
      const sGaps = [];
      if (mask.n) {
        for (const along of doorsOf("n")) nGaps.push({ center: along, width: doorW });
      }
      if (mask.s && !practice) {
        for (const along of doorsOf("s")) sGaps.push({ center: along, width: doorW });
      }
      if (nsChicane || openings.N) nGaps.push(nsThrough);
      if ((nsChicane || openings.S) && !practice) sGaps.push(nsThrough);
      gapped("hall_class_n", "x", -nPos, nGaps);
      if (!practice) gapped("hall_class_s", "x", sPos, sGaps);
      if (!nsChicane && openings.N) {
        const run = stemRun(nPos);
        stem("hall_class_stem_n_w", -stemHalf, -run.mid, t, run.len);
        stem("hall_class_stem_n_e", stemHalf, -run.mid, t, run.len);
      }
      if (!nsChicane && openings.S) {
        const run = stemRun(sPos);
        stem("hall_class_stem_s_w", -stemHalf, run.mid, t, run.len);
        stem("hall_class_stem_s_e", stemHalf, run.mid, t, run.len);
      }
      if (!nsChicane && !openings.N) {
        const run = stemRun(nPos);
        stem("hall_class_split_n", 0, -run.mid, t, run.len);
      }
      if (!nsChicane && !openings.S && !practice) {
        const run = stemRun(sPos);
        stem("hall_class_split_s", 0, run.mid, t, run.len);
      }
    }

    if (nsChicane) {
      const wGaps = [];
      const eGaps = [];
      if (ewChicane || openings.W) wGaps.push(ewThrough);
      if (ewChicane || openings.E) eGaps.push(ewThrough);
      if (!ewChicane && mask.w) {
        for (const along of doorsOf("w")) wGaps.push({ center: along, width: doorW });
      }
      if (!ewChicane && mask.e) {
        for (const along of doorsOf("e")) eGaps.push({ center: along, width: doorW });
      }
      gapped("hall_class_w", "z", -wPos, wGaps);
      gapped("hall_class_e", "z", ePos, eGaps);
      if (!ewChicane && openings.W) {
        const run = stemRun(wPos);
        stem("hall_class_stem_w_n", -run.mid, -stemHalf, run.len, t);
        stem("hall_class_stem_w_s", -run.mid, stemHalf, run.len, t);
      }
      if (!ewChicane && openings.E) {
        const run = stemRun(ePos);
        stem("hall_class_stem_e_n", run.mid, -stemHalf, run.len, t);
        stem("hall_class_stem_e_s", run.mid, stemHalf, run.len, t);
      }
      if (!ewChicane && !openings.W) {
        const run = stemRun(wPos);
        stem("hall_class_split_w", -run.mid, 0, run.len, t);
      }
      if (!ewChicane && !openings.E) {
        const run = stemRun(ePos);
        stem("hall_class_split_e", run.mid, 0, run.len, t);
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
    const winN = nPos + 0.03;
    const winS = sPos + 0.03;
    const winW = wPos + 0.03;
    const winE = ePos + 0.03;
    const fluoroAlong = this.getHallFluoroAlong(chunk.cx, chunk.cz);
    const fluoroName = (index, pair) => (fluoroAlong.length <= 2 ? pair[index] : `hall_fluoro_${index}`);
    const skipDoorGlass = this.isClosedIdentityHall(chunk.cx, chunk.cz);
    if (ewChicane) {
      if (mask.n) {
        doorsOf("n").forEach((along, i) => {
          panel(`hall_class_door_n_${i}`, along, -winN, 0.9, 0.04);
        });
        if (!skipDoorGlass) {
          glass("hall_window_n_w", -2.9, -winN, 0.28, 0.32, 0.03);
          glass("hall_window_n_e", 2.9, -winN, 0.28, 0.32, 0.03);
        }
      }
      if (mask.s && !this.isPracticeChunk(chunk.cx, chunk.cz)) {
        doorsOf("s").forEach((along, i) => {
          panel(`hall_class_door_s_${i}`, along, winS, 0.9, 0.04);
        });
        if (!skipDoorGlass) {
          glass("hall_window_s_w", -2.9, winS, 0.28, 0.32, 0.03);
          glass("hall_window_s_e", 2.9, winS, 0.28, 0.32, 0.03);
        }
      }
      const stripeZ = (sides.s - sides.n) / 2;
      const skipCloneKit = this.isClosedIdentityHall(chunk.cx, chunk.cz)
        || this.isGlassHallChunk(chunk.cx, chunk.cz);
      if (!practice) {
        if (!skipCloneKit) {
          this.placeDressedBox(
            chunk, chunkId, "hall_stripe_ew",
            center.x, floorY + 0.012, center.z + stripeZ, 14.6, 0.02, 0.09,
            this.schoolStripeMat, false,
          );
        }
        fluoroAlong.forEach((x, index) => {
          this.placeDressedBox(
            chunk, chunkId, fluoroName(index, ["hall_fluoro_w", "hall_fluoro_e"]),
            center.x + x, floorY + 2.68, center.z + stripeZ, 2.35, 0.05, 0.14,
            this.schoolFluoroMat, false,
          );
        });
      }
      if (!skipCloneKit) {
        this.addHallPaGroup(
          chunk, chunkId, "hall_pa_n",
          center.x + (fluoroAlong[0] ?? -4.15), floorY + 2.48, center.z - winN, 0,
        );
        this.addHallPaGroup(
          chunk, chunkId, "hall_pa_s",
          center.x + (fluoroAlong[fluoroAlong.length - 1] ?? 4.15), floorY + 2.48, center.z + winS, Math.PI,
        );
        this.addHallClockGroup(
          chunk, chunkId, "hall_clock",
          center.x + 3.15, floorY + 2.18, center.z - winN, 0,
        );
        this.placeDressedBox(
          chunk, chunkId, "hall_paper_n",
          center.x + 2.9, floorY + 1.55, center.z - winN,
          0.42, 0.55, 0.02, this.schoolPaperMat, false,
        );
      }
    }
    if (nsChicane) {
      if (!ewChicane) {
        if (mask.w) {
          doorsOf("w").forEach((along, i) => {
            panel(`hall_class_door_w_${i}`, -winW, along, 0.04, 0.9);
          });
        }
        if (mask.e) {
          doorsOf("e").forEach((along, i) => {
            panel(`hall_class_door_e_${i}`, winE, along, 0.04, 0.9);
          });
        }
      }
      if (mask.w && !skipDoorGlass) {
        glass("hall_window_w_n", -winW, -2.9, 0.03, 0.32, 0.28);
        glass("hall_window_w_s", -winW, 2.9, 0.03, 0.32, 0.28);
      }
      if (mask.e && !skipDoorGlass) {
        glass("hall_window_e_n", winE, -2.9, 0.03, 0.32, 0.28);
        glass("hall_window_e_s", winE, 2.9, 0.03, 0.32, 0.28);
      }
      const stripeX = (sides.e - sides.w) / 2;
      const skipCloneKitNs = this.isClosedIdentityHall(chunk.cx, chunk.cz)
        || this.isGlassHallChunk(chunk.cx, chunk.cz);
      if (!skipCloneKitNs) {
        this.placeDressedBox(
          chunk, chunkId, "hall_stripe_ns",
          center.x + stripeX, floorY + 0.012, center.z, 0.09, 0.02, 14.6,
          this.schoolStripeMat, false,
        );
      }
      fluoroAlong.forEach((z, index) => {
        this.placeDressedBox(
          chunk, chunkId, fluoroName(index, ["hall_fluoro_n", "hall_fluoro_s"]),
          center.x + stripeX, floorY + 2.68, center.z + z, 0.14, 0.05, 2.35,
          this.schoolFluoroMat, false,
        );
      });
      if (!ewChicane && !skipCloneKitNs) {
        this.addHallPaGroup(
          chunk, chunkId, "hall_pa_w",
          center.x - winW, floorY + 2.48, center.z + (fluoroAlong[0] ?? -4.15), Math.PI / 2,
        );
        this.addHallClockGroup(
          chunk, chunkId, "hall_clock_ns",
          center.x - winW, floorY + 2.18, center.z + 3.15, Math.PI / 2,
        );
      }
    }
    this.dressHallThroughDoors(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane);
    this.dressHallClassroomNooks(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane);
    this.dressHallWindowWall(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane, mask);
    if (this.isSkybridgeChunk(chunk.cx, chunk.cz)) {
      this.dressSkybridge(chunk, center, chunkId, floorY);
    }
    if (this.isMemorialChunk(chunk.cx, chunk.cz)) {
      this.dressMemorialHall(chunk, center, chunkId, floorY);
    }
    if (this.isTrophyChunk(chunk.cx, chunk.cz)) {
      this.dressTrophyHall(chunk, center, chunkId, floorY);
    }
    if (this.isArcadeChunk(chunk.cx, chunk.cz)) {
      this.dressArcadeHall(chunk, center, chunkId, floorY);
    }
    if (this.isSpecimenChunk(chunk.cx, chunk.cz)) {
      this.dressSpecimenHall(chunk, center, chunkId, floorY);
    }
    if (this.isStageWingChunk(chunk.cx, chunk.cz)) {
      this.dressStageWingHall(chunk, center, chunkId, floorY);
    }
    if (this.isLaundryChunk(chunk.cx, chunk.cz)) {
      this.dressLaundryHall(chunk, center, chunkId, floorY);
    }
    if (this.isLabLinkChunk(chunk.cx, chunk.cz)) {
      this.dressLabLinkHall(chunk, center, chunkId, floorY);
    }
    if (this.isStairHallChunk(chunk.cx, chunk.cz)) {
      this.dressStairHall(chunk, center, chunkId, floorY);
    }
    if (this.isNurseryHallChunk(chunk.cx, chunk.cz)) {
      this.dressNurseryHall(chunk, center, chunkId, floorY);
    }
    if (this.isDollHallChunk(chunk.cx, chunk.cz)) {
      this.dressDollHall(chunk, center, chunkId, floorY);
    }
    if (this.isArchiveHallChunk(chunk.cx, chunk.cz)) {
      this.dressArchiveHall(chunk, center, chunkId, floorY);
    }
    if (this.isStorageHallChunk(chunk.cx, chunk.cz)) {
      this.dressStorageHall(chunk, center, chunkId, floorY);
    }
    if (this.isTeaHallChunk(chunk.cx, chunk.cz)) {
      this.dressTeaHall(chunk, center, chunkId, floorY);
    }
    if (this.isLostFoundChunk(chunk.cx, chunk.cz)) {
      this.dressLostFoundHall(chunk, center, chunkId, floorY);
    }
    if (this.isEastWashChunk(chunk.cx, chunk.cz)) {
      this.dressEastWashHall(chunk, center, chunkId, floorY);
    }
    if (this.isAngelHallChunk(chunk.cx, chunk.cz)) {
      this.dressAngelHall(chunk, center, chunkId, floorY);
    }
    if (this.isWashFourChunk(chunk.cx, chunk.cz)) {
      this.dressWashFourHall(chunk, center, chunkId, floorY);
    }
    if (this.isPracticeChunk(chunk.cx, chunk.cz)) {
      this.dressPracticeHall(chunk, center, chunkId, floorY);
    }
    if (this.isStartHallChunk(chunk.cx, chunk.cz)) {
      this.dressStartHall(chunk, center, chunkId, floorY);
    }
    if (this.isClassWingChunk(chunk.cx, chunk.cz)) {
      this.dressClassWingHall(chunk, center, chunkId, floorY);
    }
    if (this.isUncatHallChunk(chunk.cx, chunk.cz)) {
      this.dressUncatHall(chunk, center, chunkId, floorY);
    }
    if (this.isNorthHallChunk(chunk.cx, chunk.cz)) {
      this.dressNorthHall(chunk, center, chunkId, floorY);
    }
  }

  dressHallWindowWall(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane, mask) {
    if (!ewChicane && !nsChicane) return;
    this.ensureSchoolCorridorMaterials();
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const nPos = sides.n + 0.14;
    const sPos = sides.s + 0.14;
    const ePos = sides.e + 0.14;
    const wPos = sides.w + 0.14;
    const fillEnd = 7.58;
    const windowAlongs = this.getHallWindowAlongs(chunk.cx, chunk.cz);
    const pane = (name, x, z, sx, sz, yaw) => {
      const frame = this.placeDressedBox(
        chunk, chunkId, `${name}_frame`,
        center.x + x, floorY + 1.55, center.z + z,
        Math.max(sx, 0.08), 1.35, Math.max(sz, 0.08),
        this.schoolMetalMat || this.trimMaterial, false,
      );
      const glass = new THREE.Mesh(
        this.getBoxGeometry(Math.max(sx - 0.12, 0.4), 1.05, Math.max(sz - 0.12, 0.04)),
        this.schoolGlassMat,
      );
      glass.position.set(center.x + x, floorY + 1.58, center.z + z);
      glass.rotation.y = yaw;
      glass.name = `${chunkId}_${name}`;
      this.addSchoolProp(chunk, glass);
      return frame;
    };
    const fill = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, floorY + 1.4, center.z + z, sx, 2.8, sz,
        this.schoolClassWallMat,
      );
    };
    if (ewChicane && !mask.s) {
      const fillDepth = fillEnd - sPos;
      const fillAlong = (sPos + fillEnd) / 2;
      const xs = openings.S ? [[-4.85, 5.5], [4.85, 5.5]] : [[0, 15.1]];
      for (const [x, sx] of xs) fill(`hall_outer_fill_s_${x < 0 ? "w" : x > 0 ? "e" : "m"}`, x, fillAlong, sx, fillDepth);
      windowAlongs.forEach((x, i) => {
        pane(`hall_outer_window_s_${x < 0 ? "w" : "e"}_${i}`, x, sPos - 0.08, 1.55, 0.06, 0);
      });
      const glow = new THREE.PointLight(0x141820, 0.1, 3.4, 2);
      glow.position.set(center.x, floorY + 1.55, center.z + 2.4);
      glow.name = `${chunkId}_hall_outer_glow_s`;
      this.scene.add(glow);
      chunk.meshes.push(glow);
    }
    if (ewChicane && !mask.n) {
      const fillDepth = fillEnd - nPos;
      const fillAlong = (nPos + fillEnd) / 2;
      const xs = openings.N ? [[-4.85, 5.5], [4.85, 5.5]] : [[0, 15.1]];
      for (const [x, sx] of xs) fill(`hall_outer_fill_n_${x < 0 ? "w" : x > 0 ? "e" : "m"}`, x, -fillAlong, sx, fillDepth);
      windowAlongs.forEach((x, i) => {
        pane(`hall_outer_window_n_${x < 0 ? "w" : "e"}_${i}`, x, -nPos + 0.08, 1.55, 0.06, Math.PI);
      });
      const glow = new THREE.PointLight(0x141820, 0.1, 3.4, 2);
      glow.position.set(center.x, floorY + 1.55, center.z - 2.4);
      glow.name = `${chunkId}_hall_outer_glow_n`;
      this.scene.add(glow);
      chunk.meshes.push(glow);
    }
    if (nsChicane && !ewChicane && !mask.e) {
      const fillDepth = fillEnd - ePos;
      const fillAlong = (ePos + fillEnd) / 2;
      const zs = openings.E ? [[-4.85, 5.5], [4.85, 5.5]] : [[0, 15.1]];
      for (const [z, sz] of zs) fill(`hall_outer_fill_e_${z < 0 ? "n" : z > 0 ? "s" : "m"}`, fillAlong, z, fillDepth, sz);
      windowAlongs.forEach((z, i) => {
        pane(`hall_outer_window_e_${z < 0 ? "n" : "s"}_${i}`, ePos + 0.04, z, 0.06, 1.55, Math.PI / 2);
      });
    }
    if (nsChicane && !ewChicane && !mask.w) {
      const fillDepth = fillEnd - wPos;
      const fillAlong = (wPos + fillEnd) / 2;
      const zs = openings.W ? [[-4.85, 5.5], [4.85, 5.5]] : [[0, 15.1]];
      for (const [z, sz] of zs) fill(`hall_outer_fill_w_${z < 0 ? "n" : z > 0 ? "s" : "m"}`, -fillAlong, z, fillDepth, sz);
      windowAlongs.forEach((z, i) => {
        pane(`hall_outer_window_w_${z < 0 ? "n" : "s"}_${i}`, -wPos - 0.04, z, 0.06, 1.55, -Math.PI / 2);
      });
    }
  }

  dressHallThroughDoors(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane) {
    if (!ewChicane && !nsChicane) return;
    this.ensureSchoolCorridorMaterials();
    const maze = (dx, dz) => this.isMazeHall(chunk.cx + dx, chunk.cz + dz);
    const selfMask = this.getHallNookMask(chunk.cx, chunk.cz);
    const eastMask = this.getHallNookMask(chunk.cx + 1, chunk.cz);
    const westMask = this.getHallNookMask(chunk.cx - 1, chunk.cz);
    const northMask = this.getHallNookMask(chunk.cx, chunk.cz - 1);
    const southMask = this.getHallNookMask(chunk.cx, chunk.cz + 1);
    const jamb = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, floorY + 1.18, center.z + z, sx, 2.32, sz,
        this.schoolClassDoorMat, false,
      );
    };
    const header = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, floorY + 2.28, center.z + z, sx, 0.18, sz,
        this.schoolDeskDark || this.trimMaterial, false,
      );
    };
    const leaf = (name, x, z, yaw) => {
      const group = new THREE.Group();
      group.position.set(center.x + x, floorY + 1.08, center.z + z);
      group.rotation.y = yaw;
      group.name = `${chunkId}_${name}`;
      const door = new THREE.Mesh(this.getBoxGeometry(0.05, 2.08, 0.78), this.schoolClassDoorMat);
      group.add(door);
      const glass = new THREE.Mesh(this.getBoxGeometry(0.02, 0.38, 0.26), this.schoolGlassMat);
      glass.position.set(0.028, 0.46, 0);
      group.add(glass);
      this.addSchoolProp(chunk, group);
    };
    const plate = (name, x, z, yaw) => {
      this.addHallNookSign(chunk, chunkId, name, center.x + x, floorY + 2.08, center.z + z, yaw, "뒷문");
    };
    if (ewChicane && openings.E && maze(1, 0)) {
      if (selfMask.n && eastMask.n) {
        jamb("hall_through_e_n_a", 7.72, -7.38, 0.2, 0.16);
        jamb("hall_through_e_n_b", 7.72, -5.92, 0.2, 0.16);
        header("hall_through_e_n_head", 7.72, -6.65, 0.2, 1.48);
        leaf("hall_through_e_n_leaf", 7.42, -5.88, -1.22);
        plate("hall_through_e_n_sign", 7.58, -6.65, Math.PI / 2);
      }
      if (selfMask.s && eastMask.s) {
        jamb("hall_through_e_s_a", 7.72, 5.92, 0.2, 0.16);
        jamb("hall_through_e_s_b", 7.72, 7.38, 0.2, 0.16);
        header("hall_through_e_s_head", 7.72, 6.65, 0.2, 1.48);
        leaf("hall_through_e_s_leaf", 7.42, 5.88, 1.22);
        plate("hall_through_e_s_sign", 7.58, 6.65, Math.PI / 2);
      }
    }
    if (ewChicane && openings.W && maze(-1, 0)) {
      if (selfMask.n && westMask.n) {
        jamb("hall_through_w_n_a", -7.72, -7.38, 0.2, 0.16);
        jamb("hall_through_w_n_b", -7.72, -5.92, 0.2, 0.16);
        header("hall_through_w_n_head", -7.72, -6.65, 0.2, 1.48);
        leaf("hall_through_w_n_leaf", -7.42, -5.88, 1.22);
        plate("hall_through_w_n_sign", -7.58, -6.65, -Math.PI / 2);
      }
      if (selfMask.s && westMask.s) {
        jamb("hall_through_w_s_a", -7.72, 5.92, 0.2, 0.16);
        jamb("hall_through_w_s_b", -7.72, 7.38, 0.2, 0.16);
        header("hall_through_w_s_head", -7.72, 6.65, 0.2, 1.48);
        leaf("hall_through_w_s_leaf", -7.42, 5.88, -1.22);
        plate("hall_through_w_s_sign", -7.58, 6.65, -Math.PI / 2);
      }
    }
    if (nsChicane && openings.N && maze(0, -1)) {
      if (selfMask.w && northMask.w) {
        jamb("hall_through_n_w_a", -7.52, -7.72, 0.16, 0.2);
        jamb("hall_through_n_w_b", -6.28, -7.72, 0.16, 0.2);
        header("hall_through_n_w_head", -6.9, -7.72, 1.28, 0.2);
        leaf("hall_through_n_w_leaf", -6.22, -7.42, 0.52);
        plate("hall_through_n_w_sign", -6.9, -7.58, Math.PI);
      }
      if (selfMask.e && northMask.e) {
        jamb("hall_through_n_e_a", 6.28, -7.72, 0.16, 0.2);
        jamb("hall_through_n_e_b", 7.52, -7.72, 0.16, 0.2);
        header("hall_through_n_e_head", 6.9, -7.72, 1.28, 0.2);
        leaf("hall_through_n_e_leaf", 6.22, -7.42, -0.52);
        plate("hall_through_n_e_sign", 6.9, -7.58, Math.PI);
      }
    }
    if (nsChicane && openings.S && maze(0, 1)) {
      if (selfMask.w && southMask.w) {
        jamb("hall_through_s_w_a", -7.52, 7.72, 0.16, 0.2);
        jamb("hall_through_s_w_b", -6.28, 7.72, 0.16, 0.2);
        header("hall_through_s_w_head", -6.9, 7.72, 1.28, 0.2);
        leaf("hall_through_s_w_leaf", -6.22, 7.42, -0.52);
        plate("hall_through_s_w_sign", -6.9, 7.58, 0);
      }
      if (selfMask.e && southMask.e) {
        jamb("hall_through_s_e_a", 6.28, 7.72, 0.16, 0.2);
        jamb("hall_through_s_e_b", 7.52, 7.72, 0.16, 0.2);
        header("hall_through_s_e_head", 6.9, 7.72, 1.28, 0.2);
        leaf("hall_through_s_e_leaf", 6.22, 7.42, 0.52);
        plate("hall_through_s_e_sign", 6.9, 7.58, 0);
      }
    }
  }

  dressHallClassroomNooks(chunk, center, chunkId, floorY, openings, ewChicane, nsChicane) {
    // Each hide-alcove is a classroom: linoleum, desk rows, a board on the
    // inner wall, windows on the outer wall. The ±5.25 door-to-locker aisle
    // stays open so chase-hide still walks.
    if (!ewChicane && !nsChicane) return;
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBoardMat) {
      this.schoolBoardMat = new THREE.MeshStandardMaterial({
        color: 0x1a2a1c,
        roughness: 0.88,
        metalness: 0.04,
      });
    }
    if (!this.schoolLinoMat) {
      this.schoolLinoMat = new THREE.MeshStandardMaterial({
        color: 0x2a2218,
        roughness: 0.94,
        metalness: 0,
      });
    }
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const wallN = sides.n + 0.14;
    const wallS = sides.s + 0.14;
    const wallE = sides.e + 0.14;
    const wallW = sides.w + 0.14;
    let idx = 0;
    const mask = this.getHallNookMask(chunk.cx, chunk.cz);
    const doorsOf = (side) => this.getHallDoorAlong(chunk.cx, chunk.cz, side);
    const southT = Boolean(ewChicane && (nsChicane || openings?.S));
    const northT = Boolean(ewChicane && (nsChicane || openings?.N));
    const westT = Boolean(nsChicane && (ewChicane || openings?.W));
    const eastT = Boolean(nsChicane && (ewChicane || openings?.E));

    const mazeN = (dx, dz) => this.isMazeHall(chunk.cx + dx, chunk.cz + dz);
    const nookHasThrough = (sideX, sideZ, inX, inZ) => {
      if (sideX < -0.5) {
        const other = this.getHallNookMask(chunk.cx + 1, chunk.cz);
        const side = inZ < 0 ? "n" : "s";
        return Boolean(openings.E && mazeN(1, 0) && mask[side] && other[side]);
      }
      if (sideX > 0.5) {
        const other = this.getHallNookMask(chunk.cx - 1, chunk.cz);
        const side = inZ < 0 ? "n" : "s";
        return Boolean(openings.W && mazeN(-1, 0) && mask[side] && other[side]);
      }
      if (sideZ < -0.5) {
        const other = this.getHallNookMask(chunk.cx, chunk.cz + 1);
        const side = inX < 0 ? "w" : "e";
        return Boolean(openings.S && mazeN(0, 1) && mask[side] && other[side]);
      }
      if (sideZ > 0.5) {
        const other = this.getHallNookMask(chunk.cx, chunk.cz - 1);
        const side = inX < 0 ? "w" : "e";
        return Boolean(openings.N && mazeN(0, -1) && mask[side] && other[side]);
      }
      return false;
    };

    const dressNook = (doorX, doorZ, inX, inZ, sideX, sideZ) => {
      const faceYaw = Math.atan2(sideX, sideZ);
      const kind = this.getHallNookKind(chunk.cx, chunk.cz, idx);
      if (kind !== "class") {
        this.dressSpecialHallNook(chunk, chunkId, center, floorY, {
          doorX, doorZ, inX, inZ, sideX, sideZ, idx, kind, faceYaw,
        });
        idx += 1;
        return;
      }
      // Closed identity rooms keep their own props / L-jogs instead of the
      // shared 1.7m aisle desk grid. East-wash still dresses class nooks so
      // the (1,0)→(2,0) cut-through classroom stays a hide path.
      if (this.isClosedIdentityHall(chunk.cx, chunk.cz) && !this.isEastWashChunk(chunk.cx, chunk.cz)) {
        idx += 1;
        return;
      }
      const variant = Math.abs((chunk.cx * 13 + chunk.cz * 7 + idx) % 3);
      const rows = variant === 2 ? 1 : 2;
      const aisle = 1.7;
      const firstDeskX = doorX + sideX * aisle + inX * 1.51;
      const firstDeskZ = doorZ + sideZ * aisle + inZ * 1.51;

      const alongSide = 3.15;
      const alongIn = 4.35;
      const floor = new THREE.Mesh(
        this.getBoxGeometry(
          Math.abs(sideX) * alongSide + Math.abs(inX) * alongIn,
          0.02,
          Math.abs(sideZ) * alongSide + Math.abs(inZ) * alongIn,
        ),
        this.schoolLinoMat,
      );
      floor.position.set(
        center.x + doorX + sideX * 1.85 + inX * 2.85,
        floorY + 0.008,
        center.z + doorZ + sideZ * 1.85 + inZ * 2.85,
      );
      floor.name = `${chunkId}_hall_lino_${idx}`;
      floor.receiveShadow = true;
      this.scene.add(floor);
      chunk.meshes.push(floor);

      for (let row = 0; row < rows; row += 1) {
        for (let col = 0; col < 2; col += 1) {
          const lx = doorX + sideX * (aisle + col * 1.12) + inX * (1.51 + row * 1.18);
          const lz = doorZ + sideZ * (aisle + col * 1.12) + inZ * (1.51 + row * 1.18);
          const deskName = row === 0 && col === 0
            ? `${chunkId}_hall_desk_${idx}`
            : `${chunkId}_hall_desk_${idx}_${row}${col}`;
          const desk = this.addSchoolDeskGroup(
            chunk, chunkId, deskName,
            center.x + lx, floorY + 0.35, center.z + lz, faceYaw,
            [0.58, 0.7, 0.44],
          );
          if (row === 0 && col === 0) {
            const book = new THREE.Mesh(this.getBoxGeometry(0.16, 0.03, 0.22), this.schoolPaperMat);
            book.position.set(0.1, 0.36, 0.04);
            book.rotation.y = 0.18;
            desk.add(book);
          }
          this.addSchoolChairGroup(
            chunk, chunkId, `${chunkId}_hall_chair_${idx}_${row}${col}`,
            center.x + lx + sideX * 0.42,
            floorY + 0.21,
            center.z + lz + sideZ * 0.42,
            faceYaw,
            { collideH: 0.42 },
          );
        }
      }

      this.addSchoolDeskGroup(
        chunk, chunkId, `${chunkId}_hall_teacher_${idx}`,
        center.x + doorX + sideX * 2.15 + inX * 4.52,
        floorY + 0.37,
        center.z + doorZ + sideZ * 2.15 + inZ * 4.52,
        faceYaw,
        [0.92, 0.74, 0.52],
        "teacher",
      );

      const pinched = (southT && inZ > 0) || (northT && inZ < 0) || (westT && inX < 0) || (eastT && inX > 0);
      const inner = pinched ? 3.35 : 4.85;
      this.addChalkboardGroup(
        chunk, chunkId, `${chunkId}_hall_board_${idx}`,
        center.x + doorX + sideX * inner + inX * 2.35,
        floorY + 1.48,
        center.z + doorZ + sideZ * inner + inZ * 2.35,
        faceYaw,
      );

      for (const along of [-1.15, 1.35]) {
        const glass = new THREE.Mesh(this.getBoxGeometry(0.72, 0.7, 0.04), this.schoolGlassMat);
        glass.position.set(
          center.x + doorX + sideX * (1.1 + along) + inX * 5.58,
          floorY + 1.62,
          center.z + doorZ + sideZ * (1.1 + along) + inZ * 5.58,
        );
        glass.rotation.y = faceYaw;
        glass.name = `${chunkId}_hall_class_glass_${idx}_${along < 0 ? "a" : "b"}`;
        this.scene.add(glass);
        chunk.meshes.push(glass);
      }

      const bag = new THREE.Group();
      bag.position.set(
        center.x + firstDeskX + inX * 0.22 + sideX * -0.55,
        floorY + 0.12,
        center.z + firstDeskZ + inZ * 0.22 + sideZ * -0.55,
      );
      bag.rotation.y = faceYaw + 0.4;
      bag.name = `${chunkId}_hall_bag_${idx}`;
      const bagBody = new THREE.Mesh(this.getBoxGeometry(0.22, 0.2, 0.14), this.trimMaterial);
      bag.add(bagBody);
      const bagFlap = new THREE.Mesh(this.getBoxGeometry(0.22, 0.04, 0.16), this.schoolDeskDark || this.trimMaterial);
      bagFlap.position.set(0, 0.1, 0.01);
      bag.add(bagFlap);
      const bagStrap = new THREE.Mesh(this.getBoxGeometry(0.04, 0.16, 0.02), this.schoolDeskDark || this.trimMaterial);
      bagStrap.position.set(0, 0.14, -0.07);
      bag.add(bagStrap);
      this.addSchoolProp(chunk, bag);

      const paper = new THREE.Mesh(this.getBoxGeometry(0.22, 0.012, 0.16), this.schoolPaperMat);
      paper.position.set(
        center.x + firstDeskX + sideX * 0.55,
        floorY + 0.01,
        center.z + firstDeskZ + sideZ * 0.55,
      );
      paper.rotation.y = faceYaw + 0.35;
      paper.name = `${chunkId}_hall_paper_${idx}`;
      this.scene.add(paper);
      chunk.meshes.push(paper);

      if (variant === 1) {
        this.addSchoolChairGroup(
          chunk, chunkId, `${chunkId}_hall_chair_fallen_${idx}`,
          center.x + doorX + sideX * 3.1 + inX * 3.4,
          floorY + 0.12,
          center.z + doorZ + sideZ * 3.1 + inZ * 3.4,
          faceYaw,
          { fallen: true, collideH: 0.42 },
        );
      }

      const glow = new THREE.PointLight(0x3a2c1c, 0.14, 2.8, 2.2);
      glow.position.set(
        center.x + doorX + sideX * 1.8 + inX * 2.2,
        floorY + 1.78,
        center.z + doorZ + sideZ * 1.8 + inZ * 2.2,
      );
      glow.name = `${chunkId}_hall_class_glow_${idx}`;
      this.scene.add(glow);
      chunk.meshes.push(glow);

      const fluoro = new THREE.Mesh(this.getBoxGeometry(1.85, 0.04, 0.12), this.schoolFluoroMat);
      fluoro.position.set(
        center.x + doorX + sideX * 1.8 + inX * 2.2,
        floorY + 2.64,
        center.z + doorZ + sideZ * 1.8 + inZ * 2.2,
      );
      fluoro.rotation.y = faceYaw;
      fluoro.name = `${chunkId}_hall_class_fluoro_${idx}`;
      this.scene.add(fluoro);
      chunk.meshes.push(fluoro);
      this.dressClassroomNookClutter(
        chunk, chunkId, center, floorY,
        doorX, doorZ, inX, inZ, sideX, sideZ, idx, variant, faceYaw,
      );
      if (nookHasThrough(sideX, sideZ, inX, inZ)) {
        this.dressThroughClassCorner(chunk, chunkId, center, floorY, {
          doorX, doorZ, inX, inZ, sideX, sideZ, idx, faceYaw,
        });
      }
      idx += 1;
    };

    if (ewChicane) {
      if (mask.n) {
        const doors = doorsOf("n");
        doors.forEach((along, i) => {
          dressNook(along, -wallN, 0, -1, this.hallDoorSideSign(doors, i), 0);
        });
        idx += Math.max(0, 2 - doors.length);
      } else {
        idx += 2;
      }
      if (mask.s && !this.isPracticeChunk(chunk.cx, chunk.cz)) {
        const doors = doorsOf("s");
        doors.forEach((along, i) => {
          dressNook(along, wallS, 0, 1, this.hallDoorSideSign(doors, i), 0);
        });
      }
    }
    if (nsChicane && !ewChicane) {
      if (mask.w) {
        const doors = doorsOf("w");
        doors.forEach((along, i) => {
          dressNook(-wallW, along, -1, 0, 0, this.hallDoorSideSign(doors, i));
        });
        idx += Math.max(0, 2 - doors.length);
      } else {
        idx += 2;
      }
      if (mask.e) {
        const doors = doorsOf("e");
        doors.forEach((along, i) => {
          dressNook(wallE, along, 1, 0, 0, this.hallDoorSideSign(doors, i));
        });
      }
    }
  }

  dressSpecialHallNook(chunk, chunkId, center, floorY, spec) {
    const { doorX, doorZ, inX, inZ, sideX, sideZ, idx, kind, faceYaw } = spec;
    this.ensureSpecialNookMaterials();
    if (!this.schoolLinoMat) {
      this.schoolLinoMat = new THREE.MeshStandardMaterial({
        color: 0x2a2218,
        roughness: 0.94,
        metalness: 0,
      });
    }
    const alongSide = 3.15;
    const alongIn = 4.35;
    const floor = new THREE.Mesh(
      this.getBoxGeometry(
        Math.abs(sideX) * alongSide + Math.abs(inX) * alongIn,
        0.02,
        Math.abs(sideZ) * alongSide + Math.abs(inZ) * alongIn,
      ),
      kind === "washroom" ? this.schoolTileMat : this.schoolLinoMat,
    );
    floor.position.set(
      center.x + doorX + sideX * 1.85 + inX * 2.85,
      floorY + 0.008,
      center.z + doorZ + sideZ * 1.85 + inZ * 2.85,
    );
    floor.name = `${chunkId}_hall_lino_${idx}`;
    floor.receiveShadow = true;
    this.scene.add(floor);
    chunk.meshes.push(floor);

    const pos = (along, inward, y = 0) => ({
      x: center.x + doorX + sideX * along + inX * inward,
      y: floorY + y,
      z: center.z + doorZ + sideZ * along + inZ * inward,
    });
    const boxSize = (sideLen, inLen) => ([
      Math.abs(sideX) * sideLen + Math.abs(inX) * inLen,
      Math.abs(sideZ) * sideLen + Math.abs(inZ) * inLen,
    ]);
    const collideAt = (name, point, sideLen, inLen, h) => {
      const [sx, sz] = boxSize(sideLen, inLen);
      this.collisionWorld.addStaticBox(
        `${chunkId}_${name}`,
        new THREE.Vector3(point.x, point.y, point.z),
        new THREE.Vector3(sx, h, sz),
        chunkId,
      );
    };
    const faceDoorYaw = Math.atan2(-inX, -inZ);
    const signLabel = kind === "library" ? "도서실"
      : kind === "washroom" ? "화장실"
        : kind === "boarded" ? "폐쇄"
          : kind === "shoes" ? "신발"
            : kind === "music" ? "음악"
              : kind === "science" ? "과학"
                : kind === "shrine" ? "위패"
                  : "공실";
    const sign = pos(1.55, 0.52, 2.12);
    this.addHallNookSign(chunk, chunkId, `hall_sign_${idx}`, sign.x, sign.y, sign.z, faceDoorYaw, signLabel);

    if (kind === "library") {
      const shelfKeys = [["a", 1.25], ["b", 2.4], ["c", 3.52]];
      for (const [key, along] of shelfKeys) {
        const shelf = pos(along, 3.62, 0.86);
        this.addLibraryShelfUnit(chunk, chunkId, `hall_shelf_${idx}_${key}`, shelf.x, shelf.y, shelf.z, faceDoorYaw);
        collideAt(`hall_shelf_${idx}_${key}_col`, shelf, 0.94, 0.3, 1.68);
      }
      const back = pos(2.35, 5.08, 0.86);
      this.addLibraryShelfUnit(chunk, chunkId, `hall_shelf_${idx}_d`, back.x, back.y, back.z, faceDoorYaw);
      collideAt(`hall_shelf_${idx}_d_col`, back, 0.94, 0.3, 1.68);
      const table = pos(2.15, 1.88, 0.37);
      this.addSchoolDeskGroup(
        chunk, chunkId, `${chunkId}_hall_read_${idx}`,
        table.x, table.y, table.z, faceYaw, [0.92, 0.74, 0.52], "teacher",
      );
      this.addLooseBooks(chunk, table.x, floorY + 0.76, table.z, faceDoorYaw, idx);
      for (const along of [-1.15, 1.35]) {
        const win = pos(1.1 + along, 5.22, 1.62);
        this.addBoardedWindowUnit(
          chunk, chunkId, `hall_class_${idx}_lib_board_${along < 0 ? "a" : "b"}`,
          win.x, win.y, win.z, faceDoorYaw,
        );
      }
      const lamp = new THREE.PointLight(0xffc898, 0.28, 3.4, 2);
      lamp.position.set(table.x, floorY + 1.35, table.z);
      lamp.name = `${chunkId}_hall_lib_lamp_${idx}`;
      this.scene.add(lamp);
      chunk.meshes.push(lamp);
    } else if (kind === "washroom") {
      for (let i = 0; i < 3; i += 1) {
        const stall = pos(1.28 + i * 0.94, 3.12, 0.82);
        this.addWashStallUnit(chunk, chunkId, `hall_stall_${idx}_${i}`, stall.x, stall.y, stall.z, faceDoorYaw, i);
        collideAt(`hall_stall_${idx}_${i}_col`, stall, 0.74, 0.88, 1.52);
      }
      const sinkKeys = [["a", 1.4], ["b", 2.35], ["c", 3.28]];
      for (const [key, along] of sinkKeys) {
        const sink = pos(along, 4.42, 0.46);
        this.addWashSinkUnit(chunk, chunkId, `hall_sink_${idx}_${key}`, sink.x, sink.y, sink.z, faceDoorYaw);
        collideAt(`hall_sink_${idx}_${key}_col`, sink, 0.46, 0.36, 0.5);
      }
      const mirror = new THREE.Mesh(this.getBoxGeometry(1.55, 0.55, 0.03), this.schoolGlassMat);
      const mirrorPos = pos(2.35, 5.18, 1.48);
      mirror.position.set(mirrorPos.x, mirrorPos.y, mirrorPos.z);
      mirror.rotation.y = faceDoorYaw;
      mirror.name = `${chunkId}_hall_class_glass_${idx}_a`;
      this.scene.add(mirror);
      chunk.meshes.push(mirror);
      const puddle = new THREE.Mesh(this.getPlaneGeometry(1.45, 0.95), this.schoolWetMat);
      puddle.rotation.x = -Math.PI / 2;
      const wet = pos(2.3, 4.05, 0.02);
      puddle.position.set(wet.x, wet.y, wet.z);
      puddle.name = `${chunkId}_hall_wash_wet_${idx}`;
      this.scene.add(puddle);
      chunk.meshes.push(puddle);
    } else if (kind === "boarded") {
      for (const along of [-1.15, 1.35]) {
        const win = pos(1.1 + along, 5.22, 1.62);
        this.addBoardedWindowUnit(
          chunk, chunkId, `hall_class_${idx}_board_${along < 0 ? "a" : "b"}`,
          win.x, win.y, win.z, faceDoorYaw,
        );
      }
      const lean = pos(3.05, 4.15, 0.82);
      this.addBoardedWindowUnit(chunk, chunkId, `hall_class_${idx}_board_lean`, lean.x, lean.y, lean.z, faceDoorYaw + 0.35);
      this.addCautionTape(chunk, chunkId, `hall_class_${idx}_board_tape`, pos(2.15, 3.55, 1.42).x, floorY + 1.42, pos(2.15, 3.55, 1.42).z, faceDoorYaw);
      const stack = pos(2.4, 3.15, 0.28);
      this.placeDressedBox(
        chunk, chunkId, `hall_boarded_stack_${idx}`,
        stack.x, stack.y, stack.z, 0.62, 0.48, 0.48, this.schoolDeskDark || this.trimMaterial,
      );
      this.addSchoolChairGroup(
        chunk, chunkId, `${chunkId}_hall_chair_fallen_${idx}`,
        stack.x + sideX * 0.55, floorY + 0.12, stack.z + sideZ * 0.55, faceYaw,
        { fallen: true, collideH: 0.42 },
      );
      const board = this.addChalkboardGroup(
        chunk, chunkId, `${chunkId}_hall_board_${idx}`,
        pos(2.2, 4.4, 1.48).x, pos(2.2, 4.4, 1.48).y, pos(2.2, 4.4, 1.48).z, faceDoorYaw,
      );
      const label = new THREE.Mesh(this.getPlaneGeometry(1.22, 0.28), this.schoolChalkLabelMat);
      label.position.set(0, 0.12, 0.04);
      board.add(label);
    } else if (kind === "shoes") {
      for (const [key, along] of [["a", 1.15], ["b", 2.25], ["c", 3.35]]) {
        const cubby = pos(along, 3.58, 0.82);
        this.addShoeCubbyUnit(chunk, chunkId, `hall_cubby_${idx}_${key}`, cubby.x, cubby.y, cubby.z, faceDoorYaw);
        collideAt(`hall_cubby_${idx}_${key}_col`, cubby, 0.94, 0.32, 1.55);
      }
      this.addCautionTape(
        chunk, chunkId, `hall_cubby_${idx}_tape`,
        pos(2.15, 1.55, 1.42).x, floorY + 1.42, pos(2.15, 1.55, 1.42).z, faceDoorYaw,
      );
    } else if (kind === "music") {
      for (const [key, along] of [["a", 1.35], ["b", 2.45], ["c", 3.45]]) {
        const stand = pos(along, 3.35, 0.62);
        this.addMusicStandUnit(chunk, chunkId, `hall_stand_${idx}_${key}`, stand.x, stand.y, stand.z, faceDoorYaw);
      }
      this.addChalkboardGroup(
        chunk, chunkId, `${chunkId}_hall_board_${idx}`,
        pos(2.2, 4.5, 1.48).x, pos(2.2, 4.5, 1.48).y, pos(2.2, 4.5, 1.48).z, faceDoorYaw,
      );
    } else if (kind === "science") {
      const bench = pos(2.15, 3.55, 0.42);
      this.addSchoolDeskGroup(
        chunk, chunkId, `${chunkId}_hall_labbench_${idx}`,
        bench.x, bench.y, bench.z, faceYaw, [0.92, 0.74, 0.52], "teacher",
      );
      for (const [key, along] of [["a", 1.45], ["b", 2.15], ["c", 2.85]]) {
        const jar = pos(along, 3.55, 0.92);
        this.addSpecimenJarUnit(chunk, chunkId, `hall_jar_${idx}_${key}`, jar.x, jar.y, jar.z);
      }
      this.addChalkboardGroup(
        chunk, chunkId, `${chunkId}_hall_board_${idx}`,
        pos(2.2, 4.5, 1.48).x, pos(2.2, 4.5, 1.48).y, pos(2.2, 4.5, 1.48).z, faceDoorYaw,
      );
    } else if (kind === "shrine") {
      for (const [key, along] of [["a", 1.25], ["b", 2.35], ["c", 3.45]]) {
        const frame = pos(along, 5.05, 1.38);
        this.addPortraitFrameUnit(
          chunk, chunkId, `hall_portrait_${idx}_${key}`,
          frame.x, frame.y, frame.z, faceDoorYaw,
        );
      }
      const table = pos(2.2, 2.15, 0.28);
      this.addSchoolDeskGroup(
        chunk, chunkId, `${chunkId}_hall_shrine_table_${idx}`,
        table.x, table.y, table.z, faceYaw, [0.72, 0.56, 0.44],
      );
    } else {
      const piled = pos(2.8, 3.6, 0.38);
      this.addSchoolDeskGroup(
        chunk, chunkId, `${chunkId}_hall_empty_desk_${idx}`,
        piled.x, piled.y, piled.z, faceYaw, [0.58, 0.7, 0.44],
      );
      this.addSchoolChairGroup(
        chunk, chunkId, `${chunkId}_hall_chair_fallen_${idx}`,
        piled.x + inX * 0.7, floorY + 0.12, piled.z + inZ * 0.7, faceYaw,
        { fallen: true, collideH: 0.42 },
      );
      this.addChalkboardGroup(
        chunk, chunkId, `${chunkId}_hall_board_${idx}`,
        pos(2.2, 4.5, 1.48).x, pos(2.2, 4.5, 1.48).y, pos(2.2, 4.5, 1.48).z, faceDoorYaw,
      );
    }

    const glow = new THREE.PointLight(kind === "washroom" ? 0x1c2a28 : 0x3a2c1c, kind === "library" ? 0.18 : 0.12, 2.8, 2.2);
    const glowPos = pos(1.8, 2.2, 1.78);
    glow.position.set(glowPos.x, glowPos.y, glowPos.z);
    glow.name = `${chunkId}_hall_class_glow_${idx}`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressClassroomNookClutter(chunk, chunkId, center, floorY, doorX, doorZ, inX, inZ, sideX, sideZ, idx, variant, faceYaw) {
    // Landmarks live in the classroom, never on the ±5.25 locker aisle.
    const kit = Math.abs(chunk.cx * 19 + chunk.cz * 11 + idx * 7) % 4;
    const deepX = doorX + sideX * 2.95 + inX * 3.25;
    const deepZ = doorZ + sideZ * 2.95 + inZ * 3.25;
    if (kit === 0 || kit === 2) {
      for (let i = 0; i < 3; i += 1) {
        const shoe = new THREE.Mesh(this.getBoxGeometry(0.22, 0.08, 0.12), this.trimMaterial);
        shoe.position.set(
          center.x + deepX + sideX * (i * 0.18 - 0.12) + inX * 0.08,
          floorY + 0.05,
          center.z + deepZ + sideZ * (i * 0.18 - 0.12) + inZ * 0.08,
        );
        shoe.rotation.y = faceYaw + i * 0.4;
        shoe.name = `${chunkId}_hall_shoes_${idx}_${i}`;
        this.addSchoolProp(chunk, shoe);
      }
    }
    if (kit === 1 || kit === 3) {
      const satchel = new THREE.Group();
      satchel.position.set(
        center.x + doorX + sideX * 2.4 + inX * 4.1,
        floorY + 1.15,
        center.z + doorZ + sideZ * 2.4 + inZ * 4.1,
      );
      satchel.rotation.y = faceYaw;
      satchel.name = `${chunkId}_hall_satchel_${idx}`;
      const body = new THREE.Mesh(this.getBoxGeometry(0.2, 0.28, 0.12), this.schoolDeskDark || this.trimMaterial);
      satchel.add(body);
      const strap = new THREE.Mesh(this.getBoxGeometry(0.04, 0.55, 0.02), this.trimMaterial);
      strap.position.set(0, 0.32, -0.02);
      satchel.add(strap);
      this.addSchoolProp(chunk, satchel);
    }
    if (variant === 2) {
      const stack = new THREE.Mesh(this.getBoxGeometry(0.28, 0.16, 0.22), this.schoolPaperMat);
      stack.position.set(
        center.x + doorX + sideX * 2.2 + inX * 2.4,
        floorY + 0.09,
        center.z + doorZ + sideZ * 2.2 + inZ * 2.4,
      );
      stack.rotation.y = faceYaw + 0.25;
      stack.name = `${chunkId}_hall_books_${idx}`;
      this.addSchoolProp(chunk, stack);
    }
  }

  dressThroughClassCorner(chunk, chunkId, center, floorY, spec) {
    // Desks sit beside the back-door run, never in the 1.5m seam gap
    // (along ≈ -2.55, inward ≈ 4.8) or the ±5.25 locker aisle.
    const { doorX, doorZ, inX, inZ, sideX, sideZ, idx, faceYaw } = spec;
    const put = (along, inward, y = 0) => ({
      x: center.x + doorX + sideX * along + inX * inward,
      y: floorY + y,
      z: center.z + doorZ + sideZ * along + inZ * inward,
    });
    const a = put(0.55, 3.85);
    const b = put(1.35, 4.95);
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_hall_through_desk_${idx}`,
      a.x, floorY + 0.35, a.z, faceYaw, [0.58, 0.7, 0.44],
    );
    this.addSchoolChairGroup(
      chunk, chunkId, `${chunkId}_hall_through_chair_${idx}`,
      a.x + sideX * 0.42, floorY + 0.21, a.z + sideZ * 0.42, faceYaw,
      { collideH: 0.42 },
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_hall_through_desk_${idx}_b`,
      b.x, floorY + 0.35, b.z, faceYaw, [0.58, 0.7, 0.44],
    );
    const board = put(-0.15, 5.52, 1.48);
    this.addChalkboardGroup(
      chunk, chunkId, `${chunkId}_hall_through_board_${idx}`,
      board.x, board.y, board.z, Math.atan2(-inX, -inZ),
    );
    const fallen = put(0.95, 3.15);
    this.addSchoolChairGroup(
      chunk, chunkId, `${chunkId}_hall_through_chair_fallen_${idx}`,
      fallen.x, floorY + 0.12, fallen.z, faceYaw,
      { fallen: true, collideH: 0.42 },
    );
    const book = new THREE.Mesh(this.getBoxGeometry(0.16, 0.03, 0.22), this.schoolPaperMat);
    book.position.set(a.x + sideX * 0.08, floorY + 0.74, a.z + inZ * 0.06);
    book.rotation.y = faceYaw + 0.2;
    book.name = `${chunkId}_hall_through_book_${idx}`;
    this.addSchoolProp(chunk, book);
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
    if (type === "gymnasium") {
      this.dressGymnasium(chunk, center, chunkId, floorY);
      return;
    }
    const open = this.getOpenings(chunk.cx, chunk.cz);
    const doorFace = open.N ? "N" : open.S ? "S" : open.E ? "E" : "W";
    const faceYaw = doorFace === "S" ? 0 : doorFace === "N" ? Math.PI : doorFace === "E" ? Math.PI / 2 : -Math.PI / 2;
    let boardX = center.x;
    let boardZ = center.z;
    let podiumX = center.x;
    let podiumZ = center.z;
    if (doorFace === "S") {
      boardZ = center.z - 7.52;
      podiumX = center.x - 2.2;
      podiumZ = center.z - 5.1;
    } else if (doorFace === "N") {
      boardZ = center.z + 7.52;
      podiumX = center.x + 2.2;
      podiumZ = center.z + 5.1;
    } else if (doorFace === "E") {
      boardX = center.x - 7.52;
      podiumX = center.x - 5.1;
      podiumZ = center.z - 2.2;
    } else {
      boardX = center.x + 7.52;
      podiumX = center.x + 5.1;
      podiumZ = center.z + 2.2;
    }
    this.addChalkboardGroup(
      chunk, chunkId, `${chunkId}_chalkboard`,
      boardX, floorY + 1.55, boardZ, faceYaw, 3.6, 1.15,
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_podium`,
      podiumX, floorY + 0.39, podiumZ, faceYaw,
      [0.9, 0.78, 0.55],
      "teacher",
    );

    let deskIndex = 0;
    for (let row = 0; row < 3; row += 1) {
      for (let col = 0; col < 3; col += 1) {
        const u = (col - 1) * 1.7;
        let x = center.x + u;
        let z = center.z;
        if (doorFace === "N" || doorFace === "S") {
          z = doorFace === "S" ? center.z - 0.4 + row * 1.55 : center.z + 0.4 - row * 1.55;
          x = center.x + u;
        } else {
          x = doorFace === "E" ? center.x - 0.4 + row * 1.55 : center.x + 0.4 - row * 1.55;
          z = center.z + u;
        }
        this.addSchoolDeskGroup(
          chunk, chunkId, `${chunkId}_desk_${deskIndex}`,
          x, floorY + 0.36, z, faceYaw,
          [0.62, 0.72, 0.48],
        );
        const chairBack = doorFace === "S" ? 0.42 : doorFace === "N" ? -0.42 : 0;
        const chairSide = doorFace === "E" ? 0.42 : doorFace === "W" ? -0.42 : 0;
        this.addSchoolChairGroup(
          chunk, chunkId, `${chunkId}_chair_${deskIndex}`,
          x + chairSide, floorY + 0.23, z + chairBack, faceYaw,
          { collide: [0.36, 0.46, 0.36], collideH: 0.46 },
        );
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
    this.ensureSpecialNookMaterials();
    const linen = new THREE.MeshStandardMaterial({ color: 0xd8c8b0, roughness: 0.9 });
    const steel = new THREE.MeshStandardMaterial({ color: 0x6a7674, roughness: 0.42, metalness: 0.35 });
    const curtain = new THREE.MeshStandardMaterial({
      color: 0x5a3040,
      roughness: 0.92,
      transparent: true,
      opacity: 0.72,
    });
    const bed = new THREE.Group();
    bed.position.set(center.x + 2.1, floorY + 0.32, center.z + 2.4);
    bed.name = `${chunkId}_nurse_bed`;
    const frame = new THREE.Mesh(this.getBoxGeometry(1.92, 0.12, 0.88), this.schoolMetalMat);
    bed.add(frame);
    const mattress = new THREE.Mesh(this.getBoxGeometry(1.82, 0.16, 0.8), linen);
    mattress.position.y = 0.14;
    mattress.name = `${chunkId}_nurse_sheet`;
    bed.add(mattress);
    const pillow = new THREE.Mesh(this.getBoxGeometry(0.42, 0.1, 0.36), linen);
    pillow.position.set(-0.62, 0.26, 0);
    bed.add(pillow);
    for (const [lx, lz] of [[-0.82, -0.34], [0.82, -0.34], [-0.82, 0.34], [0.82, 0.34]]) {
      const leg = new THREE.Mesh(this.getBoxGeometry(0.06, 0.32, 0.06), this.schoolMetalMat);
      leg.position.set(lx, -0.22, lz);
      bed.add(leg);
    }
    this.addSchoolProp(chunk, bed);
    this.collisionWorld.addStaticBox(bed.name, bed.position, new THREE.Vector3(1.9, 0.64, 0.86), chunkId);
    this.placeDressedBox(chunk, chunkId, "nurse_cabinet", center.x - 5.6, floorY + 0.7, center.z, 0.42, 1.4, 2.2, steel);
    this.placeDressedBox(chunk, chunkId, "nurse_desk", center.x - 2.4, floorY + 0.38, center.z - 4.4, 1.35, 0.76, 0.7, this.propMaterial);
    const rail = new THREE.Mesh(this.getBoxGeometry(0.04, 0.04, 2.4), steel);
    rail.position.set(center.x + 0.4, floorY + 2.28, center.z + 1.1);
    rail.name = `${chunkId}_nurse_rail`;
    this.addSchoolProp(chunk, rail);
    this.placeDressedBox(chunk, chunkId, "nurse_curtain", center.x + 0.4, floorY + 1.15, center.z + 1.1, 0.06, 2.2, 2.4, curtain, false);
  }

  dressMusicRoom(chunk, center, chunkId, floorY) {
    this.ensureSpecialNookMaterials();
    const lacquer = new THREE.MeshStandardMaterial({
      color: 0x1a120e,
      roughness: 0.38,
      metalness: 0.08,
      emissive: 0x120806,
      emissiveIntensity: 0.08,
    });
    const ivory = new THREE.MeshStandardMaterial({ color: 0xe8dcc4, roughness: 0.55 });
    const ebony = new THREE.MeshStandardMaterial({ color: 0x12100e, roughness: 0.45 });
    const piano = new THREE.Group();
    piano.position.set(center.x + 3.4, floorY + 0.46, center.z);
    piano.name = `${chunkId}_piano`;
    const body = new THREE.Mesh(this.getBoxGeometry(1.55, 0.62, 0.62), lacquer);
    piano.add(body);
    const lid = new THREE.Mesh(this.getBoxGeometry(1.48, 0.05, 0.52), lacquer);
    lid.position.set(0, 0.52, -0.06);
    lid.rotation.x = -0.35;
    lid.name = `${chunkId}_piano_lid`;
    piano.add(lid);
    for (let i = 0; i < 14; i += 1) {
      const black = i % 7 === 1 || i % 7 === 4;
      const key = new THREE.Mesh(
        this.getBoxGeometry(black ? 0.06 : 0.08, 0.03, black ? 0.28 : 0.42),
        black ? ebony : ivory,
      );
      key.position.set(-0.62 + i * 0.09, 0.33, 0.04);
      if (i === 0) key.name = `${chunkId}_piano_keys`;
      piano.add(key);
    }
    for (const lx of [-0.62, 0.62]) {
      const leg = new THREE.Mesh(this.getBoxGeometry(0.08, 0.42, 0.08), lacquer);
      leg.position.set(lx, -0.5, 0.18);
      piano.add(leg);
    }
    this.addSchoolProp(chunk, piano);
    this.collisionWorld.addStaticBox(piano.name, piano.position, new THREE.Vector3(1.55, 0.92, 0.62), chunkId);
    this.addSchoolChairGroup(
      chunk, chunkId, `${chunkId}_music_stool`,
      center.x + 2.1, floorY + 0.22, center.z, Math.PI / 2,
    );
    this.addMusicStandUnit(chunk, chunkId, "music_stand_a", center.x - 2.6, floorY + 0.62, center.z - 2.8, 0.4);
    this.addMusicStandUnit(chunk, chunkId, "music_stand_b", center.x - 3.4, floorY + 0.62, center.z + 1.6, -0.55);
  }

  dressFacultyOffice(chunk, center, chunkId, floorY) {
    this.ensureSpecialNookMaterials();
    const paper = new THREE.MeshStandardMaterial({ color: 0xe4d4b8, roughness: 0.92 });
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_faculty_desk`,
      center.x - 3.2, floorY + 0.38, center.z, Math.PI / 2, [1.7, 0.76, 0.86], "teacher",
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_faculty_desk_b`,
      center.x + 3.4, floorY + 0.38, center.z + 2.6, -Math.PI / 2, [1.55, 0.76, 0.8], "teacher",
    );
    this.placeDressedBox(chunk, chunkId, "faculty_file", center.x + 5.8, floorY + 0.7, center.z - 2.2, 0.46, 1.4, 1.6, this.trimMaterial);
    this.placeDressedBox(chunk, chunkId, "faculty_file_b", center.x - 5.8, floorY + 0.7, center.z + 2.4, 0.46, 1.4, 1.6, this.trimMaterial);
    this.placeDressedBox(chunk, chunkId, "faculty_papers", center.x - 3.2, floorY + 0.8, center.z, 0.62, 0.04, 0.42, paper, false);
    this.addLooseBooks(chunk, center.x + 3.4, floorY + 0.8, center.z + 2.6, 0.2, 9);
    const lamp = new THREE.Mesh(this.getBoxGeometry(0.12, 0.28, 0.12), this.schoolMetalMat);
    lamp.position.set(center.x - 2.55, floorY + 0.96, center.z - 0.18);
    lamp.name = `${chunkId}_faculty_lamp`;
    this.addSchoolProp(chunk, lamp);
    const glow = new THREE.PointLight(0xffc898, 0.2, 2.6, 2);
    glow.position.copy(lamp.position);
    glow.position.y += 0.12;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressScienceLab(chunk, center, chunkId, floorY) {
    this.ensureSpecialNookMaterials();
    const pipe = new THREE.MeshStandardMaterial({ color: 0x2a2420, roughness: 0.55, metalness: 0.4 });
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_lab_bench`,
      center.x - 3.4, floorY + 0.46, center.z, Math.PI / 2, [2.4, 0.92, 0.72], "teacher",
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_lab_bench_b`,
      center.x + 3.4, floorY + 0.46, center.z, -Math.PI / 2, [2.4, 0.92, 0.72], "teacher",
    );
    this.addSpecimenJarUnit(chunk, chunkId, "lab_bottle_a", center.x - 3.1, floorY + 1.08, center.z - 0.12);
    this.addSpecimenJarUnit(chunk, chunkId, "lab_bottle_b", center.x - 2.7, floorY + 1.12, center.z + 0.16);
    this.addSpecimenJarUnit(chunk, chunkId, "lab_bottle_c", center.x + 3.6, floorY + 1.08, center.z);
    this.addSpecimenJarUnit(chunk, chunkId, "lab_bottle_d", center.x + 3.2, floorY + 1.16, center.z - 0.18);
    this.placeDressedBox(chunk, chunkId, "lab_pipe", center.x, floorY + 2.42, center.z, 8.4, 0.1, 0.1, pipe, false);
    const tap = new THREE.Mesh(this.getBoxGeometry(0.08, 0.22, 0.08), this.schoolMetalMat);
    tap.position.set(center.x - 2.4, floorY + 1.08, center.z);
    tap.name = `${chunkId}_lab_tap`;
    this.addSchoolProp(chunk, tap);
    const stain = new THREE.Mesh(this.getPlaneGeometry(0.8, 0.5), this.schoolWetMat);
    stain.rotation.x = -Math.PI / 2;
    stain.position.set(center.x - 3.1, floorY + 0.93, center.z);
    stain.name = `${chunkId}_lab_stain`;
    this.addSchoolProp(chunk, stain);
  }

  dressGymnasium(chunk, center, chunkId, floorY) {
    this.ensureSpecialNookMaterials();
    if (!this.schoolGymFloorMat) this.schoolGymFloorMat = this.createGymFloorMaterial();
    if (!this.schoolGymWoodMat) {
      this.schoolGymWoodMat = new THREE.MeshStandardMaterial({
        color: 0x5a3a22,
        roughness: 0.78,
        metalness: 0.04,
      });
    }
    if (!this.schoolGymLineMat) {
      this.schoolGymLineMat = new THREE.MeshStandardMaterial({
        color: 0xc8b090,
        roughness: 0.62,
        metalness: 0.02,
        emissive: 0x2a2010,
        emissiveIntensity: 0.08,
      });
    }
    if (!this.schoolGymBallGeo) {
      this.schoolGymBallGeo = new THREE.SphereGeometry(0.13, 10, 8);
    }
    const court = new THREE.Mesh(this.getPlaneGeometry(14.6, 14.6), this.schoolGymFloorMat);
    court.rotation.x = -Math.PI / 2;
    court.position.set(center.x, floorY + 0.012, center.z);
    court.receiveShadow = true;
    court.name = `${chunkId}_gym_court`;
    this.addSchoolProp(chunk, court);

    for (let step = 0; step < 3; step += 1) {
      const depth = 0.52;
      const rise = 0.28 + step * 0.26;
      const zOff = 4.55 + step * 0.52;
      this.placeDressedBox(
        chunk, chunkId, `gym_bleacher_n_${step}`,
        center.x + 1.15, floorY + rise / 2, center.z - zOff,
        9.2, rise, depth, this.schoolGymWoodMat,
      );
      this.placeDressedBox(
        chunk, chunkId, `gym_bleacher_s_${step}`,
        center.x + 1.15, floorY + rise / 2, center.z + zOff,
        9.2, rise, depth, this.schoolGymWoodMat,
      );
    }

    this.placeDressedBox(
      chunk, chunkId, "gym_hoop_pole",
      center.x + 6.55, floorY + 1.35, center.z,
      0.12, 2.7, 0.12, this.schoolMetalMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "gym_hoop_board",
      center.x + 6.72, floorY + 2.35, center.z,
      0.06, 1.05, 1.55, this.schoolPaperMat, false,
    );
    const rim = new THREE.Mesh(this.getBoxGeometry(0.42, 0.04, 0.42), this.schoolMetalMat);
    rim.position.set(center.x + 6.28, floorY + 2.05, center.z);
    rim.name = `${chunkId}_gym_hoop_rim`;
    this.addSchoolProp(chunk, rim);

    this.placeDressedBox(
      chunk, chunkId, "gym_mat_stack",
      center.x + 4.85, floorY + 0.28, center.z + 3.15,
      1.15, 0.56, 0.72, this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "gym_cone_a",
      center.x + 3.4, floorY + 0.16, center.z - 2.85,
      0.18, 0.32, 0.18, this.schoolCautionMat || this.trimMaterial, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "gym_cone_b",
      center.x + 2.6, floorY + 0.16, center.z - 3.05,
      0.18, 0.32, 0.18, this.schoolCautionMat || this.trimMaterial, false,
    );

    const ballMat = new THREE.MeshStandardMaterial({
      color: 0x6a2418,
      roughness: 0.72,
      metalness: 0.08,
    });
    for (const [key, lx, lz] of [["a", 5.35, -3.15], ["b", 4.55, 3.45], ["c", 5.9, 2.55]]) {
      const ball = new THREE.Mesh(this.schoolGymBallGeo, ballMat);
      ball.position.set(center.x + lx, floorY + 0.14, center.z + lz);
      ball.name = `${chunkId}_gym_ball_${key}`;
      this.addSchoolProp(chunk, ball);
    }

    for (const along of [-3.4, 0, 3.4]) {
      const glass = new THREE.Mesh(this.getBoxGeometry(0.04, 1.15, 1.85), this.schoolGlassMat);
      glass.position.set(center.x + 7.55, floorY + 1.72, center.z + along);
      glass.name = `${chunkId}_gym_window_${along === 0 ? "c" : along < 0 ? "n" : "s"}`;
      this.addSchoolProp(chunk, glass);
    }

    this.addHallNookSign(
      chunk, chunkId, "gym_sign",
      center.x - 7.52, floorY + 2.18, center.z,
      Math.PI / 2, "체육관",
    );
    this.addHallPaGroup(chunk, chunkId, "gym_pa", center.x - 7.35, floorY + 2.48, center.z + 2.4, Math.PI / 2);
    this.addHallClockGroup(chunk, chunkId, "gym_clock", center.x + 7.35, floorY + 2.22, center.z - 4.2, -Math.PI / 2);

    for (const [name, x, z] of [["gym_fluoro_w", -3.2, 0], ["gym_fluoro_e", 3.2, 0]]) {
      this.placeDressedBox(
        chunk, chunkId, name,
        center.x + x, floorY + 2.66, center.z + z, 2.6, 0.05, 0.14,
        this.schoolFluoroMat, false,
      );
    }
    const glow = new THREE.PointLight(0x3a2c1c, 0.16, 6.5, 2);
    glow.position.set(center.x, floorY + 2.2, center.z);
    glow.name = `${chunkId}_gym_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressSkybridge(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolGrateMat) {
      this.schoolGrateMat = new THREE.MeshStandardMaterial({
        color: 0x2a2c30,
        roughness: 0.42,
        metalness: 0.55,
        emissive: 0x08090c,
        emissiveIntensity: 0.06,
      });
    }
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const stripeZ = (sides.s - sides.n) / 2;
    const grateDepth = sides.n + sides.s - 0.22;
    for (let i = -6; i <= 6; i += 1) {
      this.placeDressedBox(
        chunk, chunkId, `sky_grate_x_${i < 0 ? "w" : "e"}_${Math.abs(i)}`,
        center.x + i * 1.05, floorY + 0.018, center.z + stripeZ,
        0.06, 0.02, grateDepth, this.schoolGrateMat, false,
      );
    }
    for (const [key, z] of [["n", -sides.n * 0.72], ["c", stripeZ], ["s", sides.s * 0.72]]) {
      this.placeDressedBox(
        chunk, chunkId, `sky_grate_z_${key}`,
        center.x, floorY + 0.02, center.z + z,
        14.2, 0.02, 0.05, this.schoolGrateMat, false,
      );
    }
    for (const x of [-5.6, -2.8, 2.8, 5.6]) {
      const tag = x < 0 ? "w" : "e";
      this.placeDressedBox(
        chunk, chunkId, `sky_rib_n_${tag}_${Math.abs(x) > 4 ? "a" : "b"}`,
        center.x + x, floorY + 1.4, center.z - sides.n,
        0.1, 2.8, 0.08, this.schoolMetalMat, false,
      );
      this.placeDressedBox(
        chunk, chunkId, `sky_rib_s_${tag}_${Math.abs(x) > 4 ? "a" : "b"}`,
        center.x + x, floorY + 1.4, center.z + sides.s,
        0.1, 2.8, 0.08, this.schoolMetalMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "sky_sign",
      center.x - 6.85, floorY + 2.12, center.z - sides.n - 0.02,
      0, "연결복도",
    );
    this.addCautionTape(
      chunk, chunkId, "sky_tape_w",
      center.x - 4.4, floorY + 1.15, center.z + sides.s - 0.53, 0,
    );
    this.addCautionTape(
      chunk, chunkId, "sky_tape_e",
      center.x + 3.6, floorY + 1.22, center.z - sides.n + 0.2, Math.PI,
    );
    const stain = new THREE.Mesh(this.getPlaneGeometry(1.4, 0.55), this.schoolWetMat);
    stain.rotation.x = -Math.PI / 2;
    stain.position.set(center.x + 1.8, floorY + 0.03, center.z + 0.35);
    stain.name = `${chunkId}_sky_wet`;
    this.addSchoolProp(chunk, stain);
    for (const [key, z] of [["n", -2.55], ["s", 2.55]]) {
      const glow = new THREE.PointLight(0x10141c, 0.11, 4.2, 2);
      glow.position.set(center.x, floorY + 1.15, center.z + z);
      glow.name = `${chunkId}_sky_glow_${key}`;
      this.scene.add(glow);
      chunk.meshes.push(glow);
    }
  }

  dressStairAtrium(chunk, chunkId, floorY, {
    x,
    halfWidth,
    minZ,
    maxZ,
    railY,
    glowY,
    prefix = "atrium",
  }) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolAtriumRailMat) {
      this.schoolAtriumRailMat = new THREE.MeshStandardMaterial({
        color: 0x2a2420,
        roughness: 0.48,
        metalness: 0.42,
        emissive: 0x080604,
        emissiveIntensity: 0.05,
      });
    }
    const z0 = Math.min(minZ, maxZ);
    const z1 = Math.max(minZ, maxZ);
    const length = Math.max(1.2, z1 - z0);
    const midZ = (z0 + z1) / 2;
    const railX = halfWidth + 0.12;
    for (const side of [-1, 1]) {
      this.placeDressedBox(
        chunk, chunkId, `${prefix}_rail_${side < 0 ? "w" : "e"}`,
        x + side * railX, railY + 0.52, midZ,
        0.07, 1.04, length,
        this.schoolAtriumRailMat,
      );
      for (const t of [-0.32, 0, 0.32]) {
        this.placeDressedBox(
          chunk, chunkId, `${prefix}_post_${side < 0 ? "w" : "e"}_${t < 0 ? "a" : t > 0 ? "c" : "b"}`,
          x + side * railX, railY + 0.55, midZ + t * length,
          0.09, 1.1, 0.09,
          this.schoolAtriumRailMat,
        );
      }
    }
    const well = new THREE.Mesh(
      this.getPlaneGeometry(Math.max(2.15, halfWidth * 2 - 0.08), length - 0.2),
      this.schoolGlassMat || this.trimMaterial,
    );
    well.rotation.x = -Math.PI / 2;
    well.position.set(x, glowY, midZ);
    well.name = `${chunkId}_${prefix}_well`;
    this.addSchoolProp(chunk, well);
    const glow = new THREE.PointLight(0x1a1410, 0.16, 4.4, 2);
    glow.position.set(x, glowY, midZ);
    glow.name = `${chunkId}_${prefix}_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressMemorialHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolMemorialWood) {
      this.schoolMemorialWood = new THREE.MeshStandardMaterial({
        color: 0x2a1a12,
        roughness: 0.62,
        metalness: 0.08,
        emissive: 0x120806,
        emissiveIntensity: 0.06,
      });
    }
    if (!this.schoolMemorialGlass) {
      this.schoolMemorialGlass = new THREE.MeshStandardMaterial({
        color: 0x1a1814,
        roughness: 0.18,
        metalness: 0.22,
        transparent: true,
        opacity: 0.42,
        emissive: 0x0a0806,
        emissiveIntensity: 0.05,
      });
    }
    const caseAt = (name, x, z) => {
      this.placeDressedBox(
        chunk, chunkId, `${name}_body`,
        center.x + x, floorY + 0.78, center.z + z,
        1.15, 1.56, 0.34, this.schoolMemorialWood,
      );
      const glass = new THREE.Mesh(this.getBoxGeometry(1.02, 1.22, 0.04), this.schoolMemorialGlass);
      glass.position.set(center.x + x, floorY + 0.86, center.z + z + (z > 0 ? -0.16 : 0.16));
      glass.name = `${chunkId}_${name}_glass`;
      this.addSchoolProp(chunk, glass);
      this.placeDressedBox(
        chunk, chunkId, `${name}_cup`,
        center.x + x, floorY + 1.12, center.z + z,
        0.16, 0.28, 0.16, this.schoolMetalMat, false,
      );
    };
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    caseAt("memorial_case_nw", -5.05, -(sides.n - 0.22));
    caseAt("memorial_case_ne", 5.05, -(sides.n - 0.22));
    caseAt("memorial_case_sw", -5.05, sides.s - 0.22);
    caseAt("memorial_case_se", 5.05, sides.s - 0.22);
    for (const [name, x, z, yaw] of [
      ["n_w", -3.4, -sides.n - 0.02, 0],
      ["n_e", 3.4, -sides.n - 0.02, 0],
      ["s_w", -3.4, sides.s + 0.02, Math.PI],
      ["s_e", 3.4, sides.s + 0.02, Math.PI],
    ]) {
      const frame = new THREE.Mesh(this.getBoxGeometry(0.72, 0.92, 0.04), this.schoolMemorialWood);
      frame.position.set(center.x + x, floorY + 1.62, center.z + z);
      frame.rotation.y = yaw;
      frame.name = `${chunkId}_memorial_portrait_${name}`;
      this.addSchoolProp(chunk, frame);
      const paper = new THREE.Mesh(this.getBoxGeometry(0.58, 0.72, 0.02), this.schoolPaperMat);
      paper.position.set(center.x + x, floorY + 1.62, center.z + z + (Math.abs(yaw) < 0.1 ? 0.03 : -0.03));
      paper.name = `${chunkId}_memorial_photo_${name}`;
      this.addSchoolProp(chunk, paper);
    }
    this.addHallNookSign(
      chunk, chunkId, "memorial_sign",
      center.x - 6.85, floorY + 2.12, center.z - sides.n - 0.02,
      0, "기념관",
    );
    this.addCautionTape(
      chunk, chunkId, "memorial_tape",
      center.x + 2.4, floorY + 1.18, center.z + sides.s - 0.6, 0.2,
    );
    this.placeDressedBox(
      chunk, chunkId, "memorial_glass_jog_n",
      center.x - 2.85, floorY + 1.4, center.z - (sides.n - 0.18),
      1.55, 2.8, 0.34, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x2a2018, 0.14, 4.6, 2);
    glow.position.set(center.x, floorY + 2.15, center.z);
    glow.name = `${chunkId}_memorial_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressAuditorium(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolSeatMat) {
      this.schoolSeatMat = new THREE.MeshStandardMaterial({
        color: 0x3a1414,
        roughness: 0.78,
        metalness: 0.04,
        emissive: 0x120404,
        emissiveIntensity: 0.05,
      });
    }
    if (!this.schoolCurtainMat) {
      this.schoolCurtainMat = new THREE.MeshStandardMaterial({
        color: 0x4a1020,
        roughness: 0.9,
        metalness: 0,
        emissive: 0x140308,
        emissiveIntensity: 0.06,
      });
    }
    this.placeDressedBox(
      chunk, chunkId, "auditorium_stage",
      center.x + 5.65, floorY + 0.22, center.z,
      3.35, 0.44, 10.6, this.schoolDeskDark || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "auditorium_curtain",
      center.x + 7.22, floorY + 1.55, center.z,
      0.08, 2.7, 10.4, this.schoolCurtainMat, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "auditorium_podium",
      center.x + 4.55, floorY + 0.72, center.z,
      0.62, 0.95, 0.48, this.schoolDeskDark || this.trimMaterial,
    );
    let seat = 0;
    for (let row = 0; row < 4; row += 1) {
      const x = -4.15 + row * 1.45;
      const rise = 0.18 + row * 0.12;
      for (const side of [-1, 1]) {
        for (let col = 0; col < 3; col += 1) {
          const z = side * (2.05 + col * 0.82);
          this.placeDressedBox(
            chunk, chunkId, `auditorium_seat_${seat}`,
            center.x + x, floorY + rise, center.z + z,
            0.52, 0.46, 0.5, this.schoolSeatMat,
          );
          seat += 1;
        }
      }
    }
    this.addHallNookSign(
      chunk, chunkId, "auditorium_sign",
      center.x - 7.52, floorY + 2.18, center.z,
      Math.PI / 2, "강당",
    );
    this.addHallPaGroup(
      chunk, chunkId, "auditorium_pa",
      center.x + 7.05, floorY + 2.48, center.z - 3.4, -Math.PI / 2,
    );
    this.addHallClockGroup(
      chunk, chunkId, "auditorium_clock",
      center.x - 7.35, floorY + 2.22, center.z + 3.6, Math.PI / 2,
    );
    const glow = new THREE.PointLight(0x3a1818, 0.16, 6.2, 2);
    glow.position.set(center.x + 3.4, floorY + 2.25, center.z);
    glow.name = `${chunkId}_auditorium_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressFoyer(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolFoyerWood) {
      this.schoolFoyerWood = new THREE.MeshStandardMaterial({
        color: 0x2c1c14,
        roughness: 0.7,
        metalness: 0.06,
        emissive: 0x100804,
        emissiveIntensity: 0.05,
      });
    }
    if (!this.schoolFoyerCloth) {
      this.schoolFoyerCloth = new THREE.MeshStandardMaterial({
        color: 0x3a1020,
        roughness: 0.88,
        metalness: 0,
        emissive: 0x120308,
        emissiveIntensity: 0.05,
      });
    }
    const booth = (name, x, z) => {
      this.placeDressedBox(
        chunk, chunkId, `${name}_desk`,
        center.x + x, floorY + 0.52, center.z + z,
        1.72, 1.04, 0.62, this.schoolFoyerWood,
      );
      this.placeDressedBox(
        chunk, chunkId, `${name}_glass`,
        center.x + x, floorY + 1.18, center.z + z + (z > 0 ? -0.22 : 0.22),
        1.48, 0.42, 0.04, this.schoolMemorialGlass || this.schoolGlassMat, false,
      );
    };
    booth("foyer_booth_s", -4.35, 2.28);
    booth("foyer_booth_n", 4.35, -2.28);
    for (const [name, x, z] of [
      ["sw", -6.15, 5.55],
      ["se", 6.15, 5.55],
      ["nw", -6.15, -5.55],
      ["ne", 6.15, -5.55],
    ]) {
      this.placeDressedBox(
        chunk, chunkId, `foyer_coat_${name}`,
        center.x + x, floorY + 1.05, center.z + z,
        0.22, 2.1, 1.15, this.schoolMetalMat || this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `foyer_hook_${name}`,
        center.x + x, floorY + 1.55, center.z + z,
        0.34, 0.08, 1.02, this.schoolFoyerCloth, false,
      );
    }
    for (const [name, x, z] of [
      ["s_w", -3.4, 1.52],
      ["s_e", 3.4, 1.52],
      ["n_w", -3.4, -1.52],
      ["n_e", 3.4, -1.52],
    ]) {
      this.placeDressedBox(
        chunk, chunkId, `foyer_rope_${name}`,
        center.x + x, floorY + 0.72, center.z + z,
        0.08, 1.12, 0.08, this.schoolAtriumRailMat || this.schoolMetalMat,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "foyer_carpet",
      center.x, floorY + 0.02, center.z,
      10.4, 0.03, 2.15, this.schoolFoyerCloth, false,
    );
    this.addHallNookSign(
      chunk, chunkId, "foyer_sign",
      center.x - 7.45, floorY + 2.14, center.z,
      Math.PI / 2, "로비",
    );
    this.addHallPaGroup(
      chunk, chunkId, "foyer_pa",
      center.x + 7.05, floorY + 2.48, center.z - 3.2, -Math.PI / 2,
    );
    const bill = new THREE.Mesh(this.getBoxGeometry(0.72, 0.98, 0.04), this.schoolFoyerWood);
    bill.position.set(center.x + 3.2, floorY + 1.55, center.z + 7.52);
    bill.name = `${chunkId}_foyer_playbill`;
    this.addSchoolProp(chunk, bill);
    const glow = new THREE.PointLight(0x2a1814, 0.15, 5.2, 2);
    glow.position.set(center.x, floorY + 2.2, center.z);
    glow.name = `${chunkId}_foyer_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressTrophyHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolTrophyMat) {
      this.schoolTrophyMat = new THREE.MeshStandardMaterial({
        color: 0x8a6a28,
        roughness: 0.38,
        metalness: 0.55,
        emissive: 0x2a1806,
        emissiveIntensity: 0.08,
      });
    }
    if (!this.schoolBannerMat) {
      this.schoolBannerMat = new THREE.MeshStandardMaterial({
        color: 0x3a1420,
        roughness: 0.86,
        metalness: 0,
        emissive: 0x120408,
        emissiveIntensity: 0.05,
      });
    }
    const caseAt = (name, x, z) => {
      this.placeDressedBox(
        chunk, chunkId, `${name}_plinth`,
        center.x + x, floorY + 0.42, center.z + z,
        0.52, 0.84, 0.52, this.schoolDeskDark || this.trimMaterial,
      );
      const cup = new THREE.Mesh(this.getCylinderGeometry(0.09, 0.38, 10), this.schoolTrophyMat);
      cup.position.set(center.x + x, floorY + 1.08, center.z + z);
      cup.name = `${chunkId}_${name}_cup`;
      this.addSchoolProp(chunk, cup);
      const handle = new THREE.Mesh(this.getBoxGeometry(0.22, 0.08, 0.03), this.schoolTrophyMat);
      handle.position.set(center.x + x, floorY + 1.12, center.z + z);
      handle.name = `${chunkId}_${name}_cup_handle`;
      this.addSchoolProp(chunk, handle);
    };
    caseAt("trophy_case_sw", -3.55, 5.15);
    caseAt("trophy_case_se", 3.55, 5.15);
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    caseAt("trophy_case_nw", -3.05, -(sides.n - 0.32));
    caseAt("trophy_case_ne", 3.05, -(sides.n - 0.32));
    const southFace = sides.s + 0.08;
    for (const [name, x] of [["w", -3.35], ["e", 3.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `trophy_banner_${name}`,
        center.x + x, floorY + 1.85, center.z + southFace,
        0.82, 1.15, 0.04, this.schoolBannerMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "trophy_sign",
      center.x - 6.85, floorY + 2.12, center.z + southFace,
      0, "트로피",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "trophy_room_se", "se");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "trophy_room_sw", "sw");
    this.placeDressedBox(
      chunk, chunkId, "trophy_room_sw_l_z",
      center.x - 4.15, floorY + 1.4, center.z + 3.525,
      0.22, 2.8, 2.05, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "trophy_room_sw_l_x",
      center.x - 3.29, floorY + 1.4, center.z + 4.55,
      1.72, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "trophyhall_cheek_n",
      center.x + 0.88, floorY + 1.4, center.z - (sides.n - 0.20),
      1.45, 2.8, 0.36, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x3a2810, 0.14, 4.4, 2);
    glow.position.set(center.x, floorY + 2.15, center.z);
    glow.name = `${chunkId}_trophy_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressArcadeHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolArcadeMat) {
      this.schoolArcadeMat = new THREE.MeshStandardMaterial({
        color: 0x2a241c,
        roughness: 0.72,
        metalness: 0.08,
        emissive: 0x0c0a08,
        emissiveIntensity: 0.05,
      });
    }
    for (const [name, z] of [["n_a", -5.35], ["n_b", -3.15], ["s_a", 3.15], ["s_b", 5.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `arcade_col_${name}`,
        center.x + 3.85, floorY + 1.35, center.z + z,
        0.42, 2.7, 0.42, this.schoolArcadeMat,
      );
    }
    for (const [name, z] of [["n", -3.45], ["s", 3.45]]) {
      this.placeDressedBox(
        chunk, chunkId, `arcade_bench_${name}`,
        center.x + 3.15, floorY + 0.28, center.z + z,
        0.46, 0.56, 1.15, this.schoolDeskDark || this.trimMaterial,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "arcade_sign",
      center.x + 1.82, floorY + 2.12, center.z,
      -Math.PI / 2, "아케이드",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "arcade_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "arcade_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "arcade_room_sw_l_x",
      center.x - 3.525, floorY + 1.4, center.z + 4.15,
      2.05, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "arcade_room_sw_l_z",
      center.x - 4.55, floorY + 1.4, center.z + 3.29,
      0.22, 2.8, 1.72, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "arcade_room_nw_l_x",
      center.x - 3.68, floorY + 1.4, center.z - 4.28,
      2.22, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "arcade_room_nw_l_z",
      center.x - 4.72, floorY + 1.4, center.z - 3.18,
      0.22, 2.8, 1.88, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x181410, 0.12, 4.2, 2);
    glow.position.set(center.x + 2.4, floorY + 2.05, center.z);
    glow.name = `${chunkId}_arcade_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressSpecimenHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolJarMat) {
      this.schoolJarMat = new THREE.MeshStandardMaterial({
        color: 0x3a4a38,
        roughness: 0.28,
        metalness: 0.12,
        emissive: 0x0c1408,
        emissiveIntensity: 0.08,
        transparent: true,
        opacity: 0.72,
      });
    }
    // Keep cases inside the west rooms, off the classroom doors.
    for (const [name, z] of [["n", -4.65], ["s", 4.65]]) {
      this.placeDressedBox(
        chunk, chunkId, `specimen_case_${name}`,
        center.x - 4.55, floorY + 0.95, center.z + z,
        0.42, 1.9, 0.72, this.schoolDeskDark || this.trimMaterial,
      );
      this.addSpecimenJarUnit(
        chunk, chunkId, `specimen_jar_${name}`,
        center.x - 4.55, floorY + 1.72, center.z + z,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "specimen_sign",
      center.x - 1.82, floorY + 2.12, center.z,
      Math.PI / 2, "표본",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "specimen_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "specimen_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "specimen_room_nw_l_x",
      center.x - 5.22, floorY + 1.4, center.z - 3.58,
      3.52, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "specimen_room_nw_l_z",
      center.x - 3.48, floorY + 1.4, center.z - 2.52,
      0.22, 2.8, 2.12, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "specimen_room_sw_l_x",
      center.x - 5.22, floorY + 1.4, center.z + 3.58,
      3.52, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "specimen_room_sw_l_z",
      center.x - 3.48, floorY + 1.4, center.z + 2.52,
      0.22, 2.8, 2.12, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "specimenhall_baffle_n",
      center.x - 0.92, floorY + 1.4, center.z - 2.95,
      0.48, 2.8, 1.28, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "specimenhall_baffle_s",
      center.x + 0.92, floorY + 1.4, center.z + 2.65,
      0.48, 2.8, 1.28, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x182014, 0.12, 4.0, 2);
    glow.position.set(center.x - 2.4, floorY + 2.05, center.z);
    glow.name = `${chunkId}_specimen_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressStageWingHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolCostumeMat) {
      this.schoolCostumeMat = new THREE.MeshStandardMaterial({
        color: 0x3a1824,
        roughness: 0.9,
        metalness: 0,
        emissive: 0x120408,
        emissiveIntensity: 0.05,
      });
    }
    const rackZ = center.z + (this.getHallSides(chunk.cx, chunk.cz).s - 0.19);
    for (const [name, x] of [["w", -2.88], ["e", 2.88]]) {
      this.placeDressedBox(
        chunk, chunkId, `stagewing_rack_${name}`,
        center.x + x, floorY + 1.15, rackZ,
        0.72, 2.3, 0.18, this.schoolMetalMat || this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `stagewing_cloth_${name}`,
        center.x + x, floorY + 1.35, rackZ,
        0.62, 1.55, 0.28, this.schoolCostumeMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "stagewing_sign",
      center.x, floorY + 2.12, center.z - 1.82,
      0, "무대",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "stagewing_room_nw", "nw");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "stagewing_room_ne", "ne");
    this.placeDressedBox(
      chunk, chunkId, "stagewing_room_ne_l_z",
      center.x + 3.15, floorY + 1.4, center.z - 5.25,
      0.22, 2.8, 3.55, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "stagewing_room_ne_l_x",
      center.x + 2.28, floorY + 1.4, center.z - 3.48,
      1.74, 2.8, 0.22, this.schoolClassWallMat,
    );
    const stageSides = this.getHallSides(chunk.cx, chunk.cz);
    this.placeDressedBox(
      chunk, chunkId, "stagewing_cheek_s",
      center.x + 4.55, floorY + 1.4, center.z + (stageSides.s - 0.20),
      1.35, 2.8, 0.36, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201418, 0.12, 4.0, 2);
    glow.position.set(center.x, floorY + 2.05, center.z - 2.4);
    glow.name = `${chunkId}_stagewing_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressLaundryHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBasketMat) {
      this.schoolBasketMat = new THREE.MeshStandardMaterial({
        color: 0x4a3a28,
        roughness: 0.86,
        metalness: 0.02,
        emissive: 0x100804,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, z] of [["n", -4.65], ["s", 4.65]]) {
      this.placeDressedBox(
        chunk, chunkId, `laundry_cart_${name}`,
        center.x - 4.55, floorY + 0.38, center.z + z,
        0.72, 0.76, 0.92, this.schoolBasketMat,
      );
      this.placeDressedBox(
        chunk, chunkId, `laundry_pile_${name}`,
        center.x - 4.55, floorY + 0.82, center.z + z,
        0.55, 0.22, 0.62, this.schoolHomeEcMat || this.schoolBasketMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "laundry_sign",
      center.x - 1.82, floorY + 2.12, center.z,
      Math.PI / 2, "세탁",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "laundry_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "laundry_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "laundry_room_nw_l_x",
      center.x - 5.465, floorY + 1.4, center.z - 3.40,
      3.83, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "laundry_room_nw_l_z",
      center.x - 3.55, floorY + 1.4, center.z - 2.435,
      0.22, 2.8, 1.93, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "laundry_room_sw_l_x",
      center.x - 5.465, floorY + 1.4, center.z + 3.40,
      3.83, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "laundry_room_sw_l_z",
      center.x - 3.55, floorY + 1.4, center.z + 2.435,
      0.22, 2.8, 1.93, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "laundryhall_baffle_n",
      center.x - 0.95, floorY + 1.4, center.z - 3.85,
      0.48, 2.8, 1.40, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "laundryhall_baffle_s",
      center.x + 0.95, floorY + 1.4, center.z + 3.55,
      0.48, 2.8, 1.40, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x1c1410, 0.11, 3.8, 2);
    glow.position.set(center.x - 2.2, floorY + 2.0, center.z);
    glow.name = `${chunkId}_laundry_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressLabLinkHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolLinkGlass) {
      this.schoolLinkGlass = new THREE.MeshStandardMaterial({
        color: 0x243038,
        roughness: 0.22,
        metalness: 0.18,
        emissive: 0x081018,
        emissiveIntensity: 0.07,
        transparent: true,
        opacity: 0.55,
      });
    }
    for (const [name, x] of [["w", -2.45], ["e", 2.45]]) {
      this.placeDressedBox(
        chunk, chunkId, `lablink_case_${name}`,
        center.x + x, floorY + 0.95, center.z + 4.65,
        1.15, 1.9, 0.42, this.schoolDeskDark || this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `lablink_glass_${name}`,
        center.x + x, floorY + 1.15, center.z + 4.52,
        0.95, 1.15, 0.04, this.schoolLinkGlass, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "lablink_sign",
      center.x, floorY + 2.12, center.z + 1.82,
      Math.PI, "실험",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "lablink_room_se", "se");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "lablink_room_sw", "sw");
    this.placeDressedBox(
      chunk, chunkId, "lablink_room_sw_l_x",
      center.x - 5.55, floorY + 1.4, center.z + 3.55,
      2.40, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "lablink_room_sw_l_z",
      center.x - 4.35, floorY + 1.4, center.z + 2.65,
      0.22, 2.8, 1.80, this.schoolClassWallMat,
    );
    const labSides = this.getHallSides(chunk.cx, chunk.cz);
    this.placeDressedBox(
      chunk, chunkId, "lablink_cheek_n",
      center.x + 3.15, floorY + 1.4, center.z - (labSides.n - 0.20),
      1.42, 2.8, 0.36, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x101820, 0.11, 3.8, 2);
    glow.position.set(center.x, floorY + 2.0, center.z + 2.2);
    glow.name = `${chunkId}_lablink_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressStairHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolWetMat) {
      this.schoolWetMat = new THREE.MeshStandardMaterial({
        color: 0x1a2420,
        roughness: 0.28,
        metalness: 0.08,
        emissive: 0x061008,
        emissiveIntensity: 0.06,
        transparent: true,
        opacity: 0.55,
      });
    }
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    for (const [name, x] of [["w", -4.55], ["e", 4.55]]) {
      this.placeDressedBox(
        chunk, chunkId, `stairhall_cone_${name}`,
        center.x + x, floorY + 0.52, center.z - (sides.n - 0.28),
        0.52, 1.04, 0.52, this.schoolMetalMat || this.trimMaterial,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "stairhall_wet",
      center.x, floorY + 0.02, center.z - (sides.n - 0.42),
      4.6, 0.03, 0.85, this.schoolWetMat, false,
    );
    this.addHallNookSign(
      chunk, chunkId, "stairhall_sign",
      center.x, floorY + 2.12, center.z - sides.n - 0.02,
      0, "지하",
    );
    const glow = new THREE.PointLight(0x142018, 0.48, 6.2, 2);
    glow.position.set(center.x, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_stairhall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressNurseryHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolCribMat) {
      this.schoolCribMat = new THREE.MeshStandardMaterial({
        color: 0x3a2a22,
        roughness: 0.82,
        metalness: 0.04,
        emissive: 0x100804,
        emissiveIntensity: 0.05,
      });
    }
    const cribX = center.x - 5.95;
    for (const [name, z] of [["n", -5.15], ["s", 6.55]]) {
      this.placeDressedBox(
        chunk, chunkId, `nurseryhall_crib_${name}`,
        cribX, floorY + 0.42, center.z + z,
        0.72, 0.84, 0.85, this.schoolCribMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "nurseryhall_sign",
      center.x - 1.72, floorY + 2.12, center.z,
      Math.PI / 2, "보육",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "nursery_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "nursery_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "nursery_room_sw_l_x",
      center.x - 3.35, floorY + 1.4, center.z + 4.28,
      1.88, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "nursery_room_sw_l_z",
      center.x - 4.28, floorY + 1.4, center.z + 3.42,
      0.22, 2.8, 1.55, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "nursery_room_nw_l_x",
      center.x - 3.42, floorY + 1.4, center.z - 4.05,
      1.95, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "nursery_room_nw_l_z",
      center.x - 4.38, floorY + 1.4, center.z - 3.22,
      0.22, 2.8, 1.62, this.schoolClassWallMat,
    );
    const nurserySides = this.getHallSides(chunk.cx, chunk.cz);
    this.placeDressedBox(
      chunk, chunkId, "nurseryhall_dogleg_e",
      center.x + (nurserySides.e - 0.18), floorY + 1.4, center.z + 4.55,
      0.34, 2.8, 1.85, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201410, 0.48, 6.0, 2);
    glow.position.set(center.x - 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_nurseryhall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressDollHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolDollMat) {
      this.schoolDollMat = new THREE.MeshStandardMaterial({
        color: 0x4a3030,
        roughness: 0.78,
        metalness: 0.02,
        emissive: 0x140808,
        emissiveIntensity: 0.06,
      });
    }
    const shelfX = center.x - (this.getHallSides(chunk.cx, chunk.cz).w - 0.19);
    for (const [name, z] of [["n", -2.88], ["s", 2.88]]) {
      this.placeDressedBox(
        chunk, chunkId, `dollhall_shelf_${name}`,
        shelfX, floorY + 0.95, center.z + z,
        0.42, 1.9, 1.15, this.schoolDeskDark || this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `dollhall_figure_${name}`,
        shelfX, floorY + 0.55, center.z + z,
        0.22, 0.72, 0.18, this.schoolDollMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "dollhall_sign",
      center.x - 1.72, floorY + 2.12, center.z,
      Math.PI / 2, "인형",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "doll_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "doll_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "doll_room_sw_l_x",
      center.x - 3.525, floorY + 1.4, center.z + 4.15,
      2.05, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "doll_room_sw_l_z",
      center.x - 4.55, floorY + 1.4, center.z + 3.29,
      0.22, 2.8, 1.72, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "doll_room_nw_l_x",
      center.x - 3.525, floorY + 1.4, center.z - 4.15,
      2.05, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "doll_room_nw_l_z",
      center.x - 4.55, floorY + 1.4, center.z - 3.29,
      0.22, 2.8, 1.72, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "dollhall_baffle_n",
      center.x - 0.88, floorY + 1.4, center.z - 3.25,
      0.46, 2.8, 1.32, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "dollhall_baffle_s",
      center.x + 0.88, floorY + 1.4, center.z + 3.05,
      0.46, 2.8, 1.32, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201010, 0.48, 6.0, 2);
    glow.position.set(center.x - 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_dollhall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressArchiveHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolArchiveMat) {
      this.schoolArchiveMat = new THREE.MeshStandardMaterial({
        color: 0x3a3428,
        roughness: 0.82,
        metalness: 0.04,
        emissive: 0x100c08,
        emissiveIntensity: 0.05,
      });
    }
    const caseX = center.x - (this.getHallSides(chunk.cx, chunk.cz).w - 0.19);
    for (const [name, z] of [["n", -2.88], ["s", 2.88]]) {
      this.placeDressedBox(
        chunk, chunkId, `archivehall_case_${name}`,
        caseX, floorY + 0.72, center.z + z,
        0.55, 1.44, 0.92, this.schoolArchiveMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "archivehall_sign",
      center.x - 1.72, floorY + 2.12, center.z,
      Math.PI / 2, "서고",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "archive_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "archive_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "archive_room_nw_l_x",
      center.x - 5.05, floorY + 1.4, center.z - 3.72,
      3.28, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "archive_room_nw_l_z",
      center.x - 3.38, floorY + 1.4, center.z - 2.62,
      0.22, 2.8, 1.95, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "archive_room_sw_l_x",
      center.x - 5.05, floorY + 1.4, center.z + 3.72,
      3.28, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "archive_room_sw_l_z",
      center.x - 3.38, floorY + 1.4, center.z + 2.62,
      0.22, 2.8, 1.95, this.schoolClassWallMat,
    );
    const archiveSides = this.getHallSides(chunk.cx, chunk.cz);
    this.placeDressedBox(
      chunk, chunkId, "archivehall_dogleg_e",
      center.x + (archiveSides.e - 0.18), floorY + 1.4, center.z - 4.15,
      0.34, 2.8, 1.72, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201810, 0.48, 6.0, 2);
    glow.position.set(center.x - 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_archivehall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressStorageHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolCrateMat) {
      this.schoolCrateMat = new THREE.MeshStandardMaterial({
        color: 0x4a3a28,
        roughness: 0.88,
        metalness: 0.02,
        emissive: 0x100804,
        emissiveIntensity: 0.04,
      });
    }
    const crateX = center.x - 5.95;
    for (const [name, z] of [["n", -5.15], ["s", 5.15]]) {
      this.placeDressedBox(
        chunk, chunkId, `storagehall_crate_${name}`,
        crateX, floorY + 0.42, center.z + z,
        0.72, 0.84, 0.78, this.schoolCrateMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "storagehall_sign",
      center.x - 1.72, floorY + 2.12, center.z,
      Math.PI / 2, "창고",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "storage_room_nw", "nw", "ns");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "storage_room_sw", "sw", "ns");
    this.placeDressedBox(
      chunk, chunkId, "storage_room_sw_l_x",
      center.x - 3.62, floorY + 1.4, center.z + 4.05,
      2.18, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "storage_room_sw_l_z",
      center.x - 4.68, floorY + 1.4, center.z + 3.12,
      0.22, 2.8, 1.88, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "storage_room_nw_l_x",
      center.x - 3.85, floorY + 1.4, center.z - 4.22,
      2.35, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "storage_room_nw_l_z",
      center.x - 4.92, floorY + 1.4, center.z - 3.05,
      0.22, 2.8, 2.02, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201410, 0.48, 6.0, 2);
    glow.position.set(center.x - 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_storagehall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressTeaHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolTatamiMat) {
      this.schoolTatamiMat = new THREE.MeshStandardMaterial({
        color: 0x6a5a38,
        roughness: 0.86,
        metalness: 0.02,
        emissive: 0x120c04,
        emissiveIntensity: 0.04,
      });
    }
    const benchZ = center.z - (this.getHallSides(chunk.cx, chunk.cz).n - 0.21);
    for (const [name, x] of [["w", -2.88], ["e", 2.88]]) {
      this.placeDressedBox(
        chunk, chunkId, `teahall_bench_${name}`,
        center.x + x, floorY + 0.22, benchZ,
        1.15, 0.44, 0.42, this.schoolTatamiMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "teahall_sign",
      center.x, floorY + 2.12, center.z - 1.72,
      0, "다실",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "tea_room_nw", "nw");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "tea_room_ne", "ne");
    this.placeDressedBox(
      chunk, chunkId, "tea_room_nw_l_z",
      center.x - 3.22, floorY + 1.4, center.z - 5.28,
      0.22, 2.8, 3.48, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "tea_room_nw_l_x",
      center.x - 2.28, floorY + 1.4, center.z - 3.48,
      1.72, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "tea_room_ne_l_z",
      center.x + 3.55, floorY + 1.4, center.z - 5.12,
      0.22, 2.8, 3.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "tea_room_ne_l_x",
      center.x + 2.58, floorY + 1.4, center.z - 3.42,
      1.85, 2.8, 0.22, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201808, 0.48, 6.0, 2);
    glow.position.set(center.x, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_teahall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressLostFoundHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolLostMat) {
      this.schoolLostMat = new THREE.MeshStandardMaterial({
        color: 0x3a3228,
        roughness: 0.8,
        metalness: 0.06,
        emissive: 0x0c0804,
        emissiveIntensity: 0.05,
      });
    }
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    for (const [name, z] of [["n", -5.35], ["s", 5.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `lostfound_box_${name}`,
        center.x - (sides.w - 0.36), floorY + 0.38, center.z + z,
        0.62, 0.76, 0.72, this.schoolLostMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "lostfound_sign",
      center.x - 1.72, floorY + 2.12, center.z,
      Math.PI / 2, "분실물",
    );
    const glow = new THREE.PointLight(0x201808, 0.48, 6.0, 2);
    glow.position.set(center.x - 1.15, floorY + 2.05, center.z);
    glow.name = `${chunkId}_lostfound_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressRoofHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const t = 0.32;
    const y = floorY + 1.4;
    const h = 2.8;
    const ePos = sides.e + t / 2;
    const sPos = sides.s + t / 2;
    const nPos = sides.n + t / 2;
    const wPos = sides.w + t / 2;
    const runStart = -7.45;
    // Dual inner L: SE fence on e/s, NW fence on n/w. The open band is a
    // 3.4m north aisle (spine x=0) turning into a 3.4m west aisle (spine z=0).
    this.placeDressedBox(
      chunk, chunkId, "roofhall_run_n",
      center.x + ePos, y, center.z + (runStart + sPos) / 2,
      t, h, sPos - runStart,
      this.schoolClassWallMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "roofhall_run_w",
      center.x + (runStart + ePos) / 2, y, center.z + sPos,
      ePos - runStart, h, t,
      this.schoolClassWallMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "roofhall_run_west",
      center.x - wPos, y, center.z + (runStart - nPos) / 2,
      t, h, -nPos - runStart,
      this.schoolClassWallMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "roofhall_run_north",
      center.x + (runStart - wPos) / 2, y, center.z - nPos,
      -wPos - runStart, h, t,
      this.schoolClassWallMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "roofhall_board",
      center.x, floorY + 1.35, center.z + 6.55,
      3.4, 2.5, 0.12, this.schoolPlywoodMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "roofhall_cone",
      center.x - 4.35, floorY + 0.52, center.z + 0.55,
      0.52, 1.04, 0.52, this.schoolMetalMat || this.trimMaterial,
    );
    this.addHallNookSign(
      chunk, chunkId, "roofhall_sign",
      center.x, floorY + 2.14, center.z + 6.35,
      Math.PI, "옥상",
    );
    const glow = new THREE.PointLight(0x181410, 0.48, 6.0, 2);
    glow.position.set(center.x - 0.15, floorY + 2.05, center.z - 0.35);
    glow.name = `${chunkId}_roofhall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressDormRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBunkMat) {
      this.schoolBunkMat = new THREE.MeshStandardMaterial({
        color: 0x3a2a22,
        roughness: 0.84,
        metalness: 0.04,
        emissive: 0x100804,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, x, z] of [["sw", -5.85, 5.35], ["se", 5.85, 5.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `dorm_bunk_${name}`,
        center.x + x, floorY + 0.55, center.z + z,
        0.92, 1.1, 1.85, this.schoolBunkMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "dorm_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      0, "생활관",
    );
    const glow = new THREE.PointLight(0x201410, 0.5, 6.4, 2);
    glow.position.set(center.x, floorY + 2.1, center.z + 3.4);
    glow.name = `${chunkId}_dorm_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressDollClass(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolChairMat) {
      this.schoolChairMat = new THREE.MeshStandardMaterial({
        color: 0x2a2418,
        roughness: 0.78,
        metalness: 0.04,
        emissive: 0x0c0804,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, x, z] of [["nw", -5.45, -5.45], ["se", 5.45, 5.45]]) {
      this.placeDressedBox(
        chunk, chunkId, `dollclass_chair_${name}`,
        center.x + x, floorY + 0.42, center.z + z,
        0.48, 0.84, 0.48, this.schoolChairMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "dollclass_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      0, "인형교실",
    );
    const glow = new THREE.PointLight(0x201018, 0.5, 6.4, 2);
    glow.position.set(center.x, floorY + 2.1, center.z);
    glow.name = `${chunkId}_dollclass_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressPrepStore(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolPrepMat) {
      this.schoolPrepMat = new THREE.MeshStandardMaterial({
        color: 0x4a3828,
        roughness: 0.86,
        metalness: 0.02,
        emissive: 0x100804,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, x, z] of [["ne", 5.45, -5.45], ["se", 5.45, 5.45]]) {
      this.placeDressedBox(
        chunk, chunkId, `prepstore_crate_${name}`,
        center.x + x, floorY + 0.48, center.z + z,
        0.85, 0.96, 0.72, this.schoolPrepMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "prepstore_sign",
      center.x, floorY + 2.14, center.z + 7.52,
      Math.PI, "준비물",
    );
    const glow = new THREE.PointLight(0x181410, 0.5, 6.4, 2);
    glow.position.set(center.x, floorY + 2.1, center.z);
    glow.name = `${chunkId}_prepstore_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressClosedLibrary(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.placeDressedBox(
      chunk, chunkId, "closedlib_table",
      center.x + 5.15, floorY + 0.38, center.z + 5.15,
      1.15, 0.76, 0.7, this.schoolDeskDark || this.trimMaterial,
    );
    this.addHallNookSign(
      chunk, chunkId, "closedlib_sign",
      center.x, floorY + 2.14, center.z + 7.52,
      Math.PI, "폐관",
    );
    const glow = new THREE.PointLight(0x181410, 0.5, 6.2, 2);
    glow.position.set(center.x + 3.2, floorY + 2.08, center.z + 3.2);
    glow.name = `${chunkId}_closedlib_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressEtiquetteRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolTatamiMat) {
      this.schoolTatamiMat = new THREE.MeshStandardMaterial({
        color: 0x6a5a38,
        roughness: 0.86,
        metalness: 0.02,
        emissive: 0x120c04,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, x, z] of [["ne", 2.35, -2.35], ["sw", -2.35, 2.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `etiquette_zabuton_${name}`,
        center.x + x, floorY + 0.08, center.z + z,
        0.62, 0.12, 0.62, this.schoolTatamiMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "etiquette_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      0, "예절실",
    );
    const glow = new THREE.PointLight(0x201808, 0.5, 6.4, 2);
    glow.position.set(center.x, floorY + 2.1, center.z);
    glow.name = `${chunkId}_etiquette_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressEastWashHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBucketMat) {
      this.schoolBucketMat = new THREE.MeshStandardMaterial({
        color: 0x3a3a42,
        roughness: 0.46,
        metalness: 0.22,
        emissive: 0x08080c,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, x] of [["w", -4.55], ["e", 4.55]]) {
      this.placeDressedBox(
        chunk, chunkId, `eastwash_bucket_${name}`,
        center.x + x, floorY + 0.28, center.z - 1.52,
        0.42, 0.56, 0.42, this.schoolBucketMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "eastwash_sign",
      center.x, floorY + 2.12, center.z - 1.72,
      0, "세면",
    );
    // Keep n-west CLASS open for the (1,0)→(2,0) cut-through.
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "eastwash_room_ne", "ne");
    this.placeDressedBox(
      chunk, chunkId, "eastwash_room_ne_l_z",
      center.x + 3.40, floorY + 1.4, center.z - 5.465,
      0.22, 2.8, 3.83, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "eastwash_room_ne_l_x",
      center.x + 2.435, floorY + 1.4, center.z - 3.55,
      1.93, 2.8, 0.22, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x101418, 0.48, 6.0, 2);
    glow.position.set(center.x, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_eastwash_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressAngelHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.placeDressedBox(
      chunk, chunkId, "angelhall_cart",
      center.x + 2.95, floorY + 0.38, center.z - (this.getHallSides(chunk.cx, chunk.cz).n - 0.23),
      0.72, 0.76, 0.46, this.schoolDeskDark || this.trimMaterial,
    );
    this.addHallNookSign(
      chunk, chunkId, "angelhall_sign",
      center.x + 4.15, floorY + 2.12, center.z - 1.72,
      0, "석고",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "angel_room_ne", "ne");
    this.placeDressedBox(
      chunk, chunkId, "angel_room_ne_l_z",
      center.x + 3.22, floorY + 1.4, center.z - 5.38,
      0.22, 2.8, 3.62, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "angel_room_ne_l_x",
      center.x + 2.28, floorY + 1.4, center.z - 3.58,
      1.88, 2.8, 0.22, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x181410, 0.48, 6.0, 2);
    glow.position.set(center.x + 3.2, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_angelhall_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressWashFourHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolCubbyMat) {
      this.schoolCubbyMat = new THREE.MeshStandardMaterial({
        color: 0x3a3228,
        roughness: 0.82,
        metalness: 0.04,
        emissive: 0x0c0804,
        emissiveIntensity: 0.04,
      });
    }
    const cubbyZ = center.z - (this.getHallSides(chunk.cx, chunk.cz).n - 0.21);
    for (const [name, x] of [["w", -2.88], ["e", 2.88]]) {
      this.placeDressedBox(
        chunk, chunkId, `washfour_cubby_${name}`,
        center.x + x, floorY + 0.42, cubbyZ,
        0.92, 0.84, 0.42, this.schoolCubbyMat,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "washfour_sign",
      center.x, floorY + 2.12, center.z - 1.72,
      0, "신발",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "washfour_room_ne", "ne");
    this.placeDressedBox(
      chunk, chunkId, "washfour_room_ne_l_z",
      center.x + 3.55, floorY + 1.4, center.z - 5.52,
      0.22, 2.8, 3.70, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "washfour_room_ne_l_x",
      center.x + 2.58, floorY + 1.4, center.z - 3.68,
      1.94, 2.8, 0.22, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x181410, 0.48, 6.0, 2);
    glow.position.set(center.x, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_washfour_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressAvRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolAvMat) {
      this.schoolAvMat = new THREE.MeshStandardMaterial({
        color: 0x0c1418,
        roughness: 0.32,
        metalness: 0.18,
        emissive: 0x3a5a72,
        emissiveIntensity: 0.22,
      });
    }
    for (const [name, x, z] of [
      ["nw", -3.75, -3.75],
      ["ne", 3.75, -3.75],
      ["sw", -3.75, 3.75],
      ["se", 3.75, 3.75],
    ]) {
      this.placeDressedBox(
        chunk, chunkId, `av_cart_${name}`,
        center.x + x, floorY + 0.48, center.z + z,
        0.85, 0.96, 0.62, this.schoolDeskDark || this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `av_crt_${name}`,
        center.x + x, floorY + 1.18, center.z + z,
        0.72, 0.52, 0.12, this.schoolAvMat, false,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "av_sign",
      center.x + 7.52, floorY + 2.14, center.z - 2.45,
      -Math.PI / 2, "시청각",
    );
    const glow = new THREE.PointLight(0x182028, 0.55, 6.8, 2);
    glow.position.set(center.x, floorY + 2.15, center.z);
    glow.name = `${chunkId}_av_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressSupplyRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolCageMat) {
      this.schoolCageMat = new THREE.MeshStandardMaterial({
        color: 0x2a2420,
        roughness: 0.55,
        metalness: 0.28,
        emissive: 0x080604,
        emissiveIntensity: 0.05,
      });
    }
    for (const [name, x, z] of [
      ["nw", -3.75, -3.75],
      ["ne", 3.75, -3.75],
      ["sw", -3.75, 3.75],
      ["se", 3.75, 3.75],
    ]) {
      this.placeDressedBox(
        chunk, chunkId, `supply_cage_${name}`,
        center.x + x, floorY + 1.05, center.z + z,
        1.15, 2.1, 1.15, this.schoolCageMat,
      );
      const inwardX = x > 0 ? x - 0.58 : x + 0.58;
      const inwardZ = z > 0 ? z - 0.58 : z + 0.58;
      for (const [axis, offset] of [["x", -0.36], ["x", 0], ["x", 0.36], ["z", -0.36], ["z", 0], ["z", 0.36]]) {
        this.placeDressedBox(
          chunk, chunkId, `supply_bar_${name}_${axis}_${offset < 0 ? "a" : offset > 0 ? "b" : "m"}`,
          center.x + (axis === "x" ? inwardX : x + offset),
          floorY + 1.05,
          center.z + (axis === "z" ? inwardZ : z + offset),
          0.05, 1.95, 0.05,
          this.schoolMetalMat || this.trimMaterial,
          false,
        );
      }
    }
    this.addHallNookSign(
      chunk, chunkId, "supply_sign",
      center.x + 4.35, floorY + 2.14, center.z - 7.52,
      Math.PI, "비품",
    );
    const glow = new THREE.PointLight(0x181410, 0.5, 6.4, 2);
    glow.position.set(center.x, floorY + 2.1, center.z);
    glow.name = `${chunkId}_supply_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressPracticeHall(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolPracticeMat) {
      this.schoolPracticeMat = new THREE.MeshStandardMaterial({
        color: 0x2a2418,
        roughness: 0.74,
        metalness: 0.08,
        emissive: 0x0c0804,
        emissiveIntensity: 0.05,
      });
    }
    if (!this.schoolPracticeMetal) {
      this.schoolPracticeMetal = new THREE.MeshStandardMaterial({
        color: 0x4a4638,
        roughness: 0.42,
        metalness: 0.45,
        emissive: 0x12100c,
        emissiveIntensity: 0.04,
      });
    }
    // Kiss the authored north window so the only way east is the south
    // practice floor. Vestibules |x|>3.7 at z=0 stay open.
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const northKiss = sides.n - 0.04;
    const southEdge = 1.42;
    const baffleZ = (southEdge - northKiss) / 2;
    const baffleSz = northKiss + southEdge;
    this.placeDressedBox(
      chunk, chunkId, "practice_baffle",
      center.x, floorY + 1.4, center.z + baffleZ,
      7.4, 2.8, baffleSz, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "practice_baffle_trim",
      center.x, floorY + 0.04, center.z + baffleZ,
      7.46, 0.08, baffleSz + 0.06, this.trimMaterial, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "practice_stripe",
      center.x, floorY + 0.012, center.z + 3.55,
      12.4, 0.02, 0.1, this.schoolStripeMat, false,
    );
    for (const [name, x] of [["w", -4.2], ["m", 0], ["e", 4.2]]) {
      this.placeDressedBox(
        chunk, chunkId, `practice_stand_${name}`,
        center.x + x, floorY + 0.62, center.z + 5.95,
        0.18, 1.24, 0.18, this.schoolPracticeMetal,
      );
      this.placeDressedBox(
        chunk, chunkId, `practice_chart_${name}`,
        center.x + x, floorY + 1.28, center.z + 5.95,
        0.42, 0.08, 0.32, this.schoolPaperMat, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "practice_piano",
      center.x + 2.35, floorY + 0.42, center.z + 6.25,
      1.35, 0.84, 0.62, this.schoolPracticeMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "practice_bench",
      center.x - 2.35, floorY + 0.28, center.z + 6.25,
      1.15, 0.56, 0.46, this.schoolDeskDark || this.trimMaterial,
    );
    this.addHallNookSign(
      chunk, chunkId, "practice_sign",
      center.x - 6.85, floorY + 2.12, center.z + 1.72,
      0, "연습실",
    );
    const glow = new THREE.PointLight(0x2a2014, 0.14, 4.6, 2);
    glow.position.set(center.x, floorY + 2.15, center.z + 3.4);
    glow.name = `${chunkId}_practice_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressStudio(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolStudioCloth) {
      this.schoolStudioCloth = new THREE.MeshStandardMaterial({
        color: 0x1a1218,
        roughness: 0.9,
        metalness: 0,
        emissive: 0x0a0608,
        emissiveIntensity: 0.05,
      });
    }
    if (!this.schoolStudioLight) {
      this.schoolStudioLight = new THREE.MeshStandardMaterial({
        color: 0xc8b898,
        roughness: 0.35,
        metalness: 0.08,
        emissive: 0x3a3020,
        emissiveIntensity: 0.18,
      });
    }
    this.placeDressedBox(
      chunk, chunkId, "studio_backdrop",
      center.x, floorY + 1.35, center.z - 6.35,
      9.2, 2.5, 0.12, this.schoolStudioCloth,
    );
    for (const [name, x] of [["w", -5.85], ["e", 5.85]]) {
      this.placeDressedBox(
        chunk, chunkId, `studio_softbox_${name}`,
        center.x + x, floorY + 1.85, center.z - 3.85,
        0.72, 0.92, 0.18, this.schoolStudioLight, false,
      );
      this.placeDressedBox(
        chunk, chunkId, `studio_stand_${name}`,
        center.x + x, floorY + 0.95, center.z - 3.85,
        0.08, 1.9, 0.08, this.schoolMetalMat || this.trimMaterial,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "studio_camera",
      center.x + 4.65, floorY + 1.08, center.z - 1.85,
      0.28, 0.22, 0.42, this.schoolMetalMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "studio_tripod",
      center.x + 4.65, floorY + 0.55, center.z - 1.85,
      0.12, 1.1, 0.12, this.trimMaterial,
    );
    for (const [name, x] of [["sw", -6.25], ["se", 6.25]]) {
      this.placeDressedBox(
        chunk, chunkId, `studio_drape_${name}`,
        center.x + x, floorY + 1.4, center.z + 3.15,
        0.1, 2.6, 2.4, this.schoolStudioCloth,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "studio_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      0, "촬영실",
    );
    const glow = new THREE.PointLight(0x2a1818, 0.14, 5.0, 2);
    glow.position.set(center.x, floorY + 2.15, center.z);
    glow.name = `${chunkId}_studio_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressBroadcast(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBroadcastMat) {
      this.schoolBroadcastMat = new THREE.MeshStandardMaterial({
        color: 0x1c1a16,
        roughness: 0.62,
        metalness: 0.18,
        emissive: 0x080706,
        emissiveIntensity: 0.05,
      });
    }
    this.placeDressedBox(
      chunk, chunkId, "broadcast_desk",
      center.x - 4.55, floorY + 0.48, center.z - 3.85,
      2.15, 0.96, 0.78, this.schoolDeskDark || this.trimMaterial,
    );
    for (const [name, x] of [["a", -5.15], ["b", -3.95]]) {
      this.placeDressedBox(
        chunk, chunkId, `broadcast_crt_${name}`,
        center.x + x, floorY + 1.12, center.z - 3.85,
        0.52, 0.38, 0.42, this.schoolBroadcastMat, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "broadcast_rack",
      center.x - 5.85, floorY + 0.95, center.z + 4.35,
      0.62, 1.9, 1.15, this.schoolMetalMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "broadcast_mic",
      center.x + 4.45, floorY + 1.15, center.z + 3.55,
      0.08, 1.1, 0.08, this.schoolMetalMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "broadcast_chair",
      center.x - 4.55, floorY + 0.32, center.z - 2.55,
      0.48, 0.64, 0.48, this.schoolDeskDark || this.trimMaterial,
    );
    this.addHallNookSign(
      chunk, chunkId, "broadcast_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      Math.PI, "방송실",
    );
    const glow = new THREE.PointLight(0x181410, 0.13, 4.8, 2);
    glow.position.set(center.x - 2.4, floorY + 2.1, center.z);
    glow.name = `${chunkId}_broadcast_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressDarkroom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolSafeLightMat) {
      this.schoolSafeLightMat = new THREE.MeshStandardMaterial({
        color: 0x6a1818,
        roughness: 0.42,
        metalness: 0.08,
        emissive: 0x3a0808,
        emissiveIntensity: 0.35,
      });
    }
    if (!this.schoolTrayMat) {
      this.schoolTrayMat = new THREE.MeshStandardMaterial({
        color: 0x2a2420,
        roughness: 0.38,
        metalness: 0.22,
        emissive: 0x120808,
        emissiveIntensity: 0.06,
      });
    }
    for (const [name, x] of [["w", -4.35], ["e", 4.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `darkroom_sink_${name}`,
        center.x + x, floorY + 0.42, center.z - 5.85,
        2.15, 0.84, 0.72, this.schoolTrayMat,
      );
      this.placeDressedBox(
        chunk, chunkId, `darkroom_tray_${name}`,
        center.x + x, floorY + 0.92, center.z - 5.85,
        1.55, 0.08, 0.48, this.schoolSafeLightMat, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "darkroom_line",
      center.x, floorY + 2.28, center.z - 4.85,
      8.4, 0.04, 0.04, this.schoolMetalMat || this.trimMaterial, false,
    );
    for (const x of [-2.4, 0, 2.4]) {
      this.placeDressedBox(
        chunk, chunkId, `darkroom_print_${x < 0 ? "w" : x > 0 ? "e" : "m"}`,
        center.x + x, floorY + 1.85, center.z - 4.85,
        0.42, 0.55, 0.02, this.schoolPaperMat, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "darkroom_enlarger",
      center.x + 5.15, floorY + 0.95, center.z + 4.55,
      0.42, 1.9, 0.42, this.schoolMetalMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "darkroom_lamp",
      center.x, floorY + 2.55, center.z,
      0.28, 0.16, 0.28, this.schoolSafeLightMat, false,
    );
    this.addHallNookSign(
      chunk, chunkId, "darkroom_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      0, "암실",
    );
    const glow = new THREE.PointLight(0x4a1010, 0.22, 5.2, 2);
    glow.position.set(center.x, floorY + 2.35, center.z);
    glow.name = `${chunkId}_darkroom_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressGreenroom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolGreenCloth) {
      this.schoolGreenCloth = new THREE.MeshStandardMaterial({
        color: 0x2a1820,
        roughness: 0.9,
        metalness: 0,
        emissive: 0x0c0608,
        emissiveIntensity: 0.05,
      });
    }
    this.placeDressedBox(
      chunk, chunkId, "greenroom_sofa",
      center.x, floorY + 0.38, center.z + 5.55,
      2.85, 0.76, 0.82, this.schoolGreenCloth,
    );
    this.placeDressedBox(
      chunk, chunkId, "greenroom_rack",
      center.x - 5.85, floorY + 1.15, center.z + 4.25,
      0.18, 2.3, 2.15, this.schoolMetalMat || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "greenroom_costume",
      center.x - 5.85, floorY + 1.35, center.z + 4.25,
      0.28, 1.55, 1.85, this.schoolGreenCloth, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "greenroom_mirror",
      center.x + 5.85, floorY + 1.45, center.z + 5.35,
      1.15, 1.55, 0.06, this.schoolGlassMat || this.trimMaterial, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "greenroom_chair",
      center.x + 4.85, floorY + 0.32, center.z + 4.15,
      0.48, 0.64, 0.48, this.schoolDeskDark || this.trimMaterial,
    );
    this.addHallNookSign(
      chunk, chunkId, "greenroom_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      Math.PI, "대기실",
    );
    const glow = new THREE.PointLight(0x201418, 0.13, 4.8, 2);
    glow.position.set(center.x, floorY + 2.1, center.z + 3.2);
    glow.name = `${chunkId}_greenroom_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressHomeEc(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolHomeEcMat) {
      this.schoolHomeEcMat = new THREE.MeshStandardMaterial({
        color: 0x3a2a1c,
        roughness: 0.78,
        metalness: 0.06,
        emissive: 0x100804,
        emissiveIntensity: 0.05,
      });
    }
    for (const [name, z] of [["n", -3.65], ["s", 3.65]]) {
      this.placeDressedBox(
        chunk, chunkId, `home_ec_machine_${name}`,
        center.x - 5.85, floorY + 0.42, center.z + z,
        0.72, 0.84, 0.85, this.schoolHomeEcMat,
      );
      this.placeDressedBox(
        chunk, chunkId, `home_ec_head_${name}`,
        center.x - 5.85, floorY + 0.92, center.z + z,
        0.28, 0.22, 0.42, this.schoolMetalMat || this.trimMaterial, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "home_ec_board",
      center.x - 5.55, floorY + 0.42, center.z + 5.65,
      0.92, 0.84, 1.35, this.schoolDeskDark || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "home_ec_dummy",
      center.x - 5.55, floorY + 0.95, center.z - 5.55,
      0.42, 1.9, 0.38, this.schoolHomeEcMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "home_ec_cloth",
      center.x + 5.35, floorY + 0.18, center.z + 5.35,
      1.15, 0.36, 0.92, this.schoolBannerMat || this.schoolHomeEcMat, false,
    );
    this.addHallNookSign(
      chunk, chunkId, "home_ec_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      Math.PI, "가정실",
    );
    const glow = new THREE.PointLight(0x2a1810, 0.13, 4.6, 2);
    glow.position.set(center.x - 3.2, floorY + 2.1, center.z);
    glow.name = `${chunkId}_home_ec_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressClubRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolInkMat) {
      this.schoolInkMat = new THREE.MeshStandardMaterial({
        color: 0x121014,
        roughness: 0.55,
        metalness: 0.04,
        emissive: 0x080608,
        emissiveIntensity: 0.04,
      });
    }
    for (const [name, x] of [["w", -4.55], ["e", 4.55]]) {
      this.placeDressedBox(
        chunk, chunkId, `club_table_${name}`,
        center.x + x, floorY + 0.28, center.z - 4.15,
        1.85, 0.56, 0.82, this.schoolDeskDark || this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `club_ink_${name}`,
        center.x + x, floorY + 0.62, center.z - 4.15,
        0.16, 0.12, 0.16, this.schoolInkMat, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "club_scroll",
      center.x, floorY + 1.55, center.z - 7.42,
      1.35, 1.15, 0.04, this.schoolPaperMat, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "club_brush",
      center.x - 4.15, floorY + 0.62, center.z - 4.15,
      0.42, 0.04, 0.04, this.schoolDeskDark || this.trimMaterial, false,
    );
    this.addHallNookSign(
      chunk, chunkId, "club_sign",
      center.x, floorY + 2.14, center.z + 7.52,
      0, "서도부",
    );
    const glow = new THREE.PointLight(0x181410, 0.12, 4.4, 2);
    glow.position.set(center.x, floorY + 2.1, center.z - 2.4);
    glow.name = `${chunkId}_club_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressArtRoom(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolCanvasMat) {
      this.schoolCanvasMat = new THREE.MeshStandardMaterial({
        color: 0xc8b898,
        roughness: 0.88,
        metalness: 0,
        emissive: 0x1a1008,
        emissiveIntensity: 0.06,
      });
    }
    if (!this.schoolPaintMat) {
      this.schoolPaintMat = new THREE.MeshStandardMaterial({
        color: 0x4a1828,
        roughness: 0.7,
        metalness: 0.04,
        emissive: 0x140408,
        emissiveIntensity: 0.08,
      });
    }
    this.placeDressedBox(
      chunk, chunkId, "art_table",
      center.x + 2.35, floorY + 0.42, center.z + 1.15,
      1.85, 0.84, 0.92, this.schoolDeskDark || this.trimMaterial,
    );
    for (const [name, x, z, yaw] of [
      ["w", -3.4, -1.8, Math.PI / 2],
      ["e", 3.6, -2.4, -Math.PI / 2],
      ["s", -2.2, 3.35, Math.PI],
    ]) {
      const easel = new THREE.Group();
      easel.position.set(center.x + x, floorY + 0.95, center.z + z);
      easel.rotation.y = yaw;
      easel.name = `${chunkId}_art_easel_${name}`;
      const leg = new THREE.Mesh(this.getBoxGeometry(0.08, 1.7, 0.08), this.schoolDeskDark || this.trimMaterial);
      easel.add(leg);
      const canvas = new THREE.Mesh(this.getBoxGeometry(0.72, 0.92, 0.04), this.schoolCanvasMat);
      canvas.position.set(0, 0.22, 0.06);
      canvas.name = `${chunkId}_art_canvas_${name}`;
      easel.add(canvas);
      this.addSchoolProp(chunk, easel);
      this.collisionWorld.addStaticBox(
        easel.name,
        easel.position,
        new THREE.Vector3(0.55, 1.7, 0.55),
        chunkId,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "art_jar",
      center.x + 2.15, floorY + 0.95, center.z + 1.15,
      0.16, 0.22, 0.16, this.schoolPaintMat, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "art_drip",
      center.x, floorY + 0.02, center.z + 2.4,
      1.8, 0.03, 2.4, this.schoolPaintMat, false,
    );
    this.addHallNookSign(
      chunk, chunkId, "art_sign",
      center.x, floorY + 2.14, center.z - 7.52,
      0, "미술실",
    );
    const glow = new THREE.PointLight(0x2a1810, 0.14, 5.0, 2);
    glow.position.set(center.x, floorY + 2.15, center.z);
    glow.name = `${chunkId}_art_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
  }

  dressCourtyard(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    this.ensureSpecialNookMaterials();
    if (!this.schoolYardMat) {
      this.schoolYardMat = new THREE.MeshStandardMaterial({
        color: 0x14120e,
        roughness: 0.92,
        metalness: 0.04,
        emissive: 0x080704,
        emissiveIntensity: 0.05,
      });
    }
    if (!this.schoolHedgeMat) {
      this.schoolHedgeMat = new THREE.MeshStandardMaterial({
        color: 0x1a2418,
        roughness: 0.9,
        metalness: 0,
        emissive: 0x081008,
        emissiveIntensity: 0.04,
      });
    }
    this.placeDressedBox(
      chunk, chunkId, "courtyard_well",
      center.x, floorY + 0.52, center.z,
      4.3, 1.04, 4.3, this.schoolYardMat,
    );
    const pit = new THREE.Mesh(this.getPlaneGeometry(4.05, 4.05), this.schoolGlassMat);
    pit.rotation.x = -Math.PI / 2;
    pit.position.set(center.x, floorY + 1.05, center.z);
    pit.name = `${chunkId}_courtyard_well_glass`;
    this.addSchoolProp(chunk, pit);
    this.placeDressedBox(
      chunk, chunkId, "courtyard_rail_n",
      center.x, floorY + 0.58, center.z - 2.18, 4.55, 1.16, 0.16, this.schoolMetalMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "courtyard_rail_s",
      center.x, floorY + 0.58, center.z + 2.18, 4.55, 1.16, 0.16, this.schoolMetalMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "courtyard_rail_w",
      center.x - 2.18, floorY + 0.58, center.z, 0.16, 1.16, 4.2, this.schoolMetalMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "courtyard_rail_e",
      center.x + 2.18, floorY + 0.58, center.z, 0.16, 1.16, 4.2, this.schoolMetalMat,
    );
    for (const [name, x, z, yaw] of [
      ["n", 0, -2.18, 0],
      ["s", 0, 2.18, Math.PI],
      ["w", -2.18, 0, -Math.PI / 2],
      ["e", 2.18, 0, Math.PI / 2],
    ]) {
      const glass = new THREE.Mesh(this.getBoxGeometry(3.4, 0.72, 0.03), this.schoolGlassMat);
      glass.position.set(center.x + x, floorY + 0.78, center.z + z);
      glass.rotation.y = yaw;
      glass.name = `${chunkId}_courtyard_pane_${name}`;
      this.addSchoolProp(chunk, glass);
    }
    this.placeDressedBox(
      chunk, chunkId, "courtyard_tree_trunk",
      center.x + 0.35, floorY + 0.85, center.z - 0.2,
      0.22, 1.7, 0.22, this.schoolDeskDark || this.trimMaterial, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "courtyard_tree_crown",
      center.x + 0.35, floorY + 1.72, center.z - 0.2,
      1.35, 0.7, 1.35, this.schoolHedgeMat, false,
    );
    for (const [tag, x, z] of [["nw", -6.35, -6.35], ["ne", 6.35, -6.35], ["sw", -6.35, 6.35], ["se", 6.35, 6.35]]) {
      this.placeDressedBox(
        chunk, chunkId, `courtyard_planter_${tag}`,
        center.x + x, floorY + 0.28, center.z + z,
        0.85, 0.56, 0.85, this.trimMaterial,
      );
      this.placeDressedBox(
        chunk, chunkId, `courtyard_hedge_${tag}`,
        center.x + x, floorY + 0.72, center.z + z,
        0.62, 0.38, 0.62, this.schoolHedgeMat, false,
      );
    }
    this.placeDressedBox(
      chunk, chunkId, "courtyard_bench_e",
      center.x + 5.55, floorY + 0.22, center.z + 3.85,
      0.42, 0.44, 1.35, this.schoolDeskDark || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "courtyard_bench_w",
      center.x - 5.55, floorY + 0.22, center.z - 3.85,
      0.42, 0.44, 1.35, this.schoolDeskDark || this.trimMaterial,
    );
    this.placeDressedBox(
      chunk, chunkId, "courtyard_trophy",
      center.x + 5.55, floorY + 0.62, center.z - 4.15,
      0.55, 1.24, 0.42, this.schoolMetalMat,
    );
    this.addHallNookSign(
      chunk, chunkId, "courtyard_sign",
      center.x, floorY + 2.18, center.z - 7.52,
      0, "중정",
    );
    this.addHallPaGroup(
      chunk, chunkId, "courtyard_pa",
      center.x + 7.35, floorY + 2.48, center.z - 4.2, -Math.PI / 2,
    );
    this.addHallClockGroup(
      chunk, chunkId, "courtyard_clock",
      center.x - 7.35, floorY + 2.18, center.z + 3.4, Math.PI / 2,
    );
    const wet = new THREE.Mesh(this.getPlaneGeometry(1.6, 0.7), this.schoolWetMat);
    wet.rotation.x = -Math.PI / 2;
    wet.position.set(center.x - 4.6, floorY + 0.03, center.z + 5.1);
    wet.name = `${chunkId}_courtyard_wet`;
    this.addSchoolProp(chunk, wet);
    const moon = new THREE.PointLight(0x1a2430, 0.14, 5.4, 2);
    moon.position.set(center.x, floorY + 2.35, center.z);
    moon.name = `${chunkId}_courtyard_moon`;
    this.scene.add(moon);
    chunk.meshes.push(moon);
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

  placeRoomShell(chunk, chunkId, name, spec) {
    const {
      minX, maxX, minZ, maxZ,
      wallY, height, thickness, material,
      doors = [],
      skip = {},
    } = spec;
    const gaps = (wall) => doors
      .filter((door) => door.wall === wall)
      .map((door) => ({ center: door.center, width: door.width }));
    const faces = [
      ["n", !skip.n, "x", minZ, minX, maxX],
      ["s", !skip.s, "x", maxZ, minX, maxX],
      ["w", !skip.w, "z", minX, minZ, maxZ],
      ["e", !skip.e, "z", maxX, minZ, maxZ],
    ];
    for (const [suffix, enabled, axis, pos, min, max] of faces) {
      if (!enabled) continue;
      const wallGaps = gaps(suffix);
      this.placeGappedWall(chunk, chunkId, `${name}_${suffix}`, {
        axis,
        pos,
        min,
        max,
        wallY,
        height,
        thickness,
        material,
        gaps: wallGaps,
      });
      if (wallGaps.length === 0) {
        const span = max - min;
        const mid = (min + max) / 2;
        if (axis === "x") {
          this.dressMazePartitionTrim(chunk, chunkId, `${name}_${suffix}`, mid, wallY, pos, span, thickness, height);
        } else {
          this.dressMazePartitionTrim(chunk, chunkId, `${name}_${suffix}`, pos, wallY, mid, thickness, span, height);
        }
      }
    }
  }

  // Close one classroom corner into a real room. The hall_class_* door
  // already punches the inner face, so that face is skipped. Keep the
  // authored corridor spine clear.
  dressClosedCornerRoom(chunk, center, chunkId, floorY, name, corner, along = "ew") {
    this.ensureSchoolCorridorMaterials();
    const east = corner.includes("e");
    const south = corner.includes("s");
    const clear = this.getHallClear(chunk.cx, chunk.cz);
    const sides = this.getHallSides(chunk.cx, chunk.cz);
    const inner = (along === "ns"
      ? (east ? sides.e : sides.w)
      : (south ? sides.s : sides.n)) + 0.32;
    const spine = clear + 0.25;
    const spec = along === "ns"
      ? {
        minX: center.x + (east ? inner : -7.38),
        maxX: center.x + (east ? 7.38 : -inner),
        minZ: center.z + (south ? spine : -7.28),
        maxZ: center.z + (south ? 7.28 : -spine),
        skip: east ? { w: true } : { e: true },
      }
      : {
        minX: center.x + (east ? spine : -7.28),
        maxX: center.x + (east ? 7.28 : -spine),
        minZ: center.z + (south ? inner : -7.38),
        maxZ: center.z + (south ? 7.38 : -inner),
        skip: south ? { n: true } : { s: true },
      };
    this.placeRoomShell(chunk, chunkId, name, {
      ...spec,
      wallY: floorY + 1.4,
      height: 2.8,
      thickness: 0.22,
      material: this.schoolClassWallMat,
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

    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBoilerMat) {
      this.schoolBoilerMat = new THREE.MeshStandardMaterial({
        color: 0x2a322c,
        roughness: 0.62,
        metalness: 0.28,
        emissive: 0x081410,
        emissiveIntensity: 0.06,
      });
    }
    this.addBoilerDrumUnit(
      chunk, chunkId, "b1boiler_drum",
      22.4, floorY - 5.0 + 0.58, 24.8,
    );
    this.placeDressedBox(
      chunk, chunkId, "b1boiler_pipe",
      5.95, floorY - 5.0 + 1.15, 26.05,
      0.28, 2.3, 0.28, this.schoolMetalMat || this.schoolBoilerMat,
    );
    this.addBoilerDrumUnit(
      chunk, chunkId, "b1boiler_drum_west",
      3.15, floorY - 5.0 + 0.48, 24.6,
    );
    const boilerGlow = new THREE.PointLight(0x16382e, 0.42, 5.2, 2);
    boilerGlow.position.set(22.4, floorY - 3.85, 26.2);
    boilerGlow.name = `${chunkId}_b1boiler_glow`;
    this.scene.add(boilerGlow);
    chunk.meshes.push(boilerGlow);

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
      ["b1_maze_s_h1w", -2.1, 45.1, 11.0, t],
      ["b1_maze_s_h1e", 16.45, 45.1, 6.5, t],
      ["b1_maze_s_h1ee", 23.55, 45.1, 4.1, t],
      ["b1_maze_s_h2w", -2.1, 51.4, 11.0, t],
      ["b1_maze_s_h2e", 17.7, 51.4, 9.0, t],
      ["b1_maze_s_v_dead", 4.15, 54.4, t, 3.4],
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
      this.dressMazePartitionTrim(chunk, chunkId, name, x, y, z, sx, sz, h);
    }

    this.placeRoomShell(chunk, chunkId, "b1_maze_class_w", {
      minX: 3.4,
      maxX: 7.08,
      minZ: 45.1,
      maxZ: 51.4,
      wallY: y,
      height: h,
      thickness: t,
      material: wallMat,
      doors: [{ wall: "e", center: 48.2, width: 2.55 }],
    });
    this.placeRoomShell(chunk, chunkId, "b1_maze_class_e", {
      minX: 9.72,
      maxX: 13.2,
      minZ: 45.1,
      maxZ: 51.4,
      wallY: y,
      height: h,
      thickness: t,
      material: wallMat,
      doors: [{ wall: "w", center: 48.2, width: 2.55 }],
    });

    this.ensureSchoolCorridorMaterials();
    if (!this.schoolLinoMat) {
      this.schoolLinoMat = new THREE.MeshStandardMaterial({
        color: 0x2a2218,
        roughness: 0.94,
        metalness: 0,
      });
    }
    const fy = floorY - 5.0;
    this.placeDressedBox(
      chunk, chunkId, "b1flood_lino_w",
      5.15, fy + 0.012, 48.2, 2.35, 0.02, 5.35, this.schoolLinoMat, false,
    );
    this.placeDressedBox(
      chunk, chunkId, "b1flood_lino_e",
      11.35, fy + 0.012, 48.2, 2.35, 0.02, 5.35, this.schoolLinoMat, false,
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_b1flood_desk_w`,
      5.15, fy + 0.35, 48.2, 0.4, [0.72, 0.7, 0.48],
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_b1flood_desk_e`,
      11.35, fy + 0.35, 48.2, -0.3, [0.72, 0.7, 0.48],
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_b1flood_desk_w2`,
      5.15, fy + 0.35, 46.55, 0.18, [0.72, 0.7, 0.48],
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_b1flood_desk_e2`,
      11.35, fy + 0.35, 49.75, -0.22, [0.72, 0.7, 0.48],
    );
    this.addSchoolChairGroup(
      chunk, chunkId, `${chunkId}_b1flood_chair`,
      5.55, fy + 0.12, 47.55, 0.8,
      { fallen: true, collideH: 0.42 },
    );
    this.addSchoolChairGroup(
      chunk, chunkId, `${chunkId}_b1flood_chair_e`,
      11.05, fy + 0.12, 46.85, -0.4,
      { fallen: true, collideH: 0.42 },
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_b1flood_desk_ww`,
      -12.4, fy + 0.35, 48.2, 0.2, [0.72, 0.7, 0.48],
    );
    this.addLibraryShelfUnit(
      chunk, chunkId, "b1flood_shelf_w",
      -16.15, fy + 0.86, 48.15, Math.PI / 2,
    );
    this.collisionWorld.addStaticBox(
      `${chunkId}_b1flood_shelf_w_col`,
      new THREE.Vector3(-16.15, fy + 0.86, 48.15),
      new THREE.Vector3(0.36, 1.68, 0.94),
      chunkId,
    );
    this.addSchoolDeskGroup(
      chunk, chunkId, `${chunkId}_b1flood_desk_ee`,
      38.6, fy + 0.35, 40.2, -0.5, [0.72, 0.7, 0.48],
    );
    this.addSpecimenJarUnit(chunk, chunkId, "b1flood_jar_a", 38.45, fy + 0.92, 40.05);
    this.addSpecimenJarUnit(chunk, chunkId, "b1flood_jar_b", 38.85, fy + 0.92, 40.35);
    this.addChalkboardGroup(
      chunk, chunkId, `${chunkId}_b1flood_board`,
      4.25, fy + 1.42, 48.2, Math.PI / 2, 1.85, 1.05,
    );
    this.addChalkboardGroup(
      chunk, chunkId, `${chunkId}_b1flood_board_e`,
      12.08, fy + 1.42, 50.15, -Math.PI / 2, 1.55, 0.95,
    );
    this.addHallNookSign(
      chunk, chunkId, "b1flood_sign",
      5.15, fy + 2.08, 45.32, 0, "침수",
    );
    this.ensureSchoolCorridorMaterials();
    const floodJamb = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        x, fy + 1.18, z, sx, 2.32, sz,
        this.schoolClassDoorMat, false,
      );
    };
    const floodHeader = (name, x, z, sx, sz) => {
      this.placeDressedBox(
        chunk, chunkId, name,
        x, fy + 2.28, z, sx, 0.18, sz,
        this.schoolDeskDark || this.trimMaterial, false,
      );
    };
    floodJamb("b1flood_door_w_a", 7.08, 46.82, 0.16, 0.18);
    floodJamb("b1flood_door_w_b", 7.08, 49.58, 0.16, 0.18);
    floodHeader("b1flood_door_w_head", 7.08, 48.2, 0.16, 2.58);
    floodJamb("b1flood_door_e_a", 9.72, 46.82, 0.16, 0.18);
    floodJamb("b1flood_door_e_b", 9.72, 49.58, 0.16, 0.18);
    floodHeader("b1flood_door_e_head", 9.72, 48.2, 0.16, 2.58);

    const waterY = floorY - 5.0 + 0.15;
    const waterMat = new THREE.MeshStandardMaterial({
      color: 0x2d7a68,
      roughness: 0.32,
      metalness: 0.12,
      transparent: true,
      opacity: 0.82,
      emissive: 0x145a48,
      emissiveIntensity: 0.08,
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

    const gloom = new THREE.PointLight(0x1c4a3c, 0.28, 4.2, 2.0);
    gloom.position.set(8.4, floorY - 4.05, 48.2);
    gloom.name = `${chunkId}_b1_lab_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    const gloomWest = new THREE.PointLight(0x16382e, 0.22, 3.8, 2.0);
    gloomWest.position.set(-16.2, floorY - 4.05, 30.2);
    gloomWest.name = `${chunkId}_b1_lab_gloom_w`;
    this.scene.add(gloomWest);
    chunk.meshes.push(gloomWest);
    const gloomEast = new THREE.PointLight(0x1a4034, 0.24, 4.0, 2.0);
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

    this.ensureSchoolCorridorMaterials();
    if (!this.schoolBloodFrameMat) {
      this.schoolBloodFrameMat = new THREE.MeshStandardMaterial({
        color: 0x3a1818,
        roughness: 0.72,
        metalness: 0.08,
        emissive: 0x1a0608,
        emissiveIntensity: 0.08,
      });
    }
    for (const [name, x] of [["w", -25.6], ["e", -24.4]]) {
      this.addPortraitFrameUnit(
        chunk, chunkId, `f2blood_frame_${name}`,
        x, floorY + 5.0 + 1.38, -15.1, 0,
      );
    }
    const bloodGlow = new THREE.PointLight(0x2a080c, 0.42, 5.0, 2);
    bloodGlow.position.set(-25.0, floorY + 6.35, -14.4);
    bloodGlow.name = `${chunkId}_f2blood_glow`;
    this.scene.add(bloodGlow);
    chunk.meshes.push(bloodGlow);

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
      this.dressMazePartitionTrim(chunk, chunkId, name, x, y, z, sx, sz, h);
    }

    this.placeRoomShell(chunk, chunkId, "gallery_maze_room_nw", {
      minX: -37.5,
      maxX: -33.72,
      minZ: -47.2,
      maxZ: -40.6,
      wallY: y,
      height: h,
      thickness: t,
      material: mazeMat,
      skip: { n: true, s: true },
      doors: [{ wall: "e", center: -45.2, width: 2.4 }],
    });
    this.placeRoomShell(chunk, chunkId, "gallery_maze_room_ne", {
      minX: -31.28,
      maxX: -24.6,
      minZ: -47.2,
      maxZ: -40.6,
      wallY: y,
      height: h,
      thickness: t,
      material: mazeMat,
      skip: { n: true, s: true },
      doors: [{ wall: "w", center: -45.2, width: 2.4 }],
    });
    this.placeRoomShell(chunk, chunkId, "gallery_maze_room_sw", {
      minX: -31.2,
      maxX: -23.72,
      minZ: -2.4,
      maxZ: 4.2,
      wallY: y,
      height: h,
      thickness: t,
      material: mazeMat,
      skip: { n: true, s: true },
      doors: [{ wall: "e", center: 1.2, width: 2.4 }],
    });
    this.placeRoomShell(chunk, chunkId, "gallery_maze_room_se", {
      minX: -21.28,
      maxX: -17.5,
      minZ: -2.4,
      maxZ: 4.2,
      wallY: y,
      height: h,
      thickness: t,
      material: mazeMat,
      skip: { n: true, s: true },
      doors: [{ wall: "w", center: 1.2, width: 2.4 }],
    });

    this.ensureSchoolCorridorMaterials();
    const gy = floorY + 5.0;
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2blood_portrait_n",
      -28.4, gy + 1.38, -45.2, -Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2blood_portrait_s",
      -32.5, gy + 1.38, 1.55, Math.PI,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2blood_portrait_w",
      -46.2, gy + 1.38, -16.8, Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2blood_portrait_e",
      -19.8, gy + 1.38, -10.4, -Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2gallery_frame_s0",
      -28.8, gy + 1.38, -2.12, Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2gallery_frame_s1",
      -19.4, gy + 1.38, 1.15, -Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2gallery_frame_n0",
      -36.4, gy + 1.38, -45.2, Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2gallery_frame_n1",
      -24.8, gy + 1.38, -45.2, -Math.PI / 2,
    );
    this.addPortraitFrameUnit(
      chunk, chunkId, "f2gallery_frame_w0",
      -46.2, gy + 1.38, -12.6, Math.PI / 2,
    );
    this.addHallNookSign(
      chunk, chunkId, "f2gallery_sign",
      -20.35, gy + 2.08, -10.55, -Math.PI / 2, "액자",
    );

    const bloodY = floorY + 5.04;
    const poolMat = new THREE.MeshStandardMaterial({
      color: 0x9a1418,
      roughness: 0.38,
      metalness: 0.06,
      transparent: true,
      opacity: 0.9,
      emissive: 0x5a080c,
      emissiveIntensity: 0.07,
      depthWrite: false,
    });
    this.addWingPlane(chunk, chunkId, "blood_north_lab", 12.4, 8.6, -28.4, bloodY, -45.2, poolMat);
    this.addWingPlane(chunk, chunkId, "blood_west_lab", 9.6, 16.4, -46.2, bloodY, -20.4, poolMat);
    this.addWingPlane(chunk, chunkId, "blood_south_lab", 14.8, 8.4, -32.5, bloodY, 1.6, poolMat);

    const gloom = new THREE.PointLight(0x5a1018, 0.32, 4.4, 2.0);
    gloom.position.set(-22.5, floorY + 6.15, -42.0);
    gloom.name = `${chunkId}_f2_lab_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    const gloomWest = new THREE.PointLight(0x4a0c14, 0.26, 4.0, 2.0);
    gloomWest.position.set(-48.0, floorY + 6.15, -12.0);
    gloomWest.name = `${chunkId}_f2_lab_gloom_w`;
    this.scene.add(gloomWest);
    chunk.meshes.push(gloomWest);
    const gloomSouth = new THREE.PointLight(0x521018, 0.28, 4.2, 2.0);
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
      emissiveIntensity: 0.08,
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

    const gloom = new THREE.PointLight(0x1c4a3c, 0.32, 4.6, 2.0);
    gloom.position.set(10.4, floorY - 4.05, 31.6);
    gloom.name = `${chunkId}_b1_flood_gloom`;
    this.scene.add(gloom);
    chunk.meshes.push(gloom);
    const gloomEast = new THREE.PointLight(0x16382e, 0.26, 4.2, 2.0);
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
      emissiveIntensity: 0.08,
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

    const shrineGlow = new THREE.PointLight(0x6a1018, 0.38, 4.8, 2.0);
    shrineGlow.position.set(-34.0, floorY + 6.1, -22.0);
    shrineGlow.name = `${chunkId}_2f_blood_glow`;
    this.scene.add(shrineGlow);
    chunk.meshes.push(shrineGlow);
    const poolGlow = new THREE.PointLight(0x4a0a10, 0.3, 4.2, 2.0);
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
      addDynamicDoor("door-left-workshop", "생활관 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      this.dressDormRoom(chunk, center, chunkId, floorY);
    } else if (isPlayroom) {
      addDynamicDoor("door-right-playroom", "인형 교실 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_dollclass_east`, "인형 교실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressDollClass(chunk, center, chunkId, floorY);
    } else if (isStorage) {
      addDynamicDoor("door-left-storage", "준비물 창고 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_prepstore_north`, "준비물 창고 북쪽 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_prepstore_west`, "준비물 창고 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressPrepStore(chunk, center, chunkId, floorY);
    } else if (isArchive) {
      addDynamicDoor("door-archive", "폐관 도서실 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_closedlib_north`, "폐관 도서실 북쪽 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_closedlib_east`, "폐관 도서실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);

      const deskGeo = this.getBoxGeometry(2.4, 0.72, 1.2);
      const deskMesh = new THREE.Mesh(deskGeo, this.propMaterial);
      deskMesh.position.set(center.x, floorY + 0.36, center.z + 1.5);
      deskMesh.castShadow = true;
      deskMesh.receiveShadow = true;
      deskMesh.name = `${chunkId}_archive_desk`;
      this.scene.add(deskMesh);
      chunk.meshes.push(deskMesh);
      this.collisionWorld.addStaticBox(deskMesh.name, deskMesh.position, new THREE.Vector3(2.4, 0.72, 1.2), chunkId);
      this.ensureSpecialNookMaterials();
      for (const side of [-1, 1]) {
        const spines = new THREE.Mesh(this.getBoxGeometry(0.06, 2.15, 4.7), this.schoolBookSpineMat);
        spines.position.set(center.x + side * 3.28, floorY + 1.2, center.z - 2.2);
        spines.name = `${chunkId}_archive_spines_${side < 0 ? "w" : "e"}`;
        this.addSchoolProp(chunk, spines);
      }
      this.addLooseBooks(chunk, center.x, floorY + 0.76, center.z + 1.5, 0.15, 4);
      this.dressClosedLibrary(chunk, center, chunkId, floorY);
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
      addDynamicDoor("door-final-lock-room", "비품실 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_supply_west`, "비품실 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_supply_east`, "비품실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressSupplyRoom(chunk, center, chunkId, floorY);
    } else if (type === "flicker_room") {
      addDynamicDoor("door-flicker-room", "시청각실 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_av_north`, "시청각실 북쪽 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_av_west`, "시청각실 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_av_east`, "시청각실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressAvRoom(chunk, center, chunkId, floorY);
    } else if (type === "omen_room") {
      addDynamicDoor("door-omen-room", "상담실 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      this.dressOmenRoom(chunk, center, chunkId, floorY);
    } else if (type === "static_room") {
      addDynamicDoor("door-static-room", "방송 창고 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      this.dressStaticRoom(chunk, center, chunkId, floorY);
    } else if (type === "stairs_2f") {
      addDynamicDoor("door-stairs-2f", "2층 계단실", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor("door-stairs-2f-north", "2층 북실 통로", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor("door-stairs-2f-gallery", "2층 액자방", [-1.35, 5.0, -5.8], [0.22, 2.35, 2.4]);
    } else if (type === "stairs_b1") {
      addDynamicDoor("door-stairs-b1", "지하 계단실", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
    } else if (type === "tatami_room" || type === "pillar_room") {
      addDynamicDoor("door-tatami-room", "예절실 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_etiquette_west`, "예절실 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_etiquette_east`, "예절실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);

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
      this.dressEtiquetteRoom(chunk, center, chunkId, floorY);
    } else if (isClassroomType(type)) {
      const classLabel = type === "nurse_office" ? "보건실 문"
        : type === "music_room" ? "음악실 문"
        : type === "faculty_office" ? "교무실 문"
        : type === "science_lab" ? "과학실 문"
        : type === "gymnasium" ? "체육관 문"
        : "교실 문";
      const open = this.getOpenings(chunk.cx, chunk.cz);
      if (open.N) addDynamicDoor(`${chunkId}_class_door`, classLabel, [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      else if (open.S) addDynamicDoor(`${chunkId}_class_door`, classLabel, [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      else if (open.E) addDynamicDoor(`${chunkId}_class_door`, classLabel, [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      else addDynamicDoor(`${chunkId}_class_door`, classLabel, [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressClassroom(chunk, center, chunkId, floorY);
    } else if (type === "courtyard") {
      this.dressCourtyard(chunk, center, chunkId, floorY);
    } else if (type === "auditorium") {
      addDynamicDoor(`${chunkId}_class_door`, "강당 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressAuditorium(chunk, center, chunkId, floorY);
    } else if (type === "foyer") {
      addDynamicDoor(`${chunkId}_foyer_east`, "강당 로비 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_foyer_north`, "체육관 쪽 로비 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      this.dressFoyer(chunk, center, chunkId, floorY);
    } else if (type === "art_room") {
      addDynamicDoor(`${chunkId}_art_door`, "미술실 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      this.dressArtRoom(chunk, center, chunkId, floorY);
    } else if (type === "studio") {
      addDynamicDoor(`${chunkId}_studio_south`, "촬영실 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_studio_west`, "촬영실 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressStudio(chunk, center, chunkId, floorY);
    } else if (type === "broadcast") {
      addDynamicDoor(`${chunkId}_broadcast_north`, "방송실 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_broadcast_east`, "방송실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressBroadcast(chunk, center, chunkId, floorY);
    } else if (type === "darkroom") {
      addDynamicDoor(`${chunkId}_darkroom_west`, "암실 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_darkroom_east`, "암실 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressDarkroom(chunk, center, chunkId, floorY);
    } else if (type === "greenroom") {
      addDynamicDoor(`${chunkId}_greenroom_north`, "대기실 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_greenroom_west`, "대기실 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      addDynamicDoor(`${chunkId}_greenroom_east`, "대기실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressGreenroom(chunk, center, chunkId, floorY);
    } else if (type === "home_ec") {
      addDynamicDoor(`${chunkId}_home_ec_north`, "가정실 문", [0.0, 0.0, -7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_home_ec_east`, "가정실 동쪽 문", [7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressHomeEc(chunk, center, chunkId, floorY);
    } else if (type === "club_room") {
      addDynamicDoor(`${chunkId}_club_south`, "서도부 문", [0.0, 0.0, 7.8], [3.7, 2.35, 0.22]);
      addDynamicDoor(`${chunkId}_club_west`, "서도부 서쪽 문", [-7.8, 0.0, 0.0], [0.22, 2.35, 3.7]);
      this.dressClubRoom(chunk, center, chunkId, floorY);
    }

    // 2. Cabinets
    const cabinetMaterial = this.textures.createLockerMaterial();
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
      addDynamicCabinet("cabinet-workshop", "생활관 신발장", [-5.0, 0.0, -5.0], -Math.PI / 2);
    } else if (isPlayroom) {
      addDynamicCabinet("cabinet-playroom", "인형 교실 신발장", [5.0, 0.0, -5.0], Math.PI / 2);
    } else if (isStorage) {
      addDynamicCabinet("cabinet-storage", "준비물 신발장", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (isArchive) {
      addDynamicCabinet("cabinet-archive", "폐관 신발장", [-5.0, 0.0, 5.0], -Math.PI / 2);
    } else if (chunk.cx === 1 && chunk.cz === 0) {
      addDynamicCabinet("cabinet_chokepoint_1_0", "복도 신발장", [-5.25, 0.0, 5.25], 0);
      addDynamicCabinet("cabinet_chokepoint_1_0_n", "복도 신발장", [-5.25, 0.0, -5.25], Math.PI);
    } else if (chunk.cx === 0 && chunk.cz === 1) {
      addDynamicCabinet("cabinet_junction_0_1", "교차로 신발장", [-5.25, 0.0, 5.25], 0);
    } else if (type === "omen_room") {
      addDynamicCabinet("cabinet-omen-room", "상담실 벽장", [5.4, 0.0, -5.4], Math.PI / 2);
    } else if (type === "static_room") {
      addDynamicCabinet("cabinet-static-room", "방송 창고 벽장", [-5.4, 0.0, -5.4], -Math.PI / 2);
    } else if (type === "flicker_room") {
      addDynamicCabinet("cabinet-flicker-room", "시청각실 벽장", [5.4, 0.0, 2.85], Math.PI / 2);
    } else if (type === "wide_room") {
      addDynamicCabinet("cabinet-supply-room", "비품실 벽장", [0.0, 0.0, -5.65], Math.PI);
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
      addDynamicCabinet("cabinet-tatami-room", "예절실 벽장", [7.1, 0.0, 0.0], -Math.PI / 2);
    } else if (isClassroomType(type)) {
      const cabLabel = type === "nurse_office" ? "보건실 벽장"
        : type === "music_room" ? "음악실 벽장"
        : type === "faculty_office" ? "교무실 벽장"
        : type === "science_lab" ? "과학실 벽장"
        : type === "gymnasium" ? "체육관 벽장"
        : "교실 신발장";
      const cabPos = type === "gymnasium" ? [-5.4, 0.0, 5.4] : [5.4, 0.0, 5.4];
      addDynamicCabinet(`cabinet_class_${chunk.cx}_${chunk.cz}`, cabLabel, cabPos, Math.PI / 2);
    } else if (type === "courtyard") {
      addDynamicCabinet(`cabinet_yard_${chunk.cx}_${chunk.cz}`, "중정 신발장", [5.5, 0.0, 6.35], 0);
      addDynamicCabinet(`cabinet_yard2_${chunk.cx}_${chunk.cz}`, "중정 반대 신발장", [-5.5, 0.0, -6.35], Math.PI);
    } else if (type === "auditorium") {
      addDynamicCabinet(`cabinet_aud_${chunk.cx}_${chunk.cz}`, "강당 신발장", [-5.2, 0.0, 5.85], 0);
    } else if (type === "foyer") {
      addDynamicCabinet(`cabinet_foyer_${chunk.cx}_${chunk.cz}`, "로비 신발장", [5.35, 0.0, 5.65], Math.PI);
    } else if (type === "art_room") {
      addDynamicCabinet(`cabinet_art_${chunk.cx}_${chunk.cz}`, "미술실 벽장", [5.4, 0.0, 5.4], Math.PI / 2);
    } else if (type === "studio") {
      addDynamicCabinet(`cabinet_studio_${chunk.cx}_${chunk.cz}`, "촬영실 벽장", [5.4, 0.0, 5.4], Math.PI / 2);
    } else if (type === "broadcast") {
      addDynamicCabinet(`cabinet_broadcast_${chunk.cx}_${chunk.cz}`, "방송실 벽장", [5.35, 0.0, 5.65], Math.PI);
    } else if (type === "darkroom") {
      addDynamicCabinet(`cabinet_darkroom_${chunk.cx}_${chunk.cz}`, "암실 벽장", [-5.4, 0.0, 5.4], 0);
    } else if (type === "greenroom") {
      addDynamicCabinet(`cabinet_greenroom_${chunk.cx}_${chunk.cz}`, "대기실 벽장", [5.4, 0.0, -5.4], Math.PI);
    } else if (type === "home_ec") {
      addDynamicCabinet(`cabinet_home_ec_${chunk.cx}_${chunk.cz}`, "가정실 벽장", [5.4, 0.0, 5.4], 0);
    } else if (type === "club_room") {
      addDynamicCabinet(`cabinet_club_${chunk.cx}_${chunk.cz}`, "서도부 벽장", [5.4, 0.0, 5.4], 0);
    } else if (this.isPracticeChunk(chunk.cx, chunk.cz)) {
      addDynamicCabinet(`cabinet_practice_${chunk.cx}_${chunk.cz}`, "연습실 신발장", [6.35, 0.0, 6.45], Math.PI);
      addDynamicCabinet(`cabinet_practice2_${chunk.cx}_${chunk.cz}`, "연습실 반대 신발장", [-6.35, 0.0, 6.45], Math.PI);
    } else if (this.isTrophyChunk(chunk.cx, chunk.cz)) {
      const sides = this.getHallSides(chunk.cx, chunk.cz);
      addDynamicCabinet(`cabinet_trophy_${chunk.cx}_${chunk.cz}`, "트로피 신발장", [6.35, 0.0, sides.s - 0.9], 0);
      addDynamicCabinet(`cabinet_trophy2_${chunk.cx}_${chunk.cz}`, "트로피 반대 신발장", [-6.35, 0.0, -(sides.n - 0.3)], Math.PI);
    } else if (this.isMemorialChunk(chunk.cx, chunk.cz)) {
      const sides = this.getHallSides(chunk.cx, chunk.cz);
      addDynamicCabinet(`cabinet_mem_${chunk.cx}_${chunk.cz}`, "기념관 신발장", [6.35, 0.0, sides.s - 0.84], 0);
      addDynamicCabinet(`cabinet_mem2_${chunk.cx}_${chunk.cz}`, "기념관 반대 신발장", [-6.35, 0.0, -(sides.n - 0.3)], Math.PI);
    } else if (this.isSkybridgeChunk(chunk.cx, chunk.cz)) {
      const sides = this.getHallSides(chunk.cx, chunk.cz);
      addDynamicCabinet(`cabinet_sky_${chunk.cx}_${chunk.cz}`, "연결복도 신발장", [5.35, 0.0, sides.s - 0.77], 0);
      addDynamicCabinet(`cabinet_sky2_${chunk.cx}_${chunk.cz}`, "연결복도 반대 신발장", [-5.35, 0.0, -(sides.n - 0.3)], Math.PI);
    } else if (!isStart && !isEvent && !isArchive && !type.includes("stairs") && (type.includes("room") || type.includes("storage")) && rand() < 0.4) {
      addDynamicCabinet(`cabinet_${chunk.cx}_${chunk.cz}`, "복도 신발장", [-5.2, 0.0, -5.2], -Math.PI / 2);
    }

    const hallLike = type === "start" || type === "corridor_ns" || type === "corridor_ew"
      || type === "t_junction" || type === "cross_junction" || type === "narrow_ns" || type === "dead_end";
    const mazeChicanes = this.getHallChicanes(chunk.cx, chunk.cz);
    const mazeHall = hallLike && (mazeChicanes.ew || mazeChicanes.ns);
    const hallFacingYaw = (lx, lz) => {
      if (mazeChicanes.ew && Math.abs(lz) >= Math.abs(lx) - 0.2) {
        return lz > 0 ? 0 : Math.PI;
      }
      if (mazeChicanes.ns) {
        return lx > 0 ? Math.PI / 2 : -Math.PI / 2;
      }
      return lz > 0 ? 0 : Math.PI;
    };
    if (this.isRoofHallChunk(chunk.cx, chunk.cz)) {
      addDynamicCabinet(`cabinet_roof_${chunk.cx}_${chunk.cz}`, "옥상 신발장", [-5.25, 0.0, 0.0], Math.PI / 2);
    } else if (hallLike && chunk.cabinets.length === 0) {
      const nookMask = this.getHallNookMask(chunk.cx, chunk.cz);
      const pickAlong = (side) => {
        const doors = this.getHallDoorAlong(chunk.cx, chunk.cz, side);
        const prefer = (chunk.cx + chunk.cz) % 2 === 0 ? doors.length - 1 : 0;
        return doors[Math.max(0, prefer)] ?? 5.25;
      };
      let south = [pickAlong("s"), 0.0, 5.25];
      if (nookMask.s) south = [pickAlong("s"), 0.0, 5.25];
      else if (nookMask.n) south = [pickAlong("n"), 0.0, -5.25];
      else if (nookMask.e) south = [5.25, 0.0, pickAlong("e")];
      else if (nookMask.w) south = [-5.25, 0.0, pickAlong("w")];
      addDynamicCabinet(`cabinet_hall_${chunk.cx}_${chunk.cz}`, "신발장", south, hallFacingYaw(south[0], south[2]));
    }
    if (mazeHall && chunk.cabinets.length === 1) {
      const first = chunk.cabinets[0];
      const lx = first.position.x - center.x;
      const lz = first.position.z - center.z;
      const nookMask = this.getHallNookMask(chunk.cx, chunk.cz);
      const candidates = [
        [lx, 0.0, -lz],
        [-lx, 0.0, lz],
        [-lx, 0.0, -lz],
      ];
      const live = (px, pz) => (pz < -2 && nookMask.n) || (pz > 2 && nookMask.s) || (px > 2 && nookMask.e) || (px < -2 && nookMask.w);
      const next = candidates.find(([px, , pz]) => live(px, pz));
      if (next) {
        addDynamicCabinet(
          `cabinet_hall2_${chunk.cx}_${chunk.cz}`,
          "반대편 신발장",
          next,
          hallFacingYaw(next[0], next[2]),
        );
      }
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
        "보건실 장부에 출석이 끝나지 않은 이름이 넷이다. 별관에서 문이 다르면 길을 외우십시오.");
    } else if (type === "music_room") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "피아노 뚜껑이 열린 채로 녹슬어 있다. 한 음이 모자라면 복도가 당신의 이름을 외운다.");
    } else if (type === "faculty_office") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "교무실 출석부에 네 이름이 지워져 있다. 별관 끝 교실은 한 번만 들어가십시오.");
    } else if (type === "science_lab") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "약품 냄새가 복도까지 샌다. 가스관이 아직 식지 않았다.");
    } else if (type === "gymnasium") {
      addLoreNote(`${chunkId}_lore`, [-7.35, 1.42, 0.0], Math.PI / 2,
        "체육관 줄이 어제와 같다. 창밖의 운동장은 없다. 공이 혼자 굴러간다.");
    } else if (type === "courtyard") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.45], 0,
        "중정 난간 너머는 운동장이 아니다. 아래로 떨어지지 마십시오.");
    } else if (type === "auditorium") {
      addLoreNote(`${chunkId}_lore`, [-7.35, 1.42, 0.0], Math.PI / 2,
        "강당 막이 내려와 있다. 객석 이름은 어제 출석과 같다.");
    } else if (type === "foyer") {
      addLoreNote(`${chunkId}_lore`, [-7.35, 1.42, 0.0], Math.PI / 2,
        "로비 창구는 닫혀 있다. 표 없이 강당 문만 열린다.");
    } else if (type === "art_room") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.45], 0,
        "물감이 마르지 않았다. 이젤의 얼굴은 어제 출석과 같다.");
    } else if (type === "studio") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.45], 0,
        "조명이 꺼져 있다. 카메라가 아직 당신을 보고 있다.");
    } else if (type === "broadcast") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.45], Math.PI,
        "마이크가 뜨겁다. 이름을 대지 마십시오.");
    } else if (type === "darkroom") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "빨간 불만 남아 있다. 현상액을 흔들지 마십시오.");
    } else if (type === "greenroom") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, 7.45], Math.PI,
        "의상이 이름을 입고 있다. 거울을 보지 마십시오.");
    } else if (type === "home_ec") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, 7.45], Math.PI,
        "재봉틀이 혼자 돌아간다. 실을 밟지 마십시오.");
    } else if (type === "club_room") {
      addLoreNote(`${chunkId}_lore`, [7.55, 1.42, 0.0], -Math.PI / 2,
        "먹물이 아직 마르지 않았다. 글씨를 읽지 마십시오.");
    } else if (this.isPracticeChunk(chunk.cx, chunk.cz)) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, 1.72], Math.PI,
        "연습실 가운데가 막혀 있다. 남쪽으로 돌아가십시오.");
    } else if (type === "omen_room") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "상담실 제단이 이름을 듣는다. 눈을 감지 마십시오.");
    } else if (type === "static_room") {
      addLoreNote(`${chunkId}_lore`, [-7.55, 1.42, 3.4], Math.PI / 2,
        "방송 창고 화면이 꺼져도 출석을 센다.");
    } else if (type === "flicker_room") {
      addLoreNote(`${chunkId}_lore`, [7.55, 1.42, -2.15], -Math.PI / 2,
        "시청각실 화면이 교실을 보고 있다. 테이프를 틀지 마십시오.");
    } else if (type === "wide_room") {
      addLoreNote(`${chunkId}_lore`, [4.8, 1.42, -7.55], 0,
        "비품 철창에 어제 출석이 잠겨 있다. 열쇠를 찾지 마십시오.");
    } else if (type === "classroom") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0);
    } else if (isWorkshop) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "생활관 요람은 비어 있다. 젖은 발자국이 남쪽 지하 계단으로 이어진다.");
    } else if (isPlayroom) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "인형 교실의 눈이 아직 줍지 않은 이름을 가리킨다. 마주치면 따라가십시오.");
    } else if (isStorage) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "준비물 선반 뒤에 도자기 열쇠가 있다. 문을 닫아 추격을 끊으십시오.");
    } else if (isArchive) {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.4, 7.45], Math.PI,
        "폐관 도서실이다. 대출 장부를 읽지 마십시오.");
    } else if (type === "tatami_room" || type === "pillar_room") {
      addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -7.55], 0,
        "예절실이다. 신발을 신지 마십시오.");
    } else if (hallLike && type !== "start") {
      if (this.isSkybridgeChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.72], 0,
          "유리 너머는 운동장이 아니다. 별관으로 건너기 전에 신발장을 확인하십시오.");
      } else if (this.isMemorialChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.72], 0,
          "졸업 액자가 비어 있다. 이름이 넷이면 제단이 열린다.");
      } else if (this.isTrophyChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, 1.72], Math.PI,
          "트로피 이름이 지워져 있다. 컵만 복도를 지킨다.");
      } else if (this.isArcadeChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [1.82, 1.42, 0.0], -Math.PI / 2,
          "아케이드 너머는 중정이다. 서쪽 교실이 닫혀 있다. 기둥 사이로 난간만 보인다.");
      } else if (this.isSpecimenChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "표본이 아직 떠 있다. 유리 너머를 세지 마십시오.");
      } else if (this.isStageWingChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.82], 0,
          "의상이 남쪽 벽에 걸려 있다. 이름을 걸지 마십시오.");
      } else if (this.isLaundryChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "세탁 수레가 어제 출석을 실었다. 옷을 뒤지지 마십시오.");
      } else if (this.isLabLinkChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, 1.82], Math.PI,
          "실험 복도입니다. 남쪽 교실이 닫혀 있다. 가스가 아직 식지 않았습니다.");
      } else if (this.isStairHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.82], 0,
          "지하로 가는 복도다. 콘 너머로 물이 마른다.");
      } else if (this.isNurseryHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "요람이 서쪽 교실에 있다. 동쪽은 창이다.");
      } else if (this.isDollHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "인형 선반이 서쪽 벽에 붙어 있다. 눈을 마주치지 마십시오.");
      } else if (this.isArchiveHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "서고 철 상자가 서쪽 벽에 잠겨 있다. 열지 마십시오.");
      } else if (this.isStorageHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "창고 상자가 서쪽 교실에 쌓여 있다. 어제 출석을 담고 있다.");
      } else if (this.isTeaHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.82], 0,
          "다실 앞이다. 북쪽 교실이 닫혀 있다. 신발을 신지 마십시오.");
      } else if (this.isLostFoundChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "분실물함이다. 어제 신발을 찾지 마십시오.");
      } else if (this.isRoofHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.82], 0,
          "옥상 문은 판자로 막혀 있다. 위로 가지 마십시오.");
      } else if (this.isEastWashChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.72], 0,
          "동쪽 세면 복도다. 물을 틀지 마십시오.");
      } else if (this.isAngelHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [1.82, 1.42, 0.0], -Math.PI / 2,
          "도서실 앞이다. 석고상을 보지 마십시오.");
      } else if (this.isWashFourChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.72], 0,
          "별관 세면 교차로다. 신발장만 북쪽 벽에 있다.");
      } else if (this.isAnnexGateChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.82], 0,
          "별관 입구다. 실내화를 갈아 신지 마십시오.");
      } else if (this.isStartHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, 1.82], Math.PI,
          "현관 신발장이 동쪽 팔에 붙어 있다. 제단함은 북쪽에 있다.");
      } else if (this.isClassWingChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [0.0, 1.42, -1.82], 0,
          "교실 날개다. 책상 사이로 숨으십시오. 카트는 북쪽 벽 자물쇠 앞에 있다.");
      } else if (this.isUncatHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [-1.82, 1.42, 0.0], Math.PI / 2,
          "교차로다. 칠판의 출석을 읽지 마십시오.");
      } else if (this.isNorthHallChunk(chunk.cx, chunk.cz)) {
        addLoreNote(`${chunkId}_lore`, [1.82, 1.42, 0.0], -Math.PI / 2,
          "북쪽 복도다. 알코브 신발장으로.");
      } else {
      const kinds = [0, 1, 2, 3].map((slot) => this.getHallNookKind(chunk.cx, chunk.cz, slot));
      let body = "교실 번호가 어제와 같다. 창밖의 운동장은 없다.";
      if (kinds.includes("library")) {
        body = "도서실 창이 판자로 막혀 있다. 대출 장부에 같은 이름이 네 번 적혀 있다.";
      } else if (kinds.includes("washroom")) {
        body = "화장실 수도가 혼자 열린다. 거울에 손전등을 대지 마라.";
      } else if (kinds.includes("boarded")) {
        body = "폐쇄된 교실의 출석부가 어제 날짜로 멈춰 있다. 칠판을 읽지 마라.";
      } else if (kinds.includes("empty")) {
        body = "책상을 걷어낸 교실이다. 분필 가루만 복도로 샌다.";
      } else if (kinds.includes("shoes")) {
        body = "실내화 칸이다. 어제 신발을 신지 마십시오.";
      } else if (kinds.includes("music")) {
        body = "보면대가 혼자 서 있다. 악보를 넘기지 마십시오.";
      } else if (kinds.includes("science")) {
        body = "표본 병이 아직 따뜻하다. 뚜껑을 열지 마십시오.";
      } else if (kinds.includes("shrine")) {
        body = "위패 액자의 얼굴이 비어 있다. 이름을 읽지 마십시오.";
      }
      const neighborHall = this.isMazeHall(chunk.cx + 1, chunk.cz) || this.isMazeHall(chunk.cx - 1, chunk.cz)
        || this.isMazeHall(chunk.cx, chunk.cz + 1) || this.isMazeHall(chunk.cx, chunk.cz - 1);
      if (neighborHall && this.isMazeHall(chunk.cx, chunk.cz)) {
        body = `${body} 교실 뒷문으로 옆 복도가 열린다.`;
      }
      addLoreNote(`${chunkId}_lore`, [-1.18, 1.38, -5.4], Math.PI / 2, body);
      }
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
        position: [center.x, floorY, center.z - 1.12],
      }, this.scene);
      exit.chunkId = chunkId;
      this.scene.add(exit.group);
      chunk.finalExit = exit;
      this.collisionWorld.addStaticBox(
        `${chunkId}_altar_block`,
        new THREE.Vector3(center.x, floorY + 0.4, center.z - 1.12),
        new THREE.Vector3(1.72, 0.8, 0.98),
        chunkId,
      );
    }

    if (chunk.cx === 4 && chunk.cz === 0) {
      this.dressAnnexGate(chunk, center, chunkId, floorY);
    }
    if (this.isRoofHallChunk(chunk.cx, chunk.cz)) {
      this.dressRoofHall(chunk, center, chunkId, floorY);
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
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "형광등 스위치");
      if (this.isMazeHall(chunk.cx, chunk.cz)) {
        const chicanes = this.getHallChicanes(chunk.cx, chunk.cz);
        const sides = this.getHallSides(chunk.cx, chunk.cz);
        if (chicanes.ew) {
          spawnSafeLight("wall-switch", -4.4, wallH, -(sides.n - 0.18), Math.PI, "복도 스위치");
          spawnSafeLight("wall-switch", 4.4, wallH, sides.s - 0.18, 0, "복도 스위치");
        }
        if (chicanes.ns) {
          spawnSafeLight("wall-switch", -(sides.w - 0.18), wallH, -4.4, Math.PI / 2, "복도 스위치");
          spawnSafeLight("wall-switch", sides.e - 0.18, wallH, 4.4, -Math.PI / 2, "복도 스위치");
        }
      } else if (this.isRoofHallChunk(chunk.cx, chunk.cz)) {
        const sides = this.getHallSides(chunk.cx, chunk.cz);
        spawnSafeLight("wall-switch", -(sides.w - 0.18), wallH, -4.2, Math.PI / 2, "옥상 스위치");
        spawnSafeLight("wall-switch", -4.2, wallH, -(sides.n - 0.18), Math.PI, "옥상 스위치");
      } else {
        spawnSafeLight("wall-switch", -1.18, wallH, -5.2, Math.PI / 2, "벽 스위치");
        spawnSafeLight("wall-switch", 1.18, wallH, 5.2, -Math.PI / 2, "벽 스위치");
        spawnSafeLight("wall-switch", -5.2, wallH, -1.18, Math.PI, "벽 스위치");
        spawnSafeLight("wall-switch", 5.2, wallH, 1.18, 0, "벽 스위치");
      }
      if (type === "start") {
        spawnSafeLight("floor-lamp", -6.1, floorH, -6.1, Math.PI * 0.25, "신당 낡은 스탠드");
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
      spawnSafeLight("wall-switch", 1.6, wallH, -7.58, Math.PI, "생활관 입구 스위치");
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "생활관 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "생활관 벽 스위치");
      spawnSafeLight("floor-lamp", 4.0, floorH, 2.5, -Math.PI / 4, "생활관 낡은 스탠드");
    } else if (type === "playroom") {
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "인형 교실 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, -7.58, Math.PI, "인형 교실 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "인형 교실 벽 스위치");
      spawnSafeLight("toy-lamp", 3.2, floorH, 2.8, 0, "인형 교실 장난감 램프");
    } else if (type === "storage" || type === "toy_storage") {
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "준비물 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "준비물 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "준비물 벽 스위치");
      spawnSafeLight("floor-lamp", -3.5, floorH, 3.5, 0, "준비물 낡은 스탠드");
    } else if (type === "archive") {
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "폐관 입구 스위치");
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "폐관 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "폐관 벽 스위치");
      spawnSafeLight("floor-lamp", 3.5, floorH, -3.5, 0, "폐관 낡은 스탠드");
    } else if (type === "event") {
      // Mirror event room: Flush on south entrance wall and west perimeter wall
      spawnSafeLight("wall-switch", 1.6, wallH, 7.58, 0, "거울방 입구 스위치");
      spawnSafeLight("wall-switch", -1.6, wallH, 7.58, 0, "거울방 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "거울방 벽 스위치");
      spawnSafeLight("floor-lamp", 3.5, floorH, -3.5, 0, "거울방 낡은 스탠드");
    } else if (type === "tatami_room" || type === "pillar_room") {
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "예절실 입구 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "예절실 벽 스위치");
      spawnSafeLight("floor-lamp", 6.2, floorH, 6.2, -Math.PI / 4, "예절실 낡은 스탠드");
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
    } else if (type === "gymnasium") {
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "체육관 입구 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "체육관 형광등");
    } else if (type === "courtyard") {
      spawnSafeLight("wall-switch", 0.0, wallH, -7.58, Math.PI, "중정 입구 스위치");
      spawnSafeLight("ceiling-switch", 5.2, ceilingH, 5.2, 0, "중정 형광등");
      spawnSafeLight("floor-lamp", -5.6, floorH, 5.6, 0, "중정 스탠드");
    } else if (type === "auditorium") {
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "강당 입구 스위치");
      spawnSafeLight("ceiling-switch", 2.4, ceilingH, 0.0, 0, "강당 형광등");
    } else if (type === "foyer") {
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "로비 입구 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "로비 형광등");
    } else if (type === "art_room") {
      spawnSafeLight("wall-switch", 0.0, wallH, -7.58, Math.PI, "미술실 입구 스위치");
      spawnSafeLight("ceiling-switch", 2.2, ceilingH, 1.4, 0, "미술실 형광등");
    } else if (type === "studio") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "촬영실 입구 스위치");
      spawnSafeLight("ceiling-switch", 4.4, ceilingH, -2.2, 0, "촬영실 형광등");
    } else if (type === "broadcast") {
      spawnSafeLight("wall-switch", 0.0, wallH, -7.58, Math.PI, "방송실 입구 스위치");
      spawnSafeLight("ceiling-switch", -4.2, ceilingH, -3.2, 0, "방송실 형광등");
    } else if (type === "darkroom") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "암실 입구 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "암실 안전등");
    } else if (type === "greenroom") {
      spawnSafeLight("wall-switch", 0.0, wallH, -7.58, Math.PI, "대기실 입구 스위치");
      spawnSafeLight("floor-lamp", 5.2, floorH, 3.4, 0, "대기실 스탠드");
    } else if (type === "home_ec") {
      spawnSafeLight("wall-switch", 0.0, wallH, -7.58, Math.PI, "가정실 입구 스위치");
      spawnSafeLight("ceiling-switch", 3.2, ceilingH, 0.0, 0, "가정실 형광등");
    } else if (type === "club_room") {
      spawnSafeLight("wall-switch", 0.0, wallH, 7.58, 0, "서도부 입구 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, -2.2, 0, "서도부 형광등");
    } else if (type === "flicker_room") {
      spawnSafeLight("wall-switch", -7.58, wallH, -2.4, Math.PI / 2, "시청각실 벽 스위치");
      spawnSafeLight("ceiling-switch", 0.0, ceilingH, 0.0, 0, "시청각실 형광등");
    } else {
      // Generic fallback room
      spawnSafeLight("wall-switch", -1.6, wallH, -7.58, Math.PI, "벽 스위치");
      spawnSafeLight("wall-switch", -7.58, wallH, 0.0, Math.PI / 2, "벽 스위치");
      spawnSafeLight("floor-lamp", 5.0, floorH, 5.0, -Math.PI / 4, "낡은 스탠드");
    }
  }

  createCanvasTexture(width, height, draw) {
    const canvas = typeof document !== "undefined" ? document.createElement("canvas") : null;
    if (!canvas) return null;
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    draw(ctx, width, height);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.anisotropy = 8;
    texture.needsUpdate = true;
    return texture;
  }

  createBookSpineMaterial(seed = 1) {
    if (!this.bookSpineCache) this.bookSpineCache = new Map();
    if (this.bookSpineCache.has(seed)) return this.bookSpineCache.get(seed);
    const colors = ["#4a2018", "#1a3048", "#3a2a10", "#2a1810", "#5a3020", "#243028", "#4a3828", "#1a1a22", "#6a2420", "#2c2438"];
    const texture = this.createCanvasTexture(256, 512, (ctx, width, height) => {
      ctx.fillStyle = "#1a100c";
      ctx.fillRect(0, 0, width, height);
      const rows = 4;
      for (let row = 0; row < rows; row += 1) {
        const y0 = 10 + row * 126;
        let x = 6;
        let i = 0;
        while (x < width - 6) {
          const w = 7 + ((seed * 13 + row * 11 + i * 7) % 10);
          const h = 86 + ((seed + i * 11) % 24);
          ctx.fillStyle = colors[(seed + row * 5 + i) % colors.length];
          ctx.fillRect(x, y0 + (112 - h), w - 1, h);
          ctx.fillStyle = "#c4a060";
          ctx.fillRect(x + 1, y0 + (112 - h) + 10, Math.max(2, w - 3), 2);
          x += w;
          i += 1;
        }
      }
    });
    const mat = new THREE.MeshStandardMaterial({
      map: texture,
      color: texture ? 0xffffff : 0x3a2418,
      roughness: 0.74,
      metalness: 0.04,
      emissive: 0x120a06,
      emissiveIntensity: 0.14,
    });
    this.bookSpineCache.set(seed, mat);
    return mat;
  }

  createPlywoodMaterial() {
    const texture = this.createCanvasTexture(256, 256, (ctx, width, height) => {
      ctx.fillStyle = "#3a2818";
      ctx.fillRect(0, 0, width, height);
      const bands = ["#5a4030", "#4a3424", "#6a4a32", "#523826"];
      for (let i = 0; i < 6; i += 1) {
        ctx.fillStyle = bands[i % bands.length];
        ctx.fillRect(0, i * 42, width, 38);
        ctx.fillStyle = "#2a1a10";
        ctx.fillRect(0, i * 42 + 38, width, 4);
        ctx.strokeStyle = "rgba(90,70,40,0.35)";
        ctx.lineWidth = 1;
        for (let g = 0; g < 8; g += 1) {
          ctx.beginPath();
          ctx.moveTo(0, i * 42 + 6 + g * 4);
          ctx.lineTo(width, i * 42 + 8 + g * 4);
          ctx.stroke();
        }
      }
      ctx.fillStyle = "#2a2420";
      for (const [x, y] of [[28, 18], [90, 60], [150, 24], [210, 96], [48, 140], [188, 168], [120, 210]]) {
        ctx.beginPath();
        ctx.arc(x, y, 3, 0, Math.PI * 2);
        ctx.fill();
      }
    });
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: texture ? 0xffffff : 0x4a3824,
      roughness: 0.88,
      metalness: 0,
      emissive: 0x2a1810,
      emissiveIntensity: 0.16,
    });
  }

  createWashTileMaterial() {
    const texture = this.createCanvasTexture(256, 256, (ctx, width, height) => {
      ctx.fillStyle = "#1a2220";
      ctx.fillRect(0, 0, width, height);
      for (let y = 0; y < 8; y += 1) {
        for (let x = 0; x < 8; x += 1) {
          ctx.fillStyle = (x + y) % 2 === 0 ? "#2e3836" : "#26322f";
          ctx.fillRect(x * 32 + 2, y * 32 + 2, 28, 28);
        }
      }
    });
    if (texture) {
      texture.wrapS = THREE.RepeatWrapping;
      texture.wrapT = THREE.RepeatWrapping;
      texture.repeat.set(2.4, 2.4);
    }
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: texture ? 0xffffff : 0x2a3230,
      roughness: 0.52,
      metalness: 0.08,
    });
  }

  createGymFloorMaterial() {
    const texture = this.createCanvasTexture(512, 512, (ctx, width, height) => {
      ctx.fillStyle = "#3a2818";
      ctx.fillRect(0, 0, width, height);
      ctx.strokeStyle = "#2a1c10";
      ctx.lineWidth = 6;
      for (let i = 0; i < 18; i += 1) {
        ctx.beginPath();
        ctx.moveTo(0, i * 30);
        ctx.lineTo(width, i * 30);
        ctx.stroke();
      }
      ctx.strokeStyle = "#d2c09a";
      ctx.lineWidth = 5;
      ctx.strokeRect(36, 36, width - 72, height - 72);
      ctx.beginPath();
      ctx.moveTo(width / 2, 36);
      ctx.lineTo(width / 2, height - 36);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(width / 2, height / 2, 58, 0, Math.PI * 2);
      ctx.stroke();
      ctx.strokeRect(36, height / 2 - 70, 90, 140);
      ctx.strokeRect(width - 126, height / 2 - 70, 90, 140);
    });
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: texture ? 0xffffff : 0x3a2818,
      roughness: 0.86,
      metalness: 0.02,
    });
  }

  createCautionTapeMaterial() {
    const texture = this.createCanvasTexture(256, 64, (ctx, width, height) => {
      ctx.fillStyle = "#c9a227";
      ctx.fillRect(0, 0, width, height);
      ctx.fillStyle = "#16120c";
      for (let i = -height; i < width; i += 28) {
        ctx.beginPath();
        ctx.moveTo(i, 0);
        ctx.lineTo(i + 14, 0);
        ctx.lineTo(i + 14 + height, height);
        ctx.lineTo(i + height, height);
        ctx.closePath();
        ctx.fill();
      }
    });
    if (texture) {
      texture.wrapS = THREE.RepeatWrapping;
      texture.repeat.set(3, 1);
    }
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: texture ? 0xffffff : 0xc9a227,
      roughness: 0.7,
      metalness: 0.04,
      emissive: 0x3a2a08,
      emissiveIntensity: 0.16,
    });
  }

  createChalkLabelMaterial(text) {
    const texture = this.createCanvasTexture(512, 128, (ctx, width, height) => {
      ctx.clearRect(0, 0, width, height);
      ctx.fillStyle = "#d8d0c0";
      ctx.font = "600 52px 'Noto Serif KR', serif";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText(text, width / 2, height / 2);
    });
    return new THREE.MeshStandardMaterial({
      map: texture,
      color: 0xffffff,
      transparent: true,
      opacity: 0.92,
      roughness: 0.95,
      metalness: 0,
      emissive: 0x2a2818,
      emissiveIntensity: 0.12,
      side: THREE.DoubleSide,
    });
  }

  createSignMaterial(text) {
    if (!this.signMatCache) this.signMatCache = new Map();
    if (this.signMatCache.has(text)) return this.signMatCache.get(text);
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
    const mat = new THREE.MeshStandardMaterial({
      map: texture,
      roughness: 0.78,
      metalness: 0.04,
      emissive: 0x1a1208,
      emissiveIntensity: 0.16,
      side: THREE.DoubleSide,
    });
    this.signMatCache.set(text, mat);
    return mat;
  }

  dressAnnexGate(chunk, center, chunkId, floorY) {
    this.ensureSchoolCorridorMaterials();
    if (!this.schoolRackMat) {
      this.schoolRackMat = new THREE.MeshStandardMaterial({
        color: 0x3a3228,
        roughness: 0.78,
        metalness: 0.08,
        emissive: 0x0c0804,
        emissiveIntensity: 0.05,
      });
    }
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

    // North corridor face, between the T-spur and the classroom doors at ±5.25.
    const rackZ = center.z - (this.getHallSides(chunk.cx, chunk.cz).n - 0.19);
    for (const [name, x] of [["w", -2.88], ["e", 2.88]]) {
      this.addShoeRackUnit(
        chunk, chunkId, `annexgate_rack_${name}`,
        center.x + x, floorY + 0.48, rackZ, 0,
      );
    }
    this.addHallNookSign(
      chunk, chunkId, "annexgate_sign",
      center.x, floorY + 2.12, center.z - 1.72,
      0, "별관",
    );
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "annex_room_nw", "nw");
    this.dressClosedCornerRoom(chunk, center, chunkId, floorY, "annex_room_ne", "ne");
    this.placeDressedBox(
      chunk, chunkId, "annex_room_nw_l_z",
      center.x - 3.40, floorY + 1.4, center.z - 5.465,
      0.22, 2.8, 3.83, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "annex_room_nw_l_x",
      center.x - 2.435, floorY + 1.4, center.z - 3.55,
      1.93, 2.8, 0.22, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "annex_room_ne_l_z",
      center.x + 3.40, floorY + 1.4, center.z - 5.465,
      0.22, 2.8, 3.83, this.schoolClassWallMat,
    );
    this.placeDressedBox(
      chunk, chunkId, "annex_room_ne_l_x",
      center.x + 2.435, floorY + 1.4, center.z - 3.55,
      1.93, 2.8, 0.22, this.schoolClassWallMat,
    );
    // South cheek pier: unique mid-run so the 16m tube is not a straight
    // offset rectangle. Stays west of the x=70 pinch proof.
    const annexSides = this.getHallSides(chunk.cx, chunk.cz);
    this.placeDressedBox(
      chunk, chunkId, "annexhall_cheek_s",
      center.x + 4.15, floorY + 1.4, center.z + (annexSides.s - 0.20),
      1.25, 2.8, 0.38, this.schoolClassWallMat,
    );
    const glow = new THREE.PointLight(0x201808, 0.48, 6.0, 2);
    glow.position.set(center.x + 3.4, floorY + 2.05, center.z - 1.15);
    glow.name = `${chunkId}_annexgate_glow`;
    this.scene.add(glow);
    chunk.meshes.push(glow);
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
    const skipScatter = type === "foyer" || type === "auditorium" || type === "courtyard"
      || type === "gymnasium" || type === "art_room" || type === "studio" || type === "broadcast"
      || type === "darkroom" || type === "greenroom" || type === "home_ec" || type === "club_room"
      || type === "flicker_room" || type === "wide_room" || type === "omen_room" || type === "static_room"
      || type === "workshop" || type === "playroom" || type === "storage" || type === "archive"
      || type === "tatami_room" || type === "pillar_room";
    if (!isCorridorOrStair && !skipScatter && rand() < 0.25) {
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
    if (type === "studio") return new Set(["north", "east"]);
    if (type === "broadcast") return new Set(["south", "west"]);
    if (type === "darkroom") return new Set(["north", "south"]);
    if (type === "greenroom") return new Set(["south"]);
    if (type === "home_ec") return new Set(["south", "west"]);
    if (type === "club_room") return new Set(["north", "east"]);
    if (type === "workshop" || type === "playroom") return new Set(["south", "east", "west"]);
    if (type === "storage" || type === "event" || type === "wide_room" || type === "stairs_2f") return new Set(["north", "east", "west"]);
    if (type === "stairs_b1") return new Set(["south", "east", "west"]);
    return new Set();
  }

  pickFloorPlacement(type, center, rand) {
    let localX = (rand() - 0.5) * 9.2;
    let localZ = (rand() - 0.5) * 9.2;
    const cx = Math.round(center.x / 16);
    const cz = Math.round(center.z / 16);

    if (this.isPracticeChunk(cx, cz)) {
      localX = rand() < 0.5 ? -5.15 : 5.15;
      localZ = 5.15 + rand() * 1.15;
    } else if (type === "corridor_ns") {
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
    } else if (type === "courtyard") {
      localX = rand() < 0.5 ? 5.15 : -5.15;
      localZ = (rand() - 0.5) * 8.2;
    } else if (type === "auditorium") {
      localX = (rand() - 0.6) * 6.5;
      localZ = rand() < 0.5 ? -0.55 : 0.55;
    } else if (type === "foyer") {
      localX = rand() < 0.5 ? 5.2 : -5.2;
      localZ = rand() < 0.5 ? 5.4 : -5.4;
    } else if (type === "art_room") {
      localX = rand() < 0.5 ? 5.1 : -5.1;
      localZ = rand() < 0.5 ? 5.2 : -3.4;
    } else if (type === "studio") {
      localX = rand() < 0.5 ? 5.1 : -5.1;
      localZ = rand() < 0.5 ? 5.2 : -3.4;
    } else if (type === "broadcast") {
      localX = rand() < 0.5 ? -5.2 : 5.2;
      localZ = rand() < 0.5 ? 5.4 : -4.6;
    } else if (type === "darkroom") {
      localX = rand() < 0.5 ? -5.15 : 5.15;
      localZ = 4.6 + rand() * 1.1;
    } else if (type === "greenroom") {
      localX = rand() < 0.5 ? -5.2 : 5.2;
      localZ = rand() < 0.5 ? 4.4 : -5.2;
    } else if (type === "home_ec") {
      localX = 5.15;
      localZ = rand() < 0.5 ? 5.15 : -5.15;
    } else if (type === "club_room") {
      localX = rand() < 0.5 ? 5.15 : -5.15;
      localZ = 5.15 + rand() * 0.8;
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

    if (this.isPracticeChunk(chunk.cx, chunk.cz)) {
      chunk.waypoints = [
        wp(-6.2, 0), wp(-6.2, 3.5), wp(0, 3.5), wp(6.2, 3.5), wp(6.2, 0),
        wp(-5.2, 5.4), wp(5.2, 5.4),
      ];
    } else if (type === "start" || type === "cross_junction" || type === "t_junction"
      || type === "corridor_ns" || type === "narrow_ns" || type === "corridor_ew") {
      const chicanes = this.getHallChicanes(chunk.cx, chunk.cz);
      const hallOpen = this.getOpenings(chunk.cx, chunk.cz);
      chunk.waypoints = [wp(0, 0)];
      if (!this.isGlassHallChunk(chunk.cx, chunk.cz)) {
        chunk.waypoints.push(wp(-5.2, -5.2), wp(5.2, -5.2), wp(-5.2, 5.2), wp(5.2, 5.2));
      }
      if (!chicanes.ew && !chicanes.ns) {
        chunk.waypoints.push(wp(0, -5.4), wp(0, 5.4), wp(-5.4, 0), wp(5.4, 0));
      }
      if (chicanes.ew) {
        chunk.waypoints.push(
          wp(-6.2, 0), wp(-3.2, 0), wp(3.2, 0), wp(6.2, 0),
        );
        if (!this.isGlassHallChunk(chunk.cx, chunk.cz)) {
          chunk.waypoints.push(
            wp(-5.25, 5.2), wp(5.25, 5.2), wp(-5.25, -5.2), wp(5.25, -5.2),
          );
        }
        if (chicanes.ns || hallOpen.N) chunk.waypoints.push(wp(0, -5.4), wp(0, -6.5));
        if (chicanes.ns || hallOpen.S) chunk.waypoints.push(wp(0, 5.4), wp(0, 6.5));
      }
      if (chicanes.ns) {
        chunk.waypoints.push(wp(0, -6.2), wp(0, -3.2), wp(0, 3.2), wp(0, 6.2));
        if (chicanes.ew || hallOpen.W) chunk.waypoints.push(wp(-5.4, 0), wp(-6.5, 0));
        if (chicanes.ew || hallOpen.E) chunk.waypoints.push(wp(5.4, 0), wp(6.5, 0));
        if (!chicanes.ew) {
          const mask = this.getHallNookMask(chunk.cx, chunk.cz);
          if (mask.w) chunk.waypoints.push(wp(-5.25, 5.2), wp(-5.25, -5.2));
          if (mask.e) chunk.waypoints.push(wp(5.25, 5.2), wp(5.25, -5.2));
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
    } else if (type === "courtyard") {
      chunk.waypoints = [
        wp(0, -6.4), wp(0, -4.6), wp(4.6, -4.6), wp(4.6, 0), wp(4.6, 4.6),
        wp(0, 4.6), wp(-4.6, 4.6), wp(-4.6, 0), wp(-4.6, -4.6),
        wp(6.2, 0), wp(-6.2, 0), wp(0, 6.2), wp(0, -6.2),
      ];
    } else if (type === "auditorium") {
      chunk.waypoints = [
        wp(-6.2, 0), wp(-3.2, 0), wp(0, 0), wp(2.4, 0),
        wp(-4.0, 5.4), wp(-4.0, -5.4),
      ];
    } else if (type === "foyer") {
      chunk.waypoints = [
        wp(-6.2, 0), wp(-3.2, 0), wp(0, 0), wp(3.2, 0), wp(6.2, 0),
        wp(0, -5.4), wp(0, -6.5),
        wp(5.4, 5.4), wp(-5.4, 5.4),
      ];
    } else if (type === "art_room") {
      chunk.waypoints = [
        wp(0, -6.2), wp(0, -3.2), wp(0, 0),
        wp(-4.6, 3.2), wp(4.6, 3.2),
      ];
    } else if (type === "studio") {
      chunk.waypoints = [
        wp(0, 0), wp(0, 6.2), wp(-6.2, 0),
        wp(4.2, 3.2), wp(-4.6, 3.2),
      ];
    } else if (type === "broadcast") {
      chunk.waypoints = [
        wp(0, 0), wp(0, -6.2), wp(6.2, 0),
        wp(-4.2, 4.2), wp(4.2, 4.2),
      ];
    } else if (type === "darkroom") {
      chunk.waypoints = [
        wp(-6.2, 0), wp(0, 0), wp(6.2, 0),
        wp(-4.2, 3.4), wp(3.6, 3.2),
      ];
    } else if (type === "greenroom") {
      chunk.waypoints = [
        wp(0, 0), wp(0, -6.2), wp(-6.2, 0), wp(6.2, 0),
        wp(4.2, 3.2),
      ];
    } else if (type === "home_ec") {
      chunk.waypoints = [
        wp(0, 0), wp(0, -6.2), wp(6.2, 0),
        wp(4.6, 4.2), wp(4.6, -4.2),
      ];
    } else if (type === "club_room") {
      chunk.waypoints = [
        wp(0, 0), wp(0, 6.2), wp(-6.2, 0),
        wp(4.2, 4.2), wp(-4.2, 3.2),
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
