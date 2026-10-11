import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import time
import uuid
import numpy as np
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from rigging_editor import read_skeleton,validate_edit,apply_edit
from rigging_artifacts import read_glb,accessor,inspect_glb
from rigging_motion import transforms
from rigging_edit_runtime import run_edit
job='60d905f2-6aa9-43cf-8d8f-80f8fae302c8';original_version='74961c0a-b5d6-4d1e-925a-c7d537cb1fa8'
source=root/'instance/outputs'/job/'rigging'/original_version/'rigged.glb'
data=root/'instance/DATA-20261011-A100-005-EDITOR';data.mkdir(parents=True,exist_ok=True)
before=hashlib.sha256(source.read_bytes()).hexdigest();skeleton=read_skeleton(source)
edit=dict(joints=copy.deepcopy(skeleton['joints']),groups=[],weight_mode='keep')
edit['joints'][1]['position'][0]+=.012
edit['groups']=[{'id':'arm-test','name':'검증 팔','type':'arm','joints':[edit['joints'][1]['id'],edit['joints'][2]['id']],'amplitude':22,'phase':.5}]
cleaned=validate_edit(edit,skeleton)
output=data/'edited.glb';state=apply_edit(source,output,cleaned);info=inspect_glb(output)
assert info['faces']==955709 and info['bone_count']==10
doc,raw=read_glb(source);new,binary=read_glb(output)
assert raw==binary[:len(raw)]
# Rebinding must preserve the neutral model even though its pivots moved.
world,_=transforms(new)
skin=new['skins'][0];ibm=accessor(new,binary,skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
for node in new['nodes']:
    if 'skin' not in node:continue
    for prim in new['meshes'][node['mesh']]['primitives']:
        a=prim['attributes'];p=accessor(new,binary,a['POSITION'])[::137];p=np.c_[p,np.ones(len(p))]
        indices=accessor(new,binary,a['JOINTS_0'])[::137];weights=accessor(new,binary,a['WEIGHTS_0'])[::137]
        matrices=np.array([world[j]@ibm[n] for n,j in enumerate(skin['joints'])])
        actual=(np.einsum('nkij,nj->nki',matrices[indices],p)*weights[...,None]).sum(axis=1)
        assert np.max(np.abs(actual-p))<2e-5
invalid=[]
def reject(name,modify):
    bad=copy.deepcopy(edit);modify(bad)
    try:validate_edit(bad,skeleton)
    except ValueError:invalid.append(name)
    else:raise AssertionError(name)
reject('cycle',lambda b:b['joints'][1].update(parent=b['joints'][2]['id']))
reject('duplicate-name',lambda b:b['joints'][1].update(name=b['joints'][0]['name']))
reject('nan-position',lambda b:b['joints'][1]['position'].__setitem__(0,float('nan')))
reject('outside-model',lambda b:b['joints'][1]['position'].__setitem__(0,1000))
reject('unknown-parent',lambda b:b['joints'][1].update(parent='missing'))
reject('keep-with-deletion',lambda b:b['joints'].pop())
reject('duplicate-group-joint',lambda b:b['groups'].append(copy.deepcopy(b['groups'][0])|{'id':'second'}))
reject('group-invalid-member',lambda b:b['groups'][0]['joints'].append({}))
reject('group-phase',lambda b:b['groups'][0].update(phase=2))
reject('multiple-roots',lambda b:b['joints'][1].update(parent=None))
spec=importlib.util.spec_from_file_location('editor_api_validation',root/'app.py');module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module
exec(compile((root/'app.py').read_text().replace('app = create_app()','app = None'),str(root/'app.py'),'exec'),module.__dict__)
store=data/'api'/str(uuid.uuid4());module.INSTANCE_DIR=store;module.UPLOAD_DIR=store/'uploads';module.OUTPUT_DIR=store/'outputs';module.JOBS_FILE=store/'jobs.json'
folder=module.OUTPUT_DIR/job/'rigging'/original_version;folder.mkdir(parents=True,exist_ok=True)
for name in ['rigged.glb','rig-preview.glb','rigging-result.json','motion-actions-v2.json']:shutil.copy2(source.parent/name,folder/name)
record=json.loads((folder/'rigging-result.json').read_text());record.update(status='completed',model_path=str(folder/'rigged.glb'),preview_path=str(folder/'rig-preview.glb'),run_id=original_version)
module.JOBS_FILE.write_text(json.dumps({job:{'id':job,'created_at':time.time(),'status':'completed','message':'DATA editor test','options':{'model':'trellis2','texture':True},'model_path':str(folder/'rigged.glb'),'rigging':record}}))
calls=[]
class Executor:
    def __init__(self,**kwargs):pass
    def submit(self,*args):calls.append(args)
module.ThreadPoolExecutor=Executor;module.available_gpu_ids=lambda:[0,1];module.rigging_ready=lambda:True
app=module.create_app();app.template_folder=str(root/'templates');client=app.test_client();base=f'/api/jobs/{job}/rigging'
assert client.get(f'/jobs/{job}/rigging/edit').status_code==200
assert client.get(base+'/skeleton').json['version']==original_version
assert client.get(base+'/skeleton?v=../../other').status_code==404
payload=dict(edit,base_version=original_version,expected_version=original_version)
assert client.post(base+'/edit',json=dict(payload,expected_version=str(uuid.uuid4()))).status_code==409
response=client.post(base+'/edit',json=payload);assert response.status_code==202
assert client.post(base+'/edit',json=payload).status_code==409
# Execute the captured real CPU save worker, not a fake result.
callback,jid,src,target,options,version=calls[-1];callback(jid,src,target,options,version)
rig=client.get(base).json;assert rig['status']=='completed' and rig['edited'] and rig['weight_mode']=='keep'
assert len(rig['motion_clips'])==6
assert client.get(rig['motion_download_url']).status_code==200
history=client.get(base+'/versions').json;assert len(history['versions'])==2
assert client.post(base+'/restore',json={'version':original_version,'expected_version':original_version}).status_code==409
assert client.post(base+'/restore',json={'version':original_version,'expected_version':target.name}).status_code==200
assert client.get(base).json['download_url'].endswith(original_version)
assert client.get(rig['download_url']).status_code==200
assert hashlib.sha256(source.read_bytes()).hexdigest()==before
report={'change_id':'CHG-20261011-A100-005','data_id':'DATA-20261011-A100-005-EDITOR','passed':True,'invalid_cases':invalid,
        'neutral_rebind_preserved':True,'source_binary_preserved':True,'cpu_worker_executed':True,'original_sha256':before,
        'isolated_api_save_version':target.name,'history_and_restore':True,'versioned_downloads':True}
(root/'logs/rig-editor-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print(json.dumps(report,ensure_ascii=False))
