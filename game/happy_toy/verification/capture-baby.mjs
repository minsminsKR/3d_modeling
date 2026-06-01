import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/?debug=1";

console.log("Capturing screenshots of Baby in crying and crawling states...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 8000 });

  // Teleport player near workshop baby
  await page.evaluate(() => {
    const game = window.__happyToy;
    // Baby is spawned at [32.5, 0, 31.0]
    // Teleport player to look at it
    game.player.setPosition({ x: 32.5, y: 0, z: 27.5 });
    // Look south (toward baby)
    game.player.resetLook(Math.PI, 0);
    game.player.updateCamera(0.016);
    game.updateBackrooms(0.016);
  });

  // Wait for rendering
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: "verification/baby-crying.png" });
  console.log("Captured verification/baby-crying.png");

  // Now, wake up the baby and let it crawl toward the player
  await page.evaluate(() => {
    const game = window.__happyToy;
    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Trigger wake up
    baby.babyAwake = true;
    baby.state = "chase";
    baby.playAction("chase", 0);
    
    // Tick game a few times to let animation advance and position update
    for (let i = 0; i < 30; i++) {
      game.update(0.033);
    }
  });

  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: "verification/baby-crawling.png" });
  console.log("Captured verification/baby-crawling.png");

} catch (err) {
  console.error("Error during screenshot capture:", err);
} finally {
  await browser.close();
}
