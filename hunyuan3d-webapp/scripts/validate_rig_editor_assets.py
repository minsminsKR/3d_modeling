from pathlib import Path
import json,sys
import numpy as np
from scipy.spatial import cKDTree
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from rigging_editor import read_skeleton
from rigging_artifacts import read_glb,accessor
from rigging_motion import transforms
folder=root/'instance/outputs/DATA-20261011-A100-005-EDITOR-GPU'
checks=[]
for name in ['penguin','multi']:
    target=folder/name;expected=read_skeleton(target/'edited-input.glb');actual=read_skeleton(target/'rigged.glb')
    by_name={j['name']:j for j in actual['joints']};expected_name={j['id']:j['name'] for j in expected['joints']};actual_name={j['id']:j['name'] for j in actual['joints']}
    error=0
    for j in expected['joints']:
        a=by_name[j['name']];error=max(error,float(np.linalg.norm(np.array(j['position'])-a['position'])))
        assert (expected_name.get(j['parent'])==actual_name.get(a['parent']))
    assert error<2e-5
    source,raw=read_glb(target/'edited-input.glb');doc,binary=read_glb(target/'rigged.glb');world,_=transforms(doc)
    source_positions=[]
    for mesh in source['meshes']:
        for prim in mesh['primitives']:source_positions.append(accessor(source,raw,prim['attributes']['POSITION']))
    tree=cKDTree(np.concatenate(source_positions))
    max_distance=0
    for node in doc['nodes']:
        if 'skin' not in node:continue
        skin=doc['skins'][node['skin']];ibm=accessor(doc,binary,skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
        for prim in doc['meshes'][node['mesh']]['primitives']:
            a=prim['attributes'];p=accessor(doc,binary,a['POSITION'])[::max(1,doc['accessors'][a['POSITION']]['count']//4000)]
            max_distance=max(max_distance,float(tree.query(p)[0].max()))
            p=np.c_[p,np.ones(len(p))];j=accessor(doc,binary,a['JOINTS_0'])[::max(1,doc['accessors'][a['POSITION']]['count']//4000)];w=accessor(doc,binary,a['WEIGHTS_0'])[::max(1,doc['accessors'][a['POSITION']]['count']//4000)]
            matrices=np.array([world[k]@ibm[n] for n,k in enumerate(skin['joints'])]);posed=(np.einsum('nkij,nj->nki',matrices[j],p)*w[...,None]).sum(axis=1)
            assert np.max(np.abs(posed-p))<2e-5
    assert max_distance<2e-5
    checks.append({'case':name,'authored_joint_max_error':error,'geometry_sample_max_distance':max_distance,'neutral_pose_preserved':True})
doc,binary=read_glb(folder/'multi/rig-actions-v2.glb');rest,_=transforms(doc);skin=doc['skins'][0]
node=next(n for n in doc['nodes'] if 'skin' in n);prim=doc['meshes'][node['mesh']]['primitives'][0];a=prim['attributes']
p=accessor(doc,binary,a['POSITION']);p=np.c_[p,np.ones(len(p))];j=accessor(doc,binary,a['JOINTS_0']);w=accessor(doc,binary,a['WEIGHTS_0']);ibm=accessor(doc,binary,skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
def deform(world):
    m=np.array([world[k]@ibm[n] for n,k in enumerate(skin['joints'])]);return (np.einsum('nkij,nj->nki',m[j],p)*w[...,None]).sum(axis=1)[:,:3]
base=deform(rest);limbs=[]
for animation in doc['animations']:
    if not animation['name'].startswith('Limb_'):continue
    overrides={}
    for channel in animation['channels']:
        sampler=animation['samplers'][channel['sampler']];values=accessor(doc,binary,sampler['output']);t=channel['target']
        overrides.setdefault(t['node'],{})[t['path']]=values[len(values)//4].tolist()
    posed,_=transforms(doc,overrides);delta=float(np.linalg.norm(deform(posed)-base,axis=1).max());assert delta>.001
    limbs.append({'clip':animation['name'],'max_vertex_displacement':delta})
assert len(limbs)==6
file=root/'logs/rig-editor-gpu-validation.json';report=json.loads(file.read_text());report['exact_joint_and_mesh_checks']=checks;report['individual_limb_deformation_checks']=limbs
file.write_text(json.dumps(report,ensure_ascii=False,indent=2));print(json.dumps({'passed':True,'checks':checks,'limb_checks':limbs},ensure_ascii=False))
