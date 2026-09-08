import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function assertVec3(actual, expected, message) {
  assert(
    Array.isArray(actual)
      && actual.length >= 3
      && actual[0] === expected[0]
      && actual[1] === expected[1]
      && actual[2] === expected[2],
    `${message} (got ${JSON.stringify(actual)}, expected ${JSON.stringify(expected)})`,
  );
}

console.log("=== Testing Reconfigured Hwacat (2F) and Baby (B1) Events ===");
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);
const browser = await chromium.launch({
  executablePath,
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
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  const start = page.locator("#start-button, #btn-start-game");
  if (await start.count()) {
    await start.first().click({ timeout: 3000 }).catch(() => {});
  }
  console.log("Game initialized.");

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
        babyHome: [babyIntro.babyHome.x, babyIntro.babyHome.y, babyIntro.babyHome.z],
        babyLookTarget: [babyIntro.babyLookTarget.x, babyIntro.babyLookTarget.y, babyIntro.babyLookTarget.z],
        frameCameraPos: [babyIntro.frameCameraPos.x, babyIntro.frameCameraPos.y, babyIntro.frameCameraPos.z],
      },
    };
  });

  console.log("Config checks result:", JSON.stringify(configChecks, null, 2));

  // Assert Hwacat Config (Game.js runtime + MAP_CONFIG)
  assertVec3(configChecks.gameHwacat.triggerPosition, [-31.0, 5.0, -22.0], "Game hwacat triggerPosition must be [-31.0, 5.0, -22.0]");
  assert(configChecks.gameHwacat.triggerRadius === 2.4, "Game hwacat triggerRadius must be 2.4");
  assertVec3(configChecks.gameHwacat.spawnPosition, [-35.5, 5.0, -22.0], "Game hwacat spawnPosition must be [-35.5, 5.0, -22.0]");
  assertVec3(configChecks.gameHwacat.lookAtPosition, [-35.5, 6.2, -22.0], "Game hwacat lookAtPosition must be [-35.5, 6.2, -22.0]");
  assert(configChecks.gameHwacat.paintingId === "upper-hwa-painting", "paintingId must be upper-hwa-painting");
  assertVec3(configChecks.gameHwacat.paintingDropTargetPosition, [-35.5, 5.08, -22.0], "paintingDropTargetPosition must be [-35.5, 5.08, -22.0]");
  assert(configChecks.gameHwacat.rewardKeyId === "key-hwacat", "rewardKeyId must be key-hwacat");

  assertVec3(configChecks.configHwacat.triggerPosition, [-31.0, 5.0, -22.0], "MAP_CONFIG hwacat triggerPosition must be [-31.0, 5.0, -22.0]");
  assert(configChecks.configHwacat.triggerRadius === 2.4, "MAP_CONFIG hwacat triggerRadius must be 2.4");
  assertVec3(configChecks.configHwacat.spawnPosition, [-35.5, 5.0, -22.0], "MAP_CONFIG hwacat spawnPosition must be [-35.5, 5.0, -22.0]");
  assertVec3(configChecks.hwacatAngrySpawn, [-35.5, 5.0, -22.0], "hwacat-angry spawn must be [-35.5, 5.0, -22.0]");
  assertVec3(configChecks.keyHwacatPos, [-35.5, 5.0, -22.0], "key-hwacat position must be [-35.5, 5.0, -22.0]");

  // Assert Baby Config
  assertVec3(configChecks.babyWorkshopSpawn, [-3.5, -5.0, 28.5], "Baby spawn must be [-3.5, -5.0, 28.5]");
  assertVec3(configChecks.keyWorkshopPos, [-2.5, -5.0, 26.5], "key-workshop position must be [-2.5, -5.0, 26.5]");
  assertVec3(configChecks.babyIntro.triggerPosition, [-0.2, -5.0, 30.0], "BabyIntro triggerPosition must be [-0.2, -5.0, 30.0]");
  assert(configChecks.babyIntro.triggerRadius === 2.6, "BabyIntro triggerRadius must be 2.6");
  assertVec3(configChecks.babyIntro.babyHome, [-3.5, -5.0, 28.5], "BabyIntro babyHome must be [-3.5, -5.0, 28.5]");
  assertVec3(configChecks.babyIntro.babyLookTarget, [-3.5, -4.12, 28.5], "BabyIntro babyLookTarget must be [-3.5, -4.12, 28.5]");
  assertVec3(configChecks.babyIntro.frameCameraPos, [-0.4, -3.38, 31.0], "BabyIntro frameCameraPos must be [-0.4, -3.38, 31.0]");

  console.log("PASS: All config assertions succeeded!");

  // -------------------------------------------------------------
  // Test 2: Baby Intro Event and Crawling in B1
  // -------------------------------------------------------------
  console.log("\n[TEST 2] Testing Baby Intro Event trigger in B1 crib, 1F reject, and stair reject...");
  const babyTest = await page.evaluate(async () => {
    const game = window.__happyToy;
    game.start();

    const babyIntro = game.monsterIntroManager.events.find(e => e.constructor.name === "BabyIntroEvent");
    babyIntro.reset();

    // 1. Directly above the crib trigger on 1F — must not fire.
    game.player.setPosition({ x: -0.2, y: 0.0, z: 30.0 });
    babyIntro.update(0.016);
    const triggeredOn1F = babyIntro.hasTriggered;

    // 2. B1 stair shaft — must not fire.
    game.player.setPosition({ x: 14.5, y: -5.0, z: 32.0 });
    babyIntro.update(0.016);
    const triggeredOnStairs = babyIntro.hasTriggered;

    // 3. Inside crib room on B1 — must fire.
    game.player.setPosition({ x: -0.2, y: -5.0, z: 30.0 });
    babyIntro.update(0.016);
    const triggeredOnB1 = babyIntro.hasTriggered;

    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    const babyPosInitial = baby ? [baby.group.position.x, baby.group.position.y, baby.group.position.z] : null;

    if (babyIntro.releaseControl) babyIntro.releaseControl();
    babyIntro.state = "done";
    babyIntro.isControlLocked = false;
    game.cinematicLightScale = 1;

    if (baby) {
      baby.setDormant(false);
      baby.babyAwake = false;
      baby.state = "crying";
      baby.caughtPlayer = false;
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
      triggeredOnStairs,
      triggeredOnB1,
      babyPosInitial,
      babyAwake,
      babyState,
      babyPosAfterAwaken,
    };
  });

  console.log("Baby test results:", babyTest);
  assert(!babyTest.triggeredOn1F, "BabyIntroEvent must NOT trigger when player is on 1F at (-0.2, 0, 30)");
  assert(!babyTest.triggeredOnStairs, "BabyIntroEvent must NOT trigger on the B1 stair shaft (14.5, -5, 32)");
  assert(babyTest.triggeredOnB1, "BabyIntroEvent MUST trigger inside the B1 crib room at (-0.2, -5, 30)");
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

    // 1. Directly below the 2F trigger on 1F — must not fire.
    game.player.setPosition({ x: -31.0, y: 0.0, z: -22.0 });
    hwacatEvent.update(0.016);
    const triggeredOn1F = hwacatEvent.hasTriggered;

    // 2. On 2F at the trigger — must fire.
    game.player.setPosition({ x: -31.0, y: 5.0, z: -22.0 });
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
  assert(!hwacatTest.triggeredOn1F, "HwacatEvent must NOT trigger when player is on 1F at (-31, 0, -22)");
  assert(hwacatTest.triggeredOn2F, "HwacatEvent MUST trigger when player is on 2F at (-31, 5, -22)");
  assert(hwacatTest.stateAfterTrigger === "paintingDrop", "HwacatEvent state should transition to paintingDrop");

  // Wait and step through painting drop and Hwacat sequence
  console.log("Stepping through Hwacat sequence (painting drop -> stand up -> dance -> transform)...");
  let angryAndKeyReady = false;
  for (let step = 0; step < 15; step++) {
    await page.evaluate(() => {
      const game = window.__happyToy;
      const hwacatEvent = game.mirrorEvents[0];
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
      assert(Math.abs(stepInfo.angryPos[0] - (-35.5)) < 0.5, `hwacat-angry must spawn near X = -35.5 (got ${stepInfo.angryPos[0]})`);
      assert(Math.abs(stepInfo.angryPos[1] - 5.0) < 0.2, `hwacat-angry must spawn at Y = 5.0 (got ${stepInfo.angryPos[1]})`);
      assert(Math.abs(stepInfo.angryPos[2] - (-22.0)) < 0.5, `hwacat-angry must spawn near Z = -22.0 (got ${stepInfo.angryPos[2]})`);
      assert(Math.abs(stepInfo.keyPos[0] - (-35.5)) < 0.5, `key-hwacat must be revealed near X = -35.5 (got ${stepInfo.keyPos[0]})`);
      assert(Math.abs(stepInfo.keyPos[1] - 5.0) < 0.2, `key-hwacat must be revealed at Y = 5.0 (got ${stepInfo.keyPos[1]})`);
      assert(Math.abs(stepInfo.keyPos[2] - (-22.0)) < 0.5, `key-hwacat must be revealed near Z = -22.0 (got ${stepInfo.keyPos[2]})`);
      console.log("PASS: hwacat-angry spawned at Y = 5.0 and key-hwacat revealed at Y = 5.0!");
      angryAndKeyReady = true;
      break;
    }
  }
  assert(angryAndKeyReady, "hwacat-angry and key-hwacat must appear during the sequence");

  console.log("\n=== ALL VERIFICATION TESTS PASSED SUCCESSFULLY! ===");

} catch (err) {
  console.error("Test failed with error:", err);
  process.exit(1);
} finally {
  await browser.close();
}
