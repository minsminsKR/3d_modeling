// Happy Toy의 최상위 조립 모듈입니다.
// 렌더러, 장면, 맵, 플레이어, 적, HUD, 루프를 생성하고 서로 연결합니다.

import * as THREE from "three";
import { createChapterSession, CHAPTERS } from "../config/chapterConfig.js";
import { CABINET_CONFIG, CAMERA_CONFIG, PLAYER_CONFIG, WORLD_CONFIG } from "../config/gameConfig.js";
import { CollisionWorld } from "../world/CollisionWorld.js";
import { EnemyManager } from "../entities/EnemyManager.js";
import { GlitchController } from "../effects/GlitchController.js";
import { HorrorEventManager } from "../events/HorrorEventManager.js";
import { MirrorHwacatEvent } from "../events/MirrorHwacatEvent.js";
import { Hud } from "../ui/Hud.js";
import { Input } from "./Input.js";
import { Loop } from "./Loop.js";
import { MapBuilder } from "../world/MapBuilder.js";
import { FlashlightController } from "../player/FlashlightController.js";
import { PlayerController } from "../player/PlayerController.js";

export class Game {
  constructor(rootElement) {
    this.rootElement = rootElement;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(WORLD_CONFIG.fogColor);
    this.scene.fog = new THREE.Fog(WORLD_CONFIG.fogColor, 20, 75);

    this.camera = new THREE.PerspectiveCamera(
      CAMERA_CONFIG.fov,
      window.innerWidth / window.innerHeight,
      CAMERA_CONFIG.near,
      CAMERA_CONFIG.far,
    );

    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    // Shadows disabled globally — enabling shadow maps causes WebGL to bind extra
    // shadow-map texture units per light, which pushed us past MAX_TEXTURE_IMAGE_UNITS
    // and forced costly shader recompilation on every new PointLight.
    this.renderer.shadowMap.enabled = false;
    this.renderer.toneMappingExposure = 1.24;
    this.rootElement.appendChild(this.renderer.domElement);

    this.hud = new Hud();
    const urlParams = new URLSearchParams(window.location.search);
    this.debugEnabled = urlParams.get("debug") === "1";
    this.chapterSession = createChapterSession();
    this.mapConfig = this.chapterSession.mapConfig;
    this.enemyConfigs = this.chapterSession.enemyConfigs;
    this.hud.setDebugEnabled(this.debugEnabled);
    this.input = new Input(this.renderer.domElement);
    this.collisionWorld = new CollisionWorld();
    this.doors = [];
    this.keys = [];
    this.cabinets = [];
    this.lovelyDolls = [];
    this.spawnedDollIds = new Set();
    this.dollCountFound = 0;
    this.collectedKeyIds = new Set();
    this.finalExit = null;
    this.keyCount = 0;
    this.player = null;
    this.flashlightController = null;
    this.enemyManager = null;
    this.horrorEventManager = null;
    this.glitchController = new GlitchController();
    this.mirrorEvents = [];
    this.horrorLights = [];
    this.flashlight = null;
    this.gameOver = false;
    this.gameCleared = false;
    this.isStarted = false;
    this.isPaused = false;
    this.cabinetEvent = null;
    this.cutsceneEvent = null;
    this.elapsedTime = 0;
    this.assetsReady = false;
    this.testSafeMode = false;
    this.firstDetectionScareReady = true;
    this.detectionFreezeTimer = 0;
    this.detectionFreezeThreat = 0;
    // Fixed PointLight pool — pre-allocated so they NEVER leave the scene at runtime.
    // Moving/resizing a light is free; adding/removing forces WebGL shader recompilation.
    this._pointLightPool = [];
    this._POINT_LIGHT_BUDGET = 8;
    // Throttle state for chunk boundary and flicker updates
    this._lastPlayerChunkCx = null;
    this._lastPlayerChunkCz = null;
    this._flickerAccum = 0;
    this.loop = new Loop((deltaTime) => this.update(deltaTime));

    this.handleResize = this.handleResize.bind(this);
    this.handlePointerLockChange = this.handlePointerLockChange.bind(this);
    this.start = this.start.bind(this);
    this.restart = this.restart.bind(this);
    this.resume = this.resume.bind(this);
    this.quitToTitle = this.quitToTitle.bind(this);
  }

