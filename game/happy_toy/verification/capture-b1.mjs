import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Launching browser to capture B1 Nursery screenshot...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 15000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

  await page.evaluate(() => {
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

    const game = window.__happyToy;
    // Position player directly in B1 Nursery doorway looking at baby
    game.player.setPosition({ x: 14.5, y: -5.0, z: 32.0 });
    game.player.resetLook(Math.PI * 0.78, -0.12);
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
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: "verification/screenshot-b1-nursery.png" });
  console.log("Captured clean verification/screenshot-b1-nursery.png");
} catch (err) {
  console.error(err);
} finally {
  await browser.close();
}
