// Happy Toy의 첫 번째 테스트 맵을 생성하는 모듈입니다.
// 벽, 바닥, 천장, 문, 장난감 소품을 만들고 CollisionWorld에 충돌체를 등록합니다.

import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { MAP_CONFIG, WORLD_CONFIG } from "../config/gameConfig.js";
import { Cabinet } from "./Cabinet.js";
import { Door } from "./Door.js";
import { FinalExit } from "./FinalExit.js";
import { KeyItem } from "./KeyItem.js";
import { TextureLibrary } from "./TextureLibrary.js";

export class MapBuilder {
  constructor(scene, collisionWorld, options = {}) {
    this.scene = scene;
    this.collisionWorld = collisionWorld;
    this.mapConfig = options.mapConfig || MAP_CONFIG;
    this.debugEnabled = options.debugEnabled ?? false;
    this.doors = [];
    this.keys = [];
    this.cabinets = [];
    this.finalExit = null;
    this.textures = new TextureLibrary();
    this.gltfLoader = new GLTFLoader();
    this.textureLoader = new THREE.TextureLoader();
    this.pendingAssets = [];
  }

  build() {
    this.registerNavigationSurfaces();
    this.createRoomShell();
    this.createCeilingPanels();
    this.createStairways();
    this.createWalls();
    this.createDoors();
    this.createKeys();
    this.createCabinets();
    this.createFinalExit();
    this.createProps();
    if (this.debugEnabled) {
      this.createDebugOverlays();
    }
    return {
      doors: this.doors,
      keys: this.keys,
      cabinets: this.cabinets,
      finalExit: this.finalExit,
      playerStart: new THREE.Vector3(...this.mapConfig.playerStart),
      pendingAssets: this.pendingAssets,
    };
  }

  registerNavigationSurfaces() {
    for (const area of this.mapConfig.floorAreas || []) {
      this.collisionWorld.addFloorArea(area);
    }
    for (const landing of this.mapConfig.landingAreas || []) {
      this.collisionWorld.addLandingArea(landing);
    }
    for (const room of this.mapConfig.roomAreas || []) {
      this.collisionWorld.addRoomArea(room);
    }
    for (const blocked of this.mapConfig.blockedAreas || []) {
      this.collisionWorld.addBlockedArea(blocked);
    }
    for (const voidArea of this.mapConfig.voidAreas || []) {
      this.collisionWorld.addVoidArea(voidArea);
    }
    for (const dropZone of this.mapConfig.dropZones || []) {
      this.collisionWorld.addDropZone(dropZone);
    }
    for (const ramp of this.mapConfig.ramps || []) {
      this.collisionWorld.addRamp(ramp);
    }
    for (const waypoint of this.mapConfig.transitionWaypoints || []) {
      this.collisionWorld.addTransitionWaypoint(waypoint);
    }
  }

  createRoomShell() {
    for (const panel of this.mapConfig.floorPanels || []) {
      const slabThickness = panel.slabThickness ?? 0;
      const floorGeometry = slabThickness > 0
        ? new THREE.BoxGeometry(panel.size[0], slabThickness, panel.size[1])
        : new THREE.PlaneGeometry(...panel.size);
      const floorMaterial = this.textures.createFloorMaterial(panel.size[0], panel.size[1]);
      const floor = new THREE.Mesh(
        floorGeometry,
        floorMaterial,
      );
      floor.name = panel.id;
      if (slabThickness > 0) {
        floor.position.set(panel.position[0], panel.y - slabThickness / 2, panel.position[2]);
        floor.castShadow = true;
      } else {
        floor.rotation.x = -Math.PI / 2;
        floor.position.set(...panel.position);
      }
      floor.receiveShadow = true;
      this.scene.add(floor);

      if (panel.ceiling === false) {
        continue;
      }

      const ceiling = new THREE.Mesh(
        new THREE.PlaneGeometry(...panel.size),
        this.textures.createCeilingMaterial(panel.size[0], panel.size[1]),
      );
      ceiling.name = `${panel.id}-ceiling`;
      ceiling.rotation.x = Math.PI / 2;
      ceiling.position.set(panel.position[0], panel.y + 3, panel.position[2]);
      ceiling.receiveShadow = true;
      this.scene.add(ceiling);
    }
  }

  createCeilingPanels() {
    for (const panel of this.mapConfig.ceilingPanels || []) {
      const material = this.textures.createCeilingMaterial(panel.size[0], panel.size[2]);
      const ceiling = new THREE.Mesh(
        new THREE.BoxGeometry(panel.size[0], panel.size[1], panel.size[2]),
        material,
      );
      ceiling.name = panel.id;
      ceiling.position.set(...panel.position);
      ceiling.receiveShadow = true;
      this.scene.add(ceiling);
    }
  }

