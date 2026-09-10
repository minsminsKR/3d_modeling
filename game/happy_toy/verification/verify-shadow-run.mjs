import assert from "node:assert/strict";
import { createRequire } from "node:module";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const browser = await chromium.launch({ executablePath, headless: true });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.menuSystem?.hideMenu();
    game.start();
    game.loop?.stop();
    game.ghostMode = true;
    game.testSafeMode = false;
  });
  await page.waitForTimeout(200);

  const walkTo = (target, maxSteps = 360) => page.evaluate(({ target, maxSteps }) => {
    const game = window.__happyToy;
    const player = game.player;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.loop?.stop();
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
    for (const door of game.doors || []) {
      if (Math.abs(door.position.x) < 9 && Math.abs(door.position.z) < 9 && !door.isLocked && !door.isBlocked) {
        door.isOpen = true;
        door.openAmount = 1;
        door.update(0.3);
      }
    }
  });

  const annexWalk = [];
  for (const stop of [
    { x: 10.2, z: 0 },
    { x: 10.75, z: 0 },
    { x: 10.75, z: 5.15 },
    { x: 10.75, z: 0 },
    { x: 16, z: 0 },
    { x: 32, z: 0 },
    { x: 48, z: 0 },
    { x: 64, z: 0 },
    { x: 80, z: 0 },
    { x: 80, z: -16 },
  ]) {
    annexWalk.push(await walkTo(stop, 400));
    console.log("walk", stop, annexWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: 11.5, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const loopWalk = [];
  for (const stop of [
    { x: 16, z: 0 },
    { x: 16, z: -5.5 },
    { x: 16, z: 0 },
    { x: 10.75, z: 0 },
    { x: 10.75, z: -5.25 },
    { x: 10.75, z: 0 },
    { x: 16, z: 0 },
    { x: 21.25, z: 0 },
    { x: 21.25, z: 5.25 },
  ]) {
    loopWalk.push(await walkTo(stop, 360));
    console.log("loop", stop, loopWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: 11.5, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const ringWalk = [];
  for (const stop of [
    { x: 11.5, z: 0 },
    { x: 16, z: 0 },
    { x: 20.5, z: 0 },
    { x: 21.25, z: 5.25 },
  ]) {
    ringWalk.push(await walkTo(stop, 360));
    console.log("ring", stop, ringWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 0);
    game.player.setPosition({ x: 20.5, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const longRingWalk = [];
  for (const stop of [
    { x: 24.2, z: 0 },
    { x: 27.5, z: 0 },
    { x: 32, z: 0 },
  ]) {
    longRingWalk.push(await walkTo(stop, 360));
    console.log("longring", stop, longRingWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(0, -1);
    game.player.setPosition({ x: 0, y: 0, z: -8 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const northWalk = [];
  for (const stop of [
    { x: 0, z: -11.5 },
    { x: -5.25, z: -10.75 },
    { x: 0, z: -11.5 },
    { x: 0, z: -16 },
    { x: 5.2, z: -16 },
    { x: 0, z: -16 },
    { x: 0, z: -20.5 },
  ]) {
    northWalk.push(await walkTo(stop, 360));
    console.log("north", stop, northWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: 0, y: 0, z: -8 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const nsLoopWalk = [];
  for (const stop of [
    { x: 0, z: -11.5 },
    { x: -5.25, z: -10.75 },
    { x: 0, z: -11.5 },
    { x: 0, z: -16 },
    { x: 5.2, z: -16 },
    { x: 0, z: -16 },
    { x: 0, z: -20.5 },
  ]) {
    nsLoopWalk.push(await walkTo(stop, 360));
    console.log("nsloop", stop, nsLoopWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.mapBuilder.generator.generateChunk(1, 0);
    game.player.setPosition({ x: 16, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const f1MazeWalk = [];
  for (const stop of [
    { x: 16, z: 0 },
    { x: 10.75, z: 0 },
    { x: 10.75, z: 5.25 },
  ]) {
    f1MazeWalk.push(await walkTo(stop, 280));
    console.log("f1maze", stop, f1MazeWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(1, 0);
    game.mapBuilder.generator.generateChunk(2, 0);
    game.player.setPosition({ x: 16, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const throughWalk = [];
  for (const stop of [
    { x: 21.15, z: -2.15 },
    { x: 22.7, z: -6.45 },
    { x: 25.3, z: -6.45 },
    { x: 26.85, z: -2.1 },
    { x: 26.75, z: 0 },
    { x: 32, z: 0 },
  ]) {
    throughWalk.push(await walkTo(stop, 420));
    console.log("through", stop, throughWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(7, 0);
    game.mapBuilder.generator.generateChunk(8, 0);
    game.player.setPosition({ x: 112, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const gymWalk = [];
  for (const stop of [
    { x: 112, z: 6.5 },
    { x: 112, z: 0 },
    { x: 118.5, z: 0 },
    { x: 122.4, z: 0 },
    { x: 128, z: 0 },
  ]) {
    gymWalk.push(await walkTo(stop, 420));
    console.log("gym", stop, gymWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(7, 0);
    game.player.setPosition({ x: 112, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const trophyWalk = [];
  for (const stop of [
    { x: 117.2, z: 0 },
    { x: 117.2, z: 4.2 },
  ]) {
    trophyWalk.push(await walkTo(stop, 320));
    console.log("trophy_room", stop, trophyWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(7, 0);
    game.player.setPosition({ x: 112, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const trophySwWalk = [];
  for (const stop of [
    { x: 105.5, z: 0 },
    { x: 105.5, z: 4.2 },
  ]) {
    trophySwWalk.push(await walkTo(stop, 320));
    console.log("trophy_sw", stop, trophySwWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(7, 0);
    game.player.setPosition({ x: 112, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const trophyWideWalk = [];
  for (const stop of [
    { x: 114.8, z: 0 },
    { x: 114.8, z: 2.85 },
  ]) {
    trophyWideWalk.push(await walkTo(stop, 280));
    console.log("trophy_wide", stop, trophyWideWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 0);
    game.player.setPosition({ x: 64, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const annexNorthWalk = [];
  for (const stop of [{ x: 64, z: -6.5 }]) {
    annexNorthWalk.push(await walkTo(stop, 320));
    console.log("annex_north", stop, annexNorthWalk.at(-1));
  }
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: 64, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const annexRoomWalk = [];
  for (const stop of [
    { x: 70.2, z: 0 },
    { x: 70.2, z: -4.2 },
  ]) {
    annexRoomWalk.push(await walkTo(stop, 320));
    console.log("annex_room", stop, annexRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 0);
    game.player.setPosition({ x: 64, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const annexPinchWalk = [];
  for (const stop of [
    { x: 70.0, z: 0 },
    { x: 70.0, z: 2.5 },
  ]) {
    annexPinchWalk.push(await walkTo(stop, 220));
    console.log("annex_pinch", stop, annexPinchWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 0);
    game.player.setPosition({ x: 68.2, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const annexOffsetWalk = [];
  for (const stop of [
    { x: 68.2, z: -2.5 },
  ]) {
    annexOffsetWalk.push(await walkTo(stop, 220));
    console.log("annex_offset", stop, annexOffsetWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 0);
    game.player.setPosition({ x: 32, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const eastwashRoomWalk = [];
  for (const stop of [
    { x: 38.3, z: 0 },
    { x: 38.3, z: -4.2 },
  ]) {
    eastwashRoomWalk.push(await walkTo(stop, 320));
    console.log("eastwash_room", stop, eastwashRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(5, 0);
    game.player.setPosition({ x: 80, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const washfourRoomWalk = [];
  for (const stop of [
    { x: 86.2, z: 0 },
    { x: 86.2, z: -4.2 },
  ]) {
    washfourRoomWalk.push(await walkTo(stop, 320));
    console.log("washfour_room", stop, washfourRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-1, 0);
    game.player.setPosition({ x: -16, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const angelRoomWalk = [];
  for (const stop of [
    { x: -10.8, z: 0 },
    { x: -10.8, z: -4.2 },
  ]) {
    angelRoomWalk.push(await walkTo(stop, 320));
    console.log("angel_room", stop, angelRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 0);
    game.player.setPosition({ x: 32, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const windowBlock = await walkTo({ x: 37.2, z: 5.25 }, 220);
  console.log("windowBlock", windowBlock);

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(5, 0);
    game.mapBuilder.generator.generateChunk(5, 1);
    game.player.setPosition({ x: 80, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const yardWalk = [];
  for (const stop of [
    { x: 80, z: 7.4 },
    { x: 80, z: 11.2 },
    { x: 84.4, z: 11.2 },
    { x: 84.4, z: 16 },
    { x: 84.4, z: 20.8 },
    { x: 80, z: 20.8 },
  ]) {
    yardWalk.push(await walkTo(stop, 360));
    console.log("yard", stop, yardWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 0);
    game.mapBuilder.generator.generateChunk(4, 1);
    game.mapBuilder.generator.generateChunk(5, 1);
    game.player.setPosition({ x: 64, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const arcadeWalk = [];
  for (const stop of [
    { x: 64, z: 10 },
    { x: 64, z: 16 },
    { x: 70, z: 16 },
    { x: 64, z: 16 },
    { x: 64, z: 22 },
  ]) {
    arcadeWalk.push(await walkTo(stop, 360));
    console.log("arcade", stop, arcadeWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(8, -2);
    game.mapBuilder.generator.generateChunk(8, -1);
    game.player.setPosition({ x: 128, y: 0, z: -32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const artWalk = [];
  for (const stop of [
    { x: 128, z: -26 },
    { x: 128, z: -16 },
  ]) {
    artWalk.push(await walkTo(stop, 420));
    console.log("art", stop, artWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(6, 0);
    game.player.setPosition({ x: 96, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const memorialWalk = [];
  for (const stop of [
    { x: 96, z: 0 },
    { x: 96, z: 6.5 },
    { x: 96, z: 0 },
    { x: 102, z: 0 },
  ]) {
    memorialWalk.push(await walkTo(stop, 360));
    console.log("memorial", stop, memorialWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(6, 0);
    game.player.setPosition({ x: 99.4, y: 0, z: 0 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const memorialOffsetWalk = [];
  for (const stop of [
    { x: 99.4, z: 3.2 },
  ]) {
    memorialOffsetWalk.push(await walkTo(stop, 220));
    console.log("memorial_offset", stop, memorialOffsetWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(7, 1);
    game.mapBuilder.generator.generateChunk(8, 1);
    game.player.setPosition({ x: 112, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const foyerWalk = [];
  for (const stop of [
    { x: 112, z: 16 },
    { x: 112, z: 10 },
    { x: 112, z: 16 },
    { x: 118, z: 16 },
  ]) {
    foyerWalk.push(await walkTo(stop, 360));
    console.log("foyer", stop, foyerWalk.at(-1));
  }
  const audWalk = [];
  for (const stop of [
    { x: 122, z: 16 },
    { x: 128, z: 16 },
  ]) {
    audWalk.push(await walkTo(stop, 420));
    console.log("aud", stop, audWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(6, 2);
    game.mapBuilder.generator.generateChunk(7, 2);
    game.mapBuilder.generator.generateChunk(8, 2);
    game.player.setPosition({ x: 106, y: 0, z: 32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const practiceBlock = await page.evaluate(() => {
    const game = window.__happyToy;
    const start = { x: 106, y: 0.9, z: 32 };
    const end = { x: 118, y: 0.9, z: 32 };
    const center = { x: 112, y: 0.9, z: 32 };
    const los = game.collisionWorld.hasLineOfSight(start, end);
    const blocked = game.collisionWorld.isCircleBlocked(center, 0.34);
    game.player.setPosition({ x: 106, y: 0, z: 32 });
    for (let i = 0; i < 22; i += 1) {
      const dx = 118 - game.player.position.x;
      const dz = 32 - game.player.position.z;
      game.player.yaw = Math.atan2(-dx, -dz);
      game.player.pitch = 0;
      game.input.keys.add("w");
      game.update(0.05, { skipRender: true });
    }
    game.input.keys.delete("w");
    const dist = Math.hypot(118 - game.player.position.x, 32 - game.player.position.z);
    return {
      los,
      blocked,
      ok: dist < 2.2,
      x: game.player.position.x,
      z: game.player.position.z,
      dist,
    };
  });
  console.log("practiceBlock", practiceBlock);
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: 106, y: 0, z: 32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const practiceWalk = [];
  for (const stop of [
    { x: 106, z: 35.5 },
    { x: 112, z: 35.5 },
    { x: 118, z: 35.5 },
    { x: 118, z: 32 },
    { x: 122, z: 32 },
    { x: 128, z: 32 },
  ]) {
    practiceWalk.push(await walkTo(stop, 420));
    console.log("practice", stop, practiceWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 1);
    game.mapBuilder.generator.generateChunk(4, 2);
    game.mapBuilder.generator.generateChunk(5, 2);
    game.player.setPosition({ x: 64, y: 0, z: 22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const broadcastWalk = [];
  for (const stop of [
    { x: 64, z: 28 },
    { x: 64, z: 32 },
    { x: 70, z: 32 },
  ]) {
    broadcastWalk.push(await walkTo(stop, 360));
    console.log("broadcast", stop, broadcastWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(8, -2);
    game.mapBuilder.generator.generateChunk(8, -1);
    game.player.setPosition({ x: 128, y: 0, z: -32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const studioWalk = [];
  for (const stop of [
    { x: 122, z: -32 },
    { x: 128, z: -32 },
    { x: 128, z: -26 },
  ]) {
    studioWalk.push(await walkTo(stop, 360));
    console.log("studio", stop, studioWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(8, -2);
    game.mapBuilder.generator.generateChunk(7, -2);
    game.player.setPosition({ x: 122, y: 0, z: -32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const darkroomWalk = [];
  for (const stop of [
    { x: 118, z: -32 },
    { x: 112, z: -32 },
    { x: 106, z: -32 },
  ]) {
    darkroomWalk.push(await walkTo(stop, 360));
    console.log("darkroom", stop, darkroomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 2);
    game.mapBuilder.generator.generateChunk(5, 2);
    game.mapBuilder.generator.generateChunk(5, 1);
    game.player.setPosition({ x: 70, y: 0, z: 32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const greenroomWalk = [];
  for (const stop of [
    { x: 74, z: 32 },
    { x: 80, z: 32 },
    { x: 80, z: 26 },
  ]) {
    greenroomWalk.push(await walkTo(stop, 360));
    console.log("greenroom", stop, greenroomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, -1);
    game.mapBuilder.generator.generateChunk(4, -2);
    game.mapBuilder.generator.generateChunk(5, -2);
    game.player.setPosition({ x: 64, y: 0, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const homeEcWalk = [];
  for (const stop of [
    { x: 64, z: -28 },
    { x: 64, z: -32 },
    { x: 70, z: -32 },
  ]) {
    homeEcWalk.push(await walkTo(stop, 360));
    console.log("home_ec", stop, homeEcWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(7, 0);
    game.mapBuilder.generator.generateChunk(7, -1);
    game.mapBuilder.generator.generateChunk(6, -1);
    game.player.setPosition({ x: 112, y: 0, z: -6 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const clubWalk = [];
  for (const stop of [
    { x: 112, z: -10 },
    { x: 112, z: -16 },
    { x: 106, z: -16 },
  ]) {
    clubWalk.push(await walkTo(stop, 360));
    console.log("club", stop, clubWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(6, 0);
    game.mapBuilder.generator.generateChunk(6, -1);
    game.mapBuilder.generator.generateChunk(7, -1);
    game.player.setPosition({ x: 96, y: 0, z: -6 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const specimenWalk = [];
  for (const stop of [
    { x: 96, z: -10 },
    { x: 96, z: -16 },
    { x: 102, z: -16 },
  ]) {
    specimenWalk.push(await walkTo(stop, 360));
    console.log("specimen", stop, specimenWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(5, 2);
    game.mapBuilder.generator.generateChunk(6, 2);
    game.mapBuilder.generator.generateChunk(6, 1);
    game.player.setPosition({ x: 86, y: 0, z: 32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const stageWalk = [];
  for (const stop of [
    { x: 90, z: 32 },
    { x: 96, z: 32 },
    { x: 96, z: 26 },
  ]) {
    stageWalk.push(await walkTo(stop, 360));
    console.log("stagewing", stop, stageWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, 0);
    game.mapBuilder.generator.generateChunk(4, -1);
    game.mapBuilder.generator.generateChunk(4, -2);
    game.player.setPosition({ x: 64, y: 0, z: -10 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const laundryWalk = [];
  for (const stop of [
    { x: 64, z: -16 },
    { x: 64, z: -22 },
  ]) {
    laundryWalk.push(await walkTo(stop, 360));
    console.log("laundry", stop, laundryWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, -1);
    game.player.setPosition({ x: 64, y: 0, z: -16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const laundryRoomWalk = [];
  for (const stop of [
    { x: 64, z: -22.3 },
    { x: 59.6, z: -22.3 },
  ]) {
    laundryRoomWalk.push(await walkTo(stop, 320));
    console.log("laundry_room", stop, laundryRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(6, -1);
    game.player.setPosition({ x: 96, y: 0, z: -16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const specimenRoomWalk = [];
  for (const stop of [
    { x: 96, z: -22.4 },
    { x: 91.6, z: -22.4 },
  ]) {
    specimenRoomWalk.push(await walkTo(stop, 320));
    console.log("specimen_room", stop, specimenRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(4, -2);
    game.mapBuilder.generator.generateChunk(5, -2);
    game.player.setPosition({ x: 70, y: 0, z: -32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const labLinkWalk = [];
  for (const stop of [
    { x: 74, z: -32 },
    { x: 80, z: -32 },
    { x: 86, z: -32 },
  ]) {
    labLinkWalk.push(await walkTo(stop, 360));
    console.log("lablink", stop, labLinkWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(1, 0);
    game.mapBuilder.generator.generateChunk(1, -1);
    game.mapBuilder.generator.generateChunk(2, -1);
    game.player.setPosition({ x: 16, y: 0, z: -10 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const avWalk = [];
  for (const stop of [
    { x: 16, z: -10 },
    { x: 16, z: -16 },
    { x: 22, z: -16 },
  ]) {
    avWalk.push(await walkTo(stop, 360));
    console.log("av", stop, avWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(0, -1);
    game.mapBuilder.generator.generateChunk(0, -2);
    game.mapBuilder.generator.generateChunk(1, -2);
    game.player.setPosition({ x: 0, y: 0, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const supplyWalk = [];
  for (const stop of [
    { x: 0, z: -22 },
    { x: 0, z: -32 },
    { x: 6, z: -32 },
  ]) {
    supplyWalk.push(await walkTo(stop, 360));
    console.log("supply", stop, supplyWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-1, -1);
    game.mapBuilder.generator.generateChunk(-1, -2);
    game.player.setPosition({ x: -16, y: 0, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const counselWalk = [];
  for (const stop of [
    { x: -16, z: -22 },
    { x: -16, z: -28.6 },
  ]) {
    counselWalk.push(await walkTo(stop, 360));
    console.log("counsel", stop, counselWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(1, -1);
    game.mapBuilder.generator.generateChunk(1, -2);
    game.player.setPosition({ x: 16, y: 0, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const staticWalk = [];
  for (const stop of [
    { x: 16, z: -22 },
    { x: 16, z: -28.6 },
  ]) {
    staticWalk.push(await walkTo(stop, 360));
    console.log("staticset", stop, staticWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(0, 1);
    game.mapBuilder.generator.generateChunk(1, 1);
    game.mapBuilder.generator.generateChunk(1, 2);
    game.player.setPosition({ x: 10, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const stairHallWalk = [];
  for (const stop of [
    { x: 10, z: 16 },
    { x: 16, z: 16 },
    { x: 16, z: 22 },
  ]) {
    stairHallWalk.push(await walkTo(stop, 360));
    console.log("stairhall", stop, stairHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(1, 1);
    game.player.setPosition({ x: 12.5, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const stairOffsetWalk = [];
  for (const stop of [
    { x: 12.5, z: 19.4 },
  ]) {
    stairOffsetWalk.push(await walkTo(stop, 220));
    console.log("stair_offset", stop, stairOffsetWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 0);
    game.mapBuilder.generator.generateChunk(2, 1);
    game.mapBuilder.generator.generateChunk(1, 1);
    game.player.setPosition({ x: 32, y: 0, z: 10 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const nurseryHallWalk = [];
  for (const stop of [
    { x: 32, z: 10 },
    { x: 32, z: 16 },
    { x: 26, z: 16 },
  ]) {
    nurseryHallWalk.push(await walkTo(stop, 360));
    console.log("nurseryhall", stop, nurseryHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-2, 0);
    game.mapBuilder.generator.generateChunk(-2, 1);
    game.mapBuilder.generator.generateChunk(-1, 1);
    game.player.setPosition({ x: -32, y: 0, z: 10 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const dollHallWalk = [];
  for (const stop of [
    { x: -32, z: 10 },
    { x: -32, z: 16 },
    { x: -26, z: 16 },
  ]) {
    dollHallWalk.push(await walkTo(stop, 360));
    console.log("dollhall", stop, dollHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-2, 0);
    game.mapBuilder.generator.generateChunk(-2, -1);
    game.player.setPosition({ x: -32, y: 0, z: -10 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const archiveHallWalk = [];
  for (const stop of [
    { x: -32, z: -10 },
    { x: -32, z: -16 },
    { x: -32, z: -22 },
  ]) {
    archiveHallWalk.push(await walkTo(stop, 360));
    console.log("archivehall", stop, archiveHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 0);
    game.mapBuilder.generator.generateChunk(2, -1);
    game.player.setPosition({ x: 32, y: 0, z: -10 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const storageHallWalk = [];
  for (const stop of [
    { x: 32, z: -10 },
    { x: 32, z: -16 },
    { x: 32, z: -22 },
  ]) {
    storageHallWalk.push(await walkTo(stop, 360));
    console.log("storagehall", stop, storageHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(0, 1);
    game.mapBuilder.generator.generateChunk(-1, 1);
    game.mapBuilder.generator.generateChunk(-1, 2);
    game.player.setPosition({ x: -10, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const teaHallWalk = [];
  for (const stop of [
    { x: -10, z: 16 },
    { x: -16, z: 16 },
    { x: -16, z: 22 },
  ]) {
    teaHallWalk.push(await walkTo(stop, 360));
    console.log("teahall", stop, teaHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 1);
    game.player.setPosition({ x: 32, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const nurseryRoomWalk = [];
  for (const stop of [
    { x: 32, z: 21.5 },
    { x: 26.6, z: 21.5 },
  ]) {
    nurseryRoomWalk.push(await walkTo(stop, 320));
    console.log("nursery_room", stop, nurseryRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: 32, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const nurseryPinchWalk = [];
  for (const stop of [
    { x: 34.8, z: 16 },
  ]) {
    nurseryPinchWalk.push(await walkTo(stop, 220));
    console.log("nursery_pinch", stop, nurseryPinchWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 1);
    game.player.setPosition({ x: 32, y: 0, z: 19.4 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const nurseryOffsetWalk = [];
  for (const stop of [
    { x: 29.2, z: 19.4 },
  ]) {
    nurseryOffsetWalk.push(await walkTo(stop, 220));
    console.log("nursery_offset", stop, nurseryOffsetWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-2, 1);
    game.player.setPosition({ x: -32, y: 0, z: 16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const dollRoomWalk = [];
  for (const stop of [
    { x: -32, z: 20.85 },
    { x: -37.2, z: 20.85 },
  ]) {
    dollRoomWalk.push(await walkTo(stop, 320));
    console.log("doll_room", stop, dollRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, -1);
    game.player.setPosition({ x: 32, y: 0, z: -16 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const storageRoomWalk = [];
  for (const stop of [
    { x: 32, z: -9.0 },
    { x: 26.6, z: -9.0 },
  ]) {
    storageRoomWalk.push(await walkTo(stop, 320));
    console.log("storage_room", stop, storageRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(5, -2);
    game.player.setPosition({ x: 80, y: 0, z: -32 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const labLinkRoomWalk = [];
  for (const stop of [
    { x: 84.95, z: -32 },
    { x: 84.95, z: -27.2 },
  ]) {
    labLinkRoomWalk.push(await walkTo(stop, 320));
    console.log("lablink_room", stop, labLinkRoomWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, 1);
    game.mapBuilder.generator.generateChunk(2, 2);
    game.player.setPosition({ x: 32, y: 0, z: 22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const dormWalk = [];
  for (const stop of [
    { x: 32, z: 22 },
    { x: 32, z: 26 },
    { x: 32, z: 30 },
  ]) {
    dormWalk.push(await walkTo(stop, 360));
    console.log("dorm", stop, dormWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-2, 1);
    game.mapBuilder.generator.generateChunk(-2, 2);
    game.mapBuilder.generator.generateChunk(-1, 2);
    game.player.setPosition({ x: -32, y: 0, z: 22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const dollClassWalk = [];
  for (const stop of [
    { x: -32, z: 22 },
    { x: -32, z: 32 },
    { x: -26, z: 32 },
  ]) {
    dollClassWalk.push(await walkTo(stop, 360));
    console.log("dollclass", stop, dollClassWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(2, -1);
    game.mapBuilder.generator.generateChunk(2, -2);
    game.mapBuilder.generator.generateChunk(1, -2);
    game.player.setPosition({ x: 32, y: 0, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const prepStoreWalk = [];
  for (const stop of [
    { x: 32, z: -22 },
    { x: 32, z: -32 },
    { x: 26, z: -32 },
  ]) {
    prepStoreWalk.push(await walkTo(stop, 360));
    console.log("prepstore", stop, prepStoreWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-2, -1);
    game.mapBuilder.generator.generateChunk(-2, -2);
    game.mapBuilder.generator.generateChunk(-1, -2);
    game.player.setPosition({ x: -32, y: 0, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const closedLibWalk = [];
  for (const stop of [
    { x: -32, z: -22 },
    { x: -32, z: -32 },
    { x: -26, z: -32 },
  ]) {
    closedLibWalk.push(await walkTo(stop, 360));
    console.log("closedlib", stop, closedLibWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-1, 1);
    game.mapBuilder.generator.generateChunk(-1, 2);
    game.player.setPosition({ x: -16, y: 0, z: 22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const etiquetteWalk = [];
  for (const stop of [
    { x: -16, z: 22 },
    { x: -16, z: 26 },
    { x: -16, z: 28.5 },
  ]) {
    etiquetteWalk.push(await walkTo(stop, 360));
    console.log("etiquette", stop, etiquetteWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(0, 1);
    game.mapBuilder.generator.generateChunk(0, 2);
    game.mapBuilder.generator.generateChunk(-1, 2);
    game.player.setPosition({ x: 0, y: 0, z: 22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const roofHallWalk = [];
  for (const stop of [
    { x: 0, z: 22 },
    { x: 0, z: 28 },
    { x: -6, z: 32 },
  ]) {
    roofHallWalk.push(await walkTo(stop, 360));
    console.log("roofhall", stop, roofHallWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-2, -1);
    game.mapBuilder.generator.generateChunk(-2, 0);
    game.mapBuilder.generator.generateChunk(-2, 1);
    game.player.setPosition({ x: -32, y: 0, z: -6 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const lostFoundWalk = [];
  for (const stop of [
    { x: -32, z: -6 },
    { x: -32, z: 0 },
    { x: -32, z: 6 },
  ]) {
    lostFoundWalk.push(await walkTo(stop, 360));
    console.log("lostfound", stop, lostFoundWalk.at(-1));
  }

  const rooms = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder.generator;
    const nurse = generator.generateChunk(5, -1);
    const music = generator.generateChunk(8, 2);
    const faculty = generator.generateChunk(6, 1);
    const science = generator.generateChunk(6, -2);
    const gate = generator.generateChunk(4, 0);
    const hall = generator.generateChunk(1, 0);
    const northHall = generator.generateChunk(0, -1);
    const uncatHall = generator.generateChunk(0, 1);
    const startHall = generator.generateChunk(0, 0);
    const b1 = generator.generateChunk(1, 2);
    const f2 = generator.generateChunk(-1, -1);
    const westHall = generator.generateChunk(-1, 0);
    const eastWing = generator.generateChunk(2, 0);
    const gym = generator.generateChunk(8, 0);
    const bridge = generator.generateChunk(3, 0);
    const yard = generator.generateChunk(5, 1);
    const memorial = generator.generateChunk(6, 0);
    const trophy = generator.generateChunk(7, 0);
    const arcade = generator.generateChunk(4, 1);
    const art = generator.generateChunk(8, -1);
    const foyer = generator.generateChunk(7, 1);
    const aud = generator.generateChunk(8, 1);
    const practice = generator.generateChunk(7, 2);
    const studio = generator.generateChunk(8, -2);
    const broadcast = generator.generateChunk(4, 2);
    const darkroom = generator.generateChunk(7, -2);
    const greenroom = generator.generateChunk(5, 2);
    const homeEc = generator.generateChunk(4, -2);
    const club = generator.generateChunk(7, -1);
    const specimen = generator.generateChunk(6, -1);
    const stageWing = generator.generateChunk(6, 2);
    const laundry = generator.generateChunk(4, -1);
    const labLink = generator.generateChunk(5, -2);
    const avRoom = generator.generateChunk(1, -1);
    const supply = generator.generateChunk(0, -2);
    const counsel = generator.generateChunk(-1, -2);
    const staticRoom = generator.generateChunk(1, -2);
    const stairHall = generator.generateChunk(1, 1);
    const nurseryHall = generator.generateChunk(2, 1);
    const dollHall = generator.generateChunk(-2, 1);
    const archiveHall = generator.generateChunk(-2, -1);
    const storageHall = generator.generateChunk(2, -1);
    const teaHall = generator.generateChunk(-1, 1);
    const dorm = generator.generateChunk(2, 2);
    const dollClass = generator.generateChunk(-2, 2);
    const prepStore = generator.generateChunk(2, -2);
    const closedLib = generator.generateChunk(-2, -2);
    const etiquette = generator.generateChunk(-1, 2);
    const roofHall = generator.generateChunk(0, 2);
    const lostFound = generator.generateChunk(-2, 0);
    const washFour = generator.generateChunk(5, 0);
    const names = (chunk) => (chunk.meshes || []).map((mesh) => String(mesh.name || ""));
    game.playTime = 12;
    game._lastPlayerChunkCx = 4;
    game._lastPlayerChunkCz = 0;
    game.onEnterSchoolChunk(4, 0);
    game.onEnterSchoolChunk(5, -1);
    game.onEnterSchoolChunk(8, 2);
    game.onEnterSchoolChunk(6, 1);
    game.onEnterSchoolChunk(6, -2);
    game.onEnterSchoolChunk(8, 0);
    game.onEnterSchoolChunk(3, 0);
    game.onEnterSchoolChunk(5, 1);
    game.onEnterSchoolChunk(6, 0);
    game.onEnterSchoolChunk(7, 0);
    game.onEnterSchoolChunk(4, 1);
    game.onEnterSchoolChunk(8, -1);
    game.onEnterSchoolChunk(7, 1);
    game.onEnterSchoolChunk(8, 1);
    game.onEnterSchoolChunk(7, 2);
    game.onEnterSchoolChunk(8, -2);
    game.onEnterSchoolChunk(4, 2);
    game.onEnterSchoolChunk(7, -2);
    game.onEnterSchoolChunk(5, 2);
    game.onEnterSchoolChunk(4, -2);
    game.onEnterSchoolChunk(7, -1);
    game.onEnterSchoolChunk(6, -1);
    game.onEnterSchoolChunk(6, 2);
    game.onEnterSchoolChunk(4, -1);
    game.onEnterSchoolChunk(5, -2);
    game.onEnterSchoolChunk(1, -1);
    game.onEnterSchoolChunk(0, -2);
    game.onEnterSchoolChunk(-1, -2);
    game.onEnterSchoolChunk(1, -2);
    game.onEnterSchoolChunk(1, 1);
    game.onEnterSchoolChunk(2, 1);
    game.onEnterSchoolChunk(-2, 1);
    game.onEnterSchoolChunk(-2, -1);
    game.onEnterSchoolChunk(2, -1);
    game.onEnterSchoolChunk(-1, 1);
    game.onEnterSchoolChunk(2, 2);
    game.onEnterSchoolChunk(-2, 2);
    game.onEnterSchoolChunk(2, -2);
    game.onEnterSchoolChunk(-2, -2);
    game.onEnterSchoolChunk(-1, 2);
    game.onEnterSchoolChunk(0, 2);
    game.onEnterSchoolChunk(-2, 0);
    game.onEnterSchoolChunk(2, 0);
    game.onEnterSchoolChunk(-1, 0);
    game.onEnterSchoolChunk(5, 0);
    game.onEnterSchoolChunk(1, 0);
    game.onEnterSchoolChunk(0, 1);
    game.onEnterSchoolChunk(0, -1);
    game.player.setPosition({ x: -20, y: 0, z: 0 });
    for (let i = 0; i < 6; i += 1) game.update(0.05, { skipRender: true });
    game.player.setPosition({ x: 32, y: 0, z: 0 });
    for (let i = 0; i < 6; i += 1) game.update(0.05, { skipRender: true });
    game.player.setPosition({ x: 0, y: 0, z: 16 });
    for (let i = 0; i < 6; i += 1) game.update(0.05, { skipRender: true });
    return {
      nurseType: nurse.type,
      musicType: music.type,
      facultyType: faculty.type,
      scienceType: science.type,
      annexSign: names(gate).some((name) => name.includes("annex_sign")),
      annexGateRack: names(gate).some((name) => name.includes("annexgate_rack_")),
      hallMaze: names(hall).filter((name) => name.includes("hall_maze_")).length,
      hallJog: names(hall).some((name) => name.includes("hall_maze_jog")),
      hallJogE: names(hall).some((name) => name.includes("hall_maze_jog_e")),
      hallJogNs: names(northHall).some((name) => name.includes("hall_maze_jog_ns")),
      hallJogNsS: names(northHall).some((name) => name.includes("hall_maze_jog_ns_s")),
      hallLoopN: names(hall).some((name) => name.includes("hall_maze_loop_n")),
      hallLoopW: names(northHall).some((name) => name.includes("hall_maze_loop_w")),
      hallRib: names(hall).some((name) => name.includes("hall_maze_rib") || name.includes("hall_maze_pocket")),
      northRib: names(northHall).some((name) => name.includes("hall_maze_rib") || name.includes("hall_maze_pocket")),
      hallLockers: names(hall).filter((name) => name.includes("hall_lockers_")).length,
      hallClass: names(hall).some((name) => name.includes("hall_class_")),
      hallClassCount: names(hall).filter((name) => name.includes("hall_class_")).length,
      hallStripe: names(hall).some((name) => name.includes("hall_stripe_")),
      hallWindow: names(hall).some((name) => name.includes("hall_window_")),
      hallDesk: names(hall).some((name) => name.includes("hall_desk_")),
      hallDeskCount: names(hall).filter((name) => name.includes("hall_desk_")).length,
      hallLino: names(hall).some((name) => name.includes("hall_lino_")),
      hallTeacher: names(hall).some((name) => name.includes("hall_teacher_")),
      hallPa: names(hall).some((name) => name.includes("hall_pa_")),
      hallClock: names(hall).some((name) => name.includes("hall_clock")),
      hallLibrary: names(westHall).some((name) => name.includes("hall_shelf_")),
      hallLibrarySpines: names(westHall).some((name) => name.includes("hall_books_")),
      hallWash: names(eastWing).some((name) => name.includes("hall_stall_") || name.includes("hall_sink_")),
      hallWashDoors: names(eastWing).some((name) => name.includes("hall_stall_door_")),
      hallBoarded: names(uncatHall).some((name) => name.includes("hall_class_") && name.includes("_board_")),
      hallNookSign: names(westHall).some((name) => name.includes("hall_sign_")),
      uncatSpineClear: !names(uncatHall).some((name) => name.includes("hall_maze_jog")),
      hallThrough: names(hall).some((name) => name.includes("hall_through_")),
      hallThroughDesk: names(hall).some((name) => name.includes("hall_through_desk_")),
      hallOuterWindow: names(eastWing).some((name) => name.includes("hall_outer_window_")),
      hallOuterFill: names(eastWing).some((name) => name.includes("hall_outer_fill_")),
      gymType: gym.type,
      gymCourt: names(gym).some((name) => name.includes("gym_court")),
      gymHoop: names(gym).some((name) => name.includes("gym_hoop_")),
      gymBleacher: names(gym).some((name) => name.includes("gym_bleacher_")),
      skyGrate: names(bridge).some((name) => name.includes("sky_grate_")),
      skyRib: names(bridge).some((name) => name.includes("sky_rib_")),
      skyBothWindows: names(bridge).some((name) => name.includes("hall_outer_window_n_"))
        && names(bridge).some((name) => name.includes("hall_outer_window_s_")),
      courtyardType: yard.type,
      courtyardWell: names(yard).some((name) => name.includes("courtyard_well")),
      courtyardRail: names(yard).some((name) => name.includes("courtyard_rail_")),
      courtyardTree: names(yard).some((name) => name.includes("courtyard_tree_")),
      memorialCase: names(memorial).some((name) => name.includes("memorial_case_")),
      memorialPortrait: names(memorial).some((name) => name.includes("memorial_portrait_")),
      memorialBothWindows: names(memorial).some((name) => name.includes("hall_outer_window_n_"))
        && names(memorial).some((name) => name.includes("hall_outer_window_s_")),
      trophyCup: names(trophy).some((name) => name.includes("trophy_case_") && name.includes("_cup")),
      trophyBanner: names(trophy).some((name) => name.includes("trophy_banner_")),
      trophyRoom: names(trophy).some((name) => name.includes("trophy_room_")),
      trophyRoomSw: names(trophy).some((name) => name.includes("trophy_room_sw")),
      trophyRoomL: names(trophy).some((name) => name.includes("trophy_room_sw_l_")),
      annexRoom: names(gate).some((name) => name.includes("annex_room_")),
      annexRoomL: names(gate).some((name) => name.includes("annex_room_ne_l_")),
      eastwashRoom: names(eastWing).some((name) => name.includes("eastwash_room_")),
      angelRoom: names(westHall).some((name) => name.includes("angel_room_")),
      washfourRoom: names(washFour).some((name) => name.includes("washfour_room_")),
      identityHallBare: !names(gate).some((name) => name.includes("hall_lockers_"))
        && !names(trophy).some((name) => name.includes("hall_lockers_"))
        && !names(eastWing).some((name) => name.includes("hall_lockers_"))
        && !names(laundry).some((name) => name.includes("hall_lockers_"))
        && !names(specimen).some((name) => name.includes("hall_lockers_"))
        && !names(nurseryHall).some((name) => name.includes("hall_lockers_"))
        && !names(dollHall).some((name) => name.includes("hall_lockers_"))
        && !names(archiveHall).some((name) => name.includes("hall_lockers_"))
        && !names(storageHall).some((name) => name.includes("hall_lockers_"))
        && !names(labLink).some((name) => name.includes("hall_lockers_"))
        && !names(teaHall).some((name) => name.includes("hall_lockers_"))
        && !names(arcade).some((name) => name.includes("hall_lockers_"))
        && !names(stageWing).some((name) => name.includes("hall_lockers_")),
      hallClear: {
        chase: generator.getHallClear(1, 0),
        cut: generator.getHallClear(2, 0),
        annex: generator.getHallClear(4, 0),
        trophy: generator.getHallClear(7, 0),
        laundry: generator.getHallClear(4, -1),
        nursery: generator.getHallClear(2, 1),
        doll: generator.getHallClear(-2, 1),
        archive: generator.getHallClear(-2, -1),
        storage: generator.getHallClear(2, -1),
        lab: generator.getHallClear(5, -2),
        tea: generator.getHallClear(-1, 1),
        arcade: generator.getHallClear(4, 1),
        stage: generator.getHallClear(6, 2),
      },
      hallDoor: {
        chase: generator.getHallDoorAlong(1, 0, "s"),
        cutWest: generator.getHallDoorAlong(2, 0, "n")[0],
        annex: generator.getHallDoorAlong(4, 0, "n"),
        nursery: generator.getHallDoorAlong(2, 1, "w"),
        doll: generator.getHallDoorAlong(-2, 1, "w"),
        lost: generator.getHallDoorAlong(-2, 0, "w"),
        stairN: generator.getHallDoorAlong(1, 1, "n"),
      },
      hallSides: {
        chase: generator.getHallSides(1, 0),
        cut: generator.getHallSides(2, 0),
        annex: generator.getHallSides(4, 0),
        nursery: generator.getHallSides(2, 1),
        memorial: generator.getHallSides(6, 0),
        sky: generator.getHallSides(3, 0),
        stair: generator.getHallSides(1, 1),
        lost: generator.getHallSides(-2, 0),
        practice: generator.getHallSides(7, 2),
        roof: generator.getHallSides(0, 2),
      },
      arcadeCol: names(arcade).some((name) => name.includes("arcade_col_")),
      arcadeBench: names(arcade).some((name) => name.includes("arcade_bench_")),
      arcadeRoom: names(arcade).some((name) => name.includes("arcade_room_")),
      artType: art.type,
      artEasel: names(art).some((name) => name.includes("art_easel_")),
      artTable: names(art).some((name) => name.includes("art_table")),
      foyerType: foyer.type,
      foyerBooth: names(foyer).some((name) => name.includes("foyer_booth_")),
      foyerCoat: names(foyer).some((name) => name.includes("foyer_coat_")),
      atriumRail: names(f2).some((name) => name.includes("atrium_rail_")),
      atriumWell: names(f2).some((name) => name.includes("atrium_well")),
      audType: aud.type,
      audStage: names(aud).some((name) => name.includes("auditorium_stage")),
      audSeat: names(aud).some((name) => name.includes("auditorium_seat_")),
      audCurtain: names(aud).some((name) => name.includes("auditorium_curtain")),
      practiceBaffle: names(practice).some((name) => name.includes("practice_baffle")),
      practiceStand: names(practice).some((name) => name.includes("practice_stand_")),
      studioBackdrop: names(studio).some((name) => name.includes("studio_backdrop")),
      studioSoftbox: names(studio).some((name) => name.includes("studio_softbox_")),
      studioType: studio.type,
      broadcastDesk: names(broadcast).some((name) => name.includes("broadcast_desk")),
      broadcastCrt: names(broadcast).some((name) => name.includes("broadcast_crt_")),
      broadcastType: broadcast.type,
      darkroomSink: names(darkroom).some((name) => name.includes("darkroom_sink_")),
      darkroomType: darkroom.type,
      greenroomSofa: names(greenroom).some((name) => name.includes("greenroom_sofa")),
      greenroomType: greenroom.type,
      homeEcMachine: names(homeEc).some((name) => name.includes("home_ec_machine_")),
      homeEcType: homeEc.type,
      clubTable: names(club).some((name) => name.includes("club_table_")),
      clubType: club.type,
      specimenCase: names(specimen).some((name) => name.includes("specimen_case_")),
      specimenRoom: names(specimen).some((name) => name.includes("specimen_room_")),
      laundryCart: names(laundry).some((name) => name.includes("laundry_cart_")),
      laundryRoom: names(laundry).some((name) => name.includes("laundry_room_")),
      stageRack: names(stageWing).some((name) => name.includes("stagewing_rack_")),
      labLinkCase: names(labLink).some((name) => name.includes("lablink_case_")),
      labLinkRoom: names(labLink).some((name) => name.includes("lablink_room_")),
      avCart: names(avRoom).some((name) => name.includes("av_cart_")),
      avType: avRoom.type,
      supplyCage: names(supply).some((name) => name.includes("supply_cage_")),
      supplyType: supply.type,
      counselAltar: names(counsel).some((name) => name.includes("omen_altar")),
      counselSign: names(counsel).some((name) => name.includes("counsel_sign")),
      counselType: counsel.type,
      staticSet: names(staticRoom).some((name) => name.includes("static_set")),
      staticType: staticRoom.type,
      stairHallCone: names(stairHall).some((name) => name.includes("stairhall_cone_")),
      nurseryHallCrib: names(nurseryHall).some((name) => name.includes("nurseryhall_crib_")),
      nurseryRoom: names(nurseryHall).some((name) => name.includes("nursery_room_")),
      dollHallShelf: names(dollHall).some((name) => name.includes("dollhall_shelf_")),
      dollRoom: names(dollHall).some((name) => name.includes("doll_room_")),
      archiveHallCase: names(archiveHall).some((name) => name.includes("archivehall_case_")),
      archiveRoom: names(archiveHall).some((name) => name.includes("archive_room_")),
      storageHallCrate: names(storageHall).some((name) => name.includes("storagehall_crate_")),
      storageRoom: names(storageHall).some((name) => name.includes("storage_room_")),
      teaHallBench: names(teaHall).some((name) => name.includes("teahall_bench_")),
      teaRoom: names(teaHall).some((name) => name.includes("tea_room_")),
      dormBunk: names(dorm).some((name) => name.includes("dorm_bunk_")),
      dormCrib: names(dorm).some((name) => name.includes("empty_crib")),
      dollClassChair: names(dollClass).some((name) => name.includes("dollclass_chair_")),
      prepStoreCrate: names(prepStore).some((name) => name.includes("prepstore_crate_")),
      closedLibTable: names(closedLib).some((name) => name.includes("closedlib_table")),
      closedLibDesk: names(closedLib).some((name) => name.includes("archive_desk")),
      etiquetteZabuton: names(etiquette).some((name) => name.includes("etiquette_zabuton_")),
      etiquetteTable: names(etiquette).some((name) => name.includes("tea_table")),
      roofHallBoard: names(roofHall).some((name) => name.includes("roofhall_board")),
      roofHallUnique: names(roofHall).some((name) => name.includes("roofhall_run_n"))
        && names(roofHall).some((name) => name.includes("roofhall_run_w")),
      lostFoundBox: names(lostFound).some((name) => name.includes("lostfound_box_")),
      startHallRack: names(startHall).some((name) => name.includes("starthall_rack_")),
      classWingCart: names(hall).some((name) => name.includes("classwing_cart_")),
      uncatHallTape: names(uncatHall).some((name) => name.includes("uncathall_tape_")),
      northHallCone: names(northHall).some((name) => name.includes("northhall_cone_")),
      northHallCubby: names(northHall).some((name) => name.includes("hall_cubby_")),
      b1BoilerDrum: names(b1).some((name) => name.includes("b1boiler_drum")),
      b1FloodDesk: names(b1).some((name) => name.includes("b1flood_desk_")),
      b1FloodBoard: names(b1).some((name) => name.includes("b1flood_board")),
      b1FloodDoor: names(b1).some((name) => name.includes("b1flood_door_")),
      b1FloodRoom: names(b1).some((name) => name.includes("b1_maze_class_")),
      f2BloodFrame: names(f2).some((name) => name.includes("f2blood_frame_")),
      f2BloodPortrait: names(f2).some((name) => name.includes("f2blood_portrait_")),
      f2GalleryFrame: names(f2).some((name) => name.includes("f2gallery_frame_")),
      f2GalleryRoom: names(f2).some((name) => name.includes("gallery_maze_room_")),
      eastWashBucket: names(eastWing).some((name) => name.includes("eastwash_bucket_")),
      angelHallCart: names(westHall).some((name) => name.includes("angelhall_cart")),
      washFourCubby: names(washFour).some((name) => name.includes("washfour_cubby_")),
      nurseBed: names(nurse).some((name) => name.includes("nurse_bed")),
      piano: names(music).some((name) => name.includes("piano")),
      facultyDesk: names(faculty).some((name) => name.includes("faculty_desk")),
      labBench: names(science).some((name) => name.includes("lab_bench")),
      b1Maze: names(b1).filter((name) => name.includes("b1_maze_")).length,
      f2Maze: names(f2).filter((name) => name.includes("gallery_maze_")).length,
      b1Lab: names(b1).some((name) => name.includes("cellar_b1_floor_south_lab")),
      b1EastLab: names(b1).some((name) => name.includes("cellar_b1_floor_east_lab")),
      f2Lab: names(f2).some((name) => name.includes("gallery_2f_floor_north_lab")),
      f2SouthLab: names(f2).some((name) => name.includes("gallery_2f_floor_south_lab")),
      b1Cabinets: (b1.cabinets || []).length,
      f2Cabinets: (f2.cabinets || []).length,
      beats: [...(game._storyBeats || [])],
      altarChildren: game.finalExit?.group?.children?.length || 0,
    };
  });

  const altarStill = await page.evaluate(() => {
    const game = window.__happyToy;
    const before = game.finalExit.group.rotation.y;
    game.finalExit.update(1.5);
    return {
      before,
      after: game.finalExit.group.rotation.y,
      label: game.finalExit.label,
    };
  });

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.mapBuilder.generator.generateChunk(1, 2);
    game.player.setPosition({ x: 16, y: -5, z: 36.5 });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
  });
  const b1Walk = [];
  for (const stop of [
    { x: 13.6, z: 38 },
    { x: 8.4, z: 38 },
    { x: 8.4, z: 33.25 },
    { x: 7.2, z: 30.0 },
    { x: 1.2, z: 30.0 },
    { x: -0.5, z: 30.0 },
    { x: -3.5, z: 29.4 },
  ]) {
    b1Walk.push(await walkTo(stop, 280));
    console.log("b1", stop, b1Walk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: 8.4, y: -5, z: 38 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const b1DeepWalk = [];
  for (const stop of [
    { x: 8.4, z: 41.8 },
    { x: 8.4, z: 48.0 },
    { x: 5.8, z: 47.25 },
    { x: 8.4, z: 48.0 },
    { x: 8.4, z: 53.4 },
  ]) {
    b1DeepWalk.push(await walkTo(stop, 320));
    console.log("b1deep", stop, b1DeepWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: 20.5, y: -5, z: 38 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const b1EastWalk = [];
  for (const stop of [
    { x: 26.2, z: 38 },
    { x: 32, z: 38 },
    { x: 32, z: 48 },
  ]) {
    b1EastWalk.push(await walkTo(stop, 320));
    console.log("b1east", stop, b1EastWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.mapBuilder.generator.generateChunk(-1, -1);
    for (const door of game.doors || []) {
      if (door.id === "door-stairs-2f-gallery") {
        door.isOpen = true;
        door.openAmount = 1;
        door.update(0.3);
      }
    }
    game.player.setPosition({ x: -16, y: 5, z: -21.8 });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
  });
  const f2Walk = [];
  for (const stop of [
    { x: -18.8, z: -21.8 },
    { x: -22.5, z: -22 },
    { x: -27.5, z: -22 },
    { x: -28.6, z: -22 },
  ]) {
    f2Walk.push(await walkTo(stop, 280));
    console.log("f2", stop, f2Walk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: -22.5, y: 5, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const f2DeepWalk = [];
  for (const stop of [
    { x: -22.5, z: -34.0 },
    { x: -22.5, z: -38.4 },
    { x: -32.5, z: -42.0 },
    { x: -32.5, z: -45.0 },
    { x: -35.2, z: -45.2 },
    { x: -32.5, z: -45.0 },
    { x: -32.5, z: -50.0 },
  ]) {
    f2DeepWalk.push(await walkTo(stop, 360));
    console.log("f2deep", stop, f2DeepWalk.at(-1));
  }

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    game.player.setPosition({ x: -22.5, y: 5, z: -22 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const f2SouthWalk = [];
  for (const stop of [
    { x: -22.5, z: -18.0 },
    { x: -23.4, z: -12.2 },
    { x: -22.5, z: -8.8 },
    { x: -22.5, z: -6.0 },
    { x: -22.5, z: 2.0 },
    { x: -22.5, z: 6.0 },
  ]) {
    f2SouthWalk.push(await walkTo(stop, 360));
    console.log("f2south", stop, f2SouthWalk.at(-1));
  }

  const story = await page.evaluate(() => {
    const game = window.__happyToy;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    return {
      beats: [...(game._storyBeats || [])],
      fired: [...(game.storyDirector?.fired || [])],
      y: game.player.position.y,
      x: game.player.position.x,
    };
  });

  const loop = await page.evaluate(() => {
    const game = window.__happyToy;
    game.ghostMode = true;
    game.testSafeMode = false;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    const load = (cx, cz) => {
      game.mapBuilder.generator.generateChunk(cx, cz);
      game.player.setPosition({
        x: cx * 16,
        y: cx === 1 && cz === 2 ? -5 : cx === -1 && cz === -1 ? 5 : 0,
        z: cz * 16,
      });
      for (let i = 0; i < 12; i += 1) game.update(0.05, { skipRender: true });
    };
    load(2, -2);
    load(-2, 2);
    load(1, 2);
    load(-1, -1);
    game.revealKeyById("key-hwacat");
    const collected = [];
    for (const id of ["key-storage", "key-playroom", "key-workshop", "key-hwacat"]) {
      let key = game.keys.find((item) => item.id === id && !item.isCollected);
      if (!key) {
        game.syncKeyHomes?.();
        key = game.keys.find((item) => item.id === id && !item.isCollected);
      }
      if (!key) {
        collected.push({ id, ok: false, reason: "missing" });
        continue;
      }
      if (key.initiallyVisible === false && !key.group.visible) {
        key.revealAt?.(key.position);
      }
      game.player.setPosition({
        x: key.position.x,
        y: key.position.y,
        z: key.position.z + 1.35,
      });
      for (let i = 0; i < 16; i += 1) {
        const dx = key.position.x - game.player.position.x;
        const dz = key.position.z - game.player.position.z;
        game.player.yaw = Math.atan2(-dx, -dz);
        game.input.keys.add("w");
        game.update(0.05, { skipRender: true });
      }
      game.input.keys.delete("w");
      if (!key.isCollected) game.collectKey(key);
      collected.push({ id, ok: key.isCollected === true, count: game.keyCount });
    }

    const cabinet = (game.cabinets || []).find((item) => Math.abs((item.position?.y || 0)) < 1.5) || game.cabinets[0];
    game.player.setPosition({
      x: cabinet.position.x,
      y: cabinet.position.y,
      z: cabinet.position.z + 0.9,
    });
    game.enterCabinet(cabinet, { forceOutcome: "safe" });
    const hiding = {
      hidden: game.player.isHidden,
      cameraY: game.camera.position.y,
      slats: document.body.classList.contains("is-hidden"),
      interior: cabinet.interiorActive === true || cabinet.interiorGroup?.visible === true,
    };
    game.exitCabinet();

    game.player.setPosition({ x: 0.15, y: 0, z: 0.55 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
    game.tryClearFinal();
    for (let i = 0; i < 130; i += 1) game.update(0.05, { skipRender: true });
    return {
      collected,
      keyCount: game.keyCount,
      total: game.getTotalKeys(),
      hiding,
      ritual: game.dreadDirector.ritualActive,
      progress: game.dreadDirector.ritualProgress,
      cleared: game.gameCleared === true,
      hud: document.querySelector("#key-count-text")?.textContent,
    };
  });

  console.log({ annexWalk, loopWalk, ringWalk, longRingWalk, northWalk, nsLoopWalk, f1MazeWalk, throughWalk, gymWalk, trophyWalk, trophySwWalk, trophyWideWalk, annexNorthWalk, annexRoomWalk, annexPinchWalk, annexOffsetWalk, eastwashRoomWalk, washfourRoomWalk, angelRoomWalk, windowBlock, yardWalk, arcadeWalk, artWalk, memorialWalk, memorialOffsetWalk, foyerWalk, audWalk, practiceBlock, practiceWalk, studioWalk, broadcastWalk, darkroomWalk, greenroomWalk, homeEcWalk, clubWalk, specimenWalk, stageWalk, laundryWalk, laundryRoomWalk, specimenRoomWalk, labLinkWalk, avWalk, supplyWalk, counselWalk, staticWalk, stairHallWalk, stairOffsetWalk, nurseryHallWalk, dollHallWalk, archiveHallWalk, storageHallWalk, teaHallWalk, nurseryRoomWalk, nurseryPinchWalk, nurseryOffsetWalk, dollRoomWalk, storageRoomWalk, labLinkRoomWalk, dormWalk, dollClassWalk, prepStoreWalk, closedLibWalk, etiquetteWalk, roofHallWalk, lostFoundWalk, rooms, altarStill, b1Walk, b1DeepWalk, b1EastWalk, f2Walk, f2DeepWalk, f2SouthWalk, story, loop });
  assert.equal(errors.length, 0, `page errors: ${errors.join(" | ")}`);
  assert.ok(annexWalk[0].x > 8, `must leave the start hall east, got ${JSON.stringify(annexWalk[0])}`);
  assert.ok(annexWalk.some((stop) => stop.z > 4.0 && stop.x < 13), "east hall south alcove locker must be walkable");
  assert.ok(annexWalk.some((stop) => Math.abs(stop.z) < 1.2 && stop.x > 30), "east school must keep a z=0 spine through the next tile");
  assert.ok(loopWalk.some((stop) => stop.z < -4.0 && stop.x < 17), "east tile north T-spur must be walkable");
  assert.ok(loopWalk.some((stop) => stop.z > 4.0 && stop.x > 17), "east tile south alcove must be walkable");
  assert.equal(loopWalk.at(-1).ok, true, `must finish the 1F hall alcoves, got ${JSON.stringify(loopWalk.at(-1))}`);
  assert.ok(ringWalk[1]?.ok && Math.abs(ringWalk[1].z) < 1.2, `center hall must cross the tile at z=0, got ${JSON.stringify(ringWalk[1])}`);
  assert.equal(ringWalk.at(-1).ok, true, `must walk the 1F school hall, got ${JSON.stringify(ringWalk.at(-1))}`);
  assert.equal(longRingWalk.at(-1).ok, true, `center hall must continue into the next maze tile, got ${JSON.stringify(longRingWalk.at(-1))}`);
  assert.ok(longRingWalk.at(-1).x > 30 && Math.abs(longRingWalk.at(-1).z) < 1.2, "long hall must stay on z=0 into chunk (2,0)");
  const annexGate = annexWalk.find((stop) => stop.x > 56 && Math.abs(stop.z) < 1.2);
  assert.ok(annexGate?.ok, `must walk to 별관 gate, got ${JSON.stringify(annexGate)}`);
  assert.equal(annexWalk.at(-1).ok, true, `must walk into 보건실, got ${JSON.stringify(annexWalk.at(-1))}`);
  assert.ok(annexWalk.at(-1).z < -10, "보건실 is on the north wing of the annex spine");
  assert.equal(f1MazeWalk.at(-1).ok, true, `must walk a 1F hall alcove, got ${JSON.stringify(f1MazeWalk.at(-1))}`);
  assert.ok(f1MazeWalk.at(-1).z > 4, "1F hall continues into the south hide alcove");
  assert.equal(rooms.hallThrough, true, "maze halls must open classroom cut-throughs into the next hall");
  assert.equal(throughWalk.at(-1).ok, true, `must cut through a classroom into the next hall, got ${JSON.stringify(throughWalk.at(-1))}`);
  assert.ok(throughWalk.at(-1).x > 30 && Math.abs(throughWalk.at(-1).z) < 1.2, "classroom cut-through must return to the z=0 spine");
  assert.ok(throughWalk.some((stop) => stop.z < -5.5 && stop.x > 22 && stop.x < 26), "cut-through must leave the corridor and pass the tile seam");
  assert.equal(rooms.hallThroughDesk, true, "classroom cut-throughs must keep desks beside the back door");
  assert.ok(story.fired.includes("throughClass"), `throughClass VO missing: ${story.fired.join(",")}`);
  assert.equal(gymWalk.at(-1).ok, true, `must walk into the annex gymnasium, got ${JSON.stringify(gymWalk.at(-1))}`);
  assert.ok(gymWalk.at(-1).x > 126 && Math.abs(gymWalk.at(-1).z) < 1.4, "gymnasium court center must stay walkable");
  assert.ok(gymWalk.some((stop) => stop.z > 5 && Math.abs(stop.x - 112) < 1.6), "trophy hall T-spur south to the foyer must stay open");
  assert.equal(rooms.trophyCup, true, "trophy hall must show cups");
  assert.equal(rooms.trophyBanner, true, "trophy hall must hang banners");
  assert.equal(rooms.trophyRoom, true, "trophy hall must close a SE classroom volume");
  assert.equal(rooms.trophyRoomSw, true, "trophy hall must close a SW classroom volume");
  assert.ok(rooms.beats.includes("trophy"), "trophy VO beat missing");
  assert.equal(trophyWalk.at(-1).ok, true, `must walk into the trophy SE room, got ${JSON.stringify(trophyWalk.at(-1))}`);
  assert.ok(trophyWalk.at(-1).z > 3 && trophyWalk.at(-1).x > 115, "trophy SE room must be enterable off the spine");
  assert.equal(trophySwWalk.at(-1).ok, true, `must walk into the trophy SW room, got ${JSON.stringify(trophySwWalk.at(-1))}`);
  assert.ok(trophySwWalk.at(-1).z > 3 && trophySwWalk.at(-1).x < 108.2, "trophy SW room must be enterable off the spine");
  assert.equal(rooms.annexRoom, true, "annex gate must close north classroom volumes");
  assert.equal(annexNorthWalk.at(-1).ok, true, `annex north T-spur must stay open, got ${JSON.stringify(annexNorthWalk.at(-1))}`);
  assert.ok(annexNorthWalk.at(-1).z < -5 && Math.abs(annexNorthWalk.at(-1).x - 64) < 1.6, "annex north T-spur to laundry must stay open");
  assert.equal(annexRoomWalk.at(-1).ok, true, `must walk into the annex NE room, got ${JSON.stringify(annexRoomWalk.at(-1))}`);
  assert.ok(annexRoomWalk.at(-1).x > 67.8 && annexRoomWalk.at(-1).z < -2.4, "annex NE room must be enterable off the spine");
  assert.equal(rooms.eastwashRoom, true, "east wash hall must close the NE classroom volume");
  assert.equal(eastwashRoomWalk.at(-1).ok, true, `must walk into the east wash NE room, got ${JSON.stringify(eastwashRoomWalk.at(-1))}`);
  assert.ok(eastwashRoomWalk.at(-1).x > 35.8 && eastwashRoomWalk.at(-1).z < -2.4, "east wash NE room must be enterable off the spine");
  assert.equal(rooms.washfourRoom, true, "wash four hall must close the NE classroom volume");
  assert.equal(washfourRoomWalk.at(-1).ok, true, `must walk into the wash four NE room, got ${JSON.stringify(washfourRoomWalk.at(-1))}`);
  assert.ok(washfourRoomWalk.at(-1).x > 83.8 && washfourRoomWalk.at(-1).z < -2.4, "wash four NE room must be enterable off the spine");
  assert.equal(rooms.angelRoom, true, "angel hall must close the NE classroom volume");
  assert.equal(angelRoomWalk.at(-1).ok, true, `must walk into the angel NE room, got ${JSON.stringify(angelRoomWalk.at(-1))}`);
  assert.ok(angelRoomWalk.at(-1).x > -12.2 && angelRoomWalk.at(-1).z < -2.4, "angel NE room must be enterable off the spine");
  assert.equal(rooms.identityHallBare, true, "closed-room identity halls must not reuse the shared locker banks");
  assert.equal(rooms.hallClear.chase, 1.7, "chase halls must stay 3.4m");
  assert.equal(rooms.hallClear.cut, 1.7, "east-wash cut-through hall must stay 3.4m");
  assert.ok(rooms.hallClear.annex < 1.35, "annex gate must pinch narrower than a 3.4m copy");
  assert.ok(rooms.hallClear.trophy > 2.05, "trophy hall must open wider than a 3.4m copy");
  assert.ok(rooms.hallClear.laundry < 1.35, "laundry hall must pinch narrower than a 3.4m copy");
  assert.ok(rooms.hallClear.nursery < 1.4, "nursery hall must pinch narrower than a 3.4m copy");
  assert.ok(rooms.hallClear.archive < 1.3, "archive hall must pinch narrower than a 3.4m copy");
  assert.ok(rooms.hallClear.stage > 1.8, "stage wing must open wider than a 3.4m copy");
  assert.deepEqual(rooms.hallDoor.chase, [-5.25, 5.25], "chase hide aisles must stay at ±5.25");
  assert.equal(rooms.hallDoor.cutWest, -5.25, "east-wash n-west door must stay on the cut-through aisle");
  assert.notDeepEqual(rooms.hallDoor.nursery, [-5.25, 5.25], "nursery west doors must leave the copied ±5.25 rhythm");
  assert.notDeepEqual(rooms.hallDoor.annex, [-5.25, 5.25], "annex north doors must leave the copied ±5.25 rhythm");
  assert.notDeepEqual(rooms.hallDoor.lost, [-5.25, 5.25], "lost-and-found west doors must leave the copied ±5.25 rhythm");
  assert.notDeepEqual(rooms.hallDoor.stairN, [-5.25, 5.25], "stair hall north doors must leave the copied ±5.25 rhythm");
  assert.equal(rooms.hallSides.chase.n, rooms.hallSides.chase.s, "chase halls stay a centered 3.4m");
  assert.equal(rooms.hallSides.cut.n, rooms.hallSides.cut.s, "east-wash cut-through stays a centered 3.4m");
  assert.notEqual(rooms.hallSides.annex.n, rooms.hallSides.annex.s, "annex gate must offset north vs south walls");
  assert.notEqual(rooms.hallSides.nursery.e, rooms.hallSides.nursery.w, "nursery hall must offset east vs west walls");
  assert.notEqual(rooms.hallSides.memorial.n, rooms.hallSides.memorial.s, "memorial hall must offset the glass walls");
  assert.notEqual(rooms.hallSides.sky.n, rooms.hallSides.sky.s, "skybridge must offset the glass walls");
  assert.notEqual(rooms.hallSides.stair.n, rooms.hallSides.stair.s, "stair plus must offset north vs south");
  assert.notEqual(rooms.hallSides.stair.e, rooms.hallSides.stair.w, "stair plus must offset east vs west");
  assert.notEqual(rooms.hallSides.practice.n, rooms.hallSides.practice.s, "practice hall must offset north vs south walls");
  assert.notEqual(rooms.hallSides.roof.n, rooms.hallSides.roof.s, "roof hall must offset north vs south walls");
  assert.notEqual(rooms.hallSides.roof.e, rooms.hallSides.roof.w, "roof hall must offset east vs west walls");
  assert.notEqual(rooms.hallSides.roof.n, 1.7, "roof hall north wall must leave the copied ±1.7 plus");
  assert.notEqual(rooms.hallSides.roof.e, 1.7, "roof hall east wall must leave the copied ±1.7 plus");
  assert.equal(rooms.annexRoomL, true, "annex NE classroom must break the copied rectangle with an L jog");
  assert.equal(rooms.trophyRoomL, true, "trophy SW classroom must break the copied rectangle with an L jog");
  assert.equal(annexOffsetWalk.at(-1).ok, true, `annex north offset must walk past the old 1.22m wall, got ${JSON.stringify(annexOffsetWalk.at(-1))}`);
  assert.ok(annexOffsetWalk.at(-1).z < -0.95 && annexOffsetWalk.at(-1).z > -1.45, "annex offset north wall must reach z≈-1 where the old pinch would stop");
  assert.equal(memorialOffsetWalk.at(-1).ok, true, `memorial south offset must walk past a 3.4m wall, got ${JSON.stringify(memorialOffsetWalk.at(-1))}`);
  assert.ok(memorialOffsetWalk.at(-1).z > 1.5 && memorialOffsetWalk.at(-1).z < 2.05, "memorial offset south wall must reach z≈1.7 where a copied 3.4m wall would stop");
  assert.equal(stairOffsetWalk.at(-1).ok, true, `stair plus south offset must walk past a 3.4m wall, got ${JSON.stringify(stairOffsetWalk.at(-1))}`);
  assert.ok(
    stairOffsetWalk.at(-1).z > 17.42 && stairOffsetWalk.at(-1).z < 18.2 && Math.abs(stairOffsetWalk.at(-1).x - 12.5) < 0.9,
    "stair offset south wall must reach z≈17.5 where a copied plus would stop",
  );
  assert.equal(nurseryOffsetWalk.at(-1).ok, true, `nursery west offset must walk past the old 1.28m wall, got ${JSON.stringify(nurseryOffsetWalk.at(-1))}`);
  assert.ok(nurseryOffsetWalk.at(-1).x < 31.05 && nurseryOffsetWalk.at(-1).x > 30.35, "nursery offset west wall must reach x≈30.9 where the old pinch would stop");
  assert.equal(rooms.arcadeRoom, true, "arcade must close west classroom volumes");
  assert.equal(trophyWideWalk.at(-1).ok, true, `trophy hall must walk off-spine inside the wide clear, got ${JSON.stringify(trophyWideWalk.at(-1))}`);
  assert.ok(trophyWideWalk.at(-1).z > 1.65 && trophyWideWalk.at(-1).x > 113.8, "trophy wide clear must reach z≈2 where a 3.4m wall would stop");
  assert.ok(
    annexPinchWalk.at(-1).ok !== true || Math.abs(annexPinchWalk.at(-1).z) < 1.28,
    `annex pinch must block the old 3.4m south classroom, got ${JSON.stringify(annexPinchWalk.at(-1))}`,
  );
  assert.equal(rooms.arcadeCol, true, "courtyard arcade must have columns");
  assert.equal(rooms.arcadeBench, true, "courtyard arcade must have benches");
  assert.ok(rooms.beats.includes("arcade"), "arcade VO beat missing");
  assert.equal(arcadeWalk.at(-1).ok, true, `must walk the arcade, got ${JSON.stringify(arcadeWalk.at(-1))}`);
  assert.ok(arcadeWalk.some((stop) => stop.x > 68 && Math.abs(stop.z - 16) < 1.6), "arcade east T-spur to the courtyard must stay open");
  assert.equal(rooms.artType, "art_room");
  assert.equal(rooms.artEasel, true, "art room must have easels");
  assert.equal(rooms.artTable, true, "art room must have a paint table");
  assert.ok(rooms.beats.includes("art_room"), "art room VO beat missing");
  assert.equal(artWalk.at(-1).ok, true, `must walk into the art room, got ${JSON.stringify(artWalk.at(-1))}`);
  assert.ok(artWalk.at(-1).z > -18 && Math.abs(artWalk.at(-1).x - 128) < 1.4, "art room interior must stay walkable");
  assert.equal(rooms.gymType, "gymnasium");
  assert.equal(rooms.gymCourt, true, "gymnasium must have a marked court");
  assert.equal(rooms.gymHoop, true, "gymnasium must have a hoop");
  assert.equal(rooms.gymBleacher, true, "gymnasium must have bleachers");
  assert.ok(rooms.beats.includes("gymnasium"));
  assert.equal(rooms.hallOuterWindow, true, "east maze hall must open a window wall instead of a plus classroom");
  assert.equal(rooms.hallOuterFill, true, "window-wall halls must fill the omitted classroom volume");
  assert.ok(
    windowBlock.ok !== true || Math.abs(windowBlock.z) < 2.4,
    `window wall must block the old south classroom, got ${JSON.stringify(windowBlock)}`,
  );
  assert.equal(rooms.skyGrate, true, "skybridge must have a metal grate floor");
  assert.equal(rooms.skyRib, true, "skybridge must have window ribs");
  assert.equal(rooms.skyBothWindows, true, "connector hall must open windows on both long walls");
  assert.ok(rooms.beats.includes("skybridge"), "skybridge VO beat missing");
  assert.ok(story.fired.includes("skybridge") || annexWalk.some((stop) => stop.x > 44 && stop.x < 52), "skybridge story should fire on the connector");
  assert.equal(rooms.courtyardType, "courtyard");
  assert.equal(rooms.courtyardWell, true, "courtyard must keep a blocked light well");
  assert.equal(rooms.courtyardRail, true, "courtyard must have rails around the well");
  assert.equal(rooms.courtyardTree, true, "courtyard well must hold a tree");
  assert.ok(rooms.beats.includes("courtyard"), "courtyard VO beat missing");
  assert.equal(yardWalk.at(-1).ok, true, `must walk the courtyard ring, got ${JSON.stringify(yardWalk.at(-1))}`);
  assert.ok(yardWalk.some((stop) => stop.x > 83 && stop.z > 14 && stop.z < 18), "courtyard walk must go around the well, not through it");
  assert.ok(story.fired.includes("courtyard") || yardWalk.at(-1).ok, "courtyard story should fire on the ring");
  assert.equal(rooms.memorialCase, true, "memorial hall must show trophy cases");
  assert.equal(rooms.memorialPortrait, true, "memorial hall must hang empty portraits");
  assert.equal(rooms.memorialBothWindows, true, "memorial hall must keep windows on both long walls");
  assert.ok(rooms.beats.includes("memorial"), "memorial VO beat missing");
  assert.equal(memorialWalk.at(-1).ok, true, `must walk the memorial spine, got ${JSON.stringify(memorialWalk.at(-1))}`);
  assert.ok(memorialWalk.some((stop) => stop.z > 5 && Math.abs(stop.x - 96) < 1.6), "memorial T-spur south must stay open");
  assert.ok(memorialWalk.at(-1).x > 100 && Math.abs(memorialWalk.at(-1).z) < 1.2, "memorial spine must continue east");
  assert.equal(rooms.foyerType, "foyer");
  assert.equal(rooms.foyerBooth, true, "foyer must have ticket booths");
  assert.equal(rooms.foyerCoat, true, "foyer must have coat racks");
  assert.ok(rooms.beats.includes("foyer"), "foyer VO beat missing");
  assert.equal(foyerWalk.at(-1).ok, true, `must walk the foyer aisle, got ${JSON.stringify(foyerWalk.at(-1))}`);
  assert.ok(foyerWalk.some((stop) => stop.z < 12 && Math.abs(stop.x - 112) < 1.6), "foyer north T-spur to the gym hall must stay open");
  assert.ok(foyerWalk.at(-1).x > 116 && Math.abs(foyerWalk.at(-1).z - 16) < 1.2, "foyer aisle must continue east to the auditorium");
  assert.equal(rooms.atriumRail, true, "2F stair well must have look-down rails");
  assert.equal(rooms.atriumWell, true, "2F stair well must keep a visible drop");
  assert.equal(rooms.audType, "auditorium");
  assert.equal(rooms.audStage, true, "auditorium must have a stage");
  assert.equal(rooms.audSeat, true, "auditorium must have raked seats");
  assert.equal(rooms.audCurtain, true, "auditorium must have a dropped curtain");
  assert.ok(rooms.beats.includes("auditorium"), "auditorium VO beat missing");
  assert.equal(audWalk.at(-1).ok, true, `must walk the auditorium aisle, got ${JSON.stringify(audWalk.at(-1))}`);
  assert.ok(audWalk.at(-1).x > 126 && Math.abs(audWalk.at(-1).z - 16) < 1.4, "auditorium center aisle must stay walkable");
  assert.equal(rooms.practiceBaffle, true, "practice hall must block the tile center");
  assert.equal(rooms.practiceStand, true, "practice hall must show music stands");
  assert.ok(rooms.beats.includes("practice"), "practice VO beat missing");
  assert.ok(practiceBlock.blocked === true, `practice baffle must occupy tile center, got ${JSON.stringify(practiceBlock)}`);
  assert.ok(practiceBlock.los === false, `practice baffle must break the z=0 sightline, got ${JSON.stringify(practiceBlock)}`);
  assert.ok(
    practiceBlock.ok !== true && practiceBlock.x < 110,
    `practice baffle must stop a short east walk, got ${JSON.stringify(practiceBlock)}`,
  );
  assert.equal(practiceWalk.at(-1).ok, true, `must walk the practice dogleg into the music room, got ${JSON.stringify(practiceWalk.at(-1))}`);
  assert.ok(practiceWalk.some((stop) => stop.z > 34 && Math.abs(stop.x - 112) < 1.6), "practice bypass must leave the blocked spine");
  assert.ok(practiceWalk.at(-1).x > 126 && Math.abs(practiceWalk.at(-1).z - 32) < 1.4, "practice dogleg must reach the music room");
  assert.equal(rooms.studioType, "studio");
  assert.equal(rooms.studioBackdrop, true, "studio must hang a backdrop");
  assert.equal(rooms.studioSoftbox, true, "studio must show soft lights");
  assert.ok(rooms.beats.includes("studio"), "studio VO beat missing");
  assert.equal(studioWalk.at(-1).ok, true, `must walk the studio aisle, got ${JSON.stringify(studioWalk.at(-1))}`);
  assert.ok(studioWalk.some((stop) => stop.x < 124 && Math.abs(stop.z + 32) < 1.6), "studio west door aisle must stay open");
  assert.ok(studioWalk.at(-1).z > -28 && Math.abs(studioWalk.at(-1).x - 128) < 1.4, "studio south aisle to the art room must stay open");
  assert.equal(rooms.broadcastType, "broadcast");
  assert.equal(rooms.broadcastDesk, true, "broadcast room must have a mixing desk");
  assert.equal(rooms.broadcastCrt, true, "broadcast room must show CRTs");
  assert.ok(rooms.beats.includes("broadcast"), "broadcast VO beat missing");
  assert.equal(broadcastWalk.at(-1).ok, true, `must walk the broadcast room, got ${JSON.stringify(broadcastWalk.at(-1))}`);
  assert.ok(broadcastWalk.some((stop) => stop.z > 30 && Math.abs(stop.x - 64) < 1.6), "broadcast north aisle from the arcade must stay open");
  assert.ok(broadcastWalk.at(-1).x > 68 && Math.abs(broadcastWalk.at(-1).z - 32) < 1.4, "broadcast east aisle must stay open");
  assert.equal(rooms.darkroomType, "darkroom");
  assert.equal(rooms.darkroomSink, true, "darkroom must have developing sinks");
  assert.ok(rooms.beats.includes("darkroom"), "darkroom VO beat missing");
  assert.equal(darkroomWalk.at(-1).ok, true, `must walk the darkroom aisle, got ${JSON.stringify(darkroomWalk.at(-1))}`);
  assert.ok(darkroomWalk.at(-1).x < 108 && Math.abs(darkroomWalk.at(-1).z + 32) < 1.4, "darkroom west aisle must stay open");
  assert.equal(rooms.greenroomType, "greenroom");
  assert.equal(rooms.greenroomSofa, true, "greenroom must have a sofa");
  assert.ok(rooms.beats.includes("greenroom"), "greenroom VO beat missing");
  assert.equal(greenroomWalk.at(-1).ok, true, `must walk the greenroom aisle, got ${JSON.stringify(greenroomWalk.at(-1))}`);
  assert.ok(greenroomWalk.some((stop) => stop.x > 78 && Math.abs(stop.z - 32) < 1.6), "greenroom east aisle from broadcast must stay open");
  assert.ok(greenroomWalk.at(-1).z < 28 && Math.abs(greenroomWalk.at(-1).x - 80) < 1.4, "greenroom north aisle to the courtyard must stay open");
  assert.equal(rooms.homeEcType, "home_ec");
  assert.equal(rooms.homeEcMachine, true, "home-ec room must have sewing machines");
  assert.ok(rooms.beats.includes("home_ec"), "home-ec VO beat missing");
  assert.equal(homeEcWalk.at(-1).ok, true, `must walk the home-ec room, got ${JSON.stringify(homeEcWalk.at(-1))}`);
  assert.ok(homeEcWalk.some((stop) => stop.z < -30 && Math.abs(stop.x - 64) < 1.6), "home-ec north aisle must stay open");
  assert.ok(homeEcWalk.at(-1).x > 68 && Math.abs(homeEcWalk.at(-1).z + 32) < 1.4, "home-ec east aisle must stay open");
  assert.equal(rooms.clubType, "club_room");
  assert.equal(rooms.clubTable, true, "club room must have calligraphy tables");
  assert.ok(rooms.beats.includes("club_room"), "club room VO beat missing");
  assert.equal(clubWalk.at(-1).ok, true, `must walk the club room, got ${JSON.stringify(clubWalk.at(-1))}`);
  assert.ok(clubWalk.some((stop) => stop.z < -14 && Math.abs(stop.x - 112) < 1.6), "club south aisle from the trophy hall must stay open");
  assert.ok(clubWalk.at(-1).x < 108 && Math.abs(clubWalk.at(-1).z + 16) < 1.4, "club west aisle must stay open");
  assert.equal(rooms.specimenCase, true, "specimen hall must show west specimen cases");
  assert.equal(rooms.specimenRoom, true, "specimen hall must close west classroom volumes");
  assert.ok(rooms.beats.includes("specimen"), "specimen VO beat missing");
  assert.equal(specimenWalk.at(-1).ok, true, `must walk the specimen hall, got ${JSON.stringify(specimenWalk.at(-1))}`);
  assert.ok(specimenWalk.some((stop) => stop.z < -14 && Math.abs(stop.x - 96) < 1.6), "specimen NS spine must stay open");
  assert.ok(specimenWalk.at(-1).x > 100 && Math.abs(specimenWalk.at(-1).z + 16) < 1.4, "specimen east T-spur to the club must stay open");
  assert.equal(specimenRoomWalk.at(-1).ok, true, `must walk into the specimen NW room, got ${JSON.stringify(specimenRoomWalk.at(-1))}`);
  assert.ok(specimenRoomWalk.at(-1).x < 93.2 && specimenRoomWalk.at(-1).z < -19.4, "specimen NW room must be enterable off the spine");
  assert.equal(rooms.stageRack, true, "stage wing must hang costume racks");
  assert.ok(rooms.beats.includes("stagewing"), "stage wing VO beat missing");
  assert.equal(stageWalk.at(-1).ok, true, `must walk the stage wing, got ${JSON.stringify(stageWalk.at(-1))}`);
  assert.ok(stageWalk.some((stop) => stop.x > 94 && Math.abs(stop.z - 32) < 1.6), "stage wing EW spine must stay open");
  assert.ok(stageWalk.at(-1).z < 28 && Math.abs(stageWalk.at(-1).x - 96) < 1.4, "stage wing north T-spur to faculty must stay open");
  assert.equal(rooms.laundryCart, true, "laundry hall must have carts");
  assert.equal(rooms.laundryRoom, true, "laundry hall must close west classroom volumes");
  assert.ok(rooms.beats.includes("laundry"), "laundry VO beat missing");
  assert.equal(laundryWalk.at(-1).ok, true, `must walk the laundry hall, got ${JSON.stringify(laundryWalk.at(-1))}`);
  assert.ok(laundryWalk.at(-1).z < -20 && Math.abs(laundryWalk.at(-1).x - 64) < 1.4, "laundry NS spine must stay open");
  assert.equal(laundryRoomWalk.at(-1).ok, true, `must walk into the laundry NW room, got ${JSON.stringify(laundryRoomWalk.at(-1))}`);
  assert.ok(laundryRoomWalk.at(-1).x < 61.2 && laundryRoomWalk.at(-1).z < -19.4, "laundry NW room must be enterable off the spine");
  assert.equal(rooms.labLinkCase, true, "lab link must show south specimen cases");
  assert.equal(rooms.labLinkRoom, true, "lab link must close south classroom volumes");
  assert.ok(rooms.beats.includes("lablink"), "lab-link VO beat missing");
  assert.equal(labLinkWalk.at(-1).ok, true, `must walk the lab-link hall, got ${JSON.stringify(labLinkWalk.at(-1))}`);
  assert.ok(labLinkWalk.at(-1).x > 84 && Math.abs(labLinkWalk.at(-1).z + 32) < 1.4, "lab-link EW spine must stay open");
  assert.equal(labLinkRoomWalk.at(-1).ok, true, `must walk into the lab-link SE room, got ${JSON.stringify(labLinkRoomWalk.at(-1))}`);
  assert.ok(labLinkRoomWalk.at(-1).x > 83.4 && labLinkRoomWalk.at(-1).z > -29.4, "lab-link SE room must be enterable off the spine");
  assert.equal(rooms.avType, "flicker_room");
  assert.equal(rooms.avCart, true, "AV room must have CRT carts");
  assert.ok(rooms.beats.includes("flicker_room"), "AV room VO beat missing");
  assert.equal(avWalk.at(-1).ok, true, `must walk the AV room, got ${JSON.stringify(avWalk.at(-1))}`);
  assert.ok(avWalk.some((stop) => stop.z < -14 && Math.abs(stop.x - 16) < 1.6), "AV north aisle must stay open");
  assert.ok(avWalk.at(-1).x > 20 && Math.abs(avWalk.at(-1).z + 16) < 1.4, "AV east aisle must stay open");
  assert.equal(rooms.supplyType, "wide_room");
  assert.equal(rooms.supplyCage, true, "supply room must have cages");
  assert.ok(rooms.beats.includes("wide_room"), "supply room VO beat missing");
  assert.equal(supplyWalk.at(-1).ok, true, `must walk the supply room, got ${JSON.stringify(supplyWalk.at(-1))}`);
  assert.ok(supplyWalk.some((stop) => stop.z < -30 && Math.abs(stop.x) < 1.6), "supply south aisle must stay open");
  assert.ok(supplyWalk.at(-1).x > 4 && Math.abs(supplyWalk.at(-1).z + 32) < 1.4, "supply east aisle must stay open");
  assert.equal(rooms.counselType, "omen_room");
  assert.equal(rooms.counselAltar, true, "counseling room must keep the altar");
  assert.equal(rooms.counselSign, true, "counseling room must hang a school sign");
  assert.ok(rooms.beats.includes("omen_room"), "counseling room VO beat missing");
  assert.equal(counselWalk.at(-1).ok, true, `must walk into the counseling room, got ${JSON.stringify(counselWalk.at(-1))}`);
  assert.ok(counselWalk.at(-1).z < -26 && Math.abs(counselWalk.at(-1).x + 16) < 1.6, "counseling south aisle must stay open");
  assert.equal(rooms.staticType, "static_room");
  assert.equal(rooms.staticSet, true, "broadcast closet must keep the TV set");
  assert.ok(rooms.beats.includes("static_room"), "broadcast closet VO beat missing");
  assert.equal(staticWalk.at(-1).ok, true, `must walk into the broadcast closet, got ${JSON.stringify(staticWalk.at(-1))}`);
  assert.ok(staticWalk.at(-1).z < -26 && Math.abs(staticWalk.at(-1).x - 16) < 1.6, "broadcast closet south aisle must stay open");
  assert.equal(rooms.stairHallCone, true, "stair hall must place caution cones");
  assert.ok(rooms.beats.includes("stairhall"), "stair hall VO beat missing");
  assert.equal(stairHallWalk.at(-1).ok, true, `must walk the stair hall, got ${JSON.stringify(stairHallWalk.at(-1))}`);
  assert.ok(stairHallWalk.some((stop) => Math.abs(stop.z - 16) < 1.6 && stop.x > 14), "stair hall EW spine must stay open");
  assert.ok(stairHallWalk.at(-1).z > 20 && Math.abs(stairHallWalk.at(-1).x - 16) < 1.4, "stair hall south T-spur to B1 must stay open");
  assert.equal(rooms.nurseryHallCrib, true, "nursery hall must show cribs");
  assert.equal(rooms.nurseryRoom, true, "nursery hall must close west classroom volumes");
  assert.ok(rooms.beats.includes("nurseryhall"), "nursery hall VO beat missing");
  assert.equal(nurseryHallWalk.at(-1).ok, true, `must walk the nursery hall, got ${JSON.stringify(nurseryHallWalk.at(-1))}`);
  assert.ok(nurseryHallWalk.some((stop) => stop.z > 14 && Math.abs(stop.x - 32) < 1.6), "nursery hall NS spine must stay open");
  assert.ok(nurseryHallWalk.at(-1).x < 28 && Math.abs(nurseryHallWalk.at(-1).z - 16) < 1.4, "nursery hall west T-spur must stay open");
  assert.equal(nurseryRoomWalk.at(-1).ok, true, `must walk into the nursery SW room, got ${JSON.stringify(nurseryRoomWalk.at(-1))}`);
  assert.ok(nurseryRoomWalk.at(-1).x < 28.2 && nurseryRoomWalk.at(-1).z > 18.8, "nursery SW room must be enterable off the spine");
  assert.ok(
    nurseryPinchWalk.at(-1).ok !== true || Math.abs(nurseryPinchWalk.at(-1).x - 32) < 1.38,
    `nursery pinch must block the old 3.4m east classroom, got ${JSON.stringify(nurseryPinchWalk.at(-1))}`,
  );
  assert.equal(rooms.dollHallShelf, true, "doll hall must show west shelves");
  assert.equal(rooms.dollRoom, true, "doll hall must close west classroom volumes");
  assert.ok(rooms.beats.includes("dollhall"), "doll hall VO beat missing");
  assert.equal(dollHallWalk.at(-1).ok, true, `must walk the doll hall, got ${JSON.stringify(dollHallWalk.at(-1))}`);
  assert.ok(dollHallWalk.some((stop) => stop.z > 14 && Math.abs(stop.x + 32) < 1.6), "doll hall NS spine must stay open");
  assert.ok(dollHallWalk.at(-1).x > -28 && Math.abs(dollHallWalk.at(-1).z - 16) < 1.4, "doll hall east T-spur must stay open");
  assert.equal(dollRoomWalk.at(-1).ok, true, `must walk into the doll SW room, got ${JSON.stringify(dollRoomWalk.at(-1))}`);
  assert.ok(dollRoomWalk.at(-1).x < -35.8 && dollRoomWalk.at(-1).z > 18.8, "doll SW room must be enterable off the spine");
  assert.equal(rooms.archiveHallCase, true, "archive hall must show west cases");
  assert.equal(rooms.archiveRoom, true, "archive hall must close west classroom volumes");
  assert.ok(rooms.beats.includes("archivehall"), "archive hall VO beat missing");
  assert.equal(archiveHallWalk.at(-1).ok, true, `must walk the archive hall, got ${JSON.stringify(archiveHallWalk.at(-1))}`);
  assert.ok(archiveHallWalk.at(-1).z < -20 && Math.abs(archiveHallWalk.at(-1).x + 32) < 1.4, "archive hall NS spine must stay open");
  assert.equal(rooms.storageHallCrate, true, "storage hall must show west crates");
  assert.equal(rooms.storageRoom, true, "storage hall must close west classroom volumes");
  assert.ok(rooms.beats.includes("storagehall"), "storage hall VO beat missing");
  assert.equal(storageHallWalk.at(-1).ok, true, `must walk the storage hall, got ${JSON.stringify(storageHallWalk.at(-1))}`);
  assert.ok(storageHallWalk.at(-1).z < -20 && Math.abs(storageHallWalk.at(-1).x - 32) < 1.4, "storage hall NS spine must stay open");
  assert.equal(storageRoomWalk.at(-1).ok, true, `must walk into the storage SW room, got ${JSON.stringify(storageRoomWalk.at(-1))}`);
  assert.ok(storageRoomWalk.at(-1).x < 28.2 && storageRoomWalk.at(-1).z > -10.6, "storage SW room must be enterable off the spine");
  assert.equal(rooms.teaHallBench, true, "tea hall must show north benches");
  assert.equal(rooms.teaRoom, true, "tea hall must close north classroom volumes");
  assert.ok(rooms.beats.includes("teahall"), "tea hall VO beat missing");
  assert.equal(teaHallWalk.at(-1).ok, true, `must walk the tea hall, got ${JSON.stringify(teaHallWalk.at(-1))}`);
  assert.ok(teaHallWalk.some((stop) => Math.abs(stop.z - 16) < 1.6 && stop.x < -14), "tea hall EW spine must stay open");
  assert.ok(teaHallWalk.at(-1).z > 20 && Math.abs(teaHallWalk.at(-1).x + 16) < 1.4, "tea hall south T-spur to the tea room must stay open");
  assert.equal(rooms.dormBunk, true, "dorm must show bunks");
  assert.equal(rooms.dormCrib, true, "dorm must keep the empty crib");
  assert.ok(rooms.beats.includes("workshop"), "dorm VO beat missing");
  assert.equal(dormWalk.at(-1).ok, true, `must walk the dorm north aisle, got ${JSON.stringify(dormWalk.at(-1))}`);
  assert.ok(dormWalk.at(-1).z > 28 && Math.abs(dormWalk.at(-1).x - 32) < 1.4, "dorm north aisle must stay open");
  assert.equal(rooms.dollClassChair, true, "doll class must show chairs");
  assert.ok(rooms.beats.includes("playroom"), "doll class VO beat missing");
  assert.equal(dollClassWalk.at(-1).ok, true, `must walk the doll class, got ${JSON.stringify(dollClassWalk.at(-1))}`);
  assert.ok(dollClassWalk.some((stop) => Math.abs(stop.x + 32) < 1.6 && Math.abs(stop.z - 32) < 1.6), "doll class center must stay open for the key");
  assert.ok(dollClassWalk.at(-1).x > -28 && Math.abs(dollClassWalk.at(-1).z - 32) < 1.4, "doll class east aisle must stay open");
  assert.equal(rooms.prepStoreCrate, true, "prep store must show crates");
  assert.ok(rooms.beats.includes("storage"), "prep store VO beat missing");
  assert.equal(prepStoreWalk.at(-1).ok, true, `must walk the prep store, got ${JSON.stringify(prepStoreWalk.at(-1))}`);
  assert.ok(prepStoreWalk.some((stop) => Math.abs(stop.x - 32) < 1.6 && Math.abs(stop.z + 32) < 1.6), "prep store center must stay open for the key");
  assert.ok(prepStoreWalk.at(-1).x < 28 && Math.abs(prepStoreWalk.at(-1).z + 32) < 1.4, "prep store west aisle must stay open");
  assert.equal(rooms.closedLibTable, true, "closed library must show a side table");
  assert.equal(rooms.closedLibDesk, true, "closed library must keep the archive desk");
  assert.ok(rooms.beats.includes("archive"), "closed library VO beat missing");
  assert.equal(closedLibWalk.at(-1).ok, true, `must walk the closed library, got ${JSON.stringify(closedLibWalk.at(-1))}`);
  assert.ok(closedLibWalk.at(-1).x > -28 && Math.abs(closedLibWalk.at(-1).z + 32) < 1.4, "closed library east aisle must stay open");
  assert.equal(rooms.etiquetteZabuton, true, "etiquette room must show zabuton");
  assert.equal(rooms.etiquetteTable, true, "etiquette room must keep the tea table");
  assert.ok(rooms.beats.includes("tatami_room"), "etiquette VO beat missing");
  assert.equal(etiquetteWalk.at(-1).ok, true, `must walk north of the tea table, got ${JSON.stringify(etiquetteWalk.at(-1))}`);
  assert.ok(etiquetteWalk.at(-1).z > 26 && etiquetteWalk.at(-1).z < 31 && Math.abs(etiquetteWalk.at(-1).x + 16) < 1.4, "etiquette north aisle must stay open");
  assert.equal(rooms.roofHallBoard, true, "roof hall must board the south door");
  assert.equal(rooms.roofHallUnique, true, "roof hall must use a unique L-run footprint");
  assert.ok(rooms.beats.includes("roofhall"), "roof hall VO beat missing");
  assert.equal(roofHallWalk.at(-1).ok, true, `must walk the roof hall, got ${JSON.stringify(roofHallWalk.at(-1))}`);
  assert.ok(roofHallWalk.some((stop) => Math.abs(stop.x) < 1.6 && stop.z > 26), "roof hall north aisle must stay open");
  assert.ok(roofHallWalk.at(-1).x < -4 && Math.abs(roofHallWalk.at(-1).z - 32) < 1.4, "roof hall west aisle must stay open");
  assert.equal(rooms.lostFoundBox, true, "lost-and-found hall must show boxes");
  assert.ok(rooms.beats.includes("lostfound"), "lost-and-found VO beat missing");
  assert.equal(rooms.startHallRack, true, "start hall must show shoe racks");
  assert.equal(rooms.classWingCart, true, "class wing must show north carts");
  assert.ok(rooms.beats.includes("classwing"), "class wing VO beat missing");
  assert.equal(rooms.uncatHallTape, true, "Uncat crossing must show caution tape");
  assert.ok(rooms.beats.includes("uncathall"), "Uncat hall VO beat missing");
  assert.equal(rooms.northHallCone, true, "north hall must show east cones");
  assert.ok(rooms.beats.includes("northhall"), "north hall VO beat missing");
  assert.equal(rooms.northHallCubby, true, "north hall must open a shoe cubby nook");
  assert.equal(rooms.b1BoilerDrum, true, "B1 maze must show a boiler drum");
  assert.equal(rooms.b1FloodDesk, true, "B1 south labyrinth must show flooded desks");
  assert.equal(rooms.b1FloodBoard, true, "B1 flooded classroom must show chalkboards");
  assert.equal(rooms.b1FloodDoor, true, "B1 flooded classroom must show door frames");
  assert.equal(rooms.b1FloodRoom, true, "B1 flooded bays must be closed classroom rooms");
  assert.equal(rooms.f2BloodFrame, true, "2F maze must show blood frames");
  assert.equal(rooms.f2BloodPortrait, true, "2F labyrinth must show extra portraits");
  assert.equal(rooms.f2GalleryFrame, true, "2F gallery must show extra portrait frames");
  assert.equal(rooms.f2GalleryRoom, true, "2F gallery bays must be closed portrait rooms");
  assert.equal(rooms.annexGateRack, true, "annex gate must show shoe racks");
  assert.ok(rooms.beats.includes("annexgate"), "annex gate VO beat missing");
  assert.equal(lostFoundWalk.at(-1).ok, true, `must walk the lost-and-found hall, got ${JSON.stringify(lostFoundWalk.at(-1))}`);
  assert.ok(lostFoundWalk.at(-1).z > 4 && Math.abs(lostFoundWalk.at(-1).x + 32) < 1.4, "lost-and-found NS spine must stay open");
  assert.equal(rooms.eastWashBucket, true, "east wash hall must show north buckets");
  assert.equal(rooms.angelHallCart, true, "angel hall must show an east cart");
  assert.equal(rooms.washFourCubby, true, "annex wash crossing must show north cubbies");
  assert.ok(rooms.hallClassCount >= 8, `1F school classroom walls missing: ${rooms.hallClassCount}`);
  assert.equal(rooms.hallRib, false, "east 1F halls must not keep S-bend ribs");
  assert.equal(rooms.northRib, false, "north 1F halls must not keep west-loop ribs");
  assert.ok(rooms.hallLockers >= 8, `1F maze halls must dress the corridor with locker banks: ${rooms.hallLockers}`);
  assert.equal(rooms.hallClass, true, "school halls must be enclosed by classroom walls");
  assert.equal(rooms.hallStripe, true, "school hallway stripe must run the center corridor");
  assert.equal(rooms.hallWindow, true, "classroom doors must have dark windows");
  assert.equal(rooms.hallDesk, true, "classroom desks must be visible through hall doors");
  assert.ok(rooms.hallDeskCount >= 8, `classroom desk rows missing: ${rooms.hallDeskCount}`);
  assert.equal(rooms.hallLino, true, "classrooms must have a linoleum floor");
  assert.equal(rooms.hallTeacher, true, "classrooms must have a teacher desk");
  assert.equal(rooms.hallPa, true, "school halls must hang a PA speaker");
  assert.equal(rooms.hallClock, true, "school halls must have a stopped clock");
  assert.equal(rooms.hallLibrary, true, "west school hall must open a library nook");
  assert.equal(rooms.hallLibrarySpines, true, "library shelves must show book spines");
  assert.equal(rooms.hallWash, true, "east school hall must open a washroom nook");
  assert.equal(rooms.hallWashDoors, true, "washroom stalls must have doors");
  assert.equal(rooms.hallBoarded, true, "Uncat south hall must have a boarded classroom");
  assert.equal(rooms.hallNookSign, true, "unique nooks must hang a room sign");
  assert.equal(rooms.hallJog, false, "east 1F halls must keep the z=0 spine open");
  assert.equal(rooms.hallJogE, false, "east 1F halls must not block the east arm with a jog");
  assert.equal(rooms.hallJogNs, false, "north 1F halls must keep the x=0 spine open");
  assert.equal(rooms.hallJogNsS, false, "north 1F halls must not block the south arm with a jog");
  assert.equal(rooms.uncatSpineClear, true, "Uncat south reveal hall must stay a straight spine");
  assert.ok(northWalk.some((stop) => stop.x > 2.0), "north school T-spur must open east off x=0");
  assert.ok(northWalk.some((stop) => stop.x < -2.0), "north school west alcove must be walkable");
  assert.equal(northWalk.at(-1).ok, true, `must walk the north 1F hall, got ${JSON.stringify(northWalk.at(-1))}`);
  assert.ok(northWalk.at(-1).z < -19.5, "north hall continues past the tile center");
  assert.ok(nsLoopWalk.some((stop) => stop.x < -3.2 && stop.z > -13), "north tile west alcove must be enterable");
  assert.ok(nsLoopWalk.some((stop) => stop.x > 1.8 && Math.abs(stop.z + 16) < 1.5), "north tile east T-spur must be walkable");
  assert.equal(nsLoopWalk.at(-1).ok, true, `must finish the north 1F hall, got ${JSON.stringify(nsLoopWalk.at(-1))}`);
  assert.equal(rooms.hallLoopN, false, "east 1F halls must not pinch the north arm with a loop baffle");
  assert.equal(rooms.hallLoopW, false, "north 1F halls must not pinch the west arm with a loop baffle");
  assert.equal(b1Walk.at(-1).ok, true, `must walk the B1 maze to the nursery, got ${JSON.stringify(b1Walk.at(-1))}`);
  assert.ok(b1Walk.at(-1).x < 0, "nursery is west of the inner crib door");
  assert.equal(f2Walk.at(-1).ok, true, `must walk the 2F maze to the shrine, got ${JSON.stringify(f2Walk.at(-1))}`);
  assert.ok(f2Walk.at(-1).x < -27.4, "shrine is west of the inner gallery door");
  assert.ok(story.fired.includes("leaveStart"), `leaveStart VO missing: ${story.fired.join(",")}`);
  assert.ok(story.fired.includes("f1maze"), `1F maze VO missing: ${story.fired.join(",")}`);
  assert.ok(story.fired.includes("f1ring") || longRingWalk.at(-1).x > 30, "long outer-ring story beat should fire");
  assert.ok(story.fired.includes("b1floor"), `B1 floor VO missing: ${story.fired.join(",")}`);
  assert.ok(story.fired.includes("nursery"), `nursery VO missing: ${story.fired.join(",")}`);
  assert.ok(story.fired.includes("f2floor"), `2F floor VO missing: ${story.fired.join(",")}`);
  assert.ok(story.fired.includes("shrine"), `shrine VO missing: ${story.fired.join(",")}`);
  assert.ok(story.beats.includes("map:b1"));
  assert.ok(story.beats.includes("map:f2"));
  assert.equal(rooms.nurseType, "nurse_office");
  assert.equal(rooms.musicType, "music_room");
  assert.equal(rooms.facultyType, "faculty_office");
  assert.equal(rooms.scienceType, "science_lab");
  assert.equal(rooms.annexSign, true, "별관 sign must exist");
  assert.equal(rooms.nurseBed, true);
  assert.equal(rooms.piano, true);
  assert.equal(rooms.facultyDesk, true);
  assert.equal(rooms.labBench, true);
  assert.ok(rooms.beats.includes("nurse_office"));
  assert.ok(rooms.beats.includes("music_room"));
  assert.ok(rooms.beats.includes("faculty_office"));
  assert.ok(rooms.beats.includes("science_lab"));
  assert.ok(rooms.beats.includes("map:f1b"));
  assert.ok(rooms.b1Maze >= 16, `B1 maze walls missing: ${rooms.b1Maze}`);
  assert.ok(rooms.f2Maze >= 12, `2F maze walls missing: ${rooms.f2Maze}`);
  assert.equal(rooms.b1Lab, true, "B1 south labyrinth floor must exist");
  assert.equal(rooms.b1EastLab, true, "B1 east labyrinth floor must exist");
  assert.equal(rooms.f2Lab, true, "2F north labyrinth floor must exist");
  assert.equal(rooms.f2SouthLab, true, "2F south labyrinth floor must exist");
  assert.equal(b1DeepWalk.at(-1).ok, true, `must walk the B1 south labyrinth, got ${JSON.stringify(b1DeepWalk.at(-1))}`);
  assert.ok(b1DeepWalk.at(-1).z > 50, "south labyrinth continues past the old basement wall");
  assert.ok(b1DeepWalk.some((stop) => stop.x < 6.75 && Math.abs(stop.z - 48) < 1.4), "must enter the west flooded classroom");
  assert.equal(b1EastWalk.at(-1).ok, true, `must walk the B1 east labyrinth, got ${JSON.stringify(b1EastWalk.at(-1))}`);
  assert.ok(b1EastWalk.at(-1).x > 28, "east labyrinth continues past the old basement wall");
  assert.equal(f2DeepWalk.at(-1).ok, true, `must walk the 2F north labyrinth, got ${JSON.stringify(f2DeepWalk.at(-1))}`);
  assert.ok(f2DeepWalk.at(-1).z < -46, "north labyrinth continues past the old gallery wall");
  assert.ok(f2DeepWalk.some((stop) => stop.x < -34.4 && Math.abs(stop.z + 45) < 1.2), "must enter the west portrait room");
  assert.equal(f2SouthWalk.at(-1).ok, true, `must walk the 2F south labyrinth, got ${JSON.stringify(f2SouthWalk.at(-1))}`);
  assert.ok(f2SouthWalk.at(-1).z > 3, "south labyrinth continues past the old gallery wall");
  assert.ok(story.fired.includes("b1deep") || b1DeepWalk.at(-1).z > 42, "B1 deep story beat should fire in the south maze");
  assert.ok(story.fired.includes("b1floodclass"), "B1 flood classroom VO beat missing");
  assert.ok(story.fired.includes("b1east") || b1EastWalk.at(-1).x > 28, "B1 east story beat should fire in the east maze");
  assert.ok(story.fired.includes("f2deep") || f2DeepWalk.at(-1).z < -37, "2F deep story beat should fire in the north maze");
  assert.ok(story.fired.includes("f2gallery"), "2F gallery VO beat missing");
  assert.ok(story.fired.includes("f2south") || f2SouthWalk.at(-1).z > -6, "2F south story beat should fire in the south maze");
  assert.ok(rooms.b1Cabinets >= 5, "basement needs extra hide spots");
  assert.ok(rooms.f2Cabinets >= 5, "2F gallery needs extra hide spots");
  assert.ok(rooms.altarChildren >= 10, "제단함 must be a shrine, not a single spinning box");
  assert.equal(altarStill.before, altarStill.after);
  assert.equal(altarStill.label, "제단함");
  for (const item of loop.collected) {
    assert.equal(item.ok, true, `failed to collect ${item.id}`);
  }
  assert.equal(loop.keyCount, 4);
  assert.equal(loop.total, 4);
  assert.equal(loop.hiding.hidden, true);
  assert.equal(loop.hiding.slats, true, "hiding must show locker slats");
  assert.equal(loop.hiding.interior, true, "hiding must look out through a 3D locker interior");
  assert.equal(loop.cleared, true, "ritual must clear the run");
  console.log("SHADOW RUN PASSED");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", errors);
  process.exit(1);
} finally {
  await browser.close();
}
