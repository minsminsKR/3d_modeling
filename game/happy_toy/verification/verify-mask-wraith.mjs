import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {mkdirSync} from 'node:fs';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const out='happy_toy/verification/mask-wraith-qa';mkdirSync(out,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
 const page=await browser.newPage({viewport:{width:1280,height:720}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
 console.log(await page.evaluate(async()=>{
  const g=window.__happyToy;g.start();g.loop.stop();g.voiceAnnouncer.announce=()=>{};g.ghostMode=false;g.testSafeMode=false;
  await Promise.all([...g.mapBuilder.pendingAssets]);g.lanternMasks.update(1.1);g.enemyManager.enemies.forEach(e=>e.group.visible=false);g.lovelyDolls.forEach(d=>d.group.visible=false);
  const a=g.lanternMasks.actors[0];await a.body.ready;window.qaMask=a;
  a.position.set(6,0,0);g.player.setPosition(new window.THREE.Vector3(6.6,0,0));a.cooldown=0;a.update(.01);
  return {state:a.state,scale:a.group.scale.toArray(),slow:g.player.slowTimer};
 }));
 assert.equal(await page.evaluate(()=>qaMask.state),'transforming');
 await page.evaluate(()=>{const g=__happyToy;g.player.setPosition(new THREE.Vector3(10,0,0));g.player.setLookAt(new THREE.Vector3(6,1.3,0));g.player.updateCamera(0);g.doors.forEach(d=>{if(!d.isLocked&&!d.isBlocked){d.isOpen=true;d.update(2);}});g.safeLights.forEach(l=>l.setActivated(true));g.updateBackrooms(.016);});
 for(const [name,steps] of [['start',0],['half',50],['formed',50]]) {
  await page.evaluate(steps=>{for(let i=0;i<steps;i++)qaMask.update(.05);const g=__happyToy;for(let i=0;i<30;i++){g.stablePointLights.lastTime=performance.now()-16.67;g.renderer.render(g.scene,g.camera);}},steps);
  await page.screenshot({path:out+'/'+name+'.png'});
 }
 for(let i=0;i<3;i++){await page.evaluate(i=>{const a=qaMask,g=__happyToy;a.isMoving=true;a.age=.12+i*.15;a.updateVisual();g.renderer.render(g.scene,g.camera);},i);await page.screenshot({path:out+'/run-'+i+'.png'});} const result=await page.evaluate(()=>{
  const g=__happyToy,a=qaMask;const completed={state:a.state,time:a.transformTime,body:a.body.root.visible};let skins=0,pbr=false;a.body.root.traverse(o=>{if(o.isSkinnedMesh){skins++;pbr=!!(o.material.map&&o.material.normalMap&&o.material.roughnessMap&&o.geometry.attributes.uv&&!o.geometry.attributes.color);}});
  const start=a.position.clone();let travelled=0,collisions=0;
  for(let i=0;i<90;i++){const p=a.position.clone();g.doors.forEach(d=>d.update(.05));a.update(.05);travelled+=p.distanceTo(a.position);if(g.collisionWorld.isCircleBlocked(a.position,.27))collisions++;}
  const chase={travelled,collisions,distance:a.position.distanceTo(g.player.position)}; const poses=[];for(let i=0;i<60;i++){a.body.animate(i/60,1,true,true);poses.push([ [a.body.bones.find(b=>b.name==='arm_L').quaternion.x,a.body.bones.find(b=>b.name==='shin_L').quaternion.x] ]);} const swing=Math.max(...poses.map(p=>p[0][0]))-Math.min(...poses.map(p=>p[0][0])); const knee=Math.max(...poses.map(p=>p[0][1]))-Math.min(...poses.map(p=>p[0][1]));
  const killed=g.gameOver;g.gameOver=false;g.player.isHidden=true;a.update(.05);const hiddenSafe=!g.gameOver;g.player.isHidden=false;g.ghostMode=true;a.update(.05);const invincibleSafe=!g.gameOver;g.ghostMode=false;const py=g.player.position.y;g.player.position.y+=5;a.update(.05);const floorSafe=!g.gameOver;g.player.position.y=py; a.reset();let lo=Infinity,hi=-Infinity;for(let i=0;i<120;i++){a.age=i/60;a.updateVisual();lo=Math.min(lo,a.group.position.y);hi=Math.max(hi,a.group.position.y);}
  const tapes=[];for(const c of g.mapBuilder.loadedChunks.values())for(const m of c.meshes)if(/hall_cubby_.*_tape/.test(m.name))tapes.push(m.name);
  return {completed,skins,pbr,chase,swing,knee,killed,hiddenSafe,invincibleSafe,floorSafe,bob:hi-lo,reset:!a.transformed&&!a.body.root.visible,tapes};
 });console.log(result);assert.equal(result.skins,1);assert.ok(result.pbr);assert.equal(result.completed.state,'chase');assert.equal(result.completed.time,5);assert.ok(result.completed.body);assert.ok(result.chase.travelled>2);assert.equal(result.chase.collisions,0);assert.ok(result.bob>.45);assert.ok(result.swing>.25);assert.ok(result.knee>.25);assert.ok(result.reset);assert.ok(result.killed&&result.hiddenSafe&&result.invincibleSafe&&result.floorSafe);assert.deepEqual(result.tapes,[]);assert.deepEqual(errors,[]);
} finally {await browser.close();}