  createStairways() {
    const stepMaterial = this.textures.createStairMaterial();
    const landingMaterial = this.textures.createStairMaterial();
    const riserMaterial = this.textures.createStairMaterial();
    const railMaterial = new THREE.MeshStandardMaterial({ color: 0x111510, roughness: 0.85 });

    for (const stair of this.mapConfig.stairways || []) {
      if (stair.landing) {
        const landingWidth = stair.landing.maxX - stair.landing.minX;
        const landingDepth = stair.landing.maxZ - stair.landing.minZ;
        const landing = new THREE.Mesh(
          new THREE.BoxGeometry(landingWidth, 0.14, landingDepth),
          landingMaterial,
        );
        landing.name = `${stair.id}-bottom-landing`;
        landing.position.set(
          (stair.landing.minX + stair.landing.maxX) / 2,
          stair.startY + 0.07,
          (stair.landing.minZ + stair.landing.maxZ) / 2,
        );
        landing.castShadow = true;
        landing.receiveShadow = true;
        this.scene.add(landing);
      }

      const width = stair.maxX - stair.minX;
      const depth = (stair.maxZ - stair.minZ) / stair.steps;
      const rise = (stair.endY - stair.startY) / stair.steps;
      for (let index = 0; index < stair.steps; index += 1) {
        const topY = stair.startY + rise * (index + 1);
        const height = Math.max(0.08, topY - stair.startY);
        const z = stair.maxZ - depth * (index + 0.5);
        const step = new THREE.Mesh(
          new THREE.BoxGeometry(width, height, depth * 0.96),
          stepMaterial,
        );
        step.name = `${stair.id}-step-${index}`;
        step.position.set((stair.minX + stair.maxX) / 2, stair.startY + height / 2, z);
        step.castShadow = true;
        step.receiveShadow = true;
        this.scene.add(step);

        const riser = new THREE.Mesh(
          new THREE.BoxGeometry(width + 0.04, Math.max(0.08, rise), 0.045),
          riserMaterial,
        );
        riser.name = `${stair.id}-riser-${index}`;
        riser.position.set((stair.minX + stair.maxX) / 2, topY - rise / 2, z - depth / 2 + 0.02);
        riser.receiveShadow = true;
        this.scene.add(riser);
      }

      const railLength = stair.maxZ - stair.minZ;
      for (const sideX of [stair.minX + 0.18, stair.maxX - 0.18]) {
        const rail = new THREE.Mesh(
          new THREE.BoxGeometry(0.18, 0.14, railLength),
          railMaterial,
        );
        rail.name = `${stair.id}-wall-handrail-${sideX.toFixed(1)}`;
        rail.position.set(sideX, (stair.startY + stair.endY) / 2 + 1.05, (stair.minZ + stair.maxZ) / 2);
        rail.rotation.x = -Math.atan2(stair.endY - stair.startY, railLength);
        rail.castShadow = true;
        this.scene.add(rail);

        if (stair.railMode === "wall-handrail") {
          continue;
        }

        const posts = Math.ceil(stair.steps / 3);
        for (let postIndex = 0; postIndex <= posts; postIndex += 1) {
          const progress = postIndex / posts;
          const z = stair.maxZ + (stair.minZ - stair.maxZ) * progress;
          const y = stair.startY + (stair.endY - stair.startY) * progress;
          const post = new THREE.Mesh(
            new THREE.BoxGeometry(0.16, 1.1, 0.16),
            railMaterial,
          );
          post.name = `${stair.id}-post-${sideX.toFixed(1)}-${postIndex}`;
          post.position.set(sideX, y + 0.55, z);
          post.castShadow = true;
          this.scene.add(post);
        }
      }
    }
  }