  async init() {
    this.setupLighting();
    this.hud.setChapterInfo(this.chapterSession, CHAPTERS);
    this.hud.setStartEnabled(false, "불러오는 중...");
    this.hud.setStatus("복도와 몬스터를 불러오는 중입니다.");
    this.mapBuilder = new MapBuilder(this.scene, this.collisionWorld, {
      debugEnabled: this.debugEnabled,
      mapConfig: this.mapConfig,
      game: this,
    });
    if (this.mapBuilder.pendingAssets) {
      await Promise.allSettled(this.mapBuilder.pendingAssets);
    }
    const map = this.mapBuilder.build();
    this.doors = map.doors;
    this.keys = map.keys;
    this.cabinets = map.cabinets;
    this.finalExit = map.finalExit;
    this.player = new PlayerController(this.camera, this.input, this.collisionWorld, this.hud);
    this.player.setPosition(map.playerStart);
    this.player.resetLook(0, 0);
    this.flashlightController = new FlashlightController(this.flashlight, this.input, this.hud);
    this.horrorEventManager = new HorrorEventManager(
      this.scene,
      this.player,
      this.doors,
      this.hud,
      this.horrorLights,
    );
    this.player.setInteractables(
      [...this.doors, ...this.keys, ...this.cabinets, this.finalExit].filter(Boolean),
      this.createInteractionContext(),
    );

    this.enemyManager = new EnemyManager(this.scene, this.collisionWorld, this.doors, this.hud, this.enemyConfigs);
    const eventChunkCenter = { x: -32, y: 0, z: -32 }; // chunk (-2, -2) center
    const backroomsHwacatEventConfig = {
      id: "hwacat-mirror-event",
      triggerPosition: [eventChunkCenter.x + 4.0, 0, eventChunkCenter.z + 0.1],
      triggerRadius: 2.2,
      spawnPosition: [eventChunkCenter.x - 0.7, 0, eventChunkCenter.z - 1.0],
      spawnYaw: Math.PI,
      lookAtPosition: [eventChunkCenter.x - 0.7, 1.05, eventChunkCenter.z - 1.0],
      cameraDuration: 1.25,
      cameraBackStep: 0.6,
      cameraLift: 0.05,
      cameraReturnDuration: 0.25,
      danceSeconds: 8,
      idleSeconds: 4,
      transformOverlapSeconds: 0.1,
      safePauseSeconds: 0.15,
      paintingId: "upper-hwa-painting",
      paintingDropSeconds: 0.75,
      paintingDropTargetPosition: [eventChunkCenter.x - 5.0, 0.06, eventChunkCenter.z],
      paintingDropTargetRotation: [-Math.PI / 2, 0, 0.0],
      rewardKeyId: "key-hwacat",
    };
    this.mirrorEvents = [new MirrorHwacatEvent(backroomsHwacatEventConfig, {
      scene: this.scene,
      camera: this.camera,
      player: this.player,
      enemyManager: this.enemyManager,
      hud: this.hud,
      revealKeyById: (keyId, position) => this.revealKeyById(keyId, position),
    })];
    this.input.connect();
    this.connectUi();
    window.addEventListener("resize", this.handleResize);
    document.addEventListener("pointerlockchange", this.handlePointerLockChange);

    await Promise.allSettled([
      ...(map.pendingAssets || []),
      this.enemyManager.loadEnemies(),
    ]);
    await this.warmUpRenderer();
    this.assetsReady = true;
    this.hud.setChapterInfo(this.chapterSession, CHAPTERS);
    this.hud.setStartEnabled(true);
    this.hud.setStatus("화면을 클릭하면 게임이 시작됩니다.");
    this.loop.start();
  }

  setupLighting() {
    const ambient = new THREE.AmbientLight(0x504b3e, 0.95);
    this.scene.add(ambient);

    const lowAmbient = new THREE.HemisphereLight(0x5c594c, 0x2e2c24, 0.85);
    this.scene.add(lowAmbient);

    this.flashlight = new THREE.SpotLight(0xfff5d2, 25.0, 35, Math.PI * 0.28, 0.55, 1.0);
    this.flashlight.position.set(0, 0, 0);
    this.flashlight.target.position.set(0, 0, -1);
    this.flashlight.castShadow = false;
    this.flashlight.visible = true;
    this.camera.add(this.flashlight);
    this.camera.add(this.flashlight.target);
    this.scene.add(this.camera);

    // Pre-allocate the fixed PointLight pool. All lights live in the scene permanently.
    // We only update their position/intensity — never add/remove during gameplay.
    for (let i = 0; i < this._POINT_LIGHT_BUDGET; i++) {
      const pl = new THREE.PointLight(0xfffee2, 0, 18, 1.0);
      pl.castShadow = false;
      pl.position.set(0, -9999, 0); // park far off-screen until assigned
      this.scene.add(pl);
      this._pointLightPool.push(pl);
    }
  }

