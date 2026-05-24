import * as THREE from "three";
import {
  BALANCE as GAME_BALANCE,
  applyProgressionReward,
  buildWavePlan,
  clampRunStats,
  enemyStats as balancedEnemyStats,
  killExp,
  nextExpNeed,
  nextWaveInterval,
  rollGateSpec as rollBalancedGateSpec,
  weaponIndexForState,
} from "./balance.js";
import { ASSETS, LANES, LIMITS, RUNNER, UPGRADES, WEAPONS } from "./config.js";
import { activateCharacter, CharacterLoader, cloneCharacter } from "./characterLoader.js";
import { AudioBus, InstancedPool } from "./instancing.js";
import { ModelDecoratorSystem } from "./modelLayer.js";

const canvas = document.getElementById("game-canvas");
const ui = {
  army: document.getElementById("army-count"),
  weapon: document.getElementById("weapon"),
  kills: document.getElementById("kills"),
  coins: document.getElementById("coins"),
  level: document.getElementById("level"),
  dps: document.getElementById("dps"),
  fps: document.getElementById("fps"),
  combo: document.getElementById("combo"),
  expFill: document.getElementById("exp-fill"),
  upgradePanel: document.getElementById("upgrade-panel"),
  upgradeOptions: document.getElementById("upgrade-options"),
  startPanel: document.getElementById("start-panel"),
  startButton: document.getElementById("start-button"),
};

const tempVec = new THREE.Vector3();
const tempScale = new THREE.Vector3();
const screenProbeLeft = new THREE.Vector3();
const screenProbeRight = new THREE.Vector3();
const clock = new THREE.Clock();
const ALLY_MODEL_SCALE = 0.58;
const DEFAULT_EFFECT = Object.freeze({
  bulletScale: 0.38,
  bulletLength: 0.38,
  trailWidth: 0.12,
  trailLength: 0.24,
  flashScale: 0.18,
  flashTtl: 0.04,
  particles: 0,
  hitParticles: 1,
  hitSize: 0.12,
});

class DefenseGame {
  constructor() {
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x1f2b40);
    this.scene.fog = new THREE.Fog(0x1f2b40, 55, 155);

    this.camera = new THREE.PerspectiveCamera(62, 1, 0.1, 220);
    this.camera.position.set(0, 13.5, -9.5);

