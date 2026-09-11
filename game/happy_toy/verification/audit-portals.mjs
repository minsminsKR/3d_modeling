import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try{const page=await browser.newPage();await page.goto('http://127.0.0.1:8014/');await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
console.log(JSON.stringify(await page.evaluate(async()=>{
const g=window.__happyToy;g.loop.stop();const results=[];
for(const x of [0,96]){const p=new window.THREE.Vector3(x,0,0);g.mapBuilder.updateLoadedChunks(p,true);while(g.mapBuilder.loadQueue.length)g.mapBuilder.updateLoadedChunks(p,false);await Promise.all([...g.mapBuilder.pendingAssets]);g.player.setPosition(p);g.lanternMasks.update(1.1);
const ds=g.mapBuilder.doors.filter(d=>!d.isDuplicate),pairs=[];
for(let i=0;i<ds.length;i++)for(let j=i+1;j<ds.length;j++){const a=ds[i],b=ds[j];if(a.axis===b.axis&&Math.abs(a.position.y-b.position.y)<.2&&Math.abs(a.position[a.axis]-b.position[a.axis])<.5&&a.position.distanceTo(b.position)<3)pairs.push([a,b].map(d=>({id:d.id,label:d.label,p:d.position.toArray(),size:d.size})));}
results.push({x,pairs,north:ds.filter(d=>Math.abs(d.position.x)<2&&d.position.z<0&&d.position.z>-20).map(d=>({id:d.id,label:d.label,p:d.position.toArray(),size:d.size})),masks:g.lanternMasks.actors.map(a=>a.group.name),nearNorth:[...g.mapBuilder.loadedChunks.values()].flatMap(c=>c.meshes).filter(m=>Math.abs(m.position.x)<3&&m.position.z< -6&&m.position.z> -9).map(m=>({name:m.name,p:m.position.toArray(),visible:m.visible}))});
}return results;
}),null,2));
await page.evaluate(()=>{const g=window.__happyToy,p=new window.THREE.Vector3(0,0,-5);g.mapBuilder.updateLoadedChunks(p,true);while(g.mapBuilder.loadQueue.length)g.mapBuilder.updateLoadedChunks(p,false);g.start();g.loop.stop();g.doors=g.mapBuilder.doors;g.player.setPosition(p);g.poseForCapture({yaw:0,pitch:0,flashlight:true,freezeLoop:true});const d=g.doors.find(d=>d.id==='chunk_0_0_door_n');d.isOpen=true;d.update(2);g.safeLights=g.mapBuilder.safeLights;g.safeLights.forEach(l=>l.setActivated(true));g.updateBackrooms(.016);g.renderer.render(g.scene,g.camera);});
await page.screenshot({path:'E:/AI/3d_modeling/game/happy_toy/verification/lantern-items-qa/north-open.png'});
}finally{await browser.close();}
