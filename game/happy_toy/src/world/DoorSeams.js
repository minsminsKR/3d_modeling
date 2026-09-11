// Two adjacent chunks may author the same portal on either side of a seam.
export function samePortal(a,b) {
  if(a.axis!==b.axis||Math.abs(a.position.y-b.position.y)>.2)return false;
  const normal=a.axis==='x'?'z':'x';
  return Math.abs(a.position[a.axis]-b.position[a.axis])<.25
    &&Math.abs(a.position[normal]-b.position[normal])<.85
    &&Math.abs(a.panelSpan-b.panelSpan)<.5;
}
export function reconcileDoorSeams(doors) {
  const kept=[];
  for(const door of [...doors].sort((a,b)=>Number(b.isLocked||b.isBlocked)-Number(a.isLocked||a.isBlocked)||a.id.localeCompare(b.id))) {
    const leader=kept.find(d=>samePortal(d,door));
    if(leader) {
      if(door.isOpen&&!leader.isLocked&&!leader.isBlocked)leader.isOpen=true;
      door.isDuplicate=true;door.canonicalDoor=leader;door.group.visible=false;
    } else {
      if(door.canonicalDoor)door.isOpen=door.canonicalDoor.isOpen;
      door.isDuplicate=false;door.canonicalDoor=null;door.group.visible=true;kept.push(door);
    }
  }
}
