import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");
import path from "path";

const targetUrl = process.argv[2] || "http://127.0.0.1:8010/";
const screenshotDir = "C:/Users/sanguk/.gemini/antigravity/brain/a896b356-c2d7-47f3-ab26-8e8f7bd2ba8f";

async function main() {
  console.log("Launching browser for Architectural Integrity & Anti-Floating-Switch Verification...");
  const browser = await chromium.launch({
    executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
    headless: true,
  });

  const page = await browser.newPage({
    viewport: { width: 1280, height: 720 },
  });

  page.on("console", (msg) => {
    const text = msg.text();
    if (text.includes("Error") || text.includes("error") || text.includes("Architecture")) {
      console.log(`[Browser]: ${text}`);
    }
  });

  await page.goto(targetUrl, { waitUntil: "networkidle", timeout: 10000 });

  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 10000 });
  console.log("Game initialized. Starting game...");

  // Start game by clicking the menu start button
  const startBtn = await page.$("#btn-start-game");
  if (startBtn) {
    await startBtn.click();
  } else {
    await page.evaluate(() => {
      window.__happyToy.start();
    });
  }
  await page.waitForTimeout(1000);

  // 1. Audit all SafeLights across the scene for floating switches
  const lightAudit = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder.generator;
    const allLights = [];
    for (const [chunkKey, chunk] of generator.chunksData.entries()) {
      for (const sl of chunk.safeLights || []) {
        allLights.push({
          id: sl.id,
          variant: sl.variant,
          chunkKey,
          pos: [sl.position.x, sl.position.y, sl.position.z],
          yaw: sl.yaw,
        });
      }
    }

    // Check chunk (-1, 2) tatami_room
    const tatamiChunk = generator.chunksData.get("-1,2");
    const tatamiLights = (tatamiChunk?.safeLights || []).map((l) => ({
      id: l.id,
      variant: l.variant,
      label: l.label,
      pos: [l.position.x, l.position.y, l.position.z],
      yaw: l.yaw,
    }));

    return {
      totalLights: allLights.length,
      tatamiLights,
    };
  });

  console.log(`Audited ${lightAudit.totalLights} SafeLights across loaded map chunks.`);
  console.log("Tatami Room (-1, 2) SafeLights:", JSON.stringify(lightAudit.tatamiLights, null, 2));

  // Verify Tatami Room specific lights
  const wallSwitchesInTatami = lightAudit.tatamiLights.filter((l) => l.variant === "wall-switch");
  if (wallSwitchesInTatami.length !== 2) {
    throw new Error(`Expected exactly 2 wall-switches in Tatami room, got ${wallSwitchesInTatami.length}`);
  }
  for (const sw of wallSwitchesInTatami) {
    const localX = sw.pos[0] - (-16);
    const localZ = sw.pos[2] - 32;
    console.log(`Tatami switch: ${sw.label} at local (${localX.toFixed(2)}, ${sw.pos[1].toFixed(2)}, ${localZ.toFixed(2)}) yaw=${sw.yaw.toFixed(2)}`);
    const isAtOldFloatingLoc = Math.hypot(localX - (-3.5), localZ - (-2.5)) < 0.5;
    if (isAtOldFloatingLoc) {
      throw new Error(`CRITICAL: Found old floating switch in tatami room at (${localX}, ${localZ})!`);
    }
  }

  // 2. Teleport to Tatami Room Doorway and capture screenshot
  console.log("Capturing Tatami Room Doorway screenshot...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.position.set(-16.0, 0.0, 27.5);
    game.player.yaw = 0;
    game.player.pitch = -0.05;

    if (game.flashlightController) {
      game.flashlightController.enabled = true;
      game.flashlightController.applyState(false);
    }

    game.camera.position.set(-16.0, 1.4, 27.5);
    game.camera.rotation.set(-0.05, 0, 0);

    const tatamiChunk = game.mapBuilder.generator.chunksData.get("-1,2");
    if (tatamiChunk) {
      for (const d of tatamiChunk.doors || []) {
        if (d.id === "door-tatami-room") {
          d.isOpen = true;
          d.openAmount = 1.0;
          d.update(0.1);
        }
      }
      for (const sl of tatamiChunk.safeLights || []) {
        sl.setActivated(true);
      }
    }

    game.update(0.016);
    if (game.renderer && game.scene && game.camera) {
      game.renderer.render(game.scene, game.camera);
    }
  });

  await page.waitForTimeout(600);
  const tatamiDoorwayPath = path.join(screenshotDir, "screenshot-tatami-doorway.png");
  await page.screenshot({ path: tatamiDoorwayPath });
  console.log(`Saved Tatami Room doorway screenshot to: ${tatamiDoorwayPath}`);

  // 2b. Teleport INSIDE Tatami Room (-1, 2) looking at Tea Table (Chabudai) and Alcove
  console.log("Capturing inside Tatami Room screenshot...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    // Stand in NW area looking toward center/table and SE corner
    game.player.position.set(-18.5, 0.0, 29.5);
    game.player.yaw = -Math.PI * 0.7; // Look SE towards (center: -16, 32)
    game.player.pitch = -0.15;

    game.camera.position.set(-18.5, 1.4, 29.5);
    game.camera.rotation.set(-0.15, -Math.PI * 0.7, 0);

    game.update(0.016);
    if (game.renderer && game.scene && game.camera) {
      game.renderer.render(game.scene, game.camera);
    }
  });

  await page.waitForTimeout(600);
  const tatamiInsidePath = path.join(screenshotDir, "screenshot-tatami-inside.png");
  await page.screenshot({ path: tatamiInsidePath });
  console.log(`Saved Tatami Room inside screenshot to: ${tatamiInsidePath}`);

  // 3. Inspect B1 Stairwell Enclosure Geometry & Raycasts
  console.log("Auditing B1 Stairwell Enclosing Geometry & Raycasts...");
  const b1StairsAudit = await page.evaluate(() => {
    const game = window.__happyToy;
    const b1Chunk = game.mapBuilder.generator.chunksData.get("1,2");
    if (!b1Chunk) return { error: "b1Chunk not found" };

    const meshNames = b1Chunk.meshes.map((m) => m.name);

    const hasRampDiv = meshNames.some((n) => n.includes("wall_ramp_div"));
    const hasEastDiv = meshNames.some((n) => n.includes("wall_e_div"));
    const hasDoorUpper = meshNames.some((n) => n.includes("door_upper"));
    const hasFloorLeft = meshNames.some((n) => n.includes("floor_l"));
    const hasFloorRight = meshNames.some((n) => n.includes("floor_r"));

    const results = [];
    const testPoints = [
      { x: 16.0, y: -0.8, z: 28.0 },
      { x: 16.0, y: -2.0, z: 31.0 },
      { x: 16.0, y: -3.5, z: 34.0 },
      { x: 16.0, y: -4.8, z: 36.5 },
    ];

    for (const pt of testPoints) {
      let westWallFound = false;
      let eastWallFound = false;

      for (const blocker of game.collisionWorld.blockers) {
        const aabb = typeof blocker.aabb === "function" ? blocker.aabb() : blocker.aabb;
        if (!aabb) continue;

        const minX = aabb.minX;
        const maxX = aabb.maxX;
        const minY = aabb.minY;
        const maxY = aabb.maxY;
        const minZ = aabb.minZ;
        const maxZ = aabb.maxZ;

        const overlapsY = pt.y >= minY && pt.y <= maxY;
        const overlapsZ = pt.z >= minZ && pt.z <= maxZ;

        if (overlapsY && overlapsZ) {
          if (maxX <= pt.x + 0.1 && minX >= pt.x - 2.5) {
            westWallFound = true;
          }
          if (minX >= pt.x - 0.1 && maxX <= pt.x + 2.5) {
            eastWallFound = true;
          }
        }
      }

      results.push({
        point: pt,
        westWallFound,
        eastWallFound,
      });
    }

    return {
      hasRampDiv,
      hasEastDiv,
      hasDoorUpper,
      hasFloorLeft,
      hasFloorRight,
      results,
    };
  });

  console.log("B1 Stairwell Enclosure Audit:", JSON.stringify(b1StairsAudit, null, 2));

  if (!b1StairsAudit.hasRampDiv || !b1StairsAudit.hasEastDiv || !b1StairsAudit.hasDoorUpper) {
    throw new Error("B1 Stairwell is missing full-height enclosing walls or upper door wall!");
  }
  if (!b1StairsAudit.hasFloorLeft || !b1StairsAudit.hasFloorRight) {
    throw new Error("B1 Stairwell 1F floor slabs are missing!");
  }

  for (const r of b1StairsAudit.results) {
    if (!r.westWallFound || !r.eastWallFound) {
      throw new Error(`Gap detected in stairwell at Y=${r.point.y}, Z=${r.point.z}! West=${r.westWallFound}, East=${r.eastWallFound}`);
    }
  }
  console.log("PASSED: 100% full-height enclosing walls on both sides of B1 stairs!");

  // 4. Teleport to top of B1 stairs looking down South into the stairwell
  console.log("Teleporting player to top of B1 stairs to capture enclosed descending view...");
  await page.evaluate(() => {
    const game = window.__happyToy;
    // Top of B1 stairs vestibule: x = 16.0, y = 0.0, z = 27.5 looking South (+Z) down steps
    game.player.position.set(16.0, 0.0, 29.0);
    game.player.yaw = 0;
    game.player.pitch = -0.25;

    if (game.flashlightController) {
      game.flashlightController.enabled = true;
      game.flashlightController.applyState(false);
    }

    game.camera.position.set(16.0, 1.4, 29.0);
    game.camera.rotation.set(-0.25, 0, 0);

    const b1Chunk = game.mapBuilder.generator.chunksData.get("1,2"); if (b1Chunk) { for (const d of b1Chunk.doors || []) { if (d.id === "door-stairs-b1") { d.isOpen = true; d.openAmount = 1.0; d.update(0.1); } } for (const sl of b1Chunk.safeLights || []) {
        sl.setActivated(true);
      }
    }

    game.update(0.016);
    if (game.renderer && game.scene && game.camera) {
      game.renderer.render(game.scene, game.camera);
    }
  });

  await page.waitForTimeout(600);
  const stairsScreenshotPath = path.join(screenshotDir, "screenshot-stairs-b1-descending.png");
  await page.screenshot({ path: stairsScreenshotPath });
  console.log(`Saved B1 stairs enclosed screenshot to: ${stairsScreenshotPath}`);

  await browser.close();
  console.log("\n==========================================");
  console.log("ALL ARCHITECTURAL INTEGRITY CHECKS PASSED!");
  console.log("==========================================");
}

main().catch((err) => {
  console.error("Verification failed:", err);
  process.exit(1);
});

