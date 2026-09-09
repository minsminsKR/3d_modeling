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
    const game = window.__happyToy;
    game.menuSystem?.hideMenu();
    game.start();
  });
  await page.waitForTimeout(1000);
  const afterStart = await page.evaluate(() => ({
    enabled: window.__happyToy.flashlightController.enabled,
    hud: document.querySelector("#flashlight-state")?.textContent,
    offClass: document.body.classList.contains("flashlight-off"),
    intensity: window.__happyToy.flashlight.intensity,
  }));
  await page.keyboard.press("KeyF");
  await page.waitForTimeout(200);
  const afterOff = await page.evaluate(() => ({
    enabled: window.__happyToy.flashlightController.enabled,
    hud: document.querySelector("#flashlight-state")?.textContent,
    offClass: document.body.classList.contains("flashlight-off"),
    intensity: window.__happyToy.flashlight.intensity,
  }));
  await page.keyboard.press("KeyF");
  await page.waitForTimeout(200);
  const afterOn = await page.evaluate(() => ({
    enabled: window.__happyToy.flashlightController.enabled,
    hud: document.querySelector("#flashlight-state")?.textContent,
    offClass: document.body.classList.contains("flashlight-off"),
    intensity: window.__happyToy.flashlight.intensity,
  }));

  console.log({ afterStart, afterOff, afterOn });
  assert.equal(errors.length, 0, errors.join(" | "));
  assert.equal(afterStart.enabled, true);
  assert.equal(afterStart.hud, "켜짐");
  assert.equal(afterStart.offClass, false);
  assert.ok(afterStart.intensity > 8, `expected a live beam, got ${afterStart.intensity}`);
  assert.equal(afterOff.enabled, false);
  assert.equal(afterOff.hud, "꺼짐");
  assert.equal(afterOff.offClass, true);
  assert.ok(afterOff.intensity < 0.5);
  assert.equal(afterOn.enabled, true);
  assert.equal(afterOn.hud, "켜짐");
  assert.equal(afterOn.offClass, false);
  console.log("FLASHLIGHT HUD PASSED");
} finally {
  await browser.close();
}
