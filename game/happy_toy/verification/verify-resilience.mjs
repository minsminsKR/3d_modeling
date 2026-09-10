import {chromium} from 'playwright';
import assert from 'node:assert/strict';
const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const url=process.argv[2]||'http://127.0.0.1:8014/';
try {
  const page=await browser.newPage();
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{
    localStorage.setItem('happy_toy_high_scores','{"corrupted":true}');
    localStorage.setItem('happy_toy_sensitivity','1.35');
    localStorage.setItem('happy_toy_quality','performance');
  });
  await page.goto(url);
  await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
  const result=await page.evaluate(()=>{
    const g=window.__happyToy;g.loop.stop();
    const settings={sensitivity:g.player.mouseSensitivity,quality:g.renderQuality,records:g.menuSystem.highScores.length};
    g.start();g.loop.stop();
    const intros=g.monsterIntroManager.events.filter(e=>e.constructor.name==='UncatIntroEvent'||e.constructor.name==='CyclopseIntroEvent');
    const floors=intros.map(e=>{g.player.position.copy(e.triggerPosition).y=-5;e.checkTrigger();return e.hasTriggered;});
    g.input.keys.add('w');window.dispatchEvent(new Event('blur'));
    return {settings,floors,keys:g.input.keys.size};
  });
  assert.ok(Math.abs(result.settings.sensitivity-.0022*1.35)<1e-8);
  assert.equal(result.settings.quality,'performance');assert.equal(result.settings.records,0);
  assert.deepEqual(result.floors,[false,false]);assert.equal(result.keys,0);assert.deepEqual(errors,[]);
  await page.close();
  for(const missing of ['**/characters/Uncat/**/Walking.fbx','**/characters/Uncat/**/model_textured.jpg']) {
    const broken=await browser.newPage();
    await broken.route(missing,route=>route.abort());
    await broken.goto(url);
    await broken.waitForFunction(()=>document.querySelector('#menu-system-root')?.textContent.includes('다시 불러오기'),null,{timeout:120000});
    assert.equal(await broken.evaluate(()=>window.__happyToy.assetsReady),false);
    await broken.close();
  }
  console.log('RESILIENCE PASSED: saved settings, corrupt records, floor triggers, focus loss, missing FBX, missing texture');
} finally {await browser.close();}
