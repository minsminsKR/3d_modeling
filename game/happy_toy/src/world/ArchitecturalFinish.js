import * as THREE from 'three';
import { LIGHTING_CONFIG } from '../config/gameConfig.js';

// Shared, meter-scaled finishes. UVs belong to each surface; cached materials
// cannot be recolored by a different floor or stretched by a larger slab.
export class ArchitecturalFinish {
  constructor(builder) {
    this.builder = builder;
    this.materials = new Map();
    this.lampBody = new THREE.BoxGeometry(1.24, .075, .24);
    this.lampTube = new THREE.BoxGeometry(1.12, .026, .15);
    this.lampMetal = new THREE.MeshStandardMaterial({color:0x797c76,roughness:.65,metalness:.3});
  }

  material(kind) {
    if (this.materials.has(kind)) return this.materials.get(kind);
    const canvas = document.createElement('canvas'); canvas.width=512;canvas.height=512;
    const ctx=canvas.getContext('2d');let seed=451;
    const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
    if (kind==='wood') {
      ctx.fillStyle='#584938';ctx.fillRect(0,0,512,512);
      for(let row=0;row<10;row++) {
        const x=row*51.2,v=88+Math.floor(random()*20);
        ctx.fillStyle=`rgb(${v+23},${v+7},${v-12})`;ctx.fillRect(x+1,0,50,512);
        for(let n=0;n<60;n++) {
          ctx.strokeStyle=`rgba(${random()>.5?'220,200,160':'45,32,20'},${.03+random()*.07})`;
          const gx=x+random()*50;ctx.beginPath();ctx.moveTo(gx,0);ctx.bezierCurveTo(gx+3,150,gx-2,370,gx,512);ctx.stroke();
        }
        const joint=(row%3)*170;ctx.fillStyle='rgba(40,31,23,.5)';ctx.fillRect(x,joint,51,1.4);
      }
    } else if(kind==='upper') {
      ctx.fillStyle='#68665d';ctx.fillRect(0,0,512,512);
      for(let x=0;x<4;x++)for(let y=0;y<4;y++) {
        const v=117+Math.floor(random()*9);ctx.fillStyle=`rgb(${v+4},${v+3},${v-6})`;
        ctx.fillRect(x*128+1,y*128+1,126,126);
      }
      for(let i=0;i<18000;i++){ctx.fillStyle=random()>.5?'rgba(230,226,207,.11)':'rgba(39,42,37,.08)';ctx.fillRect(random()*512,random()*512,1.2,1.2);}
    } else {
      ctx.fillStyle=kind==='wall-upper'?'#b1ada1':'#c1bcaa';ctx.fillRect(0,0,512,512);
      ctx.fillStyle=kind==='wall-upper'?'#777b76':'#7d8878';ctx.fillRect(0,325,512,187);
      ctx.fillStyle='#535e53';ctx.fillRect(0,324,512,3);
      for(let i=0;i<24000;i++){ctx.fillStyle=random()>.5?'rgba(242,237,215,.045)':'rgba(46,46,34,.035)';ctx.fillRect(random()*512,random()*512,1+random()*3,1+random()*2);}
      // Localized water damage at the skirting, rather than a repeated giant mold stamp.
      for(let i=0;i<50;i++){const x=random()*512,y=475+random()*37,r=8+random()*25;const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,'rgba(55,53,37,.1)');g.addColorStop(1,'rgba(55,53,37,0)');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
    }
    const map=new THREE.CanvasTexture(canvas);map.colorSpace=THREE.SRGBColorSpace;
    map.wrapS=map.wrapT=THREE.RepeatWrapping;map.anisotropy=8;
    const mat=new THREE.MeshStandardMaterial({map,roughness:kind==='wood'?.72:.88,metalness:0});
    mat.name=`architectural-${kind}`;this.materials.set(kind,mat);return mat;
  }

