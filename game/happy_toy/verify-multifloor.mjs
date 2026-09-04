// Verification script: check multi-floor implementation correctness
// Run with: node verify-multifloor.mjs

import { chromium } from 'playwright';

const URL = process.argv[2] || 'http://127.0.0.1:8010/';

async function run() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  
  page.on('console', msg => {
    if (msg.type() === 'error') {
      console.log(`[CONSOLE ERROR] ${msg.text()}`);
    }
  });
  
  page.on('pageerror', err => {
    console.log(`[PAGE ERROR] ${err.message}`);
  });

  console.log('Loading game...');
  await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 30000 });
  
  await page.waitForFunction(() => {
    return window.__happyToy && window.__happyToy.mapBuilder;
  }, { timeout: 20000 });

  await page.evaluate(() => {
    const game = window.__happyToy;
    if (game.titleMenu && game.titleMenu.startGame) {
      game.titleMenu.startGame('normal');
    }
    game.testSafeMode = true;
  });
  
  await page.waitForTimeout(2000);

  const results = await page.evaluate(() => {
    const game = window.__happyToy;
    const generator = game.mapBuilder?.generator;
    const collisionWorld = game.collisionWorld;
    const checks = {};
    
    if (!generator) return { error: 'No generator found' };
    
    const chunksData = generator.chunksData;
    
    // 1. Wall mesh Y positions for non-1F chunks
    const wallTests = [];
    for (const [key, chunk] of chunksData.entries()) {
      if (chunk.cy === 0) continue;
      const expectedWallY = chunk.floorY + 1.4;
      let wallMeshY = null;
      let foundWallMesh = false;
      
      for (const mesh of chunk.meshes) {
        if (mesh.name && mesh.name.includes('_walls_inst')) {
          foundWallMesh = true;
          const matrix = new window.THREE.Matrix4();
          mesh.getMatrixAt(0, matrix);
          const pos = new window.THREE.Vector3();
          pos.setFromMatrixPosition(matrix);
          wallMeshY = pos.y;
          break;
        }
      }
      wallTests.push({ key, cy: chunk.cy, floorY: chunk.floorY, expectedWallY, wallMeshY, foundWallMesh, correct: foundWallMesh && Math.abs(wallMeshY - expectedWallY) < 0.5 });
    }
    checks.wallPositions = wallTests;
    
    // 2. Ramp data completeness
    const ramps = collisionWorld.ramps;
    checks.ramps = ramps.map(r => ({
      id: r.id, axis: r.axis, startY: r.startY, endY: r.endY,
      minX: r.minX, maxX: r.maxX, minZ: r.minZ, maxZ: r.maxZ,
      hasAxis: !!r.axis,
      hasBounds: Number.isFinite(r.minX) && Number.isFinite(r.maxX) && Number.isFinite(r.minZ) && Number.isFinite(r.maxZ),
    }));
    
    // 3. findRampAt detection
    checks.rampDetection = ramps.map(ramp => {
      const midX = ((ramp.minX || 0) + (ramp.maxX || 0)) / 2;
      const midZ = ((ramp.minZ || 0) + (ramp.maxZ || 0)) / 2;
      const found = collisionWorld.findRampAt({ x: midX, y: (ramp.startY + ramp.endY) / 2, z: midZ });
      return { id: ramp.id, found: !!found };
    });
    
    // 4. Transition waypoints
    checks.transitionWaypoints = collisionWorld.transitionWaypoints.map(tw => ({ id: tw.id, floor: tw.floor, links: tw.links }));
    
    // 5. Waypoint Y for non-1F
    checks.waypointYValues = [];
    for (const [key, chunk] of chunksData.entries()) {
      if (chunk.cy === 0 || chunk.waypoints.length === 0) continue;
      checks.waypointYValues.push({ key, cy: chunk.cy, floorY: chunk.floorY, wpY: chunk.waypoints[0][1], correct: Math.abs(chunk.waypoints[0][1] - chunk.floorY) < 0.01 });
    }
    
    // 6. Light Y for non-1F
    checks.lightPositions = [];
    for (const [key, chunk] of chunksData.entries()) {
      if (chunk.cy === 0 || chunk.lights.length === 0) continue;
      const expectedY = chunk.floorY + 2.78;
      const actualY = chunk.lights[0].mesh.position.y;
      checks.lightPositions.push({ key, cy: chunk.cy, expectedY, actualY, correct: Math.abs(actualY - expectedY) < 0.1 });
    }
    
    // 7. Stair mesh count
    let stairMeshCount = 0;
    game.scene.traverse(obj => { if (obj.name && obj.name.includes('_stair_')) stairMeshCount++; });
    checks.stairMeshCount = stairMeshCount;
    
    // 8. Floor inference
    checks.floorInference = { y0: collisionWorld.getFloorForY(0), y5: collisionWorld.getFloorForY(5), yN5: collisionWorld.getFloorForY(-5) };
    
    return checks;
  });

  let allPass = true;
  console.log('\n=== MULTI-FLOOR VERIFICATION ===\n');
  
  // Walls
  for (const wt of results.wallPositions || []) {
    const s = wt.correct ? 'PASS' : 'FAIL'; if (!wt.correct) allPass = false;
    console.log(`[${s}] Wall chunk ${wt.key} cy=${wt.cy}: Y=${wt.wallMeshY?.toFixed(2)} (expect ${wt.expectedWallY.toFixed(2)})`);
  }
  
  // Ramps
  for (const rt of results.ramps || []) {
    const s = rt.hasAxis && rt.hasBounds ? 'PASS' : 'FAIL'; if (!rt.hasAxis || !rt.hasBounds) allPass = false;
    console.log(`[${s}] Ramp ${rt.id}: axis=${rt.axis}, bounds=[${rt.minX},${rt.maxX}]x[${rt.minZ},${rt.maxZ}]`);
  }
  
  // Ramp detection
  for (const rd of results.rampDetection || []) {
    const s = rd.found ? 'PASS' : 'FAIL'; if (!rd.found) allPass = false;
    console.log(`[${s}] findRampAt(${rd.id}): found=${rd.found}`);
  }
  
  // Transition waypoints
  const twc = (results.transitionWaypoints || []).length;
  const tws = twc >= 4 ? 'PASS' : 'FAIL'; if (twc < 4) allPass = false;
  console.log(`[${tws}] Transition waypoints: ${twc} (need >=4)`);
  
  // Waypoint Y
  for (const wt of results.waypointYValues || []) {
    const s = wt.correct ? 'PASS' : 'FAIL'; if (!wt.correct) allPass = false;
    console.log(`[${s}] Waypoint ${wt.key} cy=${wt.cy}: Y=${wt.wpY} (expect ${wt.floorY})`);
  }
  
  // Light Y
  for (const lt of results.lightPositions || []) {
    const s = lt.correct ? 'PASS' : 'FAIL'; if (!lt.correct) allPass = false;
    console.log(`[${s}] Light ${lt.key} cy=${lt.cy}: Y=${lt.actualY?.toFixed(2)} (expect ${lt.expectedY.toFixed(2)})`);
  }
  
  // Stairs
  const ss = results.stairMeshCount >= 20 ? 'PASS' : 'FAIL'; if (results.stairMeshCount < 20) allPass = false;
  console.log(`[${ss}] Stair meshes: ${results.stairMeshCount} (need >=20)`);
  
  // Floor inference
  const fi = results.floorInference || {};
  const fiOk = fi.y0 === 1 && fi.y5 === 2 && fi.yN5 === -1;
  if (!fiOk) allPass = false;
  console.log(`[${fiOk ? 'PASS' : 'FAIL'}] inferFloor: Y=0→${fi.y0}, Y=5→${fi.y5}, Y=-5→${fi.yN5}`);
  
  console.log(`\n=== ${allPass ? 'ALL PASSED' : 'SOME FAILED'} ===\n`);
  
  await browser.close();
  process.exit(allPass ? 0 : 1);
}

run().catch(err => { console.error('Failed:', err); process.exit(1); });
