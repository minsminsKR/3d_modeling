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

  const meshes = await page.evaluate(() => {
    const game = window.__happyToy;
    const scene = game.scene;
    const items = [];
    scene.traverse((obj) => {
      if (obj.isMesh && obj.position.distanceTo(new THREE.Vector3(0, 0, -8)) < 12) {
        items.push({
          name: obj.name,
          geo: obj.geometry?.type,
          pos: [obj.position.x, obj.position.y, obj.position.z],
          color: obj.material?.color ? obj.material.color.getHexString() : null,
          visible: obj.visible,
        });
      }
    });
    return items;
  });
  console.log('Nearby meshes around door N:', JSON.stringify(meshes, null, 2));

} finally {
  await browser.close();
}
