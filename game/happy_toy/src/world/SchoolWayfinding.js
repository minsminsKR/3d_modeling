import * as THREE from 'three';

const LABELS = { classroom:'교실', start:'중앙 홀 · 제단', stairs_b1:'지하 보육실 ↓', stairs_2f:'2층 액자실 ↑', playroom:'놀이방', storage:'준비물 창고', archive:'폐관 도서실', workshop:'보육실', nurse_office:'보건실', music_room:'음악실', faculty_office:'교무실', science_lab:'과학실', gymnasium:'체육관', courtyard:'중정', auditorium:'강당', foyer:'강당 로비', art_room:'미술실', studio:'촬영실', broadcast:'방송실', darkroom:'암실', greenroom:'대기실', home_ec:'가정실', club_room:'서도부', tatami_room:'예절실' };

// Physical hanging plates mark actual graph connections. No colliders or invented exits.
export class SchoolWayfinding {
  constructor(builder) {
    this.builder=builder;
    this.materials=new Map();
    this.frameMaterial=new THREE.MeshStandardMaterial({color:0x303b34,roughness:.78,metalness:.25});
    this.frameGeometry=new THREE.BoxGeometry(1.82,.36,.065);
    this.faceGeometry=new THREE.PlaneGeometry(1.72,.28);
    this.rodGeometry=new THREE.CylinderGeometry(.008,.008,.25,5);
  }
  material(text,annex) {
    const key=`${annex}:${text}`;
    if(this.materials.has(key)) return this.materials.get(key);
    const c=document.createElement('canvas');c.width=768;c.height=128;
    const ctx=c.getContext('2d');
    ctx.fillStyle=annex?'#b9b39b':'#c9c4b2';ctx.fillRect(0,0,768,128);
    ctx.fillStyle=annex?'#543e2c':'#31413b';ctx.fillRect(0,0,14,128);
    ctx.font='500 44px "Malgun Gothic", sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';
    ctx.fillText(text,384,64,720);
    ctx.strokeStyle='#8a8875';ctx.lineWidth=2;ctx.strokeRect(24,12,726,104);
    const map=new THREE.CanvasTexture(c);map.colorSpace=THREE.SRGBColorSpace;map.anisotropy=4;
    const material=new THREE.MeshStandardMaterial({map,roughness:.82,emissive:0x19170f,emissiveIntensity:.16});
    this.materials.set(key,material);return material;
  }
  decorate(chunk) {
    if(chunk.type==='void'||chunk.type==='stairs_b1'||chunk.type==='stairs_2f') return;
    const g=this.builder.generator, open=g.getOpenings(chunk.cx,chunk.cz);
    for(const [side,dx,dz,yaw] of [['N',0,-1,0],['S',0,1,Math.PI],['E',1,0,-Math.PI/2],['W',-1,0,Math.PI/2]]) {
      if(!open[side]) continue;
      const x=chunk.cx+dx,z=chunk.cz+dz;
      const type=g.getChunkType(x,z);
      const label=LABELS[type] || (x===3&&z===0?'본관 ↔ 별관':`${x>=4?'별관':'본관'} ${z<0?'북':z>0?'남':'중앙'}복도`);
      const anchor=new THREE.Group();anchor.name=`${chunk.chunkId}_wayfinding_${side}`;
      anchor.position.set(chunk.center.x+dx*7.35,2.42,chunk.center.z+dz*7.35);anchor.rotation.y=yaw;
      const frame=new THREE.Mesh(this.frameGeometry,this.frameMaterial);frame.castShadow=true;anchor.add(frame);
      const face=new THREE.Mesh(this.faceGeometry,this.material(label,x>=4));face.position.z=.034;anchor.add(face);
      for(const sx of [-.66,.66]) {const rod=new THREE.Mesh(this.rodGeometry,this.frameMaterial);rod.position.set(sx,.29,0);anchor.add(rod);}
      this.builder.scene.add(anchor);chunk.meshes.push(anchor);
    }
  }
}
