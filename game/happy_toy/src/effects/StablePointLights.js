import * as THREE from 'three';

// Source lights keep their authored transforms and animation. Only this fixed
// set reaches WebGL, so streamed rooms and temporary effects cannot alter the
// shader light layout. Layer 31 is reserved for non-rendered light sources.
export class StablePointLights {
  constructor(scene, count = 12) {
    this.scene = scene;
    this.position = new THREE.Vector3();
    this.candidates = [];
    this.lastTime=performance.now();
    this.slots=Array.from({length:count},()=>({source:null,level:0}));
    this.pool = Array.from({ length: count }, () => {
      const light = new THREE.PointLight(0xffffff, 0);
      light.userData.renderLight = true;
      scene.add(light);
      return light;
    });
  }

  update(camera) {
    this.scene.updateMatrixWorld(true);
    camera.getWorldPosition(this.position);
    // Head bob must not reshuffle equal-distance ceiling lights.
    this.position.y=Math.round((this.position.y-1.6)/5)*5+1.6;
    const now=performance.now(),dt=Math.min(.05,Math.max(.001,(now-this.lastTime)/1000));
    this.lastTime=now;
    const retained=new Set(this.slots.map(s=>s.source));
    const candidates = this.candidates;
    candidates.length = 0;
    this.scene.traverseVisible(source => {
      if (!source.isPointLight || source.userData.renderLight) return;
      source.layers.set(31);
      if (source.intensity <= 0) return;
      const position = source.userData.renderPosition ||= new THREE.Vector3();
      position.setFromMatrixPosition(source.matrixWorld);
      if (Math.abs(position.y - this.position.y) > 3.0) return;
      const distance = position.distanceTo(this.position);
      if (source.distance > 0 && distance > source.distance + 10) return;
      source.userData.renderScore = source.intensity / (1 + distance * distance)*(retained.has(source)?1.4:1);
      candidates.push(source);
    });
    candidates.sort((a,b) => b.userData.renderScore - a.userData.renderScore);
    const wanted=new Set(candidates.slice(0,this.pool.length));
    const assigned=new Set(this.slots.map(s=>s.source));
    for(let i=0;i<this.pool.length;i++) {
      const slot=this.slots[i],target=this.pool[i];
      if(slot.source&&!wanted.has(slot.source)) {
        slot.level=Math.max(0,slot.level-dt/.32);
        if(slot.level===0){assigned.delete(slot.source);slot.source=null;target.intensity=0;}
      }
      if(!slot.source) {
        const next=candidates.find(s=>wanted.has(s)&&!assigned.has(s));
        if(next){slot.source=next;assigned.add(next);slot.level=0;}
      }
      const source=slot.source;
      if(!source){target.intensity=0;continue;}
      if(wanted.has(source))slot.level=Math.min(1,slot.level+dt/.32);
      // Retiring lights stay at their original fixture until fully dark.
      target.position.copy(source.userData.renderPosition);
      target.color.copy(source.color);target.distance=source.distance;target.decay=source.decay;
      target.intensity=source.intensity*slot.level;
    }
  }
}
