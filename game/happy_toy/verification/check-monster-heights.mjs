import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Checking monster bone heights...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 8000 });

  const boneData = await page.evaluate(() => {
    const game = window.__happyToy;
    const THREE = window.THREE || window.__happyToyTHREE;
    
    // Teleport player to load chunks
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    game.enemyManager.update(0.016, { position: game.player.position, isSprinting: false });
    
    const results = {};

    function getFootAndHipsBones(modelRoot) {
      const bones = [];
      modelRoot.traverse(child => {
        if (child.isBone) {
          const nameLower = child.name.toLowerCase();
          if (nameLower.includes("foot") || nameLower.includes("toe") || nameLower.includes("hips") || nameLower.includes("root") || nameLower.includes("pelvis")) {
            child.updateMatrixWorld(true);
            const worldPos = new THREE.Vector3();
            child.getWorldPosition(worldPos);
            bones.push({
              name: child.name,
              worldY: worldPos.y,
              localY: child.position.y
            });
          }
        }
      });
      return bones;
    }

    // 1. Check Uncat
    const uncat = game.enemyManager.enemies.find(e => e.config.id === "uncat");
    if (uncat) {
      uncat.modelRoot.updateMatrixWorld(true);
      results.uncat = {
        state: uncat.state,
        action: uncat.currentActionName,
        groupY: uncat.group.position.y,
        modelRootY: uncat.modelRoot.position.y,
        bones: getFootAndHipsBones(uncat.modelRoot)
      };
    }

    // 2. Check Cyclopse
    const cyclopse = game.enemyManager.enemies.find(e => e.config.id === "cyclopse");
    if (cyclopse) {
      cyclopse.modelRoot.updateMatrixWorld(true);
      results.cyclopse = {
        state: cyclopse.state,
        action: cyclopse.currentActionName,
        groupY: cyclopse.group.position.y,
        modelRootY: cyclopse.modelRoot.position.y,
        bones: getFootAndHipsBones(cyclopse.modelRoot)
      };
    }

    // 3. Check Baby (crying state)
    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    if (baby) {
      baby.modelRoot.updateMatrixWorld(true);
      results.baby_crying = {
        state: baby.state,
        action: baby.currentActionName,
        groupY: baby.group.position.y,
        modelRootY: baby.modelRoot.position.y,
        bones: getFootAndHipsBones(baby.modelRoot)
      };

      // 4. Check Baby (chase state - Zombie Crawl)
      baby.babyAwake = true;
      baby.state = "chase";
      baby.playAction("chase", 0);
      
      // Let animation play for 1 second (approx 30 frames at 30fps)
      for (let i = 0; i < 30; i++) {
        baby.mixer.update(0.033);
        baby.snapModelToGround(false);
      }
      baby.modelRoot.updateMatrixWorld(true);
      
      results.baby_crawling = {
        state: baby.state,
        action: baby.currentActionName,
        groupY: baby.group.position.y,
        modelRootY: baby.modelRoot.position.y,
        bones: getFootAndHipsBones(baby.modelRoot)
      };
    }

    return results;
  });

  console.log("BONE HEIGHTS DATA:");
  console.log(JSON.stringify(boneData, null, 2));

} catch (err) {
  console.error("Error during check:", err);
} finally {
  await browser.close();
}
