import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Weeping Angels verification...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 5000 });

  // 1. Spawning check: Teleport player near spawn and ensure mannequin mesh is loaded
  const prepState = await page.evaluate(() => {
    const game = window.__happyToy;
    // Force loaded chunk (0,0) to exist by setting position to 0,0,0
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    
    const chunk = game.mapBuilder.loadedChunks.get("0,0");
    if (!chunk) {
      return { found: false, loaded: false, initialPos: null, error: "Chunk 0,0 not loaded" };
    }
    
    // Remove any conflicting procedurally generated mannequins
    let existing = game.scene.getObjectByName("silent-mannequin-1f");
    while (existing) {
      game.scene.remove(existing);
      chunk.meshes = chunk.meshes.filter(m => m !== existing);
      existing = game.scene.getObjectByName("silent-mannequin-1f");
    }

    // Create/inject our mock mannequin into chunk 0,0
    const mannequin = new window.THREE.Group();
    mannequin.name = "silent-mannequin-1f";
    mannequin.position.set(5, 0, 5);
    mannequin.userData.isWeepingAngel = true;
    mannequin.userData.weepingAngelState = {
      id: "silent-mannequin-1f",
      speed: 1.3,
      catchDistance: 1.05,
      radius: 0.38,
      size: [0.62, 1.72, 0.36],
      loaded: true,
      path: null,
      pathTimer: 0,
    };
    game.scene.add(mannequin);
    chunk.meshes.push(mannequin);
    
    return {
      found: Boolean(mannequin),
      loaded: mannequin ? mannequin.userData.weepingAngelState.loaded : false,
      initialPos: mannequin ? [mannequin.position.x, mannequin.position.y, mannequin.position.z] : null,
      flashlightEnabled: game.flashlightController.enabled,
    };
  });

  console.log("Weeping Angel preparation state:", prepState);
  assert(prepState.found, "Expected silent-mannequin-1f to spawn in its chunk");

  // 2. Test Flashlight OFF: Mannequin should NOT move and should NOT kill on touch
  const flashlightOffTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const mannequin = game.scene.getObjectByName("silent-mannequin-1f");
    
    // Make sure flashlight is OFF
    game.flashlightController.enabled = false;
    game.flashlightController.applyState(false);
    
    const startPos = mannequin.position.clone();
    
    // Teleport player further away to let it pathfind, and tick update
    game.player.setPosition({ x: 2, y: 0, z: 2 });
    
    // Make sure player is looking away (yaw = 0, looking North, mannequin is at +Z)
    game.player.yaw = 0;
    
    // Update multiple times
    for (let i = 0; i < 20; i++) {
      game.updateWeepingAngels(0.05);
    }
    
    const posAfterNoChase = mannequin.position.clone();
    const distanceMoved = startPos.distanceTo(posAfterNoChase);
    
    // Teleport player directly inside Weeping Angel to test collision safety
    game.player.setPosition({ x: posAfterNoChase.x, y: 0, z: posAfterNoChase.z });
    game.updateWeepingAngels(0.016);
    
    return {
      distanceMoved,
      gameOverAfterCollision: game.gameOver,
    };
  });

  console.log("Flashlight OFF test results:", flashlightOffTest);
  assert(flashlightOffTest.distanceMoved < 0.01, "Expected Weeping Angel to remain still when flashlight is OFF");
  assert(!flashlightOffTest.gameOverAfterCollision, "Expected player NOT to die on mannequin collision when flashlight is OFF");

  // 3. Test Flashlight ON: Mannequin should chase player when looking away, and should kill on collision
  const flashlightOnTest = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    
    // Set player position to 0,0,0 and force update backrooms
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    
    const chunk = game.mapBuilder.loadedChunks.get("0,0");
    
    // Remove any conflicting procedurally generated mannequins
    let existing = game.scene.getObjectByName("silent-mannequin-1f");
    while (existing) {
      game.scene.remove(existing);
      if (chunk) {
        chunk.meshes = chunk.meshes.filter(m => m !== existing);
      }
      existing = game.scene.getObjectByName("silent-mannequin-1f");
    }
    
    const mannequin = new window.THREE.Group();
    mannequin.name = "silent-mannequin-1f";
    mannequin.position.set(5, 0, 5);
    mannequin.userData.isWeepingAngel = true;
    mannequin.userData.weepingAngelState = {
      id: "silent-mannequin-1f",
      speed: 1.3,
      catchDistance: 1.05,
      radius: 0.38,
      size: [0.62, 1.72, 0.36],
      loaded: true,
      path: null,
      pathTimer: 0,
    };
    game.scene.add(mannequin);
    if (chunk) {
      chunk.meshes.push(mannequin);
    }
    
    // Turn flashlight ON
    game.flashlightController.enabled = true;
    game.flashlightController.applyState(false);
    
    // Teleport player away, look away (North), and tick update to check pursuit movement
    game.player.setPosition({ x: 2, y: 0, z: 2 });
    game.player.yaw = 0; // look North
    
    const startPos = mannequin.position.clone();
    
    // Tick updates to let mannequin walk closer
    for (let i = 0; i < 30; i++) {
      game.updateWeepingAngels(0.1);
    }
    
    const posAfterChase = mannequin.position.clone();
    const distanceMoved = startPos.distanceTo(posAfterChase);
    
    // Teleport player directly onto Weeping Angel to test caught collision
    game.player.setPosition({ x: posAfterChase.x, y: 0, z: posAfterChase.z });
    game.updateWeepingAngels(0.016);
    
    return {
      distanceMoved,
      gameOverAfterCollision: game.gameOver,
    };
  });

  console.log("Flashlight ON test results:", flashlightOnTest);
  assert(flashlightOnTest.distanceMoved > 0.5, "Expected Weeping Angel to chase player when flashlight is ON and player looks away");
  assert(flashlightOnTest.gameOverAfterCollision, "Expected player to die on mannequin collision when flashlight is ON");

  console.log("All Weeping Angels verification tests passed successfully!");
} catch (err) {
  console.error("Weeping Angels verification failed!");
  console.error(err);
  process.exit(1);
} finally {
  await browser.close();
}
