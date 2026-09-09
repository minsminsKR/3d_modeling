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
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 20; i += 1) game.update(0.05);
    }
    return game.poseForCapture({
      yaw: options.yaw,
      pitch: options.pitch,
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name) });
  console.log(name, info);
  return info;
}

const b1 = await poseShot("b1_maze_flood.png", {
  x: 8.4, y: -5, z: 36.6, lookAt: [8.4, -3.8, 32.4],
});
const nursery = await poseShot("b1_nursery_story.png", {
  x: 2.4, y: -5, z: 30.0, lookAt: [-3.5, -4.2, 28.5],
});
const f2 = await poseShot("f2_maze_blood.png", {
  x: -22.5, y: 5, z: -21.6, lookAt: [-27.4, 6.2, -22.0],
});
const shrine = await poseShot("f2_shrine_story.png", {
  x: -29.2, y: 5, z: -22.0, lookAt: [-35.5, 6.4, -22.0],
});

if (!b1.flashlight || b1.intensity < 8) {
  throw new Error(`B1 maze capture failed: ${JSON.stringify(b1)}`);
}
if (!f2.flashlight || f2.intensity < 8) {
  throw new Error(`2F maze capture failed: ${JSON.stringify(f2)}`);
}
if (!nursery.flashlight || nursery.intensity < 8) {
  throw new Error(`nursery capture failed: ${JSON.stringify(nursery)}`);
}
if (!shrine.flashlight || shrine.intensity < 8) {
  throw new Error(`shrine capture failed: ${JSON.stringify(shrine)}`);
}

console.log("ok");
await browser.close();
