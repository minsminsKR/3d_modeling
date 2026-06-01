import * as THREE from "three";
import { LOVELY_DOLL_CONFIG } from "../config/gameConfig.js";

export class LovelyDoll {
  constructor(id, loadedAsset, collisionWorld, game) {
    this.id = id;
    this.collisionWorld = collisionWorld;
    this.game = game;
    this.hud = game.hud;
    
    this.group = new THREE.Group();
    this.group.name = id;
    this.shadowMesh = addShadowBlob(this.group, 0.35);
    
    // Check if asset loaded correctly
    if (loadedAsset && loadedAsset.root) {
      this.modelRoot = cloneSkinnedMeshModel(loadedAsset.root);
      this.group.add(this.modelRoot);
      
      // Set up materials for fading (making sure we can change opacity)
      this.modelRoot.traverse((child) => {
        if (child.isMesh || child.isSkinnedMesh) {
          child.castShadow = true;
          child.receiveShadow = true;
          if (child.material) {
            if (Array.isArray(child.material)) {
              child.material = child.material.map(m => m.clone());
            } else {
              child.material = child.material.clone();
            }
          }
        }
      });

      this.mixer = loadedAsset.animations && loadedAsset.animations.length
        ? new THREE.AnimationMixer(this.modelRoot)
        : null;
        
      // Set up actions
      this.actions = {};
      if (loadedAsset.actions) {
        for (const [name, clip] of Object.entries(loadedAsset.actions)) {
          if (!clip) continue;
          const action = this.mixer.clipAction(clip);
          action.enabled = true;
          this.actions[name] = action;
        }
      }
    } else {
      // Fallback capsule if asset loading failed
      this.modelRoot = new THREE.Group();
      const body = new THREE.Mesh(
        new THREE.CapsuleGeometry(0.2, 0.6, 8, 16),
        new THREE.MeshStandardMaterial({ color: 0xffaacc, roughness: 0.6 })
      );
      body.position.y = 0.4;
      this.modelRoot.add(body);
      this.group.add(this.modelRoot);
      this.mixer = null;
      this.actions = {};
    }
    
    this.currentActionName = null;
    this.state = "dance"; // "dance", "walking", "run", "fade"
    this.isActivated = false;
    this.lookTimer = 0; // Cumulative time player stared at it
    this.fadeTimer = 0;
    this.dollIndex = 0; // Order index when activated (1-5)
    
    this.path = null;
    this.pathTimer = 0;
    this.targetPosition = null;
    
    this.playAction("dance", 0);
  }

  playAction(name, fadeSeconds = 0.2) {
    const nextAction = this.actions[name];
    if (!nextAction || this.currentActionName === name) {
      return;
    }
    const previousAction = this.currentActionName ? this.actions[this.currentActionName] : null;
    nextAction.reset();
    nextAction.play();
    nextAction.fadeIn(fadeSeconds);
    if (previousAction && previousAction !== nextAction) {
      previousAction.fadeOut(fadeSeconds);
    }
    this.currentActionName = name;
  }

  getLowestGroundPoint() {
    let currentMinY = null;
    let hasBones = false;
    this.modelRoot.traverse((child) => {
      if (child.isBone) hasBones = true;
    });

    if (hasBones) {
      let minY = Infinity;
      this.modelRoot.traverse((child) => {
        if (child.isBone) {
          const name = child.name.toLowerCase();
          if (name.includes("root") || name.includes("hips") || name.includes("pelvis") || 
              name.includes("spine") || name.includes("chest") || name.includes("neck") || 
              name.includes("head") || name.includes("clavicle") || name.includes("shoulder")) {
            return;
          }
          child.updateMatrixWorld(true);
          const worldPos = new THREE.Vector3();
          child.getWorldPosition(worldPos);
          if (worldPos.y < minY) {
            minY = worldPos.y;
          }
        }
      });
      if (Number.isFinite(minY)) {
        currentMinY = minY;
      }
    }

    if (currentMinY === null) {
      this.modelRoot.updateMatrixWorld(true);
      const bounds = new THREE.Box3().setFromObject(this.modelRoot);
      if (Number.isFinite(bounds.min.y)) {
        currentMinY = bounds.min.y;
      }
    }
    return currentMinY;
  }

  snapModelToGround() {
    if (!this.modelRoot) return;
    const currentMinY = this.getLowestGroundPoint();
    if (currentMinY === null) {
      return;
    }
    const groundY = this.group.position.y - (LOVELY_DOLL_CONFIG.visualGroundSink ?? 0.03);
    const offset = groundY - currentMinY;
    if (Math.abs(offset) > 0.001) {
      this.modelRoot.position.y += offset;
      this.modelRoot.updateMatrixWorld(true);
    }
  }

