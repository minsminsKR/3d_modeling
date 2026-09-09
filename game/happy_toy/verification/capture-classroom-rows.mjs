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

const rows = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(true);
  uncat.group.visible = false;
  game.hud.setStatus("교실 문이 반쯤 열려 있습니다. 책상 너머로 숨으십시오.", 4200);
  return game.poseForCapture({
    x: 12.15,
    y: 0,
    z: 2.85,
    lookAt: [12.7, 0.92, 5.4],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_classroom_desk_rows.png"), timeout: 120000 });
console.log("rows", rows);

const aisle = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(true);
  uncat.group.visible = false;
  game.hud.setStatus("교실 문이 반쯤 열려 있습니다. 책상 너머로 숨으십시오.", 4200);
  return game.poseForCapture({
    x: 10.75,
    y: 0,
    z: 2.15,
    lookAt: [12.45, 0.98, 3.55],
    flashlight: true,
    freezeLoop: true,
  });
});
await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
await page.screenshot({ path: path.join(outDir, "f1_classroom_from_aisle.png"), timeout: 120000 });
console.log("aisle", aisle);

const hide = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.mapBuilder.generator.generateChunk(1, 0);
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
    yaw: cabinet?.yaw ?? null,
    cabinet: cabinet?.id || null,
  };
});
console.log("hide", hide);
if (!hide.hidden || !hide.interior) {
  throw new Error(`classroom hide failed: ${JSON.stringify(hide)}`);
}
await page.screenshot({ path: path.join(outDir, "f1_locker_peek_hall_uncat.png"), timeout: 120000 });

await browser.close();
console.log("CLASSROOM ROW CAPTURES WRITTEN");
