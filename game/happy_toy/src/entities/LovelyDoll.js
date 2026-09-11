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
    this.isFriendly = true;
    this.group.userData.isFriendly = true;
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
    this.state = "dance"; // "dance", "waiting", "walking", "run", "fade"
    this.isActivated = false;
    this.lookTimer = 0; // Cumulative time player stared at it
    this.fadeTimer = 0;
    this.dollIndex = 0; // Order index when activated (1-5)

    this.guideLight = null;
    this.hasWoken = false;
    this.guideTargetKeyId = null;
    this.waitTimer = 0;
    this.retargetTimer = 0;
    this.guideKind = null;
    this.chaseHintShown = false;
    
    this.path = null;
    this.pathTimer = 0;
    this.directCheckTimer = 0;
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

  wakeUp() {
    if (this.hasWoken) {
      return;
    }
    this.hasWoken = true;
    this.playAction("dance");
    this.guideLight = new THREE.PointLight(0xffc89a, 2.4, 7.5, 1.6);
    this.guideLight.position.set(0, 1.05, 0);
    this.guideLight.castShadow = false;
    this.group.add(this.guideLight);
    this.hud.setStatus("작은 인형이 고개를 끄덕입니다. 눈을 맞추면 함께 길을 찾아줍니다.", 4000);
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
    } else if (this.state === "waiting") {
      this.updateWaiting(deltaTime);
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
    
    const isStaring = inFrustum && hasLos && (gazeDot > 0.72);

    if (isStaring) {
      this.lookTimer += deltaTime;
      const progress = Math.min(100, Math.floor((this.lookTimer / 2.0) * 100));
      this.hud.setStatus(`인형이 길을 기억합니다... (${progress}%)`, 200);
      
      if (this.lookTimer >= 2.0) {
        this.activate();
      }
    } else {
      this.lookTimer = Math.max(0, this.lookTimer - deltaTime);
    }
  }

  activate() {
    this.wakeUp();
    this.isActivated = true;
    this.game.dollCountFound += 1;
    this.dollIndex = this.game.dollCountFound;
    this.game.spawnedDollIds.add(this.id);

    const result = this.resolveGuideTarget();
    if (result.kind === "key") {
      this.hud.setStatus(`인형이 ${result.label} 쪽으로 안내하기 시작합니다.`, 3000);
    } else {
      this.hud.setStatus("인형이 출구 상자로 안내합니다.", 3000);
    }

    this.state = "walking";
    this.playAction("walking", 0.3);
  }

  resolveGuideTarget() {
    const dollPos = this.group.position;
    const keys = (this.game.keys || []).filter((key) => (
      !key.isCollected && key.isAvailable && key.group?.visible
    ));
    const sameFloor = keys.filter((key) => Math.abs(key.position.y - dollPos.y) < 2.2);
    const pool = sameFloor.length > 0 ? sameFloor : keys.slice();
    pool.sort((a, b) => {
      const distA = Math.hypot(a.position.x - dollPos.x, a.position.z - dollPos.z);
      const distB = Math.hypot(b.position.x - dollPos.x, b.position.z - dollPos.z);
      return distA - distB;
    });

    let result;
    if (pool.length > 0) {
      const key = pool[0];
      result = {
        position: key.position.clone(),
        keyId: key.id,
        label: key.label,
        kind: "key",
      };
    } else {
      const exit = this.game.finalExit;
      const exitPos = exit?.group?.position || exit?.position || new THREE.Vector3(0, 0, 0);
      result = {
        position: exitPos.clone ? exitPos.clone() : new THREE.Vector3(exitPos.x, exitPos.y, exitPos.z),
        keyId: null,
        label: "장난감 상자",
        kind: "exit",
      };
    }

    this.targetPosition = result.position.clone();
    this.directCheckTimer = 0;
    this.guideTargetKeyId = result.keyId;
    this.guideKind = result.kind;
    this.path = null;
    this.pathTimer = 0;
    return result;
  }

  isGuideKeyPresent() {
    if (this.guideKind !== "key" || !this.guideTargetKeyId || !this.game.keys) {
      return false;
    }
    const key = this.game.keys.find((item) => item.id === this.guideTargetKeyId);
    return Boolean(key && !key.isCollected && key.isAvailable && key.group?.visible);
  }

  updateMovement(deltaTime) {
    this.retargetTimer -= deltaTime;
    if (this.retargetTimer <= 0) {
      this.retargetTimer = 1.2;
      if (this.guideKind === "key" && !this.isGuideKeyPresent()) {
        this.resolveGuideTarget();
      }
    }

    if (!this.targetPosition) return;
    let goal = this.targetPosition;
    
    const distToGoal = Math.hypot(this.group.position.x - goal.x, this.group.position.z - goal.z);
    if (distToGoal < 2.0 && Math.abs(this.group.position.y-goal.y)<0.6
        && this.collisionWorld.hasLineOfSight(this.group.position, goal)) {
      if (this.guideKind === "key" && !this.isGuideKeyPresent()) {
        this.resolveGuideTarget();
        goal = this.targetPosition;
      } else {
        this.state = "waiting";
        this.playAction("dance", 0.3);
        this.waitTimer = 0;
        if (this.guideKind === "key") {
          this.hud.setStatus("여기예요. 열쇠를 주우면 다음 곳으로 안내할게요.", 3000);
        }
        return;
      }
    }

    this.pathTimer -= deltaTime;
    this.directCheckTimer -= deltaTime;
    if(this.directCheckTimer<=0) {
      this.directPathClear=this.canWalkDirect(this.group.position,goal);
      this.directCheckTimer=.25;
    }
    const canMoveDirect = this.directPathClear;
    let target = goal;

    if (canMoveDirect) {
      this.path = [];
      this.pathTimer = 0.5;
    } else {
      if (this.path === null || this.pathTimer <= 0) {
        this.path = this.collisionWorld.findPath(this.group.position, goal, 0.35, {
          cellSize: 0.5,
          allowInterFloor: true,
          maxIterations: 14000,
        });
        this.pathTimer = 3;
        // The grid's first cell is an approximation of our current position.
        if (this.path.length > 1) this.path.shift();
      }

      while (this.path && this.path.length > 1 && this.group.position.distanceTo(this.path[0]) < 0.12) {
        this.path.shift();
      }
      target = this.path?.[0];
      if (!target) {
        this.playAction('dance');
        return; // Never steer into a wall when no route exists; retry later.
      }
    }

    const isChased = this.game.enemyManager && this.game.enemyManager.enemies.some(e => e.state === "chase");
    const speed = isChased ? 4.2 : 1.9;
    this.state = isChased ? "run" : "walking";
    this.playAction(isChased ? "run" : "walking");
    if (isChased) {
      if (!this.chaseHintShown) {
        this.chaseHintShown = true;
        this.hud.setStatus("같이 뛰어요!", 1800);
      }
    } else {
      this.chaseHintShown = false;
    }

    // Move
    const direction = new THREE.Vector3(target.x - this.group.position.x, 0, target.z - this.group.position.z);
    if (direction.lengthSq() > 0.0001) {
      const remainingDistance = direction.length();
      direction.normalize();
      
      this.openDoorOnPath(direction);

      const previousPosition = this.group.position.clone();
      this.group.position.addScaledVector(direction, Math.min(speed * deltaTime, remainingDistance));
      this.collisionWorld.resolveCircle(this.group.position, 0.35);
      this.collisionWorld.resolveActorPosition(
        previousPosition,
        this.group.position,
        0.35,
        { actorId: this.id },
      );
      this.group.rotation.y = Math.atan2(direction.x, direction.z);
      this.stuckTime = this.group.position.distanceToSquared(previousPosition)<0.000001
        ? (this.stuckTime || 0)+deltaTime : 0;
      if (this.stuckTime>0.6) { this.pathTimer=0; this.stuckTime=0; }
    }
  }

  canWalkDirect(start, goal) {
    if (Math.abs(start.y-goal.y)>0.35) return false;
    const steps=Math.ceil(start.distanceTo(goal)/0.2);
    const point=new THREE.Vector3();
    for(let i=1;i<=steps;i++) {
      point.lerpVectors(start,goal,i/steps);
      const surface=this.collisionWorld.getSurfaceAt(point);
      if (!surface.walkable || surface.type==='stair/transition'
          || this.collisionWorld.isCircleBlocked(point,.35)) return false;
    }
    return true;
  }

  updateWaiting(deltaTime) {
    this.playAction("dance");
    this.waitTimer += deltaTime;

    if (this.guideKind === "key") {
      if (!this.isGuideKeyPresent()) {
        const result = this.resolveGuideTarget();
        this.state = "walking";
        this.playAction("walking", 0.3);
        if (result.kind === "key") {
          this.hud.setStatus(`인형이 ${result.label} 쪽으로 안내하기 시작합니다.`, 3000);
        } else {
          this.hud.setStatus("인형이 출구 상자로 안내합니다.", 3000);
        }
        return;
      }

      // Keep waiting at the objective while the player catches up.
      return;
    }

    if (this.guideKind === "exit" && this.waitTimer >= 8) {
      this.state = "fade";
      this.playAction("dance", 0.3);
      this.fadeTimer = 10.0;
      this.hud.setStatus("안내를 마쳤어요. 조심히 가요.", 3000);
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
    if (this.guideLight) {
      this.guideLight.intensity = 2.4 * opacity;
    }
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
    if (this.guideLight) {
      this.group.remove(this.guideLight);
      this.guideLight.dispose();
      this.guideLight = null;
    }
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
          // Geometry is shared with the cached model and other dolls.
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
