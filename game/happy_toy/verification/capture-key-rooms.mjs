import { createRequire } from "node:module";
import path from "node:path";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const outDir = process.argv[3] || "/opt/cursor/artifacts";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const shots = [
  {
    file: "f1_dorm_bunks.png",
    chunks: [[2, 2], [2, 1]],
    status: "생활관입니다. 요람은 비어 있습니다.",
    x: 35.1, y: 0, z: 35.2,
    lookAt: [37.85, 0.7, 37.35],
  },
  {
    file: "f1_dollclass_chairs.png",
    chunks: [[-2, 2], [-2, 1]],
    status: "인형 교실입니다. 눈이 마주치면 따라가십시오.",
    x: -28.2, y: 0, z: 34.1,
    lookAt: [-26.55, 0.55, 37.45],
  },
  {
    file: "f1_prepstore_crates.png",
    chunks: [[2, -2], [2, -1]],
    status: "준비물 창고입니다. 선반 뒤에 도자기 열쇠가 있습니다.",
    x: 35.1, y: 0, z: -35.1,
    lookAt: [37.45, 0.55, -37.45],
  },
  {
    file: "f1_closedlib_table.png",
    chunks: [[-2, -2], [-2, -1]],
    status: "폐관 도서실입니다. 대출 장부를 읽지 마십시오.",
    x: -29.2, y: 0, z: -29.2,
    lookAt: [-26.85, 0.5, -26.85],
  },
  {
    file: "f1_etiquette_cushions.png",
    chunks: [[-1, 2], [-1, 1]],
    status: "예절실입니다. 신발을 신지 마십시오.",
    x: -16.0, y: 0, z: 28.2,
    lookAt: [-13.65, 0.2, 29.65],
  },
  {
    file: "f1_lostfound_boxes.png",
    chunks: [[-2, 0], [-2, -1]],
    status: "분실물함입니다. 어제 신발을 찾지 마십시오.",
    x: -32.0, y: 0, z: 2.15,
    lookAt: [-33.48, 0.5, 5.35],
  },
  {
    file: "f1_roofhall_board.png",
    chunks: [[0, 2], [0, 1]],
    status: "옥상 복도입니다. 문이 판자로 막혀 있습니다.",
    x: 1.6, y: 0, z: 34.2,
    lookAt: [0.2, 1.2, 38.55],
  },
  {
    file: "f1_eastwash_buckets.png",
    chunks: [[2, 0], [1, 0]],
    status: "화장실 문이 안쪽에서 잠겨 있습니다. 물을 틀지 마십시오.",
    x: 32.0, y: 0, z: 0.4,
    lookAt: [36.55, 0.4, -1.52],
  },
  {
    file: "f1_angelhall_cart.png",
    chunks: [[-1, 0], [0, 0]],
    status: "도서실입니다. 창이 판자로 막혀 있습니다. 책을 믿지 마십시오.",
    x: -14.2, y: 0, z: 0.35,
    lookAt: [-11.45, 0.5, -1.52],
  },
  {
    file: "f1_washfour_cubbies.png",
    chunks: [[5, 0], [4, 0]],
    status: "창밖은 복도의 몫입니다. 운동장은 없습니다.",
    x: 80.0, y: 0, z: 0.4,
    lookAt: [84.55, 0.5, -1.52],
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
