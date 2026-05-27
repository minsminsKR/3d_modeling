import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting verification for Backrooms procedural game on url:", url);

const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const browserErrors = [];
page.on("console", (message) => {
  if (message.type() === "error") {
    browserErrors.push(message.text());
  }
});
page.on("pageerror", (error) => browserErrors.push(error.message));

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  
  // Wait for game initialization
  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 5000 },
  );

  console.log("Game loaded successfully. Testing state...");

  const state = await page.evaluate(() => {
    const game = window.__happyToy;
    const map = game.mapBuilder;
    
    return {
      loadedChunksCount: map.loadedChunks.size,
      hasStartChunk: map.loadedChunks.has("0,0"),
      hasFinalExit: Boolean(game.finalExit),
      playerStartPos: [game.player.position.x, game.player.position.y, game.player.position.z],
      doorCount: game.doors.length,
      keyCount: game.keys.length,
      cabinetCount: game.cabinets.length,
      ambientIntensity: game.scene.children.find(c => c.isAmbientLight)?.intensity ?? 0,
      fogFar: game.scene.fog?.far ?? 0,
      enemyCount: game.enemyManager.enemies.length,
    };
  });

  // 1. Initial State Assertions
  assert(state.loadedChunksCount === 49, `Expected 49 loaded chunks in 7x7 grid, got ${state.loadedChunksCount}`);
  assert(state.hasStartChunk, "Expected start chunk (0,0) to be loaded");
  assert(state.hasFinalExit, "Expected final exit toy box to spawn in start chunk");
  assert(state.playerStartPos[0] === 0 && state.playerStartPos[2] === 0, `Expected player spawn at (0, 0), got ${state.playerStartPos}`);
  assert(state.doorCount === 8, `Expected 8 start room and quadrant doors, got ${state.doorCount}`);
  assert(state.ambientIntensity < 1.0, `Expected low ambient light in Backrooms, got ${state.ambientIntensity}`);
  assert(state.fogFar === 75, `Expected Backrooms fog far distance 75, got ${state.fogFar}`);
  assert(state.enemyCount === 2, `Expected 2 monsters initialized, got ${state.enemyCount}`);
  
  console.log("Initial state passed! Testing chunk generation & key placement...");

  // 2. Teleport far away to test culling, then check Workshop (2, 2)
  const workshopState = await page.evaluate(() => {
    const game = window.__happyToy;
    // Teleport player to (96, 0, 96) which is chunk (6, 6)
    game.player.setPosition({ x: 96, y: 0, z: 96 });
    // Trigger update loop to load new chunks and cull old ones
    game.updateBackrooms(0.016);
    const hasStartChunkRemoved = !game.mapBuilder.loadedChunks.has("0,0");

    // Teleport back to Workshop (32, 0, 32) which is chunk (2, 2)
    game.player.setPosition({ x: 32, y: 0, z: 32 });
    game.updateBackrooms(0.016);
    
    const key = game.keys.find(k => k.id === "key-workshop");
    const cabinet = game.cabinets.find(c => c.id === "cabinet-workshop");
    
    return {
      loadedChunksCount: game.mapBuilder.loadedChunks.size,
      hasWorkshopChunk: game.mapBuilder.loadedChunks.has("2,2"),
      hasKey: Boolean(key),
      hasCabinet: Boolean(cabinet),
      hasStartChunkRemoved,
    };
  });

  assert(workshopState.hasWorkshopChunk, "Expected chunk (2,2) to be loaded after teleporting");
  assert(workshopState.hasKey, "Expected key-workshop to spawn in chunk (2,2)");
  assert(workshopState.hasCabinet, "Expected cabinet-workshop to spawn in chunk (2,2)");
  assert(workshopState.hasStartChunkRemoved, "Expected start chunk (0,0) to cull when player is far away");

  console.log("Procedural generation and culling passed! Testing Hwacat event triggers...");

  // 3. Teleport to Event Room (-2, -2) and test Mirror Hwacat Event
  const eventState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: -32, y: 0, z: -32 });
    game.updateBackrooms(0.016);

    const eventObj = game.mirrorEvents[0];
    const painting = game.scene.getObjectByName("upper-hwa-painting");

    return {
      hasEventChunk: game.mapBuilder.loadedChunks.has("-2,-2"),
      hasPainting: Boolean(painting),
      eventState: eventObj.state,
      eventTriggered: eventObj.hasTriggered,
    };
  });

  assert(eventState.hasEventChunk, "Expected event room chunk (-2,-2) to load");
  assert(state.enemyCount === 2, "Expected initial enemy count to be 2");
  assert(eventState.hasPainting, "Expected upper-hwa-painting mesh to exist in event room");
  assert(eventState.eventState === "idle", `Expected event state to start as idle, got ${eventState.eventState}`);

  console.log("All Backrooms verification checks passed successfully!");

} catch (error) {
  console.error("Verification failed!");
  console.error(error);
  console.error("Browser errors recorded during test:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
