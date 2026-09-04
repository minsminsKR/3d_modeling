import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Deterministic Map & Weeping Angel Intro Verification...");
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

  console.log("Game loaded. Testing Deterministic Map Layout...");

  // 1. Verify fixed chunk types across 5x5 grid
  const mapTest = await page.evaluate(() => {
    const generator = window.__happyToy.mapBuilder.generator;
    const startType = generator.getChunkType(0, 0);
    const storageType = generator.getChunkType(2, -2);
    const playroomType = generator.getChunkType(-2, 2);
    const workshopType = generator.getChunkType(2, 2);
    const eventType = generator.getChunkType(-2, -2);
    const mannequinType = generator.getChunkType(-1, 0);

    return { startType, storageType, playroomType, workshopType, eventType, mannequinType };
  });

  assert(mapTest.startType === "start", `Expected (0,0) to be start, got ${mapTest.startType}`);
  assert(mapTest.storageType === "storage", `Expected (2,-2) to be storage, got ${mapTest.storageType}`);
  assert(mapTest.playroomType === "playroom", `Expected (-2,2) to be playroom, got ${mapTest.playroomType}`);
  assert(mapTest.workshopType === "workshop", `Expected (2,2) to be workshop, got ${mapTest.workshopType}`);
  assert(mapTest.eventType === "event", `Expected (-2,-2) to be event, got ${mapTest.eventType}`);
  assert(mapTest.mannequinType === "corridor_ew", `Expected (-1,0) to be mannequin corridor, got ${mapTest.mannequinType}`);
  console.log("Deterministic 5x5 Map Layout PASSED!", mapTest);

  // 2. Weeping Angel Intro Event Test (-16.0, 0, 0.0)
  const mannequinTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const mannequinEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "WeepingAngelIntroEvent");

    game.player.setPosition({ x: -16.0, y: 0, z: 0.0 });
    mannequinEvent.update(0.016);

    return { triggered: mannequinEvent.hasTriggered, state: mannequinEvent.state, locked: mannequinEvent.blocksPlayerControl };
  });

  assert(mannequinTest.triggered, "Expected WeepingAngelIntroEvent to trigger near (-16, 0, 0)");
  assert(mannequinTest.locked, "Expected WeepingAngelIntroEvent to lock player control during cutscene");
  console.log("Weeping Angel Intro Event PASSED!", mannequinTest);

  console.log("ALL DETERMINISTIC MAP & MANNEQUIN INTRO CHECKS PASSED!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