  instancedWallMaterial() {
    if(this.instancedWall) return this.instancedWall;
    const mat=this.material('wall').clone();
    mat.onBeforeCompile=shader=>{
      shader.vertexShader=shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec2 vArchitecturalUv;')
        .replace('#include <project_vertex>', `#include <project_vertex>
          vec4 architecturalPosition = vec4(transformed, 1.0);
          #ifdef USE_INSTANCING
            architecturalPosition = instanceMatrix * architecturalPosition;
          #endif
          architecturalPosition = modelMatrix * architecturalPosition;
          vArchitecturalUv = vec2(abs(normal.x) > 0.5 ? architecturalPosition.z / 2.0 : architecturalPosition.x / 2.0,
            mod(architecturalPosition.y + 0.001, 5.0) / 2.8);
        `);
      shader.fragmentShader=shader.fragmentShader.replace('#include <common>','#include <common>\nvarying vec2 vArchitecturalUv;')
        .replace('#include <map_fragment>',THREE.ShaderChunk.map_fragment.replaceAll('vMapUv','vArchitecturalUv'));
    };
    mat.customProgramCacheKey=()=> 'school-world-wall-v1';this.instancedWall=mat;return mat;
  }

  finishSurfaces(chunk,walls) {
    const ids=new Set(walls.map(w=>w.id));
    for(const mesh of chunk.meshes) {
      if(mesh.isInstancedMesh && /_walls_inst$/.test(mesh.name)) {
        mesh.material=this.instancedWallMaterial();continue;
      }
      if(!mesh.isMesh || mesh.isInstancedMesh || !mesh.geometry?.attributes.uv)continue;
      const dims=mesh.geometry.parameters;
      const isWall=ids.has(mesh.name) || (dims?.height>=2.5 && Math.min(dims.width,dims.depth)<.8 && Math.max(dims.width,dims.depth)>1);
      const isFloor=/(?:_floor(?:_|$))/.test(mesh.name)&&mesh.geometry.type==='PlaneGeometry';
      if(!isWall&&!isFloor)continue;
      const upper=mesh.position.y>3;
      mesh.material=this.material(isFloor?(upper?'upper':'wood'):(upper?'wall-upper':'wall'));
      mesh.geometry=mesh.geometry.clone();mesh.userData.ownsSurfaceGeometry=true;
      mesh.updateMatrixWorld(true);
      const pos=mesh.geometry.attributes.position,normal=mesh.geometry.attributes.normal,uv=mesh.geometry.attributes.uv;
      const point=new THREE.Vector3(),n=new THREE.Vector3();
      const floorY=isFloor?mesh.position.y:Math.round((mesh.position.y-1.4)/5)*5;
      for(let i=0;i<pos.count;i++) {
        point.fromBufferAttribute(pos,i).applyMatrix4(mesh.matrixWorld);
        n.fromBufferAttribute(normal,i).transformDirection(mesh.matrixWorld);
        if(isFloor)uv.setXY(i,point.x/2, -point.z/(upper?2:4));
        else uv.setXY(i,(Math.abs(n.x)>.5?point.z:point.x)/2,(point.y-floorY)/2.8);
      }
      uv.needsUpdate=true;mesh.receiveShadow=true;
    }
    // Old large rectangular red overlays read as detached tiles floating 4cm
    // above the floor. Retain authored small stains, remove only these sheets.
    for(const mesh of chunk.meshes)if(/_blood_(north|west|south)_lab$|_blood_carpet$|_false_arrow$|_hall_stripe_(ew|ns)$/.test(mesh.name))mesh.visible=false;
  }

