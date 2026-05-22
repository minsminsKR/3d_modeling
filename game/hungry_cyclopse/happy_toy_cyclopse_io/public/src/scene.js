import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import * as SkeletonUtils from "three/addons/utils/SkeletonUtils.js";

const COLORS = {
  hwacat: 0xffb187,
  uncat: 0x9dccff,
  angry: 0xff6375,
  giant: 0xb978ff
};

const PLAYER_BASE_SIZE = 5;
const BASE_VISUAL_DIAMETER = 6.8;
const MIN_VISUAL_SCALE = 0.5;
const FLOOR_TEXTURE = "/assets/textures/floors/floor.png";
const WALL_TEXTURE = "/assets/textures/walls/wall.png";
const DOOR_TEXTURE = "/assets/textures/doors/doors.png";
const RED_PUDDLE_TEXTURE = "/assets/textures/props/placeholder-red-puddle-1f/basecolor.png";
const SURVIVAL_FOG_COLOR = 0x09080b;
const SURVIVAL_FOG_DENSITY = 0.0068;
const WORLD_PROP_SEED = 73491;
const MAX_RENDER_PIXEL_RATIO = 1.5;
const MIN_RENDER_PIXEL_RATIO = 0.9;
const CHARACTER_FULL_MODEL_DISTANCE = 260;
const GIANT_FULL_MODEL_DISTANCE = 430;

const PROP_ASSETS = [
  { key: "barricade", url: "/assets/props/barricade/model.glb", radius: 165, count: 5, scale: [7, 10], y: 0 },
  { key: "barredWindow", url: "/assets/props/barred-window/model.glb", radius: 205, count: 4, scale: [8, 12], y: 4 },
  { key: "wire", url: "/assets/props/corridor-wire/model.glb", radius: 145, count: 6, scale: [5, 9], y: 1 },
  { key: "mannequinA", url: "/assets/props/silent-mannequin-1f/model.glb", radius: 240, count: 4, scale: [7, 12], y: 0 },
  { key: "mannequinB", url: "/assets/props/silent-mannequin-2f/model.glb", radius: 260, count: 3, scale: [7, 12], y: 0 },
  { key: "dollCircle", url: "/assets/props/upper-doll-circle/model.glb", radius: 190, count: 4, scale: [6, 9], y: 0 },
  { key: "wrappedBody", url: "/assets/props/placeholder-wrapped-body-1f/model.glb", radius: 210, count: 3, scale: [6, 10], y: 0 },
  { key: "mirrorShards", url: "/assets/props/upper-mirror-shards/model.glb", radius: 180, count: 5, scale: [6, 11], y: 0.08 }
];

const CHARACTER_ASSETS = {
  cyclopse: {
    model: "/assets/characters/Cyclopse/mixamo/Run.fbx",
    texture: "/assets/characters/Cyclopse/source/model_textured.jpg",
    visualDiameter: BASE_VISUAL_DIAMETER,
    lockRootVerticalMotion: true
  },
  hwacat: {
    model: "/assets/characters/Hwacat/mixamo/Hip%20Hop%20Dancing.fbx",
    texture: "/assets/characters/Hwacat/source/model_textured.jpg",
    visualDiameter: BASE_VISUAL_DIAMETER,
    lockRootVerticalMotion: true
  },
  uncat: {
    model: "/assets/characters/Uncat/mixamo/Run.fbx",
    texture: "/assets/characters/Uncat/source/model_textured.jpg",
    visualDiameter: BASE_VISUAL_DIAMETER,
    lockRootVerticalMotion: true
  },
  angry: {
    model: "/assets/characters/Hwacat_angry/mixamo/Zombie%20Run.fbx",
    texture: "/assets/characters/Hwacat_angry/source/model_textured.jpg",
    visualDiameter: BASE_VISUAL_DIAMETER,
    lockRootVerticalMotion: true
  }
};

export class GameScene {
  constructor(canvas) {
    this.canvas = canvas;
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    this.currentPixelRatio = Math.min(window.devicePixelRatio || 1, MAX_RENDER_PIXEL_RATIO);
    this.targetPixelRatio = this.currentPixelRatio;
    this.renderer.setPixelRatio(this.currentPixelRatio);
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.55;
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(SURVIVAL_FOG_COLOR);
    this.scene.fog = new THREE.FogExp2(SURVIVAL_FOG_COLOR, SURVIVAL_FOG_DENSITY);

    this.camera = new THREE.PerspectiveCamera(64, 1, 0.1, 1800);
    this.cameraLightRig = new THREE.Group();
    this.headLamp = new THREE.SpotLight(0xffe6bd, 0, 470, Math.PI / 4.6, 0.78, 0.82);
    this.headLamp.castShadow = true;
    this.headLamp.shadow.mapSize.set(512, 512);
    this.headLamp.shadow.camera.near = 1;
    this.headLamp.shadow.camera.far = 260;
    this.headLamp.shadow.bias = -0.0008;
    this.fillLamp = new THREE.PointLight(0xaec4ff, 0, 48, 1.15);
    this.flashlightAura = new THREE.PointLight(0xffd6a0, 0, 64, 1.2);
    this.flashlightVisuals = this.#createFlashlightVisuals();
    this.players = new Map();
    this.enemies = new Map();
    this.selfId = null;
    this.clock = new THREE.Clock();
    this.fbxLoader = new FBXLoader();
    this.gltfLoader = new GLTFLoader();
    this.textureLoader = new THREE.TextureLoader();
    this.modelCache = new Map();
    this.propCache = new Map();
    this.staticProps = [];
    this.mixers = new Set();
    this.calibrationGroups = [];
    this._footProbe = new THREE.Vector3();
    this._groundFrame = 0;
    this._lodFrame = 0;
    this._qualityTimer = 0;
    this._frameTimeAverage = 1 / 60;
    this._propVisibilityFrame = 0;

    window.__cyclopseScene = this;
    this.#lights();
    this.#cameraLights();
    this.scene.add(this.flashlightAura, this.flashlightVisuals);
    this.#arena();
    this.resize();
    window.addEventListener("resize", () => this.resize());
  }

