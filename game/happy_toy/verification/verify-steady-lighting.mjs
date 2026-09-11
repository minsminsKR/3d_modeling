import {createRequire} from 'node:module';
import {mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const out=new URL('./steady-lighting-qa/',import.meta.url);mkdirSync(out,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
const page=await browser.newPage({viewport:{width:1280,height:720}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
const lighting=await page.evaluate(()=>{
 const g=window.__happyToy;g.start();g.loop.stop();g.flashlightController.setEnabled(false,false);g.safeLights.forEach(l=>l.setActivated(true));g.updateBackrooms(.016);
 let previous=[],hotMoves=0,maxIntensityStep=0,changes=0;
 for(let frame=0;frame<480;frame++) {
  const x=frame<240?frame*.055:(480-frame)*.055;
  g.player.position.set(x,0,0);g.camera.position.set(x,1.6+Math.sin(frame*.7)*.045,0);
  g.stablePointLights.lastTime=performance.now()-1000/60;g.renderer.render(g.scene,g.camera);
  const current=g.stablePointLights.pool.map((l,i)=>({p:l.position.clone(),i:l.intensity,source:g.stablePointLights.slots[i].source?.uuid}));
  if(previous.length)current.forEach((l,i)=>{const old=previous[i];if(old.source!==l.source){changes++;if(old.i>4&&l.i>4)hotMoves++;}maxIntensityStep=Math.max(maxIntensityStep,Math.abs(l.i-old.i));});
  previous=current;
 }
 return {hotMoves,maxIntensityStep,changes,fixedSources:g.safeLights.filter(l=>l.isOn&&l.renderSource.intensity===60).length};
});console.log(lighting);assert.equal(lighting.hotMoves,0);assert.ok(lighting.changes>0);assert.ok(lighting.fixedSources>0);assert.ok(lighting.maxIntensityStep<10);
await page.screenshot({path:fileURLToPath(new URL('lit-hall.png',out))});
const rooms=await page.evaluate(async()=>{
 const g=window.__happyToy,p=new window.THREE.Vector3(96,0,0);g.mapBuilder.updateLoadedChunks(p,true);while(g.mapBuilder.loadQueue.length)g.mapBuilder.updateLoadedChunks(p,false);await Promise.all([...g.mapBuilder.pendingAssets]);
 return [...g.mapBuilder.loadedChunks.values()].filter(c=>c.type==='classroom').map(c=>({id:c.chunkId,center:c.center,desks:c.meshes.filter(m=>new RegExp('^'+c.chunkId+'_desk_\\d+$').test(m.name)).length,wallRows:g.collisionWorld.blockers.filter(b=>b.chunkId===c.chunkId&&/desk_row/.test(b.id)).length}));
});console.log('CLASSROOMS',rooms);
assert.ok(rooms.length>0);assert.ok(rooms.every(r=>r.desks===20&&r.wallRows===0));
await page.evaluate(center=>{const g=window.__happyToy;g.player.setPosition(new window.THREE.Vector3(center.x,0,center.z-6));g.player.resetLook(Math.PI,-.1);g.player.updateCamera(0);g.safeLights=g.mapBuilder.safeLights;g.safeLights.forEach(l=>l.setActivated(true));g.updateBackrooms(.016);for(let i=0;i<50;i++){g.stablePointLights.lastTime=performance.now()-1000/60;g.renderer.render(g.scene,g.camera);}},rooms[0].center);
await page.screenshot({path:fileURLToPath(new URL('classroom.png',out))});assert.deepEqual(errors,[]);
}finally{await browser.close();}
