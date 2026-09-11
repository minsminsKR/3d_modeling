import * as THREE from 'three';

export function overlaps(a,b,pad=0) {
  return a.minY<b.maxY-.02 && a.maxY>b.minY+.02
    && a.minX<b.maxX+pad && a.maxX>b.minX-pad
    && a.minZ<b.maxZ+pad && a.maxZ>b.minZ-pad;
}

export function doorClearance(door) {
  const [sx,sy,sz]=door.size,p=door.position;
  return {minX:p.x-sx/2-(sx<sz?1.4:.35),maxX:p.x+sx/2+(sx<sz?1.4:.35),
    minZ:p.z-sz/2-(sx<sz?.35:1.4),maxZ:p.z+sz/2+(sx<sz?.35:1.4),minY:p.y,maxY:p.y+sy};
}

// Put storage against actual walls, facing a usable aisle, never a doorway.
export function placeCabinets(builder,chunk,walls,extraDoors=[]) {
  const world=builder.collisionWorld;
  const doors=[...(builder.doors||[]),...(chunk.doors||[]),...extraDoors].map(doorClearance);
  for(const cabinet of chunk.cabinets||[]) {
    if(cabinet.occupied)continue;
    world.blockers=world.blockers.filter(b=>b.id!==cabinet.id);
    const original=cabinet.position.clone(),originalYaw=cabinet.yaw;
    const blockers=world.getActiveBlockers({position:original,includeDoors:false});
    let best=null;
    for(const {aabb:a} of walls) {
      if(Math.abs(a.minY-original.y)>.3)continue;
      const alongX=a.maxX-a.minX>a.maxZ-a.minZ;
      const min=alongX?a.minX:a.minZ,max=alongX?a.maxX:a.maxZ;
      const half=cabinet.size[0]/2+.12,depth=cabinet.size[2]/2+.04;
      for(let along=min+half;along<=max-half;along+=.5)for(const sign of [-1,1]) {
        cabinet.position.set(alongX?along:(sign>0?a.maxX+depth:a.minX-depth),original.y,
          alongX?(sign>0?a.maxZ+depth:a.minZ-depth):along);
        cabinet.yaw=alongX?(sign>0?Math.PI:0):(sign>0?-Math.PI/2:Math.PI/2);
        const box=cabinet.getAabb(),exit=cabinet.getExitPosition(),guard=cabinet.getGuardPosition();
        if(doors.some(d=>overlaps(box,d,.15)))continue;
        if(blockers.some(b=>overlaps(box,typeof b.aabb==='function'?b.aabb():b.aabb,.02)))continue;
        if(!world.getSurfaceAt(cabinet.position).walkable||!world.getSurfaceAt(exit).walkable
            ||world.isCircleBlocked(exit,.4,{includeDoors:false})
            ||world.isCircleBlocked(guard,.4,{includeDoors:false}))continue;
        const distance=cabinet.position.distanceTo(original);
        if(!best||distance<best.distance)best={position:cabinet.position.clone(),yaw:cabinet.yaw,distance};
      }
    }
    cabinet.position.copy(best?.position||original);cabinet.yaw=best?.yaw??originalYaw;
    cabinet.group.position.copy(cabinet.position);cabinet.group.rotation.y=cabinet.yaw;
    cabinet.group.userData.placementVerified=!!best;
    if(best)world.blockers.push({id:cabinet.id,type:'furniture',chunkId:chunk.chunkId,
      aabb:()=>cabinet.getAabb(),active:()=>!cabinet.occupied});
  }
}

export function clearDoorFurniture(builder,chunk,extraDoors=[]) {
  const world=builder.collisionWorld;
  const doors=[...(builder.doors||[]),...(chunk.doors||[]),...extraDoors].map(doorClearance);
  for(const blocker of world.blockers.filter(b=>b.chunkId===chunk.chunkId&&b.type==='static'
      &&typeof b.aabb!=='function'&&/desk|chair|shelf|cabinet|locker|bucket|bench|table/.test(b.id)
      && !/wall|frame|ceil/.test(b.id))) {
    const box=blocker.aabb;
    if(!doors.some(d=>overlaps(box,d)))continue;
    const mesh=chunk.meshes.find(m=>m.name===blocker.id);
    if(!mesh)continue;
    const others=world.blockers.filter(b=>b!==blocker&&b.active());
    let best=null;
    for(let dx=-3;dx<=3;dx+=.5)for(let dz=-3;dz<=3;dz+=.5) {
      const distance=dx*dx+dz*dz;if(best&&distance>=best.distance)continue;
      const moved={...box,minX:box.minX+dx,maxX:box.maxX+dx,minZ:box.minZ+dz,maxZ:box.maxZ+dz};
      if(doors.some(d=>overlaps(moved,d,.1))||others.some(b=>overlaps(moved,typeof b.aabb==='function'?b.aabb():b.aabb,.08)))continue;
      const points=[[moved.minX,moved.minZ],[moved.maxX,moved.minZ],[moved.minX,moved.maxZ],[moved.maxX,moved.maxZ]];
      if(points.some(([x,z])=>!world.getSurfaceAt(new THREE.Vector3(x,box.minY,z)).walkable))continue;
      best={dx,dz,distance,moved};
    }
    if(best) {mesh.position.x+=best.dx;mesh.position.z+=best.dz;blocker.aabb=best.moved;mesh.userData.clearanceAdjusted=true;}
  }
}
