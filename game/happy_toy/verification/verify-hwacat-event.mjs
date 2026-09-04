import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";


console.log("Launching browser to verify Mirror Hwacat Event...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const consoleLogs = [];
const browserErrors = [];

page.on("console", (message) => {
  const text = message.text();
  consoleLogs.push(text);
  if (message.type() === "error") {
    browserErrors.push(text);
  }
});
page.on("pageerror", (error) => browserErrors.push(error.message));

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });

  // Wait for game initialization
  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 8000 }
  );

  console.log("Game initialized. Teleporting player to 2F Gallery (-19.0, 5.0, -16.0)...");
  
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.start();
    // Teleport player to 2F Gallery which triggers the Hwacat event
    // The trigger position is [-19.0, 5.0, -16.0] with 2.2m radius
    game.player.setPosition({ x: -19.0, y: 5.0, z: -16.0 });
    game.updateBackrooms(0.016);
  });

  console.log("Monitoring Hwacat Event state for 20 seconds...");

  for (let i = 0; i < 20; i++) {
    await new Promise((resolve) => setTimeout(resolve, 1000));
    const eventStatus = await page.evaluate(() => {
      const game = window.__happyToy;
      const event = game.mirrorEvents[0];
      const angryHwacat = game.enemyManager.enemies.find(e => e.config.id === "hwacat-angry");
      return {
        hasTriggered: event.hasTriggered,
        state: event.state,
        timer: event.timer.toFixed(2),
        isTransforming: event.isTransforming,
        angryHwacatExists: Boolean(angryHwacat),
        angryHwacatState: angryHwacat ? angryHwacat.state : "none",
        angryHwacatPos: angryHwacat ? { x: angryHwacat.group.position.x, z: angryHwacat.group.position.z } : null,
      };
    });

    console.log(`Sec ${i + 1}: State=${eventStatus.state}, Timer=${eventStatus.timer}, Triggered=${eventStatus.hasTriggered}, Transforming=${eventStatus.isTransforming}, AngryExists=${eventStatus.angryHwacatExists} (State: ${eventStatus.angryHwacatState})`);

    if (eventStatus.state === "done") {
      console.log("Event completed successfully to 'done' state!");
      break;
    }
  }

  console.log("\n--- Browser Console Logs ---");
  consoleLogs.slice(-25).forEach(log => console.log(`[BROWSER] ${log}`));

  if (browserErrors.length > 0) {
    console.error("\n--- Browser Errors Detected ---");
    browserErrors.forEach(err => console.error(`[ERROR] ${err}`));
  }

} catch (error) {
  console.error("Verification failed:", error);
} finally {
  await browser.close();
}
