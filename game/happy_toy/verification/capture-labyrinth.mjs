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
const b1East = await poseShot("b1_labyrinth_east.png", {
  x: 32, y: -5, z: 38.2, lookAt: [32, -3.6, 48.0],
});
const f2 = await poseShot("f2_labyrinth_north.png", {
  x: -32.5, y: 5, z: -42.2, lookAt: [-32.5, 6.25, -50.0],
});
const f2South = await poseShot("f2_labyrinth_south.png", {
  x: -22.5, y: 5, z: -6.2, lookAt: [-22.5, 6.25, 6.0],
});
const hunt = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.ghostMode = false;
  game.player.noclip = true;
  game.player.setPosition({ x: 8.4, y: -5, z: 48.2 });
  game.poseForCapture({
    lookAt: [8.4, -3.55, 52.6],
    flashlight: true,
    freezeLoop: true,
  });
  const sil = game.floorHuntDirector.ensureSilhouette();
  sil.position.set(8.55, -5, 51.35);
  sil.lookAt(8.4, -3.7, 48.2);
  sil.visible = true;
  game.hud.setStatus("발소리가 꺾인 복도에서 돌아옵니다. 벽장으로.", 4200);
  game.renderer.render(game.scene, game.camera);
  return {
    visible: sil.visible === true,
    z: sil.position.z,
    intensity: game.flashlight?.intensity ?? 0,
  };
});
await page.screenshot({ path: path.join(outDir, "b1_floor_hunt.png"), timeout: 120000 });
console.log("b1_floor_hunt.png", hunt);

if (!b1.flashlight || b1.intensity < 8) {
  throw new Error(`B1 labyrinth capture failed: ${JSON.stringify(b1)}`);
}
if (!b1East.flashlight || b1East.intensity < 8) {
  throw new Error(`B1 east labyrinth capture failed: ${JSON.stringify(b1East)}`);
}
if (!f2.flashlight || f2.intensity < 8) {
  throw new Error(`2F labyrinth capture failed: ${JSON.stringify(f2)}`);
}
if (!f2South.flashlight || f2South.intensity < 8) {
  throw new Error(`2F south labyrinth capture failed: ${JSON.stringify(f2South)}`);
}
if (!hunt.visible) {
  throw new Error(`floor hunt silhouette missing: ${JSON.stringify(hunt)}`);
}

console.log("ok");
await browser.close();
