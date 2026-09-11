import * as THREE from 'three';
import {createMaskWraithModel,wraithStride} from './MaskWraithModel.js';

// Floating masks curse on first contact; their transformed body is lethal.
export class LanternMask {
  constructor(group,game) {
    this.group=group;this.game=game;this.world=game.collisionWorld;
    this.home=group.position.clone();this.position=group.position.clone();
    this.position.y=this.world.getGroundY(this.position);
    this.world.snapToValidSurface(this.position,{actorId:group.name});
    this.clearSpawn();
    this.state='wander';this.path=[];this.routeTimer=0;this.memory=0;this.cooldown=0;this.age=0;
    this.target=null;this.hold=0;this.travel=0;
    this.transformed=false;this.transformTime=0;this.heading=group.rotation.y;
    this.maskArt=group.children.filter(c=>c.isGroup).map(object=>({object,position:object.position.clone(),roll:object.rotation.z,scale:object.scale.clone()}));
    this.body=createMaskWraithModel();group.add(this.body.root);
    this.hardware=[];
    const shadowCanvas=document.createElement('canvas');shadowCanvas.width=shadowCanvas.height=64;
    const shadowContext=shadowCanvas.getContext('2d'),gradient=shadowContext.createRadialGradient(32,32,3,32,32,31);
    gradient.addColorStop(0,'rgba(0,0,0,.55)');gradient.addColorStop(1,'rgba(0,0,0,0)');
    shadowContext.fillStyle=gradient;shadowContext.fillRect(0,0,64,64);
    this.shadow=new THREE.Mesh(new THREE.PlaneGeometry(.95,.75),new THREE.MeshBasicMaterial({map:new THREE.CanvasTexture(shadowCanvas),transparent:true,depthWrite:false}));
    this.shadow.rotation.x=-Math.PI/2;this.shadow.visible=false;game.scene.add(this.shadow);
    group.userData.isLanternMask=true;
    const metal=new THREE.MeshStandardMaterial({color:0x383b24,metalness:.75,roughness:.45});
    const glow=new THREE.MeshStandardMaterial({color:0x176b2b,emissive:0x28ff45,emissiveIntensity:1.1,roughness:.3});
    const base=new THREE.Mesh(new THREE.CylinderGeometry(.18,.22,.08,12),metal);base.position.y=-.65;group.add(base);
    const crown=new THREE.Mesh(new THREE.ConeGeometry(.21,.14,12),metal);crown.position.y=-.25;group.add(crown);
    this.hardware.push(base,crown);
    for(let i=0;i<6;i++) {
      const bar=new THREE.Mesh(new THREE.CylinderGeometry(.012,.012,.34,5),metal);
      bar.position.set(Math.cos(i*Math.PI/3)*.16,-.46,Math.sin(i*Math.PI/3)*.16);group.add(bar);
      this.hardware.push(bar);
    }
    this.flame=new THREE.Mesh(new THREE.SphereGeometry(.075,10,8),glow);this.flame.position.y=-.45;this.flame.scale.y=1.7;group.add(this.flame);
    this.light=new THREE.PointLight(0x66ff66,1.5,3.6,1.8);this.light.position.y=-.4;group.add(this.light);
    this.wisps=[];
    const fireMat=new THREE.MeshBasicMaterial({color:0x55ee91,transparent:true,opacity:.22,depthWrite:false});
    for(let i=0;i<5;i++) {
      const wisp=new THREE.Mesh(new THREE.SphereGeometry(.07,7,5),fireMat);
      group.add(wisp);this.wisps.push(wisp);
    }
  }

  reset() {
    this.position.copy(this.home);this.position.y=this.world.getGroundY(this.position);
    this.world.snapToValidSurface(this.position,{actorId:this.group.name});
    this.clearSpawn();
    this.state='wander';this.target=null;this.path=[];this.routeTimer=0;this.cooldown=0;this.memory=0;this.noiseId=null;
    this.transformed=false;this.transformTime=0;this.body.animate(0,0);this.group.rotation.x=0;this.group.rotation.z=0;
  }

  clearSpawn() {
    // Wall-mounted art can sit in a sealed decorative recess. Start the actor
    // on a real corridor waypoint instead of the nearest empty-looking pocket.
    const waypoints=[];
    for(const chunk of this.game.mapBuilder.loadedChunks.values())for(const p of chunk.waypoints||[]) {
      const point=new THREE.Vector3(...p);
      if(Math.abs(point.y-this.position.y)<.3&&point.distanceTo(this.position)<12
          &&this.world.getSurfaceAt(point).walkable&&!this.world.isCircleBlocked(point,.32,{includeDoors:false}))waypoints.push(point);
    }
    waypoints.sort((a,b)=>a.distanceToSquared(this.position)-b.distanceToSquared(this.position));
    if(waypoints.length){this.position.copy(waypoints[0]);return;}
    if(!this.world.isCircleBlocked(this.position,.32))return;
    const origin=this.position.clone();
    for(let r=.4;r<=3;r+=.25)for(let i=0;i<16;i++) {
      const p=origin.clone().add(new THREE.Vector3(Math.cos(i*Math.PI/8)*r,0,Math.sin(i*Math.PI/8)*r));
      if(this.world.getSurfaceAt(p).walkable&&!this.world.isCircleBlocked(p,.32)) {this.position.copy(p);return;}
    }
  }

