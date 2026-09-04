import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const url = process.argv[2] || 'http://127.0.0.1:8010/';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

console.log('Starting Stamina Smooth Walking Transition Verification...');
const browser = await chromium.launch({
  executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

try {
  await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 15000 });

  // Start game
  await page.evaluate(() => {
    window.__happyToy.menuSystem.hideMenu();
    window.__happyToy.start();
  });
  await page.waitForTimeout(500);

  // Test stamina drain while sprinting and ensure smooth walking transition
  const staminaTest = await page.evaluate(() => {
    const game = window.__happyToy;
    const player = game.player;

    // Simulate holding Shift and W
    player.input.keys['shift'] = true;
    player.input.keys['w'] = true;

    const recordedSpeeds = [];
    const recordedBobs = [];

    // Simulate 350 frames (~5.6 seconds of sprinting until stamina drains to 0 and beyond)
    for (let i = 0; i < 350; i++) {
      player.update(0.016);
      recordedSpeeds.push(player.isSprinting);
      recordedBobs.push(player.camera.position.y);
    }

    // Check after exhaustion: stamina should be low, isSprinting should be false, player is walking
    const exhaustedSprint = player.isSprinting;
    const finalStamina = player.stamina;

    // Check for rapid toggling (jitter count) in the last 150 frames (where stamina is 0)
    let flipCount = 0;
    for (let i = 200; i < recordedSpeeds.length - 1; i++) {
      if (recordedSpeeds[i] !== recordedSpeeds[i + 1]) {
        flipCount++;
      }
    }

    // Check camera Y variance in the last 100 frames
    const lateBobs = recordedBobs.slice(250);
    const maxBob = Math.max(...lateBobs);
    const minBob = Math.min(...lateBobs);
    const bobRange = maxBob - minBob;

    return {
      finalStamina,
      exhaustedSprint,
      flipCount,
      bobRange,
    };
  });

  console.log('Stamina test results:', staminaTest);
  assert(!staminaTest.exhaustedSprint, 'Expected player to NOT be sprinting after stamina exhaustion');
  assert(staminaTest.flipCount === 0, 'Expected 0 jitter flips while stamina is exhausted');
  assert(staminaTest.bobRange < 0.15, 'Expected smooth camera bobbing under 0.15m');
  console.log('-> Smooth Stamina Walking Transition PASSED with ZERO jitter!');

} finally {
  await browser.close();
}
