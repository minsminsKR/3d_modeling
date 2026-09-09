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

const info = await page.evaluate(() => {
  const game = window.__happyToy;
  const THREE = window.THREE;
  game.testSafeMode = true;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  game.mapBuilder.generator.generateChunk(1, 0);
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(18.4, 0, 0);
  uncat.group.rotation.set(0, -Math.PI / 2, 0);
  uncat.state = "chase";
  for (let i = 0; i < 8; i += 1) {
    game.update(0.05, { skipRender: true });
    uncat.group.position.set(18.4, 0, 0);
    uncat.group.visible = true;
    uncat.group.rotation.set(0, -Math.PI / 2, 0);
  }
  const pose = game.poseForCapture({
    x: 15.35,
    y: 0,
    z: 0,
    lookAt: [18.4, 1.15, 0],
    flashlight: true,
    freezeLoop: true,
  });
  uncat.group.position.set(18.4, 0, 0);
  uncat.group.visible = true;
  uncat.group.rotation.set(0, -Math.PI / 2, 0);
  const box = new THREE.Box3().setFromObject(uncat.group);
  const size = box.getSize(new THREE.Vector3());
  const names = [];
  uncat.group.traverse((child) => {
    if (child.name) names.push(child.name);
  });
  game.renderer.render(game.scene, game.camera);
  return {
    pose,
    names,
    bbox: {
      min: { x: box.min.x, y: box.min.y, z: box.min.z },
      max: { x: box.max.x, y: box.max.y, z: box.max.z },
      size: { x: size.x, y: size.y, z: size.z },
    },
    cam: {
      x: game.camera.position.x,
      y: game.camera.position.y,
      z: game.camera.position.z,
    },
    fogFar: game.scene.fog?.far ?? 0,
    visible: uncat.group.visible,
    childCount: uncat.group.children.length,
  };
});
console.log(JSON.stringify(info, null, 2));
await page.screenshot({ path: path.join(outDir, "f1_uncat_black_silhouette_hall.png"), timeout: 120000 });

const peek = await page.evaluate(() => {
  const game = window.__happyToy;
  const cabinet = (game.cabinets || []).find((item) => (
    Math.abs((item.position?.y || 0)) < 1.2
    && Math.hypot((item.position?.x || 0) - 10.75, (item.position?.z || 0) - 5.25) < 1.6
  )) || game.cabinets[0];
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat.setDormant(false);
  uncat.group.visible = true;
  uncat.group.position.set(10.75, 0, 3.55);
  uncat.group.rotation.set(0, 0, 0);
  uncat.state = "investigateCabinet";
  game.enterCabinet(cabinet, { forceOutcome: "safe" });
  game.hud.setStatus("신발장 슬랫 너머로 실루엣이 머뭅니다.", 2800);
  game.update(0.05, { skipRender: false });
  uncat.group.position.set(10.75, 0, 3.55);
  uncat.group.visible = true;
  game.renderer.render(game.scene, game.camera);
  return {
    hidden: game.player.isHidden === true,
    camY: game.camera.position.y,
    camZ: game.camera.position.z,
    ux: uncat.group.position.x,
    uz: uncat.group.position.z,
  };
});
console.log("peek", peek);
await page.screenshot({ path: path.join(outDir, "f1_uncat_black_silhouette_peek.png"), timeout: 120000 });
await browser.close();
console.log("ok");
