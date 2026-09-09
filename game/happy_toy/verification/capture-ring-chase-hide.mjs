import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

fs.mkdirSync(outDir, { recursive: true });
const videoDir = path.join(outDir, "ring-chase-hide-raw");
fs.mkdirSync(videoDir, { recursive: true });

const browser = await chromium.launch({ executablePath, headless: true });
const context = await browser.newContext({
  viewport: { width: 1280, height: 720 },
  recordVideo: { dir: videoDir, size: { width: 1280, height: 720 } },
});
const page = await context.newPage();
page.on("pageerror", (error) => console.error("pageerror", error.message));

await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
await page.evaluate(() => {
  const game = window.__happyToy;
  game.start();
  game.loop?.stop();
  game.menuSystem?.hideMenu();
  game.hud?.hideStart?.();
  game.hud?.hideClickToPlay?.();
});
await page.waitForTimeout(250);

await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.ghostMode = false;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  game.mapBuilder.generator.generateChunk(2, 0);
  game.player.setPosition({ x: 12.3, y: 0, z: -6.35 });
  game.flashlightController?.setEnabled(true, false);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(22.4, 0, -6.35);
  uncat.state = "chase";
  game.hud.setStatus("바깥 복도가 이어집니다. 안개 끝의 발소리를 다른 고리로 빼십시오.", 4200);
  game.player.setLookAt(uncat.group.position.clone().setY(1.15));
  game.renderer.render(game.scene, game.camera);
});

const poseChase = (uncatX) => page.evaluate((uncatX) => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.player.setPosition({ x: 12.3, y: 0, z: -6.35 });
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(uncatX, 0, -6.35);
  uncat.group.lookAt(12.3, 1.2, -6.35);
  uncat.state = "chase";
  game.player.setLookAt(uncat.group.position.clone().setY(1.15));
  game.flashlightController?.setEnabled(true, false);
  game.update(0.05, { skipRender: false });
  uncat.group.position.set(uncatX, 0, -6.35);
  uncat.group.visible = true;
  game.renderer.render(game.scene, game.camera);
  return {
    px: game.player.position.x,
    pz: game.player.position.z,
    ux: uncat.group.position.x,
    uz: uncat.group.position.z,
    dist: Math.hypot(uncat.group.position.x - 12.3, uncat.group.position.z + 6.35),
  };
}, uncatX);

for (let i = 0; i < 12; i += 1) {
  const x = 22.2 - i * 0.72;
  console.log("chase", await poseChase(x));
  await page.waitForTimeout(70);
}

const hide = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  const cabinet = (game.cabinets || []).find((item) => (
    Math.abs((item.position?.y || 0)) < 1.2
    && Math.hypot((item.position?.x || 0) - 10.75, (item.position?.z || 0) + 5.25) < 1.6
  )) || game.cabinets[0];
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(14.6, 0, -6.35);
  uncat.state = "investigateCabinet";
  game.player.setPosition({
    x: cabinet.position.x,
    y: cabinet.position.y,
    z: cabinet.position.z - 0.85,
  });
  game.enterCabinet(cabinet, { forceOutcome: "safe" });
  game.hud.setStatus("신발장 안으로. 호흡을 끊으십시오.", 2800);
  game.update(0.05, { skipRender: false });
  game.renderer.render(game.scene, game.camera);
  return {
    hidden: game.player.isHidden === true,
    cabinet: cabinet?.id || null,
    ux: uncat.group.position.x,
    uz: uncat.group.position.z,
  };
});
console.log("hide", hide);
if (!hide.hidden) {
  throw new Error(`ring hide failed: ${JSON.stringify(hide)}`);
}

for (let i = 0; i < 8; i += 1) {
  await page.evaluate(() => {
    const game = window.__happyToy;
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    game.update(0.05, { skipRender: false });
    uncat.group.visible = true;
    uncat.group.position.set(14.6, 0, -6.35);
    game.renderer.render(game.scene, game.camera);
  });
  await page.waitForTimeout(70);
}

const stillPath = path.join(outDir, "f1_ring_chase_hide_still.png");
await page.screenshot({ path: stillPath, timeout: 120000 });

await context.close();
await browser.close();

const recorded = fs.readdirSync(videoDir).filter((name) => name.endsWith(".webm"));
if (!recorded.length) {
  throw new Error("playthrough video was not written");
}
const dest = path.join(outDir, "f1_ring_chase_hide.webm");
fs.copyFileSync(path.join(videoDir, recorded[0]), dest);
console.log("ok", { video: dest, still: stillPath, files: recorded, hide });
