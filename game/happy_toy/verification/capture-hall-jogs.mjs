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
    game.enemyManager?.enemies?.forEach((enemy) => {
      if (enemy.group) enemy.group.visible = false;
    });
    game.floorHuntDirector?.hide?.();
    game.hud?.setPrompt?.("");
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 14; i += 1) game.update(0.05, { skipRender: true });
    }
    game.hud?.setPrompt?.("");
    game.hud?.setStatus(options.status || "", 4200);
    return game.poseForCapture({
      x: options.x,
      y: options.y,
      z: options.z,
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

const east = await poseShot("f1_s_bend_east_look.png", {
  x: 12.2,
  y: 0,
  z: 2.55,
  lookAt: [16.4, 1.15, 2.45],
  status: "동쪽 복도가 두 번 꺾입니다.",
});
const north = await poseShot("f1_s_bend_north_look.png", {
  x: 2.55,
  y: 0,
  z: -12.2,
  lookAt: [2.45, 1.15, -16.4],
  status: "북쪽 복도가 두 번 꺾입니다.",
});

if (!east.flashlight || east.intensity < 8) {
  throw new Error(`east S-bend capture failed: ${JSON.stringify(east)}`);
}
if (!north.flashlight || north.intensity < 8) {
  throw new Error(`north S-bend capture failed: ${JSON.stringify(north)}`);
}

console.log("ok");
await browser.close();
