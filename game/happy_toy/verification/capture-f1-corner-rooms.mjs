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
    game.floorHuntDirector?.hide?.();
    game.mapBuilder.generator.generateChunk(options.cx, options.cz);
    game.player.setPosition({ x: options.x, y: options.y, z: options.z });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
    game.floorHuntDirector?.hide?.();
    return game.poseForCapture({
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name), timeout: 120000 });
  console.log(name, info);
}

await poseShot("f1_annex_ne_room.png", {
  cx: 4, cz: 0, x: 69.2, y: 0, z: -4.2, lookAt: [69.2, 1.4, -6.6],
});
await poseShot("f1_annex_nw_room.png", {
  cx: 4, cz: 0, x: 58.8, y: 0, z: -4.2, lookAt: [58.8, 1.4, -6.6],
});
await poseShot("f1_eastwash_ne_room.png", {
  cx: 2, cz: 0, x: 37.2, y: 0, z: -4.2, lookAt: [37.2, 1.4, -6.6],
});
await poseShot("f1_washfour_ne_room.png", {
  cx: 5, cz: 0, x: 85.2, y: 0, z: -4.2, lookAt: [85.2, 1.4, -6.6],
});
await poseShot("f1_angel_ne_room.png", {
  cx: -1, cz: 0, x: -10.8, y: 0, z: -4.2, lookAt: [-10.8, 1.4, -6.6],
});
await poseShot("f1_trophy_sw_room.png", {
  cx: 7, cz: 0, x: 106.8, y: 0, z: 4.2, lookAt: [106.8, 1.4, 6.8],
});

await browser.close();