  setSelf(id) {
    this.selfId = id;
  }

  resize() {
    const width = window.innerWidth;
    const height = window.innerHeight;
    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
  }

  render(snapshot, inputState = null) {
    const dt = Math.min(0.05, this.clock.getDelta());
    if (!snapshot) {
      this.#updateMixers(dt);
      this.renderer.render(this.scene, this.camera);
      return;
    }
    this.#syncPlayers(snapshot.players, dt, inputState);
    this.#syncEnemies(snapshot.enemies, dt);
    const self = this.players.get(this.selfId);
    this.#updateFootprintTargets(snapshot);
    this.#updateCamera(self, inputState, dt);
    this.#updateFlashlight(self, inputState, dt);
    this.#updateCharacterLod(self);
    this.#updateStaticPropVisibility(self);
    this.#updateMixers(dt, self);
    this.#snapVisibleModelsToGround(self);
    this.#updateRenderQuality(dt);
    this.renderer.render(this.scene, this.camera);
  }

  #lights() {
    this.scene.add(new THREE.HemisphereLight(0x8b8298, 0x2f2630, 2.0));
    this.scene.add(new THREE.AmbientLight(0x332d36, 2.8));
    const moon = new THREE.DirectionalLight(0x9489a4, 2.25);
    moon.position.set(-130, 210, -110);
    this.scene.add(moon);

