import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const page=await browser.newPage({viewport:{width:1440,height:900}}),errors=[];
page.on('pageerror',e=>errors.push(e.message));
page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
try {
  await page.goto(process.argv[2]||'http://127.0.0.1:8014/');
  await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
  await page.evaluate(()=>{const g=window.__happyToy;g.start();g.loop.stop();g.ghostMode=true;g.testSafeMode=true;});
  for(const [name,x,y,z,yaw] of [['gallery',-23.5,5,-18.4,.42],['annex',80,0,0,0]]) {
    await page.evaluate(({x,y,z,yaw})=>{
      const g=window.__happyToy,p=new window.THREE.Vector3(x,y,z);
      g.mapBuilder.updateLoadedChunks(p,true);
      for(let i=0;i<100&&g.mapBuilder.loadQueue.length;i++)g.mapBuilder.updateLoadedChunks(p,false);
      g.player.setPosition(p);g.input.consumePointerDelta();g.poseForCapture({yaw,pitch:-.24,flashlight:true,freezeLoop:true});
    },{x,y,z,yaw});
    await page.screenshot({path:fileURLToPath(new URL(`./release-qa/${name}.png`,import.meta.url))});
  }
  assert.deepEqual(errors,[]);console.log('FINAL SCENES PASSED: gallery, annex, no browser errors');
} finally {await browser.close();}
