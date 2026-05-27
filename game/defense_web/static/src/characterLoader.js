import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";

const DEBUG_MODEL_LOADING = true;

function stripRootMotion(clip) {
  if (!clip) return;
  for (const track of clip.tracks) {
    if (track.name.endsWith(".position") && (track.name.includes("Hips") || track.name.includes("Root"))) {
      const values = track.values;
      for (let i = 0; i < values.length; i += 3) {
        values[i] = 0;     // Force X to 0
        values[i + 2] = 0; // Force Z to 0
      }
    }
  }
}

export class CharacterLoader {
  constructor() {
    THREE.Cache.enabled = true;
    this.fbxLoader = new FBXLoader();
    this.textureLoader = new THREE.TextureLoader();
    this.cache = new Map();
    this.status = new Map();
  }

  async load(config) {
    if (this.cache.has(config.label)) {
      return this.cache.get(config.label);
    }

    this.setStatus(config, "loading", { modelUrl: config.modelUrl, textureUrl: config.textureUrl });

    const promise = Promise.all([
      this.loadFbx(config.modelUrl, config),
      this.loadTexture(config.textureUrl, config),
    ]).then(([rawRoot, texture]) => {
      let root = this.prepareObject(rawRoot, config.height);
      root.animations = rawRoot.animations || [];
      for (const clip of root.animations) {
        stripRootMotion(clip);
      }
      root.userData.assetLabel = config.label;
      root.userData.isFallback = false;
      this.applyTexture(root, texture);
      root.traverse((child) => {
        child.frustumCulled = false;
        if (child.isMesh || child.isSkinnedMesh) {
          child.castShadow = true;
          child.receiveShadow = true;
        }
      });
      root.visible = false;
      this.setStatus(config, "ready", {
        meshes: root.userData.meshCount,
        texturedMeshes: root.userData.texturedMeshCount,
        uvMissingMeshes: root.userData.uvMissingMeshCount,
        height: root.userData.normalizedHeight,
      });
      return root;
    }).catch((error) => {
      console.error(`[CharacterLoader] ${config.label} FBX load failed. Using visible capsule fallback.`, {
        modelUrl: config.modelUrl,
        textureUrl: config.textureUrl,
        error,
      });
      this.setStatus(config, "fallback", { error: error?.message || String(error) });
      const fallback = this.createFallback(config);
      fallback.userData.assetLabel = config.label;
      fallback.userData.isFallback = true;
      return fallback;
    });

    this.cache.set(config.label, promise);
    return promise;
  }

  loadFbx(url, config) {
    return new Promise((resolve, reject) => {
      const requestUrl = encodeURI(url);
      this.fbxLoader.load(
        requestUrl,
        (root) => {
          this.log(config, "FBX loaded", { url: requestUrl });
          resolve(root);
        },
        undefined,
        (error) => {
          reject(new Error(`FBX request failed for ${requestUrl}: ${error?.message || error}`));
        },
      );
    });
  }

  loadTexture(url, config) {
    return new Promise((resolve) => {
      const requestUrl = encodeURI(url);
      this.textureLoader.load(
        requestUrl,
        (texture) => {
          texture.colorSpace = THREE.SRGBColorSpace;
          texture.flipY = true;
          texture.needsUpdate = true;
          this.log(config, "Texture loaded", { url: requestUrl, flipY: texture.flipY });
          resolve(texture);
        },
        undefined,
        (error) => {
          console.warn(`[CharacterLoader] ${config.label} texture failed. Model will use fallback material.`, {
            url: requestUrl,
            error,
          });
          resolve(null);
        },
      );
    });
  }

  applyTexture(root, texture) {
    let meshCount = 0;
    let texturedMeshCount = 0;
    let uvMissingMeshCount = 0;
    root.traverse((child) => {
      if (!child.isMesh && !child.isSkinnedMesh) {
        return;
      }
      meshCount += 1;
      const hasUv = Boolean(child.geometry?.attributes?.uv);
      if (!hasUv) uvMissingMeshCount += 1;
      if (texture && hasUv) texturedMeshCount += 1;
      child.material = texture && hasUv
        ? new THREE.MeshBasicMaterial({
          map: texture,
          color: 0xffffff,
          side: THREE.DoubleSide,
        })
        : new THREE.MeshStandardMaterial({
          color: child.isSkinnedMesh ? 0x9d7750 : 0x8c7159,
          roughness: 0.82,
          metalness: 0.02,
        });
    });
    root.userData.meshCount = meshCount;
    root.userData.texturedMeshCount = texturedMeshCount;
    root.userData.uvMissingMeshCount = uvMissingMeshCount;
    if (meshCount > 0 && texture && texturedMeshCount === 0) {
      console.warn(`[CharacterLoader] ${root.userData.assetLabel || "character"} has a texture but no UV-enabled meshes.`);
    }
  }

