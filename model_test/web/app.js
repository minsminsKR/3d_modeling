import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { MTLLoader } from "three/addons/loaders/MTLLoader.js";
import { OBJLoader } from "three/addons/loaders/OBJLoader.js";

const viewport = document.querySelector("#viewport");
const statusText = document.querySelector("#status");
const modelSelect = document.querySelector("#model-select");
const reloadButton = document.querySelector("#reload-models");
const resetButton = document.querySelector("#reset-view");

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x9fb7d3);
scene.fog = new THREE.Fog(0x9fb7d3, 48, 120);

const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 500);
camera.position.set(0, 5.2, -9.5);

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
viewport.appendChild(renderer.domElement);

const clock = new THREE.Clock();
const keys = new Set();
const obstacleBoxes = [];
const GRAVITY = 18;
const JUMP_SPEED = 10;
const JUMPABLE_HEIGHT = 1.3;
const PLAYER_HEIGHT = 2.2;
const SURFACE_EPSILON = 0.08;
const CAMERA_DISTANCE = 9.5;
const CAMERA_TARGET_HEIGHT = 1.55;
const CAMERA_PITCH_MIN = THREE.MathUtils.degToRad(-10);
const CAMERA_PITCH_MAX = THREE.MathUtils.degToRad(38);
const CAMERA_FOLLOW_SPEED = 10;
const CAMERA_ROTATION_SPEED = 0.006;

const player = new THREE.Group();
player.position.set(0, 0, 4);
scene.add(player);

const cameraTarget = new THREE.Vector3();
const desiredCameraPosition = new THREE.Vector3();
let cameraYaw = Math.PI;
let cameraPitch = THREE.MathUtils.degToRad(18);
let isDraggingCamera = false;
let loadedModel = null;
let placeholder = null;
let animationMixer = null;
let animationAction = null;
let jumpAnimationAction = null;
let isJumping = false;
let jumpVelocity = 0;
let modelScale = 1;
let modelRadius = 0.75;
let characters = [];
let currentCharacter = null;

const obstacles = [
  { name: "small_box_01", position: [-6, 0.5, -3], scale: [1, 1, 1], color: 0x22d3ee },
  { name: "small_box_02", position: [5, 0.5, -6], scale: [1.2, 1, 1.2], color: 0x38bdf8 },
  { name: "wide_step_01", position: [-10, 0.25, 6], scale: [6, 0.5, 2], color: 0x84cc16 },
  { name: "wide_step_02", position: [9, 0.35, 7], scale: [5, 0.7, 2.5], color: 0x22c55e },
  { name: "tall_wall_01", position: [-14, 2, -9], scale: [1, 4, 8], color: 0xf97316 },
  { name: "tall_wall_02", position: [14, 2, -1], scale: [1, 4, 10], color: 0xef4444 },
  { name: "long_wall_01", position: [0, 1.5, 14], scale: [14, 3, 1], color: 0xfacc15 },
  { name: "pillar_01", position: [-5, 1.5, 11], scale: [1.3, 3, 1.3], color: 0xa78bfa, cylinder: true },
  { name: "pillar_02", position: [4, 2, 10], scale: [1.8, 4, 1.8], color: 0xd946ef, cylinder: true },
  { name: "large_block_01", position: [-2, 1.5, -11], scale: [4, 3, 4], color: 0x82a0ff },
  { name: "large_block_02", position: [10, 2.5, -13], scale: [5, 5, 3], color: 0xffa082 },
];

initWorld();
createPlaceholder();
loadModelList();
animate();

reloadButton.addEventListener("click", loadModelList);
resetButton.addEventListener("click", resetModelTransform);
modelSelect.addEventListener("change", () => {
  const selectedOption = modelSelect.selectedOptions[0];
  if (!selectedOption?.dataset.index) {
    createPlaceholder();
    setStatus("모델을 선택하지 않았습니다. placeholder를 표시합니다.");
    return;
  }

  currentCharacter = characters[Number(selectedOption.dataset.index)];
  loadCharacter(currentCharacter);
});

window.addEventListener("keydown", (event) => {
  keys.add(event.key.toLowerCase());
  if (event.code === "Space") {
    event.preventDefault();
    startJump();
  }
});

window.addEventListener("keyup", (event) => {
  keys.delete(event.key.toLowerCase());
});

