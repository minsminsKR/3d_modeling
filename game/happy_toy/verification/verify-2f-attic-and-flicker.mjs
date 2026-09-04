import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const path = require('node:path');

const url = process.argv[2] || 'http://127.0.0.1:8010/';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

console.log('Starting 2F Attic Chamber & Proximity Flicker Verification...');
const browser = await chromium.launch({
  executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const browserErrors = [];
page.on('pageerror', err => browserErrors.push(err.message));
page.on('console', msg => { if (msg.type() === 'error') browserErrors.push(msg.text()); });

try {
  await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

  // Start the game to dismiss the title menu overlay
  await page.evaluate(() => {
    window.__happyToy.menuSystem?.hideMenu();
    window.__happyToy.start();
  });
  await page.waitForTimeout(600);

  // ----------------------------------------------------
  // TEST 1: Weeping Angel Mannequin in West corridor
  // ----------------------------------------------------
  console.log('\n--- 1. Testing West Corridor Weeping Angel Setup ---');
  const angelCheck = await page.evaluate(() => {
    const game = window.__happyToy;
    const chunk = game.mapBuilder.loadedChunks.get("-1,0");
    const mannequin = game.scene.getObjectByName("silent-mannequin-1f");
    const spotlight = chunk?.meshes.find(m => m.isPointLight && Math.abs(m.position.x - -22.0) < 0.5);

    return {
      chunkLoaded: Boolean(chunk),
      mannequinFound: Boolean(mannequin),
      mannequinPos: mannequin ? [mannequin.position.x, mannequin.position.y, mannequin.position.z] : null,
      mannequinActive: mannequin ? mannequin.userData?.weepingAngelState?.active : null,
      spotlightFound: Boolean(spotlight),
      spotlightPos: spotlight ? [spotlight.position.x, spotlight.position.y, spotlight.position.z] : null,
    };
  });
  console.log('Angel check result:', angelCheck);
  assert(angelCheck.mannequinFound, 'Expected silent-mannequin-1f to be spawned in chunk (-1, 0)');
  assert(Math.abs(angelCheck.mannequinPos[0] - -22.0) < 0.2, 'Expected mannequin at X ~ -22.0');
  assert(angelCheck.mannequinActive === false, 'Expected mannequin to be initially inactive (active: false)');
  assert(angelCheck.spotlightFound, 'Expected overhead spotlight at mannequin location');
  console.log('-> Weeping Angel deterministic spawn & spotlight verified!');

  // Trigger Weeping Angel intro and set camera directly looking West at mannequin
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: -14.0, y: 0.0, z: 0.0 });
    game.player.resetLook(Math.PI / 2, 0); // Face straight West down corridor
    const event = game.monsterIntroManager.events.find(e => e.constructor.name === "WeepingAngelIntroEvent");
    event.checkTrigger();
  });
  await page.waitForTimeout(1600);
  await page.screenshot({ path: 'mannequin_west_corridor.png' });
  console.log('-> Captured mannequin_west_corridor.png');

  // Verify mannequin is now activated after intro
  const postIntroCheck = await page.evaluate(() => {
    const game = window.__happyToy;
    const mannequin = game.scene.getObjectByName("silent-mannequin-1f");
    return {
      active: mannequin?.userData?.weepingAngelState?.active,
    };
  });
  assert(postIntroCheck.active === true, 'Expected mannequin to be active after intro event');
  console.log('-> Weeping Angel activated successfully!');

  // ----------------------------------------------------
  // TEST 2: 2F Attic Chamber Exploration
  // ----------------------------------------------------
  console.log('\n--- 2. Testing 2F Attic Chamber Exploration ---');
  const atticCheck = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;

    // Walk up the 2F stairs and through the doorway into the 2F chamber
    player.setPosition({ x: -16.0, y: 5.0, z: -21.5 }); // On the landing

    // Walk through the doorway at X = -17.2 towards the chamber center (-20.4, 5.0, -20.0)
    for (let step = 1; step <= 20; step++) {
      const prevPos = player.position.clone();
      player.position.x = -16.0 - (step / 20) * 4.4; // from -16.0 to -20.4
      player.position.z = -21.5 + (step / 20) * 1.5; // towards -20.0
      game.collisionWorld.resolveActorPosition(prevPos, player.position, 0.35, { actorId: 'player' });
    }

    const finalPos = player.position.clone();
    const chamberFloorArea = game.collisionWorld.floorAreas.find(f => f.id === "chamber_2f_attic");
    const cabinet2F = game.cabinets.find(c => c.id.includes("stairs_2f") || c.id.includes("2f"));
    const safeLights2F = game.safeLights.filter(l => l.position.y > 4.0);
    const altar = game.scene.getObjectByName("chunk_-1_-1_chamber_2f_altar");

    return {
      finalX: finalPos.x,
      finalY: finalPos.y,
      finalZ: finalPos.z,
      walkedInsideChamber: finalPos.x < -18.5,
      maintained2FHeight: Math.abs(finalPos.y - 5.0) < 0.1,
      floorAreaFound: Boolean(chamberFloorArea),
      cabinetFound: Boolean(cabinet2F),
      cabinetPos: cabinet2F ? [cabinet2F.position.x, cabinet2F.position.y, cabinet2F.position.z] : null,
      safeLights2FCount: safeLights2F.length,
      altarFound: Boolean(altar),
    };
  });
  console.log('2F Attic exploration result:', atticCheck);
  assert(atticCheck.walkedInsideChamber, 'Expected player to walk inside 2F Attic Chamber past doorway');
  assert(atticCheck.maintained2FHeight, 'Expected player to stay at Y=5.0 in 2F chamber, got Y=' + atticCheck.finalY);
  assert(atticCheck.floorAreaFound, 'Expected chamber_2f_attic floor area in collision world');
  assert(atticCheck.cabinetFound, 'Expected 2F Cabinet in 2F Attic Chamber');
  assert(atticCheck.safeLights2FCount >= 3, 'Expected at least 3 SafeLights in 2F area');
  assert(atticCheck.altarFound, 'Expected shrine altar table in 2F Attic Chamber');
  console.log('-> 2F Attic Chamber fully functional with collision, cabinet, and shrine altar!');

  // Position player to look North towards altar table and chamber
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: -20.4, y: 5.0, z: -18.8 });
    game.player.resetLook(0, -0.15); // Look North toward altar table (Z = -22.4)
    if (!game.flashlightController.enabled) {
      game.flashlightController.toggle();
    }
    // Turn on safe lights in 2F chamber
    for (const sl of game.safeLights) {
      if (sl.position.y > 4.0 && sl.position.x < -18.0) {
        sl.setActivated(true);
      }
    }
  });
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'attic_2f_shrine_view.png' });
  console.log('-> Captured attic_2f_shrine_view.png');

  // ----------------------------------------------------
  // TEST 3: Proximity-Based Light Flickering
  // ----------------------------------------------------
  console.log('\n--- 3. Testing Monster Proximity Light Flickering ---');
  const flickerTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    player.setPosition({ x: 0, y: 0, z: 0 });

    // Enable flashlight
    if (!game.flashlightController.enabled) {
      game.flashlightController.toggle();
    }
    const baseIntensity = game.flashlightController.defaultIntensity;

    // Test 1: No monster nearby (distance = Infinity)
    const distFar = game.getMinMonsterDistance(player.position);
    game.flashlightController.update(0.016);
    const intensityFar = game.flashlight.intensity;

    // Test 2: Spawn a temporary enemy at distance 15m (should start flickering mildly)
    const mockEnemy = {
      group: new window.THREE.Group(),
      isDormant: false,
      config: { id: "test-proximity-monster" },
    };
    mockEnemy.group.position.set(15.0, 0, 0); // 15m away
    game.enemyManager.enemies.push(mockEnemy);

    const dist15m = game.getMinMonsterDistance(player.position);
    
    // Sample multiple frames to measure flickering range
    const intensities15m = [];
    for (let f = 0; f < 30; f++) {
      game.flashlightController.update(0.016);
      intensities15m.push(game.flashlight.intensity);
    }
    const hasFlicker15m = Math.min(...intensities15m) < baseIntensity;

    // Test 3: Move enemy very close at 3.5m (should flicker rapidly and dim dramatically)
    mockEnemy.group.position.set(3.5, 0, 0); // 3.5m away
    const distClose = game.getMinMonsterDistance(player.position);
    const intensitiesClose = [];
    for (let f = 0; f < 40; f++) {
      game.flashlightController.update(0.016);
      intensitiesClose.push(game.flashlight.intensity);
    }
    const minIntensityClose = Math.min(...intensitiesClose);
    const hasDramaticDim = minIntensityClose < baseIntensity * 0.2;

    // Test 4: SafeLight proximity flickering
    const safeLight = game.safeLights.find(l => Math.hypot(l.position.x - player.position.x, l.position.z - player.position.z) < 6.0) || game.safeLights[0];
    safeLight.isOn = true;
    mockEnemy.group.position.copy(safeLight.position).add(new window.THREE.Vector3(3.0, 0, 0)); // 3m from safeLight
    const safeLightIntensities = [];
    for (let f = 0; f < 30; f++) {
      game.elapsedTime = (game.elapsedTime || 0) + 0.05;
      game.updateSafeLightPool(player.position);
      const pooledLight = game._safeLightPool[0];
      if (pooledLight) safeLightIntensities.push(pooledLight.intensity);
    }
    const safeLightDimmed = Math.min(...safeLightIntensities) < 32.0;
    const pooledLight = game._safeLightPool[0];

    // Cleanup mock enemy
    game.enemyManager.enemies = game.enemyManager.enemies.filter(e => e !== mockEnemy);

    return {
      distFar,
      intensityFar,
      dist15m,
      hasFlicker15m,
      distClose,
      minIntensityClose,
      hasDramaticDim,
      safeLightDimmed,
      safeLightPooledIntensity: pooledLight.intensity,
    };
  });

  console.log('Flicker test results:', flickerTest);
  assert(flickerTest.hasFlicker15m, 'Expected flashlight to flicker when monster is within 18m (at 15m)');
  assert(flickerTest.hasDramaticDim, 'Expected flashlight to dim dramatically and strobe when monster is very close (3.5m)');
  assert(flickerTest.safeLightDimmed, 'Expected SafeLight to flicker when monster is near it');
  console.log('-> Monster proximity dynamic light flickering VERIFIED SUCCESSFULLY!');

  console.log('\n======================================================');
  console.log('ALL 2F ATTIC, MANNEQUIN & FLICKER TESTS PASSED 100%!');
  console.log('======================================================');
} finally {
  await browser.close();
}
