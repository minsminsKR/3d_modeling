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

  // Screenshot 1: Soul Gathering Title Screen
  await page.screenshot({ path: 'verification/soul-gathering-title.png' });
  console.log('Saved soul-gathering-title.png');

  // Start game via MenuSystem
  await page.evaluate(() => {
    window.__happyToy.menuSystem.hideMenu();
    window.__happyToy.start();
  });
  await page.waitForTimeout(1000);

  // Screenshot 2: Teleport into Flooded Canal (0, 1) and look down the water
  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: 0.0, y: 0.0, z: 16.0 });
    game.player.yaw = 0; // Look South into the flooded water corridor
    game.camera.rotation.set(-0.2, 0, 0, 'YXZ'); // slightly tilted down towards water floor
  });
  await page.waitForTimeout(800);

  await page.screenshot({ path: 'verification/soul-gathering-flooded-canal.png' });
  console.log('Saved soul-gathering-flooded-canal.png');

} finally {
  await browser.close();
}
