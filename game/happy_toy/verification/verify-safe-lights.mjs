import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Launching browser to verify SafeLight feature...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });

  // 1. Wait for game initialization
  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 8000 }
  );

  console.log("Game initialized. Starting game...");
  await page.evaluate(() => {
    window.__happyToy.start();
  });

  // Verify safeLights exist in mapBuilder and player interactables
  const initialData = await page.evaluate(() => {
    const game = window.__happyToy;
    const safeLights = game.safeLights;
    const playerInteractables = game.player.interactables;
    const matchingSafeLights = safeLights.filter(sl => playerInteractables.includes(sl));

    return {
      totalSafeLights: safeLights.length,
      matchingSafeLightsCount: matchingSafeLights.length,
      activeKeysCount: game.activatedSafeLightKeys.size,
    };
  });

  console.log(`Initial status: Total SafeLights=${initialData.totalSafeLights}, Matching in Player Interactables=${initialData.matchingSafeLightsCount}, Active Keys=${initialData.activeKeysCount}`);
  
  if (initialData.totalSafeLights === 0) {
    throw new Error("FAIL: No SafeLights spawned in the initial chunks!");
  }
  if (initialData.matchingSafeLightsCount !== initialData.totalSafeLights) {
    throw new Error(`FAIL: Only ${initialData.matchingSafeLightsCount}/${initialData.totalSafeLights} SafeLights were in player interactables!`);
  }

  // 2. Interact with the first SafeLight
  console.log("Interacting with the first SafeLight...");
  const interactResult = await page.evaluate(() => {
    const game = window.__happyToy;
    const firstLight = game.safeLights[0];
    
    // Interact with it
    firstLight.interact(game.createInteractionContext());
    
    // After interaction, update loaded chunks to refresh player interactables
    game.updateBackrooms(0.016);
    
    const isNowOn = firstLight.isOn;
    const hasKey = game.activatedSafeLightKeys.has(firstLight.stateKey);
    const isStillInteractable = firstLight.isInteractable(game.createInteractionContext());

    return {
      isNowOn,
      hasKey,
      isStillInteractable,
      stateKey: firstLight.stateKey,
    };
  });

  console.log("Interaction check results:", interactResult);
  if (!interactResult.isNowOn) {
    throw new Error("FAIL: SafeLight isOn is not true after interaction!");
  }
  if (!interactResult.hasKey) {
    throw new Error("FAIL: SafeLight key not added to activatedSafeLightKeys!");
  }
  if (interactResult.isStillInteractable) {
    throw new Error("FAIL: Active SafeLight should no longer be interactable!");
  }

  // 3. Test persistence across chunk reloading
  console.log("Testing SafeLight state persistence across chunk reloading...");
  const persistenceResult = await page.evaluate(async (stateKey) => {
    const game = window.__happyToy;
    
    // Simulate chunk unloading and reloading by destroying and generating chunk
    // First find the coordinates from the stateKey (e.g. "cx,cz:localId")
    const match = stateKey.match(/^(-?\d+),(-?\d+):/);
    if (!match) throw new Error("Could not parse coordinates from stateKey: " + stateKey);
    const cx = parseInt(match[1]);
    const cz = parseInt(match[2]);

    // Unload the chunk
    game.mapBuilder.generator.destroyChunk(cx, cz);
    game.mapBuilder.loadedChunks.delete(`${cx},${cz}`);
    
    // Force reload
    const chunk = game.mapBuilder.generator.generateChunk(cx, cz);
    game.mapBuilder.loadedChunks.set(`${cx},${cz}`, chunk);
    
    // Check if the re-spawned safeLight is ON
    const safeLight = chunk.safeLights.find(sl => sl.stateKey === stateKey);
    return {
      found: Boolean(safeLight),
      isOn: safeLight ? safeLight.isOn : false,
    };
  }, interactResult.stateKey);

  console.log("Persistence check results:", persistenceResult);
  if (!persistenceResult.found) {
    throw new Error("FAIL: SafeLight not found in reloaded chunk!");
  }
  if (!persistenceResult.isOn) {
    throw new Error("FAIL: SafeLight did not retain its ON state after chunk reload!");
  }

  console.log("SUCCESS: SafeLight functionality verified completely!");

} catch (error) {
  console.error("SafeLight verification failed:", error);
  process.exit(1);
} finally {
  await browser.close();
}
