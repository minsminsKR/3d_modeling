import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Monster Intro Events Verification...");
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


  console.log("Game loaded. Testing Cyclopse Intro Event...");

  // 1. Cyclopse Intro Test (14.0, 0, 0.0)
  const cyclopseTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const cyclopseEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "CyclopseIntroEvent");
    
    // Teleport player near Cyclopse trigger in single-tile corridor
    game.player.setPosition({ x: 14.0, y: 0, z: 0.0 });
    cyclopseEvent.update(0.016);


    const triggered = cyclopseEvent.hasTriggered;
    const state = cyclopseEvent.state;
    const locked = cyclopseEvent.blocksPlayerControl;

    return { triggered, state, locked };
  });

  assert(cyclopseTest.triggered, "Expected CyclopseIntroEvent to trigger near (16, 0, 4)");
  assert(cyclopseTest.locked, "Expected CyclopseIntroEvent to lock player control during cutscene");
  console.log("Cyclopse Intro Event PASSED!", cyclopseTest);

  // 2. Uncat Intro Test (0, 0, 16.0)
  const uncatTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const uncatEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "UncatIntroEvent");

    game.player.setPosition({ x: 0, y: 0, z: 16.0 });
    uncatEvent.update(0.016);

    return { triggered: uncatEvent.hasTriggered, state: uncatEvent.state, locked: uncatEvent.blocksPlayerControl };
  });

  assert(uncatTest.triggered, "Expected UncatIntroEvent to trigger near (0, 0, 16)");
  assert(uncatTest.locked, "Expected UncatIntroEvent to lock player control during cutscene");
  console.log("Uncat Intro Event PASSED!", uncatTest);

  // 3. Baby Intro Test (14.5, -5.0, 32.0)
  const babyTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const babyEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "BabyIntroEvent");

    game.player.setPosition({ x: 14.5, y: -5.0, z: 32.0 });
    babyEvent.update(0.016);

    return { triggered: babyEvent.hasTriggered, state: babyEvent.state, locked: babyEvent.blocksPlayerControl };
  });

  assert(babyTest.triggered, "Expected BabyIntroEvent to trigger near (14.5, -5.0, 32.0)");
  assert(babyTest.locked, "Expected BabyIntroEvent to lock player control during cutscene");
  console.log("Baby Intro Event PASSED!", babyTest);

  // 4. LovelyDoll Intro Test (-32.0, 0, 24.5)
  const dollTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const dollEvent = game.monsterIntroManager.events.find(e => e.constructor.name === "LovelyDollIntroEvent");

    game.player.setPosition({ x: -32.0, y: 0, z: 24.5 });
    dollEvent.update(0.016);

    return { triggered: dollEvent.hasTriggered, state: dollEvent.state, locked: dollEvent.blocksPlayerControl };
  });

  assert(dollTest.triggered, "Expected LovelyDollIntroEvent to trigger near (-32, 0, 24.5)");
  assert(dollTest.locked, "Expected LovelyDollIntroEvent to lock player control during cutscene");
  console.log("LovelyDoll Intro Event PASSED!", dollTest);

  console.log("ALL MONSTER INTRO EVENTS VERIFIED SUCCESSFULLY!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded during test:", browserErrors);
  process.exit(1);
}
 finally {
  await browser.close();
}
