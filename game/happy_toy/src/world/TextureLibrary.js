// 환경 텍스처를 한 곳에서 로드하고, 맵 메시가 같은 재질 규칙을 쓰도록 돕습니다.

import * as THREE from "three";

const TEXTURE_URLS = {
  wall: "/assets/textures/walls/wall.png",
  floor: "/assets/textures/floors/floor.png",
  ceiling: "/assets/textures/ceilings/ceiling.png",
  stair: "/assets/textures/stairs/stair.png",
  door: "/assets/textures/doors/doors.png",
  cabinet: "/assets/textures/cabinets/cabinet.png",
  hwaPaint: "/assets/textures/hwa_paint/hwa_paint.png",
  wallDecay: "/assets/textures/props/wall_decay_decal/wall-decay-decal.png",
};

export class TextureLibrary {
  constructor() {
    this.loader = new THREE.TextureLoader();
    this.cache = new Map();
    this.materialsCache = new Map();
    this.detailTextureCache = new Map();
  }

  createWallMaterial() {
    if (this.materialsCache.has("wall")) {
      return this.materialsCache.get("wall");
    }
    const wallTexture = this.createTexture("wall", { repeat: [3, 1], wrapping: THREE.RepeatWrapping });
    const mat = new THREE.MeshStandardMaterial({
      map: wallTexture,
      emissive: 0x160c07,
      emissiveIntensity: 1.65,
      bumpMap: this.createDetailTexture("wall-plaster", { repeat: [3, 1] }),
      bumpScale: 0.018,
      color: 0xd8c6aa,
      roughness: 0.82,
      metalness: 0.0,
    });
    this.materialsCache.set("wall", mat);
    return mat;
  }

  createStairMaterial() {
    if (this.materialsCache.has("stair")) {
      return this.materialsCache.get("stair");
    }
    const mat = new THREE.MeshStandardMaterial({
      map: this.load("stair"),
      bumpMap: this.createDetailTexture("worn-wood", { repeat: [2, 2] }),
      bumpScale: 0.045,
      color: 0x8c6d48,
      roughness: 0.82,
      metalness: 0.0,
    });
    this.materialsCache.set("stair", mat);
    return mat;
  }

  createDoorMaterial() {
    if (this.materialsCache.has("door")) {
      return this.materialsCache.get("door");
    }
    const mat = new THREE.MeshStandardMaterial({
      map: this.load("door"),
      bumpMap: this.createDetailTexture("door-grain", { repeat: [1, 2] }),
      bumpScale: 0.04,
      color: 0xe7d3b8,
      roughness: 0.76,
      metalness: 0.02,
    });
    this.materialsCache.set("door", mat);
    return mat;
  }

  createFloorMaterial(width = 4, depth = 4) {
    const key = `floor_${width}_${depth}`;
    if (this.materialsCache.has(key)) {
      return this.materialsCache.get(key);
    }
    const repeat = [
      Math.max(1, width / 4),
      Math.max(1, depth / 4),
    ];
    // Shadow Corridor polished Japanese cedar floorboards with warm amber sheen
    const floorTexture = this.createTexture("floor", { repeat, wrapping: THREE.RepeatWrapping });
    const mat = new THREE.MeshStandardMaterial({
      map: floorTexture,
      emissive: 0x120a06,
      emissiveIntensity: 2.0,
      bumpMap: this.createDetailTexture("floor-grain", { repeat }),
      bumpScale: 0.025,
      color: 0xd6c4aa,
      roughness: 0.68,
      metalness: 0.025,
    });
    this.materialsCache.set(key, mat);
    return mat;
  }

  createCeilingMaterial(width = 4, depth = 4) {
    const key = `ceiling_${width}_${depth}`;
    if (this.materialsCache.has(key)) {
      return this.materialsCache.get(key);
    }
    const repeat = [
      Math.max(1, width / 4),
      Math.max(1, depth / 4),
    ];
    const mat = new THREE.MeshStandardMaterial({
      map: this.createTexture("ceiling", { repeat, wrapping: THREE.RepeatWrapping }),
      bumpMap: this.createDetailTexture("ceiling-stain", { repeat }),
      bumpScale: 0.025,
      color: 0x66513b,
      roughness: 0.93,
      metalness: 0.0,
    });
    this.materialsCache.set(key, mat);
    return mat;
  }

  createCabinetMaterial() {
    if (this.materialsCache.has("cabinet")) {
      return this.materialsCache.get("cabinet");
    }
    const mat = new THREE.MeshStandardMaterial({
      map: this.load("cabinet"),
      bumpMap: this.createDetailTexture("cabinet-wear", { repeat: [1, 2] }),
      bumpScale: 0.045,
      color: 0xd8c2a5,
      roughness: 0.86,
      metalness: 0.025,
    });
    this.materialsCache.set("cabinet", mat);
    return mat;
  }

