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
    name: "f1_skybridge_south_step.png",
    cx: 3, cz: 0, x: 50.35, y: 0, z: 0.18, lookAt: [46.4, 1.12, 1.72],
  },
  {
    name: "f1_angel_south_step.png",
    cx: -1, cz: 0, x: -14.15, y: 0, z: 0.12, lookAt: [-17.35, 1.1, 1.42],
  },
  {
    name: "f1_laundry_east_step.png",
    cx: 4, cz: -1, x: 64.15, y: 0, z: -14.35, lookAt: [65.35, 1.1, -18.15],
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
    game.hud?.setStatus?.("창 벽 단차", 2400);
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
