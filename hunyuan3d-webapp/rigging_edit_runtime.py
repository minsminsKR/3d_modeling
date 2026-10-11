from pathlib import Path
import json
import os
import time
from rigging_runtime import run_logged,run_rigging,BASE_DIR

def run_edit(source,directory,edit,base_version,progress):
    folder=Path(directory);folder.mkdir(parents=True,exist_ok=True)
    request=folder/'editor-request.json'
    request.write_text(json.dumps({'source':str(source),'output_dir':str(folder),'edit':edit,'base_version':base_version},ensure_ascii=False,indent=2))
    env=os.environ.copy();env.update(CUDA_VISIBLE_DEVICES='',PYTHONUNBUFFERED='1',OMP_NUM_THREADS='8')
    started=time.time();progress('수정 뼈대를 별도 버전으로 준비 중입니다.')
    with (folder/'editor-worker.log').open('w') as log:
        run_logged([str(BASE_DIR/'.venv-rig/bin/python'),str(BASE_DIR/'scripts/rigging_edit_worker.py'),str(request)],env,BASE_DIR,log,progress,600)
    if edit['weight_mode']=='ai':
        progress('수정 뼈대를 유지하며 AI 관절 가중치를 다시 계산합니다.')
        info=run_rigging(folder/'edited-input.glb',folder,{'seed':1234,'analysis_faces':50000,'preserve_original':True,'use_skeleton':True,'editor':True},progress)
    else: info=json.loads((folder/'rigging-result.json').read_text())
    info.update(elapsed_seconds=int(time.time()-started),base_version=base_version,edited=True,weight_mode=edit['weight_mode'])
    (folder/'rigging-result.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
    return info
