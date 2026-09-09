// Happy Toy의 최상위 조립 모듈입니다.
// 렌더러, 장면, 맵, 플레이어, 적, HUD, 루프를 생성하고 서로 연결합니다.

import * as THREE from "three";
import { createChapterSession, CHAPTERS } from "../config/chapterConfig.js";
import { getMapId } from "../world/schoolMaze.js";
import { CABINET_CONFIG, CAMERA_CONFIG, PLAYER_CONFIG, WORLD_CONFIG, SAFE_LIGHT_CONFIG, LIGHTING_CONFIG, STALKER_CONFIG } from "../config/gameConfig.js";

import { CollisionWorld } from "../world/CollisionWorld.js";
import { EnemyManager } from "../entities/EnemyManager.js";
import { GlitchController } from "../effects/GlitchController.js";
import { HorrorEventManager } from "../events/HorrorEventManager.js";
import { DreadDirector } from "../events/DreadDirector.js";
import { StoryDirector } from "../events/StoryDirector.js";
import { FloorHuntDirector } from "../events/FloorHuntDirector.js";
import { MirrorHwacatEvent } from "../events/MirrorHwacatEvent.js";
import { Hud } from "../ui/Hud.js";
import { Input } from "./Input.js";
import { Loop } from "./Loop.js";
import { MapBuilder } from "../world/MapBuilder.js";
import { FlashlightController } from "../player/FlashlightController.js";
import { PlayerController } from "../player/PlayerController.js";
import { ItemSystem } from "../world/ItemSystem.js";
import { ParticleSystem } from "../effects/ParticleSystem.js";
import { MenuSystem } from "../ui/MenuSystem.js";
import { soundManager } from "../audio/SoundManager.js";
import { VoiceAnnouncer } from "../audio/VoiceAnnouncer.js";
import { MonsterIntroManager } from "../events/MonsterIntroManager.js";



