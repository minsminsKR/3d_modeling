import * as THREE from 'three';

// Finish the actual collision walls, rather than scattering floating trim.
// All trim stays on the wall footprint; door and corridor openings stay clear.
export class SchoolArchitecture {
  constructor(builder) {
    this.builder=builder;
    this.box=new THREE.BoxGeometry(1,1,1);
    this.trim=new THREE.MeshStandardMaterial({color:0x665b49,roughness:0.86});
    this.metal=new THREE.MeshStandardMaterial({color:0x706d60,roughness:0.55,metalness:0.4});
    this.red=new THREE.MeshStandardMaterial({color:0x873e30,roughness:0.6,metalness:0.25});
    this.stationGeometry={back:new THREE.BoxGeometry(.42,.76,.035),body:new THREE.CylinderGeometry(.075,.075,.36,10),handle:new THREE.BoxGeometry(.14,.035,.06),label:new THREE.PlaneGeometry(.34,.17)};
    const canvas=document.createElement('canvas');canvas.width=256;canvas.height=128;
    const ctx=canvas.getContext('2d');ctx.fillStyle='#d8d0ad';ctx.fillRect(0,0,256,128);
    ctx.fillStyle='#713b31';ctx.fillRect(8,8,240,32);
    ctx.font='bold 21px "Malgun Gothic"';ctx.textAlign='center';ctx.fillStyle='#eee6d0';ctx.fillText('화재 시 사용',128,32);
    ctx.fillStyle='#333b35';ctx.font='18px "Malgun Gothic"';ctx.fillText('안전핀 → 손잡이',128,75);ctx.fillText('소화기',128,106);
    const map=new THREE.CanvasTexture(canvas);map.colorSpace=THREE.SRGBColorSpace;
    this.label=new THREE.MeshStandardMaterial({map,roughness:0.9});
  }

  decorate(chunk) {
    const walls=this.builder.collisionWorld.blockers.filter(b=>b.chunkId===chunk.chunkId
      && b.type==='static' && /wall|hall_class|stem/.test(b.id)
      && typeof b.aabb!=='function'
      && b.aabb.maxY-b.aabb.minY>2.3
      && Math.min(b.aabb.maxX-b.aabb.minX,b.aabb.maxZ-b.aabb.minZ)<0.8);
    if(!walls.length) return;
    // Old notes used nominal tile edges even when those edges were doorways.
    // Project each note onto an actual wall face with a reachable reading spot.
    const wallFixtures=(chunk.safeLights||[]).filter(light=>['wall-switch','ceiling-switch'].includes(light.variant));
    for(const note of [...(chunk.loreNotes || []),...wallFixtures]) {
      let best=null;
      for(const {aabb:a} of walls) {
        if(note.position.y<a.minY+.3 || note.position.y>a.maxY-.3) continue;
        const alongX=a.maxX-a.minX>a.maxZ-a.minZ;
        const span=alongX?a.maxX-a.minX:a.maxZ-a.minZ;
        if(span<.4)continue;
        for(const sign of [-1,1]) {
          const x=alongX?THREE.MathUtils.clamp(note.position.x,a.minX+.18,a.maxX-.18):(sign>0?a.maxX+.025:a.minX-.025);
          const z=alongX?(sign>0?a.maxZ+.025:a.minZ-.025):THREE.MathUtils.clamp(note.position.z,a.minZ+.18,a.maxZ-.18);
          const probe=new THREE.Vector3(x+(alongX?0:sign*.75),a.minY,z+(alongX?sign*.75:0));
          if(!this.builder.collisionWorld.getSurfaceAt(probe).walkable
            ||this.builder.collisionWorld.isCircleBlocked(probe,.34,{includeDoors:false}))continue;
          const distance=Math.hypot(x-note.position.x,z-note.position.z);
          if(!best||distance<best.distance)best={x,z,distance,yaw:alongX?(sign>0?0:Math.PI):(sign>0?Math.PI/2:-Math.PI/2)};
        }
      }
      if(best) {
        note.position.x=best.x;note.position.z=best.z;note.yaw=best.yaw+(note.variant?Math.PI:0);
        note.group.position.copy(note.position);note.group.rotation.y=note.yaw;
        note.group.userData.wallMounted=true;
      }
    }
    const matrices=[]; const matrix=new THREE.Matrix4();
    const p=new THREE.Vector3(),s=new THREE.Vector3(),q=new THREE.Quaternion();
    for(const {aabb:a} of walls) {
      const x=(a.minX+a.maxX)/2,z=(a.minZ+a.maxZ)/2;
      const sx=a.maxX-a.minX,sz=a.maxZ-a.minZ;
      for(const [y,height] of [[a.minY+0.09,0.18],[a.maxY-0.07,0.14]]) {
        p.set(x,y,z);s.set(sx+0.024,height,sz+0.024);
        matrices.push(matrix.compose(p,q,s).clone());
      }
    }
    const mesh=new THREE.InstancedMesh(this.box,this.trim,matrices.length);
    matrices.forEach((m,i)=>mesh.setMatrixAt(i,m));mesh.computeBoundingSphere();
    mesh.name=`${chunk.chunkId}_architectural_courses`;mesh.receiveShadow=true;
    mesh.userData.releaseInstanceOnUnload=true;
    this.builder.scene.add(mesh);chunk.meshes.push(mesh);

    if(chunk.type.startsWith('stairs') || (Math.abs(chunk.cx)+Math.abs(chunk.cz))%3!==0) return;
    // Locate a long wall facing a verified walkable surface before mounting
    // the newly modeled safety station. Its shallow depth clears player radius.
    for(const {aabb:a} of walls) {
      const sx=a.maxX-a.minX,sz=a.maxZ-a.minZ;
      if(Math.max(sx,sz)<2) continue;
      const alongX=sx>sz;
      for(const sign of [-1,1]) {
        const x=(a.minX+a.maxX)/2+(alongX?0:sign*(sx/2+0.02));
        const z=(a.minZ+a.maxZ)/2+(alongX?sign*(sz/2+0.02):0);
        const test=new THREE.Vector3(x+(alongX?0:sign*.7),a.minY,z+(alongX?sign*.7:0));
        if(!this.builder.collisionWorld.getSurfaceAt(test).walkable
          || this.builder.collisionWorld.isCircleBlocked(test,.34)) continue;
        const group=new THREE.Group();group.name=`${chunk.chunkId}_fire_station`;
        group.position.set(x,a.minY+1.15,z);group.rotation.y=alongX?(sign>0?0:Math.PI):(sign>0?Math.PI/2:-Math.PI/2);
        const back=new THREE.Mesh(this.stationGeometry.back,this.metal);group.add(back);
        const body=new THREE.Mesh(this.stationGeometry.body,this.red);body.position.set(0,-.12,.092);group.add(body);
        const handle=new THREE.Mesh(this.stationGeometry.handle,this.trim);handle.position.set(0,.085,.092);group.add(handle);
        const label=new THREE.Mesh(this.stationGeometry.label,this.label);label.position.set(0,.24,.021);group.add(label);
        group.traverse(o=>{if(o.isMesh){o.castShadow=true;o.receiveShadow=true;}});
        this.builder.scene.add(group);chunk.meshes.push(group);return;
      }
    }
  }
}
