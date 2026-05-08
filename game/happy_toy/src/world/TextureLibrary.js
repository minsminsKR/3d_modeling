// 환경 텍스처를 한 곳에서 로드하고, 맵 메시가 같은 재질 규칙을 쓰도록 돕습니다.

import * as THREE from "three";

const TEXTURE_URLS = {
  wall: "./assets/textures/walls/wall.png",
  floor: "./assets/textures/floors/floor.png",
  ceiling: "./assets/textures/ceilings/ceiling.png",
  stair: "./assets/textures/stairs/stair.png",
  door: "./assets/textures/doors/doors.png",
  cabinet: "./assets/textures/cabinets/cabinet.png",
};

export class TextureLibrary {
  constructor() {
    this.loader = new THREE.TextureLoader();
    this.cache = new Map();
  }

  createWallMaterial() {
    return new THREE.MeshStandardMaterial({
      map: this.load("wall"),
      color: 0x8a8679,
      roughness: 0.94,
    });
  }

  createStairMaterial() {
    return new THREE.MeshStandardMaterial({
      map: this.load("stair"),
      color: 0xb0aa9b,
      roughness: 0.96,
    });
  }

  createDoorMaterial() {
    return new THREE.MeshStandardMaterial({
      map: this.load("door"),
      color: 0xffffff,
      roughness: 0.82,
      metalness: 0.04,
    });
  }

  createFloorMaterial(width = 4, depth = 4) {
    const repeat = [
      Math.max(1, width / 4),
      Math.max(1, depth / 4),
    ];
    return new THREE.MeshStandardMaterial({
      map: this.createTexture("floor", { repeat, wrapping: THREE.RepeatWrapping }),
      color: 0x9a9a90,
      roughness: 0.96,
    });
  }

  createCeilingMaterial(width = 4, depth = 4) {
    const repeat = [
      Math.max(1, width / 4),
      Math.max(1, depth / 4),
    ];
    return new THREE.MeshStandardMaterial({
      map: this.createTexture("ceiling", { repeat, wrapping: THREE.RepeatWrapping }),
      color: 0x7b7f72,
      roughness: 0.98,
    });
  }

  createCabinetMaterial() {
    return new THREE.MeshStandardMaterial({
      map: this.load("cabinet"),
      color: 0xffffff,
      roughness: 0.84,
      metalness: 0.04,
    });
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
        loadedTexture.needsUpdate = true;
      },
      undefined,
      (error) => console.warn(`[TextureLibrary] Failed to load ${url}`, error),
    );
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = options.wrapping ?? THREE.ClampToEdgeWrapping;
    texture.wrapT = options.wrapping ?? THREE.ClampToEdgeWrapping;
    if (options.repeat) {
      texture.repeat.set(options.repeat[0], options.repeat[1]);
    }
    texture.userData.textureKey = key;
    return texture;
  }
}
