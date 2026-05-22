import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
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
const SURVIVAL_FOG_COLOR = 0x09080b;
const SURVIVAL_FOG_DENSITY = 0.0115;

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
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.24;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(SURVIVAL_FOG_COLOR);
    this.scene.fog = new THREE.FogExp2(SURVIVAL_FOG_COLOR, SURVIVAL_FOG_DENSITY);

    this.camera = new THREE.PerspectiveCamera(64, 1, 0.1, 1800);
    this.cameraLightRig = new THREE.Group();
    this.headLamp = new THREE.SpotLight(0xffe2b8, 0, 260, Math.PI / 5.4, 0.62, 1.1);
    this.fillLamp = new THREE.PointLight(0x9eb8ff, 0, 150, 1.4);
    this.flashlightAura = new THREE.PointLight(0xffd6a0, 0, 88, 1.65);
    this.flashlightVisuals = this.#createFlashlightVisuals();
    this.players = new Map();
    this.enemies = new Map();
    this.selfId = null;
    this.clock = new THREE.Clock();
    this.fbxLoader = new FBXLoader();
    this.textureLoader = new THREE.TextureLoader();
    this.modelCache = new Map();
    this.mixers = new Set();
    this.calibrationGroups = [];
    this._footProbe = new THREE.Vector3();
    this._groundFrame = 0;

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
    this.#updateMixers(dt);
    this.#snapVisibleModelsToGround(self);
    this.renderer.render(this.scene, this.camera);
  }

  #lights() {
    this.scene.add(new THREE.HemisphereLight(0x675f76, 0x1b1720, 1.2));
    this.scene.add(new THREE.AmbientLight(0x1c1821, 2.15));
    const moon = new THREE.DirectionalLight(0x6b627d, 1.55);
    moon.position.set(-130, 210, -110);
    this.scene.add(moon);

    const side = new THREE.DirectionalLight(0x5a3f48, 0.58);
    side.position.set(150, 70, 120);
    this.scene.add(side);
  }

  #cameraLights() {
    this.headLamp.position.set(0, 0, 0);
    this.headLamp.target.position.set(0, 0, -1);
    this.fillLamp.position.set(0, -4, -10);
    this.cameraLightRig.add(this.headLamp, this.headLamp.target, this.fillLamp);
    this.scene.add(this.cameraLightRig);
  }

  #arena() {
    const arenaSize = 1100;
    const tileSize = 16;
    const floorMaterial = new THREE.MeshStandardMaterial({
      color: 0xffffff,
      roughness: 0.95,
      metalness: 0.0,
      emissive: 0xb8a08d,
      emissiveIntensity: 0.48
    });
    this.textureLoader.load(
      FLOOR_TEXTURE,
      (texture) => {
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.wrapS = THREE.RepeatWrapping;
        texture.wrapT = THREE.RepeatWrapping;
        texture.anisotropy = this.renderer.capabilities.getMaxAnisotropy?.() || 1;
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
    this.scene.add(floor);
    this.#groundMist(arenaSize);

    const ring = new THREE.Mesh(
      new THREE.RingGeometry(520, 526, 128),
      new THREE.MeshBasicMaterial({ color: 0xb64c5a, side: THREE.DoubleSide })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.2;
    this.scene.add(ring);

    const silhouetteMaterial = new THREE.MeshStandardMaterial({
      color: 0x141218,
      roughness: 0.97,
      metalness: 0.0,
      transparent: true,
      opacity: 0.9
    });
    for (let lane = -1; lane <= 1; lane += 2) {
      for (let i = 0; i < 28; i += 1) {
        const silhouette = new THREE.Mesh(new THREE.BoxGeometry(4, 26, 18), silhouetteMaterial);
        const sideShift = lane * (80 + (i % 3) * 14);
        silhouette.position.set(sideShift, 11, (i - 14) * 38 + (lane * 6));
        silhouette.rotation.y = (lane * 4 * Math.PI) / 180;
        this.scene.add(silhouette);
      }
    }

    const propMaterialA = new THREE.MeshStandardMaterial({ color: 0x1e1a22, roughness: 0.95 });
    const propMaterialB = new THREE.MeshStandardMaterial({ color: 0x322a36, roughness: 0.95 });
    for (let i = 0; i < 60; i += 1) {
      const prop = new THREE.Mesh(
        new THREE.BoxGeometry(1, 1, 1),
        i % 5 === 0 ? propMaterialB : propMaterialA
      );
      const angle = Math.random() * Math.PI * 2;
      const radius = 140 + Math.random() * 340;
      prop.position.set(Math.cos(angle) * radius, 4, Math.sin(angle) * radius);
      prop.scale.set(3 + Math.random() * 9, 7 + Math.random() * 22, 3 + Math.random() * 9);
      prop.rotation.y = Math.random() * Math.PI;
      this.scene.add(prop);
    }
  }

  #groundMist(arenaSize) {
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
      const patch = new THREE.Mesh(new THREE.CircleGeometry(22 + Math.random() * 34, 96), softMist);
      const angle = Math.random() * Math.PI * 2;
      const radius = 55 + Math.random() * 460;
      patch.position.set(Math.cos(angle) * radius, 0.07 + Math.random() * 0.04, Math.sin(angle) * radius);
      patch.rotation.x = -Math.PI / 2;
      patch.rotation.z = Math.random() * Math.PI;
      patch.scale.set(1.1 + Math.random() * 1.35, 0.65 + Math.random() * 0.45, 1);
      this.scene.add(patch);
    }
  }

  #syncPlayers(players, dt, inputState) {
    const seen = new Set();
    for (const data of players) {
      seen.add(data.id);
      let group = this.players.get(data.id);
      if (!group) {
        group = this.#createCharacter("cyclopse", data.color);
        group.userData.target = new THREE.Vector3();
        group.userData.server = data;
        group.userData.visualSize = data.size;
        group.userData.shield = this.#createSpawnShield();
        group.add(group.userData.shield);
        this.players.set(data.id, group);
        this.scene.add(group);
      }
      group.visible = data.alive;
      group.userData.server = data;
      group.userData.visualSize += (data.size - group.userData.visualSize) * Math.min(1, dt * 8);
      const scale = visualScaleFromSize(group.userData.visualSize);
      group.userData.target.set(data.x, 0, data.z);
      if (data.id === this.selfId && inputState && data.alive) {
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
      if (!mesh) {
        mesh = this.#createCharacter(data.kind === "giant" ? "cyclopse" : data.kind, COLORS[data.kind] || COLORS.hwacat);
        mesh.userData.target = new THREE.Vector3();
        mesh.userData.visualSize = data.size;
        this.enemies.set(data.id, mesh);
        this.scene.add(mesh);
      }
      mesh.userData.server = data;
      mesh.userData.visualSize += (data.size - mesh.userData.visualSize) * Math.min(1, dt * 8);
      const scale = visualScaleFromSize(mesh.userData.visualSize);
      mesh.userData.target.set(data.x, 0, data.z);
      mesh.position.lerp(mesh.userData.target, Math.min(1, dt * 9));
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
      new THREE.MeshStandardMaterial({ color, roughness: 0.58, emissive: new THREE.Color(color).multiplyScalar(0.18) })
    );
    body.position.y = 3.4;
    const eye = new THREE.Mesh(
      new THREE.SphereGeometry(1.05, 18, 12),
      new THREE.MeshStandardMaterial({ color: 0xf5eee4, emissive: 0x332211, roughness: 0.35 })
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

    const aura = new THREE.Mesh(
      new THREE.CircleGeometry(28, 80),
      new THREE.MeshBasicMaterial({
        color: 0xffd9a1,
        transparent: true,
        opacity: 0,
        depthWrite: false,
        side: THREE.DoubleSide,
        blending: THREE.AdditiveBlending
      })
    );
    aura.rotation.x = -Math.PI / 2;
    aura.position.y = 0.12;

    const length = 92;
    const width = 38;
    const coneShape = new THREE.Shape();
    coneShape.moveTo(0, 0);
    coneShape.lineTo(-width, -length);
    coneShape.quadraticCurveTo(0, -length - 18, width, -length);
    coneShape.lineTo(0, 0);
    const cone = new THREE.Mesh(
      new THREE.ShapeGeometry(coneShape),
      new THREE.MeshBasicMaterial({
        color: 0xffe4ad,
        transparent: true,
        opacity: 0,
        depthWrite: false,
        side: THREE.DoubleSide,
        blending: THREE.AdditiveBlending
      })
    );
    cone.rotation.x = -Math.PI / 2;
    cone.position.y = 0.135;

    group.add(aura, cone);
    group.userData.aura = aura;
    group.userData.cone = cone;
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

    this.headLamp.intensity += ((active ? 8.4 : 0) - this.headLamp.intensity) * Math.min(1, dt * 12);
    this.fillLamp.intensity += ((active ? 0.75 : 0) - this.fillLamp.intensity) * Math.min(1, dt * 10);
    this.flashlightAura.intensity += ((active ? 2.35 : 0) - this.flashlightAura.intensity) * Math.min(1, dt * 10);

    if (!selfGroup) return;
    const yaw = inputState?.yaw ?? selfGroup.rotation.y;
    const position = selfGroup.position;
    this.flashlightAura.position.set(position.x, 7.5, position.z);
    this.flashlightVisuals.position.set(position.x, 0, position.z);
    this.flashlightVisuals.rotation.y = yaw + Math.PI;
    this.flashlightVisuals.userData.aura.material.opacity = 0.18 * visibility;
    this.flashlightVisuals.userData.cone.material.opacity = 0.23 * visibility;
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
        group.add(instance);
        group.userData.modelRoot = instance;
        group.userData.groundAnchors = this.#collectGroundAnchors(instance);
        for (const placeholder of group.userData.placeholder || []) placeholder.visible = false;
        this.#snapCharacterModelToGround(group, true);
        if (animations.length) {
          const mixer = new THREE.AnimationMixer(instance);
          mixer.clipAction(animations[0]).play();
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
        texture.needsUpdate = true;
      } catch {
        texture = null;
      }
    }
    fbx.traverse((child) => {
      if (!child.isMesh && !child.isSkinnedMesh) return;
      child.castShadow = false;
      child.receiveShadow = false;
      child.frustumCulled = false;
      child.material = new THREE.MeshBasicMaterial({
        color: texture ? 0xffffff : fallbackColor,
        map: texture,
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

  #updateMixers(dt) {
    for (const mixer of this.mixers) mixer.update(dt);
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
