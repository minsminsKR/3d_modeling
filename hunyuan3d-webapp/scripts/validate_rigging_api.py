"""Exercise actual Flask rigging routes in a separate DATA store; no GPU work."""
from pathlib import Path
import importlib.util
import json
import shutil
import sys
import time

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root))
spec=importlib.util.spec_from_file_location('rig_api_validation',root/'app.py')
module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module
source=(root/'app.py').read_text()
exec(compile(source.replace('app = create_app()','app = None'),str(root/'app.py'),'exec'),module.__dict__)
directory=root/'instance/DATA-20261011-A100-003-RIG-API'
module.INSTANCE_DIR=directory;module.UPLOAD_DIR=directory/'uploads';module.OUTPUT_DIR=directory/'outputs';module.JOBS_FILE=directory/'jobs.json'
directory.mkdir(parents=True,exist_ok=True)
fixture=root/'vendor/SkinTokens/examples/giraffe.glb'
options={'model':'trellis2','texture':True,'gpu_id':1,'trellis_texture_size':4096}
jobs={key:{'id':key,'status':state,'message':'test','created_at':time.time(),'model_path':str(fixture),'options':options,'source_filename':'public-giraffe.glb'} for key,state in [('DATA-rig-a','completed'),('DATA-rig-b','completed'),('DATA-rig-incomplete','failed')]}
module.JOBS_FILE.write_text(json.dumps(jobs))
submitted=[]
class FakeExecutor:
    def __init__(self,**kwargs): pass
    def submit(self,*args): submitted.append(args)
module.ThreadPoolExecutor=FakeExecutor
module.available_gpu_ids=lambda:[0,1]
module.rigging_ready=lambda:False
app=module.create_app();app.template_folder=str(root/'templates');client=app.test_client()
results=[]
def check(name,path,status,data=None):
    response=client.post(path,json=data or {})
    assert response.status_code==status,(name,response.status_code,response.data)
    results.append({'name':name,'status':status})
    return response
check('not-ready','/api/jobs/DATA-rig-a/rigging',503)
module.rigging_ready=lambda:True
check('unknown-job','/api/jobs/missing/rigging',404)
check('incomplete-generation','/api/jobs/DATA-rig-incomplete/rigging',409)
check('invalid-mesh-budget','/api/jobs/DATA-rig-a/rigging',400,{'analysis_faces':999999})
check('invalid-preserve-type','/api/jobs/DATA-rig-a/rigging',400,{'preserve_original':'false'})
check('invalid-seed','/api/jobs/DATA-rig-a/rigging',400,{'seed':-1})
response=check('queue-default','/api/jobs/DATA-rig-a/rigging',202)
assert response.json['status']=='queued'
assert submitted[-1][-1]=={'seed':1234,'analysis_faces':50000,'preserve_original':True}
check('duplicate-blocked','/api/jobs/DATA-rig-a/rigging',409)
check('optimized-output-options','/api/jobs/DATA-rig-b/rigging',202,{'preserve_original':False,'analysis_faces':30000,'seed':42})
assert submitted[-1][-1]=={'seed':42,'analysis_faces':30000,'preserve_original':False}
status=client.get('/api/jobs/DATA-rig-a/rigging');assert status.status_code==200 and status.json['status']=='queued'
assert client.get('/jobs/DATA-rig-a/rigging/download').status_code==404
restored=module.load_jobs();assert restored['DATA-rig-a']['status']=='completed' and restored['DATA-rig-a']['rigging']['status']=='failed'
# Complete the captured default task using the verified real official GLB, while
# bypassing inference in the request harness.
artifact_dir=root/'instance/outputs/DATA-20261011-A100-003-RIG/official-giraffe'
actual=json.loads((artifact_dir/'rigging-result.json').read_text())
callback,job_id,src,target,settings=submitted[0]
target.mkdir(parents=True,exist_ok=True)
for name in ['rigged.glb','rig-preview.glb']:shutil.copy2(artifact_dir/name,target/name)
actual.update(model_path=str(target/'rigged.glb'),preview_path=str(target/'rig-preview.glb'))
(target/'rigging-result.json').write_text(json.dumps(actual))
module.run_rigging=lambda *args,**kwargs:actual
callback(job_id,src,target,settings)
status=client.get('/api/jobs/DATA-rig-a/rigging').json
assert status['status']=='completed' and status['bone_count']>1
assert status['model_url'].split('?')[0].endswith('/model') and status['preview_url'].split('?')[0].endswith('/preview')
for url in [status['model_url'],status['preview_url'],status['download_url']]: assert client.get(url).status_code==200
assert client.get(status['download_url']).data==(target/'rigged.glb').read_bytes()
assert client.get(status['download_url'].split('?')[0]+'?v=../../other').status_code==404
assert client.get('/jobs/DATA-rig-a').status_code==200
old_url=status['download_url']
check('rerun-preserves-previous-result','/api/jobs/DATA-rig-a/rigging',202,{'preserve_original':False,'analysis_faces':30000})
assert client.get('/api/jobs/DATA-rig-a/rigging').json['download_url']==old_url
callback,job_id,src,target,settings=submitted[-1]
target.mkdir(parents=True,exist_ok=True)
for name in ['rigged.glb','rig-preview.glb']:shutil.copy2(artifact_dir/name,target/name)
actual=dict(actual,model_path=str(target/'rigged.glb'),preview_path=str(target/'rig-preview.glb'))
(target/'rigging-result.json').write_text(json.dumps(actual))
callback(job_id,src,target,settings)
new_url=client.get('/api/jobs/DATA-rig-a/rigging').json['download_url']
assert old_url!=new_url and client.get(old_url).status_code==200 and client.get(new_url).status_code==200
results.append({'name':'rerun-versioned-urls-and-old-download-retained','passed':True})
results.extend([{'name':'status-and-restart-recovery','passed':True},{'name':'real-glb-download-and-preview','passed':True},{'name':'generation-result-preserved','passed':client.get('/api/jobs/DATA-rig-a').json['status']=='completed'}])
report={'change_id':'CHG-20261011-A100-003','data_id':'DATA-20261011-A100-003-RIG-API','passed':True,'executor_stubbed':True,'inference_executed':False,'cases':results}
(root/'logs/rigging-api-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps({'passed':True,'cases':len(results),'executor_submissions':len(submitted),'inference_executed':False}))
