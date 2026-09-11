import {createRequire} from 'node:module';
import {mkdirSync} from 'node:fs';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const out=new URL('./quality-qa/',import.meta.url);mkdirSync(out,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const page=await browser.newPage({viewport:{width:1440,height:900}});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
try {
await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
await page.evaluate(()=>{const g=window.__happyToy;g.start();g.loop.stop();g.testSafeMode=true;g.monsterIntroManager.events.forEach(e=>{e.state='done';});});
for(const [name,x,y,z,yaw,pitch] of [['main',0,0,2,0,-.12],['east',16,0,0,Math.PI/2,-.15],['west',-14,0,0,Math.PI/2,-.1],['class',20,0,5,Math.PI/2,-.22],['upper',-23.5,5,-18.4,.42,-.5],['gallery',-30,5,-25,Math.PI,-.3]]) {
const stats=await page.evaluate(({x,y,z,yaw,pitch})=>{const g=window.__happyToy,p=new window.THREE.Vector3(x,y,z);g.mapBuilder.updateLoadedChunks(p,true);for(let i=0;i<100&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(p,false);g.player.setPosition(p);for(let i=0;i<60;i++)g.updateFloorAtmosphere(.05);g.poseForCapture({yaw,pitch,flashlight:false,freezeLoop:true});g.updateBackrooms?.(.016);g.renderer.render(g.scene,g.camera);return {pos:g.player.position.toArray(),lights:g.stablePointLights.pool.filter(l=>l.intensity>0).map(l=>({p:l.position.toArray(),i:l.intensity})),surface:g.collisionWorld.getSurfaceAt(p)};},{x,y,z,yaw,pitch});
await page.screenshot({path:new URL(`${process.argv[2]||'before'}-${name}.png`,out).pathname.replace(/^\/([A-Z]:)/,'$1')});console.log(name,JSON.stringify(stats));
}
console.log('errors',JSON.stringify(errors));if(errors.length)throw new Error(errors.join('\n'));
}finally{await browser.close();}
