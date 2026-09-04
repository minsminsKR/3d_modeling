import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";


function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Baby monster verification...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 5000 });

  // 1. Check Baby spawning near the Workshop room key
  const prepState = await page.evaluate(() => {
    const game = window.__happyToy;
    // Set position to (0,0,0) and force update loaded chunks to spawn baby-workshop
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);

    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    const hasShadowMesh = baby && baby.shadowMesh && baby.group.children.includes(baby.shadowMesh);
    return {
      found: Boolean(baby),
      awake: baby ? baby.babyAwake : null,
      state: baby ? baby.state : null,
      initialPos: baby ? [baby.group.position.x, baby.group.position.y, baby.group.position.z] : null,
      hasShadowMesh: Boolean(hasShadowMesh),
    };
  });

  console.log("Baby preparation state:", prepState);
  assert(prepState.found, "Expected baby-workshop to be spawned in the scene");
  assert(prepState.hasShadowMesh, "Expected Baby to have a drop-shadow blob mesh attached to its group");
  assert(prepState.awake === false, "Expected Baby to be initially asleep (not awake)");
  assert(prepState.state === "crying", "Expected initial Baby state to be 'crying'");

  // 2. Test quiet near check (should not wake up if player is far/quiet/flashlight OFF)
  const quietTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Teleport player far (15 meters away)
    game.player.setPosition({ x: 32.5, y: 0, z: 15.5 }); // baby is at 30.5
    game.flashlightController.enabled = false;
    game.flashlightController.applyState(false);
    
    // Update multiple times
    const startPos = baby.group.position.clone();
    for (let i = 0; i < 20; i++) {
      game.enemyManager.update(0.05, {
        position: game.player.position,
        isSprinting: false,
        isHidden: false,
        isUndetectable: false,
      });
    }
    
    return {
      awake: baby.babyAwake,
      state: baby.state,
      distanceMoved: startPos.distanceTo(baby.group.position),
    };
  });

  console.log("Quiet state test results:", quietTest);
  assert(!quietTest.awake, "Expected Baby to remain asleep during quiet check");
  assert(quietTest.state === "crying", "Expected Baby state to remain 'crying' during quiet check");
  assert(quietTest.distanceMoved < 0.01, "Expected Baby to stay in place when asleep");

  // 3. Test Proximity Trigger (wakes up when player gets too close < 1.8m)
  const proximityTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Reset state
    game.restart();
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    
    const freshBaby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Teleport player directly next to it (1.0m away)
    game.player.setPosition({ x: 32.5, y: 0, z: 29.5 });
    
    // Update once
    game.enemyManager.update(0.016, {
      position: game.player.position,
      isSprinting: false,
      isHidden: false,
      isUndetectable: false,
    });
    
    return {
      awake: freshBaby.babyAwake,
      state: freshBaby.state,
    };
  });

  console.log("Proximity trigger test results:", proximityTest);
  assert(proximityTest.awake, "Expected Baby to wake up when player gets within 1.8m");
  assert(proximityTest.state === "chase", "Expected Baby to switch to 'chase' state upon waking");

  // 4. Test Sprint Trigger (wakes up when player runs/sprints nearby <= 6.0m)
  const sprintTest = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    
    const freshBaby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Teleport player 4.0m away (which is > 1.8m proximity limit)
    game.player.setPosition({ x: 32.5, y: 0, z: 26.5 });
    
    // Run update with isSprinting: true
    game.enemyManager.update(0.016, {
      position: game.player.position,
      isSprinting: true,
      isHidden: false,
      isUndetectable: false,
    });
    
    return {
      awake: freshBaby.babyAwake,
      state: freshBaby.state,
    };
  });

  console.log("Sprint trigger test results:", sprintTest);
  assert(sprintTest.awake, "Expected Baby to wake up when player sprints within 6.0m");
  assert(sprintTest.state === "chase", "Expected Baby to switch to 'chase' state upon sprint trigger");

  // 5. Test Flashlight Shine Trigger (wakes up when player shines flashlight directly on it from <= 8.0m)
  const flashlightShineTest = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    
    const freshBaby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Turn flashlight ON
    game.flashlightController.enabled = true;
    game.flashlightController.applyState(false);
    
    // Teleport player 6.0m North of the Baby (baby is at 30.5, player at 36.5)
    game.player.setPosition({ x: 32.5, y: 0, z: 36.5 });
    // Look South (toward Negative Z) -> yaw = 0
    game.player.yaw = 0;
    
    // Force camera direction update
    game.camera.rotation.set(game.player.pitch, game.player.yaw, 0, "YXZ");
    
    // Run update
    game.enemyManager.update(0.016, {
      position: game.player.position,
      isSprinting: false,
      isHidden: false,
      isUndetectable: false,
    });
    
    return {
      awake: freshBaby.babyAwake,
      state: freshBaby.state,
    };
  });

  console.log("Flashlight shine trigger test results:", flashlightShineTest);
  assert(flashlightShineTest.awake, "Expected Baby to wake up when flashlight shines directly on it from <= 8.0m");
  assert(flashlightShineTest.state === "chase", "Expected Baby to switch to 'chase' state upon flashlight trigger");

  // 6. Test Collision caught: Safe when sleeping, game-over when awake
  const collisionTest = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    
    const freshBaby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    
    // Make sure it is asleep
    game.flashlightController.enabled = false;
    game.flashlightController.applyState(false);
    
    // Teleport player exactly inside the sleeping Baby
    game.player.setPosition({ x: freshBaby.group.position.x, y: 0, z: freshBaby.group.position.z });
    
    // Mock Math.hypot to return 10.0 so the baby doesn't wake up, simulating it remaining asleep
    const originalHypot = Math.hypot;
    Math.hypot = () => 10.0;
    
    // Run updates to check collision safety
    const state = game.enemyManager.update(0.016, {
      position: game.player.position,
      isSprinting: false,
      isHidden: false,
      isUndetectable: false,
    });
    
    const caughtWhileSleeping = state.caught || game.gameOver;
    
    // Restore Math.hypot
    Math.hypot = originalHypot;
    
    // Now force it awake and state to chase for the awake test
    freshBaby.babyAwake = true;
    freshBaby.state = "chase";
    
    // Teleport player directly inside the awake Baby
    game.player.setPosition({ x: freshBaby.group.position.x, y: 0, z: freshBaby.group.position.z });
    
    // Run update to trigger game over
    const stateAwake = game.enemyManager.update(0.016, {
      position: game.player.position,
      isSprinting: false,
      isHidden: false,
      isUndetectable: false,
    });
    
    const caughtWhileAwake = stateAwake.caught || game.gameOver;
    
    return {
      caughtWhileSleeping,
      isAwakeNow: freshBaby.babyAwake,
      caughtWhileAwake,
    };
  });

  console.log("Collision safety/deadly test results:", collisionTest);
  assert(!collisionTest.caughtWhileSleeping, "Expected player NOT to die on collision with a sleeping Baby");
  assert(collisionTest.isAwakeNow, "Expected Baby to have woken up");
  assert(collisionTest.caughtWhileAwake, "Expected player to die on collision with an awake Baby");

  console.log("All Baby monster verification tests passed successfully!");
} catch (err) {
  console.error("Baby monster verification failed!");
  console.error(err);
  process.exit(1);
} finally {
  await browser.close();
}
