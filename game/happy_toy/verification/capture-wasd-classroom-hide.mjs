import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

fs.mkdirSync(outDir, { recursive: true });
const videoDir = path.join(outDir, "wasd-classroom-hide-raw");
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
  game.ghostMode = true;
  game.testSafeMode = false;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  game.mapBuilder.generator.generateChunk(1, 0);
  game.mapBuilder.generator.generateChunk(2, 0);
  game.player.setPosition({ x: 16, y: 0, z: 0 });
  game.flashlightController?.setEnabled(true, false);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(24.2, 0, 0);
  uncat.state = "chase";
  game.hud.setStatus("복도가 끝없이 이어집니다. 안개 끝의 발소리를 신발장으로 빼십시오.", 4200);
  game.player.setLookAt(uncat.group.position.clone().setY(1.15));
  game.renderer.render(game.scene, game.camera);
});

const stepWalk = (target, lookAt, maxSteps = 80) => page.evaluate(({ target, lookAt, maxSteps }) => {
  const game = window.__happyToy;
  const player = game.player;
  game.ghostMode = true;
  game.testSafeMode = false;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  let lastX = player.position.x;
  let lastZ = player.position.z;
  let stuck = 0;
  let frames = 0;
  for (let i = 0; i < maxSteps; i += 1) {
    const dx = target.x - player.position.x;
    const dz = target.z - player.position.z;
    const dist = Math.hypot(dx, dz);
    if (dist < 0.9) {
      game.input.keys.delete("w");
      break;
    }
    player.yaw = Math.atan2(-dx, -dz);
    player.pitch = lookAt ? Math.atan2(lookAt[1] - 1.15, Math.hypot(lookAt[0] - player.position.x, lookAt[2] - player.position.z)) * 0.15 : 0;
    game.input.keys.add("w");
    game.update(0.05, { skipRender: false });
    if (uncat) {
      uncat.group.visible = true;
      if (uncat.state !== "chase") uncat.state = "chase";
    }
    game.renderer.render(game.scene, game.camera);
    frames += 1;
    const moved = Math.hypot(player.position.x - lastX, player.position.z - lastZ);
    if (moved < 0.016) {
      stuck += 1;
      if (stuck > 20) break;
    } else {
      stuck = 0;
      lastX = player.position.x;
      lastZ = player.position.z;
    }
  }
  game.input.keys.delete("w");
  if (lookAt) game.player.setLookAt({ x: lookAt[0], y: lookAt[1], z: lookAt[2] });
  game.renderer.render(game.scene, game.camera);
  return {
    x: player.position.x,
    z: player.position.z,
    dist: Math.hypot(target.x - player.position.x, target.z - player.position.z),
    frames,
    ux: uncat?.group.position.x,
    uz: uncat?.group.position.z,
  };
}, { target, lookAt, maxSteps });

const holds = async (n, fn) => {
  for (let i = 0; i < n; i += 1) {
    console.log(await fn(i));
    await page.waitForTimeout(50);
  }
};

await holds(3, (i) => stepWalk({ x: 12.4 - i * 0.4, z: 0 }, [22, 1.1, 0], 18));
console.log("door", await stepWalk({ x: 10.75, z: 0 }, [10.75, 0.9, 4.2], 40));
await page.waitForTimeout(80);
console.log("aisle", await stepWalk({ x: 10.75, z: 2.2 }, [12.5, 0.75, 4.2], 50));
await page.waitForTimeout(120);
console.log("desks", await stepWalk({ x: 11.2, z: 3.0 }, [12.7, 0.7, 4.4], 30));
await page.waitForTimeout(100);
console.log("locker", await stepWalk({ x: 10.75, z: 5.15 }, [10.75, 1.1, 1.2], 50));

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
  uncat.group.position.set(10.75, 0, 1.15);
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
  uncat.group.position.set(10.75, 0, 1.15);
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
  throw new Error(`classroom WASD hide failed: ${JSON.stringify(hide)}`);
}

for (let i = 0; i < 10; i += 1) {
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
