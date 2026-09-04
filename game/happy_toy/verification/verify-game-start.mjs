import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Game Start Pointer Lock Fix Verification...");
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

  console.log("Game loaded. Clicking '게임 시작' button...");

  // Click Title Screen Start Game button
  await page.click("#btn-start-game");
  await page.waitForTimeout(500);

  const gameState = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      isStarted: game.isStarted,
      isPaused: game.isPaused,
      wasPointerLocked: game.wasPointerLocked,
      menuVisible: game.menuSystem.container.style.display !== "none",
    };
  });

  console.log("Game state after clicking '게임 시작':", gameState);

  assert(gameState.isStarted === true, "Expected isStarted to be true");
  assert(gameState.isPaused === false, "Expected isPaused to be false immediately upon game start!");
  assert(gameState.menuVisible === false, "Expected title menu to be hidden");

  console.log("GAME START IMMEDIATE UNFREEZE PASSED 100%!");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
