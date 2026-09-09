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
await page.waitForTimeout(300);

const library = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.mapBuilder.generator.generateChunk(-1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat?.setDormant?.(true);
  if (uncat) uncat.group.visible = false;
  game.hud.setStatus("도서실입니다. 창이 판자로 막혀 있습니다.", 2800);
  return game.poseForCapture({
    x: -20.55,
    y: 0,
    z: -2.35,
    lookAt: [-19.05, 1.12, -4.55],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_library_bookspines.png"), timeout: 120000 });
console.log("library", library);

const wash = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.mapBuilder.generator.generateChunk(2, 0);
  game.hud.setStatus("화장실 문이 안쪽에서 잠겨 있습니다.", 2800);
  return game.poseForCapture({
    x: 27.15,
    y: 0,
    z: -2.35,
    lookAt: [28.7, 1.05, -4.55],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_washroom_cubicles.png"), timeout: 120000 });
console.log("wash", wash);

const boarded = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.mapBuilder.generator.generateChunk(0, 1);
  game.hud.setStatus("폐쇄된 교실입니다.", 2800);
  return game.poseForCapture({
    x: -2.35,
    y: 0,
    z: 20.45,
    lookAt: [-5.35, 1.42, 18.75],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_boarded_planks.png"), timeout: 120000 });
console.log("boarded", boarded);
await browser.close();
console.log("ok");