    this.renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: false,
      powerPreference: "high-performance",
    });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;

    this.loader = new CharacterLoader();
    this.models = new ModelDecoratorSystem(this.scene, this.loader);
    this.audio = new AudioBus();

    this.input = { left: false, right: false, pointerDown: false, targetX: 0 };
    this.running = false;
    this.pausedForUpgrade = false;
    this.lastTime = performance.now();
    this.uiTimer = 0;
    this.fpsTimer = 0;
    this.fpsFrames = 0;
    this.cameraShake = 0;
    this.decoratedEnemyIds = new Set();
    this.playerModelReady = null;
    this.shotEvents = 0;
    this.muzzleEvents = 0;
    this.soundEvents = 0;
    this.gateApplications = 0;
    this.shootingLogTimer = 0;
    this.debugBossPreview = new URLSearchParams(window.location.search).has("bossPreview");
    this.didDebugBossPreview = false;

    this.resetState();
    this.createScene();
    this.createPools();
    this.bindInput();
    this.resize();
    window.defenseGame = this;
    window.addEventListener("defense-force-boss-preview", () => this.forceBossPreview());
    window.addEventListener("resize", () => this.resize());
    ui.startButton.addEventListener("click", () => this.start());
  }

  resetState() {
    this.player = { x: 0, y: 0.55, z: 0, prevZ: 0, speed: RUNNER.playerSpeed, shootTimer: 0 };
    this.kills = 0;
    this.coins = 0;
    this.gems = 0;
    this.level = 1;
    this.exp = 0;
    this.expNeed = GAME_BALANCE.run.initialExpNeed;
    this.pendingUpgradeCount = 0;
    this.combo = 0;
    this.comboTimer = 0;
    this.elapsed = 0;
    this.wave = 0;
    this.nextGateTime = 12.0;
    this.enemySpawnTimer = 1.2; // Start at 1.2 to trigger first spawn immediately
    this.nextMidBossTime = 45.0;
    this.nextBigBossTime = 90.0;
    this.lastLoggedDifficulty = -1;
    this.weaponIndex = 0;
    this.activeBuffs = { AttackSpeed: 0, TripleShot: 0, Piercing: 0, Explosive: 0, Shield: 0, Magnet: 0, Freeze: 0 };
    this.ultGauge = 0;
    this.eventTimer = 0;
    this.nextEventTime = 45.0 + Math.random() * 45.0;
    this.currentEvent = null;
    this.currentEventTimer = 0;
    this.slowMoTimer = 0;
    this.rewardBannerTimer = 0;

    // Reset overlay elements
    const eventBanner = document.getElementById("event-banner");
    if (eventBanner) {
      eventBanner.className = "hidden";
      eventBanner.textContent = "";
    }
    const rewardBanner = document.getElementById("reward-banner");
    if (rewardBanner) {
      rewardBanner.className = "hidden";
      rewardBanner.textContent = "";
    }
    const buffsList = document.getElementById("buffs-list");
    if (buffsList) {
      buffsList.innerHTML = "";
    }
    const vignette = document.getElementById("vignette");
    if (vignette) {
      vignette.className = "";
    }
    this.fireRateMult = 1;
    this.damageMult = 1;
    this.extraProjectiles = 0;
    this.spreadBonus = 0;
    this.critChance = 0;
    this.allyCap = Math.min(GAME_BALANCE.caps.allyCap, LIMITS.allies);
    this.allySerial = 0;
    this.allies = [];
    this.allyModelRoots = [];
    this.allyVisualMode = "loading";
    this.bullets = Array.from({ length: LIMITS.bullets }, () => ({ active: false }));
    this.muzzleFlashes = Array.from({ length: LIMITS.muzzleFlashes }, () => ({ active: false }));
    this.enemies = Array.from({ length: LIMITS.enemies }, () => ({ active: false }));
    this.pickups = Array.from({ length: LIMITS.pickups }, () => ({ active: false }));
    this.particles = Array.from({ length: LIMITS.particles }, () => ({ active: false }));
    this.gates = [];
    this.popups = [];
    this.roadSegments = [];
    this.shotEvents = 0;
    this.muzzleEvents = 0;
    this.soundEvents = 0;
    this.gateApplications = 0;
    this.shootingLogTimer = 0;
    this.addAllies(1, { log: false });
    clampRunStats(this);
  }

  get allyCount() {
    return this.allies?.length ?? 0;
  }

  set allyCount(value) {
    const target = clamp(Math.floor(Number(value) || 0), 0, this.allyCap ?? LIMITS.allies);
    if (!this.allies) return;
    const current = this.allies.length;
    if (target > current) {
      this.addAllies(target - current);
    } else if (target < current) {
      this.removeAllies(current - target);
    }
  }

  createAlly(index) {
    const target = this.formationTarget(index, Math.max(1, index + 1));
    return {
      id: this.allySerial++,
      x: target.x,
      z: target.z,
      fireTimer: Math.random() * Math.max(0.08, this.currentWeapon().fireRate * this.fireRateMult),
    };
  }

  refreshAllyVisuals() {
    if (!this.allies) return;
    for (let i = 0; i < this.allies.length; i += 1) {
      this.ensureAllyModel(i);
    }
  }

  ensureAllyModel(index) {
    if (this.allyModelRoots[index]) return this.allyModelRoots[index];
    let root = null;
    if (this.playerModelTemplate && this.playerModelReady) {
      root = cloneCharacter(this.playerModelTemplate);
      root.userData.baseScale = root.userData.baseScale?.clone?.() ?? new THREE.Vector3(1, 1, 1);
      this.allyVisualMode = "playerModel";
    } else if (this.allyVisualMode === "fallback") {
      root = this.createAllyFallbackModel();
      this.allyVisualMode = "fallback";
    }
    if (!root) return null;

    // Glowing green indicator marker above head for readability
    const marker = new THREE.Mesh(
      new THREE.SphereGeometry(0.1, 5, 5),
      new THREE.MeshBasicMaterial({ color: 0x39ff14, toneMapped: false })
    );
    marker.position.set(0, 2.1, 0);
    root.add(marker);

    root.visible = false;
    this.scene.add(root);
    this.allyModelRoots[index] = root;
    return root;
  }

  createAllyFallbackModel() {
    const root = new THREE.Group();
    const body = new THREE.Mesh(
      new THREE.CapsuleGeometry(0.28, 0.42, 8, 12),
      new THREE.MeshStandardMaterial({ color: 0x5ef075, roughness: 0.62 }),
    );
    body.position.y = 0.62;
    root.add(body);
    root.userData.baseScale = root.scale.clone();
    return root;
  }

  applyAllyVisual(index, ally) {
    const root = this.ensureAllyModel(index);
    if (!root) return false;
    root.visible = true;
    root.position.set(ally.x, 0, ally.z);
    root.rotation.set(0, Math.PI, 0);
    const baseScale = root.userData.baseScale || tempScale.set(1, 1, 1);
    root.scale.copy(baseScale).multiplyScalar(ALLY_MODEL_SCALE);
    return true;
  }

  addAllies(count, { log = true } = {}) {
    const requested = Math.max(0, Math.floor(count));
    const before = this.allies.length;
    const amount = Math.min(requested, Math.max(0, this.allyCap - before));
    for (let i = 0; i < amount; i += 1) {
      this.allies.push(this.createAlly(this.allies.length));
      if (log) console.log(`[ALLY] spawned, total=${this.allies.length}`);
    }
    if (amount > 0) this.reflowAllies(0);
    this.updateHudNow?.();
    return { before, after: this.allies.length, added: amount };
  }

  removeAllies(count) {
    const amount = Math.min(Math.max(0, Math.floor(count)), this.allies.length);
    if (amount <= 0) return { before: this.allies.length, after: this.allies.length, removed: 0 };
    const before = this.allies.length;
    this.allies.splice(before - amount, amount);
    for (let i = this.allies.length; i < before; i += 1) {
      this.allyPool?.hide(i);
      if (this.allyModelRoots[i]) this.allyModelRoots[i].visible = false;
    }
    this.reflowAllies(0);
    this.updateHudNow?.();
    return { before, after: this.allies.length, removed: amount };
  }

  createScene() {
    const hemi = new THREE.HemisphereLight(0x9ecbff, 0x111724, 1.15);
    this.scene.add(hemi);
    const sun = new THREE.DirectionalLight(0xffffff, 1.9);
    sun.position.set(-7, 14, -6);
    this.scene.add(sun);

    this.playerRoot = new THREE.Group();
    this.scene.add(this.playerRoot);

    this.playerMesh = new THREE.Mesh(
      new THREE.CapsuleGeometry(0.42, 0.55, 8, 14),
      new THREE.MeshStandardMaterial({ color: 0x5ef075, roughness: 0.62, transparent: true, opacity: 0.55 }),
    );
    this.playerMesh.position.y = 0.78;
    this.playerRoot.add(this.playerMesh);

    // Glowing indicator marker above player's head for readability
    const marker = new THREE.Mesh(
      new THREE.SphereGeometry(0.15, 6, 6),
      new THREE.MeshBasicMaterial({ color: 0x00f3ff, toneMapped: false })
    );
    marker.position.set(0, 2.3, 0);
    this.playerRoot.add(marker);

    this.attachPlayerModel();

    this.roadGroup = new THREE.Group();
    this.scene.add(this.roadGroup);
    this.spawnInitialRoad();
  }

  async attachPlayerModel() {
    try {
      const template = await this.loader.load(ASSETS.player);
      const root = activateCharacter(template);
      root.userData.baseScale = root.userData.baseScale?.clone?.() ?? root.scale.clone();
      root.scale.copy(root.userData.baseScale).multiplyScalar(0.98);
      root.rotation.y = Math.PI;
      root.position.set(0, 0, 0);
      this.playerRoot.add(root);
      this.playerModel = root;
      this.playerModelTemplate = root;
      this.playerModelReady = true;
      this.allyVisualMode = "playerModel";
      this.playerMesh.visible = false;
      this.refreshAllyVisuals();
    } catch (error) {
      console.error("[Defense] Player model failed; keeping fallback capsule.", error);
      this.playerMesh.visible = true;
      this.playerModelReady = false;
      this.playerModelTemplate = null;
      this.allyVisualMode = "fallback";
      this.refreshAllyVisuals();
    }
  }

  createPools() {
    this.allyPool = new InstancedPool(this.scene, {
      capacity: LIMITS.allies,
      geometry: new THREE.BoxGeometry(0.45, 0.65, 0.45),
      material: new THREE.MeshStandardMaterial({ color: 0x5cbcff, roughness: 0.72 }),
    });
    this.bulletPool = new InstancedPool(this.scene, {
      capacity: LIMITS.bullets,
      geometry: new THREE.BoxGeometry(0.34, 0.34, 1.85),
      material: new THREE.MeshBasicMaterial({
        color: 0xfff276,
        depthTest: false,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        toneMapped: false,
      }),
    });
    this.bulletTrailPool = new InstancedPool(this.scene, {
      capacity: LIMITS.bullets,
      geometry: new THREE.BoxGeometry(0.34, 0.12, 5.8),
      material: new THREE.MeshBasicMaterial({
        color: 0xfff4a8,
        transparent: true,
        opacity: 0.9,
        depthTest: false,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        toneMapped: false,
      }),
    });
    this.muzzleFlashPool = new InstancedPool(this.scene, {
      capacity: LIMITS.muzzleFlashes,
      geometry: new THREE.BoxGeometry(0.7, 0.42, 1.05),
      material: new THREE.MeshBasicMaterial({
        color: 0xfff7a6,
        transparent: true,
        opacity: 0.95,
        depthTest: false,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        toneMapped: false,
      }),
    });
    this.enemyPool = new InstancedPool(this.scene, {
      capacity: LIMITS.enemies,
      geometry: new THREE.CapsuleGeometry(0.4, 0.52, 6, 10),
      material: new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.76, emissive: 0x2b2b2b }),
    });
    this.pickupPool = new InstancedPool(this.scene, {
      capacity: LIMITS.pickups,
      geometry: new THREE.SphereGeometry(0.16, 8, 8),
      material: new THREE.MeshBasicMaterial({ color: 0xffd85e }),
    });
    this.particlePool = new InstancedPool(this.scene, {
      capacity: LIMITS.particles,
      geometry: new THREE.BoxGeometry(0.16, 0.16, 0.16),
      material: new THREE.MeshBasicMaterial({ color: 0xff6b49 }),
    });
  }

  bindInput() {
    window.addEventListener("keydown", (event) => {
      if (event.key === "ArrowLeft" || event.key.toLowerCase() === "a") this.input.left = true;
      if (event.key === "ArrowRight" || event.key.toLowerCase() === "d") this.input.right = true;
      if (event.key === " ") {
        if (!this.running) {
          this.start();
        } else if (this.ultGauge >= 100) {
          this.triggerUltimate();
        }
      }
    });
    window.addEventListener("keyup", (event) => {
      if (event.key === "ArrowLeft" || event.key.toLowerCase() === "a") this.input.left = false;
      if (event.key === "ArrowRight" || event.key.toLowerCase() === "d") this.input.right = false;
    });
    window.addEventListener("pointerdown", (event) => {
      this.input.pointerDown = true;
      this.updatePointer(event);
    });
    window.addEventListener("pointermove", (event) => {
      if (this.input.pointerDown) this.updatePointer(event);
    });
    window.addEventListener("pointerup", () => {
      this.input.pointerDown = false;
    });
  }

  updatePointer(event) {
    const x01 = event.clientX / Math.max(1, window.innerWidth);
    this.input.targetX = this.screenRightSign() > 0
      ? LANES.minX + x01 * LANES.width
      : LANES.maxX - x01 * LANES.width;
  }

  screenRightSign() {
    const y = this.player?.y ?? 0.55;
    const z = this.player?.z ?? 0;
    screenProbeLeft.set(-1, y, z).project(this.camera);
    screenProbeRight.set(1, y, z).project(this.camera);
    return screenProbeRight.x >= screenProbeLeft.x ? 1 : -1;
  }

  start() {
    ui.startPanel.classList.add("hidden");
    this.running = true;
    if (this.debugBossPreview && !this.didDebugBossPreview) {
      this.didDebugBossPreview = true;
      setTimeout(() => this.forceBossPreview(), 900);
    }
  }

  resize() {
    const width = window.innerWidth;
    const height = window.innerHeight;
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  spawnInitialRoad() {
    for (let i = 0; i < 9; i += 1) {
      this.createRoadSegment(-28 + i * 36);
    }
  }

  createRoadSegment(z) {
    const group = new THREE.Group();
    group.position.z = z;

    const road = new THREE.Mesh(
      new THREE.BoxGeometry(13.2, 0.08, 36),
      new THREE.MeshStandardMaterial({ color: 0x0b1320, roughness: 0.9 }),
    );
    road.position.y = -0.04;
    group.add(road);

    for (const x of [-6.65, 6.65]) {
      const rail = new THREE.Mesh(
        new THREE.BoxGeometry(0.22, 0.36, 36),
        new THREE.MeshBasicMaterial({ color: 0x1bdfff }),
      );
      rail.position.set(x, 0.22, 0);
      group.add(rail);
    }

    for (const x of [-3, 0, 3]) {
      const lane = new THREE.Mesh(
        new THREE.BoxGeometry(0.04, 0.035, 21),
        new THREE.MeshBasicMaterial({ color: 0x14415c }),
      );
      lane.position.set(x, 0.02, 0);
      group.add(lane);
    }

    this.roadGroup.add(group);
    this.roadSegments.push(group);
  }

  updateRoad() {
    const frontZ = this.player.z + RUNNER.roadAheadDistance;
    for (const segment of this.roadSegments) {
      if (segment.position.z + 20 < this.player.z - RUNNER.roadRecycleBehind) {
        segment.position.z += RUNNER.roadSegmentLength * this.roadSegments.length;
      }
      if (segment.position.z < frontZ) {
        segment.visible = true;
      }
    }
  }

  animate() {
    requestAnimationFrame(() => this.animate());
    const now = performance.now();
    let dt = Math.min(0.033, (now - this.lastTime) / 1000 || 0.016);
    this.lastTime = now;

    if (this.running && !this.pausedForUpgrade) {
      this.update(dt);
    }
    this.renderer.render(this.scene, this.camera);
    this.fpsFrames += 1;
    this.fpsTimer += dt;
    if (this.fpsTimer > 0.5) {
      ui.fps.textContent = `${Math.round(this.fpsFrames / this.fpsTimer)} fps`;
      this.fpsFrames = 0;
      this.fpsTimer = 0;
    }
  }

  update(dt) {
    // Update buffers, events, UI overlays in real-time (not slowed down)
    this.updateBuffsAndOverlays(dt);

    let gameplayDt = dt;
    if (this.slowMoTimer > 0) {
      this.slowMoTimer -= dt;
      gameplayDt = dt * 0.35; // 65% slow down
      const vignette = document.getElementById("vignette");
      if (vignette && !vignette.classList.contains("slow-mo")) {
        vignette.classList.add("slow-mo");
      }
    } else {
      const vignette = document.getElementById("vignette");
      if (vignette) vignette.classList.remove("slow-mo");
    }

    this.elapsed += gameplayDt;
    this.updatePlayer(gameplayDt);
    this.updateGates(gameplayDt);
    this.updateAllies(gameplayDt);
    this.updateWeapon();
    this.updateBullets(gameplayDt);
    this.updateMuzzleFlashes(gameplayDt);
    this.updateEnemies(gameplayDt);
    this.updatePickups(gameplayDt);
    this.updateParticles(gameplayDt);
    this.updatePopups(gameplayDt);
    this.updateRoad();
    this.updateCamera(gameplayDt);
    this.models.update(this.enemies, this.player);
    this.updateHudThrottled(gameplayDt);
    this.logShootingDebug(gameplayDt);
  }

  updateBuffsAndOverlays(dt) {
    // 1. Tick buff timers
    let buffsChanged = false;
    for (const buff in this.activeBuffs) {
      if (this.activeBuffs[buff] > 0) {
        this.activeBuffs[buff] -= dt;
        if (this.activeBuffs[buff] <= 0) {
          this.activeBuffs[buff] = 0;
          buffsChanged = true;
          console.log(`[BUFF] ${buff} expired`);
        }
      }
    }
    
    // Update HTML buffs list if changed or periodically
    if (buffsChanged || this.activeBuffListDirty) {
      this.activeBuffListDirty = false;
      const buffsList = document.getElementById("buffs-list");
      if (buffsList) {
        buffsList.innerHTML = "";
        for (const buff in this.activeBuffs) {
          if (this.activeBuffs[buff] > 0) {
            const div = document.createElement("div");
            div.className = "buff-item";
            div.textContent = `${buff.toUpperCase()}: ${Math.ceil(this.activeBuffs[buff])}s`;
            buffsList.appendChild(div);
          }
        }
      }
    } else {
      // Just update countdowns
      const buffsList = document.getElementById("buffs-list");
      if (buffsList) {
        let i = 0;
        for (const buff in this.activeBuffs) {
          if (this.activeBuffs[buff] > 0) {
            const child = buffsList.children[i];
            if (child) {
              child.textContent = `${buff.toUpperCase()}: ${Math.ceil(this.activeBuffs[buff])}s`;
            }
            i++;
          }
        }
      }
    }

    // 2. Special Event Timer
    if (this.currentEvent) {
      this.currentEventTimer -= dt;
      if (this.currentEventTimer <= 0) {
        console.log(`[EVENT] Finished ${this.currentEvent}`);
        this.currentEvent = null;
        const banner = document.getElementById("event-banner");
        if (banner) {
          banner.className = "hidden";
          banner.textContent = "";
        }
      }
    } else {
      this.eventTimer += dt;
      if (this.eventTimer >= this.nextEventTime) {
        this.eventTimer = 0;
        this.nextEventTime = 45.0 + Math.random() * 45.0; // Next event in 45-90s
        this.triggerSpecialEvent();
      }
    }

    // 3. Reward Banner timer
    if (this.rewardBannerTimer > 0) {
      this.rewardBannerTimer -= dt;
      if (this.rewardBannerTimer <= 0) {
        const rewardBanner = document.getElementById("reward-banner");
        if (rewardBanner) {
          rewardBanner.classList.remove("show");
          setTimeout(() => {
            rewardBanner.classList.add("hidden");
          }, 300);
        }
      }
    }

    // 4. Ultimate Hud bar update
    const ultFill = document.getElementById("ult-fill");
    if (ultFill) {
      ultFill.style.width = `${Math.min(100, this.ultGauge)}%`;
    }
    const ultLabel = document.getElementById("ult-label");
    if (ultLabel) {
      if (this.ultGauge >= 100) {
        ultLabel.classList.add("ready");
        ultLabel.textContent = "ULTIMATE [SPACE] READY";
      } else {
        ultLabel.classList.remove("ready");
        ultLabel.textContent = "ULTIMATE CHARGING";
      }
    }

    // 5. Danger Vignette check
    const vignette = document.getElementById("vignette");
    if (vignette) {
      if (this.allies.length <= 3 && this.running) {
        vignette.classList.add("danger");
      } else {
        vignette.classList.remove("danger");
      }
    }
  }

  triggerSpecialEvent() {
    const events = ["GOLD RUSH", "WEAPON FEVER", "ALLY RUSH", "SLOW MOTION"];
    const chosen = events[Math.floor(Math.random() * events.length)];
    this.currentEvent = chosen;
    this.currentEventTimer = 15.0;
    
    const banner = document.getElementById("event-banner");
    if (banner) {
      banner.className = "";
      if (chosen === "GOLD RUSH") {
        banner.classList.add("gold-rush");
        banner.textContent = "★ GOLD RUSH ★";
      } else if (chosen === "WEAPON FEVER") {
        banner.classList.add("weapon-fever");
        banner.textContent = "⚡ WEAPON FEVER ⚡";
      } else if (chosen === "ALLY RUSH") {
        banner.classList.add("ally-rush");
        banner.textContent = "👥 ALLY FEVER (+10 Allies!)";
        this.addAllies(10);
      } else if (chosen === "SLOW MOTION") {
        banner.classList.add("slow-motion");
        banner.textContent = "⏳ SLOW MOTION ⏳";
        this.slowMoTimer = 15.0;
      }
    }
    console.log(`[EVENT] Started ${chosen}`);
  }

  triggerUltimate() {
    this.ultGauge = 0;
    this.cameraShake = 0.55;
    this.cameraZoomTimer = 2.0;

    // Show ultimate banner overlay
    this.showRewardBanner("ULTIMATE ACTIVE!", 1.5);

    // Freeze all enemies and deal 150 damage
    for (const enemy of this.enemies) {
      if (!enemy.active) continue;
      enemy.freezeTimer = 6.0;
      enemy.hp -= 150;
      enemy.flash = 0.22;
      this.spawnHit(enemy.x, enemy.z, 0xff00ff, 4, 0.25);
      if (enemy.hp <= 0) {
        this.killEnemy(enemy);
      }
    }

    // Bullet storm: 120 bullets in a circle
    const count = 120;
    for (let i = 0; i < count; i += 1) {
      const angle = (i * Math.PI * 2) / count;
      this.spawnBullet({
        x: this.player.x,
        y: 1.18,
        z: this.player.z + 0.5,
        dx: Math.sin(angle),
        dz: Math.cos(angle),
        speed: 38,
        damage: 60,
        color: 0xff00ff,
        splash: 1.8,
        bulletScale: 0.8,
        bulletLength: 0.8,
        trailWidth: 0.25,
        trailLength: 0.5,
        hitParticles: 3,
        hitSize: 0.22,
      });
    }

    // Spawn massive shockwave particles
    for (let i = 0; i < 40; i += 1) {
      const angle = Math.random() * Math.PI * 2;
      const speed = 4 + Math.random() * 8;
      this.spawnParticle(
        this.player.x,
        0.8,
        this.player.z,
        0xff33aa,
        0.4,
        speed * 0.15,
        0.8
      );
    }

    this.audio.play("fire:Rocket", 0.01); // Play an explosion sound
    console.log("[ULTIMATE] Fired Bullet Storm + Freeze Wave!");
  }

  showRewardBanner(text, duration = 1.2) {
    const rewardBanner = document.getElementById("reward-banner");
    if (rewardBanner) {
      rewardBanner.textContent = text;
      rewardBanner.className = "";
      // Force layout reflow
      void rewardBanner.offsetWidth;
      rewardBanner.classList.add("show");
      this.rewardBannerTimer = duration;
    }
  }

  updatePlayer(dt) {
    let move = 0;
    if (this.input.left) move -= 1;
    if (this.input.right) move += 1;
    if (this.input.pointerDown) {
      this.player.x += (this.input.targetX - this.player.x) * Math.min(1, dt * 12);
    } else {
      this.player.x += move * this.screenRightSign() * 8 * dt;
    }
    this.player.x = clamp(this.player.x, LANES.minX, LANES.maxX);
    this.player.prevZ = this.player.z;
    this.player.z += this.player.speed * dt;
    this.playerRoot.position.set(this.player.x, 0, this.player.z);
    this.playerRoot.rotation.y = Math.sin(this.elapsed * 9) * 0.035;

    this.player.shootTimer += dt;
    const weapon = this.currentWeapon();
    let fireRate = weapon.fireRate * this.fireRateMult;
    if (this.activeBuffs.AttackSpeed > 0) fireRate *= 0.5;
    if (this.currentEvent === "WEAPON FEVER") fireRate *= 0.5;

    if (this.player.shootTimer >= fireRate) {
      this.player.shootTimer = 0;
      this.fireWeapon(this.player.x, this.player.z + 0.55, 0);
    }
  }

  updateAllies(dt) {
    const weapon = this.currentWeapon();
    const total = this.allies.length;
    for (let i = 0; i < LIMITS.allies; i += 1) {
      if (i >= total) {
        this.allyPool.hide(i);
        if (this.allyModelRoots[i]) this.allyModelRoots[i].visible = false;
        continue;
      }
      const ally = this.allies[i];
      const { x: tx, z: tz } = this.formationTarget(i, total);
      ally.x += (tx - ally.x) * Math.min(1, dt * 9.5);
      ally.z += (tz - ally.z) * Math.min(1, dt * 9.5);
      ally.fireTimer += dt;
      let cadence = Math.max(0.045, weapon.fireRate * this.fireRateMult * 1.08);
      if (this.activeBuffs.AttackSpeed > 0) cadence *= 0.5;
      if (this.currentEvent === "WEAPON FEVER") cadence *= 0.5;

      if (ally.fireTimer >= cadence) {
        ally.fireTimer = Math.random() * cadence * 0.18;
        this.fireWeapon(ally.x, ally.z + 0.42, i * 0.37);
      }
      if (this.applyAllyVisual(i, ally)) {
        this.allyPool.hide(i);
      } else {
        this.allyPool.set(i, tempVec.set(ally.x, 0.42, ally.z), tempScale.set(0.5, 0.5, 0.5), 0x5ef075);
      }
    }
    this.allyPool.flush();
  }

  formationTarget(index, total = this.allyCount) {
    const cols = Math.min(20, Math.max(3, Math.floor(Math.sqrt(Math.max(1, total)) * 1.9)));
    const spacing = Math.max(0.42, 0.86 - total * 0.0014);
    const row = Math.floor(index / cols);
    const col = index % cols;
    const rowCount = Math.min(cols, total - row * cols);
    return {
      x: clamp(this.player.x + (col - (rowCount - 1) * 0.5) * spacing, LANES.minX, LANES.maxX),
      z: this.player.z - 1.2 - row * spacing * 0.95,
    };
  }

  reflowAllies(startIndex = 0) {
    for (let i = Math.max(0, startIndex); i < this.allyCount; i += 1) {
      const ally = this.allies[i];
      const target = this.formationTarget(i, this.allyCount);
      ally.x = target.x;
      ally.z = target.z;
      ally.fireTimer = Math.random() * Math.max(0.08, this.currentWeapon().fireRate * this.fireRateMult);
    }
  }

  fireWeapon(x, z, seed) {
    const weapon = this.currentWeapon();
    const effect = this.weaponEffect(weapon);
    let count = weapon.count + this.extraProjectiles;
    if (this.activeBuffs.TripleShot > 0) count += 2;
    let spread = weapon.spread + this.spreadBonus;
    if (this.activeBuffs.TripleShot > 0) spread += 5.0;
    this.shotEvents += 1;
    for (let i = 0; i < count; i += 1) {
      const lane = count === 1 ? 0 : (i - (count - 1) / 2) * 0.22;
      const angle = ((Math.random() - 0.5) * spread + lane * 3 + Math.sin(seed + i) * 0.4) * Math.PI / 180;
      let damage = Math.floor(weapon.damage * this.damageMult * (Math.random() < this.critChance ? 2.4 : 1));
      if (this.activeBuffs.Explosive > 0) damage = Math.floor(damage * 1.25);
      
      let color = weapon.color;
      if (this.activeBuffs.Explosive > 0) {
        color = 0xff3344; // Explosive bullets glow neon red
      } else if (this.activeBuffs.TripleShot > 0) {
        color = 0x00f3ff; // Triple shot bullets glow neon cyan
      }
      
      let splash = weapon.splash || 0;
      if (this.activeBuffs.Explosive > 0) splash = Math.max(splash, 2.2);

      this.spawnBullet({
        x: x + lane,
        y: 1.18,
        z,
        dx: Math.sin(angle),
        dz: Math.cos(angle),
        speed: weapon.speed,
        damage,
        color,
        splash,
        bulletScale: effect.bulletScale * (this.activeBuffs.Explosive > 0 ? 1.4 : 1),
        bulletLength: effect.bulletLength,
        trailWidth: effect.trailWidth * (this.activeBuffs.Explosive > 0 ? 1.4 : 1),
        trailLength: effect.trailLength,
        hitParticles: effect.hitParticles + (this.activeBuffs.Explosive > 0 ? 2 : 0),
        hitSize: effect.hitSize * (this.activeBuffs.Explosive > 0 ? 1.4 : 1),
        pierceCount: this.activeBuffs.Piercing > 0 ? 3 : 0,
      });
      if (effect.muzzlePerProjectile && i < 4) {
        this.spawnMuzzleFlash(x + lane, z + 0.6, color, seed + i, effect);
      }
    }
    if (!effect.muzzlePerProjectile) {
      this.spawnMuzzleFlash(x, z + 0.6, weapon.color, seed, effect);
    }
    this.playFireSound(weapon);
    for (let i = 0; i < (effect.particles || 0); i += 1) {
      this.spawnParticle(x + (Math.random() - 0.5) * 0.22, 0.72, z + 0.5, weapon.color, 0.1 + effect.hitSize * 0.35, 0.11 + effect.hitSize * 0.3, 0.16 + effect.flashTtl);
    }
    if (effect.smoke) {
      for (let i = 0; i < 2; i += 1) {
        this.spawnParticle(x + (Math.random() - 0.5) * 0.28, 0.6, z - 0.2, 0x8d94a1, 0.22, 0.08, 0.38);
      }
    }
  }

  weaponEffect(weapon) {
    return { ...DEFAULT_EFFECT, ...(weapon.effect || {}) };
  }

  spawnMuzzleFlash(x, z, color, seed, effect = DEFAULT_EFFECT) {
    for (let i = 0; i < LIMITS.muzzleFlashes; i += 1) {
      const flash = this.muzzleFlashes[i];
      if (flash.active) continue;
      Object.assign(flash, {
        active: true,
        x,
        y: 1.08,
        z,
        color,
        ttl: effect.flashTtl,
        maxTtl: effect.flashTtl,
        baseScale: effect.flashScale * (effect.wideFlash ? 1.35 : 1),
        wide: Boolean(effect.wideFlash),
        yaw: Math.sin(seed) * 0.12,
        pulse: 1 + Math.random() * 0.35,
      });
      this.muzzleEvents += 1;
      return;
    }
  }

  playFireSound(weapon) {
    this.soundEvents += 1;
    this.audio.play(`fire:${weapon.name}`, 0.018);
    if (typeof window.CustomEvent === "function") {
      window.dispatchEvent(new CustomEvent("defense-sound", { detail: { key: "fire", weapon: weapon.name } }));
    }
  }

  spawnBullet(data) {
    for (const bullet of this.bullets) {
      if (bullet.active) continue;
      Object.assign(bullet, data, { active: true, life: 1.6 });
      return;
    }
  }

  updateBullets(dt) {
    for (let i = 0; i < LIMITS.bullets; i += 1) {
      const bullet = this.bullets[i];
      if (!bullet.active) {
        this.bulletPool.hide(i);
        this.bulletTrailPool.hide(i);
        continue;
      }
      bullet.x += bullet.dx * bullet.speed * dt;
      bullet.z += bullet.dz * bullet.speed * dt;
      bullet.life -= dt;
      if (bullet.life <= 0 || bullet.z > this.player.z + 95 || bullet.x < LANES.minX - 2 || bullet.x > LANES.maxX + 2) {
        bullet.active = false;
        this.bulletPool.hide(i);
        this.bulletTrailPool.hide(i);
        continue;
      }
      const yaw = Math.atan2(bullet.dx, bullet.dz);
      const trailLength = bullet.trailLength ?? DEFAULT_EFFECT.trailLength;
      const trailX = bullet.x - bullet.dx * trailLength * 2.8;
      const trailZ = bullet.z - bullet.dz * trailLength * 2.8;
      this.bulletTrailPool.set(i, tempVec.set(trailX, bullet.y - 0.04, trailZ), tempScale.set(bullet.trailWidth ?? DEFAULT_EFFECT.trailWidth, 1.0, trailLength), bullet.color, yaw);
      this.bulletPool.set(i, tempVec.set(bullet.x, bullet.y, bullet.z), tempScale.set(bullet.bulletScale ?? DEFAULT_EFFECT.bulletScale, bullet.bulletScale ?? DEFAULT_EFFECT.bulletScale, bullet.bulletLength ?? DEFAULT_EFFECT.bulletLength), bullet.color, yaw);
    }
    this.bulletTrailPool.flush();
    this.bulletPool.flush();
  }

  updateMuzzleFlashes(dt) {
    for (let i = 0; i < LIMITS.muzzleFlashes; i += 1) {
      const flash = this.muzzleFlashes[i];
      if (!flash.active) {
        this.muzzleFlashPool.hide(i);
        continue;
      }
      flash.ttl -= dt;
      if (flash.ttl <= 0) {
        flash.active = false;
        this.muzzleFlashPool.hide(i);
        continue;
      }
      const fade = flash.ttl / flash.maxTtl;
      const scale = flash.baseScale * flash.pulse * (0.45 + fade * 1.8);
      this.muzzleFlashPool.set(i, tempVec.set(flash.x, flash.y, flash.z), tempScale.set(scale * (flash.wide ? 1.55 : 1), scale * 0.68, scale * (flash.wide ? 0.92 : 1.28)), flash.color, flash.yaw);
    }
    this.muzzleFlashPool.flush();
  }

  updateEnemies(dt) {
    const difficultyLevel = Math.floor(this.elapsed / 30);
    const spawnInterval = Math.max(0.25, 1.2 - difficultyLevel * 0.15);

    if (difficultyLevel !== this.lastLoggedDifficulty) {
      this.lastLoggedDifficulty = difficultyLevel;
      console.log(`[DIFFICULTY] Level changed to ${difficultyLevel} at elapsed=${this.elapsed.toFixed(1)}s. SpawnInterval=${spawnInterval.toFixed(2)}s`);
    }

    this.enemySpawnTimer += dt;
    if (this.enemySpawnTimer >= spawnInterval) {
      this.enemySpawnTimer = 0;
      this.spawnWave();
    }

    const bossDistance = GAME_BALANCE.waves.bossSpawnDistance;
    if (this.elapsed >= this.nextMidBossTime) {
      this.spawnEnemy("midBoss", 0, this.player.z + bossDistance, this.elapsed);
      this.nextMidBossTime += 90.0;
    }
    if (this.elapsed >= this.nextBigBossTime) {
      this.spawnEnemy("bigBoss", 0, this.player.z + bossDistance + 7, this.elapsed);
      this.nextBigBossTime += 90.0;
    }

    const bins = new Map();
    for (let i = 0; i < LIMITS.enemies; i += 1) {
      const enemy = this.enemies[i];
      if (!enemy.active) {
        this.enemyPool.hide(i);
        continue;
      }
      
      const isFrozen = enemy.freezeTimer && enemy.freezeTimer > 0;
      if (isFrozen) {
        enemy.freezeTimer -= dt;
      }
      
      const speedRamp = isFrozen ? 0.08 : (1 + Math.min(0.58, this.elapsed / 165));
      enemy.z -= enemy.speed * speedRamp * dt;
      enemy.x += (((isFrozen ? 0 : enemy.diagonalSpeed) || 0) + Math.sin(enemy.z * 0.17 + enemy.seed) * enemy.sway * (isFrozen ? 0.08 : 1) + enemy.knockX) * dt;
      enemy.z += enemy.knockZ * dt;
      enemy.knockX *= Math.max(0, 1 - dt * 8);
      enemy.knockZ *= Math.max(0, 1 - dt * 8);

      if (enemy.x < LANES.minX) {
        enemy.x = LANES.minX;
        if (enemy.diagonalSpeed < 0) enemy.diagonalSpeed *= -1;
      } else if (enemy.x > LANES.maxX) {
        enemy.x = LANES.maxX;
        if (enemy.diagonalSpeed > 0) enemy.diagonalSpeed *= -1;
      }

      enemy.flash = Math.max(0, enemy.flash - dt);
      if (enemy.z < this.player.z - 12) {
        enemy.active = false;
        this.enemyPool.hide(i);
        continue;
      }
      const key = binKey(enemy.x, enemy.z);
      let list = bins.get(key);
      if (!list) {
        list = [];
        bins.set(key, list);
      }
      list.push(enemy);
      const flashColor = enemy.flash > 0 ? 0xffffff : enemy.color;
      this.enemyPool.set(i, tempVec.set(enemy.x, enemy.height * 0.5, enemy.z), tempScale.set(enemy.scale, enemy.height, enemy.scale), flashColor);
    }
    this.enemyPool.flush();

    for (const bullet of this.bullets) {
      if (!bullet.active) continue;
      const candidates = collectBins(bins, bullet.x, bullet.z);
      for (const enemy of candidates) {
        if (!enemy.active) continue;
        if (bullet.lastHitEnemy === enemy) continue; // Prevent same bullet hitting same enemy twice in pierce
        
        const dx = bullet.x - enemy.x;
        const dz = bullet.z - enemy.z;
        const radius = enemy.radius + 0.18;
        if (dx * dx + dz * dz > radius * radius) continue;
        
        bullet.lastHitEnemy = enemy;
        if (bullet.pierceCount && bullet.pierceCount > 0) {
          bullet.pierceCount -= 1;
        } else {
          bullet.active = false;
        }
        
        enemy.hp -= bullet.damage;
        enemy.flash = 0.11;
        enemy.knockX += bullet.dx * 2.2;
        enemy.knockZ += 4.2;
        this.spawnHit(enemy.x, enemy.z, bullet.color, bullet.hitParticles, bullet.hitSize);
        this.cameraShake = Math.min(0.055, this.cameraShake + 0.008 + Math.min(0.012, this.combo * 0.0007));
        if (bullet.splash > 0) this.applySplash(enemy.x, enemy.z, bullet.splash, bullet.damage * 0.55);
        if (enemy.hp <= 0) this.killEnemy(enemy);
        break;
      }
    }

    for (const enemy of this.enemies) {
      if (!enemy.active) continue;
      const dx = enemy.x - this.player.x;
      const dz = enemy.z - this.player.z;
      if (dx * dx + dz * dz < 0.85) {
        enemy.active = false;
        if (this.activeBuffs.Shield > 0) {
          // Play collision shield effect
          this.spawnHit(enemy.x, enemy.z, 0x00f3ff, 4, 0.2);
          this.audio.play("fire:Shotgun", 0.05);
          // Elite check or ultimate charging on collide
          this.killEnemy(enemy);
        } else {
          this.removeAllies(1);
        }
      }
    }
  }

  spawnWave() {
    this.wave += 1;
    for (const spawn of buildWavePlan(this.elapsed, this.player.z)) {
      this.spawnEnemy(spawn.type, spawn.x, spawn.z, this.elapsed);
    }
  }

  spawnEnemy(type, x, z, elapsed) {
    const stats = balancedEnemyStats(type, elapsed);
    for (const enemy of this.enemies) {
      if (enemy.active) continue;
      
      const isElite = (type !== "midBoss" && type !== "bigBoss") && (Math.random() < 0.15);
      let diagonalSpeed = 0;
      if ((type === "basic" || type === "fast") && !isElite) {
        if (Math.random() < 0.35) {
          diagonalSpeed = (Math.random() - 0.5) * 1.5;
        }
      }
      
      let color = stats.color;
      let scale = stats.scale;
      let height = stats.height;
      let hp = stats.hp;
      let speed = stats.speed;
      let points = stats.points;
      let radius = stats.radius;
      
      if (isElite) {
        color = 0x00f3ff; // Glowing neon cyan
        scale *= 1.4;
        height *= 1.4;
        hp *= 3.0;
        speed *= 1.35;
        points *= 10;
        radius *= 1.4;
      }
      
      Object.assign(enemy, stats, {
        active: true,
        isElite,
        type,
        modelType: type === "midBoss" ? "hwacat" : type === "bigBoss" ? "hwacatAngry" : "cyclopse",
        x,
        z,
        color,
        scale,
        height,
        hp,
        speed,
        points,
        radius,
        seed: Math.random() * 20,
        flash: 0,
        knockX: 0,
        knockZ: 0,
        diagonalSpeed,
        freezeTimer: 0,
      });
      return;
    }
  }

  applySplash(x, z, radius, damage) {
    const r2 = radius * radius;
    for (const enemy of this.enemies) {
      if (!enemy.active) continue;
      const dx = x - enemy.x;
      const dz = z - enemy.z;
      if (dx * dx + dz * dz <= r2) {
        enemy.hp -= damage;
        if (enemy.hp <= 0) this.killEnemy(enemy);
      }
    }
  }

  killEnemy(enemy) {
    if (!enemy.active) return;
    enemy.active = false;
    this.kills += enemy.points;
    this.combo += enemy.points;
    this.comboTimer = 2.2;
    
    // Gold Rush doubles coin yields
    const goldMult = this.currentEvent === "GOLD RUSH" ? 2 : 1;
    this.coins += enemy.points * (1 + Math.min(8, Math.floor(this.combo / 12))) * goldMult;
    
    // Add exp (elites give 5x points value)
    const expGain = killExp(enemy.points) * (enemy.isElite ? 5 : 1);
    this.addExp(expGain);

    // Charge Ultimate gauge
    this.ultGauge = Math.min(100, this.ultGauge + (enemy.isElite ? 5 : 1));

    // Spawn pickups: Elite drops 10 coins, normal drops points.
    const coinValue = enemy.isElite ? 10 : enemy.points;
    this.spawnPickup(enemy.x, enemy.z, coinValue, false);
    
    // Buff drops: Elite 100%, normal 4%
    if (enemy.isElite || Math.random() < 0.04) {
      this.spawnPickup(enemy.x, enemy.z, 0, true);
    }

    this.spawnDeath(enemy.x, enemy.z, enemy.points);
    this.cameraShake = Math.min(0.12, this.cameraShake + 0.025 + enemy.points * 0.002);

    // Chain Explosions
    const isExplosive = this.activeBuffs.Explosive > 0 || Math.random() < 0.12;
    if (isExplosive) {
      this.explodeEnemy(enemy.x, enemy.z);
    }
  }

  explodeEnemy(x, z, damage = 20) {
    this.audio.play("fire:Rocket", 0.05);
    
    // Explosion particles
    for (let i = 0; i < 8; i += 1) {
      this.spawnParticle(
        x + (Math.random() - 0.5) * 0.4,
        0.6,
        z + (Math.random() - 0.5) * 0.4,
        0xff5500,
        0.3,
        1.5,
        0.4
      );
    }
    
    const r2 = 2.5 * 2.5;
    for (const target of this.enemies) {
      if (!target.active || target.hp <= 0) continue;
      const dx = x - target.x;
      const dz = z - target.z;
      if (dx * dx + dz * dz <= r2) {
        target.hp -= damage;
        target.flash = 0.15;
        target.knockX += (target.x - x) * 2.0;
        target.knockZ += (target.z - z) * 2.0;
        if (target.hp <= 0) {
          this.killEnemy(target);
        }
      }
    }
  }

  updateGates(dt) {
    if (this.elapsed >= this.nextGateTime) {
      const spawnZ = this.player.z + GAME_BALANCE.gates.triggerLookahead;
      this.spawnGatePair(spawnZ);
      this.nextGateTime += GAME_BALANCE.gates.ally_gate_interval;
    }
    for (const gate of this.gates) {
      if (!gate.active) continue;
      gate.group.position.y = 1.1 + Math.sin(this.elapsed * 3 + gate.z) * 0.06;
      gate.left.sprite.lookAt(this.camera.position);
      gate.right.sprite.lookAt(this.camera.position);
      const crossed = this.player.z >= gate.z;
      if (crossed) {
        const hit = Math.abs(this.player.x - gate.left.x) < RUNNER.gateHitWidth ? gate.left
          : Math.abs(this.player.x - gate.right.x) < RUNNER.gateHitWidth ? gate.right
            : null;
        if (hit) {
          this.applyGate(hit.spec, gate);
        }
        gate.active = false;
        gate.consumed = true;
        gate.group.visible = false;
      }
    }
  }

  spawnGatePair(z) {
    const group = new THREE.Group();
    group.position.z = z;
    const leftSpec = rollBalancedGateSpec({ wave: this.wave });
    const rightSpec = rollBalancedGateSpec({ wave: this.wave, excludeKey: leftSpec.key });
    const left = this.createGate(-2.65, leftSpec);
    const right = this.createGate(2.65, rightSpec);
    group.add(left.mesh, right.mesh, left.sprite, right.sprite);
    this.scene.add(group);
    this.gates.push({ z, group, left, right, active: true });
  }

  createGate(x, spec) {
    const mesh = new THREE.Mesh(
      new THREE.BoxGeometry(3.35, 2.75, 0.28),
      new THREE.MeshBasicMaterial({ color: spec.color, transparent: true, opacity: 0.86, toneMapped: false }),
    );
    mesh.position.set(x, 1.1, 0);
    const sprite = createTextSprite(spec.label, spec.dramatic ? "#ffe66d" : "#ffffff", "ARMY");
    sprite.position.set(x, 1.48, -0.42);
    sprite.scale.set(6.9, 2.7, 1);
    return { x, spec, mesh, sprite };
  }

  applyGate(spec, gate = null) {
    if (gate?.consumed) return;
    if (gate) gate.consumed = true;
    const before = this.allies.length;
    if (spec.key === "add") {
      this.addAllies(spec.value);
    } else if (spec.key === "mult") {
      this.addAllies(before * Math.max(0, spec.value - 1));
    } else {
      applyProgressionReward(this, spec.key);
      if (spec.key === "random") {
        this.weaponIndex = Math.max(this.weaponIndex, 2 + Math.floor(Math.random() * Math.max(1, WEAPONS.length - 2)));
      }
    }
    const after = this.allies.length;
    const gained = after - before;
    console.log(`[GATE] type=${spec.label}, before=${before}, after=${after}`);
    this.gateApplications += 1;
    this.reflowAllies(0);
    this.updateHudNow();

    // Show dramatic popups
    const popupText = spec.key === "mult" ? `ALLY COUNT x${spec.value}!` : gained > 0 ? `+${gained} ALLIES!` : spec.label;
    this.showRewardBanner(popupText, 1.2);

    // Apply brief slow-motion feedback
    if (spec.dramatic) {
      this.slowMoTimer = 1.2;
    } else {
      this.slowMoTimer = 0.6;
    }

    this.spawnGateBurst(this.player.x, this.player.z);
    this.cameraShake = 0.16;
    return { before, after };
  }

  currentWeapon() {
    return WEAPONS[this.weaponIndex];
  }

  updateWeapon() {
    this.weaponIndex = weaponIndexForState({
      currentIndex: this.weaponIndex,
      kills: this.kills,
      level: this.level,
      wave: this.wave,
      elapsed: this.elapsed,
    });
  }

  addExp(amount) {
    this.exp += amount;
    while (this.exp >= this.expNeed) {
      this.exp -= this.expNeed;
      this.level += 1;
      this.expNeed = nextExpNeed(this.expNeed, this.level);
      this.pendingUpgradeCount += 1;
    }
    if (this.pendingUpgradeCount > 0 && !this.pausedForUpgrade) {
      this.showUpgrade();
    }
  }

  showUpgrade() {
    if (this.pendingUpgradeCount <= 0) return;
    this.pausedForUpgrade = true;
    ui.upgradeOptions.innerHTML = "";
    const picks = shuffle([...UPGRADES]).slice(0, 3);
    for (const upgrade of picks) {
      const button = document.createElement("button");
      button.className = "upgrade-card";
      button.innerHTML = `<strong>${upgrade.title}</strong>${upgrade.body}`;
      button.addEventListener("click", () => {
        this.applyUpgrade(upgrade.key);
        this.pendingUpgradeCount = Math.max(0, this.pendingUpgradeCount - 1);
        ui.upgradePanel.classList.add("hidden");
        
        // Reward visual feedback
        this.showRewardBanner(upgrade.title.toUpperCase(), 1.2);
        this.slowMoTimer = 0.8; // Trigger short slow motion

        if (this.pendingUpgradeCount > 0) {
          this.showUpgrade();
        } else {
          this.pausedForUpgrade = false;
        }
      });
      ui.upgradeOptions.appendChild(button);
    }
    ui.upgradePanel.classList.remove("hidden");
  }

  applyUpgrade(key) {
    if (key === "allies") {
      this.addAllies(5);
      return;
    }
    applyProgressionReward(this, key);
  }

  spawnPickup(x, z, value, isBuff = false, buffType = "") {
    if (isBuff) {
      const pickup = this.pickups.find((entry) => !entry.active);
      if (!pickup) return;
      const buffTypes = ["AttackSpeed", "TripleShot", "Piercing", "Explosive", "Shield", "Magnet", "Freeze"];
      const chosen = buffType || buffTypes[Math.floor(Math.random() * buffTypes.length)];
      Object.assign(pickup, {
        active: true,
        isBuff: true,
        buffType: chosen,
        x,
        y: 0.45,
        z,
        vx: (Math.random() - 0.5) * 1.5,
        vz: (Math.random() - 0.5) * 1.5,
        value: 0,
      });
      return;
    }

    const coinCount = value;
    for (let i = 0; i < Math.min(15, 1 + coinCount); i += 1) {
      const pickup = this.pickups.find((entry) => !entry.active);
      if (!pickup) return;
      Object.assign(pickup, {
        active: true,
        isBuff: false,
        buffType: "",
        x: x + (Math.random() - 0.5) * 0.8,
        y: 0.35,
        z: z + (Math.random() - 0.5) * 0.8,
        vx: (Math.random() - 0.5) * 2.5,
        vz: (Math.random() - 0.5) * 2.5,
        value: 1,
      });
    }
  }

  updatePickups(dt) {
    for (let i = 0; i < LIMITS.pickups; i += 1) {
      const pickup = this.pickups[i];
      if (!pickup.active) {
        this.pickupPool.hide(i);
        continue;
      }
      const dx = this.player.x - pickup.x;
      const dz = this.player.z - pickup.z;
      const d2 = dx * dx + dz * dz;

      // Triple pull range if Magnet is active
      const pullRange = this.activeBuffs.Magnet > 0 ? 400 : 42;
      if (d2 < pullRange) {
        const inv = 1 / Math.max(0.5, Math.sqrt(d2));
        const speed = this.activeBuffs.Magnet > 0 ? 32 : 18;
        pickup.vx += dx * inv * speed * dt;
        pickup.vz += dz * inv * speed * dt;
      }
      pickup.x += pickup.vx * dt;
      pickup.z += pickup.vz * dt;
      pickup.vx *= 0.94;
      pickup.vz *= 0.94;

      if (d2 < 0.75) {
        if (pickup.isBuff) {
          const type = pickup.buffType;
          this.activeBuffs[type] = 15.0; // Last 15 seconds
          this.activeBuffListDirty = true;
          this.showRewardBanner(`${type.toUpperCase()} ACTIVE!`, 1.0);
          this.audio.play("fire:Dual Pistol", 0.01);
          
          if (type === "Freeze") {
            // Apply freeze wave effect immediately
            for (const enemy of this.enemies) {
              if (enemy.active) {
                enemy.freezeTimer = 10.0;
                enemy.flash = 0.15;
              }
            }
          }
        } else {
          this.coins += pickup.value;
        }
        pickup.active = false;
        continue;
      }

      if (pickup.isBuff) {
        const color = pickup.buffType === "Shield" ? 0x00f3ff : 0xff00ff;
        this.pickupPool.set(i, tempVec.set(pickup.x, 0.45, pickup.z), tempScale.set(2.2, 2.2, 2.2), color);
      } else {
        this.pickupPool.set(i, tempVec.set(pickup.x, pickup.y, pickup.z), tempScale.set(1, 1, 1), 0xffd85e);
      }
    }
    this.pickupPool.flush();
  }

  spawnHit(x, z, color, count = DEFAULT_EFFECT.hitParticles, size = DEFAULT_EFFECT.hitSize) {
    for (let i = 0; i < count; i += 1) {
      this.spawnParticle(x, 0.62, z, i === 0 ? 0xffffff : color, size, size, 0.18 + size * 0.42);
    }
  }

  spawnDeath(x, z, points) {
    for (let i = 0; i < Math.min(12, 4 + points); i += 1) this.spawnParticle(x, 0.62, z, 0xff684a, 0.34, 0.36, 0.46);
  }

  spawnGateBurst(x, z) {
    for (let i = 0; i < 18; i += 1) this.spawnParticle(x, 1.0, z, 0x5df5ff, 0.34, 0.42, 0.55);
  }

  spawnPopup(text, color, x, z) {
    const sprite = createTextSprite(text, `#${color.toString(16).padStart(6, "0")}`, "POWER");
    sprite.position.set(x, 2.75, z);
    sprite.scale.set(6.4, 2.5, 1);
    this.scene.add(sprite);
    this.popups.push({ sprite, ttl: 1.05, maxTtl: 1.05, vz: 4.8 });
  }

  spawnParticle(x, y, z, color, size, speed, ttl) {
    const particle = this.particles.find((entry) => !entry.active);
    if (!particle) return;
    Object.assign(particle, {
      active: true,
      x,
      y,
      z,
      vx: (Math.random() - 0.5) * speed * 9,
      vy: Math.random() * speed * 8,
      vz: (Math.random() - 0.5) * speed * 9,
      ttl,
      maxTtl: ttl,
      color,
      size,
    });
  }

  updateParticles(dt) {
    for (let i = 0; i < LIMITS.particles; i += 1) {
      const particle = this.particles[i];
      if (!particle.active) {
        this.particlePool.hide(i);
        continue;
      }
      particle.ttl -= dt;
      if (particle.ttl <= 0) {
        particle.active = false;
        this.particlePool.hide(i);
        continue;
      }
      particle.x += particle.vx * dt;
      particle.y += particle.vy * dt;
      particle.z += particle.vz * dt;
      particle.vy -= dt * 5;
      const s = particle.size * Math.max(0.15, particle.ttl / particle.maxTtl);
      this.particlePool.set(i, tempVec.set(particle.x, particle.y, particle.z), tempScale.set(s, s, s), particle.color);
    }
    this.particlePool.flush();
  }

  updatePopups(dt) {
    for (let i = this.popups.length - 1; i >= 0; i -= 1) {
      const popup = this.popups[i];
      popup.ttl -= dt;
      popup.sprite.lookAt(this.camera.position);
      popup.sprite.position.y += dt * 1.1;
      popup.sprite.position.z += popup.vz * dt;
      const alpha = Math.max(0, popup.ttl / popup.maxTtl);
      popup.sprite.material.opacity = alpha;
      if (popup.ttl <= 0) {
        this.scene.remove(popup.sprite);
        popup.sprite.material.map?.dispose();
        popup.sprite.material.dispose();
        this.popups.splice(i, 1);
      }
    }
  }

  updateCamera(dt) {
    this.comboTimer -= dt;
    if (this.comboTimer <= 0) this.combo = 0;
    const shake = this.cameraShake;
    this.cameraShake = Math.max(0, this.cameraShake - dt * 0.75);
    const sx = (Math.random() - 0.5) * shake;
    const sy = (Math.random() - 0.5) * shake;

    let zoomOffset = 0;
    if (this.cameraZoomTimer && this.cameraZoomTimer > 0) {
      this.cameraZoomTimer -= dt;
      zoomOffset = 4.0 * Math.min(1.0, this.cameraZoomTimer);
    }

    const target = tempVec.set(0 + sx, 13.5 + sy + zoomOffset * 0.5, this.player.z - 10.5 - zoomOffset);
    this.camera.position.lerp(target, Math.min(1, dt * 7));
    this.camera.lookAt(0, 0.6, this.player.z + 6);
  }

  updateHudThrottled(dt) {
    this.uiTimer += dt;
    if (this.uiTimer < 0.08) return;
    this.uiTimer = 0;
    this.updateHudNow();
  }

  updateHudNow() {
    const weapon = this.currentWeapon();
    ui.army.textContent = `${this.allies.length}`;
    ui.weapon.textContent = weapon.name;
    ui.kills.textContent = `${this.kills}`;
    ui.coins.textContent = `${this.coins}`;
    ui.level.textContent = `${this.level}`;
    ui.dps.textContent = `${Math.round((weapon.damage * this.damageMult * (weapon.count + this.extraProjectiles)) / Math.max(0.035, weapon.fireRate * this.fireRateMult))}`;
    ui.expFill.style.width = `${Math.min(100, (this.exp / this.expNeed) * 100)}%`;
    ui.combo.textContent = this.combo >= 4 ? `COMBO x${this.combo}` : "";
  }

  activeBulletCount() {
    return this.bullets.reduce((count, bullet) => count + (bullet.active ? 1 : 0), 0);
  }

  logShootingDebug(dt) {
    this.shootingLogTimer += dt;
    if (this.shootingLogTimer < 1) return;
    this.shootingLogTimer = 0;
    console.log(`[SHOOTING] player/allies firing, ally_count=${this.allies.length}, active_bullets=${this.activeBulletCount()}`);
  }

  getDebugState() {
    return {
      running: this.running,
      pausedForUpgrade: this.pausedForUpgrade,
      elapsed: Number(this.elapsed.toFixed(2)),
      wave: this.wave,
      level: this.level,
      exp: Math.floor(this.exp),
      expNeed: this.expNeed,
      pendingUpgradeCount: this.pendingUpgradeCount,
      allies: this.allies.length,
      weapon: this.currentWeapon().name,
      weaponEffect: this.currentWeapon().effect || null,
      kills: this.kills,
      coins: this.coins,
      activeEnemies: this.enemies.filter((enemy) => enemy.active).length,
      activeBullets: this.activeBulletCount(),
      activeMuzzleFlashes: this.muzzleFlashes.filter((flash) => flash.active).length,
      shotEvents: this.shotEvents,
      muzzleEvents: this.muzzleEvents,
      soundEvents: this.soundEvents,
      gateApplications: this.gateApplications,
      screenRightSign: this.screenRightSign(),
      allyVisualMode: this.allyVisualMode,
      allyModelCount: this.allyModelRoots.filter(Boolean).length,
      modelReady: this.models.ready,
      visibleModels: this.models.visibleCount,
      modelLoadError: this.models.loadError ? String(this.models.loadError?.message || this.models.loadError) : null,
      loaderStatus: Object.fromEntries(this.loader.status.entries()),
    };
  }

  forceWave(count = 1) {
    for (let i = 0; i < count; i += 1) this.spawnWave();
    return this.getDebugState();
  }

  forceBossPreview() {
    this.spawnEnemy("midBoss", -2.4, this.player.z + 34, Math.max(this.wave, 6));
    this.spawnEnemy("bigBoss", 2.4, this.player.z + 42, Math.max(this.wave, 12));
    return this.getDebugState();
  }
}

