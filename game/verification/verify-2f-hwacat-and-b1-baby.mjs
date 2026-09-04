import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8010/";

console.log(`[TEST] Launching browser to verify 2F Hwacat, B1 Baby, and Corner Cinematics at ${url}...`);
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const consoleLogs = [];
const browserErrors = [];

page.on("console", (message) => {
  const text = message.text();
  consoleLogs.push(text);
  if (message.type() === "error") {
    browserErrors.push(text);
  }
});
page.on("pageerror", (error) => browserErrors.push(error.message));

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 15000 });

  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 15000 }
  );

  console.log("Game assets ready. Initializing tests...");
  await page.evaluate(() => {
    window.__happyToy.start();
  });
  await new Promise((resolve) => setTimeout(resolve, 1000));

  // TEST 1: Cyclops Corner Emergence
  console.log("\n--- TEST 1: Cyclops Corner Emergence ---");
  const cyclopsResult = await page.evaluate(async () => {
    const game = window.__happyToy;
    const cyclopse = game.enemyManager?.enemies.find(e => e.config.id === "cyclopse");
    const intro = game.cyclopseIntroEvent;

    // Teleport player right before east corridor trigger
    game.player.setPosition({ x: 13.0, y: 0.0, z: 0.0 });
    game.player.resetLook(-Math.PI / 2, 0); // look East
    game.updateBackrooms(0.016);

    // Step into trigger
    game.player.setPosition({ x: 14.0, y: 0.0, z: 0.0 });
    intro.update(0.016);

    const triggered = intro.hasTriggered;
    const initialCyclopsPos = cyclopse ? { x: cyclopse.group.position.x, y: cyclopse.group.position.y, z: cyclopse.group.position.z } : null;

    // Advance 0.9s to corner emergence
    for (let i = 0; i < 60; i++) {
      intro.update(0.016);
      if (cyclopse) cyclopse.update(0.016, game.player);
    }

    const midCyclopsPos = cyclopse ? { x: cyclopse.group.position.x, y: cyclopse.group.position.y, z: cyclopse.group.position.z } : null;

    return {
      triggered,
      initialCyclopsPos,
      midCyclopsPos,
      isControlLocked: intro.isControlLocked,
    };
  });
  console.log("Cyclops Test Result:", JSON.stringify(cyclopsResult, null, 2));

  // TEST 2: Uncat Corner Emergence
  console.log("\n--- TEST 2: Uncat Corner Emergence ---");
  const uncatResult = await page.evaluate(async () => {
    const game = window.__happyToy;
    const uncat = game.enemyManager?.enemies.find(e => e.config.id === "uncat");
    const intro = game.uncatIntroEvent;

    // Teleport player right before south corridor trigger
    game.player.setPosition({ x: 0.0, y: 0.0, z: 13.0 });
    game.player.resetLook(Math.PI, 0); // look South
    game.updateBackrooms(0.016);

    // Step into trigger
    game.player.setPosition({ x: 0.0, y: 0.0, z: 14.2 });
    intro.update(0.016);

    const triggered = intro.hasTriggered;
    const initialUncatPos = uncat ? { x: uncat.group.position.x, y: uncat.group.position.y, z: uncat.group.position.z } : null;

    for (let i = 0; i < 60; i++) {
      intro.update(0.016);
      if (uncat) uncat.update(0.016, game.player);
    }

    const midUncatPos = uncat ? { x: uncat.group.position.x, y: uncat.group.position.y, z: uncat.group.position.z } : null;

    return {
      triggered,
      initialUncatPos,
      midUncatPos,
      isControlLocked: intro.isControlLocked,
    };
  });
  console.log("Uncat Test Result:", JSON.stringify(uncatResult, null, 2));

  // TEST 3: 2F Upper Floor & Hwacat Event
  console.log("\n--- TEST 3: 2F Upper Floor & Hwacat Event ---");
  const hwacatResult = await page.evaluate(async () => {
    const game = window.__happyToy;
    // Teleport player to 2F Gallery
    game.player.setPosition({ x: -19.0, y: 5.0, z: -16.0 });
    game.player.resetLook(Math.PI / 2, 0); // look West towards painting
    game.updateBackrooms(0.016);

    const playerPos2F = { ...game.player.position };
    const floorArea = game.collisionWorld?.findFloorArea(playerPos2F);

    const hwacatEvent = game.mirrorEvents ? game.mirrorEvents[0] : null;
    let eventState = "none";
    let triggered = false;

    if (hwacatEvent) {
      hwacatEvent.tryTrigger();
      triggered = hwacatEvent.hasTriggered;
      eventState = hwacatEvent.state;
    }

    return {
      playerPos2F,
      floorAreaId: floorArea?.id,
      floorY: floorArea?.y,
      hwacatTriggered: triggered,
      hwacatState: eventState,
    };
  });
  console.log("2F Hwacat Test Result:", JSON.stringify(hwacatResult, null, 2));

  // Capture 2F Gallery view
  await page.screenshot({ path: "verification/screenshot-2f-gallery.png" });
  console.log("Captured verification/screenshot-2f-gallery.png");

  // TEST 4: B1 Basement Floor & Baby Event
  console.log("\n--- TEST 4: B1 Basement Floor & Baby Event ---");
  const babyResult = await page.evaluate(async () => {
    const game = window.__happyToy;
    // Teleport player to B1 Nursery Entrance
    game.player.setPosition({ x: 14.0, y: -5.0, z: 32.0 });
    game.player.resetLook(Math.PI / 2, 0);
    game.updateBackrooms(0.016);

    const playerPosB1 = { ...game.player.position };
    const floorArea = game.collisionWorld?.findFloorArea(playerPosB1);

    const babyIntro = game.babyIntroEvent;
    const babyEnemy = game.enemyManager?.enemies.find(e => e.config.id?.includes("baby") || e.config.type === "baby");

    if (babyIntro) {
      babyIntro.checkTrigger();
    }

    return {
      playerPosB1,
      floorAreaId: floorArea?.id,
      floorY: floorArea?.y,
      babyIntroTriggered: babyIntro?.hasTriggered,
      babyEnemyPos: babyEnemy ? { x: babyEnemy.group.position.x, y: babyEnemy.group.position.y, z: babyEnemy.group.position.z } : null,
      babyDormant: babyEnemy ? babyEnemy.isDormant : null,
    };
  });
  console.log("B1 Baby Test Result:", JSON.stringify(babyResult, null, 2));

  // Capture B1 Cellar view
  await page.screenshot({ path: "verification/screenshot-b1-cellar.png" });
  console.log("Captured verification/screenshot-b1-cellar.png");

  console.log("\nAll checks completed.");
} catch (err) {
  console.error("Test execution failed:", err);
} finally {
  await browser.close();
}
