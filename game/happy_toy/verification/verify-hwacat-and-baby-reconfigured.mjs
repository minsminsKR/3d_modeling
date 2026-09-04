import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("=== Testing Reconfigured Hwacat (2F) and Baby (B1) Events ===");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const consoleLogs = [];
const browserErrors = [];

page.on("console", (msg) => {
  const t = msg.text();
  consoleLogs.push(t);
  if (msg.type() === "error") {
    browserErrors.push(t);
  }
});
page.on("pageerror", (err) => browserErrors.push(err.message));

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 10000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 10000 });
  console.log("Game initialized on port 8010.");

  // -------------------------------------------------------------
  // Test 1: Verify Configs
  // -------------------------------------------------------------
  console.log("\n[TEST 1] Verifying config parameters in Game and gameConfig...");
  const configChecks = await page.evaluate(async () => {
    const game = window.__happyToy;
    const { MAP_CONFIG, ENEMY_CONFIGS, HWACAT_ANGRY_ENEMY_CONFIG } = await import("./src/config/gameConfig.js");

    const hwacatEvent = game.mirrorEvents[0];
    const hwacatCfg = hwacatEvent.config;

    const babyCfg = ENEMY_CONFIGS.find(e => e.id === "baby-workshop");
    const keyWorkshop = MAP_CONFIG.keys.find(k => k.id === "key-workshop");
    const keyHwacat = MAP_CONFIG.keys.find(k => k.id === "key-hwacat");
    const mirrorCfg = MAP_CONFIG.mirrorEvents[0];

    const babyIntro = game.monsterIntroManager.events.find(e => e.constructor.name === "BabyIntroEvent");

    return {
      gameHwacat: {
        triggerPosition: hwacatCfg.triggerPosition,
        triggerRadius: hwacatCfg.triggerRadius,
        spawnPosition: hwacatCfg.spawnPosition,
        lookAtPosition: hwacatCfg.lookAtPosition,
        paintingId: hwacatCfg.paintingId,
        paintingDropTargetPosition: hwacatCfg.paintingDropTargetPosition,
        rewardKeyId: hwacatCfg.rewardKeyId,
      },
      configHwacat: {
        triggerPosition: mirrorCfg.triggerPosition,
        triggerRadius: mirrorCfg.triggerRadius,
        spawnPosition: mirrorCfg.spawnPosition,
        lookAtPosition: mirrorCfg.lookAtPosition,
        paintingId: mirrorCfg.paintingId,
        paintingDropTargetPosition: mirrorCfg.paintingDropTargetPosition,
        rewardKeyId: mirrorCfg.rewardKeyId,
      },
      hwacatAngrySpawn: HWACAT_ANGRY_ENEMY_CONFIG.spawn,
      babyWorkshopSpawn: babyCfg ? babyCfg.spawn : null,
      keyWorkshopPos: keyWorkshop ? keyWorkshop.position : null,
      keyHwacatPos: keyHwacat ? keyHwacat.position : null,
      babyIntro: {
        triggerPosition: [babyIntro.triggerPosition.x, babyIntro.triggerPosition.y, babyIntro.triggerPosition.z],
        triggerRadius: babyIntro.triggerRadius,
        babyLookTarget: [babyIntro.babyLookTarget.x, babyIntro.babyLookTarget.y, babyIntro.babyLookTarget.z],
      },
    };
  });

  console.log("Config checks result:", JSON.stringify(configChecks, null, 2));

  // Assert Hwacat Config
  assert(configChecks.gameHwacat.triggerPosition[0] === -19.0 && configChecks.gameHwacat.triggerPosition[1] === 5.0 && configChecks.gameHwacat.triggerPosition[2] === -16.0, "Game hwacat triggerPosition must be [-19.0, 5.0, -16.0]");
  assert(configChecks.gameHwacat.triggerRadius === 2.2, "Game hwacat triggerRadius must be 2.2");
  assert(configChecks.gameHwacat.spawnPosition[0] === -22.5 && configChecks.gameHwacat.spawnPosition[1] === 5.0 && configChecks.gameHwacat.spawnPosition[2] === -16.0, "Game hwacat spawnPosition must be [-22.5, 5.0, -16.0]");
  assert(configChecks.gameHwacat.lookAtPosition[0] === -22.5 && configChecks.gameHwacat.lookAtPosition[1] === 6.2 && configChecks.gameHwacat.lookAtPosition[2] === -16.0, "Game hwacat lookAtPosition must be [-22.5, 6.2, -16.0]");
  assert(configChecks.gameHwacat.paintingId === "upper-hwa-painting", "paintingId must be upper-hwa-painting");
  assert(configChecks.gameHwacat.paintingDropTargetPosition[0] === -22.5 && configChecks.gameHwacat.paintingDropTargetPosition[1] === 5.08 && configChecks.gameHwacat.paintingDropTargetPosition[2] === -16.0, "paintingDropTargetPosition must be [-22.5, 5.08, -16.0]");
  assert(configChecks.gameHwacat.rewardKeyId === "key-hwacat", "rewardKeyId must be key-hwacat");

  // Assert Baby Config
  assert(configChecks.babyWorkshopSpawn[0] === 10.5 && configChecks.babyWorkshopSpawn[1] === -5.0 && configChecks.babyWorkshopSpawn[2] === 30.0, "Baby spawn must be [10.5, -5.0, 30.0]");
  assert(configChecks.keyWorkshopPos[0] === 10.5 && configChecks.keyWorkshopPos[1] === -5.0 && configChecks.keyWorkshopPos[2] === 27.5, "key-workshop position must be [10.5, -5.0, 27.5]");
  assert(configChecks.babyIntro.triggerPosition[0] === 14.5 && configChecks.babyIntro.triggerPosition[1] === -5.0 && configChecks.babyIntro.triggerPosition[2] === 32.0, "BabyIntro triggerPosition must be [14.5, -5.0, 32.0]");
  assert(configChecks.babyIntro.triggerRadius === 3.5, "BabyIntro triggerRadius must be 3.5");
  assert(configChecks.babyIntro.babyLookTarget[0] === 10.5 && configChecks.babyIntro.babyLookTarget[1] === -4.5 && configChecks.babyIntro.babyLookTarget[2] === 30.0, "BabyIntro babyLookTarget must be [10.5, -4.5, 30.0]");

  console.log("PASS: All config assertions succeeded!");

  // -------------------------------------------------------------
  // Test 2: Baby Intro Event and Crawling in B1
  // -------------------------------------------------------------
  console.log("\n[TEST 2] Testing Baby Intro Event trigger in B1 and vertical rejection from 1F...");
  const babyTest = await page.evaluate(async () => {
    const game = window.__happyToy;
    game.start();

    const babyIntro = game.monsterIntroManager.events.find(e => e.constructor.name === "BabyIntroEvent");
    babyIntro.reset();

    // 1. Position player right above B1 nursery entrance on 1F (Y = 0.0, X = 14.5, Z = 32.0)
    game.player.setPosition({ x: 14.5, y: 0.0, z: 32.0 });
    babyIntro.update(0.016);
    const triggeredOn1F = babyIntro.hasTriggered;

    // 2. Now move player to B1 nursery entrance (Y = -5.0, X = 14.5, Z = 32.0)
    game.player.setPosition({ x: 14.5, y: -5.0, z: 32.0 });
    babyIntro.update(0.016);
    const triggeredOnB1 = babyIntro.hasTriggered;

    // 3. Find baby-workshop enemy
    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    const babyPosInitial = baby ? [baby.group.position.x, baby.group.position.y, baby.group.position.z] : null;

    // 4. Awaken Baby in B1
    if (baby) {
      baby.setDormant(false);
      // Simulate proximity / alert
      game.enemyManager.update(0.05, {
        position: game.player.position,
        isSprinting: true,
        isHidden: false,
        isUndetectable: false,
      });
    }

    const babyAwake = baby ? baby.babyAwake : false;
    const babyState = baby ? baby.state : null;
    const babyPosAfterAwaken = baby ? [baby.group.position.x, baby.group.position.y, baby.group.position.z] : null;

    return {
      triggeredOn1F,
      triggeredOnB1,
      babyPosInitial,
      babyAwake,
      babyState,
      babyPosAfterAwaken,
    };
  });

  console.log("Baby test results:", babyTest);
  assert(!babyTest.triggeredOn1F, "BabyIntroEvent must NOT trigger when player is on 1F (Y=0.0)");
  assert(babyTest.triggeredOnB1, "BabyIntroEvent MUST trigger when player is at B1 nursery entrance (Y=-5.0)");
  assert(babyTest.babyPosInitial && Math.abs(babyTest.babyPosInitial[1] - (-5.0)) < 0.2, `Baby must be positioned at Y = -5.0 in B1 (got ${babyTest.babyPosInitial?.[1]})`);
  assert(babyTest.babyAwake, "Baby must awaken in B1");
  assert(babyTest.babyState === "chase", "Baby state must be chase when awake");
  assert(babyTest.babyPosAfterAwaken && Math.abs(babyTest.babyPosAfterAwaken[1] - (-5.0)) < 0.2, `Baby crawling plane must remain at Y = -5.0 (got ${babyTest.babyPosAfterAwaken?.[1]})`);

  console.log("PASS: Baby Intro and crawling at Y = -5.0 verified!");

  // -------------------------------------------------------------
  // Test 3: Hwacat Event on 2F Gallery (Y = 5.0)
  // -------------------------------------------------------------
  console.log("\n[TEST 3] Testing Hwacat Event on 2F Gallery...");
  const hwacatTest = await page.evaluate(async () => {
    const game = window.__happyToy;
    const hwacatEvent = game.mirrorEvents[0];
    hwacatEvent.reset();

    // 1. Position player directly below 2F Gallery on 1F (Y = 0.0, X = -19.0, Z = -16.0)
    game.player.setPosition({ x: -19.0, y: 0.0, z: -16.0 });
    hwacatEvent.update(0.016);
    const triggeredOn1F = hwacatEvent.hasTriggered;

    // 2. Position player on 2F Gallery (Y = 5.0, X = -19.0, Z = -16.0)
    game.player.setPosition({ x: -19.0, y: 5.0, z: -16.0 });
    hwacatEvent.update(0.016);
    const triggeredOn2F = hwacatEvent.hasTriggered;
    const stateAfterTrigger = hwacatEvent.state;

    return {
      triggeredOn1F,
      triggeredOn2F,
      stateAfterTrigger,
    };
  });

  console.log("Hwacat trigger results:", hwacatTest);
  assert(!hwacatTest.triggeredOn1F, "HwacatEvent must NOT trigger when player is on 1F (Y=0.0)");
  assert(hwacatTest.triggeredOn2F, "HwacatEvent MUST trigger when player is on 2F gallery (Y=5.0)");
  assert(hwacatTest.stateAfterTrigger === "paintingDrop", "HwacatEvent state should transition to paintingDrop");

  // Wait and step through painting drop and Hwacat sequence
  console.log("Stepping through Hwacat sequence (painting drop -> stand up -> dance -> transform)...");
  for (let step = 0; step < 15; step++) {
    await page.evaluate(() => {
      const game = window.__happyToy;
      const hwacatEvent = game.mirrorEvents[0];
      // Fast forward by calling update with 1 second steps
      hwacatEvent.update(1.0);
    });
    await new Promise((r) => setTimeout(r, 200));

    const stepInfo = await page.evaluate(() => {
      const game = window.__happyToy;
      const hwacatEvent = game.mirrorEvents[0];
      const angryHwacat = game.enemyManager.enemies.find(e => e.config.id === "hwacat-angry");
      const keyHwacat = game.keys.find(k => k.id === "key-hwacat");
      return {
        state: hwacatEvent.state,
        timer: hwacatEvent.timer.toFixed(2),
        isTransforming: hwacatEvent.isTransforming,
        angryExists: Boolean(angryHwacat),
        angryPos: angryHwacat ? [angryHwacat.group.position.x, angryHwacat.group.position.y, angryHwacat.group.position.z] : null,
        keyPos: keyHwacat ? [keyHwacat.position.x, keyHwacat.position.y, keyHwacat.position.z] : null,
        keyVisible: keyHwacat ? keyHwacat.isAvailable : false,
      };
    });

    console.log(`Step ${step + 1}: State=${stepInfo.state}, AngryExists=${stepInfo.angryExists}, AngryPos=${JSON.stringify(stepInfo.angryPos)}, KeyPos=${JSON.stringify(stepInfo.keyPos)}, KeyVisible=${stepInfo.keyVisible}`);

    if (stepInfo.angryExists && stepInfo.keyVisible) {
      assert(Math.abs(stepInfo.angryPos[1] - 5.0) < 0.2, `hwacat-angry must spawn at Y = 5.0 (got ${stepInfo.angryPos[1]})`);
      assert(Math.abs(stepInfo.keyPos[1] - 5.0) < 0.2, `key-hwacat must be revealed at Y = 5.0 (got ${stepInfo.keyPos[1]})`);
      console.log("PASS: hwacat-angry spawned at Y = 5.0 and key-hwacat revealed at Y = 5.0!");
      break;
    }
  }

  console.log("\n=== ALL VERIFICATION TESTS PASSED SUCCESSFULLY! ===");

} catch (err) {
  console.error("Test failed with error:", err);
  process.exit(1);
} finally {
  await browser.close();
}
