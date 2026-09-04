import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const url = process.argv[2] || 'http://127.0.0.1:8010/';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

console.log('Starting Stairs Real-Movement Simulation...');
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

  // 1. Walk up 2F stairs in chunk (-1, -1)
  const climb2F = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    // Chunk (-1, -1) center is (-16, 0, -16)
    // 1F vestibule is z = -8.5, bottom of stairs at z = -13.5, top at z = -20.5, landing at z = -22.5
    player.setPosition({ x: -16.0, y: 0.0, z: -9.0 });

    const yHistory = [];
    for (let step = 0; step <= 30; step++) {
      const targetZ = -9.0 - (step / 30) * 13.0; // from -9.0 to -22.0
      const prevPos = player.position.clone();
      player.position.z = targetZ;
      game.collisionWorld.resolveActorPosition(prevPos, player.position, 0.35, { actorId: 'player' });
      yHistory.push({ z: player.position.z, y: player.position.y });
    }

    return {
      finalZ: player.position.z,
      finalY: player.position.y,
      startY: yHistory[0].y,
      midY: yHistory[15].y,
      reaches2F: Math.abs(player.position.y - 5.0) < 0.1,
    };
  });
  console.log('2F Staircase climbing result:', climb2F);
  assert(climb2F.reaches2F, `Expected player to reach 2F (Y=5.0), got ${climb2F.finalY}`);
  console.log('-> 2F Staircase Climbing PASSED!');

  // 2. Walk down B1 stairs in chunk (1, 2)
  const descendB1 = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    // Chunk (1, 2) center is (16, 0, 32)
    // 1F vestibule is z = 24.5, top of stairs at z = 29.5, bottom at z = 36.5, landing at z = 38.5
    player.setPosition({ x: 16.0, y: 0.0, z: 25.0 });

    const yHistory = [];
    for (let step = 0; step <= 30; step++) {
      const targetZ = 25.0 + (step / 30) * 13.0; // from 25.0 to 38.0
      const prevPos = player.position.clone();
      player.position.z = targetZ;
      game.collisionWorld.resolveActorPosition(prevPos, player.position, 0.35, { actorId: 'player' });
      yHistory.push({ z: player.position.z, y: player.position.y });
    }

    return {
      finalZ: player.position.z,
      finalY: player.position.y,
      startY: yHistory[0].y,
      midY: yHistory[15].y,
      reachesB1: Math.abs(player.position.y - -5.0) < 0.1,
    };
  });
  console.log('B1 Staircase descending result:', descendB1);
  assert(descendB1.reachesB1, `Expected player to reach B1 (Y=-5.0), got ${descendB1.finalY}`);
  console.log('-> B1 Staircase Descending PASSED!');

  console.log('==========================================');
  console.log('ALL STAIRCASE CLIMB & DESCEND CHECKS PASSED!');
  console.log('==========================================');
} finally {
  await browser.close();
}
