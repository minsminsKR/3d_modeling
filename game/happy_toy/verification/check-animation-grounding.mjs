import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Analyzing character animation grounding (Free Play vs Stiff Snap)...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 8000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 8000 });

  const groundingData = await page.evaluate(() => {
    const game = window.__happyToy;
    const THREE = window.THREE || window.__happyToyTHREE;

    // Teleport player to load chunks
    game.player.setPosition({ x: 0, y: 0, z: 0 });
    game.updateBackrooms(0.016);
    game.enemyManager.update(0.016, { position: game.player.position, isSprinting: false });

    const results = {};

    function analyzeAnimationFree(characterObj, actionName, isDoll = false) {
      const modelRoot = characterObj.modelRoot;
      const group = characterObj.group;
      const mixer = characterObj.mixer;
      const actions = characterObj.actions;
      
      const action = actions[actionName];
      if (!action) return null;

      // Reset action
      mixer.stopAllAction();
      action.reset();
      action.play();
      action.weight = 1.0;
      
      // Get foot bones
      const footBoneNames = [];
      modelRoot.traverse(child => {
        if (child.isBone) {
          const nameLower = child.name.toLowerCase();
          if (nameLower.includes("foot") || nameLower.includes("toe")) {
            footBoneNames.push(child.name);
          }
        }
      });

      // 1. First, snap to ground in BIND POSE (first frame) to establish base offset
      mixer.update(0); // bind pose
      if (isDoll) {
        characterObj.snapModelToGround();
      } else {
        characterObj.snapModelToGround(false);
      }
      modelRoot.updateMatrixWorld(true);

      const baseModelRootY = modelRoot.position.y;

      // 2. Now play the animation loop WITHOUT snapping on each frame
      let minFootYAcrossLoop = Infinity;
      let maxFootYAcrossLoop = -Infinity;
      const steps = 40;
      const dt = 0.05;

      for (let step = 0; step < steps; step++) {
        mixer.update(dt);
        modelRoot.updateMatrixWorld(true);

        let stepMinFootY = Infinity;
        modelRoot.traverse(child => {
          if (child.isBone && footBoneNames.includes(child.name)) {
            const worldPos = new THREE.Vector3();
            child.getWorldPosition(worldPos);
            if (worldPos.y < stepMinFootY) {
              stepMinFootY = worldPos.y;
            }
          }
        });

        if (stepMinFootY < minFootYAcrossLoop) {
          minFootYAcrossLoop = stepMinFootY;
        }
        if (stepMinFootY > maxFootYAcrossLoop) {
          maxFootYAcrossLoop = stepMinFootY;
        }
      }

      return {
        actionName,
        baseModelRootY,
        minFootY: minFootYAcrossLoop, // under free play
        maxFootY: maxFootYAcrossLoop, // under free play
        lowestPointInAir: minFootYAcrossLoop - group.position.y // distance from floor
      };
    }

    // Uncat
    const uncat = game.enemyManager.enemies.find(e => e.config.id === "uncat");
    if (uncat) {
      results.uncat = {
        patrol: analyzeAnimationFree(uncat, "patrol"),
        chase: analyzeAnimationFree(uncat, "chase")
      };
    }

    // Cyclopse
    const cyclopse = game.enemyManager.enemies.find(e => e.config.id === "cyclopse");
    if (cyclopse) {
      results.cyclopse = {
        patrol: analyzeAnimationFree(cyclopse, "patrol"),
        chase: analyzeAnimationFree(cyclopse, "chase")
      };
    }

    // Baby
    const baby = game.enemyManager.enemies.find(e => e.config.id === "baby-workshop");
    if (baby) {
      baby.babyAwake = true;
      results.baby = {
        crying: analyzeAnimationFree(baby, "crying"),
        chase: analyzeAnimationFree(baby, "chase")
      };
    }

    // Lovely Doll
    if (game.lovelyDolls && game.lovelyDolls.length > 0) {
      const doll = game.lovelyDolls[0];
      results.lovelyDoll = {
        dance: analyzeAnimationFree(doll, "dance", true),
        walking: analyzeAnimationFree(doll, "walking", true),
        run: analyzeAnimationFree(doll, "run", true)
      };
    }

    return results;
  });

  console.log("GROUNDING LOOP ANALYSIS (FREE PLAY):");
  console.log(JSON.stringify(groundingData, null, 2));

} catch (err) {
  console.error("Error during grounding analysis:", err);
} finally {
  await browser.close();
}
