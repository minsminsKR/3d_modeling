import assert from "node:assert/strict";
import { createRequire } from "node:module";

const { chromium } = createRequire(import.meta.url)("playwright");
const url = process.argv[2] || "http://127.0.0.1:8010/";
const executablePath = process.env.CHROME_PATH
  || (process.platform === "win32" ? "C:/Program Files/Google/Chrome/Application/chrome.exe" : undefined);

const browser = await chromium.launch({ executablePath, headless: true });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForFunction(() => window.__happyToy?.assetsReady === true, null, { timeout: 90000 });
  await page.evaluate(() => window.__happyToy.start());

  const result = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder.generator;
    const collision = game.collisionWorld;

    const playableTypes = [];
    const visited = new Set();
    const queue = [[0, 0]];
    visited.add("0,0");
    const mismatched = [];
    const allVisited = new Set(["0,0"]);
    const allQueue = [[0, 0]];

    while (queue.length) {
      const [cx, cz] = queue.shift();
      const open = generator.getOpenings(cx, cz);
      const faces = [
        ["N", 0, -1, "S"],
        ["S", 0, 1, "N"],
        ["E", 1, 0, "W"],
        ["W", -1, 0, "E"],
      ];
      for (const [face, dx, dz, opposite] of faces) {
        if (!open[face]) continue;
        const nx = cx + dx;
        const nz = cz + dz;
        const back = generator.getOpenings(nx, nz);
        if (!back[opposite]) {
          mismatched.push(`${cx},${cz} ${face} -> ${nx},${nz}`);
        }
        const key = `${nx},${nz}`;
        if (!visited.has(key) && Math.abs(nx) <= 2 && Math.abs(nz) <= 2) {
          visited.add(key);
          queue.push([nx, nz]);
        }
      }
    }

    while (allQueue.length) {
      const [cx, cz] = allQueue.shift();
      const open = generator.getOpenings(cx, cz);
      const faces = [
        ["N", 0, -1, "S"],
        ["S", 0, 1, "N"],
        ["E", 1, 0, "W"],
        ["W", -1, 0, "E"],
      ];
      for (const [face, dx, dz, opposite] of faces) {
        if (!open[face]) continue;
        const nx = cx + dx;
        const nz = cz + dz;
        const key = `${nx},${nz}`;
        if (!allVisited.has(key) && generator.isPlayableChunk(nx, nz)) {
          allVisited.add(key);
          allQueue.push([nx, nz]);
        }
      }
    }

    for (let cx = -2; cx <= 2; cx += 1) {
      for (let cz = -2; cz <= 2; cz += 1) {
        const chunk = generator.generateChunk(cx, cz);
        playableTypes.push({
          key: `${cx},${cz}`,
          type: chunk.type,
          meshCount: chunk.meshes.length,
        });
      }
    }

    let radius = 0;
    while (radius < 20 && generator.isPlayableChunk(radius, 0)) radius += 1;
    const voidChunk = generator.generateChunk(radius, 0);
    const ringChunk = generator.generateChunk(3, 0);
    const farChunk = generator.generateChunk(8, 0);
    const beyondChunk = generator.generateChunk(9, 0);
    const ringOpen = generator.getOpenings(3, 0);
    const annexOpen = generator.getOpenings(4, 0);
    const inverted = [];
    for (const chunk of generator.chunksData.values()) {
      if (!chunk.safeLights || Math.abs(chunk.cx) > 2 || Math.abs(chunk.cz) > 2) continue;
      for (const light of chunk.safeLights) {
        if (light.variant !== "wall-switch") continue;
        const localX = light.position.x - chunk.center.x;
        const localZ = light.position.z - chunk.center.z;
        if (Math.abs(localX) > 8.2 || Math.abs(localZ) > 8.2) continue;
        const faceX = -Math.sin(light.yaw);
        const faceZ = -Math.cos(light.yaw);
        let expectedX = 0;
        let expectedZ = 0;
        if (Math.abs(localZ) > 6 && Math.abs(localZ) > Math.abs(localX) + 2.5) {
          expectedZ = localZ > 0 ? -1 : 1;
        } else if (Math.abs(localX) > 6 && Math.abs(localX) > Math.abs(localZ) + 2.5) {
          expectedX = localX > 0 ? -1 : 1;
        } else if (Math.abs(Math.abs(localX) - 1.2) < 0.55 && Math.abs(localZ) > 2.8) {
          expectedX = localX > 0 ? -1 : 1;
        } else if (Math.abs(Math.abs(localZ) - 1.2) < 0.55 && Math.abs(localX) > 2.8) {
          expectedZ = localZ > 0 ? -1 : 1;
        } else {
          continue;
        }
        const dot = faceX * expectedX + faceZ * expectedZ;
        if (dot < 0.7) {
          inverted.push({
            id: light.id,
            yaw: light.yaw,
            localX,
            localZ,
            faceX,
            faceZ,
            dot,
          });
        }
      }
    }

    const pathTo = (goal) => collision.findPath(
      { x: 0, y: 0.9, z: 0 },
      goal,
      0.34,
      { cellSize: 0.55, maxIterations: 12000 },
    );

    const alcoveStart = pathTo({ x: -5.1, y: 0.9, z: -5.1 });
    const alcoveCorridor = pathTo({ x: -4.5, y: 0.9, z: -11.5 });
    const omenRoom = pathTo({ x: -16, y: 0.9, z: -28.6 });
    const staticRoom = pathTo({ x: 16, y: 0.9, z: -28.6 });
    const westJog = pathTo({ x: -20.5, y: 0.9, z: 16 });
    const northJog = pathTo({ x: 0, y: 0.9, z: -20.5 });
    const longRing = pathTo({ x: 32, y: 0.9, z: 0 });
    const flickerSide = pathTo({ x: 16, y: 0.9, z: -16 });

    const sampleBlocked = (x, z) => collision.isCircleBlocked({ x, y: 0.9, z }, 0.34);
    const sampleWalkable = (x, z) => collision.getSurfaceAt({ x, y: 0.9, z }).walkable;

    return {
      reachable: visited.size,
      mismatched,
      playableVoid: playableTypes.filter((cell) => cell.type === "void").map((cell) => cell.key),
      voidType: voidChunk.type,
      voidMeshes: voidChunk.meshes.length,
      ringType: ringChunk.type,
      ringMeshes: ringChunk.meshes.length,
      ringWest: ringOpen.W,
      farType: farChunk.type,
      farMeshes: farChunk.meshes.length,
      beyondType: beyondChunk.type,
      beyondMeshes: beyondChunk.meshes.length,
      annexWest: annexOpen.W,
      allReachable: allVisited.size,
      expectedCells: 51,
      worldExtent: radius,
      inverted,
      alcoveStartLen: alcoveStart.length,
      alcoveCorridorLen: alcoveCorridor.length,
      omenLen: omenRoom.length,
      staticLen: staticRoom.length,
      westJogLen: westJog.length,
      northJogLen: northJog.length,
      longRingLen: longRing.length,
      flickerSideLen: flickerSide.length,
      alcoveStartBlocked: sampleBlocked(-5.1, -5.1),
      alcoveCorridorBlocked: sampleBlocked(-4.5, -11.5),
      omenWalkable: sampleWalkable(-16, -28.6),
      staticWalkable: sampleWalkable(16, -28.6),
    };
  });

  console.log(result);
  assert.equal(errors.length, 0, `page errors: ${errors.join(" | ")}`);
  assert.equal(result.mismatched.length, 0, `unilateral openings: ${result.mismatched.join(", ")}`);
  assert.equal(result.reachable, 25, `reachable playable cells: ${result.reachable}`);
  assert.equal(result.playableVoid.length, 0, `playable void cells: ${result.playableVoid.join(", ")}`);
  assert.equal(result.voidType, "void");
  assert.equal(result.voidMeshes, 0, "exterior hull must not build fake rooms");
  assert.notEqual(result.ringType, "void", "annex corridor east of the core must be a walkable hall");
  assert.ok(result.ringMeshes > 0, "annex corridor must build geometry");
  assert.equal(result.ringWest, true, "annex corridor must open into the core");
  assert.notEqual(result.farType, "void", "별관 must remain walkable");
  assert.ok(result.farMeshes > 0, "별관 must build geometry");
  assert.equal(result.beyondType, "void", "school must stop after the east annex");
  assert.equal(result.beyondMeshes, 0, "outside the four maps must stay empty");
  assert.equal(result.annexWest, true, "별관 must connect back toward the core");
  assert.ok(result.worldExtent <= 9, `1F should end at the annex, got ${result.worldExtent}`);
  assert.equal(result.allReachable, result.expectedCells, `full 1F graph should be ${result.expectedCells}, got ${result.allReachable}`);
  assert.equal(result.inverted.length, 0, `wall-switch facing inward failed: ${JSON.stringify(result.inverted, null, 2)}`);
  assert.equal(result.alcoveStartBlocked, false, "start-room alcove should be enterable");
  assert.equal(result.alcoveCorridorBlocked, false, "corridor side alcove should be enterable");
  assert.ok(result.alcoveStartLen > 0, "path from spawn to start alcove");
  assert.ok(result.alcoveCorridorLen > 0, "path from spawn to corridor alcove");
  assert.ok(result.omenLen > 0, "path from spawn to omen room");
  assert.ok(result.staticLen > 0, "path from spawn to static room");
  assert.ok(result.westJogLen > 0, "west 1F chicane must stay pathable past the baffle");
  assert.ok(result.northJogLen > 0, "north 1F chicane must stay pathable past the baffle");
  assert.ok(result.longRingLen > 0, "center school hall must continue into the next maze tile");
  assert.ok(result.flickerSideLen > 0, "flicker room must be reachable as a side loop");
  assert.equal(result.omenWalkable, true);
  assert.equal(result.staticWalkable, true);
  console.log("PASS: wall lamps face inward, alcoves and north rooms are walkable, void chunks stay empty");
} finally {
  await browser.close();
}