function binKey(x, z) {
  return `${Math.floor((x + 8) / 2)}:${Math.floor(z / 4)}`;
}

function collectBins(bins, x, z) {
  const result = [];
  const bx = Math.floor((x + 8) / 2);
  const bz = Math.floor(z / 4);
  for (let ix = bx - 1; ix <= bx + 1; ix += 1) {
    for (let iz = bz - 1; iz <= bz + 1; iz += 1) {
      const list = bins.get(`${ix}:${iz}`);
      if (list) result.push(...list);
    }
  }
  return result;
}

function createTextSprite(text, color, subtitle = "POWER") {
  const canvas = document.createElement("canvas");
  canvas.width = 768;
  canvas.height = 320;
  const ctx = canvas.getContext("2d");
  ctx.fillStyle = "rgba(0,0,0,0)";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.shadowColor = color;
  ctx.shadowBlur = 34;
  ctx.font = "900 132px Arial";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.strokeStyle = "rgba(0,0,0,0.7)";
  ctx.lineWidth = 18;
  ctx.strokeText(text, 384, 132);
  ctx.fillStyle = color;
  ctx.fillText(text, 384, 132);
  ctx.shadowBlur = 0;
  ctx.font = "900 46px Arial";
  ctx.lineWidth = 10;
  ctx.strokeText(subtitle, 384, 230);
  ctx.fillStyle = "rgba(255,255,255,0.85)";
  ctx.fillText(subtitle, 384, 230);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthWrite: false,
    depthTest: false,
    toneMapped: false,
  });
  return new THREE.Sprite(material);
}

function shuffle(items) {
  for (let i = items.length - 1; i > 0; i -= 1) {
    const j = Math.floor(Math.random() * (i + 1));
    [items[i], items[j]] = [items[j], items[i]];
  }
  return items;
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

const game = new DefenseGame();
game.animate();