  createHwaPaintMaterial() {
    if (this.materialsCache.has("hwaPaint")) {
      return this.materialsCache.get("hwaPaint");
    }
    const mat = new THREE.MeshStandardMaterial({
      map: this.load("hwaPaint"),
      color: 0xffffff,
      roughness: 0.78,
      metalness: 0.02,
    });
    this.materialsCache.set("hwaPaint", mat);
    return mat;
  }

  createWallDecayDecalMaterial() {
    if (this.materialsCache.has("wallDecayDecal")) {
      return this.materialsCache.get("wallDecayDecal");
    }
    const mat = new THREE.MeshStandardMaterial({
      map: this.load("wallDecay"),
      color: 0xb49b7b,
      transparent: true,
      opacity: 0.78,
      alphaTest: 0.018,
      depthWrite: false,
      roughness: 0.96,
      metalness: 0.0,
      polygonOffset: true,
      polygonOffsetFactor: -1,
      polygonOffsetUnits: -2,
    });
    this.materialsCache.set("wallDecayDecal", mat);
    return mat;
  }

  load(key) {
    if (this.cache.has(key)) {
      return this.cache.get(key);
    }

    const texture = this.createTexture(key);
    this.cache.set(key, texture);
    return texture;
  }

  createTexture(key, options = {}) {
    const url = TEXTURE_URLS[key];
    const texture = this.loader.load(
      url,
      (loadedTexture) => {
        loadedTexture.colorSpace = THREE.SRGBColorSpace;
        loadedTexture.anisotropy = 16;
        loadedTexture.generateMipmaps = true;
        loadedTexture.minFilter = THREE.LinearMipmapLinearFilter;
        loadedTexture.magFilter = THREE.LinearFilter;
        loadedTexture.needsUpdate = true;
      },
      undefined,
      (error) => console.warn(`[TextureLibrary] Failed to load ${url}`, error),
    );
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.anisotropy = 16;
    texture.generateMipmaps = true;
    texture.minFilter = THREE.LinearMipmapLinearFilter;
    texture.magFilter = THREE.LinearFilter;
    texture.wrapS = options.wrapping ?? THREE.ClampToEdgeWrapping;
    texture.wrapT = options.wrapping ?? THREE.ClampToEdgeWrapping;
    if (options.repeat) {
      texture.repeat.set(options.repeat[0], options.repeat[1]);
    }
    texture.userData.textureKey = key;
    return texture;
  }

  createDetailTexture(key, options = {}) {
    const repeat = options.repeat || [1, 1];
    const cacheKey = `${key}_${repeat[0]}_${repeat[1]}`;
    if (this.detailTextureCache.has(cacheKey)) {
      return this.detailTextureCache.get(cacheKey);
    }

    const canvas = document.createElement("canvas");
    canvas.width = 128;
    canvas.height = 128;
    const ctx = canvas.getContext("2d");
    const image = ctx.createImageData(canvas.width, canvas.height);
    let seed = Array.from(key).reduce((value, character) => ((value * 31) + character.charCodeAt(0)) >>> 0, 2166136261);
    const random = () => {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      return seed / 4294967296;
    };

    for (let i = 0; i < image.data.length; i += 4) {
      const coarse = random() * 22;
      const pinhole = random() > 0.985 ? random() * 52 : 0;
      const value = Math.max(52, Math.min(205, 118 + coarse - pinhole));
      image.data[i] = value;
      image.data[i + 1] = value;
      image.data[i + 2] = value;
      image.data[i + 3] = 255;
    }
    ctx.putImageData(image, 0, 0);

    // Long, very faint fibres stop large flat surfaces from reading as plastic.
    ctx.globalAlpha = 0.12;
    ctx.strokeStyle = "#e2e2e2";
    ctx.lineWidth = 1;
    for (let i = 0; i < 18; i++) {
      const y = random() * canvas.height;
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.bezierCurveTo(34, y + random() * 5, 88, y - random() * 5, 128, y + random() * 3);
      ctx.stroke();
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.NoColorSpace;
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(repeat[0], repeat[1]);
    texture.anisotropy = 4;
    texture.generateMipmaps = true;
    texture.minFilter = THREE.LinearMipmapLinearFilter;
    texture.magFilter = THREE.LinearFilter;
    texture.userData.textureKey = `detail:${key}`;
    this.detailTextureCache.set(cacheKey, texture);
    return texture;
  }
}
