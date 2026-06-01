import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/?debug=1";

console.log("Capturing screenshots of Lovely Doll...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 8000 });

  // 1. Spawning Lovely Doll and look at it in DANCE state
  await page.evaluate(() => {
    const game = window.__happyToy;
    // Spawn doll at player position so we can see it clearly
    // The player is at (0, 0, 0)
    // Teleport player back a bit so we look at the doll
    game.player.setPosition({ x: 0, y: 0, z: -2.5 });
    game.player.resetLook(0, 0); // look forward (north)
    game.player.updateCamera(0.016);
    
    // Find or create lovely doll in the start chunk
    // Actually, lovely dolls spawn in start chunk. Let's force spawn one at (0, 0, 1.5)
    let doll = game.lovelyDolls[0];
    if (!doll) {
      // Create a mock doll if none spawned
      console.log("No doll spawned, spawning one");
    }
    
    doll.group.position.set(0, 0, 1.5);
    doll.group.rotation.set(0, Math.PI, 0); // face player
    doll.state = "dance";
    doll.playAction("dance", 0);
    
    // Let mixer update and snap
    for (let i = 0; i < 20; i++) {
      doll.mixer?.update(0.033);
      doll.snapModelToGround();
    }
  });

  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: "verification/doll-dance.png" });
  console.log("Captured verification/doll-dance.png");

  // 2. Lovely Doll in WALKING state
  await page.evaluate(() => {
    const game = window.__happyToy;
    const doll = game.lovelyDolls[0];
    doll.state = "walking";
    doll.playAction("walking", 0);
    for (let i = 0; i < 20; i++) {
      doll.mixer?.update(0.033);
      doll.snapModelToGround();
    }
  });

  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: "verification/doll-walking.png" });
  console.log("Captured verification/doll-walking.png");

  // 3. Lovely Doll in RUN state
  await page.evaluate(() => {
    const game = window.__happyToy;
    const doll = game.lovelyDolls[0];
    doll.state = "run";
    doll.playAction("run", 0);
    for (let i = 0; i < 20; i++) {
      doll.mixer?.update(0.033);
      doll.snapModelToGround();
    }
  });

  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: "verification/doll-run.png" });
  console.log("Captured verification/doll-run.png");

} catch (err) {
  console.error("Error during doll capture:", err);
} finally {
  await browser.close();
}
