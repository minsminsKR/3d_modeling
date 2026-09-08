import assert from "node:assert/strict";
import { createRequire } from "node:module";

const { chromium } = createRequire(import.meta.url)("playwright");
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);
const browser = await chromium.launch({ executablePath, headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(process.argv[2] || "http://127.0.0.1:8010/", { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  const start = page.locator("#start-button, #btn-start-game");
  if (await start.count()) {
    await start.first().click({ timeout: 3000 }).catch(() => {});
  }
  const result = await page.evaluate(() => {
    const game = window.__happyToy;
    const baby = game.enemyManager.enemies.find((enemy) => enemy.isBaby);
    const intro = game.monsterIntroManager.events.find((event) => event.constructor.name === "BabyIntroEvent");
    const spawn = baby.group.position.toArray();
    const basementSpawn = Math.abs(spawn[1] + 5) < 0.6 && Math.abs(spawn[0] + 3.5) < 0.35 && Math.abs(spawn[2] - 28.5) < 0.35;

    game.flashlightController.enabled = true;
    game.player.position.set(-3.5, 0, 28.5);
    game.camera.lookAt(-3.5, -5, 28.5);
    baby.setDormant(true);
    baby.babyAwake = false;
    baby.state = "crying";
    baby.update(0.1, { position: game.player.position, isSprinting: false, isHidden: false });
    const upperCannotWake = baby.isDormant && !baby.babyAwake;

    game.player.position.set(14.5, -5, 32);
    intro.checkTrigger();
    const ignoredStairShaft = intro.hasTriggered === false;

    game.player.position.set(-0.2, -5, 30);
    intro.checkTrigger();
    const framedInsideRoom = Math.abs(intro.holdCameraPos.x + 0.4) < 0.05
      && Math.abs(intro.holdCameraPos.z - 31.0) < 0.05;
    const triggeredInBasement = intro.hasTriggered && intro.state === "cutscene"
      && !baby.isDormant && Math.abs(baby.group.position.y + 5) < 0.6 && baby.state === "cutscene"
      && framedInsideRoom;

    intro.timer = 4.2;
    intro.update(0.016);
    const silence = intro.getPhase() === "silence" && intro.cryCut === true && baby.mixer?.timeScale === 0;

    intro.timer = 6.2;
    intro.update(0.016);
    const facingDelta = Math.abs(Math.atan2(
      Math.sin(baby.group.rotation.y - intro.faceYaw),
      Math.cos(baby.group.rotation.y - intro.faceYaw),
    ));
    const turning = intro.getPhase() === "turn" && intro.turned && facingDelta < 1.2;

    intro.timer = 7.6;
    intro.update(0.016);
    const stingDist = Math.hypot(baby.group.position.x + 3.5, baby.group.position.z - 28.5);
    const sting = intro.getPhase() === "sting" && intro.stingPlayed && stingDist > 0.6 && game.cinematicLightScale < 0.2;

    intro.timer = 8.9;
    intro.update(0.2);
    const finished = intro.state === "done" && !intro.blocksPlayerControl && baby.state === "crying"
      && baby.babyAwake === false && game.cinematicLightScale === 1;

    baby.setDormant(false);
    baby.babyAwake = false;
    baby.state = "crying";
    baby.update(0.1, {
      position: game.player.position,
      isSprinting: true,
      isHidden: false,
      isUndetectable: true,
    });
    const ghostDoesNotWake = baby.babyAwake === false && baby.state === "crying";

    game.resetRunState();
    const resetBaby = game.enemyManager.enemies.find((enemy) => enemy.isBaby);
    const resetIntro = game.monsterIntroManager.events.find((event) => event.constructor.name === "BabyIntroEvent");
    const resetDormant = resetBaby.isDormant && !resetBaby.group.visible && resetIntro.state === "idle";
    return {
      spawn, basementSpawn, upperCannotWake, ignoredStairShaft, triggeredInBasement,
      silence, turning, sting, finished, ghostDoesNotWake, resetDormant,
    };
  });
  for (const [name, passed] of Object.entries(result)) {
    if (name === "spawn") continue;
    assert.equal(passed, true, name);
  }
  assert.deepEqual(errors, []);
  console.log("PASS: Baby basement spawn, horror cinematic, ghost isolation", result);
} finally {
  await browser.close();
}