export class Game {
  constructor(rootElement) {
    this.rootElement = rootElement;
    this.scene = new THREE.Scene();
    // Keep the distance void oppressive without crushing every unlit surface
    // to display black after ACES. This remains much darker than any material.
    const atmosphericBlack = LIGHTING_CONFIG.fogColor ?? 0x040201;
    this.scene.background = new THREE.Color(atmosphericBlack);
    this.scene.fog = new THREE.Fog(atmosphericBlack, LIGHTING_CONFIG.fogNear, LIGHTING_CONFIG.fogFar);


    this.camera = new THREE.PerspectiveCamera(
      CAMERA_CONFIG.fov,
      window.innerWidth / window.innerHeight,
      CAMERA_CONFIG.near,
      CAMERA_CONFIG.far,
    );

    this.renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: false,
      powerPreference: "high-performance",
    });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.shadowMap.autoUpdate = true;
    this.renderer.toneMappingExposure = LIGHTING_CONFIG.rendererExposure ?? 0.8;

    this.rootElement.appendChild(this.renderer.domElement);

    this.hud = new Hud();
    const urlParams = new URLSearchParams(window.location.search);
    this.debugEnabled = urlParams.get("debug") === "1";
    this.chapterSession = createChapterSession();
    this.mapConfig = this.chapterSession.mapConfig;
    this.enemyConfigs = this.chapterSession.enemyConfigs;
    this.requiredKeyCount = Math.max(1, this.mapConfig?.keys?.length || 4);
    this.keyHomes = new Map();
    for (const key of this.mapConfig?.keys || []) {
      this.keyHomes.set(key.id, {
        id: key.id,
        label: key.label,
        position: new THREE.Vector3(...key.position),
        isAvailable: key.initiallyVisible !== false,
      });
    }
    this.hud.setDebugEnabled(this.debugEnabled);
    this.input = new Input(this.renderer.domElement);
    this.collisionWorld = new CollisionWorld();
    this.doors = [];
    this.keys = [];
    this.cabinets = [];
    this.safeLights = [];
    this.loreNotes = [];
    this.activatedSafeLightKeys = new Set();
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
    this.spawnedWeepingAngel1F = false;
    this.spawnedWeepingAngel2F = false;
    this.activatedSafeLightKeys = new Set();
    this.activatedSafeLights = this.activatedSafeLightKeys;
    this.isStarted = false;
    this.pendingStart = false;
    this.wasPointerLocked = false;
    this.isPaused = false;

    this.cabinetEvent = null;
    this.cutsceneEvent = null;
    this.elapsedTime = 0;
    this.playTime = 0;
    this.stalkerReleased = false;
    this._storyBeats = new Set();
    this._lastPaAt = 0;
    this.deathSequence = null;
    this.assetsReady = false;
    this.testSafeMode = false;
    this.ghostMode = false;
    this.cinematicLightScale = 1;
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
    this.handlePlayGesture = this.handlePlayGesture.bind(this);
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
    this.safeLights = map.safeLights || [];
    this.loreNotes = map.loreNotes || [];
    this.finalExit = map.finalExit;
    this.syncKeyHomes();
    this.player = new PlayerController(this.camera, this.input, this.collisionWorld, this.hud);
    this.player.setPosition(map.playerStart);
    this.player.resetLook(0, 0);
    this.flashlightController = new FlashlightController(this.flashlight, this.input, this.hud, this);
    this.horrorEventManager = new HorrorEventManager(
      this.scene,
      this.player,
      this.doors,
      this.hud,
      this.horrorLights,
    );
    this.enemyManager = new EnemyManager(this.scene, this.collisionWorld, this.doors, this.hud, this.enemyConfigs);
    this.itemSystem = new ItemSystem(this.scene, this.enemyManager, this.hud);
    this.particleSystem = new ParticleSystem(this.scene);
    this.menuSystem = new MenuSystem(this);
    this.monsterIntroManager = new MonsterIntroManager(this);
    this.dreadDirector = new DreadDirector(this);
    this.storyDirector = new StoryDirector(this);
    this.floorHuntDirector = new FloorHuntDirector(this);
    this.floorHuntDirector.ensureSilhouette();
    this.voiceAnnouncer = new VoiceAnnouncer();


    this.itemSystem.spawnPickups([
      [2.4, 0, 3.6],
      [-2.6, 0, 4.2],
      [32.0, 0, 0.0],
      [96.0, 0, 0.0],
      [10.5, -5, 30.4],
      [-24.2, 5, -18.6],
    ]);

    this.refreshInteractables();

    const hwacat2FGalleryConfig = {
      id: "hwacat-mirror-event",
      triggerPosition: [-31.0, 5.0, -22.0],
      triggerRadius: 2.4,
      spawnPosition: [-35.5, 5.0, -22.0],
      spawnYaw: Math.PI / 2,
      lookAtPosition: [-35.5, 6.2, -22.0],
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
      paintingDropTargetPosition: [-35.5, 5.08, -22.0],
      paintingDropTargetRotation: [-Math.PI / 2, 0, 0.08],
      rewardKeyId: "key-hwacat",
    };
    this.mirrorEvents = [new MirrorHwacatEvent(hwacat2FGalleryConfig, {
      scene: this.scene,
      camera: this.camera,
      player: this.player,
      enemyManager: this.enemyManager,
      hud: this.hud,
      revealKeyById: (keyId, position) => this.revealKeyById(keyId, position),
      isInvincible: () => this.ghostMode,
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
    this.handleResize();
    this.syncWorldPreview();
    if (this.pendingStart || this.isStarted) {
      this.start();
    } else {
      this.hud.showClickToPlay();
      this.hud.setStatus("화면을 클릭하면 게임이 시작됩니다.");
    }
    this.loop.start();
  }

  setupLighting() {
    this.ambientLight = new THREE.AmbientLight(
      LIGHTING_CONFIG.ambientColor,
      LIGHTING_CONFIG.ambientIntensity,
    );
    this.scene.add(this.ambientLight);

    this.hemisphereLight = new THREE.HemisphereLight(
      new THREE.Color(LIGHTING_CONFIG.hemisphereSkyColor),
      new THREE.Color(LIGHTING_CONFIG.hemisphereGroundColor),
      LIGHTING_CONFIG.hemisphereIntensity,
    );
    this.scene.add(this.hemisphereLight);


    this.flashlight = new THREE.SpotLight(
      LIGHTING_CONFIG.flashlightColor,
      LIGHTING_CONFIG.flashlightIntensity,
      LIGHTING_CONFIG.flashlightRange,
      LIGHTING_CONFIG.flashlightAngle,
      LIGHTING_CONFIG.flashlightPenumbra,
      LIGHTING_CONFIG.flashlightDecay ?? 1.15,
    );
    this.flashlight.position.set(0.12, -0.08, 0.1);
    this.flashlight.target.position.set(0, -0.32, -1);
    this.flashlight.castShadow = true;
    this.flashlight.shadow.mapSize.width = LIGHTING_CONFIG.flashlightShadowMapSize || 512;
    this.flashlight.shadow.mapSize.height = LIGHTING_CONFIG.flashlightShadowMapSize || 512;
    this.flashlight.shadow.camera.near = LIGHTING_CONFIG.flashlightShadowNear || 0.5;
    this.flashlight.shadow.camera.far = LIGHTING_CONFIG.flashlightShadowFar || 62;
    this.flashlight.shadow.bias = -0.00018;
    this.flashlight.shadow.normalBias = 0.035;
    this.flashlight.shadow.radius = 2.1;

    this.flashlight.visible = true;
    this.camera.add(this.flashlight);
    this.camera.add(this.flashlight.target);

    this.flashlightFill = new THREE.PointLight(
      LIGHTING_CONFIG.flashlightColor,
      0,
      LIGHTING_CONFIG.flashlightFillRange ?? 18,
      1.05,
    );
    this.flashlightFill.position.set(0.05, -0.12, -0.35);
    this.camera.add(this.flashlightFill);
    this.scene.add(this.camera);

    // Pre-allocate the fixed PointLight pool. All lights live in the scene permanently.
    // We only update their position/intensity — never add/remove during gameplay.
    const ceilingLightColor = new THREE.Color(LIGHTING_CONFIG.ceilingLightColor);
    ceilingLightColor.lerp(new THREE.Color(0xffd7ae), 0.58);
    for (let i = 0; i < this._POINT_LIGHT_BUDGET; i++) {
      const pl = new THREE.PointLight(
        ceilingLightColor,
        0,
        Math.max(LIGHTING_CONFIG.ceilingLightRange || 14, 16),
        LIGHTING_CONFIG.ceilingLightDecay || 2,
      );
      pl.castShadow = false;
      pl.position.set(0, -9999, 0); // park far off-screen until assigned
      this.scene.add(pl);
      this._pointLightPool.push(pl);
    }

    // Pre-allocate the fixed SafeLight PointLight pool.
    this._safeLightPool = [];
    this._SAFE_LIGHT_BUDGET = SAFE_LIGHT_CONFIG.maxActiveLights || 8;
    for (let i = 0; i < this._SAFE_LIGHT_BUDGET; i++) {
      const pl = new THREE.PointLight(SAFE_LIGHT_CONFIG.color, 0, SAFE_LIGHT_CONFIG.distance, SAFE_LIGHT_CONFIG.decay);
      pl.castShadow = false;
      pl.position.set(0, -9999, 0); // park far off-screen until assigned
      this.scene.add(pl);
      this._safeLightPool.push(pl);
    }
  }

  connectUi() {
    this.hud.startButton.addEventListener("click", this.start);
    this.hud.clickToPlayButton?.addEventListener("click", this.start);
    this.renderer.domElement.addEventListener("pointerdown", this.handlePlayGesture);
    this.rootElement.addEventListener("pointerdown", this.handlePlayGesture);
    document.addEventListener("pointerdown", this.handlePlayGesture);
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

  handlePlayGesture(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
      this.start();
      return;
    }
    if (target.closest(".pause-screen, .caught-screen, .clear-screen, input, select, textarea, a")) {
      return;
    }
    if (target.closest("#menu-system-root")) {
      if (target.closest("button") && !target.closest("#btn-start-game, #btn-retry-game, #btn-victory-replay")) {
        return;
      }
    }
    this.start();
  }

  refreshInteractables() {
    if (!this.player) return;
    this.player.setInteractables(
      [
        ...this.doors,
        ...this.keys,
        ...this.cabinets,
        ...this.safeLights,
        ...(this.loreNotes || []),
        ...(this.itemSystem?.getInteractables() || []),
        this.finalExit,
      ].filter(Boolean),
      this.createInteractionContext(),
    );
  }

  onLoreRead(note) {
    if (!note?.read) return;
    soundManager.playSFX("whisper");
    this.voiceAnnouncer?.announce("lore", note.body);
  }

  createInteractionContext() {

    return {
      hud: this.hud,
      game: this,
      getKeyCount: () => this.keyCount,
      getTotalKeys: () => this.getTotalKeys(),
      collectKey: (key) => this.collectKey(key),
      enterCabinet: (cabinet) => this.enterCabinet(cabinet),
      exitCabinet: () => this.exitCabinet(),
      canExitCabinet: () => this.canExitCabinet(),
      getHiddenPrompt: () => this.getHiddenPrompt(),
      tryClearFinal: (finalExit) => this.tryClearFinal(finalExit),
      isCleared: () => this.gameCleared,
      isSafeLightActivated: (key) => this.activatedSafeLightKeys.has(key),
      activateSafeLight: (safeLight) => this.activateSafeLight(safeLight),
    };
  }

  activateSafeLight(safeLight) {
    if (this.activatedSafeLightKeys.has(safeLight.stateKey)) return;
    safeLight.setActivated(true);
    this.activatedSafeLightKeys.add(safeLight.stateKey);
    this.updateSafeLightPool();
    this.hud.setStatus(`조명을 켰습니다 - ${safeLight.label}`, 1500);
  }

  start() {
    if (!this.assetsReady) {
      this.pendingStart = true;
      this.menuSystem?.hideMenu();
      this.hud.showClickToPlay("불러오는 중...");
      this.hud.setStatus("복도를 불러오는 중입니다. 끝나면 바로 시작합니다.");
      return;
    }

    if (this.isStarted && !this.isPaused) {
      this.tryPointerLock(true);
      return;
    }

    this.isStarted = true;
    this.pendingStart = false;
    this.isPaused = false;
    this.wasPointerLocked = false;
    this.applyDifficultySettings();
    soundManager.init();
    soundManager.resume();
    this.glitchController.primeAudio();
    this.hud.hideStart();
    this.hud.hidePause();
    this.hud.hideClickToPlay();
    this.menuSystem?.hideMenu();
    this.handleResize();
    this.flashlightController?.setEnabled(true, false);
    this.input?.clearKey?.("f");
    this.syncWorldPreview();
    this.tryPointerLock(false);
    this.hud.setStatus("손전등이 유일한 길입니다. 발소리가 나면 신발장에 숨으십시오.", 4200);
    this.voiceAnnouncer?.announce("start", "오늘은 하교하지 않습니다. 복도에서 기다리십시오.");
    this.floorHuntDirector?.ensureSilhouette?.();
  }

  tryPointerLock(notifyOnFail = false) {
    const request = this.input.requestPointerLock();
    request?.then?.((locked) => {
      if (locked || this.input.pointerLocked) {
        return;
      }
      if (notifyOnFail) {
        this.hud.setStatus("마우스 잠금을 쓸 수 없습니다. 클릭한 채로 시선을 돌리고 WASD로 이동하세요.", 2800);
      }
    });
  }

  syncWorldPreview() {
    if (!this.player || !this.mapBuilder) {
      return;
    }
    this.updateBackrooms(0);
    this.player.resetLook(this.player.yaw, this.player.pitch);
  }

  restart() {
    this.resetRunState();
    this.isStarted = true;
    this.pendingStart = false;
    this.isPaused = false;
    this.wasPointerLocked = false;
    this.applyDifficultySettings();
    this.hud.hideCaught();
    this.hud.hideClear();
    this.hud.hidePause();
    this.hud.hideStart();
    this.hud.hideClickToPlay();
    this.menuSystem?.hideMenu();
    this.handleResize();
    this.flashlightController?.setEnabled(true, false);
    this.input?.clearKey?.("f");
    this.syncWorldPreview();
    this.tryPointerLock(false);
    this.hud.setStatus("다시 복도 한가운데에 섰습니다.", 1800);
  }


  applyDifficultySettings() {
    const mode = this.menuSystem?.currentMode || "normal";
    let speedMult = 1.0;
    let detectionMult = 1.0;
    let batteryDrainMult = 1.0;
    let staminaRegenMult = 1.0;

    if (mode === "nightmare") {
      speedMult = 1.2;
      detectionMult = 1.25;
      batteryDrainMult = 1.5;
      staminaRegenMult = 0.8;
    } else if (mode === "hardcore") {
      speedMult = 1.4;
      detectionMult = 1.5;
      batteryDrainMult = 2.0;
      staminaRegenMult = 0.65;
    }

    if (this.enemyManager) {
      for (const enemy of this.enemyManager.enemies) {
        enemy.speedMultiplier = speedMult;
        enemy.detectionMultiplier = detectionMult;
      }
    }
    if (this.flashlightController) {
      this.flashlightController.drainMultiplier = batteryDrainMult;
    }
    if (this.player) {
      this.player.staminaRegenMultiplier = staminaRegenMult;
    }
  }


  update(deltaTime, options = {}) {
    if (this.input.consumePressed("0") || this.input.consumePressed("numpad0")) {
      this.toggleTestSafeMode();
    }
    if (this.input.consumePressed("`") || this.input.consumePressed("backquote")) {
      this.toggleGhostMode();
    }

    if (!this.isStarted && this.assetsReady) {
      if (
        this.input.consumePressed("w")
        || this.input.consumePressed("a")
        || this.input.consumePressed("s")
        || this.input.consumePressed("d")
        || this.input.consumePressed("f")
        || this.input.consumePressed(" ")
      ) {
        this.start();
      }
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

    if (!this.isStarted && !this.isPaused) {
      this.syncWorldPreview();
    }

    if (this.isStarted && !this.isPaused && !this.gameOver && !this.gameCleared && !this.cutsceneEvent) {
      this.playTime += deltaTime;
      this.tryReleaseCorridorStalker();
      this.updateBackrooms(deltaTime);
      this.updateLovelyDolls(deltaTime);
      this.updateWeepingAngels(deltaTime);
      this.monsterIntroManager?.update(deltaTime);
      if (!this.monsterIntroManager?.blocksPlayerControl) {
        this.player.update(deltaTime);
      }
      this.updateFloorAtmosphere(deltaTime);
      this.flashlightController.update(deltaTime);
      this.itemSystem?.update(deltaTime);
      this.particleSystem?.update(deltaTime, this.player.position);
      this.horrorEventManager?.update(deltaTime);
      this.dreadDirector?.update(deltaTime);
      this.storyDirector?.update();
      this.floorHuntDirector?.update(deltaTime);
      if (this.gameCleared) {
        this.renderer.render(this.scene, this.camera);
        this.input.endFrame();
        return;
      }


      // Heartbeat audio update based on closest monster
      let minDist = 999;
      if (this.enemyManager && this.enemyManager.enemies) {
        for (const enemy of this.enemyManager.enemies) {
          if (enemy.isDormant || !enemy.isSameLevelAs(this.player.position)) continue;
          const dist = Math.hypot(enemy.group.position.x - this.player.position.x, enemy.group.position.z - this.player.position.z);
          if (dist < minDist) minDist = dist;
        }
      }
      soundManager.updateHeartbeat(deltaTime, minDist);
      const chasing = (this.enemyManager?.enemies || []).some((enemy) => (
        !enemy.isDormant && (enemy.state === "chase" || enemy.state === "flee")
      ));
      soundManager.setChaseActive(chasing);

      // Compass target
      const compassTarget = this.getCompassTarget();
      this.hud.updateCompass(this.player.position, compassTarget?.position, this.player.yaw,
        compassTarget === this.finalExit ? "제단" : "혼");
      this.hud.setKeyCount(this.keyCount, this.getTotalKeys());

      const enemyState = this.enemyManager?.update(deltaTime, {
        position: this.player.position,
        isHidden: this.player.isHidden,
        isUndetectable: this.isInvincible,
        isMoving: this.player.isMoving,
        isSprinting: this.player.isSprinting,
      });
      this.updateGlitch(deltaTime, enemyState);
      if (enemyState?.caught) {
        this.handleCaught();
      }
      this.maybePullLockerHunt();
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
    if (this.gameOver && this.deathSequence && !this.deathSequence.shown) {
      this.updateDeathSequence(deltaTime);
    }
    if (!options.skipRender) {
      this.renderer.render(this.scene, this.camera);
    }
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
    debug.ghostMode = this.ghostMode;
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
    for (const pl of this._safeLightPool) {
      pl.intensity = SAFE_LIGHT_CONFIG.intensity;
      pl.position.set(0, 1.5, 0);
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
    for (const pl of this._safeLightPool) {
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
      hunt: this.dreadDirector?.phase === "hunt",
    });
  }

  handlePointerLockChange() {
    const isLocked = document.pointerLockElement === this.renderer.domElement;
    if (isLocked) {
      this.wasPointerLocked = true;
      this.input.pointerLockBlocked = false;
      return;
    }
    if (
      this.isStarted
      && !this.isPaused
      && !this.gameOver
      && !this.gameCleared
      && !this.cutsceneEvent
      && this.wasPointerLocked
    ) {
      this.wasPointerLocked = false;
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
    this.wasPointerLocked = false;
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
    this.hud.showClickToPlay();
    this.menuSystem?.showTitleScreen();
    this.hud.setStatus("화면을 클릭하면 게임이 시작됩니다.");
  }

  resetRunState() {
    this.gameOver = false;
    this.gameCleared = false;
    this.spawnedWeepingAngel1F = false;
    this.spawnedWeepingAngel2F = false;
    this.keyCount = 0;
    this.playTime = 0;
    this.stalkerReleased = false;
    this._stairWaitAnnounced = false;
    this._storyBeats = new Set();
    this._lastPaAt = 0;
    this.deathSequence = null;
    document.body.classList.remove("death-veil");
    this.cabinetEvent = null;
    this.cutsceneEvent = null;
    this.firstDetectionScareReady = true;
    this.detectionFreezeTimer = 0;
    this.detectionFreezeThreat = 0;
    this.collisionWorld.clearDropAttempt();
    for (const event of this.mirrorEvents) {
      event.reset();
    }
    this.monsterIntroManager?.reset();
    this.horrorEventManager?.reset();
    this.dreadDirector?.reset();
    this.storyDirector?.reset();
    this.floorHuntDirector?.reset();

    this.glitchController.reset();
    this.testSafeMode = false;
    this.ghostMode = false;
    this.cinematicLightScale = 1;
    this.hud?.setGhostMode?.(false);
    this.player.exitCabinet();
    this.hud?.setHidden(false);

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
    this.activatedSafeLightKeys.clear();

    if (this.mapBuilder) {
      for (const chunk of this.mapBuilder.loadedChunks.values()) {
        this.mapBuilder.generator.destroyChunk(chunk.cx, chunk.cz);
      }
      this.mapBuilder.loadedChunks.clear();
      this.mapBuilder.doors = [];
      this.mapBuilder.keys = [];
      this.mapBuilder.cabinets = [];
      this.mapBuilder.safeLights = [];
      this.mapBuilder.loreNotes = [];
      this.mapBuilder.finalExit = null;
      const map = this.mapBuilder.build();
      this.doors = map.doors;
      this.keys = map.keys;
      this.cabinets = map.cabinets;
      this.safeLights = map.safeLights || [];
      this.loreNotes = map.loreNotes || [];
      this.finalExit = map.finalExit;
      this.syncKeyHomes();
    }

    if (this.itemSystem) {
      this.itemSystem.reset();
    }

    this.player.setPosition(new THREE.Vector3(0, 0, 0));
    this.player.resetLook(0, 0);
    this.refreshInteractables();

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

  getTotalKeys() {
    return this.requiredKeyCount || Math.max(1, this.mapConfig?.keys?.length || 4);
  }

  syncKeyHomes() {
    if (!this.keyHomes) this.keyHomes = new Map();
    for (const key of this.keys || []) {
      this.keyHomes.set(key.id, {
        id: key.id,
        label: key.label,
        position: key.position.clone(),
        isAvailable: key.isAvailable,
      });
    }
  }

  getCompassTarget() {
    if (this.keyCount >= this.getTotalKeys()) return this.finalExit;
    const position = this.player.position;
    const score = (point) => Math.hypot(point.x - position.x, point.z - position.z)
      + (Math.abs(point.y - position.y) > 1.8 ? 40 : 0);
    const live = this.keys.filter((key) => key.isAvailable && !key.isCollected);
    if (live.length > 0) {
      return live.sort((a, b) => score(a.position) - score(b.position))[0];
    }
    const ghosts = [];
    for (const home of this.keyHomes?.values() || []) {
      if (this.collectedKeyIds?.has(home.id)) continue;
      if (!home.isAvailable) continue;
      ghosts.push({ position: home.position, id: home.id });
    }
    return ghosts.sort((a, b) => score(a.position) - score(b.position))[0] || this.finalExit;
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
    const total = this.getTotalKeys();
    this.dreadDirector?.onRelic(key.position, this.keyCount, total);
    soundManager.playSFX("key_pickup");
    const remaining = total - this.keyCount;
    const voiceKey = this.keyCount >= total
      ? "keysDone"
      : this.keyCount === 3
        ? "key3"
        : this.keyCount === 2
          ? "key2"
          : "key";
    const line = this.keyCount >= total
      ? "모든 이름을 모았습니다. 제단으로 돌아가십시오."
      : this.keyCount === 3
        ? "이름이 셋입니다. 마지막 혼은 아직 액자 뒤에 있습니다."
        : this.keyCount === 2
          ? "이름이 둘입니다. 복도가 당신을 세기 시작합니다."
          : `이름을 찾았습니다. ${remaining}개가 남았습니다.`;
    this.voiceAnnouncer?.announce(voiceKey, line);
    this.hud.setStatus(this.keyCount >= total
      ? "모든 혼을 모았습니다. 나침반 [4]을 따라 제단으로 돌아가십시오."
      : this.keyCount === 3
        ? "혼 3/4 · 2층 액자 사건이 마지막 이름을 숨기고 있습니다."
        : this.keyCount === 2
          ? "혼 2/4 · 별관과 지하, 2층이 당신을 세기 시작합니다."
          : `혼 ${this.keyCount}/${total} · 종이 울리기 전에 퇴로를 확보하십시오.`, 4500);
  }

  revealKeyById(keyId, position) {
    const key = this.keys.find((item) => item.id === keyId);
    if (!key) {
      console.warn(`[Game] revealKeyById failed: missing key ${keyId}`);
      return;
    }
    let point;
    if (Array.isArray(position) && position.length >= 3) {
      point = position;
    } else if (position && Number.isFinite(position.x)) {
      point = [position.x, position.y, position.z];
    } else {
      point = [key.position.x, key.position.y, key.position.z];
    }
    key.revealAt(point);
    if (this.keyHomes?.has(keyId)) {
      const home = this.keyHomes.get(keyId);
      home.isAvailable = true;
      home.position.set(point[0], point[1], point[2]);
    }
  }

  enterCabinet(cabinet, options = {}) {
    if (this.gameOver || this.gameCleared || this.player.isHidden) {
      return;
    }

    const chasingEnemy = this.enemyManager.getClosestChasingEnemy(this.player.position);
    const hunt = this.dreadDirector?.phase === "hunt";
    const nearby = chasingEnemy || (hunt ? this.enemyManager.getClosestAwakeEnemy(this.player.position) : null);
    const dist = nearby
      ? Math.hypot(nearby.group.position.x - this.player.position.x, nearby.group.position.z - this.player.position.z)
      : Infinity;
    const witnessed = this.enemyManager.enemies.some((enemy) =>
      !enemy.isDormant && enemy.isActivelyChasing() && enemy.hasVisualContact
      && enemy.isSameLevelAs(this.player.position)
      && this.collisionWorld.hasLineOfSight(enemy.group.position, this.player.position));
    cabinet.setOccupied?.(true);
    cabinet.occupied = true;
    this.player.enterCabinet(cabinet);
    this.hud?.setHidden(true);
    this.voiceAnnouncer?.announce("hide", "신발장 안으로. 호흡을 끊으십시오.");

    if (!nearby || dist > (CABINET_CONFIG.huntHidePullDistance ?? 20)) {
      this.hud.setStatus("신발장 안으로 몸을 숨겼습니다. 숨이 들리지 않게 하십시오.", 1800);
      return;
    }

    const forcedOutcome = options.forceOutcome;
    const heard = dist < 16;
    const huntCatch = hunt && heard && Math.random() < (CABINET_CONFIG.huntHideCatchChance ?? 0.38);
    const caught = forcedOutcome === "caught"
      || (forcedOutcome !== "safe" && (witnessed || huntCatch));
    nearby.beginCabinetInvestigation(cabinet);
    this.cabinetEvent = {
      cabinet,
      enemy: nearby,
      outcome: caught ? "caught" : "safe",
      timer: 0,
      arrived: false,
    };
    this.hud.setStatus(caught
      ? "신발장 문이 들켰습니다. [E]로 뛰쳐나와 시야를 끊으십시오."
      : "문이 닫혔습니다. 앞의 발소리가 멀어질 때까지 숨죽이십시오.", 3500);
  }

  exitCabinet() {
    if (!this.player.isHidden) {
      return;
    }

    const interruptedEvent = this.cabinetEvent;
    this.cabinetEvent = null;

    if (this.player.hiddenCabinet) {
      this.player.hiddenCabinet.setOccupied?.(false);
      this.player.hiddenCabinet.occupied = false;
    }
    this.player.exitCabinet();
    this.hud?.setHidden(false);

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
      soundManager.playSFX("door_open");
      this.hud.setStatus(this.cabinetEvent.outcome === "caught"
        ? "문고리가 돌아갑니다! [E] 지금 빠져나오십시오."
        : "문밖에서 숨소리가 들립니다. 아직 나가지 마십시오.", 2500);
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
    if (this.gameOver || this.gameCleared) return;
    if (this.keyCount < this.getTotalKeys()) {
      const remaining = this.getTotalKeys() - this.keyCount;
      this.hud.setStatus(`아직 혼이 ${remaining}개 부족합니다.`, 1600);
      return;
    }

    this.dreadDirector.beginRitual();
  }

  clearGame() {
    this.gameCleared = true;
    this.hud.setDread(0, "", "quiet");
    this.cabinetEvent = null;
    document.exitPointerLock?.();
    this.voiceAnnouncer?.announce("clear", "제단이 문을 삼켰습니다.");

    if (this.menuSystem) {
      this.menuSystem.showVictoryClear(this.elapsedTime);
    } else {
      this.hud.showClear({
        title: `${this.chapterSession.title} Clear`,
        message: "장난감 상자가 열리고 복도의 소리가 사라졌습니다.",
        buttonText: "다시 시작",
      });
    }
  }

  handleCaught(message = "발소리가 바로 뒤에서 멈췄습니다.") {
    if (this.isInvincible) {
      this.hud.setStatus(this.ghostMode ? "투명 상태라 포획되지 않습니다." : "테스트 안전 모드라 포획되지 않습니다.", 900);
      return;
    }
    if (this.gameOver || this.gameCleared) {
      return;
    }

    this.gameOver = true;
    this.hud.setDread(0.92, "출석이 끝났습니다", "hunt");
    this.voiceAnnouncer?.announce("death", "복도가 당신의 이름을 외웠습니다.");
    this.detectionFreezeTimer = 0;
    if (this.player.hiddenCabinet) {
      this.player.hiddenCabinet.setOccupied?.(false);
      this.player.hiddenCabinet.occupied = false;
    }
    this.cabinetEvent = null;
    document.exitPointerLock?.();
    this.glitchController.trigger({ strength: 1.15, full: true });
    this.flashlightController?.setEnabled(false, false);
    soundManager.playSFX("death_breath");
    this.deathSequence = { t: 0, shown: false, message };
    document.body.classList.add("death-veil");
  }

  updateDeathSequence(deltaTime) {
    const seq = this.deathSequence;
    if (!seq || seq.shown) {
      return;
    }
    seq.t += Math.min(deltaTime, 0.08);
    const enemy = this.enemyManager?.getClosestAwakeEnemy?.(this.player.position)
      || this.enemyManager?.getClosestChasingEnemy?.(this.player.position);
    if (enemy && this.camera) {
      const target = enemy.group.position.clone();
      target.y += Math.max(1.15, (enemy.config?.height ?? 1.7) * 0.62);
      this.camera.lookAt(target);
    }
    this.glitchController.update(deltaTime, { threat: 1 });
    if (seq.t >= 1.55) {
      seq.shown = true;
      if (this.menuSystem) {
        this.menuSystem.showGameOverScreamer();
      } else {
        this.hud.showCaught(seq.message);
      }
    }
  }

  tryReleaseCorridorStalker() {
    if (this.stalkerReleased || this.isInvincible) {
      return false;
    }
    const intro = this.monsterIntroManager?.events?.find((event) => event.constructor?.name === "UncatIntroEvent");
    if (intro?.state === "cutscene" || intro?.isControlLocked) {
      return false;
    }
    const grace = STALKER_CONFIG.graceSeconds ?? 14;
    if (this.playTime < grace) {
      return false;
    }
    this.stalkerReleased = true;
    const released = this.enemyManager?.releaseStalker(STALKER_CONFIG.id || "uncat", {
      spawn: STALKER_CONFIG.spawn || [0, 0, 16],
      hunt: true,
      playerPosition: this.player.position,
    });
    if (released) {
      soundManager.playSFX("school_chime");
      soundManager.playSFX("corridor_wind");
      this.voiceAnnouncer?.announce("hunt", "누군가 복도를 걷고 있습니다.");
    }
    return released;
  }

  maybePullLockerHunt() {
    if (this.cabinetEvent || !this.player?.isHidden || this.isInvincible) {
      return;
    }
    const cabinet = this.player.hiddenCabinet;
    if (!cabinet) {
      return;
    }
    const hunt = this.dreadDirector?.phase === "hunt" || this.stalkerReleased || this.playTime > 24;
    if (!hunt) {
      return;
    }
    const enemy = this.enemyManager?.getClosestAwakeEnemy(this.player.position);
    if (!enemy) {
      return;
    }
    const dist = Math.hypot(
      enemy.group.position.x - this.player.position.x,
      enemy.group.position.z - this.player.position.z,
    );
    if (dist > (CABINET_CONFIG.huntHidePullDistance ?? 20)) {
      return;
    }
    enemy.beginCabinetInvestigation(cabinet);
    const caught = this.dreadDirector?.phase === "hunt" && dist < 9 && Math.random() < 0.22;
    this.cabinetEvent = {
      cabinet,
      enemy,
      outcome: caught ? "caught" : "safe",
      timer: 0,
      arrived: false,
    };
    this.hud.setStatus(
      caught ? "신발장 앞에서 숨이 멈췄습니다." : "문 너머에서 손잡이를 더듬는 소리가 납니다.",
      2800,
    );
  }

  get isInvincible() {
    return Boolean(this.testSafeMode || this.ghostMode);
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

  toggleGhostMode() {
    this.ghostMode = !this.ghostMode;
    this.hud.setGhostMode(this.ghostMode);
    if (this.ghostMode) {
      this.cabinetEvent = null;
      this.enemyManager?.breakAggro();
      this.hud.setThreat(0);
      this.glitchController.reset();
      this.hud.setStatus("무적 모드 ON · 투명화 · 적에게 보이지 않음 (` 로 해제)", 2400);
      return;
    }
    this.hud.setStatus("무적 모드 OFF", 1400);
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
    const extraCenters = [];
    const enemyChunks = [];
    for (const enemy of this.enemyManager?.enemies || []) {
      if (enemy.isDormant) continue;
      extraCenters.push({ x: enemy.group.position.x, z: enemy.group.position.z });
      enemyChunks.push(`${Math.floor((enemy.group.position.x + 8) / 16)},${Math.floor((enemy.group.position.z + 8) / 16)}`);
    }
    const extraChanged = enemyChunks.join("|") !== (this._lastEnemyChunks || []).join("|");
    this._lastEnemyChunks = enemyChunks;

    const changed = this.mapBuilder.updateLoadedChunks(
      this.player.position,
      chunkChanged || extraChanged,
      extraCenters,
    );
    if (changed) {
      this.doors = this.mapBuilder.doors;
      this.keys = this.mapBuilder.keys;
      this.cabinets = this.mapBuilder.cabinets;
      this.safeLights = this.mapBuilder.safeLights || [];
      this.loreNotes = this.mapBuilder.loreNotes || [];
      this.finalExit = this.mapBuilder.finalExit;
      this.syncKeyHomes();
      this.refreshInteractables();
    }
    if (chunkChanged) {
      this._lastPlayerChunkCx = cx;
      this._lastPlayerChunkCz = cz;
      this.onEnterSchoolChunk(cx, cz);
    }

    // 2. Manage ceiling lights via the fixed PointLight pool.
    // Collect all panels from loaded chunks, sort by distance, assign pool slots.
    // Pool lights are NEVER added/removed — only position and intensity change.
    this._flickerAccum += deltaTime;
    const doFlicker = this._flickerAccum >= 0.1; // throttle flicker to 10 Hz
    if (doFlicker) this._flickerAccum = 0;

    const playerPos = this.player.position;
    const playerThreat = this.getMonsterThreat(playerPos);
    const allPanels = [];
    for (const chunk of this.mapBuilder.loadedChunks.values()) {
      if (!chunk.lights || chunk.lights.length === 0) continue;
      for (const light of chunk.lights) {
        const gx = chunk.center.x + light.localPos.x;
        const gz = chunk.center.z + light.localPos.z;
        const gy = chunk.center.y + light.localPos.y;
        const dx = gx - playerPos.x;
        const dz = gz - playerPos.z;
        const distSq = dx * dx + dz * dz;
        allPanels.push({ light, gx, gy, gz, distSq });

        // Flicker logic — nearby ceiling lamps die harder as monsters close in.
        if (doFlicker) {
          const nearPlayer = distSq < 12 * 12;
          const localThreat = nearPlayer ? playerThreat : playerThreat * 0.2;
          const canThreatFlicker = localThreat > 0.22;
          if (light.isFlickering || canThreatFlicker) {
            const tick = 0.1 * (1 + localThreat * 5.5);
            light.flickerTimer = (light.flickerTimer ?? 0.4) - tick;
            if (light.flickerTimer <= 0) {
              const offChance = (light.isFlickering ? 0.25 : 0) + localThreat * 0.62;
              const isOff = Math.random() < Math.min(0.92, offChance);
              if (isOff) {
                light.mesh.material.color.setHex(LIGHTING_CONFIG.ceilingPanelDimColor || 0x3b2618);
                light.mesh.material.emissive.setHex(0x140704);
                light.mesh.material.emissiveIntensity = LIGHTING_CONFIG.ceilingPanelDimEmissiveIntensity ?? 0.1;
                light.currentIntensity = 0;
                light.flickerTimer = 0.04 + Math.random() * (0.12 + localThreat * 0.55);
              } else {
                light.mesh.material.color.setHex(LIGHTING_CONFIG.ceilingPanelOnColor || 0xb47b4c);
                light.mesh.material.emissive.setHex(0x9a3f12);
                light.mesh.material.emissiveIntensity = LIGHTING_CONFIG.ceilingPanelOnEmissiveIntensity ?? 0.58;
                light.currentIntensity = light.baseIntensity;
                light.flickerTimer = (0.18 + Math.random() * 4.2) * (1 - localThreat * 0.78);
              }
            }
          }
        }
      }
    }

    // Sort panels closest-first and assign pool slots
    const budget = this._POINT_LIGHT_BUDGET;
    if (allPanels.length === 0) {
      for (let i = 0; i < budget; i++) {
        const pl = this._pointLightPool?.[i];
        if (pl && (pl.intensity !== 0 || pl.position.y !== -9999)) {
          pl.position.set(0, -9999, 0);
          pl.intensity = 0;
        }
      }
    } else {
      allPanels.sort((a, b) => a.distSq - b.distSq);
      for (let i = 0; i < budget; i++) {
        const pl = this._pointLightPool[i];
        if (!pl) continue;
        if (i < allPanels.length) {
          const { light, gx, gy, gz, distSq } = allPanels[i];
          const inRange = distSq < 24 * 24;
          if (inRange) {
            pl.position.set(gx, gy, gz);
            const targetIntensity = light.currentIntensity ?? light.baseIntensity ?? 0;
            // A barely perceptible voltage drift feels organic without becoming
            // a distracting global strobe. A true flicker-off remains at zero.
            const voltageBreath = targetIntensity > 0
              ? 0.96 + 0.04 * Math.sin(this.elapsedTime * 1.35 + (light.voltagePhase || 0))
              : 0;
            pl.intensity = targetIntensity * voltageBreath
              * (this.dreadDirector?.lightScale ?? 1)
              * (this.cinematicLightScale ?? 1);
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
    }

    // 2.2 Manage SafeLights PointLight pool
    this.updateSafeLightPool(playerPos);

    // 3. Keep floor hunters on their own map instead of yanking Baby onto 1F.
    this.repositionHunters(playerPos);
  }

  playerFloorIndex(y = 0) {
    if (y >= 3.2) return 2;
    if (y <= -2.2) return -1;
    return 1;
  }

  repositionHunters(playerPos) {
    if (!this.enemyManager || this.elapsedTime < 5) return;
    const playerFloor = this.playerFloorIndex(playerPos.y);
    for (const enemy of this.enemyManager.enemies) {
      if (enemy.isDormant || enemy.state === "cutscene") continue;
      if (enemy.isBaby && !enemy.babyAwake) continue;
      if (enemy.config.id === "hwacat-angry" && !enemy.isDynamic) continue;

      const allowed = enemy.config.allowedFloor;
      if (allowed !== undefined && allowed !== playerFloor) {
        this.holdHunterAtStairMouth(enemy, allowed, playerFloor);
        continue;
      }

      const distance = Math.hypot(
        enemy.group.position.x - playerPos.x,
        enemy.group.position.z - playerPos.z,
      );
      if (distance <= 42) continue;
      this.relocateHunterNearPlayer(enemy, playerPos);
    }
  }

  holdHunterAtStairMouth(enemy, hunterFloor, playerFloor) {
    if (hunterFloor !== 1) return;
    const target = playerFloor === -1
      ? { x: 16, y: 0, z: 22.4 }
      : playerFloor === 2
        ? { x: -16, y: 0, z: -6.2 }
        : null;
    if (!target) return;
    const dist = Math.hypot(enemy.group.position.x - target.x, enemy.group.position.z - target.z);
    if (dist > 14) {
      enemy.group.position.set(target.x, target.y, target.z);
      this.collisionWorld.snapToValidSurface(enemy.group.position, { actorId: enemy.config.id, floor: 1 });
      enemy.patrolPath = [];
      enemy.patrolPathGoal = null;
      enemy.chasePath = [];
      enemy.chasePathGoal = null;
    }
    if (enemy.state === "chase" || enemy.state === "flee") {
      enemy.beginSearch?.(enemy.group.position.clone(), 3.2);
    }
    if (!this._stairWaitAnnounced) {
      this._stairWaitAnnounced = true;
      this.voiceAnnouncer?.announce("stairWait", "계단 입구에서 실내화가 멈추고 기다립니다.");
    }
  }

  relocateHunterNearPlayer(enemy, playerPos) {
    const ecx = Math.floor((playerPos.x + 8) / 16);
    const ecz = Math.floor((playerPos.z + 8) / 16);
    const candidates = [];
    for (let ddx = -2; ddx <= 2; ddx += 1) {
      for (let ddz = -2; ddz <= 2; ddz += 1) {
        if (ddx === 0 && ddz === 0) continue;
        const chunk = this.mapBuilder.loadedChunks.get(`${ecx + ddx},${ecz + ddz}`);
        if (!chunk) continue;
        const spawnPos = chunk.center.clone();
        spawnPos.y = playerPos.y;
        if (typeof enemy.matchesFloor === "function" && !enemy.matchesFloor(spawnPos.y)) continue;
        const hasLos = this.collisionWorld.hasLineOfSight(playerPos, spawnPos);
        if (!hasLos) candidates.push(spawnPos);
      }
    }
    if (candidates.length === 0) return;
    const previous = enemy.group.position.clone();
    const targetSpawn = candidates[Math.floor(Math.random() * candidates.length)];
    enemy.group.position.copy(targetSpawn);
    this.collisionWorld.snapToValidSurface(enemy.group.position, {
      actorId: enemy.config.id,
      floor: enemy.config.allowedFloor,
    });
    if (typeof enemy.matchesFloor === "function" && !enemy.matchesFloor(enemy.group.position.y)) {
      enemy.group.position.copy(previous);
      return;
    }
    enemy.patrolPath = [];
    enemy.patrolPathGoal = null;
    enemy.chasePath = [];
    enemy.chasePathGoal = null;
  }

  onEnterSchoolChunk(cx, cz) {
    const generator = this.mapBuilder?.generator;
    if (!generator || !this.isStarted || this.gameOver || this.gameCleared) return;
    const type = generator.getChunkType(cx, cz);
    if (!type || type === "void") return;
    if (!this._storyBeats) this._storyBeats = new Set();
    const mapId = getMapId(cx, cz, this.player?.position?.y || 0);
    const roomLines = {
      nurse_office: ["nurse", "보건실입니다. 장부에 끝나지 않은 출석이 남아 있습니다."],
      music_room: ["music", "음악실입니다. 한 음이 모자란 피아노가 열려 있습니다."],
      faculty_office: ["faculty", "교무실입니다. 지워진 네 이름을 찾으십시오."],
      science_lab: ["science", "과학실입니다. 가스관이 아직 식지 않았습니다."],
      gymnasium: ["gym", "체육관입니다. 줄은 남아 있는데 운동장은 없습니다."],
      courtyard: ["courtyard", "중정입니다. 난간 너머로 내려가지 마십시오."],
    };
    if (cx === 3 && cz === 0 && !this._storyBeats.has("skybridge")) {
      this._storyBeats.add("skybridge");
      this._lastPaAt = this.playTime;
      soundManager.playSFX("school_chime");
      this.voiceAnnouncer?.announce("skybridge", "본관과 별관을 잇는 유리복도입니다. 아래는 운동장이 아닙니다.");
      this.hud.setStatus("본관과 별관을 잇는 유리복도입니다. 아래는 운동장이 아닙니다.", 3600);
      return;
    }
    const room = roomLines[type];
    if (room && !this._storyBeats.has(type)) {
      this._storyBeats.add(type);
      this._lastPaAt = this.playTime;
      soundManager.playSFX("radio_static");
      this.voiceAnnouncer?.announce(room[0], room[1]);
      this.hud.setStatus(room[1], 3600);
      return;
    }
    if (type === "start") return;
    if (this.playTime < 9) return;
    const mapBeat = mapId ? `map:${mapId}` : "";
    if ((mapId === "f1b" || mapId === "b1" || mapId === "f2") && mapBeat && !this._storyBeats.has(mapBeat)) {
      this._storyBeats.add(mapBeat);
      this._lastPaAt = this.playTime;
      const line = mapId === "b1"
        ? "방송이 끊깁니다. 지하의 물이 이름을 적고 있습니다."
        : mapId === "f2"
          ? "2층입니다. 복도가 아직 마르지 않았습니다."
          : "별관입니다. 같은 교실을 두 번 지나치지 마십시오.";
      soundManager.playSFX(mapId === "b1" ? "drip" : mapId === "f2" ? "blood_drip" : "school_chime");
      this.voiceAnnouncer?.announce(mapId, line);
      this.hud.setStatus(line, 3200);
      return;
    }
    const now = this.playTime;
    if (now - (this._lastPaAt || 0) < 16) return;
    this._lastPaAt = now;
    const line = "방송입니다. 하교하지 않습니다. 복도에서 기다리십시오.";
    soundManager.playSFX(Math.random() < 0.5 ? "school_chime" : "radio_static");
    this.voiceAnnouncer?.announce("pa", line);
    this.hud.setStatus(line, 3200);
  }

  updateFloorAtmosphere(deltaTime, snap = false) {
    const pos = this.player?.position;
    const y = pos?.y ?? 0;
    const cx = Math.floor(((pos?.x ?? 0) + 8) / 16);
    const cz = Math.floor(((pos?.z ?? 0) + 8) / 16);
    const mapId = getMapId(cx, cz, y) || (y < -2.2 ? "b1" : y > 3.2 ? "f2" : "f1a");
    if (this._floorMapId !== mapId) {
      this._floorMapId = mapId;
      snap = true;
    }
    const profile = mapId === "b1"
      ? LIGHTING_CONFIG.basement
      : mapId === "f2"
        ? LIGHTING_CONFIG.upper
        : mapId === "f1b"
          ? (LIGHTING_CONFIG.annex || LIGHTING_CONFIG)
          : LIGHTING_CONFIG;
    const colorBlend = snap ? 1 : Math.min(1, deltaTime * 2.4);
    const distBlend = snap ? 1 : Math.min(1, deltaTime * 3);
    const fog = this.scene.fog;
    if (fog) {
      fog.color.lerp(new THREE.Color(profile.fogColor ?? LIGHTING_CONFIG.fogColor), colorBlend);
      this.scene.background?.lerp?.(fog.color, colorBlend);
      fog.near += ((profile.fogNear ?? LIGHTING_CONFIG.fogNear) - fog.near) * distBlend;
      fog.far += ((profile.fogFar ?? LIGHTING_CONFIG.fogFar) - fog.far) * distBlend;
    }
    const exposure = profile.exposure ?? LIGHTING_CONFIG.rendererExposure;
    this.renderer.toneMappingExposure += (exposure - this.renderer.toneMappingExposure) * distBlend;
    if (this.ambientLight) {
      if (profile.ambientColor) {
        this.ambientLight.color.lerp(new THREE.Color(profile.ambientColor), colorBlend);
      }
      const ambientIntensity = profile.ambientIntensity ?? LIGHTING_CONFIG.ambientIntensity;
      this.ambientLight.intensity += (ambientIntensity - this.ambientLight.intensity) * distBlend;
    }
    if (this.hemisphereLight) {
      if (profile.hemisphereSkyColor) {
        this.hemisphereLight.color.lerp(new THREE.Color(profile.hemisphereSkyColor), colorBlend);
        this.hemisphereLight.groundColor.lerp(new THREE.Color(profile.hemisphereGroundColor), colorBlend);
      }
      const hemiIntensity = profile.hemisphereIntensity ?? LIGHTING_CONFIG.hemisphereIntensity;
      this.hemisphereLight.intensity += (hemiIntensity - this.hemisphereLight.intensity) * distBlend;
    }
    const beamColor = profile.flashlightColor ?? LIGHTING_CONFIG.flashlightColor;
    if (this.flashlightController?.healthyColor) {
      this.flashlightController.healthyColor.lerp(new THREE.Color(beamColor), colorBlend);
    }
    if (this.flashlight && profile.flashlightColor) {
      this.flashlight.color.lerp(new THREE.Color(profile.flashlightColor), colorBlend);
      this.flashlightFill?.color.lerp(this.flashlight.color, 1);
    }
    const intensity = profile.flashlightIntensity ?? LIGHTING_CONFIG.flashlightIntensity;
    if (this.flashlightController) {
      this.flashlightController.defaultIntensity += (intensity - this.flashlightController.defaultIntensity) * distBlend;
    }
    if (this.flashlight) {
      const range = profile.flashlightRange ?? LIGHTING_CONFIG.flashlightRange;
      this.flashlight.distance += (range - this.flashlight.distance) * distBlend;
    }
    if (this.flashlightFill) {
      const fillRange = profile.flashlightFillRange ?? LIGHTING_CONFIG.flashlightFillRange;
      this.flashlightFill.distance += (fillRange - this.flashlightFill.distance) * distBlend;
    }
    this._floorAmbienceAt = (this._floorAmbienceAt || 0) + deltaTime;
    const interval = mapId === "b1" ? 1.15 : mapId === "f2" ? 1.45 : 8;
    if (this._floorAmbienceAt >= interval) {
      this._floorAmbienceAt = 0;
      if (mapId === "b1") soundManager.playSFX("drip");
      else if (mapId === "f2") soundManager.playSFX("blood_drip");
    }
  }

  poseForCapture({
    x,
    y,
    z,
    yaw = 0,
    pitch = -0.42,
    lookAt = null,
    flashlight = true,
    freezeLoop = true,
  } = {}) {
    this.testSafeMode = true;
    if (freezeLoop) this.loop?.stop();
    if (Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z)) {
      this.player.noclip = true;
      this.player.setPosition({ x, y, z });
      this.updateBackrooms(0);
    }
    this.updateFloorAtmosphere(8, true);
    this.floorHuntDirector?.hide?.();
    this.hud?.setPrompt?.("");
    if (Array.isArray(lookAt) && lookAt.length >= 3) {
      this.player.setLookAt(new THREE.Vector3(lookAt[0], lookAt[1], lookAt[2]));
    } else {
      this.player.resetLook(yaw, pitch);
    }
    const fl = this.flashlightController;
    if (fl) {
      fl.batteryLevel = 1;
      fl.eventTimer = 0;
      fl.eventKind = "none";
      fl.smoothThreat = 0;
      fl.toggleLock = 1;
      this.input?.clearKey?.("f");
      fl.setEnabled(Boolean(flashlight), false);
      fl.applyOutput(flashlight ? 1 : 0);
    }
    this.renderer.render(this.scene, this.camera);
    return {
      flashlight: fl?.enabled === true,
      hud: document.querySelector("#flashlight-state")?.textContent || "",
      intensity: this.flashlight?.intensity ?? 0,
      y: this.player.position.y,
      pitch: this.player.pitch,
      yaw: this.player.yaw,
      fogFar: this.scene.fog?.far ?? 0,
    };
  }

  getMinMonsterDistance(targetPos, options = {}) {
    if (!targetPos) return Infinity;
    const sameFloor = Boolean(options.sameFloor);
    const floorSlack = options.floorSlack ?? 2.2;
    let minDist = Infinity;

    // 1. EnemyManager enemies (Cyclopse, Uncat, Baby, Hwacat-Angry)
    if (this.enemyManager && this.enemyManager.enemies) {
      for (const enemy of this.enemyManager.enemies) {
        if (!enemy.group || enemy.isDormant) continue;
        if (sameFloor && typeof enemy.isSameLevelAs === "function") {
          if (!enemy.isSameLevelAs(targetPos)) continue;
        } else if (sameFloor && Math.abs((enemy.group.position.y ?? 0) - (targetPos.y ?? 0)) > floorSlack) {
          continue;
        }
        const d = sameFloor
          ? Math.hypot(targetPos.x - enemy.group.position.x, targetPos.z - enemy.group.position.z)
          : targetPos.distanceTo(enemy.group.position);
        if (d < minDist) minDist = d;
      }
    }

    // 2. Active Weeping Angels (Mannequins)
    if (this.mapBuilder && this.mapBuilder.loadedChunks) {
      for (const chunk of this.mapBuilder.loadedChunks.values()) {
        for (const mesh of chunk.meshes) {
          if (mesh.userData && mesh.userData.isWeepingAngel && mesh.position) {
            const state = mesh.userData.weepingAngelState;
            if (state && state.active === false) continue;
            if (sameFloor && Math.abs((mesh.position.y ?? 0) - (targetPos.y ?? 0)) > floorSlack) continue;
            const d = sameFloor
              ? Math.hypot(targetPos.x - mesh.position.x, targetPos.z - mesh.position.z)
              : Math.hypot(targetPos.x - mesh.position.x, targetPos.z - mesh.position.z);
            if (d < minDist) minDist = d;
          }
        }
      }
    }

    return minDist;
  }

  getMonsterThreat(targetPos = this.player?.position, maxDistance = 15) {
    const dist = this.getMinMonsterDistance(targetPos, { sameFloor: true });
    if (!Number.isFinite(dist)) return 0;
    const inner = 1.15;
    const span = Math.max(0.01, maxDistance - inner);
    const linear = 1 - Math.max(0, Math.min(1, (dist - inner) / span));
    return linear * linear;
  }

  updateSafeLightPool(playerPos = this.player?.position) {
    if (!playerPos || !this._safeLightPool) return;
    const allSafePanels = [];
    for (const safeLight of this.safeLights) {
      if (!safeLight.isOn) continue;
      const pos = safeLight.getLightWorldPosition();
      const dx = pos.x - playerPos.x;
      const dz = pos.z - playerPos.z;
      const distSq = dx * dx + dz * dz;
      allSafePanels.push({ safeLight, pos, distSq });
    }

    allSafePanels.sort((a, b) => a.distSq - b.distSq);
    const safeBudget = this._SAFE_LIGHT_BUDGET || 8;
    for (let i = 0; i < safeBudget; i++) {
      const pl = this._safeLightPool[i];
      if (!pl) continue;
      if (i < allSafePanels.length) {
        const { safeLight, pos, distSq } = allSafePanels[i];
        const inRange = distSq < SAFE_LIGHT_CONFIG.activeDistance * SAFE_LIGHT_CONFIG.activeDistance;
        if (inRange) {
          const monsterDist = this.getMinMonsterDistance(pos);
          let flickerMult = 1.0;

          // Subtle natural flame / filament breathing waver
          const basePhase = (pos.x * 3.1 + pos.z * 5.7) % 6.28;
          const flameBreath = 0.96 + 0.04 * Math.sin((this.elapsedTime || 0) * 1.8 + basePhase);
          flickerMult = flameBreath;

          if (monsterDist < 14.0) {
            const proximity = Math.min(1.0, Math.max(0.0, 1.0 - (monsterDist / 14.0)));
            const p2 = proximity * proximity;
            const freq = 2.0 + p2 * 16;
            const phase = (pos.x * 7.91 + pos.z * 13.43) % 6.28;
            const t = (this.elapsedTime || 0) * freq + phase;
            const wave = Math.sin(t) * 0.45 + Math.sin(t * 2.6 + 0.7) * 0.3 + Math.sin(t * 7.4) * 0.25;
            const dying = 1 - p2 * 0.62;
            let strobe = 1;
            if (p2 > 0.45) {
              const cut = 0.35 - p2 * 0.55;
              strobe = Math.sin(t * (14 + p2 * 22)) > cut ? 1 : 0.05 + Math.random() * 0.06;
            }
            flickerMult = Math.max(0.03, (0.52 + 0.48 * wave) * dying * strobe);
          }
          safeLight.setFlickerState(flickerMult);
          pl.position.copy(pos);
          pl.intensity = (SAFE_LIGHT_CONFIG.intensity || 8.5) * flickerMult;
        } else {
          safeLight.setFlickerState(1.0);
          pl.position.set(0, -9999, 0);
          pl.intensity = 0;
        }
      } else {
        pl.position.set(0, -9999, 0);
        pl.intensity = 0;
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

  isPlayerLookingAt(targetPosition) {
    if (!this.player) return false;
    
    const frustum = new THREE.Frustum();
    const cameraViewProjectionMatrix = new THREE.Matrix4();
    this.camera.updateMatrixWorld();
    this.camera.matrixWorldInverse.copy(this.camera.matrixWorld).invert();
    cameraViewProjectionMatrix.multiplyMatrices(this.camera.projectionMatrix, this.camera.matrixWorldInverse);
    frustum.setFromProjectionMatrix(cameraViewProjectionMatrix);

    const checkPoint = new THREE.Vector3(targetPosition.x, targetPosition.y + 0.8, targetPosition.z);
    const inFrustum = frustum.containsPoint(checkPoint);
    if (!inFrustum) return false;
    
    const hasLos = this.collisionWorld.hasLineOfSight(this.camera.position, checkPoint);
    return hasLos;
  }

  updateWeepingAngels(deltaTime) {
    if (!this.player || !this.mapBuilder) return;
    
    const flashlightOn = this.flashlightController && this.flashlightController.enabled;
    const playerPos = this.player.position;
    const angels = [];
    
    // Find all weeping angels in loaded chunks and update them individually
    for (const chunk of this.mapBuilder.loadedChunks.values()) {
      for (const mesh of chunk.meshes) {
        if (mesh.userData && mesh.userData.isWeepingAngel && mesh.userData.weepingAngelState && mesh.userData.weepingAngelState.loaded) {
          const state = mesh.userData.weepingAngelState;
          angels.push(mesh);
          
          // 1. Gaze check: Is player looking at this angel?
          const isLooking = this.isPlayerLookingAt(mesh.position);
          
          // 2. Activeness check: Only active if intro triggered/active, flashlight is ON, and player is NOT looking
          const shouldMove = (state.active !== false) && flashlightOn && !isLooking;
          
          if (shouldMove) {
            const goal = playerPos;
            
            // Pathfinding
            state.pathTimer -= deltaTime;
            const canMoveDirect = this.collisionWorld.hasLineOfSight(mesh.position, goal);
            let target = goal;
            
            if (canMoveDirect) {
              state.path = [];
              state.pathTimer = 0.5;
            } else {
              if (state.path === null || state.pathTimer <= 0) {
                state.path = this.collisionWorld.findPath(mesh.position, goal, state.radius, {
                  cellSize: 0.85,
                  allowInterFloor: true,
                });
                state.pathTimer = 0.4 + Math.random() * 0.2;
              }
              
              while (state.path && state.path.length > 1 && Math.hypot(mesh.position.x - state.path[1].x, mesh.position.z - state.path[1].z) < 0.4) {
                state.path.shift();
              }
              target = (state.path && (state.path[1] || state.path[0])) || goal;
            }
            
            // Move
            const direction = new THREE.Vector3(target.x - mesh.position.x, 0, target.z - mesh.position.z);
            if (direction.lengthSq() > 0.0001) {
              direction.normalize();
              const previousPosition = mesh.position.clone();
              mesh.position.addScaledVector(direction, state.speed * deltaTime);
              this.collisionWorld.resolveCircle(mesh.position, state.radius);
              this.collisionWorld.resolveActorPosition(
                previousPosition,
                mesh.position,
                state.radius,
                { actorId: state.id },
              );
              mesh.rotation.y = Math.atan2(direction.x, direction.z);

              // Play creepy creak SFX while moving behind player's back
              state.creakTimer = (state.creakTimer || 0) + deltaTime;
              if (state.creakTimer > 0.8) {
                state.creakTimer = 0;
                soundManager.playSFX("mannequin_creak");
              }
            }
            
            mesh.position.y = this.collisionWorld.getGroundY(mesh.position);
          }
          
          // 3. Collision catch check: Only catches player if flashlight is ON
          if (flashlightOn) {
            const distToPlayer = Math.hypot(mesh.position.x - playerPos.x, mesh.position.z - playerPos.z);
            if (distToPlayer <= state.catchDistance && !this.player.isHidden && !this.gameOver && !this.gameCleared && !this.isInvincible) {
              this.handleCaught("마네킹이 바로 뒤에 서 있었습니다.");
            }
          }
        }
      }
    }

    // 4. Proximity whispering sound calculation
    let minAngelDist = Infinity;
    for (const mesh of angels) {
      const distToPlayer = Math.hypot(mesh.position.x - playerPos.x, mesh.position.z - playerPos.z);
      if (distToPlayer < minAngelDist) {
        minAngelDist = distToPlayer;
      }
    }
    const whisperIntensity = Number.isFinite(minAngelDist) && minAngelDist < 14
      ? Math.max(0, 1 - minAngelDist / 14)
      : 0;
    soundManager.setWhisperIntensity(whisperIntensity);

    
    // Separation pass between Weeping Angels to prevent them from merging/overlapping
    for (let i = 0; i < angels.length; i++) {
      const meshA = angels[i];
      const radiusA = meshA.userData.weepingAngelState.radius || 0.38;
      
      // Separate from other angels
      for (let j = i + 1; j < angels.length; j++) {
        const meshB = angels[j];
        const radiusB = meshB.userData.weepingAngelState.radius || 0.38;
        
        const dx = meshB.position.x - meshA.position.x;
        const dz = meshB.position.z - meshA.position.z;
        const dist = Math.hypot(dx, dz);
        const minDist = radiusA + radiusB + 0.1;
        
        if (dist < minDist && dist > 0.001) {
          const overlap = minDist - dist;
          const nx = dx / dist;
          const nz = dz / dist;
          const pushDistance = overlap * 0.5;
          
          meshA.position.x -= nx * pushDistance;
          meshA.position.z -= nz * pushDistance;
          meshB.position.x += nx * pushDistance;
          meshB.position.z += nz * pushDistance;
          
          this.collisionWorld.resolveCircle(meshA.position, radiusA);
          meshA.position.y = this.collisionWorld.getGroundY(meshA.position);
          
          this.collisionWorld.resolveCircle(meshB.position, radiusB);
          meshB.position.y = this.collisionWorld.getGroundY(meshB.position);
        }
      }
      
      // Separate from other main enemies (Uncat, Cyclopse, etc.)
      if (this.enemyManager && this.enemyManager.enemies) {
        for (const enemy of this.enemyManager.enemies) {
          const radiusB = enemy.config.radius || 0.35;
          const dx = enemy.group.position.x - meshA.position.x;
          const dz = enemy.group.position.z - meshA.position.z;
          const dist = Math.hypot(dx, dz);
          const minDist = radiusA + radiusB + 0.1;
          
          if (dist < minDist && dist > 0.001) {
            const overlap = minDist - dist;
            const nx = dx / dist;
            const nz = dz / dist;
            
            meshA.position.x -= nx * overlap;
            meshA.position.z -= nz * overlap;
            
            this.collisionWorld.resolveCircle(meshA.position, radiusA);
            meshA.position.y = this.collisionWorld.getGroundY(meshA.position);
          }
        }
      }
    }
  }
}
