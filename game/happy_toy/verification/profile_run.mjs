import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

async function run() {
  console.log("Launching browser for profiling...");
  const browser = await chromium.launch({
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
    headless: true,
  });

  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  
  const perfLogs = [];
  page.on("console", (msg) => {
    const text = msg.text();
    if (text.includes("[PERF]") || text.includes("warning")) {
      console.log(text);
      perfLogs.push(text);
    }
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

    console.log("Starting game...");
    await page.evaluate(() => {
      window.__happyToy.start();
    });

    console.log("Simulating forward movement...");
    await page.keyboard.down("KeyW");

    // Walk for 15 seconds to trigger multiple chunk crossings
    await page.waitForTimeout(15000);

    await page.keyboard.up("KeyW");
    console.log("Stopped movement. Collecting stats...");

    const stats = await page.evaluate(() => {
      const times = window.__happyToyFrameTimes || [];
      if (times.length === 0) return { count: 0 };
      
      const sorted = [...times].sort((a, b) => a - b);
      const sum = sorted.reduce((acc, v) => acc + v, 0);
      const avg = sum / sorted.length;
      const max = sorted[sorted.length - 1];
      const p95 = sorted[Math.floor(sorted.length * 0.95)];
      
      return {
        count: sorted.length,
        avg,
        max,
        p95,
      };
    });

    console.log("--- PROFILING RESULTS ---");
    console.log(`Total Frames: ${stats.count}`);
    console.log(`Average Frame Time: ${stats.avg?.toFixed(2)}ms`);
    console.log(`95th Percentile Frame Time: ${stats.p95?.toFixed(2)}ms`);
    console.log(`Max Frame Time (Worst Spike): ${stats.max?.toFixed(2)}ms`);
    console.log("-------------------------");

  } catch (error) {
    console.error("Error during profiling run:", error);
  } finally {
    await browser.close();
    console.log("Browser closed.");
  }
}

run();
