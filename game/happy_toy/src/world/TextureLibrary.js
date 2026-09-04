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
};

export class TextureLibrary {
  constructor() {
    this.loader = new THREE.TextureLoader();
    this.cache = new Map();
    this.materialsCache = new Map();
  }

  createWallMaterial() {
    if (this.materialsCache.has("wall")) {
      return this.materialsCache.get("wall");
    }
    const mat = new THREE.MeshStandardMaterial({
      map: this.load("wall"),
      color: 0xba9872,
      roughness: 0.65,
      metalness: 0.02,
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
      color: 0x8c6d48,
      roughness: 0.75,
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
      color: 0xffffff,
      roughness: 0.72,
      metalness: 0.04,
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
    const mat = new THREE.MeshStandardMaterial({
      map: this.createTexture("floor", { repeat, wrapping: THREE.RepeatWrapping }),
      color: 0xa88258,
      roughness: 0.52,
      metalness: 0.06,
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
      color: 0x54402e,
      roughness: 0.85,
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
      color: 0xffffff,
      roughness: 0.84,
      metalness: 0.04,
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
}
