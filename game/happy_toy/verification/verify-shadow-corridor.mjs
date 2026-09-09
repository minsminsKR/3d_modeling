import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { CABINET_CONFIG, LIGHTING_CONFIG, STALKER_CONFIG } from "../src/config/gameConfig.js";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

assert.ok(LIGHTING_CONFIG.fogFar <= 12, "fog must collapse to flashlight range");
assert.ok(LIGHTING_CONFIG.ambientIntensity <= 0.03, "ambient must leave unlit space nearly black");
assert.ok(LIGHTING_CONFIG.flashlightFillIntensity <= 2, "fill light must not wash the corridor");
assert.ok(STALKER_CONFIG.graceSeconds <= 10, "stalker should enter after a short grace");
assert.ok(CABINET_CONFIG.caughtDelaySeconds >= 3, "locker checks must linger");

const browser = await chromium.launch({ executablePath, headless: true });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  await page.evaluate(() => window.__happyToy.start());

  const result = await page.evaluate(() => {
    const game = window.__happyToy;
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    const before = {
      dormant: uncat.isDormant,
      title: document.title,
      loreCount: (game.loreNotes || []).length,
      cabinetCount: (game.cabinets || []).length,
    };

    game.playTime = 8;
    game.tryReleaseCorridorStalker();
    const afterRelease = {
      dormant: uncat.isDormant,
      visible: uncat.group.visible,
      stalkerFlag: game.stalkerReleased,
      state: uncat.state,
    };

    game.testSafeMode = true;
    const startDist = Math.hypot(
      uncat.group.position.x - game.player.position.x,
      uncat.group.position.z - game.player.position.z,
    );
    for (let i = 0; i < 90; i += 1) {
      game.update(0.05, { skipRender: true });
    }
    const afterChase = {
      startDist,
      endDist: Math.hypot(
        uncat.group.position.x - game.player.position.x,
        uncat.group.position.z - game.player.position.z,
      ),
      state: uncat.state,
    };

    game.testSafeMode = true;
    game.ghostMode = false;
    game.mapBuilder.generator.generateChunk(0, 0);
    game.mapBuilder.generator.generateChunk(0, 1);
    game.mapBuilder.generator.generateChunk(1, 0);
    game.player.setPosition({ x: 16, y: 0, z: 2.6 });
    for (let i = 0; i < 8; i += 1) game.update(0.05, { skipRender: true });
    uncat.setDormant(false);
    uncat.group.visible = true;
    uncat.group.position.set(0, 0, 16);
    uncat.state = "chase";
    uncat.caughtPlayer = false;
    uncat.chasePath = null;
    uncat.chasePathTimer = 0;
    uncat.chasePathGoal = null;
    uncat.lastKnownPlayerPosition = game.player.position.clone();
    uncat.memoryTimer = 14;
    const mazeStartDist = Math.hypot(uncat.group.position.x - 16, uncat.group.position.z - 2.6);
    for (let i = 0; i < 240; i += 1) game.update(0.05, { skipRender: true });
    const mazeChase = {
      startDist: mazeStartDist,
      endDist: Math.hypot(
        uncat.group.position.x - game.player.position.x,
        uncat.group.position.z - game.player.position.z,
      ),
      x: uncat.group.position.x,
      z: uncat.group.position.z,
      pathLen: uncat.chasePath?.length || 0,
      state: uncat.state,
    };

    game.player.setPosition({ x: 16, y: 0, z: -2.6 });
    uncat.group.position.set(11.5, 0, -2.6);
    uncat.state = "chase";
    uncat.caughtPlayer = false;
    uncat.chasePath = null;
    uncat.chasePathTimer = 0;
    uncat.chasePathGoal = null;
    uncat.lastKnownPlayerPosition = game.player.position.clone();
    uncat.memoryTimer = 14;
    const loopStartDist = Math.hypot(uncat.group.position.x - 16, uncat.group.position.z + 2.6);
    let loopMaxZ = -9;
    let loopMinZ = 9;
    for (let i = 0; i < 180; i += 1) {
      game.update(0.05, { skipRender: true });
      loopMaxZ = Math.max(loopMaxZ, uncat.group.position.z);
      loopMinZ = Math.min(loopMinZ, uncat.group.position.z);
    }
    const mazeChaseLoop = {
      startDist: loopStartDist,
      endDist: Math.hypot(
        uncat.group.position.x - game.player.position.x,
        uncat.group.position.z - game.player.position.z,
      ),
      x: uncat.group.position.x,
      z: uncat.group.position.z,
      maxZ: loopMaxZ,
      minZ: loopMinZ,
      state: uncat.state,
    };

    game.player.setPosition({ x: 20.5, y: 0, z: -6.4 });
    uncat.group.position.set(11.6, 0, -6.4);
    uncat.state = "chase";
    uncat.caughtPlayer = false;
    uncat.chasePath = null;
    uncat.chasePathTimer = 0;
    uncat.chasePathGoal = null;
    uncat.lastKnownPlayerPosition = game.player.position.clone();
    uncat.memoryTimer = 14;
    const ringStartDist = Math.hypot(uncat.group.position.x - 20.5, uncat.group.position.z + 6.4);
    let ringMaxZ = -9;
    let ringMinZ = 9;
    for (let i = 0; i < 200; i += 1) {
      game.update(0.05, { skipRender: true });
      ringMaxZ = Math.max(ringMaxZ, uncat.group.position.z);
      ringMinZ = Math.min(ringMinZ, uncat.group.position.z);
    }
    const mazeChaseRing = {
      startDist: ringStartDist,
      endDist: Math.hypot(
        uncat.group.position.x - game.player.position.x,
        uncat.group.position.z - game.player.position.z,
      ),
      x: uncat.group.position.x,
      z: uncat.group.position.z,
      maxZ: ringMaxZ,
      minZ: ringMinZ,
      state: uncat.state,
    };

    const hallCabinets = game.mapBuilder.generator.generateChunk(1, 0).cabinets || [];
    const bendCab = [...(game.cabinets || []), ...hallCabinets].find((item) => (
      Math.abs((item.position?.y || 0)) < 1.2
      && Math.hypot((item.position?.x || 0) - 10.75, (item.position?.z || 0) - 5.25) < 4
    ));
    if (bendCab && !(game.cabinets || []).includes(bendCab)) {
      game.cabinets.push(bendCab);
    }
    let bendHide = { cabinet: Boolean(bendCab), x: bendCab?.position?.x ?? null, z: bendCab?.position?.z ?? null };
    if (bendCab) {
      uncat.group.position.set(16, 0, 2.6);
      uncat.state = "chase";
      uncat.hasVisualContact = true;
      uncat.lastKnownPlayerPosition = bendCab.position.clone();
      game.player.setPosition({
        x: bendCab.position.x,
        y: bendCab.position.y,
        z: bendCab.position.z + 0.9,
      });
      game.enterCabinet(bendCab, { forceOutcome: "safe" });
      bendHide = {
        cabinet: true,
        hidden: game.player.isHidden,
        investigating: uncat.state === "investigateCabinet",
        hasEvent: Boolean(game.cabinetEvent),
      };
      game.exitCabinet();
    }

    game.testSafeMode = true;
    const b1 = game.mapBuilder.generator.generateChunk(1, 2);
    const f2 = game.mapBuilder.generator.generateChunk(-1, -1);
    const floors = {
      b1Type: b1.type,
      f2Type: f2.type,
      flood: (b1.meshes || []).some((mesh) => String(mesh.name || "").includes("flood_water")),
      drip: (b1.meshes || []).some((mesh) => String(mesh.name || "").includes("drip")),
      foam: (b1.meshes || []).some((mesh) => String(mesh.name || "").includes("flood_foam")),
      b1Maze: (b1.meshes || []).some((mesh) => String(mesh.name || "").includes("b1_maze_")),
      b1Lab: (b1.meshes || []).some((mesh) => String(mesh.name || "").includes("cellar_b1_south_lab") || String(mesh.name || "").includes("cellar_b1_floor_south_lab")),
      b1EastLab: (b1.meshes || []).some((mesh) => String(mesh.name || "").includes("cellar_b1_floor_east_lab")),
      blood: (f2.meshes || []).some((mesh) => String(mesh.name || "").includes("blood")),
      bloodWall: (f2.meshes || []).some((mesh) => String(mesh.name || "").includes("blood_wall")),
      f2Maze: (f2.meshes || []).some((mesh) => String(mesh.name || "").includes("gallery_maze_")),
      f2Lab: (f2.meshes || []).some((mesh) => String(mesh.name || "").includes("gallery_2f_floor_north_lab")),
      f2SouthLab: (f2.meshes || []).some((mesh) => String(mesh.name || "").includes("gallery_2f_floor_south_lab")),
    };
    const hall = game.mapBuilder.generator.generateChunk(1, 0);
    const northHall = game.mapBuilder.generator.generateChunk(0, -1);
    const uncatHall = game.mapBuilder.generator.generateChunk(0, 1);
    floors.hallMaze = (hall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_"));
    floors.hallJog = (hall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_jog"));
    floors.hallJogE = (hall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_jog_e"));
    floors.hallJogNs = (northHall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_jog_ns"));
    floors.hallJogNsS = (northHall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_jog_ns_s"));
    floors.hallLoopN = (hall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_loop_n"));
    floors.hallLoopW = (northHall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_loop_w"));
    floors.hallRib = (hall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_rib") || String(mesh.name || "").includes("hall_maze_pocket"));
    floors.northRib = (northHall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_rib") || String(mesh.name || "").includes("hall_maze_pocket"));
    floors.uncatSpineClear = !(uncatHall.meshes || []).some((mesh) => String(mesh.name || "").includes("hall_maze_jog"));
    game.player.setPosition({ x: 80, y: 0, z: 0 });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
    const deepWing = {
      totalKeys: game.getTotalKeys(),
      hud: document.querySelector("#key-count-text")?.textContent,
      liveKeys: game.keys.length,
      chunkType: game.mapBuilder.generator.getChunkType(5, 0),
      openings: game.mapBuilder.generator.getOpenings(5, 0),
    };
    game.player.setPosition({ x: 96, y: 0, z: 0 });
    for (let i = 0; i < 12; i += 1) game.update(0.05, { skipRender: true });
    const atWing = {
      totalKeys: game.getTotalKeys(),
      hud: document.querySelector("#key-count-text")?.textContent,
      liveKeys: game.keys.length,
    };
    game.ghostMode = true;
    game.testSafeMode = false;
    game.player.setPosition({ x: 10.5, y: -5, z: 32 });
    for (let i = 0; i < 50; i += 1) game.update(0.05, { skipRender: true });
    const floorHunt = {
      uncatY: uncat.group.position.y,
      b1Cabinets: (b1.cabinets || []).length,
      f2Cabinets: (f2.cabinets || []).length,
      huntVisible: game.floorHuntDirector?.silhouette?.visible === true,
      huntY: game.floorHuntDirector?.silhouette?.position?.y ?? null,
      huntModel: game.floorHuntDirector?.modelReady === true,
    };
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
    game.ghostMode = false;
    game.testSafeMode = false;

    const cabinet = game.cabinets[0];
    uncat.group.position.set(cabinet.position.x, cabinet.position.y, cabinet.position.z + 1.6);
    uncat.state = "chase";
    uncat.hasVisualContact = true;
    uncat.lastKnownPlayerPosition = game.player.position.clone();
    game.player.setPosition({
      x: cabinet.position.x,
      y: cabinet.position.y,
      z: cabinet.position.z + 0.9,
    });
    game.enterCabinet(cabinet, { forceOutcome: "safe" });
    const hiding = {
      hidden: game.player.isHidden,
      investigating: uncat.state === "investigateCabinet",
      hasEvent: Boolean(game.cabinetEvent),
    };

    game.handleCaught("test-death");
    for (let i = 0; i < 40; i += 1) {
      game.updateDeathSequence(0.05);
    }
    return {
      before,
      afterRelease,
      afterChase,
      mazeChase,
      mazeChaseLoop,
      mazeChaseRing,
      bendHide,
      floors,
      deepWing,
      atWing,
      floorHunt,
      hiding,
      death: {
        gameOver: game.gameOver,
        shown: game.deathSequence?.shown === true,
        veil: document.body.classList.contains("death-veil"),
      },
    };
  });

  console.log(result);
  assert.equal(errors.length, 0, `page errors: ${errors.join(" | ")}`);
  assert.equal(result.before.title, "그림자복도");
  assert.ok(result.before.loreCount >= 1, "start chunk should spawn a lore note");
  assert.ok(result.before.cabinetCount >= 1, "lockers must exist");
  assert.equal(result.before.dormant, true, "Uncat starts dormant");
  assert.equal(result.afterRelease.dormant, false, "grace release must wake Uncat");
  assert.equal(result.afterRelease.visible, true);
  assert.equal(result.afterRelease.stalkerFlag, true);
  assert.equal(result.afterRelease.state, "chase", "released stalker must hunt immediately");
  assert.ok(result.afterChase.startDist > 8, `stalker should spawn down the hall, got ${result.afterChase.startDist}`);
  assert.ok(
    result.afterChase.endDist < result.afterChase.startDist - 1.5,
    `stalker must close distance ${result.afterChase.startDist} -> ${result.afterChase.endDist}`,
  );
  assert.ok(
    result.mazeChase.endDist < result.mazeChase.startDist - 4,
    `Uncat must hunt through the east S-bend ${result.mazeChase.startDist} -> ${result.mazeChase.endDist} at ${result.mazeChase.x},${result.mazeChase.z}`,
  );
  assert.ok(
    result.mazeChase.x > 6 || result.mazeChase.endDist < 10,
    `Uncat must leave the south spine into the east maze, got x=${result.mazeChase.x} dist=${result.mazeChase.endDist}`,
  );
  assert.ok(
    result.mazeChaseLoop.endDist < result.mazeChaseLoop.startDist - 2,
    `Uncat must hunt the north loop ${result.mazeChaseLoop.startDist} -> ${result.mazeChaseLoop.endDist} at ${result.mazeChaseLoop.x},${result.mazeChaseLoop.z}`,
  );
  assert.ok(
    result.mazeChaseLoop.maxZ < 1.2,
    `north-loop chase must stay off the south jog, maxZ=${result.mazeChaseLoop.maxZ}`,
  );
  assert.ok(
    result.mazeChaseRing.endDist < result.mazeChaseRing.startDist - 2,
    `Uncat must hunt the outer ring ${result.mazeChaseRing.startDist} -> ${result.mazeChaseRing.endDist} at ${result.mazeChaseRing.x},${result.mazeChaseRing.z}`,
  );
  assert.ok(
    result.mazeChaseRing.z < -4.8,
    `outer-ring chase must stay on the north border, z=${result.mazeChaseRing.z}`,
  );
  assert.equal(result.floors.hallRib, true, "east 1F halls must keep inner maze ribs");
  assert.equal(result.floors.northRib, true, "north 1F halls must keep inner maze ribs");
  assert.equal(result.bendHide.cabinet, true, "east S-bend locker must exist");
  assert.equal(result.bendHide.hidden, true, "player must hide in the S-bend locker");
  assert.equal(result.bendHide.investigating, true, "Uncat must check the S-bend locker");
  assert.notEqual(result.deepWing.chunkType, "void", "별관 must continue the 1F school");
  assert.ok(
    result.deepWing.openings.W || result.deepWing.openings.E || result.deepWing.openings.N || result.deepWing.openings.S,
    "별관 must stay open",
  );
  assert.equal(result.deepWing.totalKeys, 4);
  assert.equal(result.deepWing.hud, "0 / 4");
  assert.equal(result.floors.b1Type, "stairs_b1");
  assert.equal(result.floors.f2Type, "stairs_2f");
  assert.equal(result.floors.flood, true, "basement must hold standing water");
  assert.equal(result.floors.drip, true, "basement must drip");
  assert.equal(result.floors.foam, true, "basement water must show scum");
  assert.equal(result.floors.b1Maze, true, "basement must be a maze, not an open hall");
  assert.equal(result.floors.b1Lab, true, "basement south labyrinth must exist");
  assert.equal(result.floors.b1EastLab, true, "basement east labyrinth must exist");
  assert.equal(result.floors.blood, true, "2F gallery must be blood-soaked");
  assert.equal(result.floors.bloodWall, true, "2F walls must carry blood");
  assert.equal(result.floors.f2Maze, true, "2F gallery must be a maze, not an open hall");
  assert.equal(result.floors.f2Lab, true, "2F north labyrinth must exist");
  assert.equal(result.floors.f2SouthLab, true, "2F south labyrinth must exist");
  assert.equal(result.floors.hallMaze, true, "1F halls must fold into alcove mazes");
  assert.equal(result.floors.hallJog, true, "east 1F halls must jog off the straight spine");
  assert.equal(result.floors.hallJogE, true, "east 1F halls must S-bend through both arms");
  assert.equal(result.floors.hallJogNs, true, "north 1F halls must jog off the x=0 spine");
  assert.equal(result.floors.hallJogNsS, true, "north 1F halls must S-bend through both arms");
  assert.equal(result.floors.hallLoopN, true, "east 1F loops must pinch the north plus arm");
  assert.equal(result.floors.hallLoopW, true, "north 1F loops must pinch the west plus arm");
  assert.equal(result.floors.uncatSpineClear, true, "Uncat south reveal hall must stay a straight spine");
  assert.equal(result.atWing.totalKeys, 4, "unloading key rooms must not shrink the four-name loop");
  assert.equal(result.atWing.hud, "0 / 4");
  assert.ok(Math.abs(result.floorHunt.uncatY) < 2.5, `Uncat must stay on 1F while the player is in B1, got y=${result.floorHunt.uncatY}`);
  assert.equal(result.floorHunt.huntVisible, true, "B1 floor hunt silhouette must appear after a short linger");
  assert.ok(result.floorHunt.huntY < -2, `floor hunt must stay in B1, got y=${result.floorHunt.huntY}`);
  assert.ok(result.floorHunt.b1Cabinets >= 3, "basement needs multiple hide spots");
  assert.ok(result.floorHunt.f2Cabinets >= 3, "2F gallery needs multiple hide spots");
  assert.equal(result.hiding.hidden, true);
  assert.equal(result.hiding.investigating, true);
  assert.equal(result.hiding.hasEvent, true);
  assert.equal(result.death.gameOver, true);
  assert.equal(result.death.shown, true);
  assert.equal(result.death.veil, true);
  console.log("SHADOW CORRIDOR LOOP PASSED");
} catch (error) {
  console.error("Verification failed!", error);
  console.error("Browser errors recorded:", errors);
  process.exit(1);
} finally {
  await browser.close();
}
