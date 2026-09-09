import { createRequire } from "node:module";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const shots = [
  {
    file: "f1_archive_cases.png",
    chunks: [[-2, -1], [-2, 0]],
    status: "서고 앞 복도입니다. 철 상자를 열지 마십시오.",
    x: -31.15, y: 0, z: -13.15,
    lookAt: [-33.48, 0.8, -21.35],
  },
  {
    file: "f1_storage_crates.png",
    chunks: [[2, -1], [2, 0]],
    status: "창고 앞 복도입니다. 상자가 어제 출석을 담고 있습니다.",
    x: 31.15, y: 0, z: -13.15,
    lookAt: [30.52, 0.5, -21.35],
  },
  {
    file: "f1_tea_benches.png",
    chunks: [[-1, 1], [0, 1]],
    status: "다실 앞 복도입니다. 신발을 신지 마십시오.",
    x: -16.0, y: 0, z: 15.55,
    lookAt: [-20.55, 0.4, 14.48],
  },
];

const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
page.on("pageerror", (error) => console.error("pageerror", error.message));

await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
await page.evaluate(() => window.__happyToy.start());
await page.waitForTimeout(300);

for (const shot of shots) {
  const pose = await page.evaluate((shot) => {
    const game = window.__happyToy;
    game.testSafeMode = true;
    game.cutsceneEvent = null;
    game.monsterIntroManager?.reset?.();
    for (const [cx, cz] of shot.chunks) game.mapBuilder.generator.generateChunk(cx, cz);
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    uncat?.setDormant?.(true);
    if (uncat) uncat.group.visible = false;
    game.hud.setStatus(shot.status, 2800);
    return game.poseForCapture({
      x: shot.x,
      y: shot.y,
      z: shot.z,
      lookAt: shot.lookAt,
      flashlight: true,
      freezeLoop: true,
    });
  }, shot);
  await page.evaluate(() => window.__happyToy.renderer.render(window.__happyToy.scene, window.__happyToy.camera));
  await page.screenshot({ path: path.join(outDir, shot.file), timeout: 120000 });
  console.log(shot.file, pose);
}

await browser.close();
console.log("ok");
