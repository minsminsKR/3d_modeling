import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Noclip/Flight verification...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 8000 });

  // 1. Initial State Check
  const initial = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      testSafeMode: game.testSafeMode,
      noclip: game.player.noclip,
      playerY: game.player.position.y
    };
  });
  console.log("Initial state:", initial);
  assert(!initial.testSafeMode, "Safe mode should initially be OFF");
  assert(!initial.noclip, "Noclip should initially be OFF");
  assert(initial.playerY === 0, "Player Y should start at 0");

  // 2. Toggle safe mode ON and check noclip
  const enabled = await page.evaluate(() => {
    const game = window.__happyToy;
    game.toggleTestSafeMode();
    game.player.update(0.016);
    return {
      testSafeMode: game.testSafeMode,
      noclip: game.player.noclip
    };
  });
  console.log("Enabled safe mode state:", enabled);
  assert(enabled.testSafeMode, "Safe mode should be ON after toggle");
  assert(enabled.noclip, "Noclip should be ON when safe mode is ON");

  // 3. Press Spacebar and simulate flight upwards
  const flightUp = await page.evaluate(() => {
    const game = window.__happyToy;
    // Mock spacebar input key down
    game.input.keys.add(" ");
    
    // Update multiple times to simulate float duration
    for (let i = 0; i < 20; i++) {
      game.player.update(0.05); // 1.0 second of floating
    }
    
    // Clear spacebar
    game.input.keys.delete(" ");
    
    return {
      playerY: game.player.position.y
    };
  });
  console.log("After floating UP:", flightUp);
  assert(flightUp.playerY > 1.0, `Player Y should be in the air (got ${flightUp.playerY})`);

  // 4. Toggle safe mode OFF and verify player falls and Y snaps to 0
  const disabled = await page.evaluate(() => {
    const game = window.__happyToy;
    game.toggleTestSafeMode(); // turn safe mode OFF
    game.player.update(0.016); // runs transition snapping
    return {
      testSafeMode: game.testSafeMode,
      noclip: game.player.noclip,
      playerY: game.player.position.y
    };
  });
  console.log("Disabled safe mode state:", disabled);
  assert(!disabled.testSafeMode, "Safe mode should be OFF");
  assert(!disabled.noclip, "Noclip should be OFF");
  assert(Math.abs(disabled.playerY) < 0.001, `Player Y should snap back to ground (got ${disabled.playerY})`);

  console.log("All Noclip/Flight verification tests passed successfully!");

} catch (err) {
  console.error("Verification failed:", err);
  process.exit(1);
} finally {
  await browser.close();
}
