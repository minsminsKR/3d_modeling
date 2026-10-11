import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {TransformControls} from 'three/addons/controls/TransformControls.js';
import {RoomEnvironment} from 'three/addons/environments/RoomEnvironment.js';

const get=id=>document.getElementById(id), config=JSON.parse(get('editor-config').textContent);
const clone=value=>JSON.parse(JSON.stringify(value));
let data, draft, original='', selected=null, selectedGroup='', actor, grid, transform, renderer, orbit;
let dirty=false,busy=false,testing=false,testStart=0,frame=0,dragging=false,committingField=null,version=config.version,currentVersion;
let undo=[],redo=[],newCounter=0,groupCounter=0;
const palette=['#2f6d45','#3a78aa','#b65b35','#825bb0','#b98a29','#277d79','#b84d77','#5865ad'];
const boneNodes=new Map(), originalWorld=new Map(), poseBase=new Map(), markers=new Map();
const meshes=[], skeletons=new Set(), markerGroup=new THREE.Group();
const scene=new THREE.Scene();scene.background=new THREE.Color('#eee9dc');
const camera=new THREE.PerspectiveCamera(36,1,.001,100);
const lines=new THREE.LineSegments(new THREE.BufferGeometry(),new THREE.LineBasicMaterial({vertexColors:true,depthTest:false,transparent:true,opacity:.95}));
lines.renderOrder=20;scene.add(lines,markerGroup);
const sphereGeometry=new THREE.SphereGeometry(1,10,8);
const status=(text,error=false)=>{get('editor-status').textContent=text;get('editor-status').dataset.error=String(error);};
const joint=id=>draft?.joints.find(j=>j.id===id);
const members=id=>{const out=[id];for(let n=0;n<out.length;n++) for(const j of draft.joints) if(j.parent===out[n]) out.push(j.id);return out;};
const depth=j=>{let n=0;for(let p=j.parent;p!==null&&p!==undefined;p=joint(p)?.parent)n++;return n;};
const groupFor=id=>draft.groups.find(g=>g.joints.includes(id));
const groupColor=g=>g?palette[Math.max(0,draft.groups.indexOf(g))%palette.length]:'#bd863d';
const structural=()=>data && (draft.joints.length!==data.joints.length || draft.joints.some(j=>!data.joints.some(o=>o.id===j.id&&o.parent===j.parent)));
const pushUndo=()=>{undo.push(clone(draft));if(undo.length>60)undo.shift();redo=[];};
const change=fn=>{if(busy)return;stopTest();const before=clone(draft);fn();if(JSON.stringify(before)!==JSON.stringify(draft)){undo.push(before);if(undo.length>60)undo.shift();redo=[];}sync();};
const fieldValue=(id,value)=>{if(committingField!==id)get(id).value=value;};
const requestFrame=()=>{if(!frame)frame=requestAnimationFrame(renderFrame);};
const markDirty=()=>{dirty=JSON.stringify(draft)!==original;get('editor-dirty').textContent=dirty?'저장하지 않은 수정':'저장된 뼈대';};
const api=async(url,options)=>{const response=await fetch(url,options);const body=await response.json();if(!response.ok)throw Error(body.error||'요청을 처리하지 못했습니다.');return body;};

