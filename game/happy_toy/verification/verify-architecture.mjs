import {chromium} from 'playwright';
import assert from 'node:assert/strict';
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
  const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(process.argv[2]||'http://127.0.0.1:8014/');
  await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
  const report=await page.evaluate(()=>{
    const g=window.__happyToy;g.start();g.loop.stop();g.ghostMode=true;
    const doors=new Map(),connections=new Map();let stations=0,trim=0,mounted=0;
    const centers=[[16,-5,32],[-24,5,-16]];
    for(let cx=-3;cx<=8;cx++)for(let cz=-3;cz<=3;cz++) {
      if(g.mapBuilder.generator.getChunkType(cx,cz)!=='void')centers.push([cx*16,0,cz*16]);
    }
    for(const [x,y,z] of centers) {
      const p=new window.THREE.Vector3(x,y,z);g.mapBuilder.updateLoadedChunks(p,true);
      for(let i=0;i<100&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(p,false);
      for(const d of g.mapBuilder.doors) {
        if(Math.hypot(d.position.x-x,d.position.z-z)>24)continue;
        if(d.isLocked||d.isBlocked)continue;
        const center=d.position.clone();d.isOpen=false;d.openAmount=0;
        const closed=g.collisionWorld.isCircleBlocked(center,.25);
        const across=new window.THREE.Vector3(d.axis==='x'?1:0,0,d.axis==='z'?1:0);
        const fitted=d.group.userData.fittedOpeningWidth;
        const sealed=!fitted||[-.9,-.6,0,.6,.9].every(offset=>g.collisionWorld.isCircleBlocked(center.clone().addScaledVector(across,offset),.08));
        d.isOpen=true;d.openAmount=1;d.update(0);
        const open=!g.collisionWorld.isCircleBlocked(center,.25);
        const normal=new window.THREE.Vector3(d.axis==='z'?1:0,0,d.axis==='x'?1:0);
        const faces=[-1,1].map(sign=>{
          const point=center.clone().addScaledVector(normal,sign*.8);
          return g.collisionWorld.getSurfaceAt(point).walkable&&!g.collisionWorld.isCircleBlocked(point,.25);
        });
        const obstacles=g.collisionWorld.getActiveBlockers({position:center}).filter(b=>{
          const a=typeof b.aabb==='function'?b.aabb():b.aabb;
          return center.x>=a.minX-.9&&center.x<=a.maxX+.9&&center.z>=a.minZ-.9&&center.z<=a.maxZ+.9;
        }).map(b=>({id:b.id,type:b.type}));
        doors.set(d.id,{id:d.id,closed,open,faces,sealed,position:center.toArray(),obstacles});
        d.isOpen=false;d.openAmount=0;d.update(0);
      }
      const gen=g.mapBuilder.generator;
      for(const chunk of g.mapBuilder.loadedChunks.values()) {
        if(chunk.type==='void')continue;
        const openings=gen.getOpenings(chunk.cx,chunk.cz);
        for(const [side,dx,dz,reverse] of [['N',0,-1,'S'],['S',0,1,'N'],['E',1,0,'W'],['W',-1,0,'E']]) {
          if(openings[side])connections.set(`${chunk.chunkId}:${side}`,gen.getOpenings(chunk.cx+dx,chunk.cz+dz)[reverse]);
        }
      }
      g.scene.traverse(o=>{if(o.name.endsWith('_fire_station'))stations++;if(o.name.endsWith('_architectural_courses'))trim++;if(o.userData.wallMounted)mounted++;});
    }
    return {doors:[...doors.values()],connections:connections.size,asymmetric:[...connections].filter(([,ok])=>!ok),stations,trim,mounted};
  });
  console.log(JSON.stringify({...report,doors:report.doors.length,failures:report.doors.filter(d=>!d.closed||!d.open||!d.faces.every(Boolean)),errors},null,2));
  assert.deepEqual(errors,[]);assert.deepEqual(report.asymmetric,[]);
  assert.ok(report.stations>0&&report.trim>0&&report.mounted>0);
  assert.ok(report.doors.every(d=>d.closed&&d.open&&d.sealed&&d.faces.every(Boolean)),'every unlocked doorway must fit its opening and connect two accessible spaces');
  console.log('ARCHITECTURE PASSED');
} finally {await browser.close();}
