import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Launching browser to inspect doll status...");
const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage();
try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 5000 });
  
  const status = await page.evaluate(() => {
    const game = window.__happyToy;
    const doll = game.lovelyDolls[0];
    if (!doll) return { error: "No doll in game.lovelyDolls array" };
    
    const root = doll.group;
    const pos = [root.position.x, root.position.y, root.position.z];
    const modelRootPos = doll.modelRoot ? [doll.modelRoot.position.x, doll.modelRoot.position.y, doll.modelRoot.position.z] : null;
    const scale = [root.scale.x, root.scale.y, root.scale.z];
    const modelRootScale = doll.modelRoot ? [doll.modelRoot.scale.x, doll.modelRoot.scale.y, doll.modelRoot.scale.z] : null;
    const visible = root.visible;
    
    // Check positions of meshes
    const meshDetails = [];
    root.traverse(c => {
      if (c.isMesh || c.isSkinnedMesh) {
        if (c.geometry) {
          if (!c.geometry.boundingBox) c.geometry.computeBoundingBox();
          const bbox = c.geometry.boundingBox;
          meshDetails.push({
            name: c.name,
            type: c.type,
            visible: c.visible,
            geometryType: c.geometry.type,
            geomMin: [bbox.min.x, bbox.min.y, bbox.min.z],
            geomMax: [bbox.max.x, bbox.max.y, bbox.max.z],
            meshScale: [c.scale.x, c.scale.y, c.scale.z],
            meshPos: [c.position.x, c.position.y, c.position.z],
            opacity: c.material ? (Array.isArray(c.material) ? c.material.map(m => m.opacity) : c.material.opacity) : null,
            transparent: c.material ? (Array.isArray(c.material) ? c.material.map(m => m.transparent) : c.material.transparent) : null,
          });
        }
      }
    });
    
    const inScene = Boolean(game.scene.getObjectByName(doll.id));
    
    return { pos, modelRootPos, scale, modelRootScale, visible, meshDetails, inScene };
  });
  
  console.log("Doll diagnostics:", JSON.stringify(status, null, 2));
} catch (err) {
  console.error("Diagnostics failed:", err);
} finally {
  await browser.close();
}
