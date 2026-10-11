"""One isolated GPU worker plus a private, loopback-only Blender helper."""
from pathlib import Path
import json
import os
import random
import socket
import subprocess
import sys
import time
from rigging_process_guard import install_process_guard

install_process_guard()

request = json.loads(Path(sys.argv[1]).read_text())
root = Path(__file__).resolve().parents[1]
repo = root/'vendor/SkinTokens'
os.chdir(repo)
sys.path.insert(0,str(repo))
sys.path.insert(0,str(root))
with socket.socket() as sock:
    sock.bind(('127.0.0.1',0))
    port = sock.getsockname()[1]
os.environ['TOKENRIG_BPY_PORT'] = str(port)
os.environ['HF_HUB_OFFLINE'] = '1'
os.environ['TRANSFORMERS_OFFLINE'] = '1'

def progress(message):
    print('A100_RIG_EVENT '+json.dumps({'message':message},ensure_ascii=False),flush=True)

progress('리깅 모델을 불러오는 중입니다.')
import numpy as np
import torch
import requests
import demo
from rigging_artifacts import inspect_glb,write_joint_preview

random.seed(request['seed']);np.random.seed(request['seed']);torch.manual_seed(request['seed'])
torch.set_num_threads(8)
server_env = os.environ.copy()
server_env['CUDA_VISIBLE_DEVICES'] = ''
server = subprocess.Popen([sys.executable,str(repo/'bpy_server.py')],cwd=repo,env=server_env)
try:
    deadline = time.monotonic()+90
    while True:
        if server.poll() is not None: raise RuntimeError('Blender 보조 프로세스가 종료됐습니다.')
        try:
            response = requests.get(f'http://127.0.0.1:{port}/ping',timeout=1)
            if response.status_code==200: break
        except requests.RequestException: pass
        if time.monotonic()>deadline: raise RuntimeError('Blender 보조 프로세스 준비 시간이 초과됐습니다.')
        time.sleep(.3)
    original_load = demo.load_model
    def load_model(*args,**kwargs):
        result = original_load(*args,**kwargs)
        demo.model.eval()
        if request.get('use_skeleton'):
            # New editor joints legitimately have zero old skin influence.
            # Prediction preprocessing must not trim/collapse the supplied rig.
            destructive={'AugmentTrim','AugmentCollapse','AugmentDelete','AugmentDropPart','AugmentJointPerturb','AugmentLBS'}
            augments=demo.transform.augments or []
            print('A100_EDITOR_AUGMENTS '+','.join(type(a).__name__ for a in augments),flush=True)
            demo.transform.augments=[a for a in augments if type(a).__name__ not in destructive]
        progress('이미지에서 생성한 형상을 분석하고 뼈대·스키닝을 추론 중입니다.')
        return result
    demo.load_model = load_model
    original_post = demo.post_bpy_payload
    def post_payload(endpoint,payload):
        progress('원본 재질과 리깅을 결합하고 GLB를 저장 중입니다.')
        if endpoint=='transfer':
            payload['target_path'] = request['source'] if request['preserve_original'] else request['analysis_mesh']
            if request.get('use_skeleton'): payload['skeleton_conditioned'] = True
        return original_post(endpoint,payload)
    demo.post_bpy_payload = post_payload
    result = Path(request['output_dir'])/'rigged.glb'
    with torch.inference_mode():
        demo.run_rig([Path(request['analysis_mesh'])],5,.95,1.,2.,10,request.get('use_skeleton',False),True,False,[result],demo.MODEL_CKPTS[0],None)
    if request.get('editor'):
        progress('수정한 관절 위치·연결을 확인하고 AI 가중치를 적용 중입니다.')
        from rigging_editor import restore_ai_edit
        restore_ai_edit(request['source'],result)
    progress('뼈대와 관절 가중치, 텍스처를 확인 중입니다.')
    info = inspect_glb(result)
    source_info = inspect_glb(request['source'],require_skin=False)
    if source_info['texture_sizes'] and not info['texture_sizes']: raise RuntimeError('원본 텍스처가 결과에 포함되지 않았습니다.')
    preview = Path(request['output_dir'])/'rig-preview.glb'
    info['preview_joint_count'] = write_joint_preview(result,preview)
    info['model_path'] = str(result)
    info['preview_path'] = str(preview)
    progress('AI 뼈대에 동작 프리셋과 가벼운 웹 미리보기를 적용 중입니다.')
    motion_env = os.environ.copy()
    motion_env['CUDA_VISIBLE_DEVICES'] = ''
    subprocess.run([sys.executable,str(root/'scripts/rigging_motion_prepare.py'),str(result)],
                   env=motion_env,cwd=root,check=True,timeout=600)
    info['source'] = source_info
    info['preserve_original'] = request['preserve_original']
    info['preparation'] = json.loads((Path(request['output_dir'])/'preparation.json').read_text())
    info['gpu_id'] = 1
    info['model'] = 'SkinTokens / TokenRig'
    if request.get('editor'): info['edited'] = True;info['weight_mode'] = 'ai'
    info['torch_peak_allocated_mib'] = round(torch.cuda.max_memory_allocated()/1024**2,1)
    info['torch_peak_reserved_mib'] = round(torch.cuda.max_memory_reserved()/1024**2,1)
    Path(request['output_dir'],'rigging-result.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
    progress('자동 리깅이 완료됐습니다.')
finally:
    server.terminate()
    try: server.wait(timeout=10)
    except subprocess.TimeoutExpired: server.kill();server.wait()
