import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const out=new URL('./lantern-items-qa/',import.meta.url);mkdirSync(out,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
 const page=await browser.newPage({viewport:{width:1440,height:900}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
 const result=await page.evaluate(async()=>{
  const g=window.__happyToy;g.start();g.loop.stop();g.voiceAnnouncer.announce=()=>{};g.enemyManager.enemies.forEach(e=>e.setDormant(true));
  const {samePortal}=await import('/src/world/DoorSeams.js');const doors=[];
  for(const x of [0,96,128,0]) {
   const p=new window.THREE.Vector3(x,0,0);g.mapBuilder.updateLoadedChunks(p,true);
   for(let i=0;i<120&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(p,false);
   await Promise.all([...g.mapBuilder.pendingAssets]);g.doors=g.mapBuilder.doors;
   const visible=g.doors.filter(d=>!d.isDuplicate),duplicates=[];
   for(let i=0;i<visible.length;i++)for(let j=i+1;j<visible.length;j++)if(samePortal(visible[i],visible[j]))duplicates.push([visible[i].id,visible[j].id]);
   doors.push({x,hidden:g.doors.filter(d=>d.isDuplicate).map(d=>({id:d.id,label:d.label,blocking:d.isBlocking()})),duplicates});
   g.lanternMasks.update(1.1);
  }
  const f=g.flashlightController,items=g.itemSystem;f.batteryLevel=.15;items.inventory.battery=2;
  const used=items.useItem('battery',g.player,f);const battery={used,level:f.batteryLevel,count:items.inventory.battery};
  f.batteryLevel=.95;const topped=items.useItem('battery',g.player,f);battery.topped=topped;battery.full=f.batteryLevel;
  const e=g.enemyManager.enemies.find(e=>e.config.id==='uncat');e.setDormant(false);e.group.position.set(0,0,0);e.state='chase';e.hasVisualContact=false;
  const pos=new window.THREE.Vector3(0,0,3);const chaseIgnored=!e.notifyNoise(pos,28,{duration:10,holdOnArrival:true,noiseId:'qa-chase'});
  e.beginWander();e.notifyNoise(pos,28,{duration:10,holdOnArrival:true,noiseId:'qa-hold'});
  e.updateNoiseInvestigation(4);const beforeArrival=e.investigationTimer;e.group.position.copy(pos);e.updateNoiseInvestigation(9.9);
  const holds=e.state==='investigateNoise';e.updateNoiseInvestigation(.11);const releases=e.state==='wander';
  e.group.position.set(0,0,0);e.beginWander();const destination=new window.THREE.Vector3(6,0,0);
  e.notifyNoise(destination,28,{duration:10,holdOnArrival:true,noiseId:'real-arrival'});
  let arrival=-1;
  for(let i=0;i<600;i++) {
    g.doors.forEach(d=>d.update(.05));e.update(.05,{position:new window.THREE.Vector3(-30,0,0),isHidden:true,isUndetectable:true});
    if(e.group.position.distanceTo(destination)<.9){arrival=i*.05;break;}
  }
  const arrivalPosition=e.group.position.clone();
  for(let i=0;i<195;i++)e.update(.05,{position:new window.THREE.Vector3(-30,0,0),isHidden:true,isUndetectable:true});
  const realHold={arrival,held:e.state==='investigateNoise',drift:e.group.position.distanceTo(arrivalPosition)};
  const {FirecrackerProjectile}=await import('/src/world/ItemSystem.js');
  const projectile=new FirecrackerProjectile(g.scene,new window.THREE.Vector3(0,1.45,0),new window.THREE.Vector3(0,0,-1),g.enemyManager,items.effectLights);
  projectile.explode();for(let i=0;i<99;i++)projectile.update(.1);const burning=projectile.alive;projectile.update(.11);const expired=!projectile.alive;
  const masks=g.lanternMasks.actors.map(a=>({name:a.group.name,p:a.position.toArray(),state:a.state}));
  const patrol=[];g.player.isHidden=true;
  for(const a of g.lanternMasks.actors) {
    let travelled=0,collisions=0;a.state='wander';a.target=null;a.routeTimer=0;a.path=[];
    for(let i=0;i<400;i++){const previous=a.position.clone();g.doors.forEach(d=>d.update(.05));a.update(.05);travelled+=a.position.distanceTo(previous);if(g.collisionWorld.isCircleBlocked(a.position,.27))collisions++;}
    patrol.push({name:a.group.name,travelled,collisions});
  }
  g.player.isHidden=false;
  const actor=g.lanternMasks.actors.find(a=>a.group.parent);
  let maskTest=null;
  if(actor) {
   g.player.setPosition(new window.THREE.Vector3(0,0,0));actor.position.set(0,0,-.6);actor.state='wander';actor.cooldown=0;g.ghostMode=false;g.testSafeMode=false;
   actor.update(.05);const slow=g.player.slowTimer;const alive=!g.gameOver;const retreat=actor.state;
   g.player.updateStaminaAndItemHotkeys(9.9);const remains=g.player.slowTimer;g.player.updateStaminaAndItemHotkeys(.11);
   maskTest={slow,alive,retreat,remains,ends:g.player.slowTimer};
   g.player.setPosition(new window.THREE.Vector3(0,0,0));g.player.yaw=0;g.input.keys.add('d');
   g.player.updateMovement(.1);const normalDistance=g.player.position.length();
   g.player.setPosition(new window.THREE.Vector3(0,0,0));g.player.slowTimer=10;g.player.updateMovement(.1);
   maskTest.speedRatio=g.player.position.length()/normalDistance;g.input.keys.clear();g.player.setPosition(new window.THREE.Vector3(0,0,0));
   actor.position.set(3,0,0);actor.state='wander';actor.cooldown=0;actor.update(.05);
   maskTest.chase=actor.state;maskTest.noiseIgnored=!actor.notifyNoise(pos,28,{noiseId:'mask-qa'});
   g.player.resetLook(-Math.PI/2,-.08);g.player.updateCamera(0);g.safeLights=g.mapBuilder.safeLights;g.safeLights.forEach(l=>l.setActivated(false));g.updateBackrooms(.016);g.renderer.render(g.scene,g.camera);
  }
  return {doors,battery,noise:{chaseIgnored,beforeArrival,holds,releases,burning,expired},realHold,masks,maskTest,patrol};
 });console.log(JSON.stringify(result,null,2));await page.screenshot({path:fileURLToPath(new URL('lantern-mask.png',out))});
 assert.deepEqual(errors,[]);assert.ok(result.doors.every(d=>!d.duplicates.length&&d.hidden.every(h=>!h.blocking)));
 assert.deepEqual(result.battery,{used:true,level:1,count:1,topped:true,full:1});assert.deepEqual(result.noise,{chaseIgnored:true,beforeArrival:10,holds:true,releases:true,burning:true,expired:true});
 assert.ok(result.maskTest);assert.equal(result.maskTest.slow,10);assert.equal(result.maskTest.ends,0);assert.ok(result.maskTest.alive&&result.maskTest.remains>0&&result.maskTest.noiseIgnored);assert.equal(result.maskTest.chase,'chase');
 assert.ok(result.patrol.every(a=>a.travelled>2&&a.collisions===0));
 assert.equal(result.maskTest.speedRatio,.5);assert.ok(result.realHold.arrival>=0&&result.realHold.held&&result.realHold.drift<.05);
 console.log('LANTERN / BATTERY / FIRECRACKER / DOOR CHECKS PASSED');
}finally{await browser.close();}
