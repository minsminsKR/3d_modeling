"""Official TRELLIS.2 inference in its dedicated Torch/CUDA environment."""
import json
import os
import sys
import time
import gc
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
repo = Path(os.getenv('TRELLIS_REPO', str(ROOT / 'vendor/TRELLIS.2')))
sys.path.insert(0, str(repo))
os.environ.setdefault('OPENCV_IO_ENABLE_OPENEXR', '1')
os.environ.setdefault('PYTORCH_CUDA_ALLOC_CONF', 'expandable_segments:True')
os.environ.setdefault('ATTN_BACKEND', 'flash_attn')


def progress(message):
    print('A100_EVENT ' + json.dumps({'message': message, 'timestamp': time.time()}, ensure_ascii=False), flush=True)


def main():
    payload = json.loads(Path(sys.argv[1]).read_text())
    options = payload['options']
    output = Path(payload['output_dir'])
    output.mkdir(parents=True, exist_ok=True)
    import torch
    from PIL import Image
    import trimesh
    import o_voxel
    from trellis_device_pipeline import DevicePipeline

    pipeline_type = options.get('trellis_pipeline', '1024_cascade')
    class A100Pipeline(DevicePipeline):
        model_names_to_load = [name for name in DevicePipeline.model_names_to_load if not (pipeline_type == '512' and name.endswith('_1024')) and not (pipeline_type != '512' and name == 'tex_slat_flow_model_512')]

    requested_split = options.get('gpu_mode') == 'split' and options.get('texture_gpu_id') is not None
    split = requested_split and torch.cuda.device_count() >= 2
    warnings = []
    auxiliary_budget = None
    torch.cuda.set_device(0)
    if split:
        free, total = torch.cuda.mem_get_info(1)
        # GPU 0 also hosts RTSP. Keep at least 4GiB of current free VRAM
        # outside this worker's caching allocator and cap its use at 70%.
        if free < 18 * 1024**3:
            split = False
            warnings.append('보조 GPU의 여유 메모리가 부족해 GPU 1에서 캐시 절약 모드로 생성했습니다.')
        else:
            auxiliary_budget = min(0.70, (free - 4 * 1024**3) / total)
            torch.cuda.set_per_process_memory_fraction(auxiliary_budget, 1)
    for gpu in range(torch.cuda.device_count()):
        torch.cuda.reset_peak_memory_stats(gpu)
    # Auxiliary allocator setup can leave CUDA's current device on that card.
    # Upstream rembg uses an unindexed .cuda() internally.
    torch.cuda.set_device(0)
    started = time.monotonic()

    model_dir = Path(os.getenv('TRELLIS_MODEL_DIR', str(repo / 'local_models/TRELLIS.2-4B')))
    progress('TRELLIS.2 모델 로딩 중...')
    pipeline = A100Pipeline.from_pretrained(str(model_dir), config_file='pipeline-a100.json')
    pipeline.configure_devices(split, progress, shape_physical=options['gpu_id'], material_physical=options.get('texture_gpu_id', options['gpu_id']))
    progress('TRELLIS.2 두 GPU 분담 · 형상 GPU 1 / 재질 GPU 0' if split else f"TRELLIS.2 GPU {options['gpu_id']} 캐시 절약 모드")
    image = Image.open(payload['image_paths'][0])
    if options.get('remove_background', True):
        progress('TRELLIS.2 배경 제거 및 입력 전처리 중...')
        image = pipeline.preprocess_image(image)
    else:
        image = image.convert('RGB')
    processed = output / 'processed_inputs'
    processed.mkdir(exist_ok=True)
    image.save(processed / '01.png')
    steps = options.get('trellis_steps', 12)
    max_tokens = options.get('trellis_max_tokens', 49152)
    if requested_split and not split:
        max_tokens = min(max_tokens, 49152)
        warnings.append('단일 GPU 대체 실행에서는 토큰 한도 49152를 적용했습니다.')
    progress('TRELLIS.2 형상 및 PBR 재질 생성 중...')
    mesh = pipeline.run(image, seed=options['seed'], preprocess_image=False, pipeline_type=pipeline_type,
                        sparse_structure_sampler_params={'steps': steps}, shape_slat_sampler_params={'steps': steps}, tex_slat_sampler_params={'steps': steps},
                        max_num_tokens=max_tokens)[0]
    actual_resolution = round(1 / float(mesh.voxel_size))
    requested_resolution = int(pipeline_type.split('_', 1)[0])
    if actual_resolution < requested_resolution:
        warnings.append(f'토큰 제한에 따라 실제 형상 해상도를 {actual_resolution}로 조정했습니다.')
    # The final mesh/voxel buffers are independent of the inference modules.
    # Release module references before native mesh and PBR allocations.
    timings = dict(pipeline.stage_timings)
    snapshots = list(pipeline.memory_snapshots)
    material_decode_fallback = pipeline.material_decode_fallback
    if material_decode_fallback:
        warnings.append('GPU 0의 메모리 여유를 유지하기 위해 재질 복원 단계는 GPU 1에서 완료했습니다. 형상 해상도와 텍스처 크기는 유지했습니다.')
    del pipeline
    gc.collect()
    for gpu in range(torch.cuda.device_count()):
        with torch.cuda.device(gpu):
            torch.cuda.empty_cache()
    if mesh.faces.shape[0] > 16777216:
        mesh.simplify(16777216)
    progress('TRELLIS.2 PBR 텍스처 베이킹 및 GLB 저장 중...')
    glb = o_voxel.postprocess.to_glb(vertices=mesh.vertices, faces=mesh.faces, attr_volume=mesh.attrs, coords=mesh.coords,
                                   attr_layout=mesh.layout, voxel_size=mesh.voxel_size, aabb=[[-0.5]*3, [0.5]*3],
                                   decimation_target=1000000, texture_size=options.get('trellis_texture_size', 4096),
                                   remesh=True, remesh_band=1, remesh_project=0, verbose=True)
    path = output / 'model.glb'
    glb.export(str(path), extension_webp=False)
    scene = trimesh.load(path, force='scene')
    if not scene.geometry:
        raise RuntimeError('TRELLIS.2 output has no geometry')
    metadata = output / 'metadata.json'
    metadata.write_text(json.dumps({'source_images': payload['image_paths'], 'model_path': str(path), 'used_texture': True, 'warnings': warnings, 'options': options,
                                    'actual_pipeline': pipeline_type, 'actual_steps_per_stage': steps, 'effective_voxel_size': float(mesh.voxel_size),
                                    'actual_resolution': actual_resolution,
                                    'effective_max_tokens': max_tokens,
                                    'cross_gpu_transfer': 'cpu_staging' if split else None,
                                    'actual_gpu_mode': 'split' if split else 'single_memory_safe',
                                    'gpu_devices_used': [options['gpu_id'], options['texture_gpu_id']] if split else [options['gpu_id']],
                                    'gpu_stage_timings': timings, 'gpu_memory_snapshots': snapshots,
                                    'auxiliary_memory_fraction': auxiliary_budget,
                                    'material_decode_fallback': material_decode_fallback,
                                    'torch_peak_memory_mib': [{'physical_gpu': int(os.environ.get('CUDA_VISIBLE_DEVICES','0').split(',')[gpu]),
                                                             'allocated': round(torch.cuda.max_memory_allocated(gpu)/1048576,1),
                                                             'reserved': round(torch.cuda.max_memory_reserved(gpu)/1048576,1)} for gpu in range(torch.cuda.device_count())],
                                    'elapsed_seconds': round(time.monotonic()-started,2),
                                    'note': 'Official native generation with shape/material GPU placement, independent mesh-repair overlap, and released sparse caches.'}, indent=2))
    result = {'model_path': str(path), 'metadata_path': str(metadata), 'used_texture': True, 'warnings': warnings}
    (output / 'generation_result.json').write_text(json.dumps(result))
    progress('TRELLIS.2 모델 생성 완료')


if __name__ == '__main__':
    main()