  connectUi() {
    this.hud.startButton.addEventListener("click", this.start);
    this.renderer.domElement.addEventListener("click", this.start);
    this.hud.restartButton.addEventListener("click", this.restart);
    this.hud.clearRestartButton.addEventListener("click", this.restart);
    this.hud.resumeButton.addEventListener("click", this.resume);
    this.hud.pauseRestartButton.addEventListener("click", this.restart);
    this.hud.quitButton.addEventListener("click", this.quitToTitle);
    this.hud.mouseSensitivityInput.addEventListener("input", () => {
      this.setMouseSensitivityScale(Number(this.hud.mouseSensitivityInput.value));
    });
    this.setMouseSensitivityScale(Number(this.hud.mouseSensitivityInput.value));
  }

  createInteractionContext() {
    return {
      hud: this.hud,
      game: this,
      getKeyCount: () => this.keyCount,
      getTotalKeys: () => this.keys.length,
      collectKey: (key) => this.collectKey(key),
      enterCabinet: (cabinet) => this.enterCabinet(cabinet),
      exitCabinet: () => this.exitCabinet(),
      canExitCabinet: () => this.canExitCabinet(),
      getHiddenPrompt: () => this.getHiddenPrompt(),
      tryClearFinal: (finalExit) => this.tryClearFinal(finalExit),
      isCleared: () => this.gameCleared,
    };
  }

  start() {
    if (!this.assetsReady) {
      this.hud.setStatus("아직 복도를 불러오는 중입니다.", 900);
      return;
    }

    if (this.isStarted && !this.isPaused) {
      this.input.requestPointerLock();
      return;
    }

    this.isStarted = true;
    this.isPaused = false;
    this.glitchController.primeAudio();
    this.hud.hideStart();
    this.hud.hidePause();
    this.input.requestPointerLock();
    this.hud.setStatus(`${this.chapterSession.title}에 들어섰습니다.`, 1800);
  }

  restart() {
    this.resetRunState();
    this.isStarted = true;
    this.isPaused = false;
    this.hud.hideCaught();
    this.hud.hideClear();
    this.hud.hidePause();
    this.hud.hideStart();
    this.input.requestPointerLock();
    this.hud.setStatus("다시 복도 한가운데에 섰습니다.", 1800);
  }

