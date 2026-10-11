"""Validated, versioned edits of TokenRig GLB skeletons, preserving mesh UVs."""
from pathlib import Path
import copy
import json
import re
import shutil
import time
import numpy as np
from rigging_artifacts import accessor, inspect_glb, read_glb
from rigging_motion import transforms, write_glb

MAX_JOINTS = 128


def read_skeleton(path):
    doc, binary = read_glb(path)
    if len(doc.get('skins',[])) != 1: raise ValueError('편집기는 단일 뼈대 GLB를 지원합니다.')
    skin = doc['skins'][0]; joints = skin['joints']
    world, parents = transforms(doc)
    positions = []
    for i,node in enumerate(doc['nodes']):
        if 'mesh' not in node: continue
        for prim in doc['meshes'][node['mesh']]['primitives']:
            p = accessor(doc,binary,prim['attributes']['POSITION'])
            positions.append(p @ world[i][:3,:3].T + world[i][:3,3])
    lo = np.min([p.min(axis=0) for p in positions],axis=0)
    hi = np.max([p.max(axis=0) for p in positions],axis=0)
    return {'joints':[{'id':str(j),'name':doc['nodes'][j].get('name',f'bone_{j}'),
        'parent':str(parents[j]) if parents.get(j) in joints else None,
        'position':world[j][:3,3].tolist()} for j in joints],
        'bounds':[lo.tolist(),hi.tolist()], 'max_joints':MAX_JOINTS}


def validate_edit(payload, skeleton):
    if not isinstance(payload,dict): raise ValueError('관절 편집 값이 올바르지 않습니다.')
    joints = payload.get('joints'); mode = payload.get('weight_mode','keep')
    if mode not in {'keep','ai'}: raise ValueError('가중치 계산 방식을 확인하세요.')
    if not isinstance(joints,list) or not 2<=len(joints)<=MAX_JOINTS: raise ValueError('관절은 2~128개로 구성하세요.')
    original = {j['id']:j for j in skeleton['joints']}
    lo,hi = np.array(skeleton['bounds']); center=(lo+hi)/2; extent=max(float(np.linalg.norm(hi-lo)),.01)
    cleaned=[]; ids=set(); names=set()
    for item in joints:
        if not isinstance(item,dict): raise ValueError('관절 데이터가 올바르지 않습니다.')
        key=item.get('id'); name=item.get('name'); pos=item.get('position'); parent=item.get('parent')
        if not isinstance(key,str) or not (key in original or re.fullmatch(r'new-[0-9]{1,8}',key)): raise ValueError('관절 ID가 올바르지 않습니다.')
        if key in ids or not isinstance(name,str) or not 1<=len(name.strip())<=64 or name.strip() in names: raise ValueError('관절 이름과 ID는 중복 없이 지정하세요.')
        if not isinstance(pos,list) or len(pos)!=3 or any(isinstance(v,bool) or not isinstance(v,(int,float)) for v in pos): raise ValueError('XYZ 좌표가 올바르지 않습니다.')
        p=np.array(pos,dtype=float)
        if not np.isfinite(p).all() or (np.abs(p-center)>extent*2).any(): raise ValueError('관절을 모델 주변 범위 안에 배치하세요.')
        if parent is not None and not isinstance(parent,str): raise ValueError('부모 관절을 확인하세요.')
        cleaned.append({'id':key,'name':name.strip(),'parent':parent,'position':p.tolist()});ids.add(key);names.add(name.strip())
    parents={j['id']:j['parent'] for j in cleaned}
    if sum(p is None for p in parents.values())!=1: raise ValueError('전체 뼈대에는 최상위 관절이 하나 필요합니다.')
    for key,p in parents.items():
        visited={key}
        while p is not None:
            if p not in ids or p in visited: raise ValueError('부모 연결이 끊겼거나 순환합니다.')
            visited.add(p);p=parents[p]
    structural = ids!=set(original) or any(j['id'] in original and j['parent']!=original[j['id']]['parent'] for j in cleaned)
    if mode=='keep' and structural: raise ValueError('관절 추가·삭제·부모 변경 후에는 AI 가중치 재계산을 선택하세요.')
    groups=payload.get('groups',[])
    if not isinstance(groups,list) or len(groups)>32: raise ValueError('팔다리 그룹은 최대 32개입니다.')
    clean_groups=[]; used=set(); group_ids=set()
    for g in groups:
        if not isinstance(g,dict): raise ValueError('그룹 설정이 올바르지 않습니다.')
        key=g.get('id'); name=g.get('name'); members=g.get('joints',[]); kind=g.get('type','arm')
        if not isinstance(key,str) or not re.fullmatch(r'[a-zA-Z0-9_-]{1,40}',key) or key in group_ids: raise ValueError('그룹 ID가 올바르지 않습니다.')
        if not isinstance(name,str) or not 1<=len(name.strip())<=40 or kind not in {'arm','leg','head','other'}: raise ValueError('그룹 이름·종류를 확인하세요.')
        if not isinstance(members,list) or not members or any(not isinstance(j,str) or j not in ids for j in members) or len(set(members))!=len(members) or set(members)&used: raise ValueError('각 관절은 한 그룹에만 지정하세요.')
        amplitude=g.get('amplitude',20);phase=g.get('phase',0)
        if isinstance(amplitude,bool) or isinstance(phase,bool) or not isinstance(amplitude,(float,int)) or not isinstance(phase,(float,int)) or not np.isfinite([amplitude,phase]).all() or not 0<=amplitude<=60 or not 0<=phase<=1: raise ValueError('그룹 각도는 0~60도, 타이밍은 0~100%로 지정하세요.')
        clean_groups.append({'id':key,'name':name.strip(),'type':kind,'joints':members,'amplitude':float(amplitude),'phase':float(phase)})
        used.update(members);group_ids.add(key)
    return {'joints':cleaned,'groups':clean_groups,'weight_mode':mode,'structural_edit':structural}


