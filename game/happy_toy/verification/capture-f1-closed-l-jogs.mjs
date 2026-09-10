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
    name: "f1_laundry_nw_l_jog.png",
    cx: 4, cz: -1, x: 59.6, y: 0, z: -22.3, lookAt: [58.5, 1.1, -19.4],
  },
  {
    name: "f1_specimen_nw_l_jog.png",
    cx: 6, cz: -1, x: 91.6, y: 0, z: -22.4, lookAt: [90.8, 1.1, -19.6],
  },
  {
    name: "f1_doll_sw_l_jog.png",
    cx: -2, cz: 1, x: -37.2, y: 0, z: 20.85, lookAt: [-35.5, 1.1, 20.15],
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
    game.hud?.setStatus?.("닫힌 교실 L자 칸막이", 2400);
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
