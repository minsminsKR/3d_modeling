import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

fs.mkdirSync(outDir, { recursive: true });
const videoDir = path.join(outDir, "wasd-classroom-hide-beats-raw");
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

const pose = (player, lookAt, uncat, status) => page.evaluate(({ player, lookAt, uncat, status }) => {
  const game = window.__happyToy;
  game.ghostMode = true;
  game.testSafeMode = false;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.mapBuilder.generator.generateChunk(1, 0);
  game.mapBuilder.generator.generateChunk(2, 0);
  game.player.setPosition({ x: player[0], y: 0, z: player[2] });
  game.flashlightController?.setEnabled(true, false);
  const foe = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  foe.setDormant(false);
  foe.group.visible = true;
  foe.group.position.set(uncat[0], 0, uncat[2]);
  foe.group.lookAt(player[0], 1.15, player[2]);
  foe.state = "chase";
  game.player.setLookAt({ x: lookAt[0], y: lookAt[1], z: lookAt[2] });
  if (status) game.hud.setStatus(status, 2800);
  game.update(0.05, { skipRender: false });
  foe.group.visible = true;
  foe.group.position.set(uncat[0], 0, uncat[2]);
  game.renderer.render(game.scene, game.camera);
  return {
    x: game.player.position.x,
    z: game.player.position.z,
    ux: foe.group.position.x,
    uz: foe.group.position.z,
  };
}, { player, lookAt, uncat, status });

const beats = [
  { player: [16, 0, 0], lookAt: [24, 1.15, 0], uncat: [24.2, 0, 0], status: "복도가 끝없이 이어집니다. 안개 끝의 발소리를 신발장으로 빼십시오." },
  { player: [14.2, 0, 0], lookAt: [22, 1.15, 0], uncat: [22.4, 0, 0] },
  { player: [12.4, 0, 0], lookAt: [20.5, 1.15, 0], uncat: [20.6, 0, 0] },
  { player: [10.75, 0, 0], lookAt: [10.75, 0.9, 4.4], uncat: [18.4, 0, 0] },
  { player: [10.75, 0, 1.35], lookAt: [12.4, 0.8, 3.8], uncat: [16.2, 0, 0] },
  { player: [10.75, 0, 2.2], lookAt: [12.55, 0.72, 4.2], uncat: [14.6, 0, 0.4] },
  { player: [11.15, 0, 3.05], lookAt: [12.8, 0.7, 4.45], uncat: [12.8, 0, 1.1] },
  { player: [10.75, 0, 4.15], lookAt: [10.75, 1.05, 1.3], uncat: [11.4, 0, 1.35] },
  { player: [10.75, 0, 5.05], lookAt: [10.75, 1.1, 1.2], uncat: [10.75, 0, 1.2] },
];

for (const beat of beats) {
  console.log("beat", await pose(beat.player, beat.lookAt, beat.uncat, beat.status));
  await page.waitForTimeout(90);
}

const hide = await page.evaluate(() => {
  const game = window.__happyToy;
  game.ghostMode = true;
  game.testSafeMode = false;
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
  uncat.state = "investigateCabinet";
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
if (!hide.hidden) {
  throw new Error(`classroom hide failed: ${JSON.stringify(hide)}`);
}

for (let i = 0; i < 8; i += 1) {
  await page.evaluate(() => {
    const game = window.__happyToy;
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    game.update(0.05, { skipRender: false });
    uncat.group.visible = true;
    uncat.group.position.set(10.75, 0, 1.2);
    game.renderer.render(game.scene, game.camera);
  });
  await page.waitForTimeout(70);
}

const stillPath = path.join(outDir, "f1_wasd_classroom_hide_still.png");
await page.screenshot({ path: stillPath, timeout: 120000 });

await context.close();
await browser.close();

const recorded = fs.readdirSync(videoDir).filter((name) => name.endsWith(".webm"));
if (!recorded.length) {
  throw new Error("playthrough video was not written");
}
const dest = path.join(outDir, "f1_wasd_classroom_hide.webm");
fs.copyFileSync(path.join(videoDir, recorded[0]), dest);
console.log("ok", { video: dest, still: stillPath, files: recorded, hide });