  update(deltaTime) {
    if (this.mixer) {
      this.mixer.update(deltaTime);
    }

    if (this.state === "dance" && !this.isActivated) {
      this.updateActivation(deltaTime);
    } else if (this.state === "walking" || this.state === "run") {
      this.updateMovement(deltaTime);
    } else if (this.state === "fade") {
      this.updateFade(deltaTime);
    }

    this.group.position.y = this.collisionWorld.getGroundY(this.group.position);
    this.snapModelToGround();
  }

  updateActivation(deltaTime) {
    if (!this.game.player) return;
    
    const playerPos = this.game.player.position;
    const dollPos = this.group.position;
    const distance = Math.hypot(dollPos.x - playerPos.x, dollPos.z - playerPos.z);
    
    if (distance > 5.5) {
      // Too far
      this.lookTimer = Math.max(0, this.lookTimer - deltaTime);
      return;
    }

    // Check player gaze
    const frustum = new THREE.Frustum();
    const cameraViewProjectionMatrix = new THREE.Matrix4();
    this.game.camera.updateMatrixWorld();
    this.game.camera.matrixWorldInverse.copy(this.game.camera.matrixWorld).invert();
    cameraViewProjectionMatrix.multiplyMatrices(this.game.camera.projectionMatrix, this.game.camera.matrixWorldInverse);
    frustum.setFromProjectionMatrix(cameraViewProjectionMatrix);

    const checkPoint = new THREE.Vector3(dollPos.x, dollPos.y + 0.8, dollPos.z);
    const inFrustum = frustum.containsPoint(checkPoint);
    const hasLos = inFrustum && this.collisionWorld.hasLineOfSight(this.game.camera.position, checkPoint);
    
    // Check if directly looking (yaw alignment)
    const cameraDir = new THREE.Vector3(0, 0, -1).applyQuaternion(this.game.camera.quaternion).normalize();
    const toDoll = checkPoint.clone().sub(this.game.camera.position).normalize();
    const gazeDot = cameraDir.dot(toDoll);
    
    const isStaring = inFrustum && hasLos && (gazeDot > 0.85);

    if (isStaring) {
      this.lookTimer += deltaTime;
      // Display progress status on the HUD
      const progress = Math.min(100, Math.floor((this.lookTimer / 5.0) * 100));
      this.hud.setStatus(`러블리 돌과 반응 중... (${progress}%)`, 200);
      
      if (this.lookTimer >= 5.0) {
        this.activate();
      }
    } else {
      this.lookTimer = Math.max(0, this.lookTimer - deltaTime);
    }
  }

  activate() {
    this.isActivated = true;
    this.game.dollCountFound += 1;
    this.dollIndex = this.game.dollCountFound;
    this.game.spawnedDollIds.add(this.id);

    // Determine target position based on index
    // Key 1: (32, 0, 32)
    // Key 2: (-32, 0, 32)
    // Key 3: (32, 0, -32)
    // Key 4: (-32, 0, -32)
    // Key 5: (0, 0, 0) (Exit)
    if (this.dollIndex === 1) {
      this.targetPosition = new THREE.Vector3(32, 0, 32);
      this.hud.setStatus(`러블리 돌이 깨어났습니다! 첫 번째 열쇠가 있는 곳으로 안내합니다.`, 3000);
    } else if (this.dollIndex === 2) {
      this.targetPosition = new THREE.Vector3(-32, 0, 32);
      this.hud.setStatus(`러블리 돌이 깨어났습니다! 두 번째 열쇠가 있는 곳으로 안내합니다.`, 3000);
    } else if (this.dollIndex === 3) {
      this.targetPosition = new THREE.Vector3(32, 0, -32);
      this.hud.setStatus(`러블리 돌이 깨어났습니다! 세 번째 열쇠가 있는 곳으로 안내합니다.`, 3000);
    } else if (this.dollIndex === 4) {
      this.targetPosition = new THREE.Vector3(-32, 0, -32);
      this.hud.setStatus(`러블리 돌이 깨어났습니다! 네 번째 열쇠가 있는 곳으로 안내합니다.`, 3000);
    } else {
      this.targetPosition = new THREE.Vector3(0, 0, 0);
      this.hud.setStatus(`러블리 돌이 깨어났습니다! 탈출을 위한 장난감 상자로 안내합니다.`, 3000);
    }

    this.state = "walking";
    this.playAction("walking", 0.3);
  }