  update(deltaTime) {
    if (this.input.consumePressed("0") || this.input.consumePressed("numpad0")) {
      this.toggleTestSafeMode();
    }

    if (this.isStarted && !this.gameOver && !this.gameCleared && this.input.consumePressed("escape")) {
      this.togglePause();
    }

    if (this.isPaused) {
      this.renderer.render(this.scene, this.camera);
      this.input.endFrame();
      return;
    }

    if (this.detectionFreezeTimer > 0 && this.isStarted && !this.gameOver && !this.gameCleared) {
      this.detectionFreezeTimer = Math.max(0, this.detectionFreezeTimer - Math.min(deltaTime, 0.05));
      this.glitchController.update(deltaTime, { threat: this.detectionFreezeThreat });
      this.renderer.render(this.scene, this.camera);
      this.input.endFrame();
      return;
    }

    this.elapsedTime += deltaTime;

    for (const door of this.doors) {
      door.update(deltaTime);
    }
    for (const key of this.keys) {
      key.update(deltaTime, this.elapsedTime);
    }
    this.finalExit?.update(deltaTime);
    this.updateCabinetEvent(deltaTime);
    this.updateMirrorEvents(deltaTime);

    if (this.isStarted && !this.isPaused && !this.gameOver && !this.gameCleared && !this.cutsceneEvent) {
      this.updateBackrooms(deltaTime);
      this.updateLovelyDolls(deltaTime);
      this.player.update(deltaTime);
      this.flashlightController.update();
      this.horrorEventManager?.update(deltaTime);
      const enemyState = this.enemyManager?.update(deltaTime, {
        position: this.player.position,
        isHidden: this.player.isHidden,
        isUndetectable: this.testSafeMode,
        isMoving: this.player.isMoving,
        isSprinting: this.player.isSprinting,
      });
      this.updateGlitch(deltaTime, enemyState);
      if (enemyState?.caught) {
        this.handleCaught();
      }
    } else {
      this.glitchController.update(deltaTime, { threat: 0 });
    }

    if (this.isStarted && !this.isPaused && !this.gameOver && !this.gameCleared) {
      if (!this.lastConsoleDebugTime) this.lastConsoleDebugTime = 0;
      if (this.elapsedTime - this.lastConsoleDebugTime > 2.0) {
        this.lastConsoleDebugTime = this.elapsedTime;
        const cx = Math.floor((this.player.position.x + 8) / 16);
        const cz = Math.floor((this.player.position.z + 8) / 16);
        
        const memory = this.renderer.info.memory;
        const render = this.renderer.info.render;
        const fps = Math.round(1 / Math.max(0.001, deltaTime));
        
        const metrics = {
          time: this.elapsedTime.toFixed(1),
          fps,
          chunks: this.mapBuilder.loadedChunks.size,
          geometries: memory.geometries,
          textures: memory.textures,
          drawCalls: render.calls,
          triangles: render.triangles,
          heap: performance.memory?.usedJSHeapSize ?? 0,
          colliders: this.collisionWorld.blockers.length,
          monsters: this.enemyManager.enemies.length
        };
        
        console.log(`[PERF_METRICS] ${JSON.stringify(metrics)}`);
        
        if (this.debugEnabled) {
          console.table(metrics);
        }
      }
    }

    this.updateDebugHud();
    this.renderer.render(this.scene, this.camera);
    this.input.endFrame();
  }

  updateDebugHud() {
    if (!this.debugEnabled || !this.player) {
      return;
    }
    const debug = this.collisionWorld.getDebugState(this.player.position);
    debug.monsters = this.enemyManager?.enemies.map((enemy) => enemy.getDebugState()) || [];
    debug.nearestDoor = this.getNearestDoorDebug();
    debug.testSafeMode = this.testSafeMode;
    this.hud.setFloorDebug(debug);
  }

  getNearestDoorDebug() {
    if (!this.player || this.doors.length === 0) {
      return null;
    }

    let nearestDoor = null;
    let nearestDistance = Infinity;
    for (const door of this.doors) {
      const distance = door.distanceTo(this.player.position);
      if (distance < nearestDistance) {
        nearestDoor = door;
        nearestDistance = distance;
      }
    }

    if (!nearestDoor || nearestDistance > 5) {
      return null;
    }

    return {
      ...nearestDoor.getDebugInfo(),
      distance: nearestDistance,
    };
  }

  handleResize() {
    this.camera.aspect = window.innerWidth / window.innerHeight;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(window.innerWidth, window.innerHeight);
  }

  async warmUpRenderer() {
    const previousIntensity = this.flashlight.intensity;
    const previousPosition = this.camera.position.clone();
    const previousRotation = this.camera.rotation.clone();

    // Warm up with flashlight ON
    this.flashlight.visible = true;
    this.flashlight.intensity = this.flashlightController?.defaultIntensity || previousIntensity || 11.5;

    // Activate all pool lights at full intensity so Three.js compiles the
    // shader variant with max PointLights ONCE at startup, not on first chunk load.
    for (const pl of this._pointLightPool) {
      pl.intensity = 3.5;
      pl.position.set(0, 2.5, 0);
    }

    this.renderer.compile(this.scene, this.camera);
    this.renderer.render(this.scene, this.camera);

    // Warm up with flashlight OFF
    this.flashlight.intensity = 0;
    this.renderer.compile(this.scene, this.camera);
    this.renderer.render(this.scene, this.camera);

    // Park pool lights off-screen again
    for (const pl of this._pointLightPool) {
      pl.intensity = 0;
      pl.position.set(0, -9999, 0);
    }

    this.flashlight.intensity = previousIntensity;
    this.camera.position.copy(previousPosition);
    this.camera.rotation.copy(previousRotation);
    await new Promise((resolve) => requestAnimationFrame(resolve));
  }

