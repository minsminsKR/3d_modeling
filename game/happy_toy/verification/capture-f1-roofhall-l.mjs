import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const outFile = "f1_roofhall_dual_inner.png";
const outPath = path.join(outDir, outFile);
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

if (fs.existsSync(outPath)) {
  throw new Error(`refusing to overwrite immutable artifact ${outPath}`);
}

const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.on("pageerror", (error) => console.error("pageerror", error.message));

await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
await page.evaluate(() => {
  const game = window.__happyToy;
  game.menuSystem?.hideMenu();
  game.start();
  game.loop?.stop();
  game.ghostMode = true;
  game.testSafeMode = false;
  if (game.player.isHidden) game.exitCabinet();
});
await page.waitForTimeout(200);

const walkTo = (target, maxSteps = 360) => page.evaluate(({ target, maxSteps }) => {
  const game = window.__happyToy;
  const player = game.player;
  game.ghostMode = true;
  game.testSafeMode = false;
  game.loop?.stop();
  if (player.isHidden) game.exitCabinet();
  const openDoors = () => {
    for (const door of game.doors || []) {
      if (Math.abs((door.position.y || 0) - player.position.y) > 2.4) continue;
      if (door.distanceTo(player.position) < 3.2 && !door.isLocked && !door.isBlocked) {
        if (!door.isOpen) door.interact(game);
        door.openAmount = 1;
        door.update(0.2);
      }
    }
  };
  let stuck = 0;
  let lastX = player.position.x;
  let lastZ = player.position.z;
  for (let i = 0; i < maxSteps; i += 1) {
    const dx = target.x - player.position.x;
    const dz = target.z - player.position.z;
    const dist = Math.hypot(dx, dz);
    if (dist < 0.9) {
      game.input.keys.delete("w");
      game.input.keys.delete("a");
      game.input.keys.delete("d");
      return { ok: true, steps: i, x: player.position.x, z: player.position.z, dist };
    }
    player.yaw = Math.atan2(-dx, -dz);
    player.pitch = 0;
    openDoors();
    game.input.keys.add("w");
    game.update(0.05, { skipRender: true });
    const moved = Math.hypot(player.position.x - lastX, player.position.z - lastZ);
    if (moved < 0.016) {
      stuck += 1;
      if (stuck % 8 === 0) {
        game.input.keys.delete("w");
        game.input.keys.add(stuck % 16 === 0 ? "a" : "d");
        game.update(0.05, { skipRender: true });
        game.input.keys.delete("a");
        game.input.keys.delete("d");
      }
      if (stuck > 36) break;
    } else {
      stuck = 0;
      lastX = player.position.x;
      lastZ = player.position.z;
    }
  }
  game.input.keys.delete("w");
  const dist = Math.hypot(target.x - player.position.x, target.z - player.position.z);
  return { ok: dist < 2.2, steps: maxSteps, x: player.position.x, z: player.position.z, dist };
}, { target, maxSteps });

await page.evaluate(() => {
  const game = window.__happyToy;
  game.ghostMode = true;
  game.testSafeMode = false;
  game.cutsceneEvent = null;
  game.monsterIntroManager?.reset?.();
  if (game.player.isHidden) game.exitCabinet();
  game.mapBuilder.generator.generateChunk(0, 1);
  game.mapBuilder.generator.generateChunk(0, 2);
  game.mapBuilder.generator.generateChunk(-1, 2);
  game.player.setPosition({ x: 0, y: 0, z: 22 });
  for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  if (game.player.isHidden) game.exitCabinet();
});

const roofHallWalk = [];
for (const stop of [
  { x: 0, z: 22 },
  { x: 0, z: 28 },
  { x: 0, z: 32 },
  { x: -6, z: 32 },
]) {
  roofHallWalk.push(await walkTo(stop, 360));
  console.log("roofhall", stop, roofHallWalk.at(-1));
}

const lookStop = await walkTo({ x: 0.35, z: 32.45 }, 240);
console.log("roofhall-look", lookStop);

const pose = await page.evaluate(() => {
  const game = window.__happyToy;
  if (game.player.isHidden) game.exitCabinet();
  const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
  uncat?.setDormant?.(true);
  if (uncat) uncat.group.visible = false;
  game.floorHuntDirector?.hide?.();
  game.hud?.setPrompt?.("");
  game.hud?.setStatus?.("옥상 복도. 양쪽 안쪽 L자 벽.", 2800);

  const runN = game.scene.getObjectByName("chunk_0_2_roofhall_run_n");
  const runW = game.scene.getObjectByName("chunk_0_2_roofhall_run_w");
  const runWest = game.scene.getObjectByName("chunk_0_2_roofhall_run_west");
  const lookAt = [
    runN ? runN.position.x : 2.24,
    1.18,
    runW ? runW.position.z : 34.34,
  ];
  const roof = [...(game.mapBuilder?.loadedChunks?.values?.() || [])]
    .find((chunk) => chunk.cx === 0 && chunk.cz === 2);
  const names = (roof?.meshes || []).map((mesh) => String(mesh.name || ""));
  const info = game.poseForCapture({
    lookAt,
    flashlight: true,
    freezeLoop: true,
  });
  return {
    ...info,
    lookAt,
    pos: { x: game.player.position.x, z: game.player.position.z },
    runN: runN ? { x: runN.position.x, y: runN.position.y, z: runN.position.z } : null,
    runW: runW ? { x: runW.position.x, y: runW.position.y, z: runW.position.z } : null,
    runWest: runWest ? { x: runWest.position.x, z: runWest.position.z } : null,
    sides: game.mapBuilder.generator.getHallSides(0, 2),
    openings: game.mapBuilder.generator.getOpenings(0, 2),
    type: game.mapBuilder.generator.getChunkType(0, 2),
    plusDoors: names.filter((name) => name.includes("class_door")),
    cabinets: (roof?.cabinets || []).map((cab) => ({
      id: cab.id,
      x: cab.position?.x,
      z: cab.position?.z,
    })),
    lRuns: names.filter((name) => name.includes("roofhall_run")),
    board: names.some((name) => name.includes("roofhall_board")),
  };
});

await page.evaluate(() => {
  window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera);
});
await page.screenshot({ path: outPath, timeout: 120000 });
console.log(outFile, pose);
console.log("wrote", outPath);
await browser.close();
