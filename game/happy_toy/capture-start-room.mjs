import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";
const artifactDir = "C:/Users/sanguk/.gemini/antigravity/brain/2673541e-43f3-4ea8-8fd2-45c4e87bcc77/";

console.log("Launching browser to capture screenshots...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 5000 });
  
  // Click start button
  console.log("Clicking start button...");
  await page.click("#start-button");
  await page.waitForTimeout(1000); // Wait for transition
  
  // Take screenshot looking forward (North)
  console.log("Capturing North...");
  await page.screenshot({ path: artifactDir + "start-north.png" });
  
  // Rotate player camera to look East (90 degrees right)
  console.log("Rotating to East...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.yaw = -Math.PI / 2; // East rotation
    game.player.updateCamera();
  });
  await page.waitForTimeout(200);
  await page.screenshot({ path: artifactDir + "start-east.png" });
  
  // Rotate player camera to look South (180 degrees back)
  console.log("Rotating to South...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.yaw = Math.PI; // South rotation
    game.player.updateCamera();
  });
  await page.waitForTimeout(200);
  await page.screenshot({ path: artifactDir + "start-south.png" });
  
  // Rotate player camera to look West (270 degrees left)
  console.log("Rotating to West...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.yaw = Math.PI / 2; // West rotation
    game.player.updateCamera();
  });
  await page.waitForTimeout(200);
  await page.screenshot({ path: artifactDir + "start-west.png" });
  
  console.log("All screenshots captured successfully!");
} catch (err) {
  console.error("Failed to capture:", err);
} finally {
  await browser.close();
}
