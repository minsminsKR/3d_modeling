// FBX 캐릭터와 원본 텍스처를 읽어 Three.js 장면에 올릴 수 있게 정리하는 모듈입니다.
// Mixamo에서 받은 Walking/Run FBX를 캐릭터 애니메이션으로 쓰고, 실패하면 임시 형상으로 대체합니다.

import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";

export class CharacterLoader {
  constructor() {
    this.fbxLoader = new FBXLoader();
    this.textureLoader = new THREE.TextureLoader();
  }

  async load(config) {
    try {
      const [object, texture] = await Promise.all([
        this.loadFbx(config.modelUrl),
        this.loadTexture(config.textureUrl),
      ]);
      this.applyTexture(object, texture);
      this.prepareObject(object, config.height);
      object.animations = this.prepareLoopingAnimations(object.animations || []);
      const actions = await this.loadActionClips(config.animationUrls || {}, object.animations, config);
      return {
        root: object,
        animations: object.animations || [],
        actions,
        fallback: false,
      };
    } catch (error) {
      console.warn(`Failed to load ${config.label}:`, error);
      return this.createFallback(config);
    }
  }


  loadFbx(url) {
    return new Promise((resolve, reject) => {
      this.fbxLoader.load(url, resolve, undefined, reject);
    });
  }

  loadTexture(url) {
    return new Promise((resolve) => {
      this.textureLoader.load(
        url,
        (texture) => {
          texture.colorSpace = THREE.SRGBColorSpace;
          texture.flipY = true;
          texture.generateMipmaps = true;
          texture.minFilter = THREE.LinearMipmapLinearFilter;
          texture.magFilter = THREE.LinearFilter;
          texture.anisotropy = 16;
          texture.needsUpdate = true;
          resolve(texture);
        },
        undefined,
        () => resolve(null),
      );
    });
  }

  async loadActionClips(animationUrls, baseAnimations, config) {
    const actions = {
      patrol: baseAnimations[0] || null,
      chase: baseAnimations[0] || null,
    };

    await Promise.all(
      Object.entries(animationUrls).map(async ([name, url]) => {
        const clip = await this.loadFirstClip(url);
        if (clip) {
          actions[name] = this.prepareLoopingAnimations([clip], {
            lockRootVerticalMotion: config.lockRootVerticalActions?.includes(name),
          })[0];
        }
      }),
    );

    return actions;
  }

  async loadFirstClip(url) {
    try {
      const object = await this.loadFbx(url);
      return object.animations?.[0] || null;
    } catch (error) {
      console.warn(`Failed to load animation clip: ${url}`, error);
      return null;
    }
  }

  applyTexture(object, texture) {
    object.traverse((child) => {
      if (!child.isMesh && !child.isSkinnedMesh) {
        return;
      }
      this.prepareCharacterGeometry(child);
      child.castShadow = true;
      child.receiveShadow = true;
      disposeMaterial(child.material);
      child.material = this.createLitCharacterMaterial(child, texture);
    });
  }

  prepareCharacterGeometry(child) {
    const geometry = child.geometry;
    if (!geometry?.attributes?.position) {
      return;
    }

    // Mixamo FBX meshes arrive non-indexed without normals. Built-in computeVertexNormals()
    // on non-indexed geometry creates disjoint face normals for each triangle, causing
    // harsh flat shading where all 10,000 polygon facets are visible like cracked stone.
    // We compute continuous area-weighted smooth vertex normals across shared vertex positions.
    this.computeSmoothVertexNormals(geometry);
  }

  computeSmoothVertexNormals(geometry) {
    const position = geometry.attributes.position;
    if (!position) return;

    const vertexCount = position.count;
    const pA = new THREE.Vector3();
    const pB = new THREE.Vector3();
    const pC = new THREE.Vector3();
    const cb = new THREE.Vector3();
    const ab = new THREE.Vector3();

    // Map spatial positions to accumulated area-weighted normal vectors
    const normalMap = new Map();
    const precision = 10000; // 0.1mm tolerance
    const getKey = (x, y, z) => `${Math.round(x * precision)},${Math.round(y * precision)},${Math.round(z * precision)}`;

    for (let i = 0; i < vertexCount; i += 3) {
      pA.fromBufferAttribute(position, i);
      pB.fromBufferAttribute(position, i + 1);
      pC.fromBufferAttribute(position, i + 2);

      cb.subVectors(pC, pB);
      ab.subVectors(pA, pB);
      cb.cross(ab); // area-weighted normal

      for (let j = 0; j < 3; j++) {
        const idx = i + j;
        const key = getKey(position.getX(idx), position.getY(idx), position.getZ(idx));
        let acc = normalMap.get(key);
        if (!acc) {
          acc = new THREE.Vector3();
          normalMap.set(key, acc);
        }
        acc.add(cb);
      }
    }

    // Assign normalized smooth normal to every vertex sharing that position
    const normals = new Float32Array(vertexCount * 3);
    const tempNormal = new THREE.Vector3();

    for (let i = 0; i < vertexCount; i++) {
      const key = getKey(position.getX(i), position.getY(i), position.getZ(i));
      const acc = normalMap.get(key);
      if (acc && acc.lengthSq() > 1e-10) {
        tempNormal.copy(acc).normalize();
      } else {
        tempNormal.set(0, 1, 0);
      }
      normals[i * 3] = tempNormal.x;
      normals[i * 3 + 1] = tempNormal.y;
      normals[i * 3 + 2] = tempNormal.z;
    }

    geometry.setAttribute("normal", new THREE.BufferAttribute(normals, 3));
    geometry.attributes.normal.needsUpdate = true;
  }

