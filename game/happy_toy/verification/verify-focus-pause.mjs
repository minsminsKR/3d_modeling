import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({headless:false,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',ignoreDefaultArgs:['--disable-background-timer-throttling','--disable-backgrounding-occluded-windows','--disable-renderer-backgrounding']});
try {
 const context=await browser.newContext({viewport:{width:1280,height:720}});
 const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:8014/');
 await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
 const cdp=await context.newCDPSession(page);await cdp.send('Emulation.setFocusEmulationEnabled',{enabled:false});
 await page.evaluate(()=>{const g=window.__happyToy;g.start();g.ghostMode=true;g.voiceAnnouncer.announce=()=>{};g.monsterIntroManager.events.forEach(e=>e.state='done');g.enemyManager.enemies.forEach(e=>e.setDormant(true));});
 await page.locator('canvas').first().click({position:{x:640,y:360}});
 await page.waitForTimeout(500);
 const before=await page.evaluate(()=>({time:window.__happyToy.playTime,locked:!!document.pointerLockElement}));
 const other=await context.newPage();await other.goto('about:blank');await other.bringToFront();
 await other.waitForTimeout(2500);
 const hidden=await page.evaluate(()=>({hidden:document.hidden,paused:window.__happyToy.isPaused,time:window.__happyToy.playTime}));
 console.log({before,hidden});assert.equal(hidden.paused,false);assert.ok(hidden.time-before.time>1.8);
 // CDP tab activation may retain visible state under desktop automation.
 // Exercise the real visibility listener/worker too in that environment.
 if (!hidden.hidden) {
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));});
  const t=await page.evaluate(()=>window.__happyToy.playTime);await page.waitForTimeout(1500);
  assert.ok(await page.evaluate(t=>window.__happyToy.playTime-t>1,t));
  await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});
 }
 await page.bringToFront();await page.waitForTimeout(300);
 assert.equal(await page.evaluate(()=>window.__happyToy.isPaused),false);
 await page.locator('canvas').first().click({position:{x:640,y:360}});
 await page.waitForTimeout(300);await page.keyboard.press('Escape');await page.waitForTimeout(400);
 assert.equal(await page.evaluate(()=>window.__happyToy.isPaused),true);
 const pausedTime=await page.evaluate(()=>window.__happyToy.playTime);await page.waitForTimeout(500);
 assert.equal(await page.evaluate(()=>window.__happyToy.playTime),pausedTime);
 await page.keyboard.press('Escape');await page.waitForTimeout(400);
 assert.equal(await page.evaluate(()=>window.__happyToy.isPaused),false);
 assert.deepEqual(errors,[]);console.log('FOCUS / ESC PASSED', {before,hidden});
} finally {await browser.close();}
