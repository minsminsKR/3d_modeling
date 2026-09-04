import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Dormant Monster AI Verification...");
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

  console.log("Game loaded. Testing Initial Dormant State...");

  // 1. Initial State Check (all initial monsters must be dormant and invisible)
  const initialState = await page.evaluate(() => {
    const game = window.__happyToy;
    const enemies = game.enemyManager.enemies;
    return enemies.map(e => ({
      id: e.config.id,
      isDormant: e.isDormant,
      visible: e.group.visible,
    }));
  });

  console.log("Initial enemy states at game start:", initialState);
  for (const enemy of initialState) {
    assert(enemy.isDormant === true, `Expected enemy ${enemy.id} to be dormant at game start`);
    assert(enemy.visible === false, `Expected enemy ${enemy.id} to be invisible at game start`);
  }
  console.log("Initial Dormant State PASSED! All monsters start 100% inactive & hidden.");

  // 2. Cyclopse Activation Test
  const cyclopseActivation = await page.evaluate(() => {
    const game = window.__happyToy;
    const cyclopseEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "CyclopseIntroEvent");

    game.player.setPosition({ x: 14.0, y: 0, z: 0.0 });
    cyclopseEvent.update(0.016);

    const cyclopse = game.enemyManager.enemies.find(e => e.config.id === "cyclopse");
    const uncat = game.enemyManager.enemies.find(e => e.config.id === "uncat");

    return {
      cyclopseDormant: cyclopse?.isDormant,
      cyclopseVisible: cyclopse?.group.visible,
      uncatDormant: uncat?.isDormant,
      uncatVisible: uncat?.group.visible,
    };
  });

  assert(cyclopseActivation.cyclopseDormant === false, "Expected Cyclopse to wake up after intro event");
  assert(cyclopseActivation.cyclopseVisible === true, "Expected Cyclopse to become visible after intro event");
  assert(cyclopseActivation.uncatDormant === true, "Expected Uncat to remain dormant before its own intro event");
  console.log("Cyclopse Intro Activation PASSED!", cyclopseActivation);

  console.log("ALL DORMANT MONSTER AI CHECKS PASSED!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
