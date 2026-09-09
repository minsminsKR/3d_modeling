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

const pose = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.mapBuilder.generator.generateChunk(4, 1);
  game.mapBuilder.generator.generateChunk(4, 2);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat?.setDormant?.(true);
  if (uncat) uncat.group.visible = false;
  game.hud.setStatus("방송실입니다. 마이크가 아직 뜨겁습니다. 이름을 대지 마십시오.", 2800);
  return game.poseForCapture({
    x: 64.1,
    y: 0,
    z: 29.6,
    lookAt: [60.2, 1.05, 28.2],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_broadcast_room.png"), timeout: 120000 });
console.log("broadcast", pose);
await browser.close();
console.log("ok");
