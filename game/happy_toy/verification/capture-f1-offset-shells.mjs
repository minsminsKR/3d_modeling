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

await poseShot("f1_memorial_offset_spine.png", {
  cx: 6, cz: 0, x: 99.4, y: 0, z: 0.2, lookAt: [99.4, 1.22, 6.4],
});
await poseShot("f1_stair_offset_plus.png", {
  cx: 1, cz: 1, x: 20.0, y: 0, z: 16.15, lookAt: [20.0, 1.22, 22.6],
});
await poseShot("f1_skybridge_offset_grate.png", {
  cx: 3, cz: 0, x: 48.0, y: 0, z: 0.35, lookAt: [55.4, 1.15, 1.6],
});
await poseShot("f1_annex_offset_north.png", {
  cx: 4, cz: 0, x: 68.2, y: 0, z: 0.15, lookAt: [68.2, 1.22, -6.2],
});
await poseShot("f1_lostfound_offset_west.png", {
  cx: -2, cz: 0, x: -32.0, y: 0, z: 0.2, lookAt: [-38.4, 1.22, 0.2],
});

await browser.close();
