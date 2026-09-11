import {createRequire} from 'node:module';
import {mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const require=createRequire(import.meta.url);const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const dir=new URL('./lighting-play-qa/',import.meta.url);mkdirSync(dir,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});const page=await browser.newPage({viewport:{width:1280,height:720}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
 await page.evaluate(()=>{
  const g=window.__happyToy;g.start();g.loop.stop();g.voiceAnnouncer.announce=()=>{};
  g.monsterIntroManager.events.forEach(e=>e.state='done');g.enemyManager.enemies.forEach(e=>e.setDormant(true));
  const lamp=g.safeLights.find(l=>l.variant==='wall-switch'&&l.group.userData.wallMounted&&Math.abs(l.position.y-1.2)<1);window.qaLamp=lamp;
  const outward=new window.THREE.Vector3(0,0,-1).applyQuaternion(lamp.group.quaternion);
  const p=lamp.position.clone().addScaledVector(outward,3.5).setY(0);g.player.setPosition(p);
  g.player.setLookAt(lamp.position.clone().setY(1.2));g.player.updateCamera(0);
  g.flashlightController.setEnabled(false,false);g.flashlightController.applyOutput(0);
  g.safeLights.forEach(l=>l.setActivated(false));g.updateFloorAtmosphere(1,true);
 });
 for(const [name,on,near] of [['off-far',false,false],['on-far',true,false],['on-near',true,true],['off-near',false,true]]) {
  const info=await page.evaluate(({on,near})=>{
   const g=window.__happyToy;g.glitchController.reset();g.elapsedTime=10;
   g.enemyManager.enemies.forEach(e=>e.setDormant(true));
   if(near){const e=g.enemyManager.enemies.find(e=>e.config.id==='uncat');e.setDormant(false);e.group.position.copy(g.player.position).add(new window.THREE.Vector3(0,0,3.5));e.state='chase';}
   window.qaLamp.setActivated(on);g.updateBackrooms(.016);
   if(near)for(let i=0;i<40;i++)g.glitchController.update(.05,{threat:g.getMonsterThreat(),hunt:true});
   g.renderer.render(g.scene,g.camera);
   return {filter:getComputedStyle(g.renderer.domElement).filter,flashlight:g.flashlight.intensity,ambient:g.ambientLight.intensity,lamp:g._safeLightPool[0].intensity,threat:g.getMonsterThreat()};
  },{on,near});
  await page.screenshot({path:fileURLToPath(new URL(`${process.argv[2]||'before'}-${name}.png`,dir))});console.log(name,JSON.stringify(info));
  if(info.flashlight!==0)throw new Error('Flashlight must stay off');
  if(!info.filter.startsWith('brightness(1)')&&!near)throw new Error('Unexpected global dimming');
 }
 if(errors.length)throw new Error(errors.join('\n'));
}finally{await browser.close();}