function restPose(){
  if(!actor||!draft||structural())return;
  const desired=new Map();
  for(const j of draft.joints){const base=originalWorld.get(j.id);if(base)desired.set(j.id,base.clone().setPosition(...j.position));}
  for(const j of [...draft.joints].sort((a,b)=>depth(a)-depth(b))){
    const bone=boneNodes.get(j.id), world=desired.get(j.id);if(!bone||!world)continue;
    const parentWorld=j.parent?desired.get(j.parent):bone.parent?.matrixWorld;
    const local=(parentWorld?parentWorld.clone().invert():new THREE.Matrix4()).multiply(world);
    local.decompose(bone.position,bone.quaternion,bone.scale);
  }
  actor.updateMatrixWorld(true);
  for(const skeleton of skeletons)skeleton.calculateInverses();
  poseBase.clear();
  for(const [id,bone] of boneNodes)poseBase.set(id,{position:bone.position.clone(),quaternion:bone.quaternion.clone(),scale:bone.scale.clone(),parentWorld:bone.parent.matrixWorld.clone()});
}
function showMarkers(animated=false){
  if(!draft)return;
  const radius=Math.max(data.bounds[1][1]-data.bounds[0][1],.05)*.009;
  for(const [id,object] of markers)if(!joint(id)){markerGroup.remove(object);object.material.dispose();markers.delete(id);}
  const coordinates=new Map();
  for(const j of draft.joints){
    let object=markers.get(j.id);
    if(!object){object=new THREE.Mesh(sphereGeometry,new THREE.MeshBasicMaterial({depthTest:false}));object.userData.jointId=j.id;object.renderOrder=21;markers.set(j.id,object);markerGroup.add(object);}
    const p=animated&&boneNodes.has(j.id)?boneNodes.get(j.id).getWorldPosition(new THREE.Vector3()):new THREE.Vector3(...j.position);
    coordinates.set(j.id,p);object.position.copy(p);object.scale.setScalar(radius*(j.id===selected?1.6:1));object.material.color.set(j.id===selected?'#d53b30':groupColor(groupFor(j.id)));
  }
  const values=[],colors=[];
  for(const j of draft.joints)if(j.parent&&coordinates.has(j.parent)){
    values.push(...coordinates.get(j.parent).toArray(),...coordinates.get(j.id).toArray());
    const c=new THREE.Color(groupColor(groupFor(j.id)));colors.push(c.r,c.g,c.b,c.r,c.g,c.b);
  }
  const old=lines.geometry;lines.geometry=new THREE.BufferGeometry();
  lines.geometry.setAttribute('position',new THREE.Float32BufferAttribute(values,3));lines.geometry.setAttribute('color',new THREE.Float32BufferAttribute(colors,3));old.dispose();
  if(transform){if(selected&&markers.has(selected)&&!testing&&!busy)transform.attach(markers.get(selected));else transform.detach();}
  requestFrame();
}
function renderTree(){
  const tree=get('editor-joints');tree.replaceChildren();
  const append=(parent,level)=>{
    for(const j of draft.joints.filter(j=>j.parent===parent)){
      const button=document.createElement('button');button.type='button';button.dataset.joint=j.id;
      button.setAttribute('aria-pressed',String(j.id===selected));button.style.paddingLeft=`${8+Math.min(level,8)*12}px`;
      const dot=document.createElement('span');dot.className='joint-dot';dot.style.background=groupColor(groupFor(j.id));
      const name=document.createElement('span');name.textContent=j.name;button.append(dot,name);
      button.addEventListener('click',()=>selectJoint(j.id));tree.append(button);append(j.id,level+1);
    }
  };append(null,0);get('editor-count').textContent=`관절 ${draft.joints.length}개 · 그룹 ${draft.groups.length}개`;
}
function option(value,label){const item=document.createElement('option');item.value=value;item.textContent=label;return item;}
function renderInspector(){
  const j=joint(selected);get('joint-fields').disabled=!j||busy||testing;
  get('editor-selection').textContent=j?`${j.name} · 관절 ${j.id}`:'관절을 선택하세요.';
  if(!j)return;
  fieldValue('joint-name',j.name);
  const forbidden=new Set(members(j.id));
  get('joint-parent').replaceChildren(option('','최상위 관절'),...draft.joints.filter(o=>!forbidden.has(o.id)).map(o=>option(o.id,o.name)));
  get('joint-parent').value=j.parent||'';get('joint-parent').disabled=j.parent===null||busy||testing;
  ['x','y','z'].forEach((axis,n)=>{fieldValue(`joint-${axis}`,Number(j.position[n].toFixed(5)));});
  get('joint-group').replaceChildren(option('','미지정'),...draft.groups.map(g=>option(g.id,g.name)));
  get('joint-group').value=groupFor(j.id)?.id||'';get('joint-delete').disabled=j.parent===null;get('joint-copy').disabled=j.parent===null;
}
function renderGroups(){
  const available=draft.groups.some(g=>g.id===selectedGroup);if(!available)selectedGroup=draft.groups[0]?.id||'';
  get('group-select').replaceChildren(...(draft.groups.length?draft.groups.map(g=>option(g.id,g.name)):[option('','그룹 없음')]));
  get('group-select').value=selectedGroup;
  get('editor-test-group').replaceChildren(option('all','전체 그룹'),...draft.groups.map(g=>option(g.id,g.name)));
  const g=draft.groups.find(g=>g.id===selectedGroup);get('group-fields').disabled=!g||busy||testing;
  if(g){fieldValue('group-name',g.name);get('group-type').value=g.type;fieldValue('group-amplitude',g.amplitude);fieldValue('group-phase',Math.round(g.phase*100));}
  get('group-add').disabled=!selected||busy||testing;
}
function weightView(){
  let affected=0,maximum=0;
  for(const mesh of meshes){
    const use=get('editor-weights').checked&&selected;
    if(!use){mesh.material=mesh.userData.originalMaterial;if(mesh.userData.originalColor)mesh.geometry.setAttribute('color',mesh.userData.originalColor);else mesh.geometry.deleteAttribute('color');continue;}
    const indices=mesh.geometry.getAttribute('skinIndex'), weights=mesh.geometry.getAttribute('skinWeight');
    if(!indices||!weights)continue;
    const index=mesh.skeleton.bones.indexOf(boneNodes.get(selected));
    if(mesh.userData.weightJoint===selected&&mesh.geometry.getAttribute('color')===mesh.userData.weightColor){mesh.material=mesh.userData.weightMaterial;affected+=mesh.userData.affected||0;maximum=Math.max(maximum,mesh.userData.maxWeight||0);continue;}
    if(!mesh.userData.weightColor)mesh.userData.weightColor=new THREE.BufferAttribute(new Float32Array(indices.count*3),3);
    const colors=mesh.userData.weightColor.array,color=new THREE.Color();
    let count=0,maxWeight=0;
    for(let v=0;v<indices.count;v++){
      let strength=0;for(let c=0;c<4;c++)if(indices.array[v*4+c]===index)strength+=weights.array[v*4+c];
      if(strength>.001)count++;maxWeight=Math.max(maxWeight,strength);
      color.setRGB(.12+strength*.88,.2+(1-Math.abs(strength-.5)*2)*.55,.48*(1-strength));
      colors.set([color.r,color.g,color.b],v*3);
    }
    mesh.userData.weightJoint=selected;mesh.userData.affected=count;mesh.userData.maxWeight=maxWeight;affected+=count;maximum=Math.max(maximum,maxWeight);mesh.userData.weightColor.needsUpdate=true;mesh.geometry.setAttribute('color',mesh.userData.weightColor);
    mesh.material=mesh.userData.weightMaterial;
  }get('editor-weight-summary').classList.toggle('hidden',!get('editor-weights').checked);get('editor-weight-summary').textContent=`영향 정점 ${affected.toLocaleString()}개 · 최대 ${(maximum*100).toFixed(1)}%`;requestFrame();
}
function sync(){
  if(!draft)return;markDirty();restPose();showMarkers();renderTree();renderInspector();renderGroups();weightView();
  const altered=structural();get('editor-weight-mode').options[0].disabled=altered;if(altered)get('editor-weight-mode').value='ai';
  get('editor-save').disabled=!actor||!dirty||busy;get('editor-undo').disabled=!undo.length||busy;get('editor-redo').disabled=!redo.length||busy;
  get('editor-test').disabled=!actor||!draft.groups.length||altered||busy;
  get('editor-weight-mode').disabled=busy;get('editor-version').disabled=busy;get('version-load').disabled=busy;get('version-restore').disabled=busy;
}
function selectJoint(id){stopTest();selected=id;selectedGroup=groupFor(id)?.id||selectedGroup;showMarkers();renderTree();renderInspector();renderGroups();weightView();}
function stopTest(){
  if(!testing)return;testing=false;get('editor-test').setAttribute('aria-pressed','false');get('editor-test').textContent='동작 테스트';restPose();showMarkers();renderInspector();renderGroups();
}
function startTest(){
  if(testing){stopTest();return;}if(structural()||busy)return;
  restPose();testing=true;testStart=performance.now();get('editor-test').setAttribute('aria-pressed','true');get('editor-test').textContent='테스트 정지';
  transform.detach();get('joint-fields').disabled=true;get('group-fields').disabled=true;requestFrame();
}
function renderFrame(t){
  frame=0;orbit?.update();
  if(testing){
    for(const [id,base] of poseBase){const bone=boneNodes.get(id);bone.position.copy(base.position);bone.quaternion.copy(base.quaternion);bone.scale.copy(base.scale);}
    const p=((t-testStart)/3200)%1, envelope=Math.sin(Math.PI*p)**2;
    for(const g of draft.groups){
      if(get('editor-test-group').value!=='all'&&get('editor-test-group').value!==g.id)continue;
      const set=new Set(g.joints),axis=new THREE.Vector3(...({arm:[0,0,1],leg:[1,0,0],head:[1,0,0],other:[0,1,0]}[g.type]));
      const angle=THREE.MathUtils.degToRad(g.amplitude)*Math.sin(2*Math.PI*(p+g.phase))*envelope;
      for(const id of set){if(set.has(joint(id)?.parent))continue;const bone=boneNodes.get(id),base=poseBase.get(id);if(!bone||!base)continue;
        const localAxis=axis.clone().transformDirection(base.parentWorld.clone().invert());
        const sign=g.type==='arm'&&joint(id).position[0]>0?-1:1;bone.quaternion.premultiply(new THREE.Quaternion().setFromAxisAngle(localAxis,angle*sign));
      }
    }actor.updateMatrixWorld(true);showMarkers(true);
  }
  if(renderer)renderer.render(scene,camera);
  if(testing)requestFrame();
}
function fit(front=false){
  if(!data)return;const lo=new THREE.Vector3(...data.bounds[0]),hi=new THREE.Vector3(...data.bounds[1]);
  const center=lo.clone().add(hi).multiplyScalar(.5),size=hi.clone().sub(lo),distance=Math.max(size.y,size.x/camera.aspect,size.z)*1.7;
  orbit.target.copy(center);camera.position.copy(center).add(new THREE.Vector3(front?0:distance*.75,distance*.25,distance));camera.near=Math.max(distance/1000,.0001);camera.far=distance*100;camera.updateProjectionMatrix();orbit.update();requestFrame();
}
function disposeActor(){
  if(!actor)return;scene.remove(actor);const materials=new Set(),textures=new Set();
  actor.traverse(o=>{if(o.isMesh){o.geometry.dispose();for(const m of (Array.isArray(o.material)?o.material:[o.material]))materials.add(m);for(const m of (Array.isArray(o.userData.originalMaterial)?o.userData.originalMaterial:[o.userData.originalMaterial]))if(m)materials.add(m);if(o.userData.weightMaterial)materials.add(o.userData.weightMaterial);}});
  for(const m of materials){for(const value of Object.values(m))if(value?.isTexture)textures.add(value);m.dispose();}for(const texture of textures)texture.dispose();for(const skeleton of skeletons)skeleton.dispose();actor=null;
}
function transparency(){for(const mesh of meshes)for(const material of (Array.isArray(mesh.userData.originalMaterial)?mesh.userData.originalMaterial:[mesh.userData.originalMaterial])){material.transparent=get('editor-transparent').checked;material.opacity=material.transparent?.38:1;material.depthWrite=!material.transparent;material.needsUpdate=true;}requestFrame();}
async function loadVersion(v){
  busy=true;stopTest();status('뼈대와 모델을 불러오는 중입니다.');
  try{
    const next=await api(`${config.skeleton_url}?v=${encodeURIComponent(v)}`);
    const loaded=await new GLTFLoader().loadAsync(next.model_url,progress=>{if(progress.total)status(`모델 불러오는 중 · ${Math.round(progress.loaded/progress.total*100)}%`);});
    disposeActor();data=next;version=next.version;currentVersion=next.current_version;draft={joints:clone(data.joints),groups:clone(data.groups||[])};
    actor=loaded.scene;scene.add(actor);actor.updateMatrixWorld(true);boneNodes.clear();originalWorld.clear();meshes.length=0;skeletons.clear();
    actor.traverse(o=>{if(o.isSkinnedMesh){o.material=Array.isArray(o.material)?o.material.map(m=>m.clone()):o.material.clone();o.userData.originalMaterial=o.material;o.userData.originalColor=o.geometry.getAttribute('color');o.userData.weightMaterial=new THREE.MeshStandardMaterial({vertexColors:true,roughness:1});meshes.push(o);skeletons.add(o.skeleton);}});
    const associations=loaded.parser.associations,actualBones=[...new Set([...skeletons].flatMap(s=>s.bones))];
    for(const bone of actualBones){const reference=associations.get(bone);if(reference?.nodes!==undefined)boneNodes.set(String(reference.nodes),bone);}
    for(const j of draft.joints){if(!boneNodes.has(j.id)){const bone=actualBones.find(b=>b.name===j.name||b.name===THREE.PropertyBinding.sanitizeNodeName(j.name));if(bone)boneNodes.set(j.id,bone);}if(!boneNodes.has(j.id))throw Error(`관절 ${j.name}을 모델에서 찾지 못했습니다.`);originalWorld.set(j.id,boneNodes.get(j.id).matrixWorld.clone());}
    selected=draft.joints[0]?.id||null;selectedGroup=draft.groups[0]?.id||'';original=JSON.stringify(draft);undo=[];redo=[];get('editor-weight-mode').value='keep';transparency();
    newCounter=Math.max(0,...draft.joints.map(j=>j.id.startsWith('new-')?Number(j.id.slice(4)):0));
    if(grid)scene.remove(grid);const height=Math.max(data.bounds[1][1]-data.bounds[0][1],.01);grid=new THREE.GridHelper(height*2,12,'#9a9f8f','#c8c8ba');grid.position.y=data.bounds[0][1]-.005;scene.add(grid);
    busy=false;sync();fit();await loadHistory();status(`뼈대 ${draft.joints.length}개를 불러왔습니다. 관절 위치와 그룹을 수정하세요.`);
  }catch(error){busy=false;status(error.message,true);sync();}
}
async function loadHistory(){
  const history=await api(config.versions_url);currentVersion=history.current_version;
  get('editor-version').replaceChildren(...history.versions.map((v,n)=>option(v.version,`${v.version===currentVersion?'현재 · ':''}${v.edited?'수정본':'AI 원본'} · ${v.bone_count}관절 · ${new Date(v.created_at*1000).toLocaleString('ko-KR')}`)));
  get('editor-version').value=version;
}
async function pollSave(pending){
  try{
    const rig=await api(config.status_url);status(rig.message||'저장 중입니다.');
    if(['queued','running'].includes(rig.status)){setTimeout(()=>pollSave(pending),3000);return;}
    if(rig.status==='failed')throw Error(rig.message||'수정본 저장에 실패했습니다.');
    if(rig.run_id!==pending)throw Error('저장 결과 버전이 달라졌습니다. 버전 기록에서 확인하세요.');
    dirty=false;await loadVersion(pending);get('editor-result').classList.remove('hidden');status('수정본을 저장했습니다. 동작 테스트 또는 저장된 동작 보기에서 확인하세요.');
  }catch(error){busy=false;status(error.message,true);sync();}
}
async function save(){
  if(busy||!dirty)return;stopTest();busy=true;sync();status('수정본을 대기열에 등록합니다.');
  try{const body=await api(config.save_url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({...draft,base_version:version,expected_version:currentVersion,weight_mode:get('editor-weight-mode').value})});pollSave(body.run_id);}
  catch(error){busy=false;status(error.message,true);sync();}
}
function nextGroup(){let key;do{key=`group-${++groupCounter}`;}while(draft.groups.some(g=>g.id===key));return key;}
get('editor-undo').addEventListener('click',()=>{if(!undo.length)return;stopTest();redo.push(clone(draft));draft=undo.pop();if(!joint(selected))selected=draft.joints[0].id;sync();status('이전 편집 단계로 되돌렸습니다.');});
get('editor-redo').addEventListener('click',()=>{if(!redo.length)return;stopTest();undo.push(clone(draft));draft=redo.pop();sync();status('편집을 다시 적용했습니다.');});
get('joint-name').addEventListener('change',()=>change(()=>{joint(selected).name=get('joint-name').value;}));
get('joint-parent').addEventListener('change',()=>change(()=>{joint(selected).parent=get('joint-parent').value||null;}));
['x','y','z'].forEach((axis,n)=>get(`joint-${axis}`).addEventListener('change',()=>{const value=Number(get(`joint-${axis}`).value);if(get(`joint-${axis}`).value===''||!Number.isFinite(value))return;change(()=>{joint(selected).position[n]=value;});}));
get('joint-group').addEventListener('change',()=>{const target=get('joint-group').value;change(()=>{const ids=get('group-children').checked?members(selected):[selected];for(const g of draft.groups)g.joints=g.joints.filter(id=>!ids.includes(id));const g=draft.groups.find(g=>g.id===target);if(g)g.joints.push(...ids);draft.groups=draft.groups.filter(g=>g.joints.length);selectedGroup=target;});});
get('joint-add').addEventListener('click',()=>change(()=>{const p=joint(selected),g=groupFor(p.id);let name;do{name=`joint_${++newCounter}`;}while(draft.joints.some(j=>j.name===name));const id=`new-${newCounter}`;draft.joints.push({id,name,parent:p.id,position:[p.position[0],p.position[1]+(data.bounds[1][1]-data.bounds[0][1])*.08,p.position[2]]});if(g)g.joints.push(id);selected=id;status('하위 관절을 추가했습니다. 위치를 조정한 뒤 AI로 저장하세요.');}));
get('joint-copy').addEventListener('click',()=>change(()=>{const branch=members(selected),mapping=new Map(branch.map(id=>[id,`new-${++newCounter}`])),offset=(data.bounds[1][1]-data.bounds[0][1])*.13;
  for(const id of branch){const j=joint(id);draft.joints.push({...clone(j),id:mapping.get(id),name:`${j.name}_copy${newCounter}`,parent:mapping.get(j.parent)||j.parent,position:[j.position[0],j.position[1]-offset,j.position[2]]});}
  const group=groupFor(selected);if(group){const key=nextGroup();draft.groups.push({...clone(group),id:key,name:`${group.name} 복사`,joints:branch.map(id=>mapping.get(id)),phase:(group.phase+.5)%1});selectedGroup=key;}
  selected=mapping.get(selected);status('관절 가지를 아래쪽에 복제했습니다. 축을 움직여 새 팔다리에 맞춘 뒤 AI로 저장하세요.');}));