  mountDecorations(chunk,walls) {
    for(const mesh of chunk.meshes) {
      if(/_class_door_\d+$/.test(mesh.name)){mesh.visible=false;continue;}
      if(!/room_plate|wall_decay_decal|_ofuda$|stopped_clock|_nook_sign|chalkboard|_hall_(?:through_)?board_\d+$|_hall_pa|_hall_clock/.test(mesh.name))continue;
      const bounds=new THREE.Box3().setFromObject(mesh),size=bounds.getSize(new THREE.Vector3());
      const half=Math.max(.12,Math.max(size.x,size.z)/2);
      const depth=Math.min(size.x,size.z)/2+.012;
      let best=null;
      for(const {aabb:a} of walls) {
        if(mesh.position.y<a.minY+.2||mesh.position.y>a.maxY-.15)continue;
        const alongX=a.maxX-a.minX>a.maxZ-a.minZ;
        if((alongX?a.maxX-a.minX:a.maxZ-a.minZ)<half*2+.12)continue;
        for(const sign of [-1,1]) {
          const x=alongX?THREE.MathUtils.clamp(mesh.position.x,a.minX+half+.04,a.maxX-half-.04):(sign>0?a.maxX+depth:a.minX-depth);
          const z=alongX?(sign>0?a.maxZ+depth:a.minZ-depth):THREE.MathUtils.clamp(mesh.position.z,a.minZ+half+.04,a.maxZ-half-.04);
          const probe=new THREE.Vector3(x+(alongX?0:sign*.6),a.minY+.03,z+(alongX?sign*.6:0));
          if(!this.builder.collisionWorld.getSurfaceAt(probe).walkable||this.builder.collisionWorld.isCircleBlocked(probe,.3))continue;
          const d=Math.hypot(x-mesh.position.x,z-mesh.position.z);
          if(d<3&&(!best||d<best.d))best={x,z,d,yaw:alongX?(sign>0?0:Math.PI):(sign>0?Math.PI/2:-Math.PI/2)};
        }
      }
      if(best){mesh.position.x=best.x;mesh.position.z=best.z;mesh.rotation.y=best.yaw;mesh.userData.wallMounted=true;}
      else mesh.visible=false;
      // Large decals obscure the carefully scaled finish; only small remnants remain.
      if(/wall_decay_decal/.test(mesh.name))mesh.visible=false;
    }
  }

  addLighting(chunk) {
    const world=this.builder.collisionWorld;
    const areas=world.floorAreas.filter(a=>a.chunkId===chunk.chunkId&&a.type==='walkable');
    const used=new Set();
    const ceilings=chunk.meshes.filter(m=>m.isMesh&&/(ceil|ceiling)/.test(m.name))
      .map(m=>({y:m.position.y,bounds:new THREE.Box3().setFromObject(m)}));
    for(const a of areas) {
      const xs=[],zs=[];
      for(let x=Math.ceil((a.minX+.6)/4)*4;x<a.maxX-.6;x+=4)xs.push(x);
      for(let z=Math.ceil((a.minZ+.6)/4)*4;z<a.maxZ-.6;z+=4)zs.push(z);
      if(!xs.length)xs.push((a.minX+a.maxX)/2);
      if(!zs.length)zs.push((a.minZ+a.maxZ)/2);
      for(const x of xs)for(const z of zs) {
        const key=`${x.toFixed(2)},${a.y},${z.toFixed(2)}`;
        if(used.has(key))continue;used.add(key);
        const p=new THREE.Vector3(x,a.y+.05,z);
        if(world.isCircleBlocked(p,.65)||!world.getSurfaceAt(p).walkable)continue;
        // Only install lights under a real ceiling; never hang fixtures over stair voids.
        const covered=ceilings.some(({y,bounds:b})=>Math.abs(y-(a.y+2.8))<.25
          &&x>b.min.x+.3&&x<b.max.x-.3&&z>b.min.z+.3&&z<b.max.z-.3);
        if(!covered)continue;
        const body=new THREE.Mesh(this.lampBody,this.lampMetal);body.position.set(x,a.y+2.73,z);
        body.name=`${chunk.chunkId}_ceiling_housing_${used.size}`;
        const mat=new THREE.MeshStandardMaterial({color:0xf2eedb,emissive:0xffe6ba,emissiveIntensity:.18,roughness:.55});
        const tube=new THREE.Mesh(this.lampTube,mat);tube.position.set(x,a.y+2.68,z);tube.name=`${chunk.chunkId}_ceiling_tube_${used.size}`;
        this.builder.scene.add(body,tube);chunk.meshes.push(body,tube);
        chunk.lights.push({mesh:tube,localPos:new THREE.Vector3(x-chunk.center.x,a.y+2.48-chunk.center.y,z-chunk.center.z),baseIntensity:LIGHTING_CONFIG.ceilingLightIntensity,currentIntensity:LIGHTING_CONFIG.ceilingLightIntensity,isFlickering:false,flickerTimer:5,voltagePhase:x*.17+z*.1,pooledLight:null});
      }
    }
  }
}
