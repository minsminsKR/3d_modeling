import { createRequire } from "node:module";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const shots = [
  {
    file: "f1_av_crt.png",
    chunks: [[1, -1], [1, 0]],
    status: "시청각실입니다. 화면이 꺼져 있어도 교실을 보고 있습니다.",
    x: 18.2, y: 0, z: -13.8,
    lookAt: [19.75, 1.18, -12.25],
  },
  {
    file: "f1_supply_bars.png",
    chunks: [[0, -2], [0, -1]],
    status: "비품실입니다. 철창 안에 어제 출석이 잠겨 있습니다.",
    x: 2.05, y: 0, z: -33.4,
    lookAt: [3.75, 1.05, -35.75],
  },
  {
    file: "f1_counsel_altar.png",
    chunks: [[-1, -2], [-1, -1]],
    status: "상담실입니다. 제단이 이름을 듣고 있습니다.",
    x: -16.0, y: 0, z: -28.55,
    lookAt: [-16.0, 0.92, -32.0],
  },
  {
    file: "f1_broadcast_tv.png",
    chunks: [[1, -2], [1, -1]],
    status: "방송 창고입니다. 화면이 아직 당신을 셉니다.",
    x: 16.0, y: 0, z: -33.15,
    lookAt: [16.0, 0.95, -36.45],
  },
  {
    file: "f1_stair_wet.png",
    chunks: [[1, 1], [0, 1]],
    status: "지하로 가는 복도입니다. 콘 너머로 물이 마릅니다.",
    x: 16.0, y: 0, z: 15.55,
    lookAt: [11.45, 0.55, 14.48],
  },
  {
    file: "f1_nursery_west.png",
    chunks: [[2, 1], [1, 1]],
    status: "보육 복도입니다. 요람이 서쪽 교실에 있습니다.",
    x: 31.15, y: 0, z: 13.15,
    lookAt: [30.52, 0.5, 10.65],
  },
  {
    file: "f1_doll_figure.png",
    chunks: [[-2, 1], [-1, 1]],
    status: "인형 복도입니다. 선반의 눈을 마주치지 마십시오.",
    x: -31.15, y: 0, z: 13.15,
    lookAt: [-33.52, 0.95, 10.95],
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