renderer.domElement.addEventListener("pointerdown", (event) => {
  if (event.button !== 0) {
    return;
  }

  renderer.domElement.requestPointerLock?.();
  isDraggingCamera = true;
});

document.addEventListener("mousemove", (event) => {
  const isPointerLocked = document.pointerLockElement === renderer.domElement;
  if (!isPointerLocked && !isDraggingCamera) {
    return;
  }

  rotateCameraByMouseDelta(event.movementX, event.movementY);
});

document.addEventListener("pointerlockchange", () => {
  isDraggingCamera = document.pointerLockElement === renderer.domElement;
});

document.addEventListener("pointerup", () => {
  if (document.pointerLockElement !== renderer.domElement) {
    isDraggingCamera = false;
  }
});

renderer.domElement.addEventListener("pointercancel", () => {
  isDraggingCamera = false;
});

renderer.domElement.addEventListener("wheel", (event) => {
  event.preventDefault();
}, { passive: false });

window.addEventListener("resize", () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});

function rotateCameraByMouseDelta(deltaX, deltaY) {
  cameraYaw -= deltaX * CAMERA_ROTATION_SPEED;
  cameraPitch = THREE.MathUtils.clamp(
    cameraPitch + deltaY * CAMERA_ROTATION_SPEED,
    CAMERA_PITCH_MIN,
    CAMERA_PITCH_MAX,
  );
}

function initWorld() {
  const hemiLight = new THREE.HemisphereLight(0xf8fbff, 0x4b5d4f, 1.9);
  scene.add(hemiLight);

  const sunLight = new THREE.DirectionalLight(0xffffff, 2.4);
  sunLight.position.set(18, 30, 12);
  sunLight.castShadow = true;
  sunLight.shadow.camera.left = -50;
  sunLight.shadow.camera.right = 50;
  sunLight.shadow.camera.top = 50;
  sunLight.shadow.camera.bottom = -50;
  scene.add(sunLight);

  const ground = new THREE.Mesh(
    new THREE.BoxGeometry(80, 0.2, 80),
    new THREE.MeshStandardMaterial({ color: 0x4a5e52, roughness: 0.9 }),
  );
  ground.position.y = -0.1;
  ground.receiveShadow = true;
  scene.add(ground);

  const grid = new THREE.GridHelper(80, 16, 0xffffff, 0xffffff);
  grid.material.transparent = true;
  grid.material.opacity = 0.28;
  scene.add(grid);

  for (const obstacle of obstacles) {
    const geometry = obstacle.cylinder
      ? new THREE.CylinderGeometry(0.5, 0.5, 1, 28)
      : new THREE.BoxGeometry(1, 1, 1);
    const material = new THREE.MeshStandardMaterial({ color: obstacle.color, roughness: 0.7 });
    const mesh = new THREE.Mesh(geometry, material);

    mesh.name = obstacle.name;
    mesh.position.fromArray(obstacle.position);
    mesh.scale.fromArray(obstacle.scale);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    scene.add(mesh);

    mesh.updateMatrixWorld(true);
    obstacleBoxes.push(new THREE.Box3().setFromObject(mesh));
  }
}

async function loadModelList() {
  try {
    const response = await fetch("/api/models");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const data = await response.json();
    renderModelOptions(data.models);
  } catch (error) {
    setStatus(`모델 목록을 불러오지 못했습니다: ${error.message}`);
  }
}

function renderModelOptions(models) {
  characters = models;
  modelSelect.innerHTML = "";

  const emptyOption = document.createElement("option");
  emptyOption.value = "";
  emptyOption.textContent = "모델 없음 - placeholder 사용";
  modelSelect.appendChild(emptyOption);

  for (const model of models) {
    const option = document.createElement("option");
    option.value = model.name;
    option.dataset.index = String(models.indexOf(model));
    option.textContent = `${model.name} (${model.assets.length}개 구성 파일)`;
    modelSelect.appendChild(option);
  }

  if (models.length === 0) {
    createPlaceholder();
    setStatus("model 폴더에 캐릭터 폴더 또는 FBX/GLB/GLTF/OBJ 파일이 없습니다.");
    return;
  }

  modelSelect.selectedIndex = 1;
  currentCharacter = models[0];
  loadCharacter(currentCharacter);
}

