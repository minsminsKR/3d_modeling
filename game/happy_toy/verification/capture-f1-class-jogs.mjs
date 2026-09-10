import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const shots = [
  {
    name: "f1_laundry_west_class_jog.png",
    cx: 4, cz: -1, x: 63.42, y: 0, z: -14.72, lookAt: [62.58, 1.08, -12.95],
  },
  {
    name: "f1_tea_north_class_jog.png",
    cx: -1, cz: 1, x: -15.42, y: 0, z: 15.38, lookAt: [-17.55, 1.08, 14.48],
  },
  {
    name: "f1_lab_south_class_jog.png",
    cx: 5, cz: -2, x: 81.55, y: 0, z: -31.38, lookAt: [82.85, 1.08, -30.42],
  },
];

for (const shot of shots) {
  const outPath = path.join(outDir, shot.name);
  if (fs.existsSync(outPath)) {
    throw new Error(`refusing to overwrite immutable artifact ${outPath}`);
  }
}

const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.on("pageerror", (error) => console.error("pageerror", error.message));

await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
await page.evaluate(() => window.__happyToy.start());
await page.waitForTimeout(400);

async function poseShot(shot) {
  const info = await page.evaluate((options) => {
    const game = window.__happyToy;
    game.testSafeMode = true;
    game.player.noclip = true;
    if (game.player.isHidden) game.exitCabinet();
    game.floorHuntDirector?.hide?.();
    game.mapBuilder.generator.generateChunk(options.cx, options.cz);
    game.player.setPosition({ x: options.x, y: options.y, z: options.z });
    for (let i = 0; i < 16; i += 1) game.update(0.05, { skipRender: true });
    if (game.player.isHidden) game.exitCabinet();
    game.floorHuntDirector?.hide?.();
    game.hud?.setPrompt?.("");
    game.hud?.setStatus?.("교실 장내벽 단차", 2400);
    return game.poseForCapture({
      lookAt: options.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, shot);
  const outPath = path.join(outDir, shot.name);
  await page.screenshot({ path: outPath, timeout: 120000 });
  console.log(shot.name, info);
}

for (const shot of shots) {
  await poseShot(shot);
}

await browser.close();
