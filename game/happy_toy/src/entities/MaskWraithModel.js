import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {cloneCharacterAsset} from '../loaders/CharacterLoader.js';

let sourcePromise;
export function wraithStride(age) {
  const phase=age%2.4;
  return phase<.2?.18:phase<.75?1.4:.9;
}
function strideClock(age) {
  const phase=age%2.4;
  return Math.floor(age/2.4)*2.291+Math.min(phase,.2)*.18
    +Math.min(Math.max(phase-.2,0),.55)*1.4+Math.max(phase-.75,0)*.9;
}
function loadSource() {
  return sourcePromise ||= new GLTFLoader().loadAsync('/models/mask-wraith/mask-wraith.glb');
}

// Blender-authored continuous sculpt, smooth skin weights and baked run clip.
export function createMaskWraithModel() {
  const root=new THREE.Group();root.name='mask-wraith-body';root.visible=false;
  let model,mixer,action,neck,disposed=false;
  const api={root,loaded:false,bones:[],animations:[],error:null,faceOffset:new THREE.Vector3()};
  api.ready=loadSource().then(asset=>{
    if(disposed)return;
    model=cloneCharacterAsset({root:asset.scene}).root;
    model.traverse(o=>{
      if(o.isMesh){o.castShadow=true;o.receiveShadow=true;o.frustumCulled=false;
        if(o.material.name==='Weathered_ashen_flesh')o.material.color.set(0x667377);
      }
      if(o.isBone)api.bones.push(o);
    });
    root.add(model);api.animations=asset.animations;
    neck=api.bones.find(b=>b.name==='neck');
    mixer=new THREE.AnimationMixer(model);
    const clip=asset.animations.find(a=>/Frenzied_Run/.test(a.name))||asset.animations[0];
    if(clip){action=mixer.clipAction(clip);action.play();mixer.setTime(0);}
    api.loaded=true;
  }).catch(error=>{api.error=error;console.error('Mask wraith model failed to load',error);});
  api.animate=(age,progress,running=false,moving=false)=>{
    root.visible=progress>0;
    root.scale.set(.3+.7*progress,Math.max(.001,progress),.45+.55*progress);
    const animate=running||moving;
    if(mixer&&action){
      if(animate){action.play();mixer.setTime(running?strideClock(age):age*.35);}
      else {action.play();mixer.setTime(0);}
    }
    if(neck&&progress>0&&root.parent){
      root.updateWorldMatrix(true,true);
      api.faceOffset.set(0,Math.hypot(.37,.16),0).applyMatrix4(neck.matrixWorld);
      root.parent.worldToLocal(api.faceOffset);
    }else api.faceOffset.set(0,0,0);
    root.userData.groundOffset=0;
  };
  api.dispose=()=>{
    disposed=true;mixer?.stopAllAction();
    model?.traverse(o=>{if(o.isMesh){const mats=Array.isArray(o.material)?o.material:[o.material];mats.forEach(m=>m.dispose());}if(o.isSkinnedMesh)o.skeleton.dispose();});
    if(model)mixer?.uncacheRoot(model);
  };
  return api;
}