function loadCharacter(character) {
  const loadableAssets = getLoadableAssets(currentCharacter);
  if (!loadableAssets.length) {
    createPlaceholder();
    return;
  }

  const asset = character.primaryAsset || loadableAssets[0];
  const assetSummary = currentCharacter.assets
    .map((item) => `${formatCategory(item.category)}:${item.name.split("/").at(-1)}`)
    .join(", ");

  loadModel(asset, currentCharacter, assetSummary);
}

function loadModel(asset, character, assetSummary = "") {
  clearCurrentModel();
  setStatus(`${character.name} 로딩 중... 선택 파일: ${asset.name.split("/").at(-1)}`);

  const onLoad = (object, animations = []) => {
    const hasMesh = hasRenderableMesh(object);
    if (hasMesh) {
      applySourceTextureToMixamoModel(object, asset, character);
    }
    loadedModel = normalizeModel(object, hasMesh);
    player.add(loadedModel);

    if (animations.length > 0) {
      animationMixer = new THREE.AnimationMixer(loadedModel);
      animationAction = animationMixer.clipAction(animations[0]);
      animationAction.play();
      animationAction.paused = true;
      animationMixer.addEventListener("finished", (event) => {
        if (event.action === jumpAnimationAction) {
          jumpAnimationAction.stop();
          jumpAnimationAction.reset();
          if (animationAction) {
            animationAction.paused = !isMoving();
          }
        }
      });
      loadAdditionalMixamoAnimations(character, asset);
    }

    if (hasMesh) {
      const animationText = animations.length > 0 ? ` | 애니메이션 ${animations.length}개 재생 중` : "";
      setStatus(`${character.name} 로딩 완료${animationText}. 구성 파일: ${assetSummary}`);
    } else {
      setStatus(`${character.name} 로딩 완료. 선택 파일에 메시가 없어 모션/뼈대 전용 파일로 보입니다.`);
    }
  };

  const onError = (error) => {
    console.error(error);
    createPlaceholder();
    setStatus(`${character.name} 로딩 실패. 모델 포맷 또는 텍스처 경로를 확인하세요.`);
  };

  if (asset.extension === ".fbx") {
    new FBXLoader().load(asset.url, (object) => onLoad(object, object.animations), undefined, onError);
    return;
  }

  if (asset.extension === ".glb" || asset.extension === ".gltf") {
    new GLTFLoader().load(asset.url, (gltf) => onLoad(gltf.scene, gltf.animations), undefined, onError);
    return;
  }

  if (asset.extension === ".obj") {
    loadObjWithOptionalMtl(asset, character, onLoad, onError);
    return;
  }

  createPlaceholder();
  setStatus(`${asset.extension} 파일은 아직 지원하지 않습니다.`);
}

function getLoadableAssets(character) {
  return (character?.assets || []).filter((asset) => (
    [".fbx", ".glb", ".gltf", ".obj"].includes(asset.extension)
  ));
}

function loadAdditionalMixamoAnimations(character, baseAsset) {
  if (!animationMixer) {
    return;
  }

  const extraAnimationAssets = (character?.assets || []).filter((asset) => (
    asset.category === "mixamo" &&
    asset.extension === ".fbx" &&
    asset.name !== baseAsset.name
  ));

  for (const animationAsset of extraAnimationAssets) {
    new FBXLoader().load(
      animationAsset.url,
      (object) => {
        if (!object.animations.length) {
          return;
        }
        const action = animationMixer.clipAction(object.animations[0]);
        action.enabled = true;
        action.clampWhenFinished = true;
        action.setLoop(THREE.LoopOnce, 1);
        action.stop();

        if (animationAsset.name.toLowerCase().includes("jump")) {
          jumpAnimationAction = action;
          setStatus(`${character.name} 로딩 완료. Space 키로 Jump 모션을 실행할 수 있습니다.`);
        }
      },
      undefined,
      () => {
        console.warn(`Failed to load extra animation: ${animationAsset.name}`);
      },
    );
  }
}

