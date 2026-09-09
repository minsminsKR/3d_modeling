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
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 12; i += 1) game.update(0.05, { skipRender: true });
    }
    game.floorHuntDirector?.hide?.();
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

const b1 = await poseShot("b1_labyrinth_south.png", {
  x: 8.4, y: -5, z: 48.2, lookAt: [8.4, -3.6, 53.4],
});
const f2 = await poseShot("f2_labyrinth_north.png", {
  x: -22.5, y: 5, z: -38.2, lookAt: [-32.5, 6.3, -42.0],
});
const hunt = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.ghostMode = false;
  game.player.noclip = true;
  game.player.setPosition({ x: 8.4, y: -5, z: 48.2 });
  game.floorHuntDirector.reset();
  game.floorHuntDirector.lastFloor = -1;
  game.floorHuntDirector.floorTime = 6;
  game.floorHuntDirector.approach = 0.55;
  for (let i = 0; i < 20; i += 1) game.update(0.05, { skipRender: true });
  const sil = game.floorHuntDirector.silhouette;
  game.poseForCapture({
    lookAt: sil ? [sil.position.x, sil.position.y + 1.2, sil.position.z] : [10, -3.6, 50],
    flashlight: true,
    freezeLoop: true,
  });
  if (sil) sil.visible = true;
  game.renderer.render(game.scene, game.camera);
  return {
    visible: Boolean(sil?.visible),
    y: sil?.position.y ?? null,
    intensity: game.flashlight?.intensity ?? 0,
  };
});
await page.screenshot({ path: path.join(outDir, "b1_floor_hunt.png"), timeout: 120000 });
console.log("b1_floor_hunt.png", hunt);

if (!b1.flashlight || b1.intensity < 8) {
  throw new Error(`B1 labyrinth capture failed: ${JSON.stringify(b1)}`);
}
if (!f2.flashlight || f2.intensity < 8) {
  throw new Error(`2F labyrinth capture failed: ${JSON.stringify(f2)}`);
}
if (!hunt.visible) {
  throw new Error(`floor hunt silhouette missing: ${JSON.stringify(hunt)}`);
}

console.log("ok");
await browser.close();
