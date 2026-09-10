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
    name: "f1_annex_south_cheek.png",
    cx: 4, cz: 0, x: 66.4, y: 0, z: 0.15, lookAt: [68.2, 1.15, 0.95],
  },
  {
    name: "f1_memorial_north_jog.png",
    cx: 6, cz: 0, x: 96, y: 0, z: 0.2, lookAt: [93.2, 1.2, -1.15],
  },
  {
    name: "f1_nursery_east_dogleg.png",
    cx: 2, cz: 1, x: 32, y: 0, z: 18.4, lookAt: [33.2, 1.15, 20.4],
  },
  {
    name: "f1_laundry_stagger_baffle.png",
    cx: 4, cz: -1, x: 64, y: 0, z: -16, lookAt: [63.1, 1.15, -19.6],
  },
  {
    name: "f1_tea_nw_l_jog.png",
    cx: -1, cz: 1, x: -18.4, y: 0, z: 12.6, lookAt: [-19.8, 1.1, 11.4],
  },
  {
    name: "f1_archive_nw_l_jog.png",
    cx: -2, cz: -1, x: -36.4, y: 0, z: -19.6, lookAt: [-37.6, 1.1, -18.4],
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
    game.hud?.setStatus?.("고유 복도 꺾임", 2400);
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
