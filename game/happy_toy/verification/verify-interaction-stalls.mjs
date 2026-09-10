import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)('playwright');
const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto(process.argv[2] || 'http://127.0.0.1:8014/');
  await page.waitForFunction(() => window.__happyToy?.assetsReady, null, { timeout: 120000 });
  const result = await page.evaluate(async () => {
    const g = window.__happyToy;
    g.start(); g.loop.stop();
    const cabinet = g.cabinets.find(c => Math.abs(c.position.y) < 0.1);
    g.player.setPosition(cabinet.getExitPosition());
    g.player.setLookAt(cabinet.position.clone().add({x:0,y:1.4,z:0}));
    const render = () => { const t = performance.now(); g.renderer.render(g.scene, g.camera); return performance.now()-t; };
    render(); render();
    const rows = [];
    const sample = name => {
      const ms = render();
      let lights = 0; g.scene.traverseVisible(o => { if (o.isPointLight && o.layers.test(g.camera.layers)) lights++; });
      rows.push({name, ms, lights, programs:g.renderer.info.programs.length});
    };
    sample('baseline');
    for(let i=0;i<3;i++) {
      g.enterCabinet(cabinet, {forceOutcome:'safe'}); sample('hide');
      g.player.exitCabinet(); cabinet.setOccupied(false); sample('exit');
    }
    g.itemSystem.inventory.firecracker = 4;
    for(let i=0;i<3;i++) {
      g.input.handleKeyDown({key:'q',code:'KeyQ'});
      g.player.updateStaminaAndItemHotkeys(1/60);
      g.input.handleKeyUp({key:'q',code:'KeyQ'});
      g.input.endFrame(); sample('Q');
      for(let j=0;j<80;j++) g.itemSystem.update(1/60);
      sample('explosion');
      for(let j=0;j<30;j++) g.itemSystem.update(1/60);
      sample('cleanup');
    }
    // Overlapping throws exhaust the effect pool without allocating lights.
    g.itemSystem.inventory.firecracker = 5;
    for(let i=0;i<5;i++) g.itemSystem.useItem('firecracker',g.player);
    sample('five simultaneous throws');
    g.itemSystem.reset(); sample('reset with active fuses');
    const timings = [];
    g.testSafeMode = true;
    const intro=g.monsterIntroManager.events.find(e=>e.constructor.name==='UncatIntroEvent');
    intro.triggerEvent();intro.releaseControl();intro.state='done';
    for(let i=0;i<360;i++) {
      if(i===30) g.enterCabinet(cabinet,{forceOutcome:'safe'});
      if(i===90) {g.player.exitCabinet();cabinet.setOccupied(false);g.cabinetEvent=null;}
      if(i===100) g.itemSystem.useItem('firecracker',g.player);
      const t=performance.now(); g.update(1/60); timings.push(performance.now()-t);
      if(i%30===0) await new Promise(resolve=>requestAnimationFrame(resolve));
    }
    timings.sort((a,b)=>a-b);
    return {rows, projectiles:g.itemSystem.projectiles.length,
      frames:{count:timings.length,p50:timings[180],p95:timings[342],p99:timings[356],max:timings.at(-1)},
      freeLights:g.itemSystem.effectLights.every(l=>!l.userData.inUse)};
  });
  console.log(JSON.stringify({ ...result, errors }, null, 2));
  assert.deepEqual(errors, []);
  assert.equal(result.projectiles, 0);
  assert.equal(result.freeLights,true);
  if (!process.argv.includes('--baseline')) {
    assert.equal(new Set(result.rows.map(r=>r.lights)).size,1,'interactions must not change light shader layout');
    assert.ok(result.rows.every(r=>r.ms<150),'interaction render must not stall over 150ms');
    assert.ok(result.frames.max<150,'active chase/hide/firecracker loop must not stall over 150ms');
  }
} finally { await browser.close(); }
