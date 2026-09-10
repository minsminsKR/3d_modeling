import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const page=await browser.newPage({viewport:{width:1280,height:720}}),errors=[];
page.on('pageerror',e=>errors.push(e.message));
try {
  await page.goto(process.argv[2]||'http://127.0.0.1:8014/');
  await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
  const gameplay=await page.evaluate(()=>{
    const g=window.__happyToy;g.start();g.loop.stop();
    const intro=g.monsterIntroManager.events.find(e=>e.constructor.name==='UncatIntroEvent');
    intro.update(.016);g.playTime=120;g.tryReleaseCorridorStalker();
    const enemy=intro.getEnemy();const before=enemy.isDormant&&!g.stalkerReleased;
    g.player.setPosition(intro.triggerPosition);intro.update(.016);
    const during=intro.state==='cutscene'&&enemy.state==='cutscene';
    for(let i=0;i<75;i++)intro.update(.05);
    const after=intro.state==='done'&&enemy.state==='chase'&&g.stalkerReleased;
    const cabinet=g.cabinets.find(c=>Math.abs(c.position.y)<.1);
    g.player.setPosition(cabinet.getExitPosition());
    enemy.group.position.copy(cabinet.getGuardPosition());enemy.hasVisualContact=false;enemy.state='chase';
    g.enterCabinet(cabinet,{forceOutcome:'safe'});
    let searches=0; const begin=enemy.beginCabinetInvestigation.bind(enemy);
    enemy.beginCabinetInvestigation=(...args)=>{searches++;return begin(...args);};
    const positions=[];
    for(let i=0;i<600;i++) {
      g.updateCabinetEvent(.05);
      for(const door of g.doors)door.update(.05);
      enemy.update(.05,{position:g.player.position,isHidden:true});
      g.maybePullLockerHunt();
      if(i%100===0)positions.push(enemy.group.position.distanceTo(cabinet.position));
    }
    const hidden={hidden:g.player.isHidden,event:g.cabinetEvent!==null,state:enemy.state,searches,
      checked:cabinet.searchedThisHide,distance:enemy.group.position.distanceTo(cabinet.position),positions};
    g.exitCabinet();
    enemy.state='chase';enemy.group.position.set(0,0,0);
    g.enterCabinet(cabinet,{forceOutcome:'safe'});
    enemy.group.position.set(50,0,0);
    for(let i=0;i<220;i++)g.updateCabinetEvent(.05);
    const timeout=g.cabinetEvent===null;
    g.exitCabinet();g.ghostMode=true;g.testSafeMode=true;
    return {before,during,after,hidden,timeout};
  });
  console.log('GAMEPLAY',JSON.stringify(gameplay));
  assert.ok(gameplay.before&&gameplay.during&&gameplay.after);
  assert.equal(gameplay.hidden.event,false);assert.equal(gameplay.hidden.searches,0);
  assert.ok(Math.max(...gameplay.hidden.positions)>5,'hunter must actually leave the searched cabinet');
  assert.ok(gameplay.timeout);
  const captures=[];
  for(const [name,x,y,z,yaw] of [['hall',16,0,0,Math.PI/2],['annex',80,0,0,0],['basement',10.5,-5,31.6,0],['upper',-23.5,5,-18.4,.42]]) {
    for(const enabled of [true,false]) {
      const stats=await page.evaluate(({x,y,z,yaw,enabled})=>{
        const g=window.__happyToy,p=new window.THREE.Vector3(x,y,z);
        g.mapBuilder.updateLoadedChunks(p,true);
        for(let i=0;i<100&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(p,false);
        g.player.setPosition(p);
        for(let i=0;i<60;i++)g.updateFloorAtmosphere(.05);
        g.poseForCapture({yaw,pitch:-.1,flashlight:enabled,freezeLoop:true});
        g.renderer.render(g.scene,g.camera);
        const c=document.createElement('canvas');c.width=160;c.height=90;const ctx=c.getContext('2d');
        ctx.drawImage(g.renderer.domElement,0,0,160,90);const data=ctx.getImageData(0,0,160,90).data;
        let sum=0,readable=0;for(let i=0;i<data.length;i+=4){const l=(data[i]+data[i+1]+data[i+2])/3;sum+=l;if(l>12)readable++;}
        return {mean:sum/(160*90),readable:readable/(160*90)};
      },{x,y,z,yaw,enabled});
      captures.push({name,enabled,...stats});
      await page.screenshot({path:fileURLToPath(new URL(`./release-qa/${name}-${enabled?'on':'off'}.png`,import.meta.url))});
    }
  }
  console.log('VISIBILITY',JSON.stringify(captures));
  await page.evaluate(()=>{
    const g=window.__happyToy;let station=null;
    g.scene.traverse(o=>{if(!station&&o.name.endsWith('_fire_station'))station=o;});
    if(!station)throw new Error('Missing modeled safety station');
    const direction=new window.THREE.Vector3(0,0,1).applyQuaternion(station.quaternion);
    g.camera.position.copy(station.position).addScaledVector(direction,1.6);
    g.camera.lookAt(station.position);g.renderer.render(g.scene,g.camera);
  });
  await page.screenshot({path:fileURLToPath(new URL('./release-qa/safety-station.png',import.meta.url))});
  assert.ok(captures.filter(c=>!c.enabled).every(c=>c.readable>.4),'flashlight-off environment must retain visible geometry');
  assert.deepEqual(errors,[]);
  console.log('GAMEPLAY POLISH PASSED');
} finally {await browser.close();}
