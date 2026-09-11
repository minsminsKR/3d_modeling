import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const page=await browser.newPage({viewport:{width:1280,height:720}}),errors=[];
page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
try {
await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
const checks=await page.evaluate(()=>{
 const g=window.__happyToy;g.start();g.loop.stop();const V=window.THREE.Vector3;
 const event=g.monsterIntroManager.events.find(e=>e.constructor.name==='WeepingAngelIntroEvent');
 const check=(x,y,z,yaw)=>{event.reset();g.player.position.set(x,y,z);g.player.resetLook(yaw,0);g.player.updateCamera(1);event.checkTrigger();return event.hasTriggered;};
 const upstairs=check(-14,5,0,Math.PI/2),room=check(-14,0,3,Math.PI/2),backTurned=check(-14,0,0,-Math.PI/2);
 const corridor=check(-14,0,0,Math.PI/2);
 const fixedCamera=event.startCameraPos.distanceTo(event.glideEndCameraPos)<.001;
 event.reset();
 g.glitchController.trigger({strength:1,full:true});g.glitchController.update(.05,{threat:1});
 const overlayBlend=getComputedStyle(document.querySelector('#glitch-overlay')).mixBlendMode;
 const brightness=Number(document.documentElement.style.getPropertyValue('--glitch-brightness'));g.glitchController.reset();
 const floors=[];let lamps=0,detached=0;
 for(const c of g.mapBuilder.loadedChunks.values()) {
   lamps+=c.lights.length;
   for(const m of c.meshes) {
     if(m.material?.name==='architectural-upper')floors.push(m);
     if(m.visible&&/room_plate|_ofuda$|stopped_clock/.test(m.name)&&!m.userData.wallMounted)detached++;
   }
 }
 const regular=g.mapBuilder.textures.createFloorMaterial(16,16);
 const materialIsolated=regular.color.getHex()===0xb8a487;
 g.safeLights.forEach(l=>l.setActivated(true));
 const samples=[];
 for(const [x,y,z] of [[0,0,2],[16,0,0],[-23.5,5,-18.4]]) {
   g.player.setPosition(new V(x,y,z));g.poseForCapture({flashlight:false,freezeLoop:true,yaw:0,pitch:-.2});g.updateBackrooms(0);g.renderer.render(g.scene,g.camera);
   const c=document.createElement('canvas');c.width=160;c.height=90;const ctx=c.getContext('2d');ctx.drawImage(g.renderer.domElement,0,0,160,90);
   const d=ctx.getImageData(0,0,160,90).data;let lit=0,sum=0;for(let i=0;i<d.length;i+=4){const v=(d[i]+d[i+1]+d[i+2])/3;sum+=v;if(v>18)lit++;}
   samples.push({mean:sum/14400,readable:lit/14400});
 }
 g.player.setPosition(new V(16,0,0));g.poseForCapture({yaw:Math.PI/2,pitch:-.15,flashlight:false});
 const ray=new window.THREE.Raycaster();ray.setFromCamera(new window.THREE.Vector2(0,-.55),g.camera);
 const hits=ray.intersectObjects(g.scene.children,true).filter(h=>h.object.visible).slice(0,4).map(h=>({name:h.object.name,parent:h.object.parent?.name,p:h.point.toArray()}));
 return {upstairs,room,backTurned,corridor,fixedCamera,brightness,overlayBlend,lamps,detached,upperFloors:floors.length,materialIsolated,samples,hits};
});
console.log(JSON.stringify(checks,null,2));
assert.equal(checks.upstairs,false);assert.equal(checks.room,false);assert.equal(checks.backTurned,false);assert.equal(checks.corridor,true);
assert.ok(checks.fixedCamera);assert.equal(checks.overlayBlend,'multiply');assert.ok(checks.brightness>0 && checks.brightness<=1);assert.ok(checks.lamps>25);assert.ok(checks.upperFloors>0);assert.ok(checks.materialIsolated);assert.equal(checks.detached,0);
assert.ok(checks.samples.every(s=>s.readable>.6));assert.deepEqual(errors,[]);console.log('QUALITY REGRESSION PASSED');
}finally{await browser.close();}
