import { createRequire } from "node:module";
import assert from "node:assert/strict";
const { chromium } = createRequire(import.meta.url)("playwright");
const browser = await chromium.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto(process.argv[2] || "http://127.0.0.1:8010/");
  await page.waitForFunction(() => window.__happyToy?.assetsReady, null, { timeout: 60000 });
  const result = await page.evaluate(() => {
    const g = window.__happyToy;
    const visible = g.keys.filter(k => k.isAvailable && !k.isCollected);
    const key = visible[visible.length - 1];
    g.player.position.copy(key.position);
    const nearest = g.getCompassTarget() === key;
    let calls = 0;
    let source;
    const notify = g.enemyManager.notifyNoiseEvent.bind(g.enemyManager);
    g.enemyManager.notifyNoiseEvent = (position, radius, options) => { calls++; source = options.source; return notify(position, radius, options); };
    const before = g.keyCount;
    g.collectKey(key);
    g.collectKey(key);
    const single = g.keyCount === before + 1 && calls === 0 && g.dreadDirector.phase === "warning";
    g.hud.compassActive = true;
    g.hud.updateCompass({ x: 0, y: 0, z: 0 }, { x: -10, y: 3.4, z: 0 }, Math.PI / 2);
    const facing = g.hud.compassNeedle.style.transform === "rotate(0deg)";
    const upstairs = g.hud.compassText.textContent.includes("위층");
    g.hud.updateCompass(g.player.position, null, 0);
    const hidden = g.hud.compassNeedle.style.visibility === "hidden";
    g.keyCount = g.keys.length;
    const exit = g.getCompassTarget() === g.finalExit;
    // Exercise the entry decision with controlled visibility, without moving real enemies.
    const cabinet = g.cabinets[0];
    const enemy = {
      isDormant: false, hasVisualContact: true,
      isActivelyChasing: () => true, isSameLevelAs: () => true,
      group: { position: g.player.position.clone() },
      beginCabinetInvestigation() {},
    };
    g.enemyManager.enemies = [enemy];
    g.enemyManager.getClosestChasingEnemy = () => enemy;
    g.collisionWorld.hasLineOfSight = () => true;
    g.player.enterCabinet = () => {};
    g.player.isHidden = false;
    g.enterCabinet(cabinet);
    const witnessed = g.cabinetEvent.outcome === "caught";
    g.collisionWorld.hasLineOfSight = () => false;
    g.enterCabinet(cabinet);
    const cover = g.cabinetEvent.outcome === "safe";
    return { nearest, single, facing, upstairs, hidden, exit, witnessed, cover };
  });
  for (const [name, passed] of Object.entries(result)) assert.equal(passed, true, name);
  assert.deepEqual(errors, []);
  console.log("Relic loop passed:", result);
} finally { await browser.close(); }
