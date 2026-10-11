from pathlib import Path
import json
import os
import queue
import signal
import subprocess
import threading
import time

from gpu_runtime import gpu_stage_lock

BASE_DIR = Path(__file__).resolve().parent

def rigging_ready():
    marker = BASE_DIR/'logs/rigging-ready.json'
    if not marker.exists() or not (BASE_DIR/'.venv-rig/bin/python').exists(): return False
    try: return json.loads(marker.read_text()).get('ready') is True
    except (OSError,ValueError): return False

def run_logged(command,env,cwd,log,progress,timeout):
    process = subprocess.Popen(command,cwd=cwd,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                               text=True,errors='replace',bufsize=1,start_new_session=True)
    lines = queue.Queue()
    def read():
        try:
            for line in process.stdout: lines.put(line)
        finally: lines.put(None)
    threading.Thread(target=read,daemon=True).start()
    deadline = time.monotonic()+timeout
    try:
        while True:
            if time.monotonic()>deadline: raise TimeoutError('리깅 처리 제한 시간을 초과했습니다. 다시 실행하거나 가벼운 모델로 시도하세요.')
            try: line = lines.get(timeout=1)
            except queue.Empty: continue
            if line is None: break
            log.write(line);log.flush()
            if line.startswith('A100_RIG_EVENT '): progress(json.loads(line[len('A100_RIG_EVENT '):])['message'])
        code = process.wait(timeout=10)
        if code: raise RuntimeError('자동 리깅 처리에 실패했습니다. 입력 형상이나 리깅 로그를 확인하세요.')
    finally:
        # Includes the helper and dataloader children, never unrelated server processes.
        try: os.killpg(process.pid,signal.SIGTERM)
        except ProcessLookupError: pass
        try: process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            try: os.killpg(process.pid,signal.SIGKILL)
            except ProcessLookupError: pass
            process.wait()

def run_rigging(source,output_dir,options,progress):
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True,exist_ok=True)
    source = Path(source).resolve()
    if not source.is_file() or source.suffix.lower()!='.glb': raise ValueError('리깅할 GLB 파일이 없습니다.')
    request = {'source':str(source),'output_dir':str(output_dir),'analysis_mesh':str(output_dir/'analysis.glb'),
               'analysis_faces':options['analysis_faces'],'preserve_original':options['preserve_original'],'seed':options['seed']}
    request['use_skeleton'] = options.get('use_skeleton',False)
    request['editor'] = options.get('editor',False)
    request_path = output_dir/'rigging-request.json'
    request_path.write_text(json.dumps(request,ensure_ascii=False,indent=2))
    env = os.environ.copy()
    env.update({'PYTHONUNBUFFERED':'1','CUDA_VISIBLE_DEVICES':'1','OMP_NUM_THREADS':'8','HF_HUB_OFFLINE':'1','TRANSFORMERS_OFFLINE':'1'})
    python = str(BASE_DIR/'.venv-rig/bin/python')
    started = time.time()
    with (output_dir/'rigging-worker.log').open('w') as log:
        progress('리깅 분석용 메시를 정리 중입니다. 원본 파일은 유지됩니다.')
        prepare_env=env.copy();prepare_env['CUDA_VISIBLE_DEVICES']=''
        run_logged([python,str(BASE_DIR/'scripts/rigging_prepare.py'),str(request_path)],prepare_env,BASE_DIR,log,progress,600)
        with gpu_stage_lock(1,'자동 리깅',progress):
            run_logged([python,str(BASE_DIR/'scripts/rigging_worker.py'),str(request_path)],env,BASE_DIR,log,progress,1800)
    result = json.loads((output_dir/'rigging-result.json').read_text())
    result['elapsed_seconds']=int(time.time()-started)
    (output_dir/'rigging-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    return result