  prepareObject(root, targetHeight) {
    const normalized = new THREE.Group();
    normalized.name = `${root.name || "character"}_normalized`;
    normalized.add(root);

    root.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(root);
    const size = box.getSize(new THREE.Vector3());
    if (!Number.isFinite(size.y) || size.y <= 0.001) {
      console.warn("[CharacterLoader] Invalid FBX bounds; keeping source scale.", { targetHeight, size });
      normalized.userData.baseScale = normalized.scale.clone();
      normalized.userData.normalizedHeight = targetHeight;
      return normalized;
    }

    const scale = targetHeight / size.y;
    root.scale.multiplyScalar(scale);
    normalized.updateMatrixWorld(true);

    const scaledBox = new THREE.Box3().setFromObject(normalized);
    const center = scaledBox.getCenter(new THREE.Vector3());
    root.position.x -= center.x;
    root.position.y -= scaledBox.min.y;
    root.position.z -= center.z;

    normalized.updateMatrixWorld(true);
    const finalBox = new THREE.Box3().setFromObject(normalized);
    normalized.userData.baseScale = normalized.scale.clone();
    normalized.userData.normalizedHeight = finalBox.max.y - finalBox.min.y;
    normalized.userData.bounds = {
      minY: finalBox.min.y,
      maxY: finalBox.max.y,
      width: finalBox.max.x - finalBox.min.x,
      depth: finalBox.max.z - finalBox.min.z,
    };
    return normalized;
  }

  createFallback(config) {
    const group = new THREE.Group();
    const body = new THREE.Mesh(
      new THREE.CapsuleGeometry(0.35, config.height * 0.45, 8, 12),
      new THREE.MeshStandardMaterial({ color: 0x8d6a46, roughness: 0.8 }),
    );
    body.position.y = config.height * 0.52;
    group.add(body);
    group.visible = false;
    group.userData.baseScale = group.scale.clone();
    group.userData.normalizedHeight = config.height;
    return group;
  }

  setStatus(config, state, details = {}) {
    this.status.set(config.label, { state, at: performance.now(), ...details });
    this.log(config, `status: ${state}`, details);
  }

  log(config, message, details = {}) {
    if (!DEBUG_MODEL_LOADING) return;
    console.info(`[CharacterLoader] ${config.label} ${message}`, details);
  }
}

export function cloneCharacter(root) {
  const clone = cloneSkinned(root);
  clone.animations = root.animations;
  clone.traverse((child) => {
    if (child.material) {
      child.material = child.material.clone();
    }
    child.visible = true;
    child.frustumCulled = false;
  });
  clone.visible = true;
  return clone;
}

export function activateCharacter(root) {
  root.visible = true;
  root.traverse((child) => {
    child.visible = true;
    child.frustumCulled = false;
  });
  return root;
}

function cloneSkinned(source) {
  const clone = source.clone(true);
  const sourceLookup = new Map();
  const cloneLookup = new Map();

  parallelTraverse(source, clone, (sourceNode, clonedNode) => {
    sourceLookup.set(clonedNode, sourceNode);
    cloneLookup.set(sourceNode, clonedNode);
  });

  clone.traverse((node) => {
    if (!node.isSkinnedMesh) return;
    const sourceMesh = sourceLookup.get(node);
    const sourceBones = sourceMesh?.skeleton?.bones || [];
    node.skeleton = sourceMesh.skeleton.clone();
    node.skeleton.bones = sourceBones.map((bone) => cloneLookup.get(bone)).filter(Boolean);
    node.bind(node.skeleton, sourceMesh.bindMatrix);
  });

  return clone;
}

function parallelTraverse(sourceNode, clonedNode, callback) {
  callback(sourceNode, clonedNode);
  for (let i = 0; i < sourceNode.children.length; i += 1) {
    parallelTraverse(sourceNode.children[i], clonedNode.children[i], callback);
  }
}
