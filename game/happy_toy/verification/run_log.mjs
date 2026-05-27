import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");
import * as fs from "node:fs";

const url = "http://127.0.0.1:8010/";
const logStream = fs.createWriteStream("E:/AI/3d_modeling/game/happy_toy/verification/console_logs.txt");

async function run() {
  logStream.write("Launching browser...\n");
  const browser = await chromium.launch({
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
    headless: true,
  });

  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  
  page.on("console", (msg) => {
    logStream.write(`[CONSOLE ${msg.type().toUpperCase()}] ${msg.text()}\n`);
  });
  page.on("pageerror", (err) => {
    logStream.write(`[PAGE ERROR] ${err.stack || err.message}\n`);
  });
  page.on("requestfailed", (req) => {
    logStream.write(`[REQUEST FAILED] ${req.url()} - ${req.failure()?.errorText}\n`);
  });

  try {
    logStream.write("Navigating to game...\n");
    await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });

    logStream.write("Waiting for game to load...\n");
    await page.waitForFunction(
      () => window.__happyToy?.assetsReady === true,
      null,
      { timeout: 5000 }
    );

    logStream.write("Starting game...\n");
    await page.evaluate(() => {
      window.__happyToy.start();
    });

    logStream.write("Waiting 3 seconds...\n");
    await page.waitForTimeout(3000);

    logStream.write("Done.\n");
  } catch (error) {
    logStream.write(`Error: ${error.stack || error.message}\n`);
  } finally {
    await browser.close();
    logStream.write("Browser closed.\n");
    logStream.end();
    console.log("Log capture finished.");
  }
}

run();
