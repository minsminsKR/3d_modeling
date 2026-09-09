import { createRequire } from "node:module";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.on("pageerror", (error) => console.error("pageerror", error.message));

await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
await page.evaluate(() => window.__happyToy.start());
await page.waitForTimeout(400);

async function poseShot(name, pose) {
  const info = await page.evaluate((options) => {
    const game = window.__happyToy;
    game.testSafeMode = true;
    game.player.noclip = true;
    if (Number.isFinite(options.x)) {
      game.player.setPosition({ x: options.x, y: options.y, z: options.z });
      for (let i = 0; i < 20; i += 1) game.update(0.05);
    }
    return game.poseForCapture({
      yaw: options.yaw,
      pitch: options.pitch,
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, pose);
  await page.screenshot({ path: path.join(outDir, name) });
  console.log(name, info);
  return info;
}

const altar = await poseShot("shrine_altar_names.png", {
  x: 0, y: 0, z: 2.55, lookAt: [0, 0.78, 0],
});
const annex = await poseShot("annex_gate_sign.png", {
  x: 58.4, y: 0, z: 0.15, lookAt: [64, 2.28, 0],
});
const nurse = await poseShot("annex_nurse_office.png", {
  x: 80, y: 0, z: -12.4, lookAt: [82.1, 0.7, -13.6],
});
const faculty = await poseShot("annex_faculty_office.png", {
  x: 96, y: 0, z: 12.6, lookAt: [92.8, 0.7, 16],
});
const science = await poseShot("annex_science_lab.png", {
  x: 96, y: 0, z: -28.6, lookAt: [92.6, 0.9, -32],
});

if (!altar.flashlight || altar.intensity < 8) {
  throw new Error(`altar capture failed: ${JSON.stringify(altar)}`);
}
if (!annex.flashlight || annex.intensity < 8) {
  throw new Error(`annex capture failed: ${JSON.stringify(annex)}`);
}
if (!nurse.flashlight || faculty.intensity < 8 || science.intensity < 8) {
  throw new Error("annex room captures lost the beam");
}

console.log("ok");
await browser.close();
