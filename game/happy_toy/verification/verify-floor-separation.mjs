import { chromium } from "playwright";

const URL = process.argv[2] || "http://127.0.0.1:8010/";

async function runVerification() {
  console.log(`[Test] Launching headless browser for: ${URL}`);
  const browser = await chromium.launch({
    headless: true,
    args: ["--no-sandbox", "--disable-setuid-sandbox", "--use-gl=angle", "--use-angle=swiftshader"],
  });

  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  
  page.on("console", (msg) => {
    const text = msg.text();
    if (text.includes("[PERF]") || text.includes("Error") || text.includes("warn") || text.includes("Enemy")) {
      console.log(`[Browser Console] ${msg.type()}: ${text}`);
    }
  });

  await page.goto(URL, { waitUntil: "networkidle", timeout: 30000 });

  // Wait for game initialization
  console.log("[Test] Waiting for window.__happyToy and assets to load...");
  await page.waitForFunction(() => {
    return window.__happyToy && window.__happyToy.mapBuilder && window.__happyToy.enemyManager;
  }, { timeout: 30000 });

  // Click start button if needed
  try {
    const startBtn = await page.$("#start-btn");
    if (startBtn && await startBtn.isVisible()) {
      await startBtn.click();
      console.log("[Test] Clicked start button");
    }
  } catch (e) {
    console.log("[Test] Start button click bypassed or not needed");
  }

  // Wait 3 seconds for chunks and enemies to settle
  await page.waitForTimeout(3000);

  // Evaluate tests in browser context
  const testResults = await page.evaluate(() => {
    const game = window.__happyToy;
    const errors = [];
    const logs = [];

    // 1. Check ENEMY_CONFIGS and Loaded Enemies
    const enemies = game.enemyManager.enemies;
    logs.push(`Loaded enemies count: ${enemies.length}`);
    const enemySummary = enemies.map(e => ({
      id: e.config.id,
      label: e.config.label,
      allowedFloor: e.config.allowedFloor,
      allowInterFloorPatrol: e.config.allowInterFloorPatrol,
      pos: [
        Number(e.group.position.x.toFixed(2)),
        Number(e.group.position.y.toFixed(2)),
        Number(e.group.position.z.toFixed(2))
      ],
      isDormant: e.isDormant,
    }));
    logs.push(`Enemies: ${JSON.stringify(enemySummary, null, 2)}`);

    // Ensure NO baby on 1F
    const babiesOn1F = enemies.filter(e => e.config.id.includes("baby") && e.group.position.y > -2.0);
    if (babiesOn1F.length > 0) {
      errors.push(`FAIL: Found ${babiesOn1F.length} Baby instance(s) on 1F! IDs: ${babiesOn1F.map(b => b.config.id).join(", ")}`);
    } else {
      logs.push("PASS: Zero Baby instances on 1F.");
    }

    // Ensure Baby is strictly in B1
    const babyWorkshop = enemies.find(e => e.config.id === "baby-workshop");
    if (!babyWorkshop) {
      errors.push("FAIL: baby-workshop not found in enemies list!");
    } else {
      if (babyWorkshop.config.allowedFloor !== -1) {
        errors.push(`FAIL: baby-workshop allowedFloor is ${babyWorkshop.config.allowedFloor}, expected -1`);
      }
      if (babyWorkshop.config.allowInterFloorPatrol !== false) {
        errors.push("FAIL: baby-workshop allowInterFloorPatrol is not false");
      }
      if (babyWorkshop.group.position.y > -3.0) {
        errors.push(`FAIL: baby-workshop is at Y=${babyWorkshop.group.position.y}, expected <= -4.0 in B1`);
      } else {
        logs.push(`PASS: baby-workshop is properly stationed in B1 at Y=${babyWorkshop.group.position.y}`);
      }
    }

    // Ensure 1F enemies (Uncat, Cyclops) have allowedFloor: 1 and allowInterFloorPatrol: false
    const uncat = enemies.find(e => e.config.id === "uncat");
    if (!uncat || uncat.config.allowedFloor !== 1 || uncat.config.allowInterFloorPatrol !== false) {
      errors.push("FAIL: uncat not properly configured with allowedFloor: 1 and allowInterFloorPatrol: false");
    } else {
      logs.push("PASS: uncat is properly isolated to 1F.");
    }

    const cyclops = enemies.find(e => e.config.id === "cyclopse");
    if (!cyclops || cyclops.config.allowedFloor !== 1 || cyclops.config.allowInterFloorPatrol !== false) {
      errors.push("FAIL: cyclopse not properly configured with allowedFloor: 1 and allowInterFloorPatrol: false");
    } else {
      logs.push("PASS: cyclopse is properly isolated to 1F.");
    }

    // 2. Check Keys: exactly 4 unique keys
    const keys = game.keys;
    logs.push(`Total keys in map: ${keys.length}`);
    const keyDetails = keys.map(k => ({
      id: k.id,
      label: k.label,
      pos: [
        Number(k.group.position.x.toFixed(2)),
        Number(k.group.position.y.toFixed(2)),
        Number(k.group.position.z.toFixed(2))
      ],
      chunkId: k.chunkId,
    }));
    logs.push(`Keys: ${JSON.stringify(keyDetails, null, 2)}`);

    if (keys.length !== 4) {
      errors.push(`FAIL: Expected exactly 4 keys, but found ${keys.length}!`);
    } else {
      logs.push("PASS: Map contains exactly 4 unique keys.");
    }

    const keyHwacat = keys.filter(k => k.id === "key-hwacat");
    if (keyHwacat.length !== 1) {
      errors.push(`FAIL: Expected exactly 1 key-hwacat, but found ${keyHwacat.length}`);
    } else {
      const k = keyHwacat[0];
      if (k.group.position.y < 4.0) {
        errors.push(`FAIL: key-hwacat is at Y=${k.group.position.y}, expected ~5.0 on 2F!`);
      } else {
        logs.push(`PASS: key-hwacat is correctly placed on 2F at Y=${k.group.position.y}`);
      }
    }

    const keyWorkshop = keys.find(k => k.id === "key-workshop");
    if (!keyWorkshop) {
      errors.push("FAIL: key-workshop not found!");
    } else {
      if (keyWorkshop.group.position.y > -3.0) {
        errors.push(`FAIL: key-workshop is at Y=${keyWorkshop.group.position.y}, expected ~ -5.0 in B1!`);
      } else {
        logs.push(`PASS: key-workshop is correctly placed in B1 at Y=${keyWorkshop.group.position.y}`);
      }
    }

    // 3. Check chunk (-2, -2): Must be "archive"
    const archiveChunk = game.mapBuilder.loadedChunks.get("-2,-2");
    if (!archiveChunk) {
      errors.push("FAIL: Chunk (-2, -2) is not loaded!");
    } else {
      if (archiveChunk.type !== "archive") {
        errors.push(`FAIL: Chunk (-2, -2) type is "${archiveChunk.type}", expected "archive"!`);
      } else {
        logs.push(`PASS: Chunk (-2, -2) is correctly classified as "archive"`);
      }

      const hasArchiveDoor = archiveChunk.doors.some(d => d.id === "door-archive");
      if (!hasArchiveDoor) {
        errors.push("FAIL: door-archive not found in chunk (-2, -2)");
      } else {
        logs.push("PASS: door-archive found in chunk (-2, -2)");
      }

      const hasArchiveCabinet = archiveChunk.cabinets.some(c => c.id === "cabinet-archive");
      if (!hasArchiveCabinet) {
        errors.push("FAIL: cabinet-archive not found in chunk (-2, -2)");
      } else {
        logs.push("PASS: cabinet-archive found in chunk (-2, -2)");
      }
    }

    // 4. Test Floor Isolation Logic in Enemy perception
    // When player is on 2F (Y = 5.0), 1F monsters must ignore player and stay wander
    const originalPlayerY = game.player.position.y;
    game.player.position.y = 5.0; // Player on 2F
    game.player.position.x = uncat.group.position.x;
    game.player.position.z = uncat.group.position.z + 2.0; // 2 meters away in 2D!
    
    // Update perception
    uncat.updatePerception(game.player.position, 0.1, { isSprinting: true });
    if (uncat.state === "chase" || uncat.state === "flee") {
      errors.push(`FAIL: uncat chased player who is on 2F! State: ${uncat.state}`);
    } else {
      logs.push("PASS: uncat strictly ignored player on 2F (state remains wander)");
    }

    // When player is on B1 (Y = -5.0)
    game.player.position.y = -5.0; // Player in B1
    cyclops.updatePerception(game.player.position, 0.1, { isSprinting: true });
    if (cyclops.state === "chase" || cyclops.state === "flee") {
      errors.push(`FAIL: cyclopse chased player who is on B1! State: ${cyclops.state}`);
    } else {
      logs.push("PASS: cyclopse strictly ignored player in B1 (state remains wander)");
    }

    // Restore player Y
    game.player.position.y = originalPlayerY;

    return {
      success: errors.length === 0,
      errors,
      logs,
    };
  });

  console.log("\n--- TEST LOGS ---");
  testResults.logs.forEach(l => console.log(l));

  if (!testResults.success) {
    console.error("\n--- TEST FAILURES ---");
    testResults.errors.forEach(e => console.error(e));
  } else {
    console.log("\n>>> ALL FLOOR ISOLATION & ARCHITECTURAL INTEGRITY CHECKS PASSED 100%! <<<");
  }

  // Take a visual confirmation screenshot
  await page.screenshot({ path: "verification/floor-separation-verified.png" });
  console.log("[Test] Screenshot saved to verification/floor-separation-verified.png");

  await browser.close();
  if (!testResults.success) {
    process.exit(1);
  }
}

runVerification().catch((err) => {
  console.error("Verification execution error:", err);
  process.exit(1);
});