function applySourceTextureToMixamoModel(object, asset, character) {
  if (asset.category !== "mixamo" || asset.extension !== ".fbx") {
    return;
  }

  const textureAsset = findSourceTextureAsset(character);
  const skinnedMeshes = [];

  object.traverse((child) => {
    if (!child.isMesh && !child.isSkinnedMesh) {
      return;
    }
    skinnedMeshes.push(child);
    replaceMaterial(
      child,
      new THREE.MeshBasicMaterial({
        color: 0xb17439,
        side: THREE.DoubleSide,
      }),
    );
  });

    if (skinnedMeshes.length === 0) {
      return;
    }

    if (!textureAsset) {
      setStatus(`${character.name} 로딩 완료. source/model_textured 이미지가 없어 기본 색으로 표시합니다.`);
      return;
    }

  const texturedMeshes = skinnedMeshes.filter((mesh) => Boolean(mesh.geometry?.attributes?.uv));
  if (texturedMeshes.length === 0) {
    setStatus(`${character.name} 로딩 완료. FBX에 UV가 없어 원본 텍스처 대신 기본 색을 적용했습니다.`);
    return;
  }

  new THREE.TextureLoader().load(
    textureAsset.url,
    (texture) => {
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.flipY = true;
      texture.needsUpdate = true;
      for (const mesh of texturedMeshes) {
        replaceMaterial(
          mesh,
          new THREE.MeshBasicMaterial({
            map: texture,
            color: 0xffffff,
            side: THREE.DoubleSide,
          }),
        );
      }
      setStatus(`${character.name} 로딩 완료. Mixamo FBX에 원본 텍스처를 적용했습니다.`);
    },
    undefined,
    () => {
      setStatus(`${character.name} 로딩 완료. 원본 텍스처를 불러오지 못해 기본 색을 적용했습니다.`);
    },
  );
}

function replaceMaterial(mesh, material) {
  const existingMaterials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
  for (const existingMaterial of existingMaterials) {
    existingMaterial?.dispose?.();
  }
  mesh.material = material;
  mesh.material.needsUpdate = true;
}

function findSourceTextureAsset(character) {
  const imageAssets = (character?.assets || []).filter((asset) => (
    [".jpg", ".jpeg", ".png", ".webp"].includes(asset.extension)
  ));
  return (
    imageAssets.find((asset) => asset.category === "source" && /model_textured\.(jpg|jpeg|png|webp)$/i.test(asset.name)) ||
    imageAssets.find((asset) => asset.category === "source") ||
    imageAssets[0] ||
    null
  );
}

function loadObjWithOptionalMtl(asset, character, onLoad, onError) {
  const assetFolder = asset.name.split("/").slice(0, -1).join("/");
  const materialAsset = character.assets.find((candidate) => (
    candidate.extension === ".mtl" &&
    candidate.name.split("/").slice(0, -1).join("/") === assetFolder
  ));

  const objLoader = new OBJLoader();
  if (!materialAsset) {
    objLoader.load(asset.url, (object) => onLoad(object, []), undefined, onError);
    return;
  }

  const baseUrl = asset.url.split("/").slice(0, -1).join("/") + "/";
  const materialFilename = materialAsset.url.split("/").at(-1);
  const materialLoader = new MTLLoader();
  materialLoader.setPath(baseUrl);
  materialLoader.setResourcePath(baseUrl);
  materialLoader.load(
    materialFilename,
    (materials) => {
      materials.preload();
      objLoader.setMaterials(materials);
      objLoader.load(asset.url, (object) => onLoad(object, []), undefined, onError);
    },
    undefined,
    onError,
  );
}

function formatCategory(category) {
  if (category === "mixamo") return "Mixamo";
  if (category === "exports") return "변환본";
  if (category === "source") return "원본";
  if (category === "_extracted") return "압축해제";
  return "기타";
}

function hasRenderableMesh(object) {
  let hasMesh = false;
  object.traverse((child) => {
    if (child.isMesh || child.isSkinnedMesh) {
      hasMesh = true;
    }
  });
  return hasMesh;
}

function normalizeModel(object, hasMesh) {
  const box = new THREE.Box3().setFromObject(object);
  const size = box.getSize(new THREE.Vector3());
  const center = box.getCenter(new THREE.Vector3());
  const maxDimension = Number.isFinite(size.x) ? Math.max(size.x, size.y, size.z) || 1 : 1;

  if (!hasMesh) {
    const skeleton = new THREE.SkeletonHelper(object);
    skeleton.material.color.set(0xfff176);
    object.add(skeleton);
  }

  if (Number.isFinite(center.x) && Number.isFinite(size.y)) {
    object.position.sub(center);
    object.position.y += size.y / 2;
  }
  object.scale.multiplyScalar(2.2 / maxDimension);
  object.traverse((child) => {
    if (child.isMesh) {
      child.castShadow = true;
      child.receiveShadow = true;
    }
  });

  const wrapper = new THREE.Group();
  wrapper.add(object);
  wrapper.scale.setScalar(modelScale);
  return wrapper;
}

