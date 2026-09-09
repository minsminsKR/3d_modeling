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

await page.screenshot({ path: path.join(outDir, "shadow_corridor_title.png") });
console.log("title");

await page.evaluate(() => window.__happyToy.start());
await page.waitForTimeout(400);

async function poseShot(name, pose) {
  const info = await page.evaluate((options) => {
    const game = window.__happyToy;
    game.testSafeMode = true;
    game.player.noclip = true;
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 18; i += 1) game.update(0.05);
    }
    return game.poseForCapture({
      yaw: options.yaw,
      pitch: options.pitch,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name) });
  console.log(name, info);
  return info;
}

await poseShot("shadow_f1a_main.png", { x: 0, y: 0, z: 2.4, yaw: 0, pitch: -0.18 });
await poseShot("shadow_f1b_annex.png", { x: 80, y: 0, z: 0, yaw: 0, pitch: -0.16 });
const b1 = await poseShot("shadow_b1_flood_on.png", {
  x: 10.5, y: -5, z: 31.6, yaw: 0.28, pitch: -0.62,
});
const f2 = await poseShot("shadow_f2_blood_on.png", {
  x: -23.5, y: 5, z: -18.4, yaw: 0.42, pitch: -0.58,
});

if (!b1.flashlight || b1.hud !== "켜짐" || b1.intensity < 8) {
  throw new Error(`B1 flashlight capture failed: ${JSON.stringify(b1)}`);
}
if (!f2.flashlight || f2.hud !== "켜짐" || f2.intensity < 8) {
  throw new Error(`2F flashlight capture failed: ${JSON.stringify(f2)}`);
}

console.log("ok");
await browser.close();