  updateGlitch(deltaTime, enemyState) {
    for (const event of enemyState?.detectionEvents || []) {
      const isFirstDetectionScare = this.firstDetectionScareReady;
      if (isFirstDetectionScare) {
        this.firstDetectionScareReady = false;
        const closeness = Math.max(0, 1 - Math.min(1, (event.distance ?? 8) / 8));
        this.detectionFreezeTimer = Math.max(this.detectionFreezeTimer, 0.095 + closeness * 0.055);
        this.detectionFreezeThreat = Math.max(this.detectionFreezeThreat, enemyState?.threat || event.strength || 0.85);
      }
      this.glitchController.trigger({
        strength: event.strength,
        full: event.full,
        firstDetection: isFirstDetectionScare,
        distance: event.distance,
      });
      this.hud.setStatus(`${event.label}에게 들켰습니다.`, 900);
    }

    this.glitchController.update(deltaTime, {
      threat: enemyState?.threat || 0,
    });
  }

  handlePointerLockChange() {
    if (
      this.isStarted
      && !this.isPaused
      && !this.gameOver
      && !this.gameCleared
      && !this.cutsceneEvent
      && document.pointerLockElement !== this.renderer.domElement
    ) {
      this.pause();
    }
  }

  togglePause() {
    if (this.isPaused) {
      this.resume();
    } else {
      this.pause();
    }
  }

  pause() {
    if (!this.isStarted || this.isPaused || this.gameOver || this.gameCleared) {
      return;
    }

    this.isPaused = true;
    document.exitPointerLock?.();
    this.input.clearKey("escape");
    this.input.consumePointerDelta();
    this.hud.showPause();
    this.hud.setStatus("게임이 일시정지되었습니다.");
  }

  resume() {
    if (!this.isStarted || !this.isPaused || this.gameOver || this.gameCleared) {
      return;
    }

    this.isPaused = false;
    this.hud.hidePause();
    this.input.requestPointerLock();
    this.hud.setStatus("다시 숨을 고릅니다.", 1200);
  }

  setMouseSensitivityScale(scale) {
    const safeScale = Number.isFinite(scale) ? scale : 1;
    this.player.setMouseSensitivity(PLAYER_CONFIG.mouseSensitivity * safeScale);
    this.hud.setMouseSensitivityDisplay(safeScale);
  }

  quitToTitle() {
    this.resetRunState();
    this.isStarted = false;
    this.isPaused = false;
    document.exitPointerLock?.();
    this.hud.hidePause();
    this.hud.hideCaught();
    this.hud.hideClear();
    this.hud.showStart();
    this.hud.setStatus("화면을 클릭하면 게임이 시작됩니다.");
  }

  resetRunState() {
    this.gameOver = false;
    this.gameCleared = false;
    this.keyCount = 0;
    this.cabinetEvent = null;
    this.cutsceneEvent = null;
    this.firstDetectionScareReady = true;
    this.detectionFreezeTimer = 0;
    this.detectionFreezeThreat = 0;
    this.collisionWorld.clearDropAttempt();
    for (const event of this.mirrorEvents) {
      event.reset();
    }
    this.horrorEventManager?.reset();
    this.glitchController.reset();
    this.testSafeMode = false;
    this.player.exitCabinet();

    // Reset Lovely Doll states
    if (this.lovelyDolls) {
      for (const doll of this.lovelyDolls) {
        this.scene.remove(doll.group);
        doll.dispose();
      }
    }
    this.lovelyDolls = [];
    this.spawnedDollIds.clear();
    this.dollCountFound = 0;
    this.collectedKeyIds.clear();

    if (this.mapBuilder) {
      for (const chunk of this.mapBuilder.loadedChunks.values()) {
        this.mapBuilder.generator.destroyChunk(chunk.cx, chunk.cz);
      }
      this.mapBuilder.loadedChunks.clear();
      this.mapBuilder.doors = [];
      this.mapBuilder.keys = [];
      this.mapBuilder.cabinets = [];
      this.mapBuilder.finalExit = null;
      const map = this.mapBuilder.build();
      this.doors = map.doors;
      this.keys = map.keys;
      this.cabinets = map.cabinets;
      this.finalExit = map.finalExit;
    }

    this.player.setPosition(new THREE.Vector3(0, 0, 0));
    this.player.resetLook(0, 0);
    this.player.setInteractables(
      [...this.doors, ...this.keys, ...this.cabinets, this.finalExit].filter(Boolean),
      this.createInteractionContext(),
    );
    this.flashlightController.reset();
    this.enemyManager.reset(this.doors);
    this.enemyManager.endCabinetInvestigations();
    for (const door of this.doors) {
      door.isOpen = false;
      door.openAmount = 0;
    }
    for (const key of this.keys) {
      key.reset();
    }
    for (const cabinet of this.cabinets) {
      cabinet.reset();
    }
  }

