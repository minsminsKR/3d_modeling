"""Real skin deformation and isolated Flask motion/download validation."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import time
import numpy as np
root = Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root))
from rigging_artifacts import read_glb, accessor, inspect_glb
from rigging_motion import transforms, MOTION_VERSION
job_id = '60d905f2-6aa9-43cf-8d8f-80f8fae302c8'
run_id = '74961c0a-b5d6-4d1e-925a-c7d537cb1fa8'
folder = root/'instance/outputs'/job_id/'rigging'/run_id
manifest = json.loads((folder/f'motion-{MOTION_VERSION}.json').read_text())
clean_doc, clean_raw = read_glb(folder/'rigged.glb')
full_doc, full_raw = read_glb(folder/manifest['model_file'])
assert full_raw[:len(clean_raw)] == clean_raw, 'Source geometry, weights or embedded images changed'
assert clean_doc.get('animations',[]) == [], 'Clean rig unexpectedly animated'
proxy_doc, proxy_binary = read_glb(folder/manifest['preview_file'])
for node in clean_doc['nodes']:
    if 'mesh' not in node: continue
    for prim in clean_doc['meshes'][node['mesh']]['primitives']:
        for index in [*prim['attributes'].values(),prim['indices']]:
            assert np.array_equal(accessor(clean_doc,clean_raw,index),accessor(proxy_doc,proxy_binary,index)), 'Preview changed mesh/UV/weights'
checks = []
for filename in [manifest['model_file'],manifest['preview_file']]:
    doc, binary = read_glb(folder/filename)
    info = inspect_glb(folder/filename)
    assert info['animations'] == ['Idle','Wave','Bow','Hop']
    rest, _ = transforms(doc)
    # Sample vertices influenced by lateral AI bones as well as body vertices.
    mesh_node = next(n for n in doc['nodes'] if 'skin' in n)
    skin = doc['skins'][mesh_node['skin']]
    prim = doc['meshes'][mesh_node['mesh']]['primitives'][0]
    a = prim['attributes']
    p = accessor(doc,binary,a['POSITION'])
    joint = accessor(doc,binary,a['JOINTS_0'])
    w = accessor(doc,binary,a['WEIGHTS_0'])
    samples = np.unique(np.r_[np.linspace(0,len(p)-1,3000,dtype=int), np.where((w[:,1:]>0.1).any(axis=1))[0][::30]])
    p = np.c_[p[samples],np.ones(len(samples))]; joint=joint[samples];w=w[samples]
    ibm = accessor(doc,binary,skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
    def deform(world):
        matrices = np.array([world[j] @ ibm[k] for k,j in enumerate(skin['joints'])])
        return (np.einsum('nkij,nj->nki',matrices[joint],p)*w[...,None]).sum(axis=1)[:,:3]
    base = deform(rest)
    for animation in doc['animations']:
        overrides = {}
        for channel in animation['channels']:
            sampler = animation['samplers'][channel['sampler']]
            times = accessor(doc,binary,sampler['input'])[:,0]
            values = accessor(doc,binary,sampler['output'])
            assert np.isfinite(values).all() and np.all(np.diff(times)>0)
            assert np.allclose(values[0],values[-1],atol=1e-6), 'Unstable loop seam'
            target = channel['target']; node=target['node']; path=target['path']
            assert node in skin['joints'] and 'matrix' not in doc['nodes'][node]
            if path=='rotation': assert np.allclose(np.linalg.norm(values,axis=1),1,atol=1e-5)
            overrides.setdefault(node,{})[path] = values[len(values)//2].tolist()
        posed, _ = transforms(doc,overrides)
        maximum = float(np.linalg.norm(deform(posed)-base,axis=1).max())
        # Idle has a neutral midpoint, so measure the quarter cycle as well.
        if maximum < 1e-4:
            for channel in animation['channels']:
                sampler=animation['samplers'][channel['sampler']]
                values=accessor(doc,binary,sampler['output']); target=channel['target']
                overrides[target['node']][target['path']]=values[len(values)//4].tolist()
            posed,_=transforms(doc,overrides)
            maximum=float(np.linalg.norm(deform(posed)-base,axis=1).max())
        assert maximum>1e-3,(filename,animation['name'],'No real deformation')
        if animation['name']=='Wave':
            roots = {j for j in skin['joints'] if j not in {c for k in skin['joints'] for c in doc['nodes'][k].get('children',[])}}
            root_only,_ = transforms(doc,{j:o for j,o in overrides.items() if j in roots})
            limb_delta = float(np.linalg.norm(deform(posed)-deform(root_only),axis=1).max())
            assert limb_delta>.01, 'Wave moved only the entire model'
        else: limb_delta = None
        checks.append({'file':filename,'clip':animation['name'],'max_vertex_displacement':round(maximum,6),'limb_only_displacement':limb_delta})
spec=importlib.util.spec_from_file_location('motion_api_validation',root/'app.py')
module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module
exec(compile((root/'app.py').read_text().replace('app = create_app()','app = None'),str(root/'app.py'),'exec'),module.__dict__)
data=root/'instance/DATA-20261011-A100-004-MOTION-API'
module.INSTANCE_DIR=data;module.UPLOAD_DIR=data/'uploads';module.OUTPUT_DIR=data/'outputs';module.JOBS_FILE=data/'jobs.json'
target=module.OUTPUT_DIR/job_id/'rigging'/run_id;target.mkdir(parents=True,exist_ok=True)
for name in ['rigged.glb','rig-preview.glb','rigging-result.json',manifest['model_file'],manifest['preview_file'],f'motion-{MOTION_VERSION}.json']:
    shutil.copy2(folder/name,target/name)
record=json.loads((folder/'rigging-result.json').read_text())
record.update(status='completed',model_path=str(target/'rigged.glb'),preview_path=str(target/'rig-preview.glb'))
module.JOBS_FILE.write_text(json.dumps({job_id:{'id':job_id,'status':'completed','created_at':time.time(),'message':'DATA validation','options':{'model':'trellis2','texture':True},'model_path':str(target/'rigged.glb'),'rigging':record}}))
class FakeExecutor:
    def __init__(self,**kwargs): pass
module.ThreadPoolExecutor=FakeExecutor;module.available_gpu_ids=lambda:[0,1];module.rigging_ready=lambda:True
app=module.create_app();app.template_folder=str(root/'templates');client=app.test_client()
status=client.get(f'/api/jobs/{job_id}/rigging').json
assert len(status['motion_clips'])==4 and status['preview_faces']==manifest['full_model']['faces']
assert client.get(f'/jobs/{job_id}').status_code==200
for url,file in [(status['preview_url'],manifest['preview_file']), (status['motion_download_url'],manifest['model_file']), (status['download_url'],'rigged.glb')]:
    response=client.get(url)
    assert response.status_code==200 and response.data==(target/file).read_bytes()
    if 'download' in url: assert 'attachment' in response.headers['Content-Disposition']
    assert client.get(url.split('?')[0]+'?v=../../other').status_code==404
assert client.get(f'/jobs/missing/rigging/motion-download').status_code==404
report={'change_id':'CHG-20261011-A100-004','data_id':'DATA-20261011-A100-004-MOTION','passed':True,
        'ai_inference_rerun':False,'procedural_action_presets':True,'source_binary_preserved':True,
        'clean_rig_sha256':hashlib.sha256((folder/'rigged.glb').read_bytes()).hexdigest(),
        'clips':manifest['clips'],'full_model':manifest['full_model'],'preview_model':manifest['preview_model'],
        'deformation_checks':checks,'api_download_content_checks':3,'version_path_checks':4,'jinja_page_status':200}
(root/'logs/motion-a100-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps(report,ensure_ascii=False))
