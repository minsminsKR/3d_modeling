import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Single-Tile Corridor & Outer Boundary Wall Verification...");
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

  console.log("Game loaded. Testing Single-Tile Corridor & Outer Wall Boundary...");

  // 1. Verify Outer Boundary Closure (e.g. cx = 3, cz = 0)
  const outerWallTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder.generator;
    generator.generateChunk(3, 0);

    // Verify outer chunk has 4 solid boundary walls in collisionWorld
    const blockers = game.collisionWorld.blockers.filter(b => b.id && b.id.startsWith("chunk_3_0_wall_"));
    const solidBlockers = blockers.filter(b => b.id.includes("solid"));

    return { solidCount: solidBlockers.length, totalBlockers: blockers.length };
  });


  assert(outerWallTest.solidCount === 4, `Expected outer chunk (3,0) to have 4 solid boundary walls, got ${outerWallTest.solidCount}`);
  console.log("Outer Boundary Closure PASSED!", outerWallTest);


  // 2. Verify Cyclopse Single-Tile Corridor Trigger (14.0, 0, 0.0)
  const cyclopseTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const cyclopseEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "CyclopseIntroEvent");

    game.player.setPosition({ x: 14.0, y: 0, z: 0.0 });
    cyclopseEvent.update(0.016);

    return { triggered: cyclopseEvent.hasTriggered, locked: cyclopseEvent.blocksPlayerControl };
  });

  assert(cyclopseTest.triggered, "Expected CyclopseIntroEvent to trigger in single-tile corridor");
  assert(cyclopseTest.locked, "Expected CyclopseIntroEvent to lock player control during single-tile corridor cutscene");
  console.log("Single-Tile Cyclopse Intro PASSED!", cyclopseTest);

  console.log("ALL SINGLE-TILE CORRIDOR & BOUNDARY WALL CHECKS PASSED!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