  collectKey(key) {
    if (key.isCollected || key.isAvailable === false || this.gameOver || this.gameCleared) {
      return;
    }

    key.collect();
    if (this.collectedKeyIds) {
      this.collectedKeyIds.add(key.id);
    }
    this.keyCount += 1;
    this.hud.setStatus(`${key.label}를 얻었습니다. 열쇠 ${this.keyCount}/${this.keys.length}`, 1700);
  }

  revealKeyById(keyId, position) {
    const key = this.keys.find((item) => item.id === keyId);
    if (!key) {
      console.warn(`[Game] revealKeyById failed: missing key ${keyId}`);
      return;
    }
    key.revealAt(position);
  }

  enterCabinet(cabinet, options = {}) {
    if (this.gameOver || this.gameCleared || this.player.isHidden) {
      return;
    }

    cabinet.occupied = true;
    this.player.enterCabinet(cabinet);
    const chasingEnemy = this.enemyManager.getClosestChasingEnemy(this.player.position);

    if (!chasingEnemy) {
      this.hud.setStatus("캐비넷 안으로 몸을 숨겼습니다.", 1600);
      return;
    }

    const forcedOutcome = options.forceOutcome;
    const caught = forcedOutcome === "caught"
      || (forcedOutcome !== "safe" && Math.random() < CABINET_CONFIG.deathChance);
    chasingEnemy.beginCabinetInvestigation(cabinet);
    this.cabinetEvent = {
      cabinet,
      enemy: chasingEnemy,
      outcome: caught ? "caught" : "safe",
      timer: 0,
      arrived: false,
    };
    this.hud.setStatus("쫓아오던 발소리가 캐비넷 앞에서 멈춥니다.", 1800);
  }

  exitCabinet() {
    if (!this.player.isHidden) {
      return;
    }

    const interruptedEvent = this.cabinetEvent;
    this.cabinetEvent = null;

    if (this.player.hiddenCabinet) {
      this.player.hiddenCabinet.occupied = false;
    }
    this.player.exitCabinet();

    if (interruptedEvent?.enemy) {
      interruptedEvent.enemy.resumeChaseFromCabinet(this.player.position);
      this.hud.setStatus("캐비넷을 박차고 나오자 발소리가 다시 쫓아옵니다.", 1300);
      return;
    }

    this.hud.setStatus("캐비넷 밖으로 조용히 나왔습니다.", 1300);
  }

  canExitCabinet() {
    return true;
  }

  getHiddenPrompt() {
    if (!this.cabinetEvent) {
      return "E - 캐비넷에서 나오기";
    }

    return this.cabinetEvent.outcome === "caught"
      ? "E - 캐비넷에서 뛰쳐나가기"
      : "E - 캐비넷에서 나오기 (숨죽이고 기다리는 중)";
  }

  updateCabinetEvent(deltaTime) {
    if (!this.cabinetEvent || this.gameOver || this.gameCleared) {
      return;
    }

    const { cabinet, enemy } = this.cabinetEvent;
    const guardPosition = cabinet.getGuardPosition();
    const enemyDistance = Math.hypot(enemy.group.position.x - guardPosition.x, enemy.group.position.z - guardPosition.z);
    if (enemyDistance > 0.7) {
      return;
    }

    if (!this.cabinetEvent.arrived) {
      this.cabinetEvent.arrived = true;
      this.cabinetEvent.timer = 0;
    }

    this.cabinetEvent.timer += deltaTime;
    if (
      this.cabinetEvent.outcome === "caught"
      && this.cabinetEvent.timer >= CABINET_CONFIG.caughtDelaySeconds
    ) {
      this.handleCaught("캐비넷 문이 열렸습니다.");
      return;
    }

    if (
      this.cabinetEvent.outcome === "safe"
      && this.cabinetEvent.timer >= CABINET_CONFIG.safeWaitSeconds
    ) {
      enemy.endCabinetInvestigation();
      this.cabinetEvent = null;
      this.hud.setStatus("발소리가 멀어졌습니다.", 1600);
    }
  }

