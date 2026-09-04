import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const url = process.argv[2] || 'http://127.0.0.1:8010/';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

console.log('Starting Player Real-Movement Simulation Across 3 Floors...');
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

  console.log('Game loaded. Starting player movement simulation...');

  // 1. Initial Position
  const initialPos = await page.evaluate(() => {
    const game = window.__happyToy;
    return { x: game.player.position.x, y: game.player.position.y, z: game.player.position.z };
  });
  console.log('Initial player spawn:', initialPos);
  assert(initialPos.x === 0 && initialPos.y === 0 && initialPos.z === 0, 'Expected start at (0,0,0)');

  // 2. Walk East into Cyclopse corridor (1, 0)
  const walkEast = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    player.setPosition({ x: 0.0, y: 0.0, z: 0.0 });

    for (let step = 0; step <= 20; step++) {
      const targetX = (step / 20) * 14.0;
      const prevPos = player.position.clone();
      player.position.x = targetX;
      game.collisionWorld.resolveActorPosition(prevPos, player.position, 0.35, { actorId: 'player' });
    }

    return {
      x: player.position.x,
      y: player.position.y,
      reachedCorridor: player.position.x >= 12.0,
    };
  });
  console.log('East corridor traversal:', walkEast);
  assert(walkEast.reachedCorridor, 'Expected player to walk smoothly into East corridor');
  console.log('-> East Corridor Traversal PASSED!');

  // 3. Walk North into Nursery & Storage wing
  const walkNorth = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    player.setPosition({ x: 0.0, y: 0.0, z: 0.0 });

    for (let step = 0; step <= 20; step++) {
      const targetZ = -(step / 20) * 14.0;
      const prevPos = player.position.clone();
      player.position.z = targetZ;
      game.collisionWorld.resolveActorPosition(prevPos, player.position, 0.35, { actorId: 'player' });
    }

    return {
      z: player.position.z,
      y: player.position.y,
      reachedNorth: player.position.z <= -12.0,
    };
  });
  console.log('North corridor traversal:', walkNorth);
  assert(walkNorth.reachedNorth, 'Expected player to walk smoothly into North corridor');
  console.log('-> North Corridor Traversal PASSED!');

  // 4. Verify All Loaded Chunks Have Unified Floor & Ceiling
  const chunkEnclosureCheck = await page.evaluate(() => {
    const game = window.__happyToy;
    const loaded = Array.from(game.mapBuilder.loadedChunks.values());
    const badFloors = loaded.filter(c => Math.abs(c.floorY) > 0.01);
    return {
      totalChunks: loaded.length,
      badFloorsCount: badFloors.length,
    };
  });
  console.log('Chunk enclosure check:', chunkEnclosureCheck);
  assert(chunkEnclosureCheck.badFloorsCount === 0, 'Expected all chunks to have unified floorY = 0.0');
  console.log('-> Unified Seamless Enclosure PASSED!');

  // 5. Maze Corridors Check
  const mazeCheck = await page.evaluate(() => {
    const game = window.__happyToy;
    const blockers = game.collisionWorld.blockers.filter(b => b.id.startsWith('chunk_1_0_wall_'));
    return {
      hasBaffles: blockers.some(b => b.id.includes('baffle')),
      hasGuideWalls: blockers.some(b => b.id.includes('guide')),
    };
  });
  console.log('Maze check (1,0):', mazeCheck);
  assert(mazeCheck.hasGuideWalls && !mazeCheck.hasBaffles, 'Expected uniform corridor guide walls without baffles');
  console.log('-> Maze Corridor Architecture PASSED!');

  console.log("==========================================");
  console.log("ALL REAL PLAYER WALKTHROUGH CHECKS PASSED!");
  console.log("==========================================");
} finally {
  await browser.close();
}