function createPlaceholder() {
  clearCurrentModel();

  placeholder = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.BoxGeometry(1, 2, 1),
    new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.6 }),
  );
  body.position.y = 1;
  body.castShadow = true;

  const head = new THREE.Mesh(
    new THREE.SphereGeometry(0.42, 32, 16),
    new THREE.MeshStandardMaterial({ color: 0xd1d5db, roughness: 0.5 }),
  );
  head.position.y = 2.25;
  head.castShadow = true;

  placeholder.add(body, head);
  player.add(placeholder);
}

function clearCurrentModel() {
  animationMixer = null;
  animationAction = null;
  jumpAnimationAction = null;
  isJumping = false;
  jumpVelocity = 0;
  player.position.y = 0;

  if (loadedModel) {
    player.remove(loadedModel);
    disposeObject(loadedModel);
    loadedModel = null;
  }

  if (placeholder) {
    player.remove(placeholder);
    disposeObject(placeholder);
    placeholder = null;
  }
}

function disposeObject(object) {
  object.traverse((child) => {
    if (child.geometry) {
      child.geometry.dispose();
    }

    if (child.material) {
      const materials = Array.isArray(child.material) ? child.material : [child.material];
      for (const material of materials) {
        material.dispose();
      }
    }
  });
}

function resetModelTransform() {
  player.position.set(0, 0, 4);
  player.rotation.set(0, 0, 0);
  modelScale = 1;
  cameraYaw = Math.PI;
  cameraPitch = THREE.MathUtils.degToRad(18);

  const activeModel = loadedModel || placeholder;
  if (activeModel) {
    activeModel.scale.setScalar(modelScale);
  }

  updateCameraFollow(1, true);
  setStatus("위치와 모델 크기를 초기화했습니다.");
}

function updatePlayer(delta) {
  snapToWalkableSurface();

  const forwardInput = Number(keys.has("w") || keys.has("arrowup")) - Number(keys.has("s") || keys.has("arrowdown"));
  const strafeInput = Number(keys.has("d") || keys.has("arrowright")) - Number(keys.has("a") || keys.has("arrowleft"));
  const direction = getCameraRelativeDirection(forwardInput, strafeInput);

  const isMovingNow = direction.lengthSq() > 0;
  if (animationAction) {
    animationAction.paused = isJumping || !isMovingNow;
  }

  if (isMovingNow) {
    direction.normalize();
    const nextPosition = player.position.clone().addScaledVector(direction, delta * 7);

    if (!collidesWithObstacle(nextPosition)) {
      player.position.copy(nextPosition);
    }

    player.rotation.y = Math.atan2(direction.x, direction.z);
  }

  if (keys.has("q")) player.rotation.y += delta * 2.2;
  if (keys.has("e")) player.rotation.y -= delta * 2.2;

  if (keys.has("=") || keys.has("+")) setModelScale(modelScale + delta);
  if (keys.has("-")) setModelScale(Math.max(0.1, modelScale - delta));
  if (keys.has("r")) resetModelTransform();

  updateJump(delta);
  updateCameraFollow(delta);
}

function getCameraRelativeDirection(forwardInput, strafeInput) {
  const direction = new THREE.Vector3();
  if (!forwardInput && !strafeInput) {
    return direction;
  }

  const cameraForward = new THREE.Vector3();
  camera.getWorldDirection(cameraForward);
  cameraForward.y = 0;
  cameraForward.normalize();

  const cameraRight = new THREE.Vector3().setFromMatrixColumn(camera.matrixWorld, 0);
  cameraRight.y = 0;
  cameraRight.normalize();

  direction.addScaledVector(cameraForward, forwardInput);
  direction.addScaledVector(cameraRight, strafeInput);
  return direction;
}

