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
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.floorHuntDirector?.hide?.();
    game.hud?.setPrompt?.("");
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 14; i += 1) game.update(0.05, { skipRender: true });
    }
    game.hud?.setPrompt?.("");
    game.hud?.setStatus(options.status || "", 4200);
    return game.poseForCapture({
      yaw: options.yaw,
      pitch: options.pitch,
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name), timeout: 120000 });
  console.log(name, info);
  return info;
}

const east = await poseShot("f1_east_jog_baffle.png", {
  x: 11.4,
  y: 0,
  z: 0.15,
  lookAt: [13.5, 1.2, 0.1],
  status: "동쪽 복도가 남쪽으로 꺾입니다.",
});
const north = await poseShot("f1_north_jog_baffle.png", {
  x: 0.15,
  y: 0,
  z: -16.2,
  lookAt: [0.1, 1.2, -18.6],
  status: "북쪽 복도가 서쪽으로 꺾입니다.",
});

if (!east.flashlight || east.intensity < 8) {
  throw new Error(`east jog capture failed: ${JSON.stringify(east)}`);
}
if (!north.flashlight || north.intensity < 8) {
  throw new Error(`north jog capture failed: ${JSON.stringify(north)}`);
}

console.log("ok");
await browser.close();