    const side = new THREE.DirectionalLight(0x76545f, 0.92);
    side.position.set(150, 70, 120);
    this.scene.add(side);
  }

  #cameraLights() {
    this.headLamp.position.set(0, 8, 0);
    this.headLamp.target.position.set(0, 5, -1);
    this.fillLamp.position.set(0, 6, 0);
    this.scene.add(this.headLamp, this.headLamp.target, this.fillLamp);
  }

  #arena() {
    const arenaSize = 1100;
    const tileSize = 16;
    const rng = seededRandom(WORLD_PROP_SEED);
    const floorMaterial = new THREE.MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.95,
      metalness: 0.0,
      emissive: 0xb8a08d,
      emissiveIntensity: 0.62
    });
    this.textureLoader.load(
      FLOOR_TEXTURE,
      (texture) => {
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.wrapS = THREE.RepeatWrapping;
        texture.wrapT = THREE.RepeatWrapping;
        texture.anisotropy = Math.min(this.renderer.capabilities.getMaxAnisotropy?.() || 1, 8);
        texture.repeat.set(arenaSize / tileSize, arenaSize / tileSize);
        floorMaterial.map = texture;
        floorMaterial.emissiveMap = texture;
        floorMaterial.needsUpdate = true;
      },
      undefined,
      () => {
        floorMaterial.color.set(0x5c565d);
      }
    );
    const floor = new THREE.Mesh(new THREE.PlaneGeometry(arenaSize, arenaSize, 1, 1), floorMaterial);
    floor.rotation.x = -Math.PI / 2;
    floor.receiveShadow = true;
    this.scene.add(floor);
    this.#groundMist(arenaSize, rng);
    this.#corridorWalls(arenaSize);
    this.#scatterAssetProps(rng);
    this.#featuredAssetProps();
    this.#bloodPuddles(rng);

    const ring = new THREE.Mesh(
      new THREE.RingGeometry(520, 526, 128),
      new THREE.MeshBasicMaterial({ color: 0xb64c5a, side: THREE.DoubleSide })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.2;
    this.scene.add(ring);
  }

  #corridorWalls(arenaSize) {
    const wallMaterial = new THREE.MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.92,
      metalness: 0,
      emissive: 0x171018,
      emissiveIntensity: 0.22
    });
    const innerWallMaterial = new THREE.MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.94,
      metalness: 0,
      emissive: 0x110d13,
      emissiveIntensity: 0.2,
      side: THREE.DoubleSide
    });
    const doorMaterial = new THREE.MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.9,
      metalness: 0,
      emissive: 0x120d12,
      emissiveIntensity: 0.18
    });

    this.textureLoader.load(WALL_TEXTURE, (texture) => {
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.wrapS = THREE.RepeatWrapping;
      texture.wrapT = THREE.RepeatWrapping;
      texture.repeat.set(2.5, 1.2);
      wallMaterial.map = texture;
      wallMaterial.needsUpdate = true;
      const innerTexture = texture.clone();
      innerTexture.needsUpdate = true;
      innerTexture.repeat.set(28, 1.8);
      innerWallMaterial.map = innerTexture;
      innerWallMaterial.needsUpdate = true;
    });
    this.textureLoader.load(DOOR_TEXTURE, (texture) => {
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.wrapS = THREE.RepeatWrapping;
      texture.wrapT = THREE.RepeatWrapping;
      texture.repeat.set(1.2, 1.4);
      doorMaterial.map = texture;
      doorMaterial.needsUpdate = true;
    });

    const panelGeometry = new THREE.BoxGeometry(1, 1, 1);
    const matrix = new THREE.Matrix4();
    const quaternion = new THREE.Quaternion();
    const wallTransforms = [];
    const doorTransforms = [];
    const pushTransform = (target, x, y, z, rotationY, sx, sy, sz) => {
      target.push({
        position: new THREE.Vector3(x, y, z),
        quaternion: quaternion.setFromEuler(new THREE.Euler(0, rotationY, 0)).clone(),
        scale: new THREE.Vector3(sx, sy, sz)
      });
    };
    const createInstancedPanels = (transforms, material) => {
      if (!transforms.length) return;
      const mesh = new THREE.InstancedMesh(panelGeometry, material, transforms.length);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      transforms.forEach((transform, index) => {
        matrix.compose(transform.position, transform.quaternion, transform.scale);
        mesh.setMatrixAt(index, matrix);
      });
      mesh.instanceMatrix.needsUpdate = true;
      this.scene.add(mesh);
    };

    const boundaryRadius = Math.min(arenaSize / 2 - 28, 520);
    const panelCount = 42;
    const panelHeight = 64;
    const panelWidth = (Math.PI * 2 * boundaryRadius) / panelCount + 10;
    const panelDepth = 24;

    const innerWall = new THREE.Mesh(
      new THREE.CylinderGeometry(boundaryRadius - 12, boundaryRadius - 12, panelHeight * 0.96, 128, 1, true),
      innerWallMaterial
    );
    innerWall.position.y = (panelHeight * 0.96) / 2;
    innerWall.castShadow = true;
    innerWall.receiveShadow = true;
    this.scene.add(innerWall);

    for (let i = 0; i < panelCount; i += 1) {
      const angle = (i / panelCount) * Math.PI * 2;
      const x = Math.sin(angle) * boundaryRadius;
      const z = Math.cos(angle) * boundaryRadius;
      const target = i % 8 === 3 ? doorTransforms : wallTransforms;
      pushTransform(
        target,
        x,
        panelHeight / 2,
        z,
        -angle,
        panelWidth,
        panelHeight + (i % 5 === 0 ? 10 : 0),
        panelDepth
      );
    }
    createInstancedPanels(wallTransforms, wallMaterial);
    createInstancedPanels(doorTransforms, doorMaterial);
  }

  #scatterAssetProps(rng) {
    for (const spec of PROP_ASSETS) {
      for (let i = 0; i < spec.count; i += 1) {
        const angle = rng() * Math.PI * 2;
        const radius = spec.radius + rng() * 300;
        const sideBias = rng() < 0.58 ? Math.sign(Math.cos(angle) || 1) * (80 + rng() * 135) : 0;
        const x = Math.cos(angle) * radius + sideBias;
        const z = Math.sin(angle) * radius;
        if (Math.hypot(x, z) < 72) continue;
        const scale = spec.scale[0] + rng() * (spec.scale[1] - spec.scale[0]);
        this.#placeProp(spec, {
          position: new THREE.Vector3(x, spec.y, z),
          rotationY: rng() * Math.PI * 2,
          scale
        });
      }
    }
  }

  #featuredAssetProps() {
    const specs = Object.fromEntries(PROP_ASSETS.map((spec) => [spec.key, spec]));
    const placements = [
      ["mannequinA", -64, 0, -38, 0.35, 11],
      ["mannequinB", 72, 0, 52, -0.55, 10],
      ["barricade", 46, 0, -74, 0.92, 10],
      ["wrappedBody", -20, 0, 70, 0.2, 12],
      ["barricade", 28, 0, 108, -0.35, 12],
      ["mannequinA", -42, 0, 122, 0.78, 12],
      ["barredWindow", -108, 5, 28, Math.PI / 2, 12],
      ["wrappedBody", -34, 0, 96, -0.25, 9],
      ["dollCircle", 88, 0, 130, 0.1, 9],
      ["wire", 0, 1, 154, Math.PI / 2, 8],
      ["mirrorShards", 33, 0.08, 34, 0.45, 8]
    ];

    for (const [key, x, y, z, rotationY, scale] of placements) {
      const spec = specs[key];
      if (!spec) continue;
      this.#placeProp(spec, {
        position: new THREE.Vector3(x, y, z),
        rotationY,
        scale
      });
    }
  }

  #placeProp(spec, options) {
    const group = new THREE.Group();
    group.position.copy(options.position);
    group.rotation.y = options.rotationY;
    group.scale.setScalar(options.scale);
    group.userData.visibilityRadius = Math.max(220, options.scale * 42);
    const fallback = this.#propFallback(spec);
    group.add(fallback);
    this.scene.add(group);
    this.staticProps.push(group);

    this.#loadPropModel(spec)
      .then((model) => {
        if (!group.parent) return;
        const instance = SkeletonUtils.clone(model);
        this.#makeModelMaterialsUnique(instance);
        this.#normalizePropModel(instance);
        group.add(instance);
        fallback.visible = false;
      })
      .catch(() => {
        fallback.visible = true;
      });
  }

  #propFallback(spec) {
    const material = new THREE.MeshStandardMaterial({
      color: spec.key.includes("mannequin") ? 0xd9d3c6 : 0x312833,
      roughness: 0.94,
      metalness: 0
    });
    const height = spec.key.includes("window") ? 3.2 : spec.key.includes("wire") ? 0.45 : 1.6;
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(1.8, height, 0.35), material);
    mesh.position.y = height / 2;
    return mesh;
  }

  #loadPropModel(spec) {
    if (this.propCache.has(spec.url)) return this.propCache.get(spec.url);
    const promise = this.gltfLoader.loadAsync(spec.url).then((gltf) => {
      const root = gltf.scene;
      root.traverse((child) => {
        if (!child.isMesh) return;
        child.castShadow = false;
        child.receiveShadow = false;
        child.frustumCulled = true;
        child.geometry?.computeVertexNormals?.();
        if (child.geometry?.attributes?.normal) child.geometry.attributes.normal.needsUpdate = true;
        if (child.material) {
          const materials = Array.isArray(child.material) ? child.material : [child.material];
          for (const material of materials) {
            if (material.map) {
              material.map.colorSpace = THREE.SRGBColorSpace;
              material.map.anisotropy = Math.min(this.renderer.capabilities.getMaxAnisotropy?.() || 1, 6);
            }
            material.side = THREE.DoubleSide;
            material.needsUpdate = true;
          }
        }
      });
      this.#normalizePropModel(root);
      return root;
    });
    this.propCache.set(spec.url, promise);
    return promise;
  }

  #normalizePropModel(model) {
    model.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const maxDimension = Math.max(size.x, size.y, size.z, 0.001);
    const scale = 1 / maxDimension;
    model.scale.setScalar(scale);
    model.position.set(-center.x * scale, -box.min.y * scale, -center.z * scale);
    model.updateMatrixWorld(true);
  }

  #bloodPuddles(rng) {
    const material = new THREE.MeshBasicMaterial({
      color: 0xffffff,
      transparent: true,
      opacity: 0.42,
      depthWrite: false,
      side: THREE.DoubleSide
    });
    this.textureLoader.load(RED_PUDDLE_TEXTURE, (texture) => {
      texture.colorSpace = THREE.SRGBColorSpace;
      material.map = texture;
      material.needsUpdate = true;
    });
    for (let i = 0; i < 18; i += 1) {
      const angle = rng() * Math.PI * 2;
      const radius = 80 + rng() * 420;
      const puddle = new THREE.Mesh(new THREE.PlaneGeometry(10 + rng() * 14, 7 + rng() * 10), material);
      puddle.position.set(Math.cos(angle) * radius, 0.09, Math.sin(angle) * radius);
      puddle.rotation.x = -Math.PI / 2;
      puddle.rotation.z = rng() * Math.PI;
      this.scene.add(puddle);
    }
  }

  #groundMist(arenaSize, rng) {
    const softMist = new THREE.MeshBasicMaterial({
      color: 0x6c6272,
      transparent: true,
      opacity: 0.025,
      depthWrite: false,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending
    });
    const farMist = new THREE.MeshBasicMaterial({
      color: 0x2b2530,
      transparent: true,
      opacity: 0.026,
      depthWrite: false,
      side: THREE.DoubleSide
    });

    const baseVeil = new THREE.Mesh(new THREE.PlaneGeometry(arenaSize, arenaSize), farMist);
    baseVeil.rotation.x = -Math.PI / 2;
    baseVeil.position.y = 0.055;
    this.scene.add(baseVeil);

    for (let i = 0; i < 24; i += 1) {
      const patch = new THREE.Mesh(new THREE.CircleGeometry(22 + rng() * 34, 96), softMist);
      const angle = rng() * Math.PI * 2;
      const radius = 55 + rng() * 460;
      patch.position.set(Math.cos(angle) * radius, 0.07 + rng() * 0.04, Math.sin(angle) * radius);
      patch.rotation.x = -Math.PI / 2;
      patch.rotation.z = rng() * Math.PI;
      patch.scale.set(1.1 + rng() * 1.35, 0.65 + rng() * 0.45, 1);
      this.scene.add(patch);
    }
  }

  #updateStaticPropVisibility(selfGroup) {
    if (!selfGroup?.visible || !this.staticProps.length) return;
    this._propVisibilityFrame += 1;
    if (this._propVisibilityFrame % 6 !== 0) return;
    const origin = selfGroup.position;
    for (const prop of this.staticProps) {
      const dx = prop.position.x - origin.x;
      const dz = prop.position.z - origin.z;
      const visibleDistance = prop.userData.visibilityRadius || 260;
      prop.visible = dx * dx + dz * dz < visibleDistance * visibleDistance;
    }
  }

  #updateCharacterLod(selfGroup) {
    this._lodFrame += 1;
    if (this._lodFrame % 4 !== 0) return;
    const origin = selfGroup?.visible ? selfGroup.position : this.camera.position;
    const applyLod = (group, forceFull = false) => {
      if (!group.visible || !group.userData.modelRoot) return;
      const data = group.userData.server;
      const isGiant = data?.kind === "giant" || data?.size >= 60;
      const fullDistance = isGiant ? GIANT_FULL_MODEL_DISTANCE : CHARACTER_FULL_MODEL_DISTANCE;
      const dx = group.position.x - origin.x;
      const dz = group.position.z - origin.z;
      const useFullModel = forceFull || dx * dx + dz * dz <= fullDistance * fullDistance;
      if (group.userData.fullModelVisible === useFullModel) return;
      group.userData.fullModelVisible = useFullModel;
      group.userData.modelRoot.visible = useFullModel;
      for (const placeholder of group.userData.placeholder || []) {
        placeholder.visible = !useFullModel;
      }
    };

    for (const group of this.players.values()) applyLod(group, group === selfGroup);
    for (const group of this.enemies.values()) applyLod(group);
  }

  #syncPlayers(players, dt, inputState) {
    const seen = new Set();
    for (const data of players) {
      seen.add(data.id);
      let group = this.players.get(data.id);
      let isNew = false;
      if (!group) {
        group = this.#createCharacter("cyclopse", data.color);
        group.userData.target = new THREE.Vector3();
        group.userData.server = data;
        group.userData.visualSize = data.size;
        group.userData.shield = this.#createSpawnShield();
        group.add(group.userData.shield);
        this.players.set(data.id, group);
        this.scene.add(group);
        isNew = true;
      }
      group.visible = data.alive;
      group.userData.server = data;
      group.userData.visualSize += (data.size - group.userData.visualSize) * Math.min(1, dt * 8);
      const scale = visualScaleFromSize(group.userData.visualSize);
      group.userData.target.set(data.x, 0, data.z);
      if (isNew) {
        group.position.copy(group.userData.target);
      } else if (data.id === this.selfId && inputState && data.alive) {
        this.#predictSelf(group, inputState, dt);
        group.position.lerp(group.userData.target, 0.08);
      } else {
        group.position.lerp(group.userData.target, Math.min(1, dt * 10));
      }
      group.scale.setScalar(scale);
      group.rotation.y = this.#lerpAngle(group.rotation.y, data.yaw, Math.min(1, dt * 12));
      this.#updateSpawnShield(group, Boolean(data.protected), dt);
    }
    for (const [id, group] of this.players) {
      if (!seen.has(id)) {
        if (group.userData.mixer) this.mixers.delete(group.userData.mixer);
        this.scene.remove(group);
        this.players.delete(id);
      }
    }
  }

  #syncEnemies(enemies, dt) {
    const seen = new Set();
    for (const data of enemies) {
      seen.add(data.id);
      let mesh = this.enemies.get(data.id);
      let isNew = false;
      if (!mesh) {
        mesh = this.#createCharacter(data.kind === "giant" ? "cyclopse" : data.kind, COLORS[data.kind] || COLORS.hwacat);
        mesh.userData.target = new THREE.Vector3();
        mesh.userData.visualSize = data.size;
        this.enemies.set(data.id, mesh);
        this.scene.add(mesh);
        isNew = true;
      }
      mesh.userData.server = data;
      mesh.userData.visualSize += (data.size - mesh.userData.visualSize) * Math.min(1, dt * 8);
      const scale = visualScaleFromSize(mesh.userData.visualSize);
      mesh.userData.target.set(data.x, 0, data.z);
      if (isNew) mesh.position.copy(mesh.userData.target);
      else mesh.position.lerp(mesh.userData.target, Math.min(1, dt * 9));
      mesh.scale.setScalar(scale);
      mesh.rotation.y = this.#lerpAngle(mesh.rotation.y, data.yaw, Math.min(1, dt * 10));
    }
    for (const [id, mesh] of this.enemies) {
      if (!seen.has(id)) {
        if (mesh.userData.mixer) this.mixers.delete(mesh.userData.mixer);
        this.scene.remove(mesh);
        this.enemies.delete(id);
      }
    }
  }

  #createCharacter(assetKey, color) {
    const group = new THREE.Group();
    group.userData.assetKey = assetKey;
    const body = new THREE.Mesh(
      new THREE.SphereGeometry(3.4, 24, 16),
      new THREE.MeshLambertMaterial({
        color,
        emissive: new THREE.Color(color).multiplyScalar(0.12),
        emissiveIntensity: 0.48
      })
    );
    body.position.y = 3.4;
    const eye = new THREE.Mesh(
      new THREE.SphereGeometry(1.05, 18, 12),
      new THREE.MeshLambertMaterial({ color: 0xf5eee4, emissive: 0x21170f, emissiveIntensity: 0.45 })
    );
    const pupil = new THREE.Mesh(
      new THREE.SphereGeometry(0.38, 12, 8),
      new THREE.MeshBasicMaterial({ color: 0x090609 })
    );
    eye.position.set(0, 4.3, 2.75);
    pupil.position.set(0, 4.3, 3.55);
    const footprint = this.#createFootprintDisc(color);
    group.add(footprint, body, eye, pupil);
    group.userData.footprint = footprint;
    group.userData.placeholder = [body, eye, pupil];
    this.#attachAssetModel(group, assetKey, color);
    return group;
  }

  #createFootprintDisc(color) {
    const disc = new THREE.Group();
    const shadow = new THREE.Mesh(
      new THREE.CircleGeometry(BASE_VISUAL_DIAMETER / 2, 64),
      new THREE.MeshBasicMaterial({
        color: 0xffffff,
        transparent: true,
        opacity: 0.08,
        depthWrite: false
      })
    );
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = 0.025;
    const rim = new THREE.Mesh(
      new THREE.TorusGeometry(BASE_VISUAL_DIAMETER / 2, 0.055, 8, 64),
      new THREE.MeshBasicMaterial({
        color: 0xffffff,
        transparent: true,
        opacity: 0.54,
        depthWrite: false
      })
    );
    rim.rotation.x = Math.PI / 2;
    rim.position.y = 0.045;
    disc.add(shadow, rim);
    disc.userData.shadow = shadow;
    disc.userData.rim = rim;
    disc.userData.baseColor = color;
    return disc;
  }

  #createFlashlightVisuals() {
    const group = new THREE.Group();
    group.visible = false;
    group.userData.visibility = 0;
    return group;
  }

  #createSpawnShield() {
    const shield = new THREE.Group();
    const dome = new THREE.Mesh(
      new THREE.SphereGeometry(6.6, 32, 16, 0, Math.PI * 2, 0, Math.PI * 0.54),
      new THREE.MeshBasicMaterial({
        color: 0x89d8ff,
        transparent: true,
        opacity: 0.18,
        side: THREE.DoubleSide,
        depthWrite: false
      })
    );
    dome.position.y = 0.35;
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(4.9, 0.12, 10, 48),
      new THREE.MeshBasicMaterial({
        color: 0xc8f3ff,
        transparent: true,
        opacity: 0.68,
        depthWrite: false
      })
    );
    ring.rotation.x = Math.PI / 2;
    ring.position.y = 0.18;
    shield.add(dome, ring);
    shield.visible = false;
    shield.userData.dome = dome;
    shield.userData.ring = ring;
    shield.userData.pulse = 0;
    return shield;
  }

  #updateSpawnShield(group, active, dt) {
    const shield = group.userData.shield;
    if (!shield) return;
    shield.visible = active;
    if (!active) return;
    shield.userData.pulse += dt * 4.5;
    const pulse = 1 + Math.sin(shield.userData.pulse) * 0.045;
    shield.scale.setScalar(pulse);
    shield.rotation.y += dt * 1.6;
    shield.userData.dome.material.opacity = 0.15 + Math.sin(shield.userData.pulse * 1.7) * 0.035;
    shield.userData.ring.material.opacity = 0.58 + Math.sin(shield.userData.pulse * 2.1) * 0.16;
  }

  #updateFootprintTargets(snapshot) {
    const selfData = snapshot.players.find((player) => player.id === this.selfId && player.alive);
    for (const [id, group] of this.players) {
      const data = group.userData.server;
      if (id === this.selfId) {
        this.#setFootprintStyle(group, "hidden");
      } else if (selfData && data?.alive && selfData.size > data.size + 1) {
        this.#setFootprintStyle(group, "eatable");
      } else {
        this.#setFootprintStyle(group, "hidden");
      }
    }

    for (const enemy of this.enemies.values()) {
      const data = enemy.userData.server;
      if (selfData && data && selfData.size > data.size) {
        this.#setFootprintStyle(enemy, "eatable");
      } else {
        this.#setFootprintStyle(enemy, "hidden");
      }
    }
  }

  #setFootprintStyle(group, mode) {
    const footprint = group.userData.footprint;
    if (!footprint) return;
    const rim = footprint.userData.rim;
    const shadow = footprint.userData.shadow;
    if (mode === "hidden") {
      footprint.visible = false;
      return;
    }
    footprint.visible = true;
    if (mode === "eatable") {
      rim.material.color.set(0x5cff7a);
      rim.material.opacity = 0.82;
      shadow.material.color.set(0x42ff66);
      shadow.material.opacity = 0.16;
      return;
    }
    rim.material.color.set(0xffffff);
    rim.material.opacity = 0.42;
    shadow.material.color.set(0xffffff);
    shadow.material.opacity = 0.075;
  }

  #updateFlashlight(selfGroup, inputState, dt) {
    const active = Boolean(inputState?.flashlightOn && selfGroup?.userData.server?.alive);
    const target = active ? 1 : 0;
    const current = this.flashlightVisuals.userData.visibility || 0;
    const visibility = current + (target - current) * Math.min(1, dt * 10);
    this.flashlightVisuals.userData.visibility = visibility;
    this.flashlightVisuals.visible = visibility > 0.01;

    this.headLamp.intensity += ((active ? 1500 : 0) - this.headLamp.intensity) * Math.min(1, dt * 12);
    this.fillLamp.intensity += ((active ? 14 : 0) - this.fillLamp.intensity) * Math.min(1, dt * 10);
    this.flashlightAura.intensity += ((active ? 38 : 0) - this.flashlightAura.intensity) * Math.min(1, dt * 10);

    if (!selfGroup) return;
    const yaw = inputState?.yaw ?? selfGroup.rotation.y;
    const position = selfGroup.position;
    const visualScale = visualScaleFromSize(selfGroup.userData.visualSize || PLAYER_BASE_SIZE);
    const source = new THREE.Vector3(position.x, 4.6 + visualScale * 2.1, position.z);
    const forward = new THREE.Vector3(-Math.sin(yaw), -0.17, -Math.cos(yaw)).normalize();
    source.addScaledVector(forward, Math.max(2.2, visualScale * 1.2));
    const targetPosition = source.clone().addScaledVector(forward, 240);

    this.headLamp.position.copy(source);
    this.headLamp.target.position.copy(targetPosition);
    this.headLamp.target.updateMatrixWorld();
    this.fillLamp.position.copy(source).add(new THREE.Vector3(0, 1.3, 0));
    this.flashlightAura.position.copy(source);
    this.flashlightVisuals.position.set(position.x, 0, position.z);
    this.flashlightVisuals.rotation.y = yaw + Math.PI;
  }

  #updateCamera(selfGroup, inputState, dt) {
    const self = selfGroup?.userData.server;
    if (!selfGroup || !self?.alive) {
      this.camera.position.lerp(new THREE.Vector3(0, 160, 180), 0.05);
      this.camera.lookAt(0, 0, 0);
      this.cameraLightRig.position.copy(this.camera.position);
      this.cameraLightRig.quaternion.copy(this.camera.quaternion);
      return;
    }
    const scale = visualScaleFromSize(selfGroup.userData.visualSize);
    const yaw = inputState?.yaw ?? self.yaw;
    const pitch = inputState?.pitch ?? (18 * Math.PI / 180);
    const zoom = Math.max(0.35, Math.min(2.8, inputState?.cameraZoom ?? 1));
    const target = selfGroup.position.clone().add(new THREE.Vector3(0, 4 + scale * 2.4, 0));
    const distance = (34 + scale * 9.5) * zoom;
    const horizontalDistance = Math.cos(pitch) * distance;
    const desired = new THREE.Vector3(
      target.x + Math.sin(yaw) * horizontalDistance,
      target.y + Math.sin(pitch) * distance,
      target.z + Math.cos(yaw) * horizontalDistance
    );
    const followFactor = 1 - Math.exp(-10 * dt);
    this.camera.position.lerp(desired, followFactor);
    this.camera.lookAt(target);
    this.cameraLightRig.position.copy(this.camera.position);
    this.cameraLightRig.quaternion.copy(this.camera.quaternion);
  }

  #predictSelf(group, inputState, dt) {
    let forward = 0;
    let strafe = 0;
    if (inputState.up) forward += 1;
    if (inputState.down) forward -= 1;
    if (inputState.right) strafe += 1;
    if (inputState.left) strafe -= 1;
    const length = Math.hypot(forward, strafe);
    if (!length) return;
    forward /= length;
    strafe /= length;
    const speed = inputState.sprint ? 70 : 44;
    const sin = Math.sin(inputState.yaw);
    const cos = Math.cos(inputState.yaw);
    const moveX = -sin * forward + cos * strafe;
    const moveZ = -cos * forward - sin * strafe;
    group.position.x += moveX * speed * dt;
    group.position.z += moveZ * speed * dt;
    group.rotation.y = Math.atan2(moveX, moveZ);
  }

  #lerpAngle(from, to, amount) {
    const delta = Math.atan2(Math.sin(to - from), Math.cos(to - from));
    return from + delta * amount;
  }

  #attachAssetModel(group, assetKey, fallbackColor) {
    const spec = CHARACTER_ASSETS[assetKey] || CHARACTER_ASSETS.hwacat;
    this.#loadCharacterModel(assetKey, spec, fallbackColor)
      .then(({ model, animations }) => {
        if (!group.parent) return;
        const instance = SkeletonUtils.clone(model);
        instance.userData.isAssetModel = true;
        this.#makeModelMaterialsUnique(instance);
        group.add(instance);
        group.userData.modelRoot = instance;
        group.userData.groundAnchors = this.#collectGroundAnchors(instance);
        for (const placeholder of group.userData.placeholder || []) placeholder.visible = false;
        this.#snapCharacterModelToGround(group, true);
        if (animations.length) {
          const mixer = new THREE.AnimationMixer(instance);
          mixer.clipAction(animations[0]).play();
          mixer.userData = { group };
          group.userData.mixer = mixer;
          this.mixers.add(mixer);
        }
      })
      .catch(() => {
        for (const placeholder of group.userData.placeholder || []) placeholder.visible = true;
      });
  }

  #loadCharacterModel(assetKey, spec, fallbackColor) {
    if (this.modelCache.has(assetKey)) return this.modelCache.get(assetKey);
    const promise = this.#loadCharacterModelNow(spec, fallbackColor);
    this.modelCache.set(assetKey, promise);
    return promise;
  }

  async #loadCharacterModelNow(spec, fallbackColor) {
    const fbx = await this.fbxLoader.loadAsync(spec.model);
    let texture = null;
    if (spec.texture) {
      try {
        texture = await this.textureLoader.loadAsync(spec.texture);
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.flipY = true;
        texture.anisotropy = Math.min(this.renderer.capabilities.getMaxAnisotropy?.() || 1, 6);
        texture.needsUpdate = true;
      } catch {
        texture = null;
      }
    }
    fbx.traverse((child) => {
      if (!child.isMesh && !child.isSkinnedMesh) return;
      child.castShadow = false;
      child.receiveShadow = false;
      child.frustumCulled = true;
      child.geometry?.computeVertexNormals?.();
      if (child.geometry?.attributes?.normal) child.geometry.attributes.normal.needsUpdate = true;
      child.material = new THREE.MeshLambertMaterial({
        color: texture ? 0xffffff : fallbackColor,
        map: texture,
        emissive: texture ? 0xffffff : fallbackColor,
        emissiveMap: texture,
        emissiveIntensity: texture ? 0.24 : 0.16,
        side: THREE.DoubleSide
      });
      child.material.needsUpdate = true;
    });
    this.#normalizeModel(fbx, spec.visualDiameter || BASE_VISUAL_DIAMETER);
    return {
      model: fbx,
      animations: this.#prepareLoopingAnimations(fbx.animations || [], {
        lockRootVerticalMotion: Boolean(spec.lockRootVerticalMotion)
      })
    };
  }

  #normalizeModel(model, targetDiameter) {
    model.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(model);
    const size = new THREE.Vector3();
    const center = new THREE.Vector3();
    box.getSize(size);
    box.getCenter(center);
    const horizontalFootprint = Math.max(size.x, size.z, 0.001);
    const scale = targetDiameter / horizontalFootprint;
    model.scale.setScalar(scale);
    model.position.set(-center.x * scale, -box.min.y * scale, -center.z * scale);
    model.updateMatrixWorld(true);
    model.userData.normalizedVisualDiameter = targetDiameter;
  }

  #updateMixers(dt, selfGroup = null) {
    const origin = selfGroup?.position || this.camera.position;
    for (const mixer of this.mixers) {
      const group = mixer.userData?.group;
      if (group && !group.visible) continue;
      if (group?.userData?.modelRoot && group.userData.modelRoot.visible === false) continue;
      if (group && origin) {
        const dx = group.position.x - origin.x;
        const dz = group.position.z - origin.z;
        if (dx * dx + dz * dz > 330 * 330 && this._groundFrame % 3 !== 0) continue;
      }
      mixer.update(dt);
    }
  }

  #updateRenderQuality(dt) {
    this._frameTimeAverage += (dt - this._frameTimeAverage) * 0.05;
    this._qualityTimer += dt;
    if (this._qualityTimer < 1.2) return;
    this._qualityTimer = 0;

    const fps = 1 / Math.max(this._frameTimeAverage, 0.001);
    const nativeRatio = Math.min(window.devicePixelRatio || 1, MAX_RENDER_PIXEL_RATIO);
    if (fps < 42 && this.targetPixelRatio > MIN_RENDER_PIXEL_RATIO) {
      this.targetPixelRatio = Math.max(MIN_RENDER_PIXEL_RATIO, this.targetPixelRatio - 0.15);
    } else if (fps > 56 && this.targetPixelRatio < nativeRatio) {
      this.targetPixelRatio = Math.min(nativeRatio, this.targetPixelRatio + 0.1);
    }

    if (Math.abs(this.currentPixelRatio - this.targetPixelRatio) < 0.04) return;
    this.currentPixelRatio += (this.targetPixelRatio - this.currentPixelRatio) * 0.7;
    this.renderer.setPixelRatio(this.currentPixelRatio);
    this.resize();
  }

  #snapVisibleModelsToGround(selfGroup = null) {
    this._groundFrame += 1;
    if (selfGroup?.visible) this.#snapCharacterModelToGround(selfGroup);
    if (this._groundFrame % 4 !== 0) return;
    for (const group of this.enemies.values()) {
      if (group.visible) this.#snapCharacterModelToGround(group);
    }
    for (const group of this.players.values()) {
      if (group !== selfGroup && group.visible) this.#snapCharacterModelToGround(group);
    }
  }

  #collectGroundAnchors(modelRoot) {
    const preferred = [];
    const fallback = [];
    modelRoot.traverse((child) => {
      if (!child.isBone) return;
      const normalized = child.name.toLowerCase().replace(/[^a-z0-9]/g, "");
      if (/leftfoot|rightfoot|lefttoebase|righttoebase/.test(normalized)) {
        preferred.push(child);
        return;
      }
      if (/foot|toe|ankle/.test(normalized)) fallback.push(child);
    });
    return preferred.length ? preferred : fallback;
  }

  #makeModelMaterialsUnique(modelRoot) {
    modelRoot.traverse((child) => {
      if (!child.material) return;
      child.material = Array.isArray(child.material)
        ? child.material.map((material) => material.clone())
        : child.material.clone();
    });
  }

  #getLowestFootWorldY(bones) {
    if (!bones?.length) return null;
    let minY = Infinity;
    for (const bone of bones) {
      bone.getWorldPosition(this._footProbe);
      minY = Math.min(minY, this._footProbe.y);
    }
    return Number.isFinite(minY) ? minY : null;
  }

  #snapCharacterModelToGround(group, force = false) {
    const modelRoot = group.userData.modelRoot;
    if (!modelRoot) return;
    const scaleY = Math.max(group.scale.y, 0.001);
    const groundY = group.position.y;
    const anchors = group.userData.groundAnchors;
    const footY = anchors?.length ? this.#getLowestFootWorldY(anchors) : null;

    if (footY !== null) {
      const offset = groundY - footY;
      if (force || Math.abs(offset) > 0.02) {
        modelRoot.position.y += offset / scaleY;
      }
      return;
    }

    if (!force) return;
    modelRoot.updateMatrixWorld(true);
    const bounds = new THREE.Box3().setFromObject(modelRoot);
    if (!Number.isFinite(bounds.min.y)) return;
    modelRoot.position.y += (groundY - bounds.min.y) / scaleY;
  }

  #prepareLoopingAnimations(animations, options = {}) {
    return animations.map((clip) => {
      const clonedClip = clip.clone();
      clonedClip.tracks = clonedClip.tracks.map((track) => {
        if (!isRootPositionTrack(track)) return track;
        const nextTrack = options.lockRootVerticalMotion ? lockRootVerticalMotion(track) : track;
        return shouldRemoveRootPositionDrift(nextTrack) ? removeRootPositionDrift(nextTrack) : nextTrack;
      });
      clonedClip.optimize();
      return clonedClip;
    });
  }

  getVisualCalibration() {
    const rows = [];
    const entries = [
      ...[...this.players, ...this.enemies].map(([id, group]) => ({ id, group })),
      ...this.calibrationGroups.map((group, index) => ({ id: `calibration-${index}`, group }))
    ];
    for (const { id, group } of entries) {
      const modelRoot = group.userData.modelRoot;
      if (!modelRoot || !group.visible) continue;
      group.updateMatrixWorld(true);
      const bounds = new THREE.Box3().setFromObject(modelRoot);
      const size = bounds.getSize(new THREE.Vector3());
      const maxDimension = Math.max(size.x, size.y, size.z);
      const horizontalFootprint = Math.max(size.x, size.z);
      rows.push({
        id,
        kind: group.userData.assetKey,
        internalSize: group.userData.server?.size,
        visualScale: group.scale.x,
        maxDimension: Math.round(maxDimension * 100) / 100,
        horizontalFootprint: Math.round(horizontalFootprint * 100) / 100,
        expectedFootprint: Math.round(BASE_VISUAL_DIAMETER * group.scale.x * 100) / 100,
        height: Math.round(size.y * 100) / 100,
        width: Math.round(Math.max(size.x, size.z) * 100) / 100
      });
    }
    return rows;
  }

  createCalibrationLineup(size = PLAYER_BASE_SIZE) {
    for (const group of this.calibrationGroups) {
      this.scene.remove(group);
    }
    this.calibrationGroups = [];
    const entries = ["cyclopse", "hwacat", "uncat", "angry"];
    for (let index = 0; index < entries.length; index += 1) {
      const kind = entries[index];
      const group = this.#createCharacter(kind, COLORS[kind] || 0xffffff);
      group.position.set((index - 1.5) * 18, 0, -34);
      group.scale.setScalar(visualScaleFromSize(size));
      group.userData.server = { size };
      group.userData.visualSize = size;
      group.userData.target = group.position.clone();
      const ring = new THREE.Mesh(
        new THREE.TorusGeometry(BASE_VISUAL_DIAMETER / 2, 0.08, 8, 64),
        new THREE.MeshBasicMaterial({ color: 0x65ff9a, transparent: true, opacity: 0.9 })
      );
      ring.rotation.x = Math.PI / 2;
      ring.position.y = 0.04;
      group.add(ring);
      this.scene.add(group);
      this.calibrationGroups.push(group);
    }
    this.camera.position.set(0, 18, 34);
    this.camera.lookAt(0, 4, -34);
    return `Spawned calibration lineup at size ${size}`;
  }
}

function visualScaleFromSize(size) {
  return Math.max(MIN_VISUAL_SCALE, size / PLAYER_BASE_SIZE);
}

function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return state / 4294967296;
  };
}

function isRootPositionTrack(track) {
  if (!track.name.endsWith(".position") || track.getValueSize() !== 3) return false;
  const targetName = track.name.split(".")[0].toLowerCase();
  return /(hips|pelvis|root|armature)/.test(targetName);
}

function shouldRemoveRootPositionDrift(track) {
  if (!isRootPositionTrack(track)) return false;
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