function updateCameraFollow(delta, immediate = false) {
  cameraTarget.copy(player.position).add(new THREE.Vector3(0, CAMERA_TARGET_HEIGHT, 0));

  const horizontalDistance = Math.cos(cameraPitch) * CAMERA_DISTANCE;
  desiredCameraPosition.set(
    cameraTarget.x + Math.sin(cameraYaw) * horizontalDistance,
    cameraTarget.y + Math.sin(cameraPitch) * CAMERA_DISTANCE,
    cameraTarget.z + Math.cos(cameraYaw) * horizontalDistance,
  );

  if (immediate) {
    camera.position.copy(desiredCameraPosition);
  } else {
    const followFactor = 1 - Math.exp(-CAMERA_FOLLOW_SPEED * delta);
    camera.position.lerp(desiredCameraPosition, followFactor);
  }

  camera.lookAt(cameraTarget);
}

function isMoving() {
  return keys.has("w") || keys.has("arrowup") ||
    keys.has("s") || keys.has("arrowdown") ||
    keys.has("a") || keys.has("arrowleft") ||
    keys.has("d") || keys.has("arrowright");
}

function startJump() {
  if (isJumping) {
    return;
  }

  isJumping = true;
  jumpVelocity = JUMP_SPEED;

  if (animationAction) {
    animationAction.paused = true;
  }

  if (jumpAnimationAction) {
    jumpAnimationAction.reset();
    jumpAnimationAction.setLoop(THREE.LoopOnce, 1);
    jumpAnimationAction.clampWhenFinished = true;
    jumpAnimationAction.play();
  }
}

function updateJump(delta) {
  if (!isJumping) {
    return;
  }

  const previousY = player.position.y;
  jumpVelocity -= GRAVITY * delta;
  player.position.y += jumpVelocity * delta;
  const landingHeight = getLandingHeight(player.position, previousY);

  if (player.position.y <= landingHeight) {
    player.position.y = landingHeight;
    jumpVelocity = 0;
    isJumping = false;
    if (animationAction) {
      animationAction.paused = !isMoving();
    }
  }
}

function collidesWithObstacle(position) {
  const playerBox = new THREE.Box3(
    new THREE.Vector3(position.x - modelRadius, position.y, position.z - modelRadius),
    new THREE.Vector3(position.x + modelRadius, position.y + PLAYER_HEIGHT, position.z + modelRadius),
  );

  return obstacleBoxes.some((box) => {
    if (!box.intersectsBox(playerBox)) {
      return false;
    }
    if (position.y >= box.max.y - SURFACE_EPSILON) {
      return false;
    }
    return true;
  });
}

function getLandingHeight(position, previousY) {
  let landingHeight = 0;
  for (const box of obstacleBoxes) {
    if (box.max.y > JUMPABLE_HEIGHT) {
      continue;
    }
    if (!horizontalFootprintOverlaps(position, box)) {
      continue;
    }
    if (previousY + SURFACE_EPSILON >= box.max.y && position.y <= box.max.y + SURFACE_EPSILON) {
      landingHeight = Math.max(landingHeight, box.max.y);
    }
  }
  return landingHeight;
}

function snapToWalkableSurface() {
  if (isJumping) {
    return;
  }

  let surfaceHeight = 0;
  for (const box of obstacleBoxes) {
    if (box.max.y > JUMPABLE_HEIGHT) {
      continue;
    }
    if (horizontalFootprintOverlaps(player.position, box)) {
      surfaceHeight = Math.max(surfaceHeight, box.max.y);
    }
  }
  player.position.y = surfaceHeight;
}

function horizontalFootprintOverlaps(position, box) {
  return (
    position.x + modelRadius > box.min.x &&
    position.x - modelRadius < box.max.x &&
    position.z + modelRadius > box.min.z &&
    position.z - modelRadius < box.max.z
  );
}

function setModelScale(value) {
  modelScale = Math.min(Math.max(value, 0.1), 8);
  const activeModel = loadedModel || placeholder;

  if (activeModel) {
    activeModel.scale.setScalar(modelScale);
  }
}

function setStatus(message) {
  statusText.textContent = message;
}

function animate() {
  requestAnimationFrame(animate);

  const delta = Math.min(clock.getDelta(), 0.05);
  updatePlayer(delta);

  if (animationMixer) {
    animationMixer.update(delta);
  }

  renderer.render(scene, camera);
}