  updateMirrorEvents(deltaTime) {
    if (!this.isStarted || this.isPaused || this.gameOver || this.gameCleared) {
      return;
    }

    this.cutsceneEvent = null;
    for (const event of this.mirrorEvents) {
      event.update(deltaTime);
      if (event.blocksPlayerControl) {
        this.cutsceneEvent = event;
      }
    }
  }

  tryClearFinal() {
    if (this.keyCount < this.keys.length) {
      const remaining = this.keys.length - this.keyCount;
      this.hud.setStatus(`아직 열쇠가 ${remaining}개 부족합니다.`, 1600);
      return;
    }

    this.clearGame();
  }

  clearGame() {
    this.gameCleared = true;
    this.cabinetEvent = null;
    document.exitPointerLock?.();

    this.hud.showClear({
      title: `${this.chapterSession.title} Clear`,
      message: "장난감 상자가 열리고 복도의 소리가 사라졌습니다.",
      buttonText: "다시 시작",
    });
  }

  handleCaught(message = "발소리가 바로 뒤에서 멈췄습니다.") {
    if (this.testSafeMode) {
      this.hud.setStatus("테스트 안전 모드라 포획되지 않습니다.", 900);
      return;
    }

    this.gameOver = true;
    this.detectionFreezeTimer = 0;
    if (this.player.hiddenCabinet) {
      this.player.hiddenCabinet.occupied = false;
    }
    this.cabinetEvent = null;
    document.exitPointerLock?.();
    this.glitchController.trigger({ strength: 1.15, full: true });
    this.hud.showCaught(message);
  }

  toggleTestSafeMode() {
    this.testSafeMode = !this.testSafeMode;
    if (this.testSafeMode) {
      this.cabinetEvent = null;
      this.enemyManager?.setTestSafeMode(true);
      this.hud.setThreat(0);
      this.glitchController.reset();
      this.hud.setStatus("테스트 안전 모드 ON: 발각/사망 비활성화", 1800);
      return;
    }

    this.hud.setStatus("테스트 안전 모드 OFF", 1400);
  }

