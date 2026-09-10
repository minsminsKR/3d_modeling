import assert from 'node:assert/strict';
import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
const out=new URL('./release-qa/',import.meta.url);await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const errors=[], warnings=[], failed=[];
const baseURL=process.argv[2]||'http://127.0.0.1:8010/';
const page=await browser.newPage({viewport:{width:1440,height:900}});
page.on('pageerror', e=>errors.push(e.message));
page.on('console',m=>{if(m.type()==='error')errors.push(m.text());if(m.type()==='warning')warnings.push(m.text());});
page.on('response',r=>{if(r.status()>=400)failed.push(`${r.status()} ${r.url()}`);});
const shot=async name=>page.screenshot({path:fileURLToPath(new URL(name+'.png',out))});
try {
  await page.goto(baseURL,{waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
  await shot('title');
  await page.click('#btn-settings');
  await page.selectOption('#render-quality','high');
  await page.locator('#mouse-sens').fill('1.25');
  await page.locator('#mouse-sens').dispatchEvent('input');
  assert.equal(await page.evaluate(()=>window.__happyToy.renderQuality),'high');
  await page.click('#btn-back-settings');
  await page.click('#btn-start-game');
  await page.evaluate(()=>{const g=window.__happyToy;g.loop.stop();g.ghostMode=true;for(let i=0;i<75;i++)g.update(.016);});
  await shot('start');
  const journal=await page.evaluate(()=>{const g=window.__happyToy;g.journal.record({id:'qa',label:'찢어진 출석부',body:'네 이름을 돌려놓기 전에는 하교할 수 없다.'});g.journal.open();return {open:g.journal.opened,paused:g.isPaused,visible:!g.journal.element.hidden};});
  assert.deepEqual(journal,{open:true,paused:true,visible:true});
  await shot('journal');
  await page.click('.journal-close');
  assert.equal(await page.evaluate(()=>window.__happyToy.isPaused),false);

  const models=await page.evaluate(async()=>{
    const T=window.THREE,g=window.__happyToy;
    const {ENEMY_CONFIGS,HWACAT_EVENT_CONFIG,HWACAT_ANGRY_ENEMY_CONFIG,LOVELY_DOLL_CONFIG}=await import('/src/config/gameConfig.js');
    const configs=[...ENEMY_CONFIGS,HWACAT_EVENT_CONFIG,HWACAT_ANGRY_ENEMY_CONFIG,LOVELY_DOLL_CONFIG];
    const scene=new T.Scene();scene.background=new T.Color(0x182025);
    scene.add(new T.HemisphereLight(0xe9f3ff,0x3e3330,2.4));
    const key=new T.DirectionalLight(0xffe3c0,3.2);key.position.set(0,8,8);scene.add(key);
    const camera=new T.PerspectiveCamera(42,1440/900,.1,70);camera.position.set(0,4.8,16);camera.lookAt(0,2,0);
    const report=[];
    for(let i=0;i<configs.length;i++) {
      const config=configs[i],asset=await g.enemyManager.loader.load(config);
      let meshes=0,skinned=0,textured=0,badWeights=0;
      asset.root.traverse(m=>{if(m.isMesh){meshes++;if(m.isSkinnedMesh){skinned++;const w=m.geometry.attributes.skinWeight;for(let n=0;n<w.count;n++) {const sum=w.getX(n)+w.getY(n)+w.getZ(n)+w.getW(n);if(!Number.isFinite(sum)||Math.abs(sum-1)>.002)badWeights++;}}if([m.material].flat().some(mat=>mat?.map?.image?.width>0))textured++;}});
      const group=new T.Group();group.position.set((i%3-1)*4.3, i<3?2.7:-.6,0);group.add(asset.root);scene.add(group);
      const mixer=new T.AnimationMixer(asset.root);
      const clips=Object.entries(asset.actions).filter(([,clip])=>clip);
      const clip=asset.actions.patrol||asset.animations[0];mixer.clipAction(clip).play();mixer.update(.4);group.updateMatrixWorld(true);
      const bone=(()=>{let b;asset.root.traverse(o=>{if(!b&&o.isBone&&/arm|leg/i.test(o.name))b=o;});return b;})();
      const before=bone?.quaternion.toArray();mixer.update(.3);group.updateMatrixWorld(true);
      const after=bone?.quaternion.toArray();
      const animated=before?.some((v,n)=>Math.abs(v-after[n])>1e-6);
      report.push({id:config.id,meshes,skinned,textured,badWeights,animated,actions:clips.map(([name])=>name),verified:asset.root.userData.assetVerified});
    }
    g.renderer.render(scene,camera);
    document.querySelector('#hud').style.visibility='hidden';
    const labels=document.createElement('div');labels.id='qa-labels';labels.style='position:fixed;inset:0;pointer-events:none;color:#e6daca;font:14px sans-serif';
    ['Uncat','Cyclopse','Baby','Hwacat','Hwacat Angry','Lovely Doll'].forEach((name,i)=>{const l=document.createElement('span');l.textContent=name;l.style=`position:absolute;left:${[23,48,74][i%3]}%;top:${i<3?46:83}%`;labels.append(l);});document.body.append(labels);
    window.__qaStage={scene,camera};
    return report;
  });
  await shot('all-monsters');
  for(const m of models){assert.ok(m.verified&&m.skinned>0&&m.textured===m.meshes,`${m.id}: model / texture`);assert.equal(m.badWeights,0,`${m.id}: weights`);assert.ok(m.actions.length>=2,`${m.id}: actions`);}

  const props=await page.evaluate(async()=>{
    const g=window.__happyToy,T=window.THREE;const rows=[];
    for(const name of ['silent-mannequin-1f','silent-mannequin-2f']) {
      const source=await g.mapBuilder.generator.loadPropAsset(`/assets/props/${name}/model.glb`);
      let vertices=0,textured=0;source.traverse(o=>{if(o.isMesh){vertices+=o.geometry.attributes.position.count;if([o.material].flat().some(m=>m?.map))textured++;}});
      rows.push({name,vertices,textured});
    }
    document.querySelector('#qa-labels').remove();document.querySelector('#hud').style.visibility='';
    return rows;
  });
  props.forEach(p=>assert.ok(p.vertices>100&&p.textured>0,p.name));
  await page.evaluate(async()=>{
    const T=window.THREE,g=window.__happyToy;
    const scene=new T.Scene();scene.background=new T.Color(0x182025);
    scene.add(new T.HemisphereLight(0xe9f3ff,0x3e3330,2.4));
    const light=new T.DirectionalLight(0xffe3c0,3);light.position.set(2,6,4);scene.add(light);
    for(const [i,name] of ['silent-mannequin-1f','silent-mannequin-2f'].entries()) {
      const source=await g.mapBuilder.generator.loadPropAsset(`/assets/props/${name}/model.glb`);
      const model=source.clone(true);const box=new T.Box3().setFromObject(model);model.scale.multiplyScalar(2/(box.max.y-box.min.y));model.updateMatrixWorld(true);box.setFromObject(model);const center=box.getCenter(new T.Vector3());model.position.set((i?1.1:-1.1)-center.x,-box.min.y,-center.z);scene.add(model);
    }
    const camera=new T.PerspectiveCamera(40,1440/900,.1,30);camera.position.set(0,1.4,6);camera.lookAt(0,1,0);
    document.querySelector('#hud').style.visibility='hidden';g.renderer.render(scene,camera);
  });
  await shot('mannequins');
  for(const [name,position,yaw] of [['main-hall',[16,0,0],Math.PI/2],['basement',[10.5,-5,31.6],.28],['gallery',[-23.5,5,-18.4],.42],['annex',[80,0,0],0]]) {
    await page.evaluate(({position,yaw})=>{
      const g=window.__happyToy;document.querySelector('#hud').style.visibility='';
      g.ghostMode=true;g.testSafeMode=true;
      const target=new window.THREE.Vector3(...position);
      g.mapBuilder.updateLoadedChunks(target,true);
      for(let i=0;i<100&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(target,false);
      g.player.setPosition(target);g.input.consumePointerDelta();
      for(let i=0;i<4;i++)g.update(.016,{skipRender:true});
      g.poseForCapture({yaw,pitch:-.24,flashlight:true,freezeLoop:true});
    },{position,yaw});
    await shot(name);
  }

  const state=await page.evaluate(async()=>{
    const g=window.__happyToy;g.restart();g.loop.stop();g.ghostMode=true;
    const event=g.mirrorEvents[0];
    await event.spawnHwacat();await event.transformToEnemy();
    const transformed=g.enemyManager.enemies.find(e=>e.config.id==='hwacat-angry');
    const transform={model:Boolean(transformed?.modelRoot.userData.assetVerified),key:g.keys.find(k=>k.id==='key-hwacat')?.isAvailable};
    g.restart();g.loop.stop();
    const race=g.mirrorEvents[0].beginHwacatSequence();g.mirrorEvents[0].reset();await race;
    const cancelled=!g.mirrorEvents[0].group&&g.mirrorEvents[0].state==='idle';
    g.ghostMode=true;
    for(const k of [...g.keys]) {if(!k.isAvailable)g.revealKeyById(k.id);g.collectKey(k);}
    const collected=g.keyCount;
    const final=g.finalExit;g.player.setPosition(final.position.clone());
    g.tryClearFinal();g.input.keys.add('e');
    for(let i=0;i<145;i++)g.dreadDirector.update(.05);
    const clear=g.gameCleared;
    g.quitToTitle();
    const reset={started:g.isStarted,count:g.keyCount,over:g.gameOver,cleared:g.gameCleared,journal:g.journal.notes.size,dynamic:g.enemyManager.enemies.filter(e=>e.isDynamic).length};
    return {transform,cancelled,collected,clear,reset};
  });
  assert.ok(state.transform.model&&state.transform.key,'Hwacat transform and reward');
  assert.ok(state.cancelled,'late async model must not survive restart');
  assert.equal(state.collected,4);assert.ok(state.clear,'four souls must unlock ritual escape');
  assert.deepEqual(state.reset,{started:false,count:0,over:false,cleared:false,journal:0,dynamic:0});
  assert.deepEqual(errors,[]);assert.deepEqual(failed,[]);
  const report={models,props,state,errors,failed,warnings:[...new Set(warnings)]};
  await fs.writeFile(new URL('release-report.json',out),JSON.stringify(report,null,2));
  console.log(JSON.stringify(report,null,2));console.log('RELEASE QA PASSED');
} finally {await browser.close();}
