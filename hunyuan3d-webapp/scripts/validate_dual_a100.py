"""Real HTTP generation, GLB/material checks, and two-GPU telemetry (DATA-002)."""
import json
import subprocess
import threading
import time
from pathlib import Path

import requests
import trimesh

ROOT = Path(__file__).resolve().parents[1]
BASE = 'http://127.0.0.1:5001'
REPORT = ROOT / 'logs/dual-a100-validation.json'
report = {'data_id': 'DATA-20261010-A100-002', 'tests': [], 'telemetry': []}
stop = threading.Event()


def save():
    REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')


def sample():
    while not stop.is_set():
        result = subprocess.run(['nvidia-smi', '--query-gpu=index,memory.used,utilization.gpu', '--format=csv,noheader,nounits'], capture_output=True, text=True, check=True)
        rows = [[int(value.strip()) for value in line.split(',')] for line in result.stdout.splitlines()]
        report['telemetry'].append({'timestamp': time.time(), 'gpus': rows})
        stop.wait(2)


def submit(name, gpu, model='hunyuan3d-2.1', texture=False, profile='standard', full=False):
    form = {'model': model, 'gpu_id': gpu, 'texture_profile': profile, 'num_inference_steps': '50' if full else '5', 'octree_resolution': '512' if full else '128', 'num_chunks': '50000', 'seed': '1234', 'guidance_scale': '7.5', 'remove_background': 'on'}
    if texture:
        form['texture'] = 'on'
    with (ROOT / 'vendor/Hunyuan3D-2.1/assets/demo.png').open('rb') as image:
        response = requests.post(BASE + '/jobs', data=form, files={'images': (name + '.png', image, 'image/png')}, allow_redirects=False, timeout=60)
    response.raise_for_status()
    item = {'name': name, 'job_id': response.headers['Location'].rsplit('/', 1)[-1], 'request': form}
    report['tests'].append(item)
    save()
    print('SUBMITTED', json.dumps(item), flush=True)
    return item


def wait(items):
    pending = list(items)
    deadline = time.monotonic() + 3600
    while pending and time.monotonic() < deadline:
        for item in pending[:]:
            job = requests.get(BASE + '/api/jobs/' + item['job_id'], timeout=30).json()
            state = (job['status'], job.get('message'))
            if state != item.get('_state'):
                print(item['name'], *state, flush=True)
                item['_state'] = state
            if job['status'] not in {'completed', 'failed'}:
                continue
            item.update({key: job.get(key) for key in ['status', 'used_texture', 'warnings', 'options', 'elapsed_seconds']})
            item.pop('_state', None)
            if job['status'] == 'completed':
                path = ROOT / 'instance/outputs' / item['job_id'] / 'model.glb'
                scene = trimesh.load(path, force='scene')
                item['geometry_count'] = len(scene.geometry)
                item['faces'] = sum(len(mesh.faces) for mesh in scene.geometry.values())
                item['glb_bytes'] = path.stat().st_size
                images = []
                for mesh in scene.geometry.values():
                    material = getattr(mesh.visual, 'material', None)
                    for key in ['baseColorTexture', 'metallicRoughnessTexture']:
                        image = getattr(material, key, None)
                        if image is not None:
                            images.append({'map': key, 'size': list(image.size)})
                item['textures'] = images
                if item['request'].get('texture') == 'on' and item['used_texture']:
                    size = 8192 if item['request']['texture_profile'] == 'ultra' else 4096
                    item['texture_dimensions_ok'] = len(images) >= 2 and all(image['size'] == [size, size] for image in images)
                download = requests.get(BASE + '/jobs/' + item['job_id'] + '/model', timeout=60)
                item['download_ok'] = download.status_code == 200 and download.content[:4] == b'glTF'
                assert item['geometry_count'] and item['download_ok']
            pending.remove(item)
            save()
        time.sleep(10 if pending else 0)
    if pending:
        raise TimeoutError('Generation validation timed out')


def main():
    thread = threading.Thread(target=sample, daemon=True)
    thread.start()
    try:
        auto = [submit('DATA-002-auto-' + str(i), 'auto') for i in range(2)]
        wait(auto)
        assert {item['options']['gpu_id'] for item in auto} == {0, 1}
        quality = [submit('DATA-002-detail-50-512', 'split', texture=True, profile='detail', full=True), submit('DATA-002-ultra-50-512', 'split', texture=True, profile='ultra', full=True)]
        ema = submit('DATA-002-omni-ema', 'auto', model='hunyuan3d-omni')
        wait(quality + [ema])
        report['passed'] = all(item['status'] == 'completed' and (item['request'].get('texture') != 'on' or (item['used_texture'] and item.get('texture_dimensions_ok'))) for item in report['tests'])
        report['simultaneous_gpu_samples'] = sum(all(gpu[1] > (14000 if gpu[0] == 0 else 6000) and gpu[2] > 0 for gpu in sample['gpus']) for sample in report['telemetry'])
        report['peak_used_mib'] = {str(i): max(sample['gpus'][i][1] for sample in report['telemetry']) for i in range(2)}
        assert ema['options']['omni_use_ema']
    finally:
        stop.set()
        thread.join(timeout=5)
        save()
    print('DUAL_VALIDATION_COMPLETE', report.get('passed'), flush=True)


if __name__ == '__main__':
    main()
