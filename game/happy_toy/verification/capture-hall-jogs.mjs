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

async function poseShot(name, pose) {
  const info = await page.evaluate((options) => {
    const game = window.__happyToy;
    game.testSafeMode = true;
    game.player.noclip = true;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.enemyManager?.enemies?.forEach((enemy) => {
      if (enemy.group) enemy.group.visible = false;
    });
    game.floorHuntDirector?.hide?.();
    game.hud?.setPrompt?.("");
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 14; i += 1) game.update(0.05, { skipRender: true });
    }
    game.hud?.setPrompt?.("");
    game.hud?.setStatus(options.status || "", 4200);
    return game.poseForCapture({
      x: options.x,
      y: options.y,
      z: options.z,
      yaw: options.yaw,
      pitch: options.pitch,
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name), timeout: 120000 });
  console.log(name, info);
  return info;
}

const chase = await poseShot("f1_uncat_s_bend_chase.png", {
  x: 12.2,
  y: 0,
  z: 2.55,
  lookAt: [16.2, 1.2, 2.5],
  status: "꺾인 복도에서 실내화가 따라옵니다. 벽장으로.",
});
const chaseInfo = await page.evaluate(() => {
  const game = window.__happyToy;
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(16.05, 0, 2.58);
  uncat.group.lookAt(12.2, 1.2, 2.55);
  uncat.state = "chase";
  game.hud.setPrompt("");
  game.hud.setStatus("꺾인 복도에서 실내화가 따라옵니다. 벽장으로.", 4200);
  game.renderer.render(game.scene, game.camera);
  return { visible: uncat.group.visible === true, x: uncat.group.position.x };
});
await page.screenshot({ path: path.join(outDir, "f1_uncat_s_bend_chase.png"), timeout: 120000 });
console.log("uncat overlay", chaseInfo);

const hideInfo = await page.evaluate(() => {
  const game = window.__happyToy;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.hud?.setPrompt?.("");
  const cab = (game.cabinets || []).find((item) => (
    Math.abs((item.position?.y || 0)) < 1.2
    && Math.hypot((item.position?.x || 0) - 10.75, (item.position?.z || 0) - 5.25) < 2.4
  )) || game.mapBuilder.generator.generateChunk(1, 0).cabinets?.[0];
  if (!cab) return { hidden: false };
  if (!(game.cabinets || []).includes(cab)) game.cabinets.push(cab);
  game.player.setPosition({ x: cab.position.x, y: 0, z: cab.position.z + 0.9 });
  for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  game.enterCabinet(cab, { forceOutcome: "safe" });
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(12.35, 0, 4.85);
  uncat.group.lookAt(cab.position.x, 1.2, cab.position.z);
  uncat.state = "investigateCabinet";
  uncat.cabinetTarget = cab;
  game.hud.setPrompt("");
  game.hud.setStatus("신발장 안에서. 꺾인 복도의 발소리가 문을 두드립니다.", 4200);
  game.player.applyCabinetView?.();
  game.renderer.render(game.scene, game.camera);
  return {
    hidden: game.player.isHidden === true,
    cabX: cab.position.x,
    cabZ: cab.position.z,
    uncatVisible: uncat.group.visible === true,
  };
});
await page.screenshot({ path: path.join(outDir, "f1_s_bend_locker_hide.png"), timeout: 120000 });
console.log("locker hide", hideInfo);

if (!chase.flashlight || chase.intensity < 8) {
  throw new Error(`east S-bend chase capture failed: ${JSON.stringify(chase)}`);
}
if (!chaseInfo.visible) {
  throw new Error(`Uncat missing in S-bend chase: ${JSON.stringify(chaseInfo)}`);
}
if (!hideInfo.hidden) {
  throw new Error(`S-bend locker hide capture failed: ${JSON.stringify(hideInfo)}`);
}

console.log("ok");
await browser.close();
