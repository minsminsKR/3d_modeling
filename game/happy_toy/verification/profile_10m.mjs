/**
 * 10-minute forward movement profiling and memory leak validation test.
 * Simulates a player walking continuously and reports frame time stats,
 * and monitors Three.js WebGL memory (Geometries, Textures, etc.) and JS Heap.
 */
import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const url = process.argv[2] || "http://127.0.0.1:8010/";
const WALK_SECONDS = 600; // 10 minutes

async function run() {
  console.log(`Starting 10-minute performance profile & leak check on: ${url}`);
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

  const metricsHistory = [];
  
  page.on("console", (msg) => {
    const text = msg.text();
    if (text.startsWith("[PERF_METRICS]")) {
      try {
        const jsonStr = text.replace("[PERF_METRICS] ", "");
        const metrics = JSON.parse(jsonStr);
        metricsHistory.push(metrics);
        console.log(`[Metric - ${metrics.time}s] FPS: ${metrics.fps}, Geo: ${metrics.geometries}, Tex: ${metrics.textures}, DrawCalls: ${metrics.drawCalls}, Triangles: ${metrics.triangles}, Heap: ${(metrics.heap / 1024 / 1024).toFixed(2)}MB, Colliders: ${metrics.colliders}, Monsters: ${metrics.monsters}`);
      } catch (e) {
        console.error("Failed to parse metric:", text, e);
      }
    }
  });

  page.on("pageerror", (err) => console.error("[PAGE ERROR]", err.message));

  try {
    await page.goto(url, { waitUntil: "networkidle", timeout: 15000 });
    await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

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

    const interval = 10;
    for (let elapsed = 0; elapsed < WALK_SECONDS; elapsed += interval) {
      await page.waitForTimeout(interval * 1000);
      console.log(`Progress: ${elapsed + interval}/${WALK_SECONDS} seconds elapsed...`);
    }

    await page.keyboard.up("KeyW");

    // Gather statistics
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

      let activeLights = 0;
      if (game._pointLightPool) {
        activeLights = game._pointLightPool.filter(pl => pl.intensity > 0).length;
      }

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

    console.log("\n========== 10-MINUTE WALK PROFILING RESULTS ==========");
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
    console.log(`Active PointLights:         ${stats.activeLights}`);
    console.log(`Active Colliders:           ${stats.activeColliders}`);
    console.log(`Player Position:            x=${stats.playerX}, z=${stats.playerZ}`);
    console.log(`======================================================`);

    if (metricsHistory.length === 0) {
      throw new Error("No metrics history recorded!");
    }

    const halfIndex = Math.floor(metricsHistory.length / 2);
    const middleMetrics = metricsHistory.slice(halfIndex);
    const maxGeo = Math.max(...middleMetrics.map(m => m.geometries));
    const minGeo = Math.min(...middleMetrics.map(m => m.geometries));
    const maxTex = Math.max(...middleMetrics.map(m => m.textures));
    const minTex = Math.min(...middleMetrics.map(m => m.textures));
    const maxColliders = Math.max(...middleMetrics.map(m => m.colliders));
    const minColliders = Math.min(...middleMetrics.map(m => m.colliders));

    console.log(`Second Half Stability:`);
    console.log(`  Geometries: min=${minGeo}, max=${maxGeo} (delta=${maxGeo - minGeo})`);
    console.log(`  Textures:   min=${minTex}, max=${maxTex} (delta=${maxTex - minTex})`);
    console.log(`  Colliders:  min=${minColliders}, max=${maxColliders} (delta=${maxColliders - minColliders})`);

    const heaps = metricsHistory.map(m => m.heap);
    const maxHeap = Math.max(...heaps);
    const minHeap = Math.min(...heaps);
    console.log(`JS Heap Range: ${(minHeap/1024/1024).toFixed(1)}MB to ${(maxHeap/1024/1024).toFixed(1)}MB`);

    // Write report
    let report = `10-MINUTE WALK PROFILING REPORT
==================================
Date: ${new Date().toISOString()}
Total Frames Measured: ${stats.count}
Approx Average FPS: ${stats.approxFps}
Average Frame Time: ${stats.avg}ms
95th Percentile Frame Time: ${stats.p95}ms
99th Percentile Frame Time: ${stats.p99}ms
Max Frame Time (Worst): ${stats.max}ms
Frames > 16.7ms (dropped): ${stats.spikes16}
Frames > 33.3ms (SPIKE): ${stats.spikes33}

Active Objects (End of Run):
  Chunks: ${stats.activeChunks}
  PointLights: ${stats.activeLights}
  Colliders: ${stats.activeColliders}
  Player Position: x=${stats.playerX}, z=${stats.playerZ}

Stability Analysis (Second Half of Run):
  Geometries: min=${minGeo}, max=${maxGeo} (delta=${maxGeo - minGeo})
  Textures: min=${minTex}, max=${maxTex} (delta=${maxTex - minTex})
  Colliders: min=${minColliders}, max=${maxColliders} (delta=${maxColliders - minColliders})
  JS Heap: min=${(minHeap/1024/1024).toFixed(1)}MB, max=${(maxHeap/1024/1024).toFixed(1)}MB

Metrics History:
${metricsHistory.map(m => `Time: ${m.time}s, FPS: ${m.fps}, Geo: ${m.geometries}, Tex: ${m.textures}, DrawCalls: ${m.drawCalls}, Triangles: ${m.triangles}, Heap: ${(m.heap/1024/1024).toFixed(1)}MB, Colliders: ${m.colliders}`).join("\n")}
`;

    const reportPath = path.join(__dirname, "profile_10m_report.txt");
    fs.writeFileSync(reportPath, report);
    console.log(`Report written to ${reportPath}`);

    // Assertion check for resource leak
    const secondHalfGeos = middleMetrics.map(m => m.geometries);
    let isMonotonicGeo = true;
    for (let i = 1; i < secondHalfGeos.length; i++) {
      if (secondHalfGeos[i] < secondHalfGeos[i-1]) {
        isMonotonicGeo = false;
        break;
      }
    }
    const isLeakingGeo = isMonotonicGeo && (secondHalfGeos[secondHalfGeos.length - 1] > secondHalfGeos[0]);

    if (isLeakingGeo) {
      console.error(`\nFAIL: Geometries appear to be leaking (monotonically increasing in second half: ${secondHalfGeos.slice(0, 5)}... -> ${secondHalfGeos.slice(-5)})`);
      process.exit(1);
    }

    console.log(`\nPASS: 10-minute profiling completed successfully.`);
    process.exit(0);

  } catch (err) {
    console.error("Profiling test failed:", err);
    process.exit(1);
  } finally {
    await browser.close();
  }
}

run();
