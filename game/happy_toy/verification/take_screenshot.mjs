import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

async function run() {
  console.log("Launching browser...");
  const browser = await chromium.launch({
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
    headless: true,
  });

  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  
  page.on("console", (msg) => {
    console.log(`[BROWSER CONSOLE] ${msg.type()}: ${msg.text()}`);
  });
  page.on("pageerror", (err) => {
    console.error(`[BROWSER ERROR] ${err.message}`);
  });

  try {
    console.log("Navigating to game...");
    await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });

    console.log("Waiting for game to load...");
    await page.waitForFunction(
      () => window.__happyToy?.assetsReady === true,
      null,
      { timeout: 5000 }
    );

    console.log("Taking start screen screenshot...");
    await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/screenshot_start.png" });

    console.log("Starting game...");
    await page.evaluate(() => {
      window.__happyToy.start();
    });

    console.log("Waiting 2 seconds...");
    await page.waitForTimeout(2000);

    console.log("Taking gameplay screenshot...");
    await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/screenshot_gameplay.png" });

    console.log("Toggling flashlight ON (pressing 'F')...");
    await page.keyboard.press("KeyF");
    await page.waitForTimeout(1000);

    console.log("Taking gameplay with flashlight screenshot...");
    await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/screenshot_flashlight.png" });

  } catch (error) {
    console.error("Error during screenshot script:", error);
  } finally {
    await browser.close();
    console.log("Browser closed.");
  }
}

run();
