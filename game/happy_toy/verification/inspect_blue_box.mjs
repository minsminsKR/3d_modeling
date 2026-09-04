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

  const found = await page.evaluate(() => {
    const game = window.__happyToy;
    const items = [];
    game.scene.traverse((obj) => {
      if (obj.isMesh) {
        const wp = new THREE.Vector3();
        obj.getWorldPosition(wp);
        // Look for meshes near player position (0, 0, -14)
        if (Math.abs(wp.x) < 3 && Math.abs(wp.z - (-14)) < 4) {
          items.push({
            name: obj.name,
            pos: [wp.x, wp.y, wp.z],
            geo: obj.geometry?.type,
            mat: obj.material?.type,
            color: obj.material?.color?.getHexString(),
            parent: obj.parent?.name || obj.parent?.type,
          });
        }
      }
    });
    return items;
  });
  console.log('Meshes around (0, 0, -14):', JSON.stringify(found, null, 2));

} finally {
  await browser.close();
}
