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

const ring = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.player.setPosition({ x: 12.1, y: 0, z: -6.35 });
  for (let i = 0; i < 14; i += 1) game.update(0.05, { skipRender: true });
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(16.1, 0, -6.35);
  uncat.group.lookAt(12.1, 1.2, -6.35);
  uncat.state = "chase";
  game.hud.setStatus("바깥 고리입니다. 안쪽 루프와 신발장으로 빠지십시오.", 4200);
  return game.poseForCapture({
    x: 12.1,
    y: 0,
    z: -6.35,
    lookAt: [16.2, 1.15, -6.35],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(16.1, 0, -6.35);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_outer_ring_uncat.png"), timeout: 120000 });
console.log("ring", ring);

const ribs = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(16.0, 0, 2.55);
  uncat.group.lookAt(11.6, 1.2, 2.55);
  uncat.state = "chase";
  game.hud.setStatus("복도가 갈라집니다. 한 길로 쫓기면 다른 길로, 아니면 신발장으로.", 4200);
  return game.poseForCapture({
    x: 11.55,
    y: 0,
    z: 2.55,
    lookAt: [16.1, 1.15, 2.5],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(16.0, 0, 2.55);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_maze_ribs_chase.png"), timeout: 120000 });
console.log("ribs", ribs);

const longRing = await page.evaluate(() => {
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
  uncat.group.position.set(22.4, 0, -6.35);
  uncat.group.lookAt(12.2, 1.2, -6.35);
  uncat.state = "chase";
  game.hud.setStatus("바깥 복도가 이어집니다. 안개 끝의 발소리를 다른 고리로 빼십시오.", 4200);
  return game.poseForCapture({
    x: 12.2,
    y: 0,
    z: -6.35,
    lookAt: [22.6, 1.2, -6.35],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(22.4, 0, -6.35);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_long_ring_fog_uncat.png"), timeout: 120000 });
console.log("longRing", longRing);

const lockers = await page.evaluate(() => {
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
  uncat.group.position.set(20.6, 0, -6.35);
  uncat.group.lookAt(12.4, 1.2, -6.35);
  uncat.state = "chase";
  game.hud.setStatus("바깥 복도가 이어집니다. 안개 끝의 발소리를 다른 고리로 빼십시오.", 4200);
  return game.poseForCapture({
    x: 12.4,
    y: 0,
    z: -6.35,
    lookAt: [20.8, 1.05, -7.2],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(20.6, 0, -6.35);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_outer_ring_lockers_uncat.png"), timeout: 120000 });
console.log("lockers", lockers);

const hunt = await page.evaluate(async () => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.ghostMode = false;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.player.setPosition({ x: 10.5, y: -5, z: 32 });
  game.floorHuntDirector?.ensureSilhouette?.();
  for (let i = 0; i < 80; i += 1) game.update(0.05, { skipRender: true });
  return true;
});
await page.waitForFunction(
  () => window.__happyToy?.floorHuntDirector?.modelReady === true,
  null,
  { timeout: 12000 },
).catch(() => {});
const huntShot = await page.evaluate(() => {
  const game = window.__happyToy;
  const sil = game.floorHuntDirector?.ensureSilhouette?.();
  if (sil) {
    sil.visible = true;
    sil.position.set(8.4, -5, 34.2);
  }
  game.hud.setPrompt("");
  game.hud.setStatus("지하의 발소리입니다. 손전등을 비추면 멈춥니다.", 4200);
  const look = sil.position.clone();
  look.y += 1.2;
  game.player.setLookAt(look);
  game.flashlightController?.setEnabled(true, false);
  game.renderer.render(game.scene, game.camera);
  return {
    visible: sil?.visible === true,
    y: sil?.position?.y ?? null,
    model: game.floorHuntDirector?.modelReady === true,
    flashlight: game.flashlightController?.enabled === true,
  };
});
if (!huntShot.visible) {
  throw new Error(`B1 hunt missing: ${JSON.stringify(huntShot)}`);
}
await page.screenshot({ path: path.join(outDir, "b1_hunt_uncat_shadow.png"), timeout: 120000 });
console.log("hunt", hunt, huntShot);

if (!ring.flashlight || ring.intensity < 8) {
  throw new Error(`outer ring capture failed: ${JSON.stringify(ring)}`);
}

console.log("ok");
await browser.close();
