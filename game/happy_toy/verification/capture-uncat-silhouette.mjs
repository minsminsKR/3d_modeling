import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

fs.mkdirSync(outDir, { recursive: true });
const videoDir = path.join(outDir, "uncat-silhouette-chase-raw");
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

const poseChase = (uncatX) => page.evaluate((uncatX) => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.ghostMode = false;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  game.player.setPosition({ x: 14.2, y: 0, z: 0 });
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(uncatX, 0, 0);
  uncat.group.rotation.set(0, Math.atan2(14.2 - uncatX, 0), 0);
  uncat.state = "chase";
  for (let i = 0; i < 4; i += 1) {
    game.update(0.05, { skipRender: true });
    uncat.group.position.set(uncatX, 0, 0);
    uncat.group.visible = true;
  }
  game.player.setLookAt(uncat.group.position.clone().setY(1.22));
  game.flashlightController?.setEnabled(true, false);
  game.hud.setStatus("안개 끝의 실루엣이 복도를 닫습니다.", 2800);
  game.renderer.render(game.scene, game.camera);
  const figure = uncat.group.getObjectByName("uncat-silhouette");
  return {
    px: game.player.position.x,
    ux: uncat.group.position.x,
    dist: Math.hypot(uncat.group.position.x - 14.2, uncat.group.position.z),
    silhouette: uncat.config.silhouette === true,
    figure: Boolean(figure),
    fogFar: game.scene.fog?.far ?? 0,
  };
}, uncatX);

for (let i = 0; i < 10; i += 1) {
  const x = 20.6 - i * 0.48;
  console.log("chase", await poseChase(x));
  await page.waitForTimeout(70);
}

const chasePath = path.join(outDir, "f1_uncat_silhouette_chase.png");
await page.screenshot({ path: chasePath, timeout: 120000 });

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
  uncat.group.position.set(10.75, 0, 1.15);
  uncat.group.rotation.y = Math.atan2(0, 5.25 - 1.15);
  uncat.state = "investigateCabinet";
  game.player.setPosition({
    x: cabinet.position.x,
    y: cabinet.position.y,
    z: cabinet.position.z - 0.85,
  });
  game.enterCabinet(cabinet, { forceOutcome: "safe" });
  game.hud.setStatus("신발장 슬랫 너머로 실루엣이 머뭅니다.", 2800);
  game.update(0.05, { skipRender: false });
  game.renderer.render(game.scene, game.camera);
  return {
    hidden: game.player.isHidden === true,
    cabinet: cabinet?.id || null,
    cx: cabinet?.position?.x,
    cz: cabinet?.position?.z,
    ux: uncat.group.position.x,
    uz: uncat.group.position.z,
    figure: Boolean(uncat.group.getObjectByName("uncat-silhouette")),
  };
});
console.log("hide", hide);
if (!hide.hidden) {
  throw new Error(`silhouette locker peek failed: ${JSON.stringify(hide)}`);
}

for (let i = 0; i < 6; i += 1) {
  await page.evaluate(() => {
    const game = window.__happyToy;
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    game.update(0.05, { skipRender: false });
    uncat.group.visible = true;
    uncat.group.position.set(10.75, 0, 1.15);
    game.renderer.render(game.scene, game.camera);
  });
  await page.waitForTimeout(70);
}

const peekPath = path.join(outDir, "f1_uncat_silhouette_locker_peek.png");
await page.screenshot({ path: peekPath, timeout: 120000 });

await context.close();
await browser.close();

const recorded = fs.readdirSync(videoDir).filter((name) => name.endsWith(".webm"));
if (!recorded.length) {
  throw new Error("silhouette chase video was not written");
}
const dest = path.join(outDir, "f1_uncat_silhouette_chase.webm");
fs.copyFileSync(path.join(videoDir, recorded[0]), dest);
console.log("ok", { video: dest, chase: chasePath, peek: peekPath, hide, files: recorded });
