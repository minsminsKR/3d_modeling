import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Launching browser to capture clean visual screenshots...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 15000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

  await page.evaluate(() => {
    // Start game & dismiss all menus
    window.__happyToy.start();
    const btn = document.getElementById("btn-start-game");
    if (btn) btn.click();
    const menuRoot = document.getElementById("menu-system-root");
    if (menuRoot) menuRoot.style.display = "none";
    const startScreen = document.querySelector("#start-screen");
    if (startScreen) {
      startScreen.classList.add("hidden");
      startScreen.style.display = "none";
    }
  });
  await new Promise(r => setTimeout(r, 600));

  // 1. Screenshot 2F Gallery
  console.log("Capturing 2F Gallery screenshot...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: -18.5, y: 5.0, z: -16.0 });
    game.player.resetLook(Math.PI / 2, -0.05); // Look West towards painting and shrine
    if (!game.flashlightController.enabled) {
      game.flashlightController.toggle();
    }
    game.flashlight.intensity = 35.0;
    game.flashlight.visible = true;
    for (const sl of game.safeLights) {
      if (sl.position.y > 4.0) {
        sl.setActivated(true);
      }
    }
    game.updateBackrooms(0.016);
  });
  await new Promise(r => setTimeout(r, 800));
  await page.screenshot({ path: "verification/screenshot-2f-gallery.png" });

  // 2. Screenshot B1 Nursery
  console.log("Capturing B1 Nursery screenshot...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: 13.5, y: -5.0, z: 32.0 });
    game.player.resetLook(Math.PI * 0.75, -0.15); // Look towards tatami corner where Baby is
    if (!game.flashlightController.enabled) {
      game.flashlightController.toggle();
    }
    game.flashlight.intensity = 35.0;
    game.flashlight.visible = true;
    for (const sl of game.safeLights) {
      if (sl.position.y < -3.0) {
        sl.setActivated(true);
      }
    }
    game.updateBackrooms(0.016);
  });
  await new Promise(r => setTimeout(r, 800));
  await page.screenshot({ path: "verification/screenshot-b1-nursery.png" });

  // 3. Screenshot Cyclops Corner Emergence
  console.log("Capturing Cyclops Corner Emergence screenshot...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    const intro = game.monsterIntroManager.events.find(e => e.constructor.name === "CyclopseIntroEvent");

    game.player.setPosition({ x: 14.0, y: 0.0, z: 0.0 });
    intro.checkTrigger();
    // Advance to Phase 2 where Cyclops has stepped out to [27.5, 0, 0]
    for (let i = 0; i < 90; i++) {
      intro.update(0.016);
    }
    if (!game.flashlightController.enabled) {
      game.flashlightController.toggle();
    }
    game.flashlight.intensity = 35.0;
    game.flashlight.visible = true;
  });
  await new Promise(r => setTimeout(r, 800));
  await page.screenshot({ path: "verification/screenshot-cyclops-corner.png" });

  // 4. Screenshot Uncat Corner Emergence
  console.log("Capturing Uncat Corner Emergence screenshot...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    const intro = game.monsterIntroManager.events.find(e => e.constructor.name === "UncatIntroEvent");

    game.player.setPosition({ x: 0.0, y: 0.0, z: 14.0 });
    intro.checkTrigger();
    // Advance to Phase 2 where Uncat has stepped into the intersection [0, 0, 25]
    for (let i = 0; i < 90; i++) {
      intro.update(0.016);
    }
    if (!game.flashlightController.enabled) {
      game.flashlightController.toggle();
    }
    game.flashlight.intensity = 35.0;
    game.flashlight.visible = true;
  });
  await new Promise(r => setTimeout(r, 800));
  await page.screenshot({ path: "verification/screenshot-uncat-corner.png" });

  console.log("All screenshots captured successfully!");
} catch (err) {
  console.error("Screenshot capture failed:", err);
} finally {
  await browser.close();
}
