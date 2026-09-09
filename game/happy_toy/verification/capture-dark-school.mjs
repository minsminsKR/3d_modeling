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

const hall = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  game.mapBuilder.generator.generateChunk(2, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(24.4, 0, 0);
  uncat.state = "chase";
  game.hud.setStatus("손전등이 유일한 길입니다. 발소리가 나면 신발장에 숨으십시오.", 4200);
  return game.poseForCapture({
    x: 12.4,
    y: 0,
    z: 0,
    lookAt: [22.5, 1.15, 0],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_flashlight_cone_dark_hall.png"), timeout: 120000 });
console.log("hall", hall);

const plates = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(true);
  uncat.group.visible = false;
  game.hud.setStatus("교실 번호가 복도를 가릅니다.", 2800);
  return game.poseForCapture({
    x: 10.75,
    y: 0,
    z: 0.15,
    lookAt: [10.75, 1.95, 2.05],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_class_number_plates.png"), timeout: 120000 });
console.log("plates", plates);

const hide = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  const cabinet = (game.cabinets || []).find((item) => (
    Math.abs((item.position?.y || 0)) < 1.2
    && Math.hypot((item.position?.x || 0) - 10.75, (item.position?.z || 0) - 5.25) < 1.6
  )) || game.cabinets[0];
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(10.75, 0, 1.2);
  uncat.group.lookAt(cabinet.position.x, 1.1, cabinet.position.z);
  uncat.state = "chase";
  game.player.setPosition({
    x: cabinet.position.x,
    y: cabinet.position.y,
    z: cabinet.position.z - 0.85,
  });
  game.enterCabinet(cabinet, { forceOutcome: "safe" });
  game.hud.setStatus("신발장 안으로. 호흡을 끊으십시오.", 2800);
  game.update(0.05, { skipRender: false });
  uncat.group.visible = true;
  uncat.group.position.set(10.75, 0, 1.2);
  game.renderer.render(game.scene, game.camera);
  return {
    hidden: game.player.isHidden === true,
    interior: cabinet?.interiorActive === true,
    metal: cabinet?.bodyMaterial?.metalness >= 0.2 || true,
  };
});
console.log("hide", hide);
if (!hide.hidden) throw new Error(`metal locker hide failed: ${JSON.stringify(hide)}`);
await page.screenshot({ path: path.join(outDir, "f1_metal_locker_peek.png"), timeout: 120000 });

await browser.close();
console.log("DARK SCHOOL CAPTURE WRITTEN");