get('joint-delete').addEventListener('click',()=>change(()=>{const j=joint(selected);if(!j.parent)return;draft.joints=draft.joints.filter(o=>o.id!==j.id);for(const o of draft.joints)if(o.parent===j.id)o.parent=j.parent;for(const g of draft.groups)g.joints=g.joints.filter(id=>id!==j.id);draft.groups=draft.groups.filter(g=>g.joints.length);selected=j.parent;}));
get('group-add').addEventListener('click',()=>change(()=>{const ids=members(selected),id=nextGroup();for(const g of draft.groups)g.joints=g.joints.filter(j=>!ids.includes(j));draft.groups=draft.groups.filter(g=>g.joints.length);draft.groups.push({id,name:`팔다리 ${draft.groups.length+1}`,type:'arm',joints:ids,amplitude:20,phase:0});selectedGroup=id;}));
get('group-select').addEventListener('change',()=>{stopTest();selectedGroup=get('group-select').value;renderGroups();});
for(const [field,key,multiplier] of [['group-name','name',null],['group-type','type',null],['group-amplitude','amplitude',1],['group-phase','phase',.01]])get(field).addEventListener('change',()=>change(()=>{const g=draft.groups.find(g=>g.id===selectedGroup);if(g)g[key]=multiplier===null?get(field).value:Number(get(field).value)*multiplier;}));
get('group-delete').addEventListener('click',()=>change(()=>{draft.groups=draft.groups.filter(g=>g.id!==selectedGroup);selectedGroup='';}));
get('editor-test').addEventListener('click',startTest);get('editor-test-group').addEventListener('change',()=>{if(testing)testStart=performance.now();});
get('editor-fit').addEventListener('click',()=>fit());get('editor-front').addEventListener('click',()=>fit(true));
get('editor-transparent').addEventListener('change',transparency);
get('editor-weights').addEventListener('change',weightView);get('editor-save').addEventListener('click',save);
get('version-load').addEventListener('click',()=>{if(dirty&&!window.confirm('저장하지 않은 수정을 버리고 선택한 버전을 불러올까요?'))return;loadVersion(get('editor-version').value);});
get('version-restore').addEventListener('click',async()=>{if(dirty&&!window.confirm('저장하지 않은 수정을 버리고 결과 버전을 복원할까요?'))return;try{await api(config.restore_url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({version:get('editor-version').value,expected_version:currentVersion})});dirty=false;await loadVersion(get('editor-version').value);status('결과의 활성 버전을 복원했습니다.');}catch(error){status(error.message,true);}});
window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
window.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='z'&&!['INPUT','SELECT','TEXTAREA'].includes(event.target.tagName)){event.preventDefault();get(event.shiftKey?'editor-redo':'editor-undo').click();}});
// Commit text/number edits as they are entered, including native stepper input.
for(const id of ['joint-name','joint-x','joint-y','joint-z','group-name','group-amplitude','group-phase'])get(id).addEventListener('input',()=>{committingField=id;get(id).dispatchEvent(new Event('change',{bubbles:true}));committingField=null;});

