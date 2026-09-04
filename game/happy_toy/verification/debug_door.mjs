import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const url = 'http://127.0.0.1:8010/';

const browser = await chromium.launch({
  executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

try {
  await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

  await page.evaluate(() => {
    window.__happyToy.menuSystem.hideMenu();
    window.__happyToy.start();
  });
  await page.waitForTimeout(500);

  // Check initial state
  const info = await page.evaluate(() => {
    const game = window.__happyToy;
    const doors = game.doors;
    const player = game.player;

    // Find the closest door to player (0,0,0)
    doors.sort((a, b) => a.position.distanceTo(player.position) - b.position.distanceTo(player.position));
    const closestDoor = doors[0];

    // Open closest door
    closestDoor.interact(player);

    // Look towards the door
    const dir = closestDoor.position.clone().sub(player.position).normalize();
    player.yaw = Math.atan2(-dir.x, -dir.z);
    game.camera.rotation.set(0, player.yaw, 0, 'YXZ');

    return {
      closestDoor: { id: closestDoor.id, pos: closestDoor.position, isOpen: closestDoor.isOpen },
      playerPos: player.position,
      loadedChunks: Array.from(game.mapBuilder.loadedChunks.keys()),
    };
  });
  console.log('Door info:', info);

  await page.waitForTimeout(600);
  await page.screenshot({ path: 'verification/debug_open_door.png' });
  console.log('Saved debug_open_door.png');

  // Walk forward 4 meters through the door
  await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    // Walk towards the door
    const doors = game.doors;
    doors.sort((a, b) => a.position.distanceTo(player.position) - b.position.distanceTo(player.position));
    const door = doors[0];
    const target = door.position.clone().addScaledVector(door.position.clone().normalize(), 2.0);
    player.position.copy(door.position);
    player.updateCamera(0.016);
  });
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'verification/debug_through_door.png' });
  console.log('Saved debug_through_door.png');

} finally {
  await browser.close();
}
