import assert from "node:assert/strict";
import { createRequire } from "node:module";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const browser = await chromium.launch({ executablePath, headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });

  const result = await page.evaluate(() => {
    const game = window.__happyToy;
    const canvas = document.querySelector("#game-root canvas");
    const enemy = game.enemyManager.enemies.find((entry) => !entry.isBaby) || game.enemyManager.enemies[0];

    const before = {
      ghost: game.ghostMode,
      invincible: game.isInvincible,
      badge: document.querySelector("#ghost-badge")?.hidden,
    };

    game.toggleGhostMode();
    game.player.update?.(0.016);
    const enemyState = game.enemyManager.update(0.05, {
      position: game.player.position,
      isHidden: false,
      isUndetectable: game.isInvincible,
      isMoving: true,
      isSprinting: true,
    });

    enemy.caughtPlayer = true;
    game.handleCaught("test");

    const on = {
      ghost: game.ghostMode,
      invincible: game.isInvincible,
      undetectablePassed: game.isInvincible === true,
      bodyClass: document.body.classList.contains("ghost-mode"),
      badgeVisible: document.querySelector("#ghost-badge")?.hidden === false,
      canvasFaded: Number.parseFloat(getComputedStyle(canvas).opacity) < 0.8,
      gameOver: game.gameOver,
      caughtDuringGhost: Boolean(enemyState?.caught),
      noclip: game.player.noclip,
    };

    game.toggleGhostMode();
    const off = {
      ghost: game.ghostMode,
      invincible: game.isInvincible,
      bodyClass: document.body.classList.contains("ghost-mode"),
      badgeHidden: document.querySelector("#ghost-badge")?.hidden === true,
    };

    game.input.pressedThisFrame.add("`");
    game.update(0.016);
    const viaKey = game.ghostMode === true;

    return { before, on, off, viaKey };
  });

  assert.equal(result.before.ghost, false);
  assert.equal(result.on.ghost, true);
  assert.equal(result.on.invincible, true);
  assert.equal(result.on.bodyClass, true);
  assert.equal(result.on.badgeVisible, true);
  assert.equal(result.on.canvasFaded, true);
  assert.equal(result.on.gameOver, false);
  assert.equal(result.on.noclip, false, "backtick ghost mode must not enable noclip");
  assert.equal(result.off.ghost, false);
  assert.equal(result.off.bodyClass, false);
  assert.equal(result.off.badgeHidden, true);
  assert.equal(result.viaKey, true);
  assert.deepEqual(errors, []);
  console.log("PASS: ghost/invincible mode", result);
} finally {
  await browser.close();
}
