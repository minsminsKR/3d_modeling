import json,time,sys
from pathlib import Path
import requests,trimesh
p=Path(__file__).resolve().parents[1]
base='http://127.0.0.1:5001'
assert requests.get(base,timeout=30).status_code==200
report_path=p/'logs/smoke-a100.json'
report=json.loads(report_path.read_text()) if report_path.exists() else {'data_id':'DATA-20261010-A100-001','tests':[]}
for model,texture in [('hunyuan3d-2.1',False),('hunyuan3d-2.1',True),('hunyuan3d-omni',False)]:
    stage='omni' if model=='hunyuan3d-omni' else ('texture' if texture else 'shape')
    if len(sys.argv)>1 and stage not in sys.argv[1:]: continue
    if any(x.get('model')==model and x.get('texture_requested')==texture and x.get('status')=='completed' and (not texture or x.get('used_texture')) for x in report['tests']): continue
    with (p/'vendor/Hunyuan3D-2.1/assets/demo.png').open('rb') as f:
        form={'model':model,'gpu_id':'1','num_inference_steps':'5','octree_resolution':'128','num_chunks':'8000','seed':'1234','guidance_scale':'7.5','remove_background':'on'}
        if texture: form['texture']='on'
        response=requests.post(base+'/jobs',files={'images':('DATA-20261010-A100-001-demo.png',f,'image/png')},data=form,allow_redirects=False,timeout=60)
    response.raise_for_status()
    job_id=response.headers['Location'].rsplit('/',1)[-1]
    print('TEST_JOB',model,texture,job_id,flush=True)
    item={'model':model,'texture_requested':texture,'job_id':job_id}
    report['tests'].append(item)
    report_path=p/'logs/smoke-a100.json'
    report_path.write_text(json.dumps(report,indent=2))
    deadline=time.monotonic()+2400
    while time.monotonic()<deadline:
        result=requests.get(base+'/api/jobs/'+job_id,timeout=30).json()
        print(result.get('status'),result.get('message'),flush=True)
        if result['status'] in ['completed','failed']:break
        time.sleep(15)
    item['status']=result['status']
    item['message']=result.get('message')
    item['used_texture']=result.get('used_texture')
    item['warnings']=result.get('warnings')
    if result['status']=='completed':
        dest=p/'instance/outputs'/job_id/'model.glb'
        mesh=trimesh.load(dest,force='scene')
        item['glb_bytes']=dest.stat().st_size
        item['geometry_count']=len(mesh.geometry)
        assert item['geometry_count']>0
        r=requests.get(base+'/jobs/'+job_id+'/model',timeout=60)
        item['download_http']=r.status_code
        assert r.status_code==200 and r.content[:4]==b'glTF'
    report_path.write_text(json.dumps(report,indent=2))
    if result['status']!='completed' or (texture and not result.get('used_texture')):
        raise RuntimeError('Smoke test failed: '+json.dumps(item))
print('SMOKE_COMPLETE',flush=True)