def apply_edit(source, output, edit):
    from scipy.spatial.transform import Rotation
    doc, raw=read_glb(source); world, original_parent=transforms(doc)
    skin=doc['skins'][0]; old_joints=list(skin['joints']);old_set=set(old_joints)
    root=next(j for j in old_joints if original_parent.get(j) not in old_set)
    container=original_parent.get(root)
    binary=bytearray(raw);node_ids={}
    desired={};edited={j['id']:j for j in edit['joints']}
    for j in edit['joints']:
        if j['id'].startswith('new-'):
            node_ids[j['id']]=len(doc['nodes']);doc['nodes'].append({});m=np.eye(4)
        else:
            node_ids[j['id']]=int(j['id']);m=world[int(j['id'])].copy()
        m[:3,3]=j['position'];desired[j['id']]=m
    for node in doc['nodes']:
        if 'children' in node: node['children']=[c for c in node['children'] if c not in old_set]
    for j in edit['joints']:
        parent_matrix=desired[j['parent']] if j['parent'] else (world[container] if container is not None else np.eye(4))
        local=np.linalg.solve(parent_matrix,desired[j['id']]);scale=np.linalg.norm(local[:3,:3],axis=0)
        rotation=local[:3,:3]/scale
        if np.linalg.det(rotation)<0: scale[0]*=-1;rotation[:,0]*=-1
        node=doc['nodes'][node_ids[j['id']]]
        node.pop('matrix',None);node.update(name=j['name'],translation=local[:3,3].tolist(),rotation=Rotation.from_matrix(rotation).as_quat().tolist(),scale=scale.tolist())
        parent_id=node_ids[j['parent']] if j['parent'] else container
        if parent_id is not None: doc['nodes'][parent_id].setdefault('children',[]).append(node_ids[j['id']])
        elif node_ids[j['id']] not in doc['scenes'][doc.get('scene',0)]['nodes']:
            doc['scenes'][doc.get('scene',0)]['nodes'].append(node_ids[j['id']])
    def add(data,kind,component=5126):
        values=np.asarray(data,dtype='<f4' if component==5126 else '<u2')
        binary.extend(b'\0'*(-len(binary)%4));start=len(binary);packed=values.tobytes();binary.extend(packed)
        doc['bufferViews'].append({'buffer':0,'byteOffset':start,'byteLength':len(packed)})
        doc['accessors'].append({'bufferView':len(doc['bufferViews'])-1,'componentType':component,'count':len(values),'type':kind})
        return len(doc['accessors'])-1
    new_joints=[node_ids[j['id']] for j in edit['joints']];new_index={j['id']:i for i,j in enumerate(edit['joints'])}
    mesh_ids=[i for i,n in enumerate(doc['nodes']) if n.get('skin')==0]
    mesh_matrix=world[mesh_ids[0]]
    if any(not np.allclose(world[i],mesh_matrix) for i in mesh_ids): raise ValueError('서로 다른 메시 변환을 가진 뼈대는 지원하지 않습니다.')
    skin['joints']=new_joints;skin['skeleton']=node_ids[next(j['id'] for j in edit['joints'] if j['parent'] is None)]
    skin['inverseBindMatrices']=add([np.linalg.solve(desired[j['id']],mesh_matrix).T.reshape(-1) for j in edit['joints']],'MAT4')
    # The provisional skeleton input remaps removed bones to retained ancestors;
    # newly added bones receive their real weights only through AI re-skinning.
    if new_joints!=old_joints:
        mapping=[]
        new_root=next(j['id'] for j in edit['joints'] if j['parent'] is None)
        for j in old_joints:
            while str(j) not in new_index and j in original_parent: j=original_parent[j]
            mapping.append(new_index.get(str(j),new_index[new_root]))
        for i in mesh_ids:
            for prim in doc['meshes'][doc['nodes'][i]['mesh']]['primitives']:
                attrs=prim['attributes'];idx=accessor(doc,raw,attrs['JOINTS_0'])
                attrs['JOINTS_0']=add(np.array(mapping,dtype=np.uint16)[idx],'VEC4',5123)
    doc['animations']=[]
    doc.setdefault('asset',{}).setdefault('extras',{})['a100_rig_editor']={'version':1,'weight_mode':edit['weight_mode']}
    write_glb(doc,binary,output)
    groups=[dict(g,joints=[str(node_ids[j]) for j in g['joints']]) for g in edit['groups']]
    state=read_skeleton(output);state.update(groups=groups,weight_mode=edit['weight_mode'],edited_at=time.time())
    return state


