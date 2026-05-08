// FBX 캐릭터와 원본 텍스처를 읽어 Three.js 장면에 올릴 수 있게 정리하는 모듈입니다.
// Mixamo에서 받은 Walking.fbx를 기본 애니메이션으로 쓰고, 실패하면 임시 형상으로 대체합니다.

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
      child.castShadow = true;
      child.receiveShadow = true;
      const material = texture && child.geometry?.attributes?.uv
        ? new THREE.MeshBasicMaterial({
            map: texture,
            color: 0xffffff,
            side: THREE.DoubleSide,
          })
        : new THREE.MeshBasicMaterial({
            color: 0x8d6a46,
            side: THREE.DoubleSide,
          });
      child.material = material;
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
      new THREE.MeshStandardMaterial({ color: config.id === "uncat" ? 0x93423b : 0x6f7f57, roughness: 0.82 }),
    );
    body.position.y = config.height * 0.5;
    body.castShadow = true;
    group.add(body);

    const eye = new THREE.Mesh(
      new THREE.SphereGeometry(0.09, 12, 12),
      new THREE.MeshBasicMaterial({ color: 0xffe7a4 }),
    );
    eye.position.set(0, config.height * 0.74, -0.3);
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
