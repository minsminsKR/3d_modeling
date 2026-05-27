import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Launching browser to verify monster roaming & ground snap...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });

  // Wait for game initialization
  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 8000 }
  );

  console.log("Game initialized. Starting game...");
  
  // Call game start to enable updates and monster movement
  await page.evaluate(() => {
    window.__happyToy.start();
  });

  console.log("Game started. Monitoring enemies for 15 seconds...");

  for (let i = 0; i < 8; i++) {
    // Wait 2 seconds
    await new Promise((resolve) => setTimeout(resolve, 2000));

    const enemyStates = await page.evaluate(() => {
      const game = window.__happyToy;
      return game.enemyManager.enemies.map(e => e.getDebugState());
    });

    console.log(`\n--- Tick ${i + 1} ---`);
    enemyStates.forEach(state => {
      console.log(`Enemy ID: ${state.id} (${state.label})`);
      console.log(`  State: ${state.state}`);
      console.log(`  Position: x=${state.x.toFixed(2)}, y=${state.y.toFixed(2)}, z=${state.z.toFixed(2)}`);
      console.log(`  GroundY: ${state.groundY.toFixed(2)} (Diff: ${(state.y - state.groundY).toFixed(4)})`);
      console.log(`  Current Chunk: ${state.currentChunk}`);
      console.log(`  Wander Target: ${state.wanderTarget ? `x=${state.wanderTarget.x}, z=${state.wanderTarget.z}` : "None"}`);
      console.log(`  Wander Retarget Timer: ${state.wanderRetargetTimer}`);
      console.log(`  Stuck Count: ${state.wanderStuckCount}`);
      console.log(`  Patrol Path Length: ${state.patrolPathLength}`);
    });
  }

} catch (error) {
  console.error("Verification script failed:", error);
} finally {
  await browser.close();
}