  notifyNoise(position,radius,options={}) {
    if(this.state==='chase'||this.state==='transforming'||this.state==='retreat'||Math.abs(position.y-this.position.y)>1
        ||this.position.distanceTo(position)>radius||this.noiseId===options.noiseId)return false;
    this.noiseId=options.noiseId;this.state='investigateNoise';this.target=position.clone().setY(this.position.y);
    this.hold=10;this.travel=35;this.path=[];this.routeTimer=0;return true;
  }

  chooseWander(away=false) {
    const candidates=[];
    for(const chunk of this.game.mapBuilder.loadedChunks.values())for(const p of chunk.waypoints||[]) {
      const point=Array.isArray(p)?new THREE.Vector3(...p):p.clone?p.clone():new THREE.Vector3(p.x,p.y,p.z);
      const d=point.distanceTo(this.position);
      if(Math.abs(point.y-this.position.y)<.3&&d>3&&d<11
          &&(!away||point.distanceTo(this.game.player.position)>8))candidates.push(point);
    }
    for(let i=candidates.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[candidates[i],candidates[j]]=[candidates[j],candidates[i]];}
    for(const target of candidates.slice(0,2)) {
      const path=this.world.findPath(this.position,target,.3,{cellSize:.65,allowInterFloor:false,maxIterations:1200});
      if(path.length>1){this.target=target;this.path=path.slice(1);this.routeTimer=3;return;}
    }
    this.target=null;this.routeTimer=2;
  }

  update(dt,allowPlan=true) {
    this.isMoving=false;
    const player=this.game.player;this.age+=dt;this.cooldown=Math.max(0,this.cooldown-dt);this.routeTimer-=dt;
    if(this.state==='transforming') {
      this.transformTime=Math.min(5,this.transformTime+dt+1e-12);
      this.heading=Math.atan2(player.position.x-this.position.x,player.position.z-this.position.z);
      if(this.transformTime>=5) {
        this.transformed=true;this.state='chase';this.memory=8;
        this.target=player.position.clone();this.path=[];this.routeTimer=0;
      }
      this.updateVisual();return;
    }
    const sameFloor=Math.abs(player.position.y-this.position.y)<.8;
    const distance=this.position.distanceTo(player.position);
    const eye=this.position.clone().add(new THREE.Vector3(0,1,0));
    const playerEye=player.position.clone().add(new THREE.Vector3(0,1,0));
    const sees=sameFloor&&!player.isHidden&&distance<12&&this.world.hasLineOfSight(eye,playerEye);
    if(sees&&this.state!=='retreat') {
      if(this.state!=='chase'){this.path=[];this.routeTimer=0;}
      this.state='chase';this.memory=this.transformed?8:3;this.target=player.position.clone();
    } else if(this.state==='chase') {
      this.memory-=dt;if(this.memory<=0){this.state='wander';this.target=null;this.path=[];this.routeTimer=0;}
    }
    if(this.transformed&&this.state==='chase'&&distance<.75&&sees&&!this.game.isInvincible) {
      this.game.handleCaught('가면 뒤에서 자라난 것이 당신을 붙잡았습니다.',this.group.position.clone());
      return;
    }
    if(!this.transformed&&this.state==='chase'&&distance<.85&&sees&&this.cooldown<=0&&!this.game.isInvincible) {
      player.slowTimer=10;this.cooldown=14;
      if(!this.transformed){this.state='transforming';this.transformTime=0;}
      this.target=null;this.path=[];this.routeTimer=0;
      this.game.hud.setStatus(this.transformed?'가면의 저주 · 10초간 이동속도 50%.':'가면 아래로 몸이 자라납니다. 어서 피하십시오.',3500);
      this.updateVisual();return;
    }
    if(this.state==='retreat'){this.retreatTimer-=dt;if(this.retreatTimer<=0){this.state='wander';this.target=null;this.path=[];}}
    let holding=false;
    if(this.state==='investigateNoise') {
      holding=this.target&&this.position.distanceTo(this.target)<.8;
      if(holding)this.hold-=dt;else this.travel-=dt;
      if(this.hold<=0||this.travel<=0){this.state='wander';this.target=null;this.path=[];}
    }
    if(!holding) {
      if(allowPlan&&!this.target&&this.routeTimer<=0)this.chooseWander(this.state==='retreat');
      if(allowPlan&&this.target&&this.routeTimer<=0) {
        this.path=this.world.findPath(this.position,this.target,.3,{cellSize:.65,allowInterFloor:false,maxIterations:1800}).slice(1);
        this.routeTimer=this.state==='chase'?.8:3;
      }
      while(this.path.length&&this.position.distanceTo(this.path[0])<.12)this.path.shift();
      const next=this.path[0];
      if(next) {
        const direction=new THREE.Vector3().subVectors(next,this.position);direction.y=0;const length=direction.length();
        if(length>.001) {
          direction.divideScalar(length);
          for(const door of this.game.doors)if(!door.isDuplicate&&!door.isLocked&&!door.isBlocked
              &&door.distanceTo(this.position)<1.6)door.isOpen=true;
          const previous=this.position.clone();this.position.addScaledVector(direction,Math.min(length,(this.state==='chase'?(this.transformed?3.4*wraithStride(this.age):2.7):1.15)*dt));
          this.world.resolveCircle(this.position,.3);this.world.resolveActorPosition(previous,this.position,.3,{actorId:this.group.name});
          this.isMoving=this.position.distanceToSquared(previous)>.000001;
          this.heading=Math.atan2(direction.x,direction.z);
        }
      } else if(this.target&&this.position.distanceTo(this.target)<.8&&this.state!=='chase'&&this.state!=='investigateNoise')this.target=null;
    }
    this.updateVisual();
  }

