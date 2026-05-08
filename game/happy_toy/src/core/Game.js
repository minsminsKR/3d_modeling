// Happy Toy의 최상위 조립 모듈입니다.
// 렌더러, 장면, 맵, 플레이어, 적, HUD, 루프를 생성하고 서로 연결합니다.

import * as THREE from "three";
import { CABINET_CONFIG, CAMERA_CONFIG, MAP_CONFIG, PLAYER_CONFIG, WORLD_CONFIG } from "../config/gameConfig.js";
import { CollisionWorld } from "../world/CollisionWorld.js";
import { EnemyManager } from "../entities/EnemyManager.js";
import { HorrorEventManager } from "../events/HorrorEventManager.js";
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
    this.scene.fog = new THREE.Fog(WORLD_CONFIG.fogColor, 7, 32);

    this.camera = new THREE.PerspectiveCamera(
      CAMERA_CONFIG.fov,
      window.innerWidth / window.innerHeight,
      CAMERA_CONFIG.near,
      CAMERA_CONFIG.far,
    );

    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.rootElement.appendChild(this.renderer.domElement);

    this.hud = new Hud();
    this.input = new Input(this.renderer.domElement);
    this.collisionWorld = new CollisionWorld();
    this.doors = [];
    this.keys = [];
    this.cabinets = [];
    this.finalExit = null;
    this.keyCount = 0;
    this.player = null;
    this.flashlightController = null;
    this.enemyManager = null;
    this.horrorEventManager = null;
    this.horrorLights = [];
    this.flashlight = null;
    this.gameOver = false;
    this.gameCleared = false;
    this.isStarted = false;
    this.isPaused = false;
    this.cabinetEvent = null;
    this.elapsedTime = 0;
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
    const map = new MapBuilder(this.scene, this.collisionWorld).build();
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

    this.enemyManager = new EnemyManager(this.scene, this.collisionWorld, this.doors, this.hud);
    this.input.connect();
    this.connectUi();
    window.addEventListener("resize", this.handleResize);
    document.addEventListener("pointerlockchange", this.handlePointerLockChange);

    this.loop.start();
    await this.enemyManager.loadEnemies();
  }

  setupLighting() {
    const lowAmbient = new THREE.HemisphereLight(0x6f7f68, 0x17110d, 0.42);
    this.scene.add(lowAmbient);

    const exitLamp = new THREE.PointLight(0xd4b24a, 1.1, 13, 1.6);
    exitLamp.position.set(0, 2.5, 23);
    this.scene.add(exitLamp);
    this.horrorLights.push(exitLamp);

    const redRoomLamp = new THREE.PointLight(0xc4332c, 0.8, 8, 1.7);
    redRoomLamp.position.set(8.5, 2.4, -7);
    this.scene.add(redRoomLamp);
    this.horrorLights.push(redRoomLamp);

    const finalLamp = new THREE.PointLight(0xd4b24a, 1.2, 12, 1.6);
    finalLamp.position.set(0, 2.45, -35.5);
    this.scene.add(finalLamp);
    this.horrorLights.push(finalLamp);

    const storageLamp = new THREE.PointLight(0x348f6c, 0.65, 9, 1.8);
    storageLamp.position.set(-8.6, 2.3, 4.5);
    this.scene.add(storageLamp);
    this.horrorLights.push(storageLamp);

    const stairLamp = new THREE.PointLight(0x8a392d, 1.05, 12, 1.9);
    stairLamp.position.set(-7.2, 2.9, 17.4);
    this.scene.add(stairLamp);
    this.horrorLights.push(stairLamp);

    const stairLandingLamp = new THREE.PointLight(0x6d7a58, 0.45, 7, 1.9);
    stairLandingLamp.position.set(-4.4, 1.9, 22);
    this.scene.add(stairLandingLamp);
    this.horrorLights.push(stairLandingLamp);

    const upperLandingLamp = new THREE.PointLight(0x8a392d, 0.55, 8, 1.85);
    upperLandingLamp.position.set(-4.8, 5.25, 12.4);
    this.scene.add(upperLandingLamp);
    this.horrorLights.push(upperLandingLamp);

    const upperHallLamp = new THREE.PointLight(0x6d7a58, 0.85, 14, 2);
    upperHallLamp.position.set(0.2, 5.9, -10);
    this.scene.add(upperHallLamp);
    this.horrorLights.push(upperHallLamp);

    const upperNurseryLamp = new THREE.PointLight(0x8a392d, 0.9, 10, 1.8);
    upperNurseryLamp.position.set(-8.8, 5.55, -4.2);
    this.scene.add(upperNurseryLamp);
    this.horrorLights.push(upperNurseryLamp);

    const upperMirrorLamp = new THREE.PointLight(0xc4332c, 0.75, 9, 1.8);
    upperMirrorLamp.position.set(8.2, 5.8, -16.2);
    this.scene.add(upperMirrorLamp);
    this.horrorLights.push(upperMirrorLamp);

    this.flashlight = new THREE.SpotLight(0xfff1bf, 11.5, 24, Math.PI * 0.24, 0.42, 1.05);
    this.flashlight.position.set(0, 0, 0);
    this.flashlight.target.position.set(0, 0, -1);
    this.camera.add(this.flashlight);
    this.camera.add(this.flashlight.target);
    this.scene.add(this.camera);
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
    this.hud.setMouseSensitivityDisplay(Number(this.hud.mouseSensitivityInput.value));
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
      canExitCabinet: () => !this.cabinetEvent,
      getHiddenPrompt: () => this.getHiddenPrompt(),
      tryClearFinal: (finalExit) => this.tryClearFinal(finalExit),
      isCleared: () => this.gameCleared,
    };
  }

  start() {
    if (this.isStarted && !this.isPaused) {
      this.input.requestPointerLock();
      return;
    }

    this.isStarted = true;
    this.isPaused = false;
    this.hud.hideStart();
    this.hud.hidePause();
    this.input.requestPointerLock();
    this.hud.setStatus("복도 끝에서 무언가 움직였습니다.", 1800);
  }

  restart() {
    this.gameOver = false;
    this.gameCleared = false;
    this.isStarted = true;
    this.isPaused = false;
    this.keyCount = 0;
    this.cabinetEvent = null;
    this.collisionWorld.clearDropAttempt();
    this.horrorEventManager?.reset();
    this.hud.hideCaught();
    this.hud.hideClear();
    this.hud.hidePause();
    this.hud.hideStart();
    this.input.requestPointerLock();
    this.player.exitCabinet();
    this.player.setPosition(new THREE.Vector3(...MAP_CONFIG.playerStart));
    this.player.resetLook(0, 0);
    this.flashlightController.reset();
    this.enemyManager.reset();
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
    this.hud.setStatus("다시 복도 한가운데에 섰습니다.", 1800);
  }

  update(deltaTime) {
    if (this.isStarted && !this.gameOver && !this.gameCleared && this.input.consumePressed("escape")) {
      this.togglePause();
    }

    if (this.isPaused) {
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

    if (this.isStarted && !this.isPaused && !this.gameOver && !this.gameCleared) {
      this.player.update(deltaTime);
      this.flashlightController.update();
      this.horrorEventManager?.update(deltaTime);
      const enemyState = this.enemyManager?.update(deltaTime, {
        position: this.player.position,
        isHidden: this.player.isHidden,
        isMoving: this.player.isMoving,
        isSprinting: this.player.isSprinting,
      });
      if (enemyState?.caught) {
        this.handleCaught();
      }
    }

    this.updateDebugHud();
    this.renderer.render(this.scene, this.camera);
    this.input.endFrame();
  }

  updateDebugHud() {
    if (!this.player) {
      return;
    }
    const debug = this.collisionWorld.getDebugState(this.player.position);
    debug.monsters = this.enemyManager?.enemies.map((enemy) => enemy.getDebugState()) || [];
    debug.nearestDoor = this.getNearestDoorDebug();
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

  handlePointerLockChange() {
    if (
      this.isStarted
      && !this.isPaused
      && !this.gameOver
      && !this.gameCleared
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
    this.collisionWorld.clearDropAttempt();
    this.horrorEventManager?.reset();
    this.player.exitCabinet();
    this.player.setPosition(new THREE.Vector3(...MAP_CONFIG.playerStart));
    this.player.resetLook(0, 0);
    this.flashlightController.reset();
    this.enemyManager.reset();
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
    if (key.isCollected || this.gameOver || this.gameCleared) {
      return;
    }

    key.collect();
    this.keyCount += 1;
    this.hud.setStatus(`${key.label}를 얻었습니다. 열쇠 ${this.keyCount}/${this.keys.length}`, 1700);
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
    if (!this.player.isHidden || this.cabinetEvent) {
      return;
    }

    if (this.player.hiddenCabinet) {
      this.player.hiddenCabinet.occupied = false;
    }
    this.player.exitCabinet();
    this.hud.setStatus("캐비넷 밖으로 조용히 나왔습니다.", 1300);
  }

  getHiddenPrompt() {
    if (!this.cabinetEvent) {
      return "E - 캐비넷에서 나오기";
    }

    return this.cabinetEvent.outcome === "caught"
      ? "문손잡이가 천천히 움직입니다"
      : "숨죽이고 기다리는 중";
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
    this.hud.showClear();
  }

  handleCaught(message = "발소리가 바로 뒤에서 멈췄습니다.") {
    this.gameOver = true;
    if (this.player.hiddenCabinet) {
      this.player.hiddenCabinet.occupied = false;
    }
    this.cabinetEvent = null;
    document.exitPointerLock?.();
    this.hud.showCaught(message);
  }
}
