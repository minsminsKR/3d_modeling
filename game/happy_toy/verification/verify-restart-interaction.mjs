import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Launching browser to verify restart interaction bug...");
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

  // Check initial doors and player interactables
  const initialData = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      interactablesCount: game.player.interactables?.length ?? 0,
      doorsCount: game.doors.length,
      firstDoorIsOpen: game.doors[0].isOpen,
    };
  });
  console.log(`Initial status: player.interactables=${initialData.interactablesCount}, game.doors=${initialData.doorsCount}`);

  // 2. Trigger Restart
  console.log("Triggering Game Restart...");
  await page.evaluate(() => {
    window.__happyToy.restart();
  });

  // 3. Verify player interactables and game doors again after restart
  const restartData = await page.evaluate(() => {
    const game = window.__happyToy;
    // Let's attempt to trigger interaction with the nearest door
    // Find a door near start (0,0,0) - start doors are at [0, 0, -7.8] etc.
    const playerInteractables = game.player.interactables;
    const doors = game.doors;
    
    // Check if the door objects inside player.interactables match the ones in game.doors
    const matchingDoors = doors.filter(d => playerInteractables.includes(d));

    return {
      interactablesCount: playerInteractables?.length ?? 0,
      doorsCount: doors.length,
      matchingDoorsCount: matchingDoors.length,
    };
  });

  console.log(`Post-restart status: player.interactables=${restartData.interactablesCount}, game.doors=${restartData.doorsCount}`);
  console.log(`Matching doors in player interactables list: ${restartData.matchingDoorsCount}/${restartData.doorsCount}`);

  if (restartData.matchingDoorsCount === restartData.doorsCount) {
    console.log("SUCCESS: All doors are correctly bound to player interactables after restart!");
  } else {
    throw new Error(`FAIL: Only ${restartData.matchingDoorsCount}/${restartData.doorsCount} doors matched!`);
  }

} catch (error) {
  console.error("Verification failed:", error);
  process.exit(1);
} finally {
  await browser.close();
}