  createLitCharacterMaterial(child, texture) {
    const canUseTexture = texture && child.geometry?.attributes?.uv;
    return new THREE.MeshStandardMaterial({
      map: canUseTexture ? texture : null,
      color: canUseTexture ? new THREE.Color(0xffffff) : new THREE.Color(0x8d6a46),
      roughness: 0.52,
      metalness: 0.02,
      side: THREE.DoubleSide,
      flatShading: false,
    });
  }

  prepareObject(object, targetHeight) {
    object.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(object);
    const size = box.getSize(new THREE.Vector3());
    const scale = targetHeight / Math.max(size.y, 0.001);
    object.scale.multiplyScalar(scale);

    object.updateMatrixWorld(true);
    const scaledBox = new THREE.Box3().setFromObject(object);
    const center = scaledBox.getCenter(new THREE.Vector3());
    object.position.x -= center.x;
    object.position.y -= scaledBox.min.y;
    object.position.z -= center.z;
  }

  prepareLoopingAnimations(animations, options = {}) {
    return animations.map((clip) => {
      const clonedClip = clip.clone();
      clonedClip.tracks = clonedClip.tracks.map((track) => {
        if (!isRootPositionTrack(track)) {
          return track;
        }

        let nextTrack = track;
        if (options.lockRootVerticalMotion) {
          nextTrack = lockRootVerticalMotion(nextTrack);
        }

        return shouldRemoveRootPositionDrift(nextTrack)
          ? removeRootPositionDrift(nextTrack)
          : nextTrack;
      });
      clonedClip.optimize();
      return clonedClip;
    });
  }

  createFallback(config) {
    const group = new THREE.Group();
    const body = new THREE.Mesh(
      new THREE.CapsuleGeometry(0.32, config.height * 0.52, 8, 16),
      new THREE.MeshStandardMaterial({
        color: config.id === "uncat" ? 0x93423b : 0x6f7f57,
        roughness: 0.88,
        metalness: 0.0,
      }),
    );
    body.position.y = config.height * 0.5;
    body.castShadow = true;
    body.receiveShadow = true;
    group.add(body);

    const eye = new THREE.Mesh(
      new THREE.SphereGeometry(0.09, 12, 12),
      new THREE.MeshStandardMaterial({ color: 0xffd98a, roughness: 0.7, metalness: 0.0 }),
    );
    eye.position.set(0, config.height * 0.74, -0.3);
    eye.castShadow = true;
    eye.receiveShadow = true;
    group.add(eye);

    return {
      root: group,
      animations: [],
      actions: {
        patrol: null,
        chase: null,
      },
      fallback: true,
    };
  }
}

function disposeMaterial(material) {
  if (Array.isArray(material)) {
    material.forEach((entry) => entry?.dispose?.());
    return;
  }
  material?.dispose?.();
}

function isRootPositionTrack(track) {
  if (!track.name.endsWith(".position") || track.getValueSize() !== 3) {
    return false;
  }

  const targetName = track.name.split(".")[0].toLowerCase();
  return /(hips|pelvis|root|armature)/.test(targetName);
}

function shouldRemoveRootPositionDrift(track) {
  if (!isRootPositionTrack(track)) {
    return false;
  }

  const values = track.values;
  const firstX = values[0];
  const firstY = values[1];
  const firstZ = values[2];
  const lastX = values[values.length - 3];
  const lastY = values[values.length - 2];
  const lastZ = values[values.length - 1];
  return Math.hypot(lastX - firstX, lastY - firstY, lastZ - firstZ) > 0.01;
}

function lockRootVerticalMotion(track) {
  const clonedTrack = track.clone();
  const values = clonedTrack.values;
  const lockedY = values[1];

  for (let valueIndex = 1; valueIndex < values.length; valueIndex += 3) {
    values[valueIndex] = lockedY;
  }

  return clonedTrack;
}

function removeRootPositionDrift(track) {
  const clonedTrack = track.clone();
  const values = clonedTrack.values;
  const times = clonedTrack.times;
  const duration = times[times.length - 1] || 1;
  const firstX = values[0];
  const firstY = values[1];
  const firstZ = values[2];
  const driftX = values[values.length - 3] - firstX;
  const driftY = values[values.length - 2] - firstY;
  const driftZ = values[values.length - 1] - firstZ;

  for (let valueIndex = 0, timeIndex = 0; valueIndex < values.length; valueIndex += 3, timeIndex += 1) {
    const progress = duration > 0 ? times[timeIndex] / duration : 0;
    values[valueIndex] -= driftX * progress;
    values[valueIndex + 1] -= driftY * progress;
    values[valueIndex + 2] -= driftZ * progress;
  }

  return clonedTrack;
}
