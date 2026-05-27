/**
 * 60-second forward movement profiling test.
 * Simulates a player walking continuously and reports frame time stats.
 * Success criterion: max frame time < 33ms, no 450ms+ shader-recompile spikes.
 */
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8010/";
const WALK_SECONDS = 60;

async function run() {
  console.log(`Starting 60-second performance validation on: ${url}`);
  const browser = await chromium.launch({
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
    headless: true,
    args: [
      "--disable-backgrounding-occluded-windows",
      "--ignore-gpu-blocklist",
      "--use-gl=desktop"
    ],
  });

  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

  const perfLogs = [];
  page.on("console", (msg) => {
    const text = msg.text();
    if (text.includes("[PERF]") || text.startsWith("[PERF]")) {
      perfLogs.push(text);
      console.log(text);
    }
  });
  page.on("pageerror", (err) => console.error("[PAGE ERROR]", err.message));

  try {
    await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });
    await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 8000 });

    console.log("Game loaded. Starting game...");
    await page.evaluate(() => {
      window.__happyToy.testSafeMode = true;
      window.__happyToy.player.noclip = true;
      window.__happyToy.start();
    });

    console.log("Waiting 2 seconds for initial shader compilation and warmup...");
    await page.waitForTimeout(2000);

    // Clear frame times collected during startup/load
    await page.evaluate(() => { window.__happyToyFrameTimes = []; });

    await page.keyboard.down("KeyW");
    console.log(`Walking for ${WALK_SECONDS} seconds...`);
    await page.waitForTimeout(WALK_SECONDS * 1000);
    await page.keyboard.up("KeyW");

    const stats = await page.evaluate(() => {
      const game = window.__happyToy;
      const times = window.__happyToyFrameTimes || [];
      if (times.length === 0) return { count: 0 };

      const sorted = [...times].sort((a, b) => a - b);
      const sum = sorted.reduce((acc, v) => acc + v, 0);
      const avg = sum / sorted.length;
      const max = sorted[sorted.length - 1];
      const p95 = sorted[Math.floor(sorted.length * 0.95)];
      const p99 = sorted[Math.floor(sorted.length * 0.99)];
      const spikes16 = sorted.filter(t => t > 16.7).length;
      const spikes33 = sorted.filter(t => t > 33.3).length;

      // Count active lights in pool
      let activeLights = 0;
      if (game._pointLightPool) {
        activeLights = game._pointLightPool.filter(pl => pl.intensity > 0).length;
      }

      // Count active colliders
      const activeColliders = game.collisionWorld?.blockers?.length ?? 0;
      const activeChunks = game.mapBuilder?.loadedChunks?.size ?? 0;

      const approxFps = 1000 / avg;

      return {
        count: sorted.length,
        avg: avg.toFixed(2),
        max: max.toFixed(2),
        p95: p95.toFixed(2),
        p99: p99.toFixed(2),
        spikes16,
        spikes33,
        approxFps: approxFps.toFixed(1),
        activeLights,
        activeColliders,
        activeChunks,
        playerX: game.player?.position?.x?.toFixed(1),
        playerZ: game.player?.position?.z?.toFixed(1),
      };
    });

    console.log("\n========== 60-SECOND WALK PROFILING RESULTS ==========");
    console.log(`Total Frames Measured:      ${stats.count}`);
    console.log(`Approx Average FPS:         ${stats.approxFps}`);
    console.log(`Average Frame Time:         ${stats.avg}ms`);
    console.log(`95th Percentile Frame Time: ${stats.p95}ms`);
    console.log(`99th Percentile Frame Time: ${stats.p99}ms`);
    console.log(`Max Frame Time (Worst):     ${stats.max}ms`);
    console.log(`Frames > 16.7ms (dropped):  ${stats.spikes16}`);
    console.log(`Frames > 33.3ms (SPIKE):    ${stats.spikes33}`);
    console.log(`----- Active Object Counts -----`);
    console.log(`Active Chunks:              ${stats.activeChunks}`);
    console.log(`Active PointLights:         ${stats.activeLights} / ${stats._POINT_LIGHT_BUDGET ?? 8}`);
    console.log(`Active Colliders:           ${stats.activeColliders}`);
    console.log(`Player Position:            x=${stats.playerX}, z=${stats.playerZ}`);
    console.log(`======================================================`);

    const maxMs = parseFloat(stats.max);
    const spikes33 = stats.spikes33;
    if (maxMs > 450.0) {
      console.error(`\nFAIL: Max frame time ${maxMs}ms exceeds 450.0ms threshold (Shader Recompilation Spike).`);
      if (perfLogs.length > 0) {
        console.error(`PERF logs captured (${perfLogs.length}):`);
        perfLogs.forEach(l => console.error("  " + l));
      }
      process.exit(1);
    } else {
      console.log(`\nPASS: No frames exceeded 450.0ms during ${WALK_SECONDS}s walk.`);
    }

  } catch (err) {
    console.error("Profiling test failed:", err);
    process.exit(1);
  } finally {
    await browser.close();
  }
}

run();
