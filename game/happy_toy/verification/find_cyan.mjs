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

  const check = await page.evaluate(() => {
    const game = window.__happyToy;
    const scene = game.scene;
    const found = [];
    scene.traverse((obj) => {
      if (obj.isMesh) {
        const hex = obj.material?.color?.getHexString() || '';
        if (hex.startsWith('06') || hex.startsWith('07') || hex.startsWith('33') || obj.name.includes('water') || obj.name.includes('b1')) {
          const wp = new THREE.Vector3();
          obj.getWorldPosition(wp);
          found.push({
            name: obj.name,
            geo: obj.geometry?.type,
            pos: [wp.x, wp.y, wp.z],
            hex,
            parent: obj.parent?.name || obj.parent?.type,
          });
        }
      }
    });
    return found;
  });
  console.log('Found cyan/blue/water objects:', check);

} finally {
  await browser.close();
}