def restore_ai_edit(source, result):
    """Map quantized predicted bones back to exact user coordinates and labels."""
    expected=read_skeleton(source);predicted=read_skeleton(result)
    if len(expected['joints'])!=len(predicted['joints']): raise ValueError('AI 재계산에서 관절 수가 달라졌습니다. 이전 결과는 유지됩니다.')
    # TokenRig/Blender can export a uniform pose scale after normalization.
    # Match the known ordered hierarchy in a common coordinate frame, check
    # quantization residual, then restore exact authored world coordinates.
    def ordered(skeleton):
        out=[]
        def visit(parent):
            for j in skeleton['joints']:
                if j['parent']==parent: out.append(j);visit(j['id'])
        visit(None);return out
    ej,pj=ordered(expected),ordered(predicted)
    ei={j['id']:n for n,j in enumerate(ej)};pi={j['id']:n for n,j in enumerate(pj)}
    if [ei.get(j['parent'],-1) for j in ej]!=[pi.get(j['parent'],-1) for j in pj]: raise ValueError('AI가 수정한 부모 연결을 유지하지 못했습니다.')
    a=np.array([j['position'] for j in ej]);b=np.array([j['position'] for j in pj])
    ac,bc=a-a.mean(axis=0),b-b.mean(axis=0)
    u,s,vt=np.linalg.svd(bc.T@ac)
    correction=np.eye(3);correction[-1,-1]=1 if np.linalg.det(vt.T@u.T)>0 else -1
    rotation=vt.T@correction@u.T
    variance=float((bc*bc).sum())
    if variance<1e-12: raise ValueError('겹쳐 있는 관절을 분리한 뒤 AI로 다시 계산하세요.')
    scale=float((s*np.diag(correction)).sum()/variance)
    aligned=scale*(bc@rotation.T)+a.mean(axis=0)
    extent=max(float(np.linalg.norm(np.array(expected['bounds'][1])-expected['bounds'][0])),.01)
    if np.max(np.linalg.norm(a-aligned,axis=1))>extent*.04: raise ValueError('AI가 수정 관절을 충분히 유지하지 못했습니다. 이전 결과는 유지됩니다.')
    mapping={e['id']:p['id'] for e,p in zip(ej,pj)}
    pred={j['id']:j for j in predicted['joints']}
    for j in expected['joints']:
        parent=mapping[j['parent']] if j['parent'] else None
        if pred[mapping[j['id']]]['parent']!=parent: raise ValueError('AI가 수정한 부모 연결을 유지하지 못했습니다.')
    saved=json.loads(Path(source).with_name('editor-state.json').read_text())
    edits={'joints':[dict(j,id=mapping[j['id']],parent=mapping[j['parent']] if j['parent'] else None) for j in expected['joints']],
           'groups':[dict(g,joints=[mapping[j] for j in g['joints']]) for g in saved['groups']], 'weight_mode':'ai'}
    exact=Path(result).with_name('rigged-exact.glb')
    state=apply_edit(result,exact,edits)
    shutil.copy2(result,Path(result).with_name('ai-quantized.glb'));exact.replace(result)
    Path(result).with_name('editor-state.json').write_text(json.dumps(state,ensure_ascii=False,indent=2))
    return state
