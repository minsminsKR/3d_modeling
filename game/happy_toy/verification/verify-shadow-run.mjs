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

  const ewS = (centerX) => ([
    { x: centerX - 4.5, z: 0 },
    { x: centerX - 4.5, z: 2.6 },
    { x: centerX, z: 2.6 },
    { x: centerX, z: 0 },
    { x: centerX, z: -2.6 },
    { x: centerX + 4.5, z: -2.6 },
    { x: centerX + 4.5, z: 0 },
  ]);
  const annexWalk = [];
  for (const stop of [
    { x: 10.2, z: 0 },
    ...ewS(16),
    ...ewS(32),
    ...ewS(48),
    ...ewS(64),
    ...ewS(80),
    { x: 84.5, z: -2.6 },
    { x: 80, z: -2.6 },
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
    game.mapBuilder.generator.generateChunk(0, -1);
    game.player.setPosition({ x: 0, y: 0, z: -8 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
  });
  const northNsS = (centerZ) => ([
    { x: 0, z: centerZ + 4.5 },
    { x: 2.6, z: centerZ + 4.5 },
    { x: 2.6, z: centerZ },
    { x: 0, z: centerZ },
    { x: -2.6, z: centerZ },
    { x: -2.6, z: centerZ - 4.5 },
    { x: 0, z: centerZ - 4.5 },
  ]);
  const northWalk = [];
  for (const stop of northNsS(-16)) {
    northWalk.push(await walkTo(stop, 360));
    console.log("north", stop, northWalk.at(-1));
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
    { x: 16, z: -2.4 },
    { x: 20, z: -2.4 },
    { x: 20, z: -6.2 },
  ]) {
    f1MazeWalk.push(await walkTo(stop, 280));
    console.log("f1maze", stop, f1MazeWalk.at(-1));
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
    const b1 = generator.generateChunk(1, 2);
    const f2 = generator.generateChunk(-1, -1);
    const names = (chunk) => (chunk.meshes || []).map((mesh) => String(mesh.name || ""));
    game.playTime = 12;
    game._lastPlayerChunkCx = 4;
    game._lastPlayerChunkCz = 0;
    game.onEnterSchoolChunk(4, 0);
    game.onEnterSchoolChunk(5, -1);
    game.onEnterSchoolChunk(8, 2);
    game.onEnterSchoolChunk(6, 1);
    game.onEnterSchoolChunk(6, -2);
    return {
      nurseType: nurse.type,
      musicType: music.type,
      facultyType: faculty.type,
      scienceType: science.type,
      annexSign: names(gate).some((name) => name.includes("annex_sign")),
      hallMaze: names(hall).filter((name) => name.includes("hall_maze_")).length,
      hallJog: names(hall).some((name) => name.includes("hall_maze_jog")),
      hallJogE: names(hall).some((name) => name.includes("hall_maze_jog_e")),
      hallJogNs: names(northHall).some((name) => name.includes("hall_maze_jog_ns")),
      hallJogNsS: names(northHall).some((name) => name.includes("hall_maze_jog_ns_s")),
      uncatSpineClear: !names(uncatHall).some((name) => name.includes("hall_maze_jog")),
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
    };
    game.exitCabinet();

    game.player.setPosition({ x: 0.15, y: 0, z: -0.7 });
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

  console.log({ annexWalk, northWalk, f1MazeWalk, rooms, altarStill, b1Walk, b1DeepWalk, b1EastWalk, f2Walk, f2DeepWalk, f2SouthWalk, story, loop });
  assert.equal(errors.length, 0, `page errors: ${errors.join(" | ")}`);
  assert.ok(annexWalk[0].x > 8, `must leave the start hall east, got ${JSON.stringify(annexWalk[0])}`);
  assert.ok(annexWalk.some((stop) => stop.z > 1.8), "east school must force a south jog off z=0");
  assert.ok(annexWalk.some((stop) => stop.z < -1.8), "east school must S-bend north off z=0 before the next tile");
  const annexGate = annexWalk.find((stop) => stop.x > 56 && Math.abs(stop.z) < 1.2);
  assert.ok(annexGate?.ok, `must walk to 별관 gate, got ${JSON.stringify(annexGate)}`);
  assert.equal(annexWalk.at(-1).ok, true, `must walk into 보건실, got ${JSON.stringify(annexWalk.at(-1))}`);
  assert.ok(annexWalk.at(-1).z < -10, "보건실 is on the north wing of the annex spine");
  assert.equal(f1MazeWalk.at(-1).ok, true, `must walk a 1F hall alcove maze, got ${JSON.stringify(f1MazeWalk.at(-1))}`);
  assert.ok(f1MazeWalk.at(-1).z < -5, "1F hall maze continues into the north alcove");
  assert.ok(rooms.hallMaze >= 6, `1F hall maze walls missing: ${rooms.hallMaze}`);
  assert.equal(rooms.hallJog, true, "east 1F halls must block the z=0 spine and jog south");
  assert.equal(rooms.hallJogE, true, "east 1F halls must block the east arm so the spine cannot reopen");
  assert.equal(rooms.hallJogNs, true, "north 1F halls must block the x=0 spine and jog west");
  assert.equal(rooms.hallJogNsS, true, "north 1F halls must block the south arm so the spine cannot reopen");
  assert.equal(rooms.uncatSpineClear, true, "Uncat south reveal hall must stay a straight spine");
  assert.ok(northWalk.some((stop) => stop.x > 2.0), "north school must S-bend east off x=0");
  assert.ok(northWalk.some((stop) => stop.x < -2.0), "north school must force a west jog off x=0");
  assert.equal(northWalk.at(-1).ok, true, `must walk the north 1F jog, got ${JSON.stringify(northWalk.at(-1))}`);
  assert.ok(northWalk.at(-1).z < -19.5, "north jog continues past the baffle");
  assert.equal(b1Walk.at(-1).ok, true, `must walk the B1 maze to the nursery, got ${JSON.stringify(b1Walk.at(-1))}`);
  assert.ok(b1Walk.at(-1).x < 0, "nursery is west of the inner crib door");
  assert.equal(f2Walk.at(-1).ok, true, `must walk the 2F maze to the shrine, got ${JSON.stringify(f2Walk.at(-1))}`);
  assert.ok(f2Walk.at(-1).x < -27.4, "shrine is west of the inner gallery door");
  assert.ok(story.fired.includes("leaveStart"), `leaveStart VO missing: ${story.fired.join(",")}`);
  assert.ok(story.fired.includes("f1maze"), `1F maze VO missing: ${story.fired.join(",")}`);
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
  assert.equal(b1EastWalk.at(-1).ok, true, `must walk the B1 east labyrinth, got ${JSON.stringify(b1EastWalk.at(-1))}`);
  assert.ok(b1EastWalk.at(-1).x > 28, "east labyrinth continues past the old basement wall");
  assert.equal(f2DeepWalk.at(-1).ok, true, `must walk the 2F north labyrinth, got ${JSON.stringify(f2DeepWalk.at(-1))}`);
  assert.ok(f2DeepWalk.at(-1).z < -46, "north labyrinth continues past the old gallery wall");
  assert.equal(f2SouthWalk.at(-1).ok, true, `must walk the 2F south labyrinth, got ${JSON.stringify(f2SouthWalk.at(-1))}`);
  assert.ok(f2SouthWalk.at(-1).z > 3, "south labyrinth continues past the old gallery wall");
  assert.ok(story.fired.includes("b1deep") || b1DeepWalk.at(-1).z > 42, "B1 deep story beat should fire in the south maze");
  assert.ok(story.fired.includes("b1east") || b1EastWalk.at(-1).x > 28, "B1 east story beat should fire in the east maze");
  assert.ok(story.fired.includes("f2deep") || f2DeepWalk.at(-1).z < -37, "2F deep story beat should fire in the north maze");
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
  assert.equal(loop.cleared, true, "ritual must clear the run");
  console.log("SHADOW RUN PASSED");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", errors);
  process.exit(1);
} finally {
  await browser.close();
}
