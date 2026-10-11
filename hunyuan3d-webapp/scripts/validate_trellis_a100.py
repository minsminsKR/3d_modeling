"""Real TRELLIS.2 GPU/GLB/material validation; does not enable the web model."""
import json
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import trimesh

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from gpu_runtime import gpu_stats, run_isolated_generation
from hunyuan_service import GenerationOptions

DATA_ID = 'DATA-20261010-A100-003'
REPORT = ROOT / 'logs/trellis-a100-validation.json'
report = {'data_id': DATA_ID, 'tests': [], 'telemetry': [], 'passed': False}
stop = threading.Event()
report_lock = threading.Lock()


def save():
    with report_lock:
        REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')


def sample():
    while not stop.is_set():
        stats = gpu_stats()
        if stats:
            report['telemetry'].append({'timestamp': time.time(), 'gpus': stats})
        stop.wait(2)


def summarize(data):
    baseline = data.get('baseline_used_mib') or {str(gpu['id']): gpu['used_mib'] for gpu in data['telemetry'][0]['gpus']}
    data['baseline_used_mib'] = baseline
    data['simultaneous_memory_rise_mib'] = 512
    data['simultaneous_gpu_samples'] = sum(
        len(sample['gpus']) == 2 and all(gpu['used_mib'] > baseline[str(gpu['id'])] + 512 and gpu['utilization'] > 0 for gpu in sample['gpus'])
        for sample in data['telemetry'])
    data['peak_used_mib'] = {str(gpu): max(item['used_mib'] for sample in data['telemetry'] for item in sample['gpus'] if item['id'] == gpu) for gpu in [0, 1]}
    data['passed'] = len(data['tests']) == 4 and all(item.get('passed') for item in data['tests']) and data['simultaneous_gpu_samples'] > 0
    return data


def generate(pipeline, gpu, texture_size):
    name = f'DATA-003-{pipeline}-gpu{gpu}-{texture_size}'
    output = ROOT / 'instance/outputs' / name
    if output.exists():
        raise RuntimeError(f'Preserve prior validation data; archive or select a new directory: {output}')
    options = GenerationOptions(model='trellis2', gpu_id=gpu, gpu_mode='manual', texture=True,
                                remove_background=True, seed=1234, trellis_pipeline=pipeline,
                                trellis_steps=12, trellis_texture_size=texture_size)
    item = {'name': name, 'pipeline': pipeline, 'gpu': gpu, 'texture_size': texture_size}
    with report_lock:
        report['tests'].append(item)
    save()
    started = time.monotonic()
    try:
        asset = run_isolated_generation([str(ROOT / 'vendor/Hunyuan3D-2.1/assets/demo.png')], output, options,
                                       lambda message: print(name, message, flush=True))
        scene = trimesh.load(asset.model_path, force='scene')
        images = []
        for mesh in scene.geometry.values():
            material = getattr(mesh.visual, 'material', None)
            for key in ['baseColorTexture', 'metallicRoughnessTexture']:
                image = getattr(material, key, None)
                if image is not None:
                    images.append({'map': key, 'size': list(image.size)})
        item.update(geometry_count=len(scene.geometry), faces=sum(len(mesh.faces) for mesh in scene.geometry.values()),
                    glb_bytes=asset.model_path.stat().st_size, textures=images, used_texture=asset.used_texture,
                    metadata=json.loads(asset.metadata_path.read_text()))
        item['texture_dimensions_ok'] = len(images) >= 2 and all(image['size'] == [texture_size, texture_size] for image in images)
        with asset.model_path.open('rb') as model:
            item['glb_magic_ok'] = model.read(4) == b'glTF'
        assert item['geometry_count'] and item['faces'] and item['glb_magic_ok'] and item['used_texture'] and item['texture_dimensions_ok']
        item['passed'] = True
    except Exception as error:
        item.update(passed=False, error=str(error))
        raise
    finally:
        item['elapsed_seconds'] = round(time.monotonic() - started, 1)
        save()


def main():
    config = ROOT / 'vendor/TRELLIS.2/local_models/TRELLIS.2-4B/pipeline-a100.json'
    if not config.exists():
        raise RuntimeError('Complete authorized DINOv3/RMBG-2.0 downloads before inference validation.')
    health = gpu_stats()
    assert {gpu['id'] for gpu in health} == {0, 1}, 'Expected two physical A100 GPUs'
    report['baseline_used_mib'] = {str(gpu['id']): gpu['used_mib'] for gpu in health}
    thread = threading.Thread(target=sample, daemon=True)
    thread.start()
    try:
        # Native 512 jobs validate independent GPU use, including existing GPU 0 RTSP allocation.
        with ThreadPoolExecutor(max_workers=2) as executor:
            pending = [executor.submit(generate, '512', gpu, 4096) for gpu in [0, 1]]
            for future in pending:
                future.result()
        generate('1024_cascade', 1, 4096)
        generate('1536_cascade', 1, 8192)
        summarize(report)
        assert report['passed']
    finally:
        stop.set()
        thread.join(timeout=5)
        save()
    print('TRELLIS_VALIDATION_COMPLETE', report['passed'], flush=True)


if __name__ == '__main__':
    if sys.argv[1:] == ['--summarize']:
        report = summarize(json.loads(REPORT.read_text()))
        save()
        assert report['passed'], 'Native outputs or measured simultaneous-GPU activity failed validation'
        print('TRELLIS_VALIDATION_COMPLETE', report['passed'], flush=True)
    else:
        main()
