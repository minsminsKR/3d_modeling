import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Cinematic Intros Detailed Verification...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const browserErrors = [];
page.on('pageerror', err => browserErrors.push(err.message));
page.on('console', msg => { if (msg.type() === 'error') browserErrors.push(msg.text()); });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 15000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

  // -------------------------------------------------------------
  // Test 1: Initial Monster Hidden State
  // -------------------------------------------------------------
  const initialCheck = await page.evaluate(() => {
    const game = window.__happyToy;
    const cyclopse = game.enemyManager.enemies.find(e => e.config.id === "cyclopse");
    const uncat = game.enemyManager.enemies.find(e => e.config.id === "uncat");

    return {
      cyclopseDormant: cyclopse.isDormant,
      cyclopseVisible: cyclopse.group.visible,
      cyclopsePos: [cyclopse.group.position.x, cyclopse.group.position.y, cyclopse.group.position.z],
      uncatDormant: uncat.isDormant,
      uncatVisible: uncat.group.visible,
      uncatPos: [uncat.group.position.x, uncat.group.position.y, uncat.group.position.z],
    };
  });
  console.log("Initial Check:", initialCheck);
  assert(initialCheck.cyclopseDormant, "Cyclopse must start dormant");
  assert(!initialCheck.cyclopseVisible, "Cyclopse must start invisible");
  assert(Math.abs(initialCheck.cyclopsePos[0] - 27.5) < 0.5, "Cyclopse X should be ~27.5");
  assert(Math.abs(initialCheck.cyclopsePos[2] - (-4.5)) < 0.5, "Cyclopse Z should be ~ -4.5");

  assert(initialCheck.uncatDormant, "Uncat must start dormant");
  assert(!initialCheck.uncatVisible, "Uncat must start invisible");
  assert(Math.abs(initialCheck.uncatPos[0] - 4.5) < 0.5, "Uncat X should be ~4.5");
  assert(Math.abs(initialCheck.uncatPos[2] - 25.0) < 0.5, "Uncat Z should be ~25.0");

  // -------------------------------------------------------------
  // Test 2: Cyclopse Intro Cinematic Phases
  // -------------------------------------------------------------
  console.log("Testing Cyclopse Cinematic Sequence...");
  const cyclopseResults = await page.evaluate(() => {
    const game = window.__happyToy;
    const cyclopseEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "CyclopseIntroEvent");
    const cyclopse = game.enemyManager.enemies.find(e => e.config.id === "cyclopse");

    // Position player at trigger [14.0, 0, 0.0]
    game.player.setPosition({ x: 14.0, y: 0, z: 0.0 });
    cyclopseEvent.update(0.016); // Trigger

    const afterTrigger = {
      triggered: cyclopseEvent.hasTriggered,
      state: cyclopseEvent.state,
      locked: cyclopseEvent.blocksPlayerControl,
      cyclopseVisible: cyclopse.group.visible,
      cyclopsePos: [cyclopse.group.position.x, cyclopse.group.position.y, cyclopse.group.position.z],
    };

    // Advance to end of Phase 1 (Camera glide at t = 0.85s)
    cyclopseEvent.timer = 0.84;
    cyclopseEvent.update(0.01);
    const phase1CamPos = [game.camera.position.x, game.camera.position.y, game.camera.position.z];
    const phase1MonsterPos = [cyclopse.group.position.x, cyclopse.group.position.y, cyclopse.group.position.z];

    // Advance through Phase 2 (Corner Emergence at t = 1.5s and t = 2.15s)
    cyclopseEvent.timer = 1.5;
    cyclopseEvent.update(0.01);
    const phase2MidMonsterPos = [cyclopse.group.position.x, cyclopse.group.position.y, cyclopse.group.position.z];

    cyclopseEvent.timer = 2.14;
    cyclopseEvent.update(0.01);
    const phase2EndMonsterPos = [cyclopse.group.position.x, cyclopse.group.position.y, cyclopse.group.position.z];
    const phase2EndMonsterYaw = cyclopse.group.rotation.y;

    // Advance to Phase 3 (Shock & Return at t = 2.8s)
    cyclopseEvent.timer = 2.8;
    cyclopseEvent.update(0.01);
    const phase3MidCamPos = [game.camera.position.x, game.camera.position.y, game.camera.position.z];
    const phase3Action = cyclopse.currentActionName;

    // Advance to Finish (t = 3.5s)
    cyclopseEvent.timer = 3.44;
    cyclopseEvent.update(0.02);
    const finalState = {
      state: cyclopseEvent.state,
      locked: cyclopseEvent.blocksPlayerControl,
      cyclopseState: cyclopse.state,
      finalCamPos: [game.camera.position.x, game.camera.position.y, game.camera.position.z],
    };

    return {
      afterTrigger,
      phase1CamPos,
      phase1MonsterPos,
      phase2MidMonsterPos,
      phase2EndMonsterPos,
      phase2EndMonsterYaw,
      phase3MidCamPos,
      phase3Action,
      finalState,
    };
  });

  console.log("Cyclopse Cinematic Results:", cyclopseResults);

  assert(cyclopseResults.afterTrigger.triggered, "Cyclopse should trigger at (14, 0, 0)");
  assert(cyclopseResults.afterTrigger.locked, "Cyclopse cutscene should lock control");
  // Camera arrived near [23.5, 1.4, 0.0] at end of phase 1
  assert(Math.abs(cyclopseResults.phase1CamPos[0] - 23.5) < 0.5, "Camera X should glide to ~23.5");
  // Monster emerged from z = -4.5 to z = 0.0
  assert(cyclopseResults.phase2MidMonsterPos[2] > -4.0 && cyclopseResults.phase2MidMonsterPos[2] < -0.1, "Monster should be mid-emergence");
  assert(Math.abs(cyclopseResults.phase2EndMonsterPos[2] - 0.0) < 0.1, "Monster should reach corridor center z=0.0");
  // Monster facing West (-Math.PI / 2 = -1.57)
  assert(Math.abs(cyclopseResults.phase2EndMonsterYaw - (-Math.PI / 2)) < 0.15, "Monster should face West (yaw = -PI/2)");
  // Camera returning towards player (X decreasing from 23.5 towards 14.0)
  assert(cyclopseResults.phase3MidCamPos[0] < 21.0 && cyclopseResults.phase3MidCamPos[0] > 14.0, "Camera should return towards player");
  // Final state released
  assert(!cyclopseResults.finalState.locked, "Control must be released at end of cutscene");
  assert(cyclopseResults.finalState.state === "done", "Cutscene state should be done");
  assert(cyclopseResults.finalState.cyclopseState === "chase", "Cyclopse should transition to chase");
  console.log("Cyclopse Cinematic PASSED!");

  // -------------------------------------------------------------
  // Test 3: Uncat Intro Cinematic Phases
  // -------------------------------------------------------------
  console.log("Testing Uncat Cinematic Sequence...");
  const uncatResults = await page.evaluate(() => {
    const game = window.__happyToy;
    const uncatEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "UncatIntroEvent");
    const uncat = game.enemyManager.enemies.find(e => e.config.id === "uncat");

    // Position player at trigger [0.0, 0, 14.0]
    game.player.setPosition({ x: 0.0, y: 0, z: 14.0 });
    uncatEvent.update(0.016); // Trigger

    const afterTrigger = {
      triggered: uncatEvent.hasTriggered,
      state: uncatEvent.state,
      locked: uncatEvent.blocksPlayerControl,
      uncatVisible: uncat.group.visible,
      uncatPos: [uncat.group.position.x, uncat.group.position.y, uncat.group.position.z],
    };

    // Phase 1: Camera Glide down south corridor to [0, 1.4, 19.5]
    uncatEvent.timer = 0.84;
    uncatEvent.update(0.01);
    const phase1CamPos = [game.camera.position.x, game.camera.position.y, game.camera.position.z];
    const phase1MonsterPos = [uncat.group.position.x, uncat.group.position.y, uncat.group.position.z];

    // Phase 2: Corner emergence and contortion (t = 1.5s and t = 2.24s)
    uncatEvent.timer = 1.5;
    uncatEvent.update(0.01);
    const phase2MidMonsterPos = [uncat.group.position.x, uncat.group.position.y, uncat.group.position.z];

    uncatEvent.timer = 2.24;
    uncatEvent.update(0.01);
    const phase2EndMonsterPos = [uncat.group.position.x, uncat.group.position.y, uncat.group.position.z];
    const phase2EndMonsterYaw = uncat.group.rotation.y;

    // Phase 3: Blackout sting at t = 2.26s
    uncatEvent.timer = 2.26;
    uncatEvent.update(0.01);
    const blackoutFlashlight = game.flashlight?.intensity;

    // Camera returning at t = 2.9s
    uncatEvent.timer = 2.9;
    uncatEvent.update(0.01);
    const phase3MidCamPos = [game.camera.position.x, game.camera.position.y, game.camera.position.z];

    // Finish (t = 3.6s)
    uncatEvent.timer = 3.54;
    uncatEvent.update(0.02);
    const finalState = {
      state: uncatEvent.state,
      locked: uncatEvent.blocksPlayerControl,
      uncatState: uncat.state,
      finalCamPos: [game.camera.position.x, game.camera.position.y, game.camera.position.z],
    };

    return {
      afterTrigger,
      phase1CamPos,
      phase1MonsterPos,
      phase2MidMonsterPos,
      phase2EndMonsterPos,
      phase2EndMonsterYaw,
      blackoutFlashlight,
      phase3MidCamPos,
      finalState,
    };
  });

  console.log("Uncat Cinematic Results:", uncatResults);

  assert(uncatResults.afterTrigger.triggered, "Uncat should trigger at (0, 0, 14)");
  assert(uncatResults.afterTrigger.locked, "Uncat cutscene should lock control");
  // Camera arrived near [0.0, 1.4, 19.5] at end of phase 1
  assert(Math.abs(uncatResults.phase1CamPos[2] - 19.5) < 0.5, "Camera Z should glide to ~19.5");
  // Monster emerged from x = 4.5 to x = 0.0
  assert(uncatResults.phase2MidMonsterPos[0] < 4.0 && uncatResults.phase2MidMonsterPos[0] > 0.1, "Uncat should be mid-emergence");
  assert(Math.abs(uncatResults.phase2EndMonsterPos[0] - 0.0) < 0.1, "Uncat should reach intersection center x=0.0");
  // Monster facing North (yaw = -Math.PI or Math.PI)
  assert(Math.abs(Math.abs(uncatResults.phase2EndMonsterYaw) - Math.PI) < 0.15, "Uncat should face North towards camera (yaw = -PI)");
  // Blackout test
  assert(uncatResults.blackoutFlashlight === 0, "Flashlight should be blacked out at start of Phase 3");
  // Camera returning towards player (Z decreasing from 19.5 towards 14.0)
  assert(uncatResults.phase3MidCamPos[2] < 18.5 && uncatResults.phase3MidCamPos[2] > 14.0, "Camera should return towards player");
  // Final state released
  assert(!uncatResults.finalState.locked, "Control must be released at end of cutscene");
  assert(uncatResults.finalState.state === "done", "Cutscene state should be done");
  assert(uncatResults.finalState.uncatState === "wander", "Uncat should transition to wander");
  console.log("Uncat Cinematic PASSED!");

  console.log("ALL CINEMATIC TESTS PASSED WITH 100% SUCCESS!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
