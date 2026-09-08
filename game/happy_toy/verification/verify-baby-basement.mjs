import assert from "node:assert/strict";
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(process.argv[2] || "http://127.0.0.1:8010/", { waitUntil: "networkidle" });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 60000 });
  await page.click("#btn-start-game");
  const result = await page.evaluate(() => {
    const game = window.__happyToy;
    const baby = game.enemyManager.enemies.find((enemy) => enemy.isBaby);
    const intro = game.monsterIntroManager.events.find((event) => event.constructor.name === "BabyIntroEvent");
    const spawn = baby.group.position.toArray();
    const basementSpawn = spawn[1] === -5 && spawn[0] === 10.5 && spawn[2] === 30;

    // A flashlight aimed at the baby from the first floor must not wake it.
    game.flashlightController.enabled = true;
    game.player.position.set(10.5, 0, 30);
    game.camera.lookAt(10.5, -5, 30);
    baby.setDormant(true);
    baby.babyAwake = false;
    baby.update(0.1, { position: game.player.position, isSprinting: false, isHidden: false });
    const upperCannotWake = baby.isDormant;

    // The actual basement trigger reveals Baby and starts the intro state.
    game.player.position.set(14.5, -5, 32);
    intro.checkTrigger();
    const triggeredInBasement = intro.hasTriggered && intro.state === "cutscene"
      && !baby.isDormant && baby.group.position.y === -5;
    game.resetRunState();
    const resetDormant = baby.isDormant && !baby.group.visible && intro.state === "idle";
    return { spawn, basementSpawn, upperCannotWake, triggeredInBasement, resetDormant };
  });
  assert.equal(result.basementSpawn, true);
  assert.equal(result.upperCannotWake, true);
  assert.equal(result.triggeredInBasement, true);
  assert.deepEqual(errors, []);
  console.log("PASS: Baby basement spawn, floor isolation, basement intro trigger", result);
} finally {
  await browser.close();
}
