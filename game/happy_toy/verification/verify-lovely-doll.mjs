import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";


function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting Lovely Doll guiding mechanic verification on url:", url);

const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
 Headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const browserErrors = [];
page.on("console", (message) => {
  if (message.type() === "error") {
    browserErrors.push(message.text());
  }
});
page.on("pageerror", (error) => browserErrors.push(error.message));

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  
  // Wait for game initialization
  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 5000 },
  );

  console.log("Game loaded successfully. Testing Lovely Doll spawning...");

  // 1. Spawning check: teleport around to spawn coordinates and check if dolls are registered in game.lovelyDolls
  const spawnState = await page.evaluate(() => {
    const game = window.__happyToy;
    
    // Spawn spots
    const spots = [
      { cx: 0, cz: 0, x: 0, z: 0 }
    ];

    const results = [];

    // Teleport to each spot, trigger update, and check spawned dolls
    for (const spot of spots) {
      game.player.setPosition({ x: spot.x, y: 0, z: spot.z });
      game.updateBackrooms(0.016);
      
      const chunk = game.mapBuilder.loadedChunks.get(`${spot.cx},${spot.cz}`);
      const dollId = chunk ? chunk.dollId : null;
      const registered = game.lovelyDolls.find(d => d.id === dollId);
      
      let isFallback = false;
      if (registered && registered.modelRoot) {
        registered.modelRoot.traverse(child => {
          if (child.isMesh && child.geometry && child.geometry.type === "CapsuleGeometry") {
            isFallback = true;
          }
        });
      }
      
      results.push({
        cx: spot.cx,
        cz: spot.cz,
        dollId,
        registered: Boolean(registered),
        state: registered ? registered.state : null,
        isFallback,
      });
    }

    return {
      results,
      lovelyDollsLength: game.lovelyDolls.length,
      keysRendered: game.scene.children.filter(c => c.name && c.name.startsWith("key-")).length,
    };
  });

  console.log("Spawn results:", spawnState.results);
  console.log("Total dolls spawned:", spawnState.lovelyDollsLength);
  console.log("Keys rendered in scene (should be >0 since key chunks are loaded):", spawnState.keysRendered);

  // Assertions for spawning
  assert(spawnState.lovelyDollsLength === 1, `Expected 1 lovely doll spawned, got ${spawnState.lovelyDollsLength}`);
  assert(spawnState.keysRendered > 0, `Expected keys to be added to the scene (fixing the rendering bug), got ${spawnState.keysRendered}`);
  
  for (const res of spawnState.results) {
    assert(res.dollId !== null, `Expected doll to spawn in chunk (${res.cx}, ${res.cz})`);
    assert(res.registered, `Expected doll ${res.dollId} to be registered in game.lovelyDolls`);
    assert(res.state === "dance", `Expected doll ${res.dollId} to start in 'dance' state, got ${res.state}`);
    assert(!res.isFallback, `Expected doll ${res.dollId} to load actual FBX model, but it spawned as the fallback capsule`);
  }

  console.log("Spawning verification passed! Testing activation and sequence mechanics...");

  // 2. Activation check: activate them one by one and check target positions
  const activationState = await page.evaluate(() => {
    const game = window.__happyToy;
    const results = [];

    // Sort dolls by index
    for (let i = 0; i < 1; i++) {
      const doll = game.lovelyDolls[i];
      
      // Simulate player staring and activating
      doll.activate();
      
      results.push({
        id: doll.id,
        state: doll.state,
        dollIndex: doll.dollIndex,
        targetPos: [doll.targetPosition.x, doll.targetPosition.y, doll.targetPosition.z],
      });
    }

    return results;
  });

  console.log("Activation results:", activationState);

  // Verify dynamic assignment targets
  const expectedTargets = [
    [32, 0, 32],   // Key 1: Workshop
  ];

  for (let i = 0; i < 1; i++) {
    const act = activationState[i];
    assert(act.state === "walking", `Expected doll ${act.id} to transition to 'walking' state, got ${act.state}`);
    assert(act.dollIndex === i + 1, `Expected doll ${act.id} to be assigned index ${i + 1}, got ${act.dollIndex}`);
    
    const target = act.targetPos;
    const expected = expectedTargets[i];
    assert(
      target[0] === expected[0] && target[2] === expected[2],
      `Expected doll ${act.id} to guide to ${expected}, got ${target}`
    );
  }

  console.log("Activation and guiding targets verification passed! Testing chase speed transition...");

  // 3. Chase speed change check
  const speedState = await page.evaluate(() => {
    const game = window.__happyToy;
    const doll = game.lovelyDolls[0];
    
    // Simulate chase condition
    const originalEnemyState = game.enemyManager.enemies[0].state;
    game.enemyManager.enemies[0].state = "chase";
    
    // Update movement
    doll.update(0.016);
    const stateDuringChase = doll.state;
    
    // Restore
    game.enemyManager.enemies[0].state = originalEnemyState;
    doll.update(0.016);
    const stateAfterChase = doll.state;

    return {
      stateDuringChase,
      stateAfterChase
    };
  });

  console.log("Speed state transition check:", speedState);
  assert(speedState.stateDuringChase === "run", `Expected doll to transition to 'run' state during chase, got ${speedState.stateDuringChase}`);
  assert(speedState.stateAfterChase === "walking", `Expected doll to return to 'walking' state after chase ended, got ${speedState.stateAfterChase}`);

  console.log("Chase speed transition check passed! Testing arrival and fading out...");

  // 4. Fade out check: teleport doll to target, check transition to 'fade' state, check opacity reduction
  const fadeState = await page.evaluate(() => {
    const game = window.__happyToy;
    const doll = game.lovelyDolls[0];
    
    // Move doll directly to target position
    doll.group.position.copy(doll.targetPosition);
    
    // Trigger update to detect arrival
    doll.update(0.016);
    const stateOnArrival = doll.state;
    const initialFadeTimer = doll.fadeTimer;

    // Simulate 5 seconds passing (5.0s * delta 1.0s / 5 ticks)
    doll.update(5.0);
    
    // Get opacity of one of the meshes
    let testOpacity = 1.0;
    doll.modelRoot.traverse((child) => {
      if (child.isMesh || child.isSkinnedMesh) {
        if (child.material && !Array.isArray(child.material)) {
          testOpacity = child.material.opacity;
        }
      }
    });

    return {
      stateOnArrival,
      initialFadeTimer,
      testOpacity,
    };
  });

  console.log("Fade state check:", fadeState);
  assert(fadeState.stateOnArrival === "fade", `Expected doll to transition to 'fade' state on arrival, got ${fadeState.stateOnArrival}`);
  assert(Math.abs(fadeState.initialFadeTimer - 10.0) < 0.1, `Expected fadeTimer to start at 10.0, got ${fadeState.initialFadeTimer}`);
  assert(Math.abs(fadeState.testOpacity - 0.5) < 0.05, `Expected opacity to be around 0.5 after 5 seconds, got ${fadeState.testOpacity}`);

  console.log("Fading out check passed! Testing final cleanup on fade completion...");

  const cleanupState = await page.evaluate(() => {
    const game = window.__happyToy;
    const doll = game.lovelyDolls[0];
    const dollId = doll.id;
    
    // Complete the remaining 5 seconds of fading
    doll.update(5.1);
    
    const isRemoved = !game.lovelyDolls.includes(doll);
    const sceneObject = game.scene.getObjectByName(dollId);

    return {
      isRemoved,
      sceneObjectExists: Boolean(sceneObject),
    };
  });

  console.log("Cleanup check:", cleanupState);
  assert(cleanupState.isRemoved, "Expected doll to be removed from game.lovelyDolls after fading completed");
  assert(!cleanupState.sceneObjectExists, "Expected doll group to be removed from THREE.Scene after fading completed");

  console.log("All Lovely Doll guiding mechanic verification checks passed successfully!");

} catch (error) {
  console.error("Lovely Doll Verification failed!");
  console.error(error);
  console.error("Browser errors recorded during test:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}
