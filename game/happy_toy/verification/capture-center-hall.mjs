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
  uncat.group.position.set(22.4, 0, 0);
  uncat.group.lookAt(12.2, 1.2, 0);
  uncat.state = "chase";
  game.hud.setStatus("복도가 끝없이 이어집니다. 안개 끝의 발소리를 신발장으로 빼십시오.", 4200);
  return game.poseForCapture({
    x: 12.2,
    y: 0,
    z: 0,
    lookAt: [22.6, 1.15, 0],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(22.4, 0, 0);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_center_hall_uncat.png"), timeout: 120000 });
console.log("hall", hall);

const fog = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(2, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(28.4, 0, 0);
  uncat.group.lookAt(18.2, 1.2, 0);
  uncat.state = "chase";
  game.hud.setStatus("복도가 끝없이 이어집니다. 안개 끝의 발소리를 신발장으로 빼십시오.", 4200);
  return game.poseForCapture({
    x: 18.2,
    y: 0,
    z: 0,
    lookAt: [28.6, 1.15, 0],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(28.4, 0, 0);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_center_hall_long_fog.png"), timeout: 120000 });
console.log("fog", fog);

const hide = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(16.0, 0, 0);
  uncat.group.lookAt(10.75, 1.1, 5.2);
  uncat.state = "chase";
  game.hud.setStatus("신발장 안으로. 호흡을 끊으십시오.", 4200);
  return game.poseForCapture({
    x: 10.75,
    y: 0,
    z: 3.4,
    lookAt: [10.75, 1.05, 5.4],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(16.0, 0, 0);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_center_alcove_hide.png"), timeout: 120000 });
console.log("hide", hide);

const spur = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(16.0, 0, -5.4);
  uncat.group.lookAt(16.0, 1.2, 0);
  uncat.state = "chase";
  game.hud.setStatus("복도가 갈라집니다. 한 길로 쫓기면 다른 길로, 아니면 신발장으로.", 4200);
  return game.poseForCapture({
    x: 16.0,
    y: 0,
    z: 0.2,
    lookAt: [16.0, 1.15, -6.2],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(16.0, 0, -5.4);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_center_tspur.png"), timeout: 120000 });
console.log("spur", spur);

const foyer = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(true);
  uncat.group.visible = false;
  game.hud.setStatus("제단함은 이 홀에 있다. 이름을 넷 모으기 전에는 열리지 않는다.", 4200);
  return game.poseForCapture({
    x: 0.2,
    y: 0,
    z: 3.4,
    lookAt: [0, 1.05, -2.1],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_start_foyer_altar.png"), timeout: 120000 });
console.log("foyer", foyer);

const south = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(0, 1);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(0, 0, 21.2);
  uncat.group.lookAt(0, 1.2, 12.4);
  uncat.state = "chase";
  game.hud.setStatus("기괴한 고양이 괴물이 복도의 조명을 삼키며 나타났습니다!", 4200);
  return game.poseForCapture({
    x: 0,
    y: 0,
    z: 12.4,
    lookAt: [0, 1.15, 21.4],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.group.visible = true;
  uncat.group.position.set(0, 0, 21.2);
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_uncat_south_school_hall.png"), timeout: 120000 });
console.log("south", south);

const west = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(-1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(true);
  uncat.group.visible = false;
  game.hud.setStatus("액자가 아니라 사람이 서 있습니다. 손전등을 끄지 마십시오.", 4200);
  return game.poseForCapture({
    x: -10.8,
    y: 0,
    z: 0,
    lookAt: [-21.6, 1.15, 0],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => {
  const game = window.__happyToy;
  game.renderer.render(game.scene, game.camera);
});
await page.screenshot({ path: path.join(outDir, "f1_angel_west_school_hall.png"), timeout: 120000 });
console.log("west", west);

await browser.close();
console.log("CENTER HALL CAPTURES WRITTEN");
