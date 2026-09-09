import assert from "node:assert/strict";
import { createRequire } from "node:module";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const browser = await chromium.launch({ executablePath, headless: true });
const errors = [];

try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });

  await page.evaluate(() => {
    window.__happyToy.menuSystem.hideMenu();
  });
  await page.mouse.click(640, 360);
  await page.waitForTimeout(400);

  const afterCanvasClick = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      isStarted: game.isStarted,
      isPaused: game.isPaused,
      flashlightOn: game.flashlightController?.enabled === true,
      menuDisplay: game.menuSystem?.container?.style?.display,
      chunks: game.mapBuilder?.loadedChunks?.size ?? 0,
      calls: game.renderer.info.render.calls,
    };
  });
  console.log("after canvas click", afterCanvasClick);
  assert.equal(afterCanvasClick.isStarted, true, "canvas click should start the game");
  assert.equal(afterCanvasClick.isPaused, false, "start should not freeze into pause");
  assert.equal(afterCanvasClick.flashlightOn, true, "flashlight should turn on at start");
  assert.equal(afterCanvasClick.menuDisplay, "none", "title menu should stay hidden");
  assert.ok(afterCanvasClick.chunks > 0, "spawn chunks should be loaded");
  assert.ok(afterCanvasClick.calls > 0, "renderer should be drawing");

  await page.reload({ waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  await page.click("#click-to-play", { force: true });
  await page.waitForTimeout(300);
  const afterButton = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      isStarted: game.isStarted,
      flashlightOn: game.flashlightController?.enabled === true,
    };
  });
  console.log("after click-to-play", afterButton);
  assert.equal(afterButton.isStarted, true, "HUD start button should start the game even under overlays");
  assert.equal(afterButton.flashlightOn, true, "HUD start button should turn the flashlight on");

  await page.reload({ waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  await page.mouse.click(40, 40);
  await page.waitForTimeout(300);
  const afterOverlay = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      isStarted: game.isStarted,
      flashlightOn: game.flashlightController?.enabled === true,
      menuDisplay: game.menuSystem?.container?.style?.display,
    };
  });
  console.log("after overlay click", afterOverlay);
  assert.equal(afterOverlay.isStarted, true, "clicking the dark title overlay should start the game");
  assert.equal(afterOverlay.flashlightOn, true, "overlay click should turn the flashlight on");
  assert.equal(afterOverlay.menuDisplay, "none", "overlay click should hide the title menu");

  assert.equal(errors.length, 0, `page errors: ${errors.join(" | ")}`);
  console.log("CLICK TO START PASSED");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", errors);
  process.exit(1);
} finally {
  await browser.close();
}
