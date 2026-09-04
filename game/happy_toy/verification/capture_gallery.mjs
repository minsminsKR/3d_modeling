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

  // 1. Dark corridor with player flashlight
  await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    // Open North door
    const northDoor = game.doors.find(d => d.id === 'chunk_0_0_door_n');
    if (northDoor) northDoor.interact(player);

    player.setPosition({ x: 0, y: 0, z: -4 });
    player.yaw = 0;
    player.updateCamera(0.016);
  });
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'C:/Users/sanguk/.gemini/antigravity/brain/a896b356-c2d7-47f3-ab26-8e8f7bd2ba8f/dark_uniform_corridor.png' });
  console.log('Saved dark_uniform_corridor.png');

  // 2. Approach 2F stairs in (-1, -1)
  await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    game.flashlightController.enabled = true;
    game.flashlightController.applyState(false);
    player.setPosition({ x: -16, y: 0.5, z: -10 });
    player.yaw = 0;
    player.pitch = 0.15;
    player.updateCamera(0.016);
  });
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'C:/Users/sanguk/.gemini/antigravity/brain/a896b356-c2d7-47f3-ab26-8e8f7bd2ba8f/stairs_2f_view.png' });
  console.log('Saved stairs_2f_view.png');

  // 3. Approach B1 stairs in (1, 2)
  await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    player.setPosition({ x: 16, y: 0.0, z: 27 });
    player.yaw = Math.PI; // facing +Z towards descending stairs
    player.pitch = -0.2;
    player.updateCamera(0.016);
  });
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'C:/Users/sanguk/.gemini/antigravity/brain/a896b356-c2d7-47f3-ab26-8e8f7bd2ba8f/stairs_b1_view.png' });
  console.log('Saved stairs_b1_view.png');

} finally {
  await browser.close();
}
