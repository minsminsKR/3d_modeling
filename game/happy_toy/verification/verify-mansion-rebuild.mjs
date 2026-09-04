import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting 3-Story Haunted Mansion & Flooded Basement Verification...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const browserErrors = [];
page.on('pageerror', err => browserErrors.push(err.message));
page.on('console', msg => { if (msg.type() === 'error') browserErrors.push(msg.text()); });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 12000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 12000 });

  console.log("Game loaded. Testing 3-Story Mansion Architecture & Flooded Basement...");

  // 1. Verify B1 Flooded Basement Water Mesh (Y = -4.85)
  const basementWaterTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder.generator;
    const b1Chunk = generator.generateChunk(0, 1);

    const waterMesh = b1Chunk.meshes.find(m => m.name && m.name.includes("water"));
    return {
      cy: b1Chunk.cy,
      floorY: b1Chunk.floorY,
      hasWaterMesh: Boolean(waterMesh),
      waterY: waterMesh ? waterMesh.position.y : null,
    };
  });

  assert(basementWaterTest.cy === -1, "Expected B1 chunk elevation cy to be -1");
  assert(basementWaterTest.hasWaterMesh, "Expected B1 basement chunk to have water plane mesh");
  assert(basementWaterTest.waterY === -4.85, `Expected water mesh Y to be -4.85, got ${basementWaterTest.waterY}`);
  console.log("B1 Flooded Basement Water Surface Mesh PASSED!", basementWaterTest);

  // 2. Verify 2F Playroom Elevation (Y = 5.0)
  const floor2Test = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder.generator;
    const f2Chunk = generator.generateChunk(-2, 2);

    return {
      cy: f2Chunk.cy,
      floorY: f2Chunk.floorY,
    };
  });

  assert(floor2Test.cy === 1, "Expected 2F chunk elevation cy to be 1");
  assert(floor2Test.floorY === 5.0, `Expected 2F floorY to be 5.0, got ${floor2Test.floorY}`);
  console.log("2F Upper Manor Floor Elevation PASSED!", floor2Test);

  // 3. Verify B1 Uncat Basement Intro (0, -5.0, 16.0)
  const uncatBasementTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const uncatEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "UncatIntroEvent");

    game.player.setPosition({ x: 0, y: -5.0, z: 16.0 });
    uncatEvent.update(0.016);

    const uncat = game.enemyManager.enemies.find(e => e.config.id === "uncat");

    return {
      triggered: uncatEvent.hasTriggered,
      uncatY: uncat?.group.position.y,
      uncatDormant: uncat?.isDormant,
    };
  });

  assert(uncatBasementTest.triggered, "Expected UncatIntroEvent to trigger in B1 Basement");
  assert(uncatBasementTest.uncatY === -5.0, `Expected Uncat Y position in B1 Basement to be -5.0, got ${uncatBasementTest.uncatY}`);
  assert(uncatBasementTest.uncatDormant === false, "Expected Uncat to wake up after basement intro event");
  console.log("B1 Flooded Basement Uncat Intro PASSED!", uncatBasementTest);

  console.log("ALL 3-STORY HAUNTED MANSION & FLOODED BASEMENT CHECKS PASSED!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
