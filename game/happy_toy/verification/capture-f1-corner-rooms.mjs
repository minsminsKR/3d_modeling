import { createRequire } from "node:module";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.on("pageerror", (error) => console.error("pageerror", error.message));

await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
await page.evaluate(() => window.__happyToy.start());
await page.waitForTimeout(400);

async function poseShot(name, pose) {
  const info = await page.evaluate((options) => {
    const game = window.__happyToy;
    game.testSafeMode = true;
    game.player.noclip = true;
    if (game.player.isHidden) game.exitCabinet();
    game.floorHuntDirector?.hide?.();
    game.mapBuilder.generator.generateChunk(options.cx, options.cz);
    game.player.setPosition({ x: options.x, y: options.y, z: options.z });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
    if (game.player.isHidden) game.exitCabinet();
    game.floorHuntDirector?.hide?.();
    game.hud?.setPrompt?.("");
    return game.poseForCapture({
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name), timeout: 120000 });
  console.log(name, info);
}

// Unique stills only — never reuse prior artifact filenames.
await poseShot("f1_annex_narrow_spine.png", {
  cx: 4, cz: 0, x: 64.0, y: 0, z: 0.0, lookAt: [71.2, 1.25, 0.0],
});
await poseShot("f1_trophy_wide_spine.png", {
  cx: 7, cz: 0, x: 112.0, y: 0, z: 0.0, lookAt: [119.4, 1.25, 0.0],
});
await poseShot("f1_laundry_narrow_spine.png", {
  cx: 4, cz: -1, x: 64.0, y: 0, z: -16.0, lookAt: [64.0, 1.25, -23.2],
});
await poseShot("f1_annex_ne_after_pinch.png", {
  cx: 4, cz: 0, x: 69.15, y: 0, z: -2.55, lookAt: [72.35, 1.32, -5.85],
});

await browser.close();