  updateMovement(deltaTime) {
    if (!this.targetPosition) return;
    const goal = this.targetPosition;
    
    const distToGoal = Math.hypot(this.group.position.x - goal.x, this.group.position.z - goal.z);
    if (distToGoal < 1.8) {
      // Arrived!
      this.state = "fade";
      this.playAction("dance", 0.3);
      this.fadeTimer = 10.0;
      this.hud.setStatus("러블리 돌이 안내를 마치고 사라집니다.", 3000);
      return;
    }

    this.pathTimer -= deltaTime;
    const canMoveDirect = this.collisionWorld.hasLineOfSight(this.group.position, goal);
    let target = goal;

    if (canMoveDirect) {
      this.path = [];
      this.pathTimer = 0.5;
    } else {
      if (this.path === null || this.pathTimer <= 0) {
        this.path = this.collisionWorld.findPath(this.group.position, goal, 0.35, {
          cellSize: 0.85,
          allowInterFloor: false,
        });
        this.pathTimer = 0.4 + Math.random() * 0.2;
      }

      while (this.path && this.path.length > 1 && Math.hypot(this.group.position.x - this.path[1].x, this.group.position.z - this.path[1].z) < 0.4) {
        this.path.shift();
      }
      target = (this.path && (this.path[1] || this.path[0])) || goal;
    }

    // Check player chase
    const isChased = this.game.enemyManager && this.game.enemyManager.enemies.some(e => e.state === "chase");
    const speed = isChased ? 4.2 : 1.8;
    this.state = isChased ? "run" : "walking";
    this.playAction(isChased ? "run" : "walking");

    // Move
    const direction = new THREE.Vector3(target.x - this.group.position.x, 0, target.z - this.group.position.z);
    if (direction.lengthSq() > 0.0001) {
      direction.normalize();
      
      this.openDoorOnPath(direction);

      const previousPosition = this.group.position.clone();
      this.group.position.addScaledVector(direction, speed * deltaTime);
      this.collisionWorld.resolveCircle(this.group.position, 0.35);
      this.collisionWorld.resolveActorPosition(
        previousPosition,
        this.group.position,
        0.35,
        { actorId: this.id },
      );
      this.group.rotation.y = Math.atan2(direction.x, direction.z);
    }
  }

  openDoorOnPath(direction) {
    if (!this.game.doors) return;
    for (const door of this.game.doors) {
      if (door.isOpen || door.isLocked || door.isBlocked || door.distanceTo(this.group.position) > 2.0) {
        continue;
      }
      const doorDirection = new THREE.Vector3(door.position.x - this.group.position.x, 0, door.position.z - this.group.position.z).normalize();
      if (direction.dot(doorDirection) > 0.05) {
        door.isOpen = true;
      }
    }
  }

  updateFade(deltaTime) {
    this.fadeTimer -= deltaTime;
    const opacity = Math.max(0, this.fadeTimer / 10.0);
    this.setOpacity(opacity);

    if (this.fadeTimer <= 0) {
      this.dispose();
      this.game.removeLovelyDoll(this);
    }
  }

  setOpacity(opacity) {
    if (this.shadowMesh && this.shadowMesh.material) {
      this.shadowMesh.material.opacity = opacity;
      this.shadowMesh.material.needsUpdate = true;
    }
    if (!this.modelRoot) return;
    this.modelRoot.traverse((child) => {
      if (child.isMesh || child.isSkinnedMesh) {
        const mat = child.material;
        if (mat) {
          if (Array.isArray(mat)) {
            mat.forEach(m => {
              m.transparent = true;
              m.opacity = opacity;
              m.needsUpdate = true;
            });
          } else {
            mat.transparent = true;
            mat.opacity = opacity;
            mat.needsUpdate = true;
          }
        }
      }
    });
  }

  dispose() {
    this.game.scene.remove(this.group);
    if (this.shadowMesh) {
      this.shadowMesh.geometry?.dispose();
      if (this.shadowMesh.material) {
        this.shadowMesh.material.map?.dispose();
        this.shadowMesh.material.dispose();
      }
    }
    if (this.modelRoot) {
      this.modelRoot.traverse((child) => {
        if (child.isMesh || child.isSkinnedMesh) {
          child.geometry?.dispose();
          if (child.material) {
            if (Array.isArray(child.material)) {
              child.material.forEach(m => m.dispose());
            } else {
              child.material.dispose();
            }
          }
        }
      });
    }
  }
}

function cloneSkinnedMeshModel(source) {
  const clone = source.clone(true);
  
  const sourceLookup = new Map();
  const cloneLookup = new Map();
  
  source.traverse((child) => {
    if (child.isBone) {
      sourceLookup.set(child.name, child);
    }
  });
  
  clone.traverse((child) => {
    if (child.isBone) {
      cloneLookup.set(child.name, child);
    }
  });
  
  clone.traverse((child) => {
    if (child.isSkinnedMesh) {
      const originalBones = child.skeleton.bones;
      const clonedBones = [];
      for (const bone of originalBones) {
        clonedBones.push(cloneLookup.get(bone.name));
      }
      child.bind(new THREE.Skeleton(clonedBones, child.skeleton.boneInverses), child.bindMatrix);
    }
  });
  
  return clone;
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
  const material = new THREE.MeshBasicMaterial({
    map: texture,
    transparent: true,
    depthWrite: false,
    color: 0x000000,
  });
  const mesh = new THREE.Mesh(geometry, material);
  mesh.rotation.x = -Math.PI / 2;
  mesh.position.y = 0.015; // slightly above ground to prevent z-fighting
  group.add(mesh);
  return mesh;
}
