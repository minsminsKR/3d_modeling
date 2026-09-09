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
      game.update(0.05);
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
    game.player.setPosition({ x: 96, y: 0, z: 0 });
    for (let i = 0; i < 12; i += 1) game.update(0.05);
    const atWing = {
      totalKeys: game.getTotalKeys(),
      hud: document.querySelector("#key-count-text")?.textContent,
      liveKeys: game.keys.length,
    };
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    for (let i = 0; i < 16; i += 1) game.update(0.05);

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
      atWing,
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
  assert.equal(result.atWing.totalKeys, 4, "unloading key rooms must not shrink the four-name loop");
  assert.equal(result.atWing.hud, "0 / 4");
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
