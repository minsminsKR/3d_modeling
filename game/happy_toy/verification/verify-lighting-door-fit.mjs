import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
  const page=await browser.newPage({viewport:{width:1280,height:720}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(process.argv[2]||'http://127.0.0.1:8014/');
  await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
  await page.evaluate(()=>{
    const g=window.__happyToy;g.start();g.loop.stop();g.ghostMode=true;
    const lamp=g.safeLights.find(l=>l.variant==='wall-switch'&&l.group.userData.wallMounted&&Math.abs(l.position.y-1.2)<1);
    if(!lamp)throw new Error('No mounted wall lamp');
    window.qaLamp=lamp;
    const outward=new window.THREE.Vector3(0,0,-1).applyQuaternion(lamp.group.quaternion);
    g.player.setPosition(lamp.position.clone().addScaledVector(outward,2.5).setY(0));
    g.poseForCapture({flashlight:false,freezeLoop:true});
    g.camera.position.copy(lamp.position).addScaledVector(outward,2.5);g.camera.position.y=1.6;
    g.camera.lookAt(lamp.position.clone().setY(1.2));
    for(const l of g.safeLights)l.setActivated(false);
  });
  const values=[];
  for(const on of [false,true]) {
    values.push(await page.evaluate(on=>{
      const g=window.__happyToy;window.qaLamp.setActivated(on);g.updateSafeLightPool();
      g.renderer.render(g.scene,g.camera);
      const c=document.createElement('canvas');c.width=160;c.height=90;
      const ctx=c.getContext('2d');ctx.drawImage(g.renderer.domElement,0,0,160,90);
      const data=ctx.getImageData(0,0,160,90).data;let sum=0;
      for(let i=0;i<data.length;i+=4)sum+=(data[i]+data[i+1]+data[i+2])/3;
      return sum/(160*90);
    },on));
    await page.screenshot({path:fileURLToPath(new URL(`./release-qa/wall-lamp-${on?'on':'off'}.png`,import.meta.url))});
  }
  assert.ok(values[1]>values[0]*1.15,'switching on must visibly illuminate surrounding wall');
  await page.evaluate(()=>{
    const g=window.__happyToy,d=g.doors.find(d=>d.group.userData.fittedOpeningWidth);
    d.isOpen=false;d.openAmount=0;d.update(0);
    g.camera.position.copy(d.position).add(new window.THREE.Vector3(d.axis==='z'?3:0,1.6,d.axis==='x'?3:0));
    g.camera.lookAt(d.position.clone().add(new window.THREE.Vector3(0,1.1,0)));
    g.renderer.render(g.scene,g.camera);
  });
  await page.screenshot({path:fileURLToPath(new URL('./release-qa/fitted-classroom-door.png',import.meta.url))});
  assert.deepEqual(errors,[]);console.log('LIGHTING / DOOR FIT PASSED',JSON.stringify({off:values[0],on:values[1],errors}));
} finally {await browser.close();}
