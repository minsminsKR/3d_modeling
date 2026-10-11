from pathlib import Path
import json
import sys
import threading
import time
from dotenv import load_dotenv

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root))
load_dotenv(root/'.env',override=True)
from rigging_runtime import run_rigging
from gpu_runtime import gpu_stats

cases=[('official-giraffe',root/'vendor/SkinTokens/examples/giraffe.glb')]
if '--generated' in sys.argv:
    jobs=json.loads((root/'instance/jobs.json').read_text())
    cases=[('generated-trellis2'+('-optimized' if '--optimized' in sys.argv else ''),Path(jobs['60d905f2-6aa9-43cf-8d8f-80f8fae302c8']['model_path']))]
for name,source in cases:
    directory=root/'instance/outputs/DATA-20261011-A100-003-RIG'/name
    samples=[];stop=threading.Event()
    def monitor():
        while not stop.is_set():
            samples.append({'time':time.time(),'gpus':gpu_stats()})
            stop.wait(3)
    thread=threading.Thread(target=monitor,daemon=True);thread.start()
    try:
        result=run_rigging(source,directory,{'analysis_faces':30000 if '--optimized' in sys.argv else 50000,'preserve_original':'--optimized' not in sys.argv,'seed':1234},lambda message:print(message,flush=True))
        if '--optimized' in sys.argv: assert result['faces']<=30000 and result['faces']<result['source']['faces']
        report={'name':name,'passed':True,'result':result,'gpu_samples':samples,'peak_used_mib':{str(i):max((gpu['used_mib'] for sample in samples for gpu in sample['gpus'] if gpu['id']==i),default=0) for i in [0,1]}}
        (root/'logs'/('rigging-validation-'+name+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2))
        print(json.dumps({'name':name,'passed':True,'bones':result['bone_count'],'vertices':result['skinned_vertices'],'seconds':result['elapsed_seconds'],'peak_used_mib':report['peak_used_mib']}),flush=True)
    finally:
        stop.set();thread.join(timeout=5)
