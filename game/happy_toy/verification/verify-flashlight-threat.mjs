import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { LIGHTING_CONFIG } from "../src/config/gameConfig.js";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

assert.ok(LIGHTING_CONFIG.flashlightIntensity <= 40, "flashlight should not flood the corridor");
assert.ok(LIGHTING_CONFIG.flashlightFillIntensity <= 6, "fill light should stay secondary");
assert.ok(LIGHTING_CONFIG.rendererExposure <= 1.22, "exposure should stay in a horror range");

const browser = await chromium.launch({ executablePath, headless: true });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  await page.evaluate(() => window.__happyToy.start());

  const result = await page.evaluate(() => {
    const game = window.__happyToy;
    game.flashlightController.setEnabled(true, false);
    game.flashlightController.batteryLevel = 1;

    const sample = (distance, frames = 180) => {
      game.getMinMonsterDistance = () => distance;
      const values = [];
      for (let i = 0; i < frames; i += 1) {
        game.flashlightController.update(0.016);
        values.push(game.flashlight.intensity);
      }
      return values;
    };

    const summarize = (values) => {
      const mean = values.reduce((sum, value) => sum + value, 0) / values.length;
      const variance = values.reduce((sum, value) => sum + (value - mean) ** 2, 0) / values.length;
      return {
        min: Math.min(...values),
        max: Math.max(...values),
        mean,
        std: Math.sqrt(variance),
      };
    };

    const far = sample(40);
    const close = sample(1.35);
    document.body.classList.add("reduced-motion");
    const reduced = sample(1.35, 90);
    document.body.classList.remove("reduced-motion");

    return {
      far: summarize(far),
      close: summarize(close),
      reduced: summarize(reduced),
      defaultIntensity: game.flashlightController.defaultIntensity,
    };
  });

  console.log(result);
  assert.equal(errors.length, 0, `page errors: ${errors.join(" | ")}`);
  assert.ok(result.defaultIntensity <= 40, "runtime flashlight default should be toned down");
  assert.ok(result.far.mean > 18, "idle flashlight should still light the path");
  assert.ok(result.far.std < result.far.mean * 0.12, "idle flashlight should stay mostly stable");
  assert.ok(result.close.min < result.far.mean * 0.2, "nearby monsters should nearly black out the beam");
  assert.ok(result.close.std > result.far.std * 3, "nearby flicker must be much more dynamic");
  assert.ok(result.reduced.std < result.close.std * 0.55, "reduced motion should tame the strobe");
  console.log("FLASHLIGHT THREAT PASSED");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", errors);
  process.exit(1);
} finally {
  await browser.close();
}