  updateBackrooms(deltaTime) {
    if (!this.player || !this.mapBuilder) {
      return;
    }

    // 1. Update loaded chunks — throttled to chunk-boundary crossings only.
    // Chunk coordinate changes when the player crosses a 16m boundary.
    const px = this.player.position.x;
    const pz = this.player.position.z;
    const cx = Math.floor((px + 8) / 16);
    const cz = Math.floor((pz + 8) / 16);
    const chunkChanged = cx !== this._lastPlayerChunkCx || cz !== this._lastPlayerChunkCz;

    // Always drive the sliced-loading queue every frame (cheap: processes ≤1 chunk/frame)
    const changed = this.mapBuilder.updateLoadedChunks(this.player.position, chunkChanged);
    if (changed) {
      this.doors = this.mapBuilder.doors;
      this.keys = this.mapBuilder.keys;
      this.cabinets = this.mapBuilder.cabinets;
      this.finalExit = this.mapBuilder.finalExit;
      this.player.setInteractables(
        [...this.doors, ...this.keys, ...this.cabinets, this.finalExit].filter(Boolean),
        this.createInteractionContext(),
      );
    }
    if (chunkChanged) {
      this._lastPlayerChunkCx = cx;
      this._lastPlayerChunkCz = cz;
    }

    // 2. Manage ceiling lights via the fixed PointLight pool.
    // Collect all panels from loaded chunks, sort by distance, assign pool slots.
    // Pool lights are NEVER added/removed — only position and intensity change.
    this._flickerAccum += deltaTime;
    const doFlicker = this._flickerAccum >= 0.1; // throttle flicker to 10 Hz
    if (doFlicker) this._flickerAccum = 0;

    const playerPos = this.player.position;
    const allPanels = [];
    for (const chunk of this.mapBuilder.loadedChunks.values()) {
      for (const light of chunk.lights) {
        const gx = chunk.center.x + light.localPos.x;
        const gz = chunk.center.z + light.localPos.z;
        const gy = chunk.center.y + light.localPos.y;
        const dx = gx - playerPos.x;
        const dz = gz - playerPos.z;
        const distSq = dx * dx + dz * dz;
        allPanels.push({ light, gx, gy, gz, distSq });

        // Flicker logic — update emissive mesh color (throttled)
        if (doFlicker && light.isFlickering) {
          light.flickerTimer -= 0.1;
          if (light.flickerTimer <= 0) {
            const isOff = Math.random() < 0.25;
            if (isOff) {
              light.mesh.material.color.setHex(0x3a3930);
              light.currentIntensity = 0;
              light.flickerTimer = 0.05 + Math.random() * 0.2;
            } else {
              light.mesh.material.color.setHex(0xfffee4);
              light.currentIntensity = light.baseIntensity;
              light.flickerTimer = 1.0 + Math.random() * 5.0;
            }
          }
        }
      }
    }

    // Sort panels closest-first and assign pool slots
    allPanels.sort((a, b) => a.distSq - b.distSq);
    const budget = this._POINT_LIGHT_BUDGET;
    for (let i = 0; i < budget; i++) {
      const pl = this._pointLightPool[i];
      if (i < allPanels.length) {
        const { light, gx, gy, gz, distSq } = allPanels[i];
        const inRange = distSq < 24 * 24;
        if (inRange) {
          pl.position.set(gx, gy, gz);
          const targetIntensity = light.currentIntensity !== undefined
            ? light.currentIntensity
            : light.baseIntensity;
          pl.intensity = targetIntensity || 3.5;
          // Link panel to pool slot so flicker can update it
          light.pooledLight = pl;
        } else {
          pl.position.set(0, -9999, 0); // park off-screen
          pl.intensity = 0;
          light.pooledLight = null;
        }
      } else {
        pl.position.set(0, -9999, 0);
        pl.intensity = 0;
      }
    }

    // 3. Teleport far enemies closer to player (runs every frame but is a simple distance check)
    if (this.enemyManager && this.elapsedTime > 5) {
      for (const enemy of this.enemyManager.enemies) {
        if (enemy.config.id === "hwacat-angry" && !enemy.isDynamic) {
          continue;
        }

        const distance = enemy.group.position.distanceTo(playerPos);
        if (distance > 52) {
          const ecx = Math.floor((playerPos.x + 8) / 16);
          const ecz = Math.floor((playerPos.z + 8) / 16);
          const candidates = [];

          for (let ddx = -2; ddx <= 2; ddx++) {
            for (let ddz = -2; ddz <= 2; ddz++) {
              if (ddx === 0 && ddz === 0) continue;
              const key = `${ecx + ddx},${ecz + ddz}`;
              const chunk = this.mapBuilder.loadedChunks.get(key);
              if (chunk) {
                const spawnPos = chunk.center.clone();
                const hasLos = this.collisionWorld.hasLineOfSight(playerPos, spawnPos);
                if (!hasLos) {
                  candidates.push(spawnPos);
                }
              }
            }
          }

          if (candidates.length > 0) {
            const targetSpawn = candidates[Math.floor(Math.random() * candidates.length)];
            enemy.group.position.copy(targetSpawn);
            this.collisionWorld.snapToValidSurface(enemy.group.position, { actorId: enemy.config.id });
            enemy.patrolPath = [];
            enemy.patrolPathGoal = null;
            enemy.chasePath = [];
            enemy.chasePathGoal = null;
          }
        }
      }
    }
    // checkInvisibleBlockers() removed — it scanned every blocker via scene.getObjectByName
    // on every frame (O(n*m) cost), which was a major source of hidden CPU spikes.
  }

  updateLovelyDolls(deltaTime) {
    if (!this.lovelyDolls) return;
    for (let i = this.lovelyDolls.length - 1; i >= 0; i--) {
      const doll = this.lovelyDolls[i];
      doll.update(deltaTime);
    }
  }

  removeLovelyDoll(doll) {
    if (!this.lovelyDolls) return;
    this.lovelyDolls = this.lovelyDolls.filter(d => d !== doll);
  }
}
