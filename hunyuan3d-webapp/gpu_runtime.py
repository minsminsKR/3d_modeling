from __future__ import annotations

import contextlib
import json
import os
import subprocess
import sys
import time
from dataclasses import asdict
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
TEXTURE_PROFILES = {
    "standard": {"label": "표준 · 512 / 4K", "views": 6, "resolution": 512, "render": 2048, "map_size": 4096},
    "detail": {"label": "고품질 · 768 / 4K", "views": 6, "resolution": 768, "render": 2048, "map_size": 4096},
    "ultra": {"label": "고품질 · 768 / 8K", "views": 6, "resolution": 768, "render": 2048, "map_size": 8192},
}


def gpu_stats() -> list[dict]:
    try:
        result = subprocess.run(
            ["nvidia-smi", "--query-gpu=index,name,memory.total,memory.used,memory.free,utilization.gpu", "--format=csv,noheader,nounits"],
            check=True, capture_output=True, text=True, timeout=5,
        )
        rows = []
        for line in result.stdout.splitlines():
            index, name, total, used, free, utilization = [value.strip() for value in line.split(",")]
            rows.append({"id": int(index), "name": name, "total_mib": int(total), "used_mib": int(used), "free_mib": int(free), "utilization": int(utilization)})
        return rows
    except (OSError, ValueError, subprocess.SubprocessError):
        return []


@contextlib.contextmanager
def gpu_stage_lock(gpu_id: int, stage: str, progress=None):
    # Linux locks are shared by all generation subprocesses and released on exit.
    import fcntl

    instance = Path(os.getenv("APP_INSTANCE_DIR", str(BASE_DIR / "instance")))
    directory = instance / ".gpu-locks"
    directory.mkdir(parents=True, exist_ok=True)
    with (directory / f"gpu-{gpu_id}.lock").open("a+") as handle:
        waiting = False
        while True:
            try:
                fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
                break
            except BlockingIOError:
                if progress and not waiting:
                    progress(f"GPU {gpu_id} {stage} 작업 대기 중...")
                waiting = True
                time.sleep(0.5)
        try:
            if progress:
                progress(f"GPU {gpu_id} {stage} 생성 중...")
            yield
        finally:
            if progress:
                progress(f"GPU {gpu_id} {stage} 작업 종료")
            fcntl.flock(handle, fcntl.LOCK_UN)


def run_isolated_generation(image_paths, output_dir: Path, options, progress):
    from hunyuan_service import GeneratedAsset, Hunyuan3DError

    output_dir.mkdir(parents=True, exist_ok=True)
    request_path = output_dir / "generation_request.json"
    worker_options = asdict(options)
    if options.trellis_dual_postprocess:
        worker_options.update(postprocess_checkpoint=True, checkpoint_only=True, strict_resolution=True)
    request_path.write_text(json.dumps({"image_paths": image_paths, "output_dir": str(output_dir), "options": worker_options}, indent=2), encoding="utf-8")
    env = os.environ.copy()
    env["PYTHONUNBUFFERED"] = "1"
    env["OMP_NUM_THREADS"] = "8"
    trellis = options.model == "trellis2"
    worker = BASE_DIR / "scripts" / ("trellis_worker.py" if trellis else "generation_worker.py")
    python = str(BASE_DIR / ".venv-trellis/bin/python") if trellis else sys.executable
    gpu_devices = [options.gpu_id]
    if trellis and options.gpu_mode == "split" and options.texture_gpu_id is not None:
        gpu_devices.append(options.texture_gpu_id)
    if trellis:
        env["CUDA_VISIBLE_DEVICES"] = ",".join(str(gpu) for gpu in gpu_devices)
    if options.trellis_dual_postprocess:
        stats = {row["id"]: row for row in gpu_stats()}
        if stats.get(0, {}).get("free_mib", 0) < 36000 or stats.get(1, {}).get("free_mib", 0) < 38000:
            raise Hunyuan3DError("두 GPU 고해상도 추론에 필요한 여유 VRAM이 부족합니다. 다른 작업이 끝난 뒤 재시도하세요.")
        if not env.get("TRELLIS_CUMESH_PATCH_ROOT"):
            raise Hunyuan3DError("선택형 CuMesh 확장 경로가 설정되지 않았습니다.")
    stage = contextlib.ExitStack() if trellis else contextlib.nullcontext()
    log_path = output_dir / "generation_worker.log"
    with stage, log_path.open("w", encoding="utf-8") as log:
        if trellis:
            # A consistent physical order prevents two split workers from
            # deadlocking while preserving rigging/other stage locks.
            for gpu in sorted(set(gpu_devices)):
                stage.enter_context(gpu_stage_lock(gpu, "TRELLIS.2", progress))
        process = subprocess.Popen(
            [python, str(worker), str(request_path)],
            cwd=str(BASE_DIR), env=env, stdout=subprocess.PIPE, stderr=log, text=True, bufsize=1,
        )
        assert process.stdout is not None
        for line in process.stdout:
            log.write(line)
            log.flush()
            if line.startswith("A100_EVENT "):
                event = json.loads(line[len("A100_EVENT "):])
                progress(event["message"])
        code = process.wait()
        if code == 0 and options.trellis_dual_postprocess:
            progress("원본 형상·재질 저장 완료; 두 GPU 후처리 시작")
            coordinator = BASE_DIR / "scripts" / "run_dual_postprocess.py"
            process = subprocess.Popen(
                [python, str(coordinator), str(output_dir)], cwd=str(BASE_DIR), env=env,
                stdout=subprocess.PIPE, stderr=log, text=True, bufsize=1)
            assert process.stdout is not None
            for line in process.stdout:
                log.write(line)
                log.flush()
                if line.startswith("A100_EVENT "):
                    event = json.loads(line[len("A100_EVENT "):])
                    progress(event["message"])
            code = process.wait()
    if code:
        raise Hunyuan3DError(f"생성 작업 실패. 로그: {log_path.name}\n{log_path.read_text(encoding='utf-8')[-3000:]}")
    result = json.loads((output_dir / "generation_result.json").read_text(encoding="utf-8"))
    return GeneratedAsset(Path(result["model_path"]), Path(result["metadata_path"]), result["used_texture"],
                          tuple(result["warnings"]), result.get("quality_status") == "review_required")
