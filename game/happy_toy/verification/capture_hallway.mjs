import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
import path from 'node:path';

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

  // Open North door and walk into corridor
  await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;
    const doors = game.doors;
    const northDoor = doors.find(d => d.id === 'chunk_0_0_door_n');
    if (northDoor) {
      northDoor.isOpen = true;
      if (northDoor.doorMesh) northDoor.doorMesh.position.x += 1.8;
    }
    
    // Enable flashlight
    game.flashlightController.enabled = true;
    game.flashlightController.applyState(false);

    // Position player just past the doorway in chunk (0, -1) facing north
    player.position.set(0, 0, -10.5);
    player.yaw = 0; // facing -Z (North)
    player.pitch = -0.05;
    player.updateCamera(0.016);
  });
  await page.waitForTimeout(600);

  const destPath = 'C:/Users/sanguk/.gemini/antigravity/brain/a896b356-c2d7-47f3-ab26-8e8f7bd2ba8f/shadow-corridor-hallway-v2.png';
  await page.screenshot({ path: destPath });
  console.log('Saved screenshot to:', destPath);

} finally {
  await browser.close();
}
