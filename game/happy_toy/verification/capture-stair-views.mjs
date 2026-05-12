import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";
const outDir = process.argv[3] || "C:/Users/Public/Documents/ESTsoft/CreatorTemp/happy_toy_playtest/stair_views";

const views = [
  {
    name: "01_2f_arrival_front_wall.png",
    position: { x: -7.05, y: 3.4, z: 9.72 },
    yaw: 0,
    pitch: -0.03,
  },
  {
    name: "02_1f_stair_side_gap_closed.png",
    position: { x: -2.65, y: 0, z: 21.7 },
    yaw: 0.22,
    pitch: -0.08,
  },
  {
    name: "03_2f_look_back_closed_sides.png",
    position: { x: -7.05, y: 3.4, z: 9.85 },
    yaw: Math.PI,
    pitch: -0.06,
  },
  {
    name: "04_2f_turn_to_corridor_clear.png",
    position: { x: -5.6, y: 3.4, z: 9.9 },
    yaw: -Math.PI / 2,
    pitch: -0.04,
  },
];

await mkdir(outDir, { recursive: true });

const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 800, height: 609 } });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.enemyManager?.enemies?.length === 2, null, { timeout: 90000 });
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.isStarted = true;
    game.isPaused = false;
    game.hud.hideStart();
    game.hud.hidePause();
    game.hud.setStatus("", 1);
  });

  for (const view of views) {
    await page.evaluate((viewConfig) => {
      const game = window.__happyToy;
      game.player.setPosition(viewConfig.position);
      game.player.resetLook(viewConfig.yaw, viewConfig.pitch);
      game.camera.rotation.set(viewConfig.pitch, viewConfig.yaw, 0, "YXZ");

      game.flashlightController.enabled = true;
      game.flashlightController.applyState(false);

      for (const enemy of game.enemyManager.enemies) {
        enemy.group.visible = false;
      }

      game.renderer.render(game.scene, game.camera);
    }, view);

    await page.waitForTimeout(120);
    await page.screenshot({ path: join(outDir, view.name), fullPage: false });
  }

  console.log(JSON.stringify({ ok: true, outDir, files: views.map((view) => view.name) }, null, 2));
} finally {
  await browser.close();
}
