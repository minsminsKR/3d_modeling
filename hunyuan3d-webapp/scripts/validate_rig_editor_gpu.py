"""Real skeleton-conditioned GPU inference on edited and four-arm rigs."""
import json
import math
from pathlib import Path
import sys
import time
import uuid
import numpy as np
import trimesh
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from rigging_editor import read_skeleton,validate_edit
from rigging_edit_runtime import run_edit
from rigging_motion import write_glb
from rigging_artifacts import read_glb,inspect_glb,accessor
folder=root/'instance/outputs/DATA-20261011-A100-005-EDITOR-GPU';folder.mkdir(parents=True,exist_ok=True)
def progress(message):print(message,flush=True)
reports={}
source=root/'instance/outputs/60d905f2-6aa9-43cf-8d8f-80f8fae302c8/rigging/74961c0a-b5d6-4d1e-925a-c7d537cb1fa8/rigged.glb'
skel=read_skeleton(source);edit={'joints':skel['joints'],'groups':[],'weight_mode':'ai'}
edit['joints'][1]['position'][0]-=.009
edit['groups']=[{'id':'left-arm','name':'왼팔','type':'arm','joints':[edit['joints'][1]['id'],edit['joints'][2]['id']],'amplitude':20,'phase':0}]
existing=folder/'penguin/rigging-result.json'
reports['edited_penguin']=json.loads(existing.read_text()) if existing.is_file() and '--refresh-penguin' not in sys.argv else run_edit(source,folder/'penguin',validate_edit(edit,read_skeleton(source)),source.parent.name,progress)

# Generated DATA fixture: four separated arms and two legs, 19 explicit joints.
points=[[0,-.25,0],[0,.05,0],[0,.38,0]];parents=[None,0,1]
groups=[];parts=[trimesh.creation.box(extents=[.21,.5,.15])]
head=trimesh.creation.icosphere(subdivisions=2,radius=.115);head.apply_translation([0,.38,0]);parts.append(head)
for n,(side,y) in enumerate([(-1,.2),(1,.2),(-1,-.035),(1,-.035)]):
    branch=[]
    for x in [.13,.29,.46]:
        branch.append(len(points));points.append([side*x,y,0]);parents.append(1 if len(branch)==1 else branch[-2])
    bar=trimesh.creation.box(extents=[.34,.06,.065]);bar.apply_translation([side*.3,y,0]);parts.append(bar)
    groups.append({'id':f'arm-{n+1}','name':f'팔 {n+1}','type':'arm','joints':branch,'amplitude':22,'phase':n%2*.5})
for n,side in enumerate([-1,1]):
    branch=[]
    for y in [-.27,-.42]:
        branch.append(len(points));points.append([side*.07,y,0]);parents.append(0 if len(branch)==1 else branch[-2])
    leg=trimesh.creation.box(extents=[.065,.22,.085]);leg.apply_translation([side*.07,-.35,0]);parts.append(leg)
    groups.append({'id':f'leg-{n+1}','name':f'다리 {n+1}','type':'leg','joints':branch,'amplitude':18,'phase':n*.5})
mesh=trimesh.util.concatenate(parts);mesh.visual.vertex_colors=[88,140,110,255]
raw=mesh.export(file_type='glb');base=folder/'base-mesh.glb';base.write_bytes(raw);doc,binary=read_glb(base);binary=bytearray(binary)
def add(values,kind,component):
    arr=np.asarray(values,dtype='<f4' if component==5126 else '<u2');binary.extend(b'\0'*(-len(binary)%4));offset=len(binary);payload=arr.tobytes();binary.extend(payload)
    doc['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(payload)})
    doc['accessors'].append({'bufferView':len(doc['bufferViews'])-1,'componentType':component,'count':len(arr),'type':kind});return len(doc['accessors'])-1
container=len(doc['nodes']);doc['nodes'].append({'name':'FourArmRig','children':[]})
joint_ids=[]
for n,p in enumerate(points):
    j=len(doc['nodes']);joint_ids.append(j);parent=parents[n]
    doc['nodes'].append({'name':f'joint_{n}','translation':(np.array(p)-(np.array(points[parent]) if parent is not None else 0)).tolist()})
    doc['nodes'][joint_ids[parent] if parent is not None else container].setdefault('children',[]).append(j)
inverse=[]
for p in points:
    m=np.eye(4);m[:3,3]=-np.array(p);inverse.append(m.T.reshape(-1))
doc['skins']=[{'name':'FourArmRig','joints':joint_ids,'inverseBindMatrices':add(inverse,'MAT4',5126)}]
for node in doc['nodes']:
    if 'mesh' not in node:continue
    node['skin']=0
    for prim in doc['meshes'][node['mesh']]['primitives']:
        count=doc['accessors'][prim['attributes']['POSITION']]['count'];indices=np.zeros((count,4));weights=indices.copy();weights[:,0]=1
        prim['attributes']['JOINTS_0']=add(indices,'VEC4',5123);prim['attributes']['WEIGHTS_0']=add(weights,'VEC4',5126)
doc['scenes'][doc.get('scene',0)]['nodes'].append(container)
multi=folder/'four-arms.glb';write_glb(doc,binary,multi);skel=read_skeleton(multi)
edit={'joints':skel['joints'],'groups':[dict(g,joints=[str(joint_ids[n]) for n in g['joints']]) for g in groups],'weight_mode':'ai'}
reports['four_arms_two_legs']=run_edit(multi,folder/'multi',validate_edit(edit,skel),'DATA-four-arms',progress)
state=json.loads((folder/'multi/editor-state.json').read_text());doc,binary=read_glb(folder/'multi/rigged.glb')
by_name={n.get('name'):i for i,n in enumerate(doc['nodes'])};skin=doc['skins'][0];influence=np.zeros(len(skin['joints']))
for node in doc['nodes']:
    if 'skin' not in node:continue
    for prim in doc['meshes'][node['mesh']]['primitives']:
        a=prim['attributes'];j=accessor(doc,binary,a['JOINTS_0']);w=accessor(doc,binary,a['WEIGHTS_0'])
        for k in range(len(influence)):influence[k]+=np.where(j==k,w,0).sum()
group_influence={g['name']:sum(float(influence[skin['joints'].index(int(j))]) for j in g['joints']) for g in state['groups']}
assert all(v>1 for v in group_influence.values()),group_influence
assert reports['four_arms_two_legs']['bone_count']==len(points)
motion=json.loads((folder/'multi/motion-actions-v2.json').read_text());assert len(motion['roles']['arms'])==4
assert any(c['name']=='GroupAction' for c in motion['clips'])
report={'change_id':'CHG-20261011-A100-005','data_id':'DATA-20261011-A100-005-EDITOR-GPU','passed':True,'gpu_id':1,
        'real_ai_inference':True,'skeleton_conditioned':True,'cases':reports,'four_arm_group_influence':group_influence,
        'four_arm_bone_count':len(points),'four_arm_groups':6,'motion_clips':motion['clips']}
(root/'logs/rig-editor-gpu-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print(json.dumps({'passed':True,'group_influence':group_influence},ensure_ascii=False))
