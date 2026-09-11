import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const out=new URL('./guardian-layout-qa/',import.meta.url);mkdirSync(out,{recursive:true});
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
 const page=await browser.newPage({viewport:{width:1280,height:720}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
 const result=await page.evaluate(async()=>{
  const g=window.__happyToy;g.start();g.loop.stop();g.voiceAnnouncer.announce=()=>{};
  const {doorClearance,overlaps}=await import('/src/world/FurniturePlacement.js');
  const cabinets=g.cabinets.map(c=>({id:c.id,p:c.position.toArray(),verified:c.group.userData.placementVerified,doors:g.doors.filter(d=>overlaps(c.getAabb(),doorClearance(d))).map(d=>d.id)}));
  const obstacles=g.collisionWorld.blockers.filter(b=>b.type==='static'&&/desk|chair|shelf|cabinet|locker|bucket|bench|table/.test(b.id)&&!/wall|frame|ceil/.test(b.id)&&typeof b.aabb!=='function'&&g.doors.some(d=>overlaps(b.aabb,doorClearance(d)))).map(b=>({id:b.id,aabb:b.aabb}));
  const doll=g.lovelyDolls[0];doll.activate();const start=doll.group.position.clone();let collisions=0;
  for(let i=0;i<1800;i++){g.doors.forEach(d=>d.update(.05));doll.update(.05);if(g.collisionWorld.isCircleBlocked(doll.group.position,.32))collisions++;}
  const movement={start:start.toArray(),end:doll.group.position.toArray(),target:doll.targetPosition?.toArray(),state:doll.state,path:doll.path?.length,collisions};
  // A failed route must not turn into a straight walk through the intervening wall.
  const findPath=g.collisionWorld.findPath;g.collisionWorld.findPath=()=>[];
  doll.targetPosition.set(-32,0,32);doll.path=null;doll.pathTimer=0;doll.directCheckTimer=0;doll.state='walking';
  const failedStart=doll.group.position.clone();doll.updateMovement(.05);
  const failedRouteMoved=doll.group.position.distanceTo(failedStart);g.collisionWorld.findPath=findPath;
  g.enemyManager.enemies.forEach(e=>e.setDormant(true));
  for(const chunk of g.mapBuilder.loadedChunks.values())for(const mesh of chunk.meshes)if(mesh.userData.weepingAngelState)mesh.userData.weepingAngelState.active=false;
  g.player.setPosition(doll.group.position.clone().add(new window.THREE.Vector3(0,0,.8)));
  const threat=g.getMonsterThreat();
  const enemyState=g.enemyManager.update(.05,{position:g.player.position,isHidden:false,isMoving:false,isSprinting:false,isUndetectable:false});
  g.glitchController.reset();g.dreadDirector.phase='calm';g.updateGlitch(.05,enemyState);
  return {cabinets,obstacles,doll:movement,failedRouteMoved,friendly:{threat,enemyThreat:enemyState.threat,noise:document.body.classList.contains('glitch-active')},errors:[]};
 });console.log(JSON.stringify({...result,cabinets:{count:result.cabinets.length,issues:result.cabinets.filter(c=>!c.verified||c.doors.length)}} ,null,2));assert.deepEqual(errors,[]);assert.equal(result.cabinets.filter(c=>!c.verified||c.doors.length).length,0);assert.equal(result.doll.collisions,0);assert.equal(result.doll.state,'waiting');assert.equal(result.failedRouteMoved,0);assert.deepEqual(result.friendly,{threat:0,enemyThreat:0,noise:false});assert.equal(result.obstacles.length,0);
 for(const id of ['chunk_0_0_hall_door_s_0','door-right-playroom']) {
  const found=await page.evaluate(id=>{
   const g=window.__happyToy,d=g.doors.find(d=>d.id===id)||g.doors.find(d=>d.id.includes('chunk_0_0')&&!d.isLocked);if(!d)return false;
   d.isOpen=true;d.update(2);const p=d.position.clone();p.y=g.collisionWorld.getGroundY(p);if(d.axis==='x')p.z-=2.8;else p.x-=2.8;
   g.player.setPosition(p);g.player.setLookAt(d.position.clone().add(new window.THREE.Vector3(0,1.1,0)));g.player.updateCamera(0);
   g.safeLights.forEach(l=>l.setActivated(true));g.updateBackrooms(.016);g.updateFloorAtmosphere(1,true);g.renderer.render(g.scene,g.camera);return true;
  },id);
  if(found)await page.screenshot({path:fileURLToPath(new URL(`${id}.png`,out))});
 }
 const streamed=await page.evaluate(async()=>{
  const g=window.__happyToy,{doorClearance,overlaps}=await import('/src/world/FurniturePlacement.js');
  const seen=new Set(),issues=[];
  for(const x of [96,128,0]) {
   const p=new window.THREE.Vector3(x,0,0);g.mapBuilder.updateLoadedChunks(p,true);
   for(let i=0;i<150&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(p,false);
   for(const c of g.mapBuilder.cabinets){seen.add(c.id);if(!c.group.userData.placementVerified||g.mapBuilder.doors.some(d=>overlaps(c.getAabb(),doorClearance(d))))issues.push(c.id);}
  }
  return {cabinets:seen.size,issues};
 });console.log('STREAMED LAYOUT',streamed);assert.deepEqual(streamed.issues,[]);
}finally{await browser.close();}
