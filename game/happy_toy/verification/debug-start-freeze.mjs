import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

console.log("Debugging Start Freeze...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const logs = [];
const errors = [];
page.on('console', msg => logs.push(`[CONSOLE ${msg.type()}] ${msg.text()}`));
page.on('pageerror', err => errors.push(`[PAGE_ERROR] ${err.message}`));

try {
  await page.goto(url, { waitUntil: "domcontentloaded" });

  console.log("Checking immediate button click right after DOM content loaded...");

  const preAssetClickState = await page.evaluate(() => {
    const game = window.__happyToy;
    const btn = document.getElementById("btn-start-game");
    return {
      hasGame: !!game,
      assetsReady: game?.assetsReady,
      isStarted: game?.isStarted,
      isPaused: game?.isPaused,
      loopRunning: game?.loop?.isRunning,
    };
  });
  console.log("Pre-asset state:", preAssetClickState);

  // Click start game immediately
  await page.click("#btn-start-game");
  await page.waitForTimeout(1000);

  const postClickState1 = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      assetsReady: game?.assetsReady,
      isStarted: game?.isStarted,
      isPaused: game?.isPaused,
      menuDisplay: game?.menuSystem?.container?.style?.display,
      playerPos: game?.player?.position,
      cameraPos: game?.camera?.position,
      cameraRot: game?.camera?.rotation,
      loopRunning: game?.loop?.isRunning,
      introCutsceneLocked: game?.monsterIntroManager?.blocksPlayerControl,
    };
  });
  console.log("Post-click state 1 (immediately after click):", postClickState1);

  // Wait for assetsReady
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 12000 });
  console.log("Assets loaded! Clicking start game button now...");

  await page.click("#btn-start-game");
  await page.waitForTimeout(500);

  const postClickState2 = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      assetsReady: game?.assetsReady,
      isStarted: game?.isStarted,
      isPaused: game?.isPaused,
      menuDisplay: game?.menuSystem?.container?.style?.display,
    };
  });
  console.log("Post-click state 2 (after assets ready & clicked):", postClickState2);


  console.log("Console Logs captured:", logs);
  console.log("Errors captured:", errors);

  await page.screenshot({ path: "debug-start-freeze.png" });
  console.log("Screenshot saved to debug-start-freeze.png");
} catch (err) {
  console.error("Debug script error:", err);
} finally {
  await browser.close();
}