  createWalls() {
    const wallMaterial = this.textures.createWallMaterial();
    const trimMaterial = new THREE.MeshStandardMaterial({
      color: WORLD_CONFIG.trimColor,
      roughness: 0.8,
    });

    for (const wall of this.mapConfig.walls || []) {
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(...wall.size), wallMaterial);
      mesh.name = wall.id;
      mesh.position.set(...wall.position);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      this.scene.add(mesh);
      this.collisionWorld.addStaticBox(wall.id, wall.position, wall.size);

      const floorY = wall.position[1] - wall.size[1] / 2;
      const baseTrim = new THREE.Mesh(
        new THREE.BoxGeometry(wall.size[0] + 0.02, 0.08, wall.size[2] + 0.02),
        trimMaterial,
      );
      baseTrim.name = `${wall.id}-base-trim`;
      baseTrim.position.set(wall.position[0], floorY + 0.08, wall.position[2]);
      baseTrim.receiveShadow = true;
      this.scene.add(baseTrim);

      const railTrim = new THREE.Mesh(
        new THREE.BoxGeometry(wall.size[0] + 0.018, 0.06, wall.size[2] + 0.018),
        trimMaterial,
      );
      railTrim.name = `${wall.id}-rail-trim`;
      railTrim.position.set(wall.position[0], floorY + 1.05, wall.position[2]);
      railTrim.receiveShadow = true;
      this.scene.add(railTrim);
    }
  }

  createDoors() {
    const doorMaterial = this.textures.createDoorMaterial();
    const frameMaterial = new THREE.MeshStandardMaterial({
      color: 0x211712,
      roughness: 0.86,
    });

    for (const doorConfig of this.mapConfig.doors || []) {
      const door = new Door(doorConfig, doorMaterial);
      this.doors.push(door);
      this.scene.add(door.group);
      this.scene.add(this.createDoorFrame(doorConfig, frameMaterial));
      this.collisionWorld.addDoor(door);
    }
    this.validateDoorConnections();
  }

  createDoorFrame(doorConfig, material) {
    const frame = new THREE.Group();
    frame.name = `${doorConfig.id}-frame`;
    frame.position.set(...doorConfig.position);

    const [widthX, height, widthZ] = doorConfig.size;
    const isXThin = widthX < widthZ;
    if (isXThin) {
      const postGeometry = new THREE.BoxGeometry(0.18, height + 0.25, 0.16);
      for (const z of [-widthZ / 2 - 0.08, widthZ / 2 + 0.08]) {
        const post = new THREE.Mesh(postGeometry, material);
        post.position.set(0, (height + 0.25) / 2, z);
        post.castShadow = true;
        frame.add(post);
      }
      const header = new THREE.Mesh(
        new THREE.BoxGeometry(0.2, 0.22, widthZ + 0.42),
        material,
      );
      header.position.set(0, height + 0.11, 0);
      header.castShadow = true;
      frame.add(header);
    } else {
      const postGeometry = new THREE.BoxGeometry(0.16, height + 0.25, 0.18);
      for (const x of [-widthX / 2 - 0.08, widthX / 2 + 0.08]) {
        const post = new THREE.Mesh(postGeometry, material);
        post.position.set(x, (height + 0.25) / 2, 0);
        post.castShadow = true;
        frame.add(post);
      }
      const header = new THREE.Mesh(
        new THREE.BoxGeometry(widthX + 0.42, 0.22, 0.2),
        material,
      );
      header.position.set(0, height + 0.11, 0);
      header.castShadow = true;
      frame.add(header);
    }

    return frame;
  }

  createKeys() {
    for (const keyConfig of this.mapConfig.keys || []) {
      const key = new KeyItem(keyConfig);
      this.keys.push(key);
      this.scene.add(key.group);
    }
  }

  createCabinets() {
    const cabinetMaterial = this.textures.createCabinetMaterial();
    for (const cabinetConfig of this.mapConfig.cabinets || []) {
      const cabinet = new Cabinet(cabinetConfig, { bodyMaterial: cabinetMaterial });
      this.cabinets.push(cabinet);
      this.scene.add(cabinet.group);

      const isSideways = Math.abs(Math.sin(cabinet.yaw)) > 0.7;
      const collisionSize = isSideways
        ? [cabinet.size[2], cabinet.size[1], cabinet.size[0]]
        : cabinet.size;
      this.collisionWorld.addStaticBox(
        cabinet.id,
        [cabinet.position.x, cabinet.position.y + cabinet.size[1] / 2, cabinet.position.z],
        collisionSize,
      );
    }
  }

  createFinalExit() {
    if (!this.mapConfig.finalExit) {
      return;
    }

    this.finalExit = new FinalExit(this.mapConfig.finalExit);
    this.scene.add(this.finalExit.group);
    this.collisionWorld.addStaticBox(
      this.finalExit.id,
      [this.finalExit.position.x, this.finalExit.position.y + 0.4, this.finalExit.position.z],
      [1.65, 0.8, 1.1],
    );
  }

  createProps() {
    for (const prop of this.mapConfig.props || []) {
      const mesh = this.createPropMesh(prop);
      if (!mesh) {
        continue;
      }
      this.scene.add(mesh);
      if (prop.collision) {
        this.collisionWorld.addStaticBox(
          prop.id,
          [prop.position[0], prop.position[1] + prop.size[1] / 2, prop.position[2]],
          prop.size,
        );
      }
    }
  }

  createPropMesh(prop) {
    if (prop.assetUrl) {
      return this.createGltfProp(prop);
    }

    if (prop.type === "blood-stain") {
      const stain = new THREE.Mesh(
        new THREE.CircleGeometry(prop.size[0] / 2, 24),
        new THREE.MeshBasicMaterial({ color: 0x3a0507, transparent: true, opacity: 0.62, depthWrite: false }),
      );
      stain.name = prop.id;
      stain.rotation.x = -Math.PI / 2;
      stain.scale.z = prop.size[1] / prop.size[0];
      stain.position.set(...prop.position);
      return stain;
    }

    if (prop.type === "hanging-cloth") {
      const cloth = new THREE.Mesh(
        new THREE.PlaneGeometry(prop.size[0], prop.size[1]),
        new THREE.MeshStandardMaterial({ color: 0x2c1112, roughness: 0.96, side: THREE.DoubleSide }),
      );
      cloth.name = prop.id;
      cloth.position.set(...prop.position);
      cloth.rotation.set(...(prop.rotation || [0, Math.PI * 0.06, 0]));
      cloth.castShadow = true;
      return cloth;
    }

    if (prop.type === "wall-stain") {
      const stain = new THREE.Mesh(
        new THREE.PlaneGeometry(prop.size[0], prop.size[1]),
        new THREE.MeshBasicMaterial({ color: 0x241010, transparent: true, opacity: 0.48, depthWrite: false }),
      );
      stain.name = prop.id;
      stain.position.set(...prop.position);
      stain.rotation.set(...(prop.rotation || [0, 0, 0]));
      return stain;
    }

    if (prop.type === "pipe") {
      const pipe = new THREE.Mesh(
        new THREE.CylinderGeometry(prop.size[0], prop.size[0], prop.size[2], 12),
        new THREE.MeshStandardMaterial({ color: 0x141817, roughness: 0.7, metalness: 0.4 }),
      );
      pipe.name = prop.id;
      pipe.rotation.x = Math.PI / 2;
      pipe.position.set(...prop.position);
      return pipe;
    }

    if (prop.type === "shelf") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const color = WORLD_CONFIG.toyColors[prop.colorIndex % WORLD_CONFIG.toyColors.length];
      const material = new THREE.MeshStandardMaterial({ color, roughness: 0.72 });
      const frameMaterial = new THREE.MeshStandardMaterial({ color: 0x15110f, roughness: 0.86 });
      const [width, height, depth] = prop.size;
      const postGeometry = new THREE.BoxGeometry(0.08, height, 0.08);
      for (const x of [-width / 2 + 0.04, width / 2 - 0.04]) {
        for (const z of [-depth / 2 + 0.04, depth / 2 - 0.04]) {
          const post = new THREE.Mesh(postGeometry, frameMaterial);
          post.position.set(x, height / 2, z);
          post.castShadow = true;
          group.add(post);
        }
      }
      for (let shelfIndex = 0; shelfIndex < 3; shelfIndex += 1) {
        const plank = new THREE.Mesh(new THREE.BoxGeometry(width, 0.08, depth), frameMaterial);
        plank.position.y = 0.18 + shelfIndex * (height * 0.38);
        plank.castShadow = true;
        plank.receiveShadow = true;
        group.add(plank);
      }
      if (prop.decorations !== false) {
        for (let index = 0; index < 3; index += 1) {
          const doll = new THREE.Mesh(new THREE.SphereGeometry(0.18, 10, 10), material);
          doll.position.set(0, 0.36 + index * 0.44, -depth * 0.28 + index * depth * 0.28);
          group.add(doll);
        }
      }
      return group;
    }

    if (prop.type === "candle") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const candle = new THREE.Mesh(
        new THREE.CylinderGeometry(prop.size[0], prop.size[0], prop.size[1], 12),
        new THREE.MeshStandardMaterial({ color: 0xd9cfa9, roughness: 0.55 }),
      );
      candle.position.y = prop.size[1] / 2;
      group.add(candle);
      const flame = new THREE.PointLight(0xd4b24a, 0.85, 4.5, 1.8);
      flame.position.y = prop.size[1] + 0.1;
      group.add(flame);
      return group;
    }

    if (prop.type === "guard-rail") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const [length, height, depth] = prop.size;
      const railMaterial = new THREE.MeshStandardMaterial({ color: 0x17120f, roughness: 0.88 });
      const topRail = new THREE.Mesh(new THREE.BoxGeometry(length, 0.16, depth), railMaterial);
      topRail.position.y = height;
      topRail.castShadow = true;
      group.add(topRail);

      const midRail = new THREE.Mesh(new THREE.BoxGeometry(length * 0.96, 0.11, depth * 0.82), railMaterial);
      midRail.position.y = height * 0.55;
      midRail.castShadow = true;
      group.add(midRail);

      const postCount = prop.posts ?? Math.max(3, Math.ceil(length / 1.15) + 1);
      for (let index = 0; index < postCount; index += 1) {
        const t = postCount === 1 ? 0.5 : index / (postCount - 1);
        const post = new THREE.Mesh(new THREE.BoxGeometry(depth * 1.18, height, depth * 1.18), railMaterial);
        post.position.set(-length / 2 + length * t, height / 2, 0);
        post.castShadow = true;
        group.add(post);
      }
      return group;
    }

    if (prop.type === "floor-curb") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const [width, height, depth] = prop.size;
      const material = new THREE.MeshStandardMaterial({ color: 0x17120f, roughness: 0.9 });
      const curb = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth), material);
      curb.position.y = height / 2;
      curb.castShadow = true;
      curb.receiveShadow = true;
      group.add(curb);
      return group;
    }

    if (prop.type === "wall-lamp") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const [width, height, depth] = prop.size;
      const baseMaterial = new THREE.MeshStandardMaterial({ color: 0x17120f, roughness: 0.82 });
      const glowMaterial = new THREE.MeshStandardMaterial({
        color: 0x9c7a35,
        emissive: 0xd4b24a,
        emissiveIntensity: 0.42,
        roughness: 0.58,
      });

      const plate = new THREE.Mesh(new THREE.BoxGeometry(width, height, 0.05), baseMaterial);
      plate.castShadow = true;
      group.add(plate);

      const shade = new THREE.Mesh(new THREE.BoxGeometry(width * 0.64, height * 0.48, depth), glowMaterial);
      shade.position.z = depth * 0.48;
      shade.castShadow = true;
      group.add(shade);

      const light = new THREE.PointLight(0xd4b24a, prop.intensity ?? 0.45, prop.distance ?? 4.2, 1.7);
      light.position.set(0, 0, depth * 1.1);
      group.add(light);
      return group;
    }

    if (prop.type === "barricade") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const darkWood = new THREE.MeshStandardMaterial({ color: 0x211813, roughness: 0.9 });
      const rope = new THREE.MeshStandardMaterial({ color: 0x4b3a25, roughness: 0.86 });
      const [width, height, depth] = prop.size;
      for (let index = 0; index < 4; index += 1) {
        const plank = new THREE.Mesh(new THREE.BoxGeometry(width * 0.9, 0.18, depth * 0.42), darkWood);
        plank.position.set(0, 0.22 + index * (height / 4), (index % 2 === 0 ? -1 : 1) * depth * 0.12);
        plank.rotation.z = (index % 2 === 0 ? -1 : 1) * 0.08;
        plank.castShadow = true;
        group.add(plank);
      }
      for (const x of [-width * 0.35, width * 0.35]) {
        const post = new THREE.Mesh(new THREE.BoxGeometry(0.18, height, 0.18), rope);
        post.position.set(x, height / 2, 0);
        post.castShadow = true;
        group.add(post);
      }
      return group;
    }

    if (prop.type === "planks") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const material = new THREE.MeshStandardMaterial({ color: 0x2b1b13, roughness: 0.9 });
      for (let index = 0; index < 3; index += 1) {
        const plank = new THREE.Mesh(new THREE.BoxGeometry(prop.size[0], 0.16, prop.size[2]), material);
        plank.position.y = -prop.size[1] / 2 + 0.28 + index * 0.46;
        plank.rotation.z = (index - 1) * 0.13;
        plank.castShadow = true;
        group.add(plank);
      }
      return group;
    }

    if (prop.type === "doll-circle") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const bodyMaterial = new THREE.MeshStandardMaterial({ color: 0x6e4638, roughness: 0.92 });
      const faceMaterial = new THREE.MeshStandardMaterial({ color: 0xbba98a, roughness: 0.86 });
      for (let index = 0; index < 7; index += 1) {
        const angle = (Math.PI * 2 * index) / 7;
        const doll = new THREE.Group();
        doll.position.set(Math.cos(angle) * 0.75, 0, Math.sin(angle) * 0.75);
        doll.rotation.y = -angle;
        const body = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.38, 0.16), bodyMaterial);
        body.position.y = 0.22;
        const head = new THREE.Mesh(new THREE.SphereGeometry(0.13, 10, 8), faceMaterial);
        head.position.y = 0.5;
        doll.add(body, head);
        group.add(doll);
      }
      return group;
    }

    if (prop.type === "shards") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const material = new THREE.MeshStandardMaterial({ color: 0x8da0a0, roughness: 0.35, metalness: 0.25 });
      for (let index = 0; index < 11; index += 1) {
        const shard = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.025, 0.08), material);
        shard.position.set(
          (Math.random() - 0.5) * prop.size[0],
          0.02,
          (Math.random() - 0.5) * prop.size[2],
        );
        shard.rotation.y = Math.random() * Math.PI;
        shard.castShadow = true;
        group.add(shard);
      }
      return group;
    }

    if (prop.type === "mirror-panel") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const frameMaterial = new THREE.MeshStandardMaterial({ color: 0x17120f, roughness: 0.84 });
      const glassMaterial = new THREE.MeshStandardMaterial({
        color: 0x6c8580,
        metalness: 0.35,
        roughness: 0.22,
        transparent: true,
        opacity: 0.62,
      });
      const [width, height, depth] = prop.size;
      const glass = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth), glassMaterial);
      glass.castShadow = true;
      group.add(glass);
      const top = new THREE.Mesh(new THREE.BoxGeometry(width + 0.18, 0.12, depth + 0.06), frameMaterial);
      top.position.y = height / 2 + 0.08;
      const bottom = top.clone();
      bottom.position.y = -height / 2 - 0.08;
      const sideGeometry = new THREE.BoxGeometry(0.12, height + 0.28, depth + 0.06);
      const left = new THREE.Mesh(sideGeometry, frameMaterial);
      left.position.x = -width / 2 - 0.08;
      const right = new THREE.Mesh(sideGeometry, frameMaterial);
      right.position.x = width / 2 + 0.08;
      group.add(top, bottom, left, right);
      return group;
    }

    if (prop.type === "hwa-painting") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const [width, height, depth] = prop.size;
      const frameMaterial = new THREE.MeshStandardMaterial({ color: 0x1c120c, roughness: 0.86, metalness: 0.05 });
      const backingMaterial = new THREE.MeshStandardMaterial({ color: 0x0d0a08, roughness: 0.95 });
      const paintingMaterial = this.textures.createHwaPaintMaterial();

      const backing = new THREE.Mesh(new THREE.BoxGeometry(width + 0.28, height + 0.28, depth), backingMaterial);
      backing.receiveShadow = true;
      group.add(backing);

      const canvas = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth * 0.35), paintingMaterial);
      canvas.position.z = depth * 0.55;
      canvas.castShadow = true;
      canvas.receiveShadow = true;
      group.add(canvas);

      const top = new THREE.Mesh(new THREE.BoxGeometry(width + 0.36, 0.16, depth + 0.08), frameMaterial);
      top.position.set(0, height / 2 + 0.11, depth * 0.62);
      const bottom = top.clone();
      bottom.position.y = -height / 2 - 0.11;
      const sideGeometry = new THREE.BoxGeometry(0.16, height + 0.36, depth + 0.08);
      const left = new THREE.Mesh(sideGeometry, frameMaterial);
      left.position.set(-width / 2 - 0.11, 0, depth * 0.62);
      const right = new THREE.Mesh(sideGeometry, frameMaterial);
      right.position.set(width / 2 + 0.11, 0, depth * 0.62);
      for (const part of [top, bottom, left, right]) {
        part.castShadow = true;
        part.receiveShadow = true;
      }
      group.add(top, bottom, left, right);
      return group;
    }

    if (prop.type === "ritual-circle") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const radius = prop.size[0] / 2;
      const ring = new THREE.Mesh(
        new THREE.RingGeometry(radius * 0.72, radius, 48),
        new THREE.MeshBasicMaterial({ color: 0xd2191f, transparent: true, opacity: 0.82, depthWrite: false, side: THREE.DoubleSide }),
      );
      ring.rotation.x = -Math.PI / 2;
      group.add(ring);
      const light = new THREE.PointLight(0xd2191f, 0.9, 5.5, 1.8);
      light.position.y = 0.28;
      group.add(light);
      return group;
    }

    if (prop.type === "broken-desk") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.y = prop.rotation?.[1] ?? -0.25;
      const wood = new THREE.MeshStandardMaterial({ color: 0x2d2119, roughness: 0.9 });
      const top = new THREE.Mesh(new THREE.BoxGeometry(prop.size[0], 0.16, prop.size[2]), wood);
      top.position.y = 0.58;
      top.rotation.z = -0.12;
      top.castShadow = true;
      group.add(top);
      for (const x of [-prop.size[0] * 0.4, prop.size[0] * 0.4]) {
        for (const z of [-prop.size[2] * 0.35, prop.size[2] * 0.35]) {
          const leg = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.58, 0.12), wood);
          leg.position.set(x, 0.28, z);
          leg.rotation.x = (x + z) * 0.04;
          leg.castShadow = true;
          group.add(leg);
        }
      }
      return group;
    }

    if (prop.type === "barred-window") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const frameMaterial = new THREE.MeshStandardMaterial({ color: 0x171c18, roughness: 0.82 });
      const glassMaterial = new THREE.MeshStandardMaterial({
        color: 0x496052,
        roughness: 0.55,
        metalness: 0.05,
        transparent: true,
        opacity: 0.42,
      });
      const glass = new THREE.Mesh(new THREE.BoxGeometry(prop.size[0], prop.size[1], 0.035), glassMaterial);
      group.add(glass);
      for (const x of [-prop.size[0] / 2, prop.size[0] / 2]) {
        const side = new THREE.Mesh(new THREE.BoxGeometry(0.08, prop.size[1] + 0.12, 0.08), frameMaterial);
        side.position.x = x;
        side.castShadow = true;
        group.add(side);
      }
      for (const y of [-prop.size[1] / 2, prop.size[1] / 2]) {
        const rail = new THREE.Mesh(new THREE.BoxGeometry(prop.size[0] + 0.12, 0.08, 0.08), frameMaterial);
        rail.position.y = y;
        rail.castShadow = true;
        group.add(rail);
      }
      for (let index = -1; index <= 1; index += 1) {
        const bar = new THREE.Mesh(new THREE.BoxGeometry(0.045, prop.size[1] + 0.08, 0.075), frameMaterial);
        bar.position.x = index * prop.size[0] * 0.22;
        bar.castShadow = true;
        group.add(bar);
      }
      return group;
    }

    if (prop.type === "wire-bundle") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const material = new THREE.MeshStandardMaterial({ color: 0x060706, roughness: 0.7, metalness: 0.25 });
      for (let index = 0; index < 5; index += 1) {
        const wire = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, prop.size[2], 8), material);
        wire.rotation.x = Math.PI / 2;
        wire.position.set((index - 2) * 0.055, Math.sin(index) * 0.035, 0);
        group.add(wire);
      }
      return group;
    }

    if (prop.type === "cicada-shells") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      const shellMaterial = new THREE.MeshStandardMaterial({ color: 0x8b6a38, roughness: 0.92 });
      for (let index = 0; index < 9; index += 1) {
        const shell = new THREE.Mesh(new THREE.SphereGeometry(0.08 + index * 0.003, 8, 6), shellMaterial);
        shell.scale.set(1.35, 0.42, 0.72);
        shell.position.set((index % 3 - 1) * 0.22, 0.04, (Math.floor(index / 3) - 1) * 0.18);
        shell.rotation.y = index * 0.7;
        shell.castShadow = true;
        group.add(shell);
      }
      return group;
    }

    if (prop.type === "paper-strip") {
      if (prop.textureUrl) {
        return this.createTexturedHangingProp(prop);
      }

      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const material = new THREE.MeshStandardMaterial({
        color: 0xc8bfa3,
        roughness: 0.96,
        side: THREE.DoubleSide,
      });
      for (let index = 0; index < 5; index += 1) {
        const strip = new THREE.Mesh(new THREE.PlaneGeometry(0.16, prop.size[1] * (0.75 + index * 0.06)), material);
        strip.position.set((index - 2) * 0.12, -index * 0.025, 0);
        strip.rotation.z = (index - 2) * 0.045;
        strip.castShadow = true;
        group.add(strip);
      }
      return group;
    }

    if (prop.type === "mannequin") {
      const group = new THREE.Group();
      group.name = prop.id;
      group.position.set(...prop.position);
      group.rotation.set(...(prop.rotation || [0, 0, 0]));
      const bodyMaterial = new THREE.MeshStandardMaterial({ color: 0xb6aa93, roughness: 0.88 });
      const shadowMaterial = new THREE.MeshStandardMaterial({ color: 0x282119, roughness: 0.92 });
      const torso = new THREE.Mesh(new THREE.BoxGeometry(0.44, 0.95, 0.24), bodyMaterial);
      torso.position.y = 0.82;
      torso.castShadow = true;
      const head = new THREE.Mesh(new THREE.SphereGeometry(0.22, 12, 10), bodyMaterial);
      head.position.y = 1.47;
      head.castShadow = true;
      const stand = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.72, 0.16), shadowMaterial);
      stand.position.y = 0.28;
      group.add(torso, head, stand);
      return group;
    }

    if (prop.type === "horror-placeholder") {
      return this.createHorrorPlaceholder(prop);
    }

      const color = WORLD_CONFIG.toyColors[prop.colorIndex % WORLD_CONFIG.toyColors.length];
      const mesh = new THREE.Mesh(
        new THREE.BoxGeometry(...prop.size),
        new THREE.MeshStandardMaterial({ color, roughness: 0.65 }),
      );
      mesh.name = prop.id;
      mesh.position.set(...prop.position);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      mesh.rotation.y = (prop.position[0] + prop.position[2]) * 0.18;
      return mesh;
  }

  createHorrorPlaceholder(prop) {
    const group = new THREE.Group();
    group.name = prop.id;
    group.position.set(...prop.position);
    group.rotation.set(...(prop.rotation || [0, 0, 0]));
    group.userData.placeholderKind = prop.placeholderKind;
    group.userData.description = prop.description;

    const baseMaterial = new THREE.MeshStandardMaterial({ color: prop.color ?? 0x5f5145, roughness: 0.9 });
    const darkMaterial = new THREE.MeshStandardMaterial({ color: 0x110d0b, roughness: 0.95 });
    const paleMaterial = new THREE.MeshStandardMaterial({ color: 0xb6aa93, roughness: 0.88 });
    const redMaterial = new THREE.MeshStandardMaterial({ color: 0x6f1918, emissive: 0x2a0505, emissiveIntensity: 0.18, roughness: 0.82 });
    const [width, height, depth] = prop.size || [1, 1, 1];

    if (prop.textureUrl && prop.placeholderKind === "red-puddle") {
      const puddle = this.createTexturedFloorProp(prop);
      group.add(puddle);
      return group;
    }

    if (prop.placeholderKind === "wrapped-body") {
      const body = new THREE.Mesh(new THREE.CapsuleGeometry(width * 0.24, height * 0.62, 8, 12), paleMaterial);
      body.position.y = height * 0.52;
      body.rotation.z = Math.PI / 2;
      body.scale.z = depth / Math.max(width, 0.01);
      body.castShadow = true;
      const ropeCount = 4;
      for (let index = 0; index < ropeCount; index += 1) {
        const rope = new THREE.Mesh(new THREE.TorusGeometry(width * 0.26, 0.018, 8, 18), darkMaterial);
        rope.position.y = height * (0.26 + index * 0.17);
        rope.rotation.x = Math.PI / 2;
        rope.castShadow = true;
        group.add(rope);
      }
      group.add(body);
      return group;
    }

    if (prop.placeholderKind === "watching-mask") {
      const backing = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth * 0.18), darkMaterial);
      backing.position.y = height / 2;
      backing.castShadow = true;
      const face = new THREE.Mesh(new THREE.SphereGeometry(Math.min(width, height) * 0.28, 16, 12), paleMaterial);
      face.scale.set(0.8, 1.08, 0.2);
      face.position.set(0, height * 0.58, -depth * 0.12);
      const eyeGeometry = new THREE.BoxGeometry(width * 0.1, height * 0.035, depth * 0.08);
      for (const x of [-width * 0.11, width * 0.11]) {
        const eye = new THREE.Mesh(eyeGeometry, darkMaterial);
        eye.position.set(x, height * 0.61, -depth * 0.28);
        group.add(eye);
      }
      group.add(backing, face);
      return group;
    }

    if (prop.placeholderKind === "red-puddle") {
      const puddle = new THREE.Mesh(
        new THREE.CircleGeometry(Math.max(width, depth) * 0.5, 32),
        new THREE.MeshBasicMaterial({ color: 0x7a1110, transparent: true, opacity: 0.68, depthWrite: false }),
      );
      puddle.rotation.x = -Math.PI / 2;
      puddle.position.y = 0.025;
      group.add(puddle);
      for (let index = 0; index < 5; index += 1) {
        const drip = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.025, depth * (0.18 + index * 0.03)), redMaterial);
        drip.position.set((index - 2) * width * 0.16, 0.035, depth * (0.18 + index * 0.02));
        drip.rotation.y = index * 0.42;
        group.add(drip);
      }
      return group;
    }

    if (prop.placeholderKind === "hanging-bundle") {
      const cord = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, height, 8), darkMaterial);
      cord.position.y = height / 2;
      cord.castShadow = true;
      const bundle = new THREE.Mesh(new THREE.BoxGeometry(width, height * 0.34, depth), baseMaterial);
      bundle.position.y = height * 0.18;
      bundle.rotation.z = -0.08;
      bundle.castShadow = true;
      group.add(cord, bundle);
      return group;
    }

    if (prop.placeholderKind === "broken-doll-pile") {
      for (let index = 0; index < 8; index += 1) {
        const part = new THREE.Mesh(
          index % 3 === 0 ? new THREE.SphereGeometry(0.11, 10, 8) : new THREE.BoxGeometry(0.18, 0.12, 0.28),
          index % 2 === 0 ? paleMaterial : baseMaterial,
        );
        part.position.set((index % 4 - 1.5) * width * 0.18, 0.08 + index * 0.012, (Math.floor(index / 4) - 0.5) * depth * 0.28);
        part.rotation.set(index * 0.3, index * 0.7, index * 0.19);
        part.castShadow = true;
        group.add(part);
      }
      return group;
    }

    const marker = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth), baseMaterial);
    marker.position.y = height / 2;
    marker.castShadow = true;
    marker.receiveShadow = true;
    group.add(marker);
    return group;
  }

  createGltfProp(prop) {
    const group = new THREE.Group();
    group.name = prop.id;
    group.position.set(...prop.position);
    group.rotation.set(...(prop.rotation || [0, 0, 0]));
    group.userData.propType = prop.type;
    group.userData.assetUrl = prop.assetUrl;
    group.userData.description = prop.description;

    const fallback = this.createGltfLoadingFallback(prop);
    group.add(fallback);

    const loadPromise = new Promise((resolve) => {
      this.gltfLoader.load(
        prop.assetUrl,
        (gltf) => {
          const root = gltf.scene || gltf.scenes?.[0];
          if (!root) {
            console.warn(`[MapBuilder] GLB asset has no scene: ${prop.id} (${prop.assetUrl})`);
            resolve();
            return;
          }

          group.clear();
          root.name = `${prop.id}-gltf-root`;
          this.normalizeGltfRoot(root, prop);
          root.traverse((child) => {
            if (!child.isMesh) {
              return;
            }
            child.castShadow = true;
            child.receiveShadow = true;
            const materials = Array.isArray(child.material) ? child.material : [child.material];
            for (const material of materials) {
              if (!material) {
                continue;
              }
              if (prop.assetTextureCutout && material.map) {
                material.map = this.createCutoutTexture(material.map, prop.assetTextureCutout);
                material.transparent = true;
                material.alphaTest = prop.assetTextureCutout.alphaTest ?? 0.08;
                material.depthWrite = false;
                material.side = THREE.DoubleSide;
              }
              for (const key of ["map", "emissiveMap", "roughnessMap", "metalnessMap", "normalMap"]) {
                if (material[key]) {
                  material[key].colorSpace = key === "map" || key === "emissiveMap"
                    ? THREE.SRGBColorSpace
                    : material[key].colorSpace;
                }
              }
            }
          });
          group.add(root);
          resolve();
        },
        undefined,
        (error) => {
          console.warn(`[MapBuilder] Failed to load prop GLB ${prop.id}: ${prop.assetUrl}`, error);
          resolve();
        },
      );
    });
    this.pendingAssets.push(loadPromise);

    return group;
  }

  createCutoutTexture(sourceTexture, options = {}) {
    const image = sourceTexture.image;
    if (!image) {
      return sourceTexture;
    }

    const canvas = document.createElement("canvas");
    canvas.width = image.width;
    canvas.height = image.height;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
    const pixels = imageData.data;
    const samples = options.samples || [
      [8, 8],
      [canvas.width - 9, 8],
      [8, canvas.height - 9],
      [canvas.width - 9, canvas.height - 9],
      [canvas.width / 2, 8],
      [canvas.width / 2, canvas.height - 9],
    ];
    const backgroundColors = samples.map(([x, y]) => {
      const px = Math.max(0, Math.min(canvas.width - 1, Math.round(x)));
      const py = Math.max(0, Math.min(canvas.height - 1, Math.round(y)));
      const index = (py * canvas.width + px) * 4;
      return [pixels[index], pixels[index + 1], pixels[index + 2]];
    });
    const threshold = options.threshold ?? 58;
    const feather = Math.max(1, options.feather ?? 28);

    for (let index = 0; index < pixels.length; index += 4) {
      const r = pixels[index];
      const g = pixels[index + 1];
      const b = pixels[index + 2];
      let nearestDistance = Infinity;
      for (const [br, bg, bb] of backgroundColors) {
        const distance = Math.hypot(r - br, g - bg, b - bb);
        nearestDistance = Math.min(nearestDistance, distance);
      }
      if (nearestDistance < threshold) {
        pixels[index + 3] = 0;
      } else if (nearestDistance < threshold + feather) {
        pixels[index + 3] = Math.round(255 * ((nearestDistance - threshold) / feather));
      }
    }

    context.putImageData(imageData, 0, 0);
    const cutoutTexture = new THREE.CanvasTexture(canvas);
    cutoutTexture.colorSpace = THREE.SRGBColorSpace;
    cutoutTexture.flipY = sourceTexture.flipY;
    cutoutTexture.wrapS = sourceTexture.wrapS;
    cutoutTexture.wrapT = sourceTexture.wrapT;
    cutoutTexture.repeat.copy(sourceTexture.repeat);
    cutoutTexture.offset.copy(sourceTexture.offset);
    cutoutTexture.needsUpdate = true;
    return cutoutTexture;
  }

  createGltfLoadingFallback(prop) {
    const [width, height, depth] = prop.size || [0.6, 0.6, 0.6];
    const material = new THREE.MeshStandardMaterial({
      color: 0x3b3028,
      roughness: 0.9,
      transparent: true,
      opacity: 0.32,
    });
    const fallback = new THREE.Mesh(
      new THREE.BoxGeometry(Math.max(width, 0.08), Math.max(height, 0.08), Math.max(depth, 0.08)),
      material,
    );
    fallback.name = `${prop.id}-asset-loading-fallback`;
    fallback.position.y = height / 2;
    fallback.castShadow = true;
    fallback.receiveShadow = true;
    return fallback;
  }

  normalizeGltfRoot(root, prop) {
    if (prop.assetRotation) {
      root.rotation.set(...prop.assetRotation);
      root.updateMatrixWorld(true);
    }

    const targetSize = new THREE.Vector3(...(prop.size || [1, 1, 1]));
    root.updateMatrixWorld(true);
    let box = new THREE.Box3().setFromObject(root);
    const sourceSize = new THREE.Vector3();
    box.getSize(sourceSize);

    const axes = ["x", "y", "z"];
    if (prop.assetScaleMode === "stretch") {
      const scale = prop.assetScale ?? 1;
      root.scale.set(
        sourceSize.x > 0.0001 ? (targetSize.x / sourceSize.x) * scale : scale,
        sourceSize.y > 0.0001 ? (targetSize.y / sourceSize.y) * scale : scale,
        sourceSize.z > 0.0001 ? (targetSize.z / sourceSize.z) * scale : scale,
      );
      root.updateMatrixWorld(true);
      box = new THREE.Box3().setFromObject(root);
      const stretchedCenter = new THREE.Vector3();
      box.getCenter(stretchedCenter);
      root.position.x += -stretchedCenter.x;
      root.position.z += -stretchedCenter.z;
      root.position.y += (prop.assetAnchor || "bottom") === "center" ? -stretchedCenter.y : -box.min.y;
      if (prop.assetOffset) {
        root.position.x += prop.assetOffset[0] ?? 0;
        root.position.y += prop.assetOffset[1] ?? 0;
        root.position.z += prop.assetOffset[2] ?? 0;
      }
      return;
    }

    const fitAxes = prop.assetFitAxes || axes.filter((axis) => targetSize[axis] > 0.12);
    const scaleCandidates = fitAxes
      .filter((axis) => targetSize[axis] > 0 && sourceSize[axis] > 0.0001)
      .map((axis) => targetSize[axis] / sourceSize[axis]);
    const uniformScale = (scaleCandidates.length ? Math.min(...scaleCandidates) : 1) * (prop.assetScale ?? 1);
    root.scale.multiplyScalar(uniformScale);

    root.updateMatrixWorld(true);
    box = new THREE.Box3().setFromObject(root);
    const center = new THREE.Vector3();
    box.getCenter(center);

    const anchor = prop.assetAnchor
      || (prop.type === "barred-window" || prop.type === "wire-bundle" ? "center" : "bottom");
    root.position.x += -center.x;
    root.position.z += -center.z;
    root.position.y += anchor === "center" ? -center.y : -box.min.y;

    if (prop.assetOffset) {
      root.position.x += prop.assetOffset[0] ?? 0;
      root.position.y += prop.assetOffset[1] ?? 0;
      root.position.z += prop.assetOffset[2] ?? 0;
    }
  }

  createTexturedFloorProp(prop) {
    const [width, , depth] = prop.size || [1, 0.02, 1];
    const texture = this.textureLoader.load(prop.textureUrl);
    texture.colorSpace = THREE.SRGBColorSpace;
    const material = new THREE.MeshBasicMaterial({
      map: texture,
      color: 0xffffff,
      transparent: true,
      opacity: prop.opacity ?? 0.82,
      depthWrite: false,
      side: THREE.DoubleSide,
    });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(width, depth), material);
    mesh.name = `${prop.id}-texture-plane`;
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.y = 0.026;
    return mesh;
  }

  createTexturedHangingProp(prop) {
    const group = new THREE.Group();
    group.name = prop.id;
    group.position.set(...prop.position);
    group.rotation.set(...(prop.rotation || [0, 0, 0]));
    group.userData.propType = prop.type;
    group.userData.textureUrl = prop.textureUrl;

    const texture = this.textureLoader.load(prop.textureUrl);
    texture.colorSpace = THREE.SRGBColorSpace;
    const material = new THREE.MeshStandardMaterial({
      map: texture,
      color: 0xffffff,
      roughness: 0.96,
      transparent: true,
      alphaTest: 0.02,
      side: THREE.DoubleSide,
    });
    const strip = new THREE.Mesh(new THREE.PlaneGeometry(prop.size[0], prop.size[1]), material);
    strip.name = `${prop.id}-texture-plane`;
    strip.position.y = -prop.size[1] / 2;
    strip.castShadow = true;
    strip.receiveShadow = true;
    group.add(strip);
    return group;
  }

  validateDoorConnections() {
    const roomIds = new Set((this.mapConfig.roomAreas || []).map((room) => room.id));
    for (const door of this.doors) {
      if (door.connectedRoomId && !roomIds.has(door.connectedRoomId)) {
        console.warn(`[MapBuilder] Door ${door.id} references missing room ${door.connectedRoomId}.`);
        continue;
      }
      if (!door.connectedRoomId && !door.isLocked && !door.isBlocked) {
        console.warn(`[MapBuilder] Door ${door.id} has no connected room and is not marked locked/blocked.`);
      }
    }
  }

  createDebugOverlays() {
    const overlays = [
      { areas: this.mapConfig.floorAreas || [], color: 0x2f8f4f, yOffset: 0.025, opacity: 0.055 },
      { areas: this.mapConfig.blockedAreas || [], color: 0x9c2730, yOffset: 0.04, opacity: 0.1 },
      { areas: this.mapConfig.voidAreas || [], color: 0x35567a, yOffset: 0.035, opacity: 0.08 },
      { areas: this.mapConfig.dropZones || [], color: 0xd4b24a, yOffset: 0.05, opacity: 0.14 },
    ];

    for (const group of overlays) {
      for (const area of group.areas) {
        const width = area.maxX - area.minX;
        const depth = area.maxZ - area.minZ;
        if (width <= 0 || depth <= 0) {
          continue;
        }
        const mesh = new THREE.Mesh(
          new THREE.PlaneGeometry(width, depth),
          new THREE.MeshBasicMaterial({
            color: group.color,
            transparent: true,
            opacity: group.opacity,
            depthWrite: false,
            side: THREE.DoubleSide,
          }),
        );
        mesh.name = `debug-${area.type || "area"}-${area.id}`;
        mesh.rotation.x = -Math.PI / 2;
        mesh.position.set((area.minX + area.maxX) / 2, (area.y ?? this.collisionWorld.getFloorY(area.floor) ?? 0) + group.yOffset, (area.minZ + area.maxZ) / 2);
        this.scene.add(mesh);
      }
    }

    const waypointMaterial = new THREE.MeshBasicMaterial({ color: 0xd4b24a });
    for (const waypoint of this.mapConfig.transitionWaypoints || []) {
      const marker = new THREE.Mesh(new THREE.SphereGeometry(0.16, 12, 8), waypointMaterial);
      marker.name = `debug-waypoint-${waypoint.id}`;
      marker.position.set(...waypoint.position);
      marker.position.y += 0.35;
      this.scene.add(marker);
    }
  }
}