try{
  const host=get('editor-canvas');renderer=new THREE.WebGLRenderer({antialias:true});renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1;host.append(renderer.domElement);
  const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment();scene.environment=pmrem.fromScene(room,.04).texture;room.dispose();pmrem.dispose();
  scene.add(new THREE.HemisphereLight(0xffffff,0x9d9c86,2));const key=new THREE.DirectionalLight(0xffffff,2);key.position.set(2,3,4);scene.add(key);
  orbit=new OrbitControls(camera,renderer.domElement);orbit.enableDamping=true;orbit.addEventListener('change',requestFrame);
  transform=new TransformControls(camera,renderer.domElement);transform.setMode('translate');transform.setSpace('world');transform.setSize(.8);scene.add(transform.getHelper());
  transform.addEventListener('mouseDown',()=>{if(busy)return;stopTest();pushUndo();dragging=true;});
  transform.addEventListener('dragging-changed',event=>{orbit.enabled=!event.value;});
  transform.addEventListener('objectChange',()=>{if(!dragging||!selected)return;const object=transform.object;joint(selected).position=object.position.toArray();showMarkers();renderInspector();markDirty();get('editor-save').disabled=busy;requestFrame();});
  transform.addEventListener('mouseUp',()=>{dragging=false;sync();});
  let pointerStart;
  host.addEventListener('pointerdown',event=>{pointerStart=[event.clientX,event.clientY];});
  host.addEventListener('pointerup',event=>{if(dragging||!pointerStart||Math.hypot(event.clientX-pointerStart[0],event.clientY-pointerStart[1])>5)return;const r=host.getBoundingClientRect();const pointer=new THREE.Vector2((event.clientX-r.left)/r.width*2-1,-(event.clientY-r.top)/r.height*2+1);const ray=new THREE.Raycaster();ray.setFromCamera(pointer,camera);const hit=ray.intersectObjects([...markers.values()])[0];if(hit)selectJoint(hit.object.userData.jointId);});
  new ResizeObserver(()=>{const width=host.clientWidth,height=host.clientHeight;renderer.setSize(width,height,false);camera.aspect=width/height;camera.updateProjectionMatrix();requestFrame();}).observe(host);
  await loadVersion(config.version);
}catch(error){status(`3D 편집기를 준비하지 못했습니다: ${error.message}`,true);console.error(error);}
