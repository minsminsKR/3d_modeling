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
await poseShot("f1_nursery_east_room.png", {
  cx: 2, cz: 1, x: 36.8, y: 0, z: 20.6, lookAt: [40.4, 1.28, 23.4],
});
await poseShot("f1_doll_west_room.png", {
  cx: -2, cz: 1, x: -36.8, y: 0, z: 20.5, lookAt: [-40.2, 1.28, 23.3],
});
await poseShot("f1_storage_east_room.png", {
  cx: 2, cz: -1, x: 36.8, y: 0, z: -9.9, lookAt: [40.4, 1.28, -7.2],
});
await poseShot("f1_lab_south_room.png", {
  cx: 5, cz: -2, x: 84.6, y: 0, z: -27.6, lookAt: [87.8, 1.28, -24.8],
});

await browser.close();