  updateVisual() {
    const progress=this.transformed?1:this.transformTime/5;
    const running=this.transformed&&this.state==='chase'&&this.isMoving;
    this.body.animate(this.age,progress,running,this.isMoving);
    for(const piece of this.hardware){piece.visible=progress<1;piece.scale.setScalar(Math.max(.001,1-progress));}
    this.shadow.visible=progress>0;this.shadow.position.copy(this.position);this.shadow.position.y+=.022;
    this.shadow.scale.setScalar(Math.max(.001,progress));
    for(const art of this.maskArt){
      art.object.position.copy(art.position).add(this.body.faceOffset);
      art.object.rotation.z=art.roll+progress*(-.28+Math.sin(this.age*31)*.045);
      art.object.scale.copy(art.scale).multiply(new THREE.Vector3(1+progress*.07,1+progress*.16,1));
    }
    this.group.position.copy(this.position);
    this.group.position.y+=1.25+progress*.82+.26*(1-progress)*Math.sin(this.age*2.3)+.012*Math.sin(this.age*47);
    this.group.position.y+=this.body.root.userData.groundOffset||0;
    this.group.rotation.set((running?.12:0)+Math.sin(this.age*31)*.018,this.heading+Math.sin(this.age*43)*.025,Math.sin(this.age*37)*.023);
    this.wisps.forEach((w,i)=>{
      const phase=(this.age*.8+i/5)%1;
      w.position.set(Math.sin(i*2.4+this.age)*.19,-.65+phase*.9,Math.cos(i*2.4+this.age)*.19);
      w.scale.set(1-phase*.6,2.4*(1-phase)+.3,1-phase*.6);
    });
    this.flame.scale.y=1.5+Math.sin(this.age*13)*.2;
    this.light.intensity=1.2+Math.sin(this.age*9)*.18;
  }
}

export class LanternMasks {
  constructor(game){this.game=game;this.actors=[];this.scanTimer=0;}
  update(dt) {
    this.scanTimer-=dt;
    if(this.scanTimer<=0) {
      this.scanTimer=1;this.actors=this.actors.filter(a=>{if(a.group.parent)return true;a.body.dispose();a.shadow.removeFromParent();a.shadow.geometry.dispose();a.shadow.material.map.dispose();a.shadow.material.dispose();return false;});
      for(const chunk of this.game.mapBuilder.loadedChunks.values())for(const mesh of chunk.meshes) {
        if(mesh.userData.propKind==='watching-mask'&&mesh.userData.horrorPropLoaded&&!mesh.userData.isLanternMask)
          this.actors.push(new LanternMask(mesh,this.game));
      }
      this.active=this.actors.filter(a=>Math.abs(a.position.y-this.game.player.position.y)<.8)
        .sort((a,b)=>a.position.distanceToSquared(this.game.player.position)-b.position.distanceToSquared(this.game.player.position)).slice(0,5);
    }
    let planningBudget=1;
    for(const actor of this.actors) {
      const active=actor.state==='transforming'||(this.active?.includes(actor)&&actor.position.distanceTo(this.game.player.position)<36);
      if(active)actor.update(dt,actor.routeTimer>dt||planningBudget-->0);else actor.light.intensity=.25;
    }
  }
  notifyNoise(position,radius,options){let count=0;for(const actor of this.active||[])if(actor.notifyNoise(position,radius,options))count++;return count;}
  reset(){for(const actor of this.actors)actor.reset();this.scanTimer=0;}
}
