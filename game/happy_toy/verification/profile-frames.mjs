import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try {
 const page=await browser.newPage({viewport:{width:1920,height:1080}});
 await page.goto('http://127.0.0.1:8014/');
 await page.waitForFunction(()=>window.__happyToy?.assetsReady,null,{timeout:120000});
 console.log(await page.evaluate(async()=>{
  const g=window.__happyToy;g.start();g.ghostMode=true;g.voiceAnnouncer.announce=()=>{};
  const sums={};for(const [obj,key,label] of [[g,'update','total'],[g.renderer,'render','render'],[g,'updateBackrooms','world'],[g.enemyManager,'update','ai'],[g.stablePointLights,'update','lights']]){
   const original=obj[key];obj[key]=function(...args){const t=performance.now();const r=original.apply(this,args);const a=sums[label]||=[0,0,0];const dt=performance.now()-t;a[0]+=dt;a[1]++;a[2]=Math.max(a[2],dt);return r;};
  }
  await new Promise(r=>setTimeout(r,12000));const intervals=[];let prev=performance.now();await new Promise(resolve=>{function frame(t){intervals.push(t-prev);prev=t;if(intervals.length<180)requestAnimationFrame(frame);else resolve();}requestAnimationFrame(frame);});
  const gl=g.renderer.getContext();const ext=gl.getExtension('WEBGL_debug_renderer_info');
  return {gpu:ext&&gl.getParameter(ext.UNMASKED_RENDERER_WEBGL),fps:1000/(intervals.reduce((a,b)=>a+b)/intervals.length),timings:sums,render:g.renderer.info.render,objects:(()=>{let n=0;g.scene.traverse(()=>n++);return n;})()};
 }));
} finally {await browser.close();}
