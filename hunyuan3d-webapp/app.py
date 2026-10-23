from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import sys
import threading
import time
import traceback
import uuid
import zipfile
from datetime import datetime
from dataclasses import replace
from concurrent.futures import ThreadPoolExecutor
from importlib.util import find_spec
from pathlib import Path

from dotenv import load_dotenv
from flask import Flask, abort, jsonify, redirect, render_template, request, send_file, url_for
from werkzeug.utils import secure_filename
from werkzeug.exceptions import HTTPException

from generation_api import archive_for_job, find_submission, prepare_submission, record_for_job, safe_public
from modeling_contract import ModelingError, error_payload, http_error
from modeling_capabilities import build_capabilities, build_doctor

from rigging_runtime import rigging_ready, run_rigging
from rigging_motion import MOTION_VERSION
from rigging_editor import read_skeleton, validate_edit
from rigging_edit_runtime import run_edit
from hunyuan_service import GenerationOptions
from gpu_runtime import TEXTURE_PROFILES, gpu_stats, run_isolated_generation


BASE_DIR = Path(__file__).resolve().parent
load_dotenv(BASE_DIR / ".env", override=True)

INSTANCE_DIR = Path(os.getenv("APP_INSTANCE_DIR", BASE_DIR / "instance"))
if not INSTANCE_DIR.is_absolute():
    INSTANCE_DIR = BASE_DIR / INSTANCE_DIR

UPLOAD_DIR = INSTANCE_DIR / "uploads"
OUTPUT_DIR = INSTANCE_DIR / "outputs"
JOBS_FILE = INSTANCE_DIR / "jobs.json"
ALLOWED_EXTENSIONS = {"png", "jpg", "jpeg", "webp"}
MIXAMO_ALLOWED_EXTENSIONS = {"fbx", "glb", "gltf", "dae"}


def env_bool(name: str, default: bool = False) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    return value.lower() in {"1", "true", "yes", "on"}


def env_int(name: str, default: int) -> int:
    try:
        return int(os.getenv(name, str(default)))
    except ValueError:
        return default


def default_generation_model() -> str:
    model = os.getenv("APP_DEFAULT_MODEL", "hunyuan3d-2.1")
    if model not in {"hunyuan3d-2.1", "hunyuan3d-omni", "trellis2"}:
        return "hunyuan3d-2.1"
    if model == "trellis2" and not trellis_ready():
        return "hunyuan3d-2.1"
    return model


def default_gpu_mode_for_model(model: str) -> str:
    if model == "trellis2" and trellis_dual_ready():
        return "split"
    name = "TRELLIS_DEFAULT_GPU_MODE" if model == "trellis2" else "HUNYUAN_DEFAULT_GPU_MODE"
    return os.getenv(name, "auto" if model == "trellis2" else "split")


def trellis_dual_ready() -> bool:
    marker = BASE_DIR / "instance/trellis-dual-ready.json"
    try:
        return json.loads(marker.read_text()).get("status") == "ready"
    except (OSError, ValueError):
        return False


def trellis_generation_defaults(gpu_mode: str | None = None) -> dict:
    pipeline = os.getenv("TRELLIS_DEFAULT_PIPELINE", "1536_cascade")
    if pipeline not in {"512", "1024_cascade", "1536_cascade"}:
        pipeline = "1536_cascade"
    texture_size = env_int("TRELLIS_DEFAULT_TEXTURE_SIZE", 8192)
    if texture_size not in {4096, 8192}:
        texture_size = 8192
    mode = gpu_mode or default_gpu_mode_for_model("trellis2")
    tokens = env_int("TRELLIS_SPLIT_MAX_NUM_TOKENS", 65536) if mode == "split" else env_int("TRELLIS_MAX_NUM_TOKENS", 49152)
    return {"trellis_pipeline": pipeline,
            "trellis_steps": min(50, max(1, env_int("TRELLIS_DEFAULT_STEPS", 12))),
            "trellis_texture_size": texture_size,
            "trellis_max_tokens": tokens}


def generation_defaults() -> dict:
    model = default_generation_model()
    return {"model": model, "gpu_mode": default_gpu_mode_for_model(model),
            "seed": min(2**32 - 1, max(0, env_int("APP_DEFAULT_SEED", 1234))),
            "remove_background": env_bool("APP_DEFAULT_REMOVE_BACKGROUND", True),
            **trellis_generation_defaults()}


def available_gpu_ids() -> list[int]:
    try:
        import torch

        if torch.cuda.is_available():
            count = torch.cuda.device_count()
            if count > 0:
                return list(range(count))
    except Exception:
        pass
    return [0]


def default_gpu_id(gpu_ids: list[int]) -> int:
    configured = os.getenv("HUNYUAN_DEFAULT_GPU")
    if configured is not None:
        try:
            gpu_id = int(configured)
            if gpu_id in gpu_ids:
                return gpu_id
        except ValueError:
            pass
    if len(gpu_ids) > 1:
        return gpu_ids[1]
    return gpu_ids[0] if gpu_ids else 0


def form_gpu_id(gpu_ids: list[int]) -> int:
    fallback = default_gpu_id(gpu_ids)
    requested = form_int("gpu_id", fallback, 0, max(gpu_ids) if gpu_ids else 0)
    if requested not in gpu_ids:
        return fallback
    return requested


def is_windows_absolute_path(value: str) -> bool:
    return len(value) >= 3 and value[1] == ":" and value[2] in {"\\", "/"}


def resolve_repo_env_path(repo: str | None) -> Path | None:
    if not repo:
        return None
    repo_path = Path(repo).expanduser()
    if repo_path.is_absolute():
        return repo_path.resolve()
    if is_windows_absolute_path(repo):
        return repo_path
    return (BASE_DIR / repo_path).resolve()


def custom_rasterizer_ready(repo_path: Path) -> bool:
    if find_spec("custom_rasterizer") is not None or find_spec("custom_rasterizer_kernel") is not None:
        return True
    build_root = repo_path / "hy3dpaint" / "custom_rasterizer" / "build"
    if not build_root.exists():
        return False
    return any(build_root.rglob("custom_rasterizer_kernel*.so")) or any(
        build_root.rglob("custom_rasterizer_kernel*.pyd")
    )


def create_app() -> Flask:
    app = Flask(__name__)
    app.config["SECRET_KEY"] = os.getenv("FLASK_SECRET_KEY", "dev-secret")
    app.config["MAX_CONTENT_LENGTH"] = env_int("MAX_CONTENT_LENGTH_MB", 200) * 1024 * 1024

    @app.errorhandler(HTTPException)
    def api_error(error):
        if request.path.startswith("/api/"):
            result = http_error(error.code or 500, error.description,
                                retry_after=(error.get_response().headers.get("Retry-After"))).as_dict(api_request_id())
            result["error"] = error.name
            return jsonify(result), error.code
        return error

    @app.errorhandler(ModelingError)
    def modeling_error(error):
        return jsonify(error.as_dict(api_request_id())), error.http_status or 400

    def api_request_id():
        value = request.headers.get("Idempotency-Key") or request.args.get("request_id", "")
        return value if re.fullmatch(r"[A-Za-z0-9._-]{8,120}", value) else None

    for path in [INSTANCE_DIR, UPLOAD_DIR, OUTPUT_DIR]:
        path.mkdir(parents=True, exist_ok=True)

    jobs_lock = threading.Lock()
    jobs = load_jobs()
    cleanup_unsaved_jobs(jobs)
    executor = ThreadPoolExecutor(max_workers=env_int("GENERATION_WORKERS", 2))
    gpu_load = {gpu_id: 0 for gpu_id in available_gpu_ids()}
    rigging_executor = ThreadPoolExecutor(max_workers=1)

    def set_job(job_id: str, **updates) -> dict:
        with jobs_lock:
            job = jobs[job_id]
            job.update(updates)
            job["updated_at"] = time.time()
            save_jobs(jobs)
            return dict(job)

    def run_generation(job_id: str, image_paths: list[str], options: GenerationOptions) -> None:
        set_job(
            job_id,
            status="running",
            message="Starting generation...",
            started_at=time.time(),
        )
        try:
            asset = run_isolated_generation(
                image_paths,
                OUTPUT_DIR / job_id,
                options,
                progress=lambda message: set_job(job_id, status="running", message=message),
            )
            warnings = list(asset.warnings)
            message = "Model is ready."
            if options.texture and not asset.used_texture:
                message = "Model is ready, but texture generation failed. Showing untextured shape."
            set_job(
                job_id,
                status="completed",
                message=message,
                finished_at=time.time(),
                model_path=str(asset.model_path),
                metadata_path=str(asset.metadata_path),
                used_texture=asset.used_texture,
                warnings=warnings,
            )
        except Exception as exc:
            error_path = OUTPUT_DIR / job_id / "error.log"
            error_path.parent.mkdir(parents=True, exist_ok=True)
            error_path.write_text(traceback.format_exc(), encoding="utf-8")
            set_job(job_id, status="failed", message=str(exc), finished_at=time.time(), error_path=str(error_path))
        finally:
            with jobs_lock:
                gpu_load[options.gpu_id] = max(0, gpu_load.get(options.gpu_id, 0) - 1)
                if options.model == "trellis2" and options.texture_gpu_id is not None:
                    gpu_load[options.texture_gpu_id] = max(0, gpu_load.get(options.texture_gpu_id, 0) - 1)

    @app.get("/")
    def index():
        visible_recent_jobs = [
            job
            for job in sorted(jobs.values(), key=lambda item: item.get("created_at", 0), reverse=True)
            if job.get("status") != "failed"
        ][:8]
        texture_issues = texture_dependency_issues()
        omni_issues = omni_dependency_issues()
        gpu_ids = available_gpu_ids()
        defaults = generation_defaults()
        return render_template(
            "index.html",
            recent_jobs=[public_job(job) for job in visible_recent_jobs],
            saved_job_count=len(list_saved_jobs(jobs)),
            max_files=env_int("MAX_UPLOAD_FILES", 15),
            gpu_ids=gpu_ids,
            default_gpu_id=default_gpu_id(gpu_ids),
            default_model=defaults["model"],
            default_gpu_mode=defaults["gpu_mode"],
            hunyuan_default_gpu_mode=default_gpu_mode_for_model("hunyuan3d-2.1"),
            trellis_default_gpu_mode=default_gpu_mode_for_model("trellis2"),
            generation_defaults=defaults,
            default_texture_profile=os.getenv("HUNYUAN_DEFAULT_TEXTURE_PROFILE", "detail"),
            texture_profiles=TEXTURE_PROFILES,
            gpu_stats=gpu_stats(),
            omni_ema_ready=omni_ema_ready(),
            trellis_ready=trellis_ready(),
            texture_ready=not texture_issues,
            texture_issues=texture_issues,
            omni_ready=not omni_issues,
            omni_issues=omni_issues,
            multiview_ready=multiview_shape_ready(),
        )

    def submission_response(created_jobs, submission, replayed=False):
        batch_id = created_jobs[0].get("batch_id")
        return jsonify(request_id=submission["request_id"], batch_id=batch_id,
                       jobs=[public_job(job) for job in created_jobs], replayed=replayed,
                       web_url=url_for("batch_detail", batch_id=batch_id) if batch_id else
                       url_for("job_detail", job_id=created_jobs[0]["id"])), 200 if replayed else 201

    @app.post("/api/jobs")
    @app.post("/jobs")
    def create_job():
        uploaded_files = [file for file in request.files.getlist("images") if file and file.filename]
        max_files = env_int("MAX_UPLOAD_FILES", 15)
        if not uploaded_files:
            abort(400, "Upload at least one image.")
        if len(uploaded_files) > max_files:
            abort(400, f"Upload up to {max_files} images.")

        validated_files = []
        for file in uploaded_files:
            # secure_filename("캐릭터.png") becomes "png". Check the original
            # basename's extension before sanitizing its stem for disk storage.
            original_name = file.filename.replace("\\", "/").rsplit("/", 1)[-1]
            extension = original_name.rsplit(".", 1)[-1].lower() if "." in original_name else ""
            if extension not in ALLOWED_EXTENSIONS:
                abort(400, f"Unsupported image type: {original_name}")
            stem = secure_filename(original_name.rsplit(".", 1)[0])[:160] or "image"
            filename = f"{stem}.{extension}"
            validated_files.append((file, filename, original_name))

        submission = prepare_submission(request, validated_files) if request.path == "/api/jobs" else None
        if submission:
            with jobs_lock:
                existing = find_submission(jobs, submission)
                if existing:
                    return submission_response(existing, submission, replayed=True)

        defaults = generation_defaults()
        model = request.form.get("model", defaults["model"])
        if model not in {"hunyuan3d-2.1", "hunyuan3d-omni", "trellis2"}:
            abort(400, "Unsupported model selection.")
        if model == "trellis2" and not trellis_ready():
            if submission:
                raise ModelingError("TRELLIS.2 is not configured for generation; inspect doctor.",
                                    "SERVER_NOT_READY", http_status=503, details={"model": model})
            abort(400, "TRELLIS.2 모델 접근 승인과 서버 검증이 아직 완료되지 않았습니다.")
        if model == "hunyuan3d-omni" and omni_dependency_issues():
            if submission:
                raise ModelingError("Hunyuan3D-Omni is not configured for generation; inspect doctor.",
                                    "SERVER_NOT_READY", http_status=503, details={"model": model})
            abort(400, f"Hunyuan3D-Omni is not ready: {', '.join(omni_dependency_issues())}")

        texture_requested = request.form.get("texture") == "on" or model == "trellis2"
        gpu_ids = available_gpu_ids()
        requested_gpu = request.form.get("gpu_id", default_gpu_mode_for_model(model))
        if submission and "gpu_id" in request.form and requested_gpu not in {"auto", "split"} and int(requested_gpu) not in gpu_ids:
            raise ModelingError("The requested physical GPU is not available on this server.",
                                "INVALID_INPUT", http_status=400, details={"field": "gpu_id"})
        gpu_mode = requested_gpu if requested_gpu in {"auto", "split"} else "manual"
        shape_gpu = env_int("HUNYUAN_SHAPE_GPU", 0) if gpu_mode == "split" else form_gpu_id(gpu_ids)
        texture_gpu = env_int("HUNYUAN_TEXTURE_GPU", 1) if gpu_mode == "split" else None
        if shape_gpu not in gpu_ids or (texture_gpu is not None and texture_gpu not in gpu_ids):
            abort(400, "선택한 GPU 처리 방식에 필요한 GPU가 없습니다.")
        texture_profile = request.form.get("texture_profile", os.getenv("HUNYUAN_DEFAULT_TEXTURE_PROFILE", "detail"))
        if texture_profile not in TEXTURE_PROFILES:
            abort(400, "지원하지 않는 텍스처 품질 설정입니다.")
        if model != "trellis2" and texture_requested and texture_profile != "standard" and gpu_mode == "manual" and shape_gpu != env_int("HUNYUAN_TEXTURE_GPU", 1):
            abort(400, "고품질 텍스처는 여유 메모리가 있는 GPU 1 또는 두 GPU 분담 모드를 선택하세요.")
        trellis_pipeline = request.form.get("trellis_pipeline", defaults["trellis_pipeline"])
        if trellis_pipeline not in {"512", "1024_cascade", "1536_cascade"}:
            abort(400, "지원하지 않는 TRELLIS.2 해상도입니다.")
        trellis_texture_size = form_int("trellis_texture_size", defaults["trellis_texture_size"], 4096, 8192)
        if trellis_texture_size not in {4096, 8192}:
            abort(400, "TRELLIS.2 텍스처는 4K 또는 8K를 선택하세요.")
        if model == "trellis2":
            if gpu_mode == "split":
                if not trellis_dual_ready() or len(gpu_ids) < 2:
                    if submission:
                        raise ModelingError("The verified TRELLIS.2 two-GPU configuration is unavailable.",
                                            "SERVER_NOT_READY", http_status=503, details={"model": model})
                    abort(503, "TRELLIS.2 두 GPU 분담 모드가 아직 준비되지 않았습니다.")
                shape_gpu = env_int("HUNYUAN_TEXTURE_GPU", 1)
                texture_gpu = next(gpu for gpu in gpu_ids if gpu != shape_gpu)
            else:
                texture_gpu = None
            if trellis_pipeline != "512" and gpu_mode == "manual" and shape_gpu != env_int("HUNYUAN_TEXTURE_GPU", 1):
                abort(400, "TRELLIS.2 1024·1536 모드는 GPU 1을 사용하세요.")
        options = GenerationOptions(
            model=model,
            gpu_id=shape_gpu,
            texture_gpu_id=texture_gpu,
            gpu_mode=gpu_mode,
            texture_profile=texture_profile,
            omni_use_ema=model == "hunyuan3d-omni" and omni_ema_ready() and env_bool("HUNYUAN_OMNI_USE_EMA", True),
            trellis_pipeline=trellis_pipeline,
            trellis_steps=form_int("trellis_steps", defaults["trellis_steps"], 1, 50),
            trellis_texture_size=trellis_texture_size,
            trellis_max_tokens=trellis_generation_defaults(gpu_mode)["trellis_max_tokens"],
            texture=True if model == "trellis2" else texture_requested and texture_dependencies_ready(),
            remove_background=("on" in request.form.getlist("remove_background")) if "remove_background" in request.form else defaults["remove_background"],
            seed=form_int("seed", defaults["seed"], 0, 2**32 - 1),
            num_inference_steps=form_int("num_inference_steps", 50, 1, 100),
            guidance_scale=form_float("guidance_scale", 7.5, 0.1, 20.0),
            octree_resolution=form_int("octree_resolution", 512, 64, 512),
            num_chunks=form_int("num_chunks", 50000, 1000, 300000),
            output_format="glb",
        )

        now = time.time()
        batch_id = str(uuid.uuid4()) if len(uploaded_files) > 1 else None
        created_jobs = []
        with jobs_lock:
            if submission:
                existing = find_submission(jobs, submission)
                if existing:
                    return submission_response(existing, submission, replayed=True)
            for index, (file, filename, original_name) in enumerate(validated_files, start=1):
                job_options = options
                if gpu_mode == "auto":
                    # GPU 0 shares VRAM with RTSP. High-resolution texture jobs
                    # use GPU 1; standard jobs can run independently on both.
                    dedicated = trellis_pipeline != "512" if model == "trellis2" else options.texture and texture_profile != "standard"
                    candidates = [default_gpu_id(gpu_ids)] if dedicated else gpu_ids
                    assigned_gpu = min(candidates, key=lambda gpu: (gpu_load.get(gpu, 0), gpu != default_gpu_id(gpu_ids)))
                    job_options = replace(options, gpu_id=assigned_gpu)
                gpu_load[job_options.gpu_id] = gpu_load.get(job_options.gpu_id, 0) + 1
                if job_options.model == "trellis2" and job_options.texture_gpu_id is not None:
                    gpu_load[job_options.texture_gpu_id] = gpu_load.get(job_options.texture_gpu_id, 0) + 1
                job_id = str(uuid.uuid4())
                job_upload_dir = UPLOAD_DIR / job_id
                job_output_dir = OUTPUT_DIR / job_id
                job_upload_dir.mkdir(parents=True, exist_ok=True)
                job_output_dir.mkdir(parents=True, exist_ok=True)

                saved_path = job_upload_dir / filename
                file.save(saved_path)
                job = {
                    "id": job_id,
                    "status": "queued",
                    "message": "Waiting for GPU worker...",
                    "created_at": now,
                    "updated_at": now,
                    "image_paths": [str(saved_path)],
                    "source_filename": original_name,
                    "batch_index": index,
                    "batch_total": len(uploaded_files),
                    "options": job_options.__dict__,
                }
                if batch_id is not None:
                    job["batch_id"] = batch_id
                if submission:
                    job.update({key: value for key, value in submission.items() if key != "inputs"})
                    job.update(inputs=[submission["inputs"][index - 1]], saved=True, saved_at=now)
                jobs[job_id] = job
                created_jobs.append(job)
            save_jobs(jobs)

        for job in created_jobs:
            executor.submit(run_generation, job["id"], job["image_paths"], GenerationOptions(**job["options"]))

        if submission:
            return submission_response(created_jobs, submission)

        if batch_id is not None:
            return redirect(url_for("batch_detail", batch_id=batch_id))
        return redirect(url_for("job_detail", job_id=created_jobs[0]["id"]))

    @app.get("/batches/<batch_id>")
    def batch_detail(batch_id: str):
        batch_jobs = jobs_for_batch(batch_id, jobs)
        if not batch_jobs:
            abort(404)
        return render_template(
            "batch.html",
            batch_id=batch_id,
            jobs=[public_job(job) for job in batch_jobs],
            completed_count=sum(1 for job in batch_jobs if job.get("status") == "completed"),
            failed_count=sum(1 for job in batch_jobs if job.get("status") == "failed"),
        )

    @app.get("/api/batches/<batch_id>")
    def batch_status(batch_id: str):
        batch_jobs = jobs_for_batch(batch_id, jobs)
        if not batch_jobs:
            abort(404)
        public_jobs = [public_job(job) for job in batch_jobs]
        return jsonify(
            {
                "id": batch_id,
                "jobs": public_jobs,
                "total": len(public_jobs),
                "completed": sum(1 for job in public_jobs if job.get("status") == "completed"),
                "failed": sum(1 for job in public_jobs if job.get("status") == "failed"),
                "running": sum(1 for job in public_jobs if job.get("status") == "running"),
                "queued": sum(1 for job in public_jobs if job.get("status") == "queued"),
                "download_url": url_for("download_batch", batch_id=batch_id),
            }
        )

    @app.get("/saved")
    def saved_jobs_page():
        saved = [public_job(job) for job in list_saved_jobs(jobs)]
        return render_template("saved.html", saved_jobs=saved)

    @app.get("/jobs/<job_id>")
    def job_detail(job_id: str):
        job = jobs.get(job_id)
        if job is None:
            abort(404)
        return render_template("job.html", job=public_job(job))

    @app.get("/api/jobs")
    def job_history():
        try:
            limit = int(request.args.get("limit", "50"))
            offset = int(request.args.get("offset", "0"))
        except ValueError:
            abort(400, "limit and offset must be integers.")
        if not 1 <= limit <= 200 or offset < 0:
            abort(400, "limit must be 1-200 and offset must be nonnegative.")
        source = request.args.get("source")
        if source is not None and source not in {"cli", "api", "web"}:
            abort(400, "source must be cli, api or web.")
        request_id = request.args.get("request_id")
        with jobs_lock:
            selected = [dict(job) for job in jobs.values()
                        if (source is None or job.get("source", "web") == source)
                        and (request_id is None or job.get("request_id") == request_id)]
        selected.sort(key=lambda job: (job.get("created_at", 0), job["id"]), reverse=True)
        return jsonify(jobs=[public_job(job) for job in selected[offset:offset + limit]],
                       total=len(selected), limit=limit, offset=offset)

    @app.get("/api/jobs/<job_id>/record")
    def job_record(job_id):
        with jobs_lock:
            job = dict(jobs.get(job_id) or {})
        if not job:
            abort(404)
        return jsonify(record_for_job(job, public_job(job), UPLOAD_DIR, OUTPUT_DIR))

    @app.get("/api/jobs/<job_id>/archive")
    def job_archive(job_id):
        with jobs_lock:
            job = dict(jobs.get(job_id) or {})
        if not job:
            abort(404)
        return archive_for_job(job, public_job(job), UPLOAD_DIR, OUTPUT_DIR)

    @app.get("/api/jobs/<job_id>")
    def job_status(job_id: str):
        job = jobs.get(job_id)
        if job is None:
            abort(404)
        return jsonify(public_job(job))

    @app.post("/api/jobs/<job_id>/saved")
    def set_job_saved(job_id: str):
        job = jobs.get(job_id)
        if job is None:
            abort(404)
        if job.get("status") != "completed":
            abort(400, "Only completed jobs can be saved.")
        model_path = Path(job.get("model_path", ""))
        if not model_path.exists():
            abort(400, "Model file is missing; this job cannot be saved.")

        payload = request.get_json(silent=True) or {}
        save_requested = payload.get("saved", True)
        if isinstance(save_requested, str):
            save_requested = save_requested.lower() in {"1", "true", "yes", "on"}
        else:
            save_requested = bool(save_requested)

        if save_requested:
            updated = set_job(job_id, saved=True, saved_at=time.time())
        else:
            updated = set_job(job_id, saved=False, saved_at=None)
        return jsonify(public_job(updated))

    @app.get("/jobs/<job_id>/download")
    def download_model(job_id: str):
        job = jobs.get(job_id)
        if job is None or job.get("status") != "completed":
            abort(404)
        model_path = Path(job["model_path"])
        if not model_path.exists():
            abort(404)
        return send_file(model_path, as_attachment=True, download_name=download_name_for_job(job))

    @app.get("/batches/<batch_id>/download")
    def download_batch(batch_id: str):
        batch_jobs = [job for job in jobs_for_batch(batch_id, jobs) if job.get("status") == "completed"]
        if not batch_jobs:
            abort(404)

        package_path = OUTPUT_DIR / f"batch_{batch_id}_models.zip"
        with zipfile.ZipFile(package_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            written_names = set()
            for job in batch_jobs:
                model_path = Path(job["model_path"])
                if not model_path.exists():
                    continue
                archive_name = download_name_for_job(job)
                if archive_name in written_names:
                    archive_name = f"{Path(archive_name).stem}_{job['id'][:8]}{Path(archive_name).suffix}"
                archive.write(model_path, archive_name)
                written_names.add(archive_name)

        if not package_path.exists() or not written_names:
            abort(404)
        return send_file(
            package_path,
            as_attachment=True,
            download_name=f"hunyuan3d_batch_{batch_id[:8]}.zip",
            mimetype="application/zip",
        )

    @app.get("/jobs/<job_id>/model")
    def view_model(job_id: str):
        job = jobs.get(job_id)
        if job is None or job.get("status") != "completed":
            abort(404)
        model_path = Path(job["model_path"])
        if not model_path.exists():
            abort(404)
        return send_file(model_path, mimetype="model/gltf-binary")

    @app.get("/jobs/<job_id>/processed/<int:image_index>")
    def processed_input(job_id: str, image_index: int):
        if job_id not in jobs:
            abort(404)
        image_path = OUTPUT_DIR / job_id / "processed_inputs" / f"{image_index:02d}.png"
        if not image_path.exists():
            abort(404)
        return send_file(image_path, mimetype="image/png")

    def set_rigging(job_id, **updates):
        with jobs_lock:
            record = dict(jobs[job_id].get("rigging") or {})
            record.update(updates)
            record["updated_at"] = time.time()
            jobs[job_id]["rigging"] = record
            save_jobs(jobs)

    def run_auto_rigging(job_id, source, directory, options):
        set_rigging(job_id, status="running", started_at=time.time(), message="자동 리깅을 준비 중입니다.")
        try:
            result = run_rigging(source, directory, options, lambda message: set_rigging(job_id, message=message))
            set_rigging(job_id, **result, status="completed", finished_at=time.time(), message="AI 리깅과 동작 프리셋이 준비됐습니다. 아래에서 동작을 재생하세요.")
        except Exception as exc:
            Path(directory).mkdir(parents=True, exist_ok=True)
            Path(directory, "error.log").write_text(traceback.format_exc())
            set_rigging(job_id, status="failed", finished_at=time.time(), message=str(exc))
        finally:
            with jobs_lock:
                gpu_load[1] = max(0, gpu_load.get(1, 0) - 1)

    @app.post("/api/jobs/<job_id>/rigging")
    def start_auto_rigging(job_id):
        job = jobs.get(job_id)
        if job is None: abort(404)
        if job.get("status") != "completed":
            return jsonify({"error": "3D 생성이 완료된 모델에서 리깅할 수 있습니다."}), 409
        if not rigging_ready():
            return jsonify({"error": "자동 리깅 모델 설치·검증이 아직 완료되지 않았습니다."}), 503
        if 1 not in gpu_load:
            return jsonify({"error": "자동 리깅에 필요한 GPU 1을 찾지 못했습니다."}), 503
        payload = request.get_json(silent=True) or {}
        if not isinstance(payload, dict): return jsonify({"error": "올바른 리깅 설정이 필요합니다."}), 400
        try:
            seed = int(payload.get("seed", 1234))
            analysis_faces = int(payload.get("analysis_faces", 50000))
        except (ValueError, TypeError): return jsonify({"error": "리깅 설정 값이 올바르지 않습니다."}), 400
        preserve = payload.get("preserve_original", True)
        if not isinstance(preserve, bool) or not 0 <= seed <= 2**32-1 or analysis_faces not in {30000,50000,100000}:
            return jsonify({"error": "지원하지 않는 리깅 설정입니다."}), 400
        source = Path(job.get("model_path", ""))
        if not source.is_file() or source.suffix.lower() != ".glb":
            return jsonify({"error": "리깅할 원본 GLB를 찾지 못했습니다."}), 400
        options = {"seed": seed, "analysis_faces": analysis_faces, "preserve_original": preserve}
        run_id = str(uuid.uuid4())
        directory = OUTPUT_DIR / job_id / "rigging" / run_id
        with jobs_lock:
            current = dict(jobs[job_id].get("rigging") or {})
            if current.get("status") in {"queued", "running"}:
                return jsonify({"error": "이 모델의 자동 리깅이 이미 진행 중입니다."}), 409
            current.update({"status":"queued", "message":"GPU 1 리깅 대기열에 등록됐습니다.", "run_id":run_id,
                            "options":options, "created_at":time.time(), "started_at":None, "finished_at":None})
            jobs[job_id]["rigging"] = current
            gpu_load[1] = gpu_load.get(1,0)+1
            save_jobs(jobs)
        rigging_executor.submit(run_auto_rigging, job_id, str(source), directory, options)
        return jsonify(public_job(jobs[job_id])["rigging"]), 202

    @app.get("/api/jobs/<job_id>/rigging")
    def auto_rigging_status(job_id):
        job = jobs.get(job_id)
        if job is None: abort(404)
        return jsonify(public_job(job)["rigging"])

    def editor_source(job_id, version=None):
        job=jobs.get(job_id)
        if job is None: abort(404)
        record=job.get("rigging") or {}
        if version is None:
            stored=record.get("model_path")
            if not stored: abort(404)
            version=Path(stored).parent.name
        try:
            if str(uuid.UUID(version))!=version: abort(404)
        except (ValueError,TypeError): abort(404)
        folder=(OUTPUT_DIR/job_id/"rigging"/version).resolve()
        if not folder.is_relative_to((OUTPUT_DIR/job_id/"rigging").resolve()) or not (folder/"rigging-result.json").is_file() or not (folder/"rigged.glb").is_file(): abort(404)
        return job,folder

    @app.get("/jobs/<job_id>/rigging/edit")
    def rigging_editor_page(job_id):
        job,folder=editor_source(job_id,request.args.get("v"))
        config={"job_id":job_id,"version":folder.name,"skeleton_url":url_for("rigging_skeleton",job_id=job_id),
                "save_url":url_for("save_rigging_edit",job_id=job_id),"status_url":url_for("auto_rigging_status",job_id=job_id),
                "versions_url":url_for("rigging_versions",job_id=job_id),"restore_url":url_for("restore_rigging_version",job_id=job_id)}
        return render_template("rigging_editor.html",job=public_job(job),editor_config=config)

    @app.get("/api/jobs/<job_id>/rigging/skeleton")
    def rigging_skeleton(job_id):
        job,folder=editor_source(job_id,request.args.get("v"))
        try: skeleton=read_skeleton(folder/"rigged.glb")
        except ValueError as exc: return jsonify({"error":str(exc)}),400
        state=folder/"editor-state.json"
        skeleton["groups"]=json.loads(state.read_text()).get("groups",[]) if state.is_file() else []
        manifest=folder/f"motion-{MOTION_VERSION}.json"
        if not state.is_file() and manifest.is_file():
            motion=json.loads(manifest.read_text())
            for n,arm in enumerate(motion.get("roles",{}).get("arms",[])):
                members=[str(arm["node"])]
                for key in members:
                    members.extend(j["id"] for j in skeleton["joints"] if j["parent"]==key)
                skeleton["groups"].append({"id":f"auto-arm-{n+1}","name":f"팔 {n+1}","type":"arm","joints":members,"amplitude":20,"phase":n%2*.5})
        skeleton.update(version=folder.name,current_version=Path(job["rigging"]["model_path"]).parent.name,
            model_url=url_for("rigging_asset",job_id=job_id)+"?v="+folder.name)
        # url_for on the shared asset function may pick any decorator; use the
        # explicit clean model path, which preserves glTF node indices.
        skeleton["model_url"]=url_for("job_detail",job_id=job_id)+"/rigging/model?v="+folder.name
        if manifest.is_file() and (folder/f"preview-{MOTION_VERSION}.glb").is_file():
            motion=json.loads(manifest.read_text())
            if motion.get("preview_model",{}).get("vertices")==motion.get("full_model",{}).get("vertices"):
                skeleton["model_url"]=url_for("job_detail",job_id=job_id)+"/rigging/action-preview?v="+folder.name
        return jsonify(skeleton)

    def run_editor_job(job_id,source,directory,edit,base_version):
        set_rigging(job_id,status="running",started_at=time.time(),message="수정 뼈대를 적용 중입니다.")
        try:
            result=run_edit(source,directory,edit,base_version,lambda message:set_rigging(job_id,message=message))
            set_rigging(job_id,**result,status="completed",finished_at=time.time(),message="수정본과 동작 미리보기가 저장됐습니다.")
        except Exception as exc:
            Path(directory).mkdir(parents=True,exist_ok=True)
            Path(directory,"error.log").write_text(traceback.format_exc())
            set_rigging(job_id,status="failed",finished_at=time.time(),message=str(exc))
        finally:
            if edit["weight_mode"]=="ai":
                with jobs_lock: gpu_load[1]=max(0,gpu_load.get(1,0)-1)

    @app.post("/api/jobs/<job_id>/rigging/edit")
    def save_rigging_edit(job_id):
        if request.content_length and request.content_length>262144: return jsonify({"error":"편집 데이터가 너무 큽니다."}),413
        payload=request.get_json(silent=True)
        if not isinstance(payload,dict): return jsonify({"error":"편집 데이터가 필요합니다."}),400
        job,folder=editor_source(job_id,payload.get("base_version"))
        if not rigging_ready(): return jsonify({"error":"리깅 환경이 준비되지 않았습니다."}),503
        try: edit=validate_edit(payload,read_skeleton(folder/"rigged.glb"))
        except ValueError as exc: return jsonify({"error":str(exc)}),400
        run_id=str(uuid.uuid4());directory=OUTPUT_DIR/job_id/"rigging"/run_id
        with jobs_lock:
            current=dict(jobs[job_id].get("rigging") or {})
            if current.get("status") in {"queued","running"}: return jsonify({"error":"리깅 작업이 이미 진행 중입니다."}),409
            version=Path(current["model_path"]).parent.name
            if payload.get("expected_version")!=version: return jsonify({"error":"다른 수정본이 저장됐습니다. 최신 결과를 불러온 뒤 다시 저장하세요."}),409
            current.update(status="queued",run_id=run_id,created_at=time.time(),started_at=None,finished_at=None,
                operation="edit",message="AI 가중치 재계산 대기 중입니다." if edit["weight_mode"]=="ai" else "수정본 저장 대기 중입니다.")
            jobs[job_id]["rigging"]=current
            if edit["weight_mode"]=="ai": gpu_load[1]=gpu_load.get(1,0)+1
            save_jobs(jobs)
        rigging_executor.submit(run_editor_job,job_id,folder/"rigged.glb",directory,edit,folder.name)
        return jsonify(public_job(jobs[job_id])["rigging"]),202

    @app.get("/api/jobs/<job_id>/rigging/versions")
    def rigging_versions(job_id):
        job=jobs.get(job_id)
        if job is None: abort(404)
        versions=[]
        for file in (OUTPUT_DIR/job_id/"rigging").glob("*/rigging-result.json"):
            try:
                if str(uuid.UUID(file.parent.name))!=file.parent.name or not (file.parent/"rigged.glb").is_file(): continue
                info=json.loads(file.read_text())
                versions.append({"version":file.parent.name,"created_at":file.stat().st_mtime,"bone_count":info.get("bone_count"),
                                 "edited":bool(info.get("edited")),"weight_mode":info.get("weight_mode","ai"),"base_version":info.get("base_version")})
            except (ValueError,OSError): continue
        return jsonify({"current_version":Path(job["rigging"]["model_path"]).parent.name if (job.get("rigging") or {}).get("model_path") else None,
                        "versions":sorted(versions,key=lambda v:v["created_at"],reverse=True)})

    @app.post("/api/jobs/<job_id>/rigging/restore")
    def restore_rigging_version(job_id):
        payload=request.get_json(silent=True) or {}
        if not isinstance(payload,dict): return jsonify({"error":"버전 정보가 필요합니다."}),400
        job,folder=editor_source(job_id,payload.get("version"))
        with jobs_lock:
            current=job.get("rigging") or {}
            if current.get("status") in {"queued","running"}: return jsonify({"error":"작업 완료 후 이전 버전을 복원하세요."}),409
            if payload.get("expected_version")!=Path(current["model_path"]).parent.name: return jsonify({"error":"최신 결과를 다시 불러오세요."}),409
            info=json.loads((folder/"rigging-result.json").read_text())
            info.update(model_path=str(folder/"rigged.glb"),preview_path=str(folder/"rig-preview.glb"),run_id=folder.name,
                        status="completed",message="선택한 리깅 버전을 복원했습니다.",updated_at=time.time(),finished_at=time.time())
            job["rigging"]=info;save_jobs(jobs)
        return jsonify(public_job(job)["rigging"])

    @app.get("/jobs/<job_id>/rigging/model")
    @app.get("/jobs/<job_id>/rigging/preview")
    @app.get("/jobs/<job_id>/rigging/download")
    @app.get("/jobs/<job_id>/rigging/motion")
    @app.get("/jobs/<job_id>/rigging/motion-download")
    @app.get("/jobs/<job_id>/rigging/action-preview")
    def rigging_asset(job_id):
        job = jobs.get(job_id)
        if job is None: abort(404)
        record = job.get("rigging") or {}
        preview = request.path.endswith("/preview")
        motion = request.path.endswith(("/motion", "/motion-download", "/action-preview"))
        stored = record.get("preview_path" if preview else "model_path")
        version = request.args.get("v")
        if version:
            try:
                if str(uuid.UUID(version)) != version: abort(404)
            except ValueError: abort(404)
            folder = OUTPUT_DIR / job_id / "rigging" / version
            if not (folder / "rigging-result.json").is_file(): abort(404)
            stored = str(folder / ("rig-preview.glb" if preview else "rigged.glb"))
        if motion:
            if not stored: abort(404)
            folder = Path(stored).parent
            manifest = folder / f"motion-{MOTION_VERSION}.json"
            if not manifest.is_file(): abort(404)
            stored = str(folder / (f"preview-{MOTION_VERSION}.glb" if request.path.endswith("/action-preview") else f"rig-{MOTION_VERSION}.glb"))
        if not stored: abort(404)
        path = Path(stored).resolve()
        if not path.is_relative_to((OUTPUT_DIR / job_id / "rigging").resolve()) or not path.is_file(): abort(404)
        return send_file(path, mimetype="model/gltf-binary", as_attachment=request.path.endswith(("/download", "/motion-download")),
                         download_name=f"{'animated' if motion else 'rigged'}_{job_id[:8]}.glb")

    @app.get("/jobs/<job_id>/mixamo/package")
    def download_mixamo_package(job_id: str):
        job = jobs.get(job_id)
        if job is None or job.get("status") != "completed":
            abort(404)
        package_path = build_mixamo_package(job)
        return send_file(
            package_path,
            as_attachment=True,
            download_name=f"mixamo_upload_{job_id[:8]}.zip",
            mimetype="application/zip",
        )

    @app.post("/jobs/<job_id>/mixamo/upload")
    def upload_mixamo_result(job_id: str):
        job = jobs.get(job_id)
        if job is None or job.get("status") != "completed":
            abort(404)
        file = request.files.get("mixamo_file")
        if file is None or not file.filename:
            abort(400, "Upload a Mixamo result file.")
        filename = secure_filename(file.filename)
        extension = filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
        if extension not in MIXAMO_ALLOWED_EXTENSIONS:
            abort(400, "Upload FBX, GLB, GLTF, or DAE exported from Mixamo.")

        mixamo_dir = mixamo_output_dir(job_id)
        mixamo_dir.mkdir(parents=True, exist_ok=True)
        saved_name = unique_filename(mixamo_dir, filename)
        file.save(mixamo_dir / saved_name)
        set_job(job_id, mixamo_files=[item["name"] for item in mixamo_files_for_job(job_id)])
        return redirect(url_for("job_detail", job_id=job_id))

    @app.get("/jobs/<job_id>/mixamo/files/<filename>")
    def download_mixamo_result(job_id: str, filename: str):
        job = jobs.get(job_id)
        if job is None:
            abort(404)
        safe_name = secure_filename(filename)
        file_path = mixamo_output_dir(job_id) / safe_name
        if not file_path.exists() or file_path.name != safe_name:
            abort(404)
        return send_file(file_path, as_attachment=True, download_name=safe_name)

    def capability_facts():
        diagnostics = runtime_diagnostics()
        # available_gpu_ids() deliberately falls back to [0] for the old web
        # form. Diagnostics must never mistake that fallback for a real GPU.
        count = diagnostics.get("cuda_device_count", 0) if diagnostics.get("cuda_available") else 0
        gpu_ids = list(range(count)) if isinstance(count, int) and count > 0 else []
        defaults = generation_defaults()
        shape_repo = resolve_repo_env_path(os.getenv("HUNYUAN3D_REPO"))
        shape_root = resolve_repo_env_path(os.getenv("HY3DGEN_MODELS"))
        shape_weights = shape_root / os.getenv("HUNYUAN_MODEL_PATH", "tencent/Hunyuan3D-2.1") / os.getenv("HUNYUAN_SUBFOLDER", "hunyuan3d-dit-v2-1") if shape_root else None
        shape_files = bool(shape_weights and any((shape_weights / name).is_file() for name in
                           ["model.fp16.safetensors", "model.fp16.ckpt"]))
        shape_configured = bool(shape_repo and (shape_repo / "hy3dshape").is_dir())
        omni_root = resolve_repo_env_path(os.getenv("HUNYUAN_OMNI_MODEL_PATH"))
        use_ema = omni_ema_ready() and env_bool("HUNYUAN_OMNI_USE_EMA", True)
        omni_file = "pytorch_model_ema.bin" if use_ema else "pytorch_model.bin"
        omni_files = bool(omni_root and (omni_root / "model" / omni_file).is_file())
        configured = {"hunyuan3d-2.1": shape_configured and shape_files,
                      "hunyuan3d-omni": not omni_dependency_issues() and omni_files,
                      "trellis2": trellis_ready()}
        models = {}
        for name, ready in configured.items():
            reasons = []
            if not gpu_ids:
                reasons.append("cuda_unavailable")
            if not ready:
                reasons.append("local_model_configuration_or_files_missing")
            models[name] = {"available": bool(ready and gpu_ids), "configured": bool(ready),
                            "unavailable_reasons": reasons,
                            "readiness": "configured" if ready else "setup_or_download_required",
                            "automatic_download_may_be_required": not ready and name != "trellis2"}
        return {"defaults": defaults, "gpu_ids": gpu_ids, "gpu_stats": gpu_stats(),
                "shape_gpu": env_int("HUNYUAN_SHAPE_GPU", 0),
                "texture_gpu": env_int("HUNYUAN_TEXTURE_GPU", 1),
                "default_gpu": default_gpu_id(gpu_ids) if gpu_ids else None,
                "gpu_modes": {name: default_gpu_mode_for_model(name) for name in models},
                "model_readiness": models,
                "trellis_defaults": trellis_generation_defaults(default_gpu_mode_for_model("trellis2")),
                "trellis_dual_ready": trellis_dual_ready(), "texture_ready": texture_dependencies_ready(),
                "texture_profile": os.getenv("HUNYUAN_DEFAULT_TEXTURE_PROFILE", "detail"),
                "rigging_ready": rigging_ready(), "workers": env_int("GENERATION_WORKERS", 2),
                "limits": {"max_upload_files": env_int("MAX_UPLOAD_FILES", 15),
                           "max_upload_bytes": app.config["MAX_CONTENT_LENGTH"],
                           "upload_limit_scope": "Application limit; the public gateway may impose a smaller request limit.",
                           "allowed_extensions": sorted(ALLOWED_EXTENSIONS), "history_page_max": 200}}

    @app.get("/api/capabilities")
    def api_capabilities():
        return jsonify(build_capabilities(capability_facts()))

    @app.get("/api/doctor")
    def api_doctor():
        facts = capability_facts()
        with jobs_lock:
            queue = dict(gpu_load)
            job_count = len(jobs)
            rigging_tasks = sum((job.get("rigging") or {}).get("status") in {"queued", "running"} for job in jobs.values())
        try:
            usage = shutil.disk_usage(INSTANCE_DIR)
            storage = {"writable": os.access(INSTANCE_DIR, os.W_OK), "free_bytes": usage.free}
        except OSError:
            storage = {"writable": False, "free_bytes": 0}
        result = build_doctor(build_capabilities(facts), facts, storage, queue, job_count, rigging_tasks)
        if not result["ok"]:
            result.update(error_payload("SERVER_NOT_READY", "Server readiness checks need attention."))
        return jsonify(result)

    @app.get("/health")
    def health():
        with jobs_lock:
            queue_load = dict(gpu_load)
        return jsonify({"status": "ok", "jobs": len(jobs), "gpu_stats": gpu_stats(), "gpu_queue_load": queue_load,
                        "generation_workers": env_int("GENERATION_WORKERS", 2),
                        "default_gpu_mode": default_gpu_mode_for_model(default_generation_model()),
                        "generation_defaults": generation_defaults(),
                        "default_texture_profile": os.getenv("HUNYUAN_DEFAULT_TEXTURE_PROFILE", "detail"),
                        "omni_ema_ready": omni_ema_ready(), "trellis_ready": trellis_ready(), "rigging_ready": rigging_ready(), "rigging_gpu": 1,
                        "rigging_tasks":sum((j.get("rigging") or {}).get("status") in {"queued","running"} for j in jobs.values()), **runtime_diagnostics()})

    def public_job(job: dict) -> dict:
        result = dict(job)
        result.update(web_url=url_for("job_detail", job_id=job["id"]),
                      status_url=url_for("job_status", job_id=job["id"]),
                      archive_url=url_for("job_archive", job_id=job["id"]),
                      record_url=url_for("job_record", job_id=job["id"]))
        if job.get("status") == "failed":
            result["failure"] = error_payload("JOB_FAILED", "This generation failed; inspect its message and retained diagnostics.",
                                              request_id=job.get("request_id"), details={"job_id": job["id"]})
        rig = dict(job.get("rigging") or {})
        result["rigging"] = {key:value for key,value in rig.items() if key not in {"model_path","preview_path"}}
        result["rigging"].setdefault("status", "idle")
        result["rigging"]["ready"] = rigging_ready()
        result["rigging"]["start_url"] = url_for("start_auto_rigging", job_id=job["id"])
        result["rigging"]["status_url"] = url_for("auto_rigging_status", job_id=job["id"])
        if rig.get("model_path") and Path(rig["model_path"]).is_file():
            rig_base = url_for("job_detail", job_id=job["id"]) + "/rigging"
            version = Path(rig["model_path"]).parent.name
            result["rigging"]["editor_url"]=url_for("rigging_editor_page",job_id=job["id"])
            result["rigging"].update(model_url=rig_base+"/model?v="+version, download_url=rig_base+"/download?v="+version, preview_url=rig_base+"/preview?v="+version)
            manifest = Path(rig["model_path"]).parent / f"motion-{MOTION_VERSION}.json"
            if manifest.is_file():
                try:
                    motion = json.loads(manifest.read_text())
                    result["rigging"].update(motion_clips=motion["clips"], motion_method=motion["method"],
                        motion_download_url=rig_base+"/motion-download?v="+version,
                        preview_url=rig_base+"/action-preview?v="+version+"&motion="+MOTION_VERSION,
                        preview_faces=motion["preview_model"]["faces"])
                except (ValueError, KeyError):
                    app.logger.exception("Invalid motion manifest for job %s", job["id"])
        result["elapsed_seconds"] = elapsed_seconds(job)
        result["warnings"] = job.get("warnings") or read_warnings(job)
        result["display_name"] = display_name_for_job(job)
        if result["warnings"] and job.get("options", {}).get("texture") and not job.get("used_texture"):
            result["message"] = "Model is ready, but texture generation failed. Showing untextured shape."
        if job.get("status") == "completed":
            result["download_url"] = url_for("download_model", job_id=job["id"])
            result["model_url"] = url_for("view_model", job_id=job["id"])
            result["mixamo_package_url"] = url_for("download_mixamo_package", job_id=job["id"])
            result["mixamo_upload_url"] = url_for("upload_mixamo_result", job_id=job["id"])
            result["mixamo_url"] = "https://www.mixamo.com/#/?page=1&type=Character"
            result["mixamo_files"] = [
                {
                    **item,
                    "download_url": url_for("download_mixamo_result", job_id=job["id"], filename=item["name"]),
                }
                for item in mixamo_files_for_job(job["id"])
            ]
        if (OUTPUT_DIR / job["id"] / "processed_inputs" / "01.png").exists():
            result["processed_input_url"] = url_for("processed_input", job_id=job["id"], image_index=1)
        result["saved"] = bool(job.get("saved"))
        saved_at = job.get("saved_at")
        result["saved_at"] = saved_at
        result["saved_at_label"] = format_timestamp(saved_at)
        result["created_at_label"] = format_timestamp(job.get("created_at"))
        return safe_public(result)

    return app


def jobs_for_batch(batch_id: str, all_jobs: dict) -> list[dict]:
    return sorted(
        [job for job in all_jobs.values() if job.get("batch_id") == batch_id],
        key=lambda item: (item.get("batch_index", 0), item.get("created_at", 0)),
    )


def format_timestamp(value: float | None) -> str:
    if not value:
        return ""
    return datetime.fromtimestamp(value).strftime("%Y-%m-%d %H:%M")


def is_job_saved(job: dict) -> bool:
    return bool(job.get("saved"))


def list_saved_jobs(all_jobs: dict) -> list[dict]:
    saved_jobs = [
        job
        for job in all_jobs.values()
        if is_job_saved(job) and job.get("status") == "completed" and job_model_exists(job)
    ]
    return sorted(
        saved_jobs,
        key=lambda item: item.get("saved_at") or item.get("finished_at") or item.get("created_at", 0),
        reverse=True,
    )


def job_model_exists(job: dict) -> bool:
    model_path = job.get("model_path")
    if not model_path:
        return False
    return Path(model_path).exists()


def cleanup_unsaved_jobs(all_jobs: dict) -> None:
    retention_days = env_int("HUNYUAN_UNSAVED_JOB_RETENTION_DAYS", 0)
    if retention_days <= 0:
        return

    cutoff = time.time() - retention_days * 86400
    removed = False
    for job_id, job in list(all_jobs.items()):
        if is_job_saved(job):
            continue
        if job.get("status") not in {"completed", "failed"}:
            continue
        finished_at = job.get("finished_at") or job.get("updated_at") or job.get("created_at", 0)
        if finished_at >= cutoff:
            continue
        delete_job_artifacts(job_id)
        all_jobs.pop(job_id, None)
        removed = True

    if removed:
        save_jobs(all_jobs)


def delete_job_artifacts(job_id: str) -> None:
    for directory in [OUTPUT_DIR / job_id, UPLOAD_DIR / job_id]:
        if directory.exists():
            shutil.rmtree(directory, ignore_errors=True)


def display_name_for_job(job: dict) -> str:
    filename = job.get("source_filename")
    if filename:
        return Path(filename).stem or filename
    if job.get("batch_index") and job.get("batch_total"):
        return f"image_{job['batch_index']:02d}"
    return job.get("id", "job")[:8]


def download_name_for_job(job: dict) -> str:
    stem = re.sub(r"[^A-Za-z0-9_.-]+", "_", display_name_for_job(job)).strip("._")
    if not stem:
        stem = f"hunyuan3d_{job['id'][:8]}"
    return f"{stem}_{job['id'][:8]}.glb"


def mixamo_output_dir(job_id: str) -> Path:
    return OUTPUT_DIR / job_id / "mixamo"


def unique_filename(directory: Path, filename: str) -> str:
    candidate = filename
    stem = Path(filename).stem
    suffix = Path(filename).suffix
    counter = 2
    while (directory / candidate).exists():
        candidate = f"{stem}_{counter}{suffix}"
        counter += 1
    return candidate


def mixamo_files_for_job(job_id: str) -> list[dict]:
    directory = mixamo_output_dir(job_id)
    if not directory.exists():
        return []

    files = []
    for path in sorted(directory.iterdir(), key=lambda item: item.stat().st_mtime, reverse=True):
        if not path.is_file():
            continue
        extension = path.suffix.lower().lstrip(".")
        if extension not in MIXAMO_ALLOWED_EXTENSIONS:
            continue
        files.append(
            {
                "name": path.name,
                "extension": extension.upper(),
                "size_kb": max(1, round(path.stat().st_size / 1024)),
            }
        )
    return files


def build_mixamo_package(job: dict) -> Path:
    job_id = job["id"]
    output_dir = OUTPUT_DIR / job_id
    mixamo_dir = mixamo_output_dir(job_id)
    mixamo_dir.mkdir(parents=True, exist_ok=True)
    package_path = mixamo_dir / "mixamo_upload_package.zip"

    sources: list[tuple[Path, str]] = []
    model_path = Path(job["model_path"])
    if model_path.exists():
        sources.append((model_path, f"hunyuan3d_{job_id[:8]}{model_path.suffix}"))

    for name in [
        "model_textured.obj",
        "model_textured.mtl",
        "model_textured.jpg",
        "model_textured_metallic.jpg",
        "model_textured_roughness.jpg",
        "white_mesh_remesh.obj",
    ]:
        path = output_dir / name
        if path.exists():
            sources.append((path, name))

    if not any(archive_name.lower().endswith(".obj") for _, archive_name in sources):
        converted_obj = export_mixamo_obj(model_path, mixamo_dir / f"hunyuan3d_{job_id[:8]}_mixamo.obj")
        if converted_obj is not None:
            sources.append((converted_obj, converted_obj.name))

    if not sources:
        abort(404)

    readme = f"""Mixamo upload package for job {job_id}

1. Open https://www.mixamo.com/
2. Upload this ZIP, or upload the OBJ/GLB inside it if Mixamo rejects ZIP.
3. Complete Auto-Rigger marker placement in Mixamo.
4. Choose an animation and download FBX.
5. Upload the downloaded FBX back to this job page.

Note: Mixamo is an Adobe web service. This app does not store Adobe credentials or automate login.
"""
    with zipfile.ZipFile(package_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("README_MIXAMO.txt", readme)
        written_names = {"README_MIXAMO.txt"}
        for source, archive_name in sources:
            if archive_name in written_names:
                continue
            archive.write(source, archive_name)
            written_names.add(archive_name)

    return package_path


def export_mixamo_obj(source_path: Path, output_path: Path) -> Path | None:
    if not source_path.exists():
        return None
    try:
        import trimesh

        loaded = trimesh.load(source_path, force="scene")
        if isinstance(loaded, trimesh.Scene):
            meshes = [mesh for mesh in loaded.geometry.values() if isinstance(mesh, trimesh.Trimesh)]
            if not meshes:
                return None
            mesh = trimesh.util.concatenate(meshes)
        else:
            mesh = loaded
        mesh.export(output_path)
    except Exception:
        return None
    return output_path if output_path.exists() else None


def form_int(name: str, default: int, minimum: int, maximum: int) -> int:
    try:
        value = int(request.form.get(name, default))
    except ValueError:
        return default
    return max(minimum, min(maximum, value))


def form_float(name: str, default: float, minimum: float, maximum: float) -> float:
    try:
        value = float(request.form.get(name, default))
    except ValueError:
        return default
    return max(minimum, min(maximum, value))


def texture_dependency_issues() -> list[str]:
    repo_path = resolve_repo_env_path(os.getenv("HUNYUAN3D_REPO"))
    if repo_path is None:
        return ["HUNYUAN3D_REPO"]

    issues = []
    if not (repo_path / "hy3dpaint" / "ckpt" / "RealESRGAN_x4plus.pth").exists():
        issues.append("RealESRGAN checkpoint")
    if not custom_rasterizer_ready(repo_path):
        issues.append("custom rasterizer")
    return issues


def texture_dependencies_ready() -> bool:
    return not texture_dependency_issues()


def trellis_ready() -> bool:
    marker = BASE_DIR / "logs/trellis-a100-ready.json"
    if not marker.exists():
        return False
    try:
        model_dir = Path(os.getenv("TRELLIS_MODEL_DIR", str(BASE_DIR / "vendor/TRELLIS.2/local_models/TRELLIS.2-4B")))
        return (json.loads(marker.read_text()).get("status") == "ready"
                and (BASE_DIR / ".venv-trellis/bin/python").exists()
                and (model_dir / "pipeline-a100.json").exists())
    except (OSError, ValueError):
        return False


def omni_ema_ready() -> bool:
    model = resolve_repo_env_path(os.getenv("HUNYUAN_OMNI_MODEL_PATH"))
    return model is not None and (model / "model" / "pytorch_model_ema.bin").is_file()


def omni_dependency_issues() -> list[str]:
    repo_path = resolve_repo_env_path(os.getenv("HUNYUAN_OMNI_REPO"))
    if repo_path is None:
        return ["HUNYUAN_OMNI_REPO"]
    issues = []
    if not repo_path.exists():
        issues.append("HUNYUAN_OMNI_REPO path")
        return issues
    if not (repo_path / "hy3dshape").exists():
        issues.append("Omni hy3dshape")
    if not (repo_path / "inference.py").exists():
        issues.append("Omni inference.py")
    return issues


def multiview_shape_ready() -> bool:
    model_path = os.getenv("HUNYUAN_MODEL_PATH", "")
    subfolder = os.getenv("HUNYUAN_SUBFOLDER", "")
    return "mv" in model_path.lower() or "mv" in subfolder.lower()


def load_jobs() -> dict:
    if not JOBS_FILE.exists():
        return {}
    try:
        data = json.loads(JOBS_FILE.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}

    jobs = {}
    for job_id, job in data.items():
        if job.get("status") in {"queued", "running"}:
            job["status"] = "failed"
            job["message"] = "The server restarted before this job finished."
            job["finished_at"] = job.get("updated_at")
        rig = job.get("rigging")
        if rig and rig.get("status") in {"queued", "running"}:
            rig.update(status="failed", message="서버 재시작으로 리깅이 중단됐습니다. 다시 실행하세요.", finished_at=time.time())
        jobs[job_id] = job
    return jobs


def elapsed_seconds(job: dict) -> int:
    start = job.get("started_at") or job.get("created_at")
    if not start:
        return 0
    end = job.get("finished_at")
    if end is None and job.get("status") in {"completed", "failed"}:
        end = job.get("updated_at")
    if end is None:
        end = time.time()
    return max(0, int(end - start))


def read_warnings(job: dict) -> list[str]:
    job_id = job.get("id")
    if not job_id:
        return []
    warning_path = OUTPUT_DIR / job_id / "warnings.log"
    if not warning_path.exists():
        return []
    return [line.strip() for line in warning_path.read_text(encoding="utf-8").splitlines() if line.strip()]


def save_jobs(jobs: dict) -> None:
    JOBS_FILE.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = JOBS_FILE.with_suffix(".tmp")
    tmp_path.write_text(json.dumps(jobs, indent=2), encoding="utf-8")
    tmp_path.replace(JOBS_FILE)


def runtime_diagnostics() -> dict:
    diagnostics = {
        "python_executable": sys.executable,
        "torch_available": False,
        "cuda_available": False,
    }
    try:
        import torch

        diagnostics["torch_available"] = True
        diagnostics["torch_version"] = torch.__version__
        diagnostics["cuda_available"] = bool(torch.cuda.is_available())
        if torch.cuda.is_available():
            diagnostics["cuda_device_count"] = torch.cuda.device_count()
    except Exception as exc:
        diagnostics["torch_error"] = str(exc)
    return diagnostics


app = create_app()


def parse_cli_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Hunyuan3D Flask Web App")
    parser.add_argument(
        "--host",
        default=None,
        help="Bind address (default: FLASK_HOST env or 127.0.0.1)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=None,
        help="Listen port (default: FLASK_PORT env or 5001)",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        default=None,
        help="Enable Flask debug mode (overrides FLASK_DEBUG env)",
    )
    return parser.parse_args()


if __name__ == "__main__":
    cli = parse_cli_args()
    host = cli.host or os.getenv("FLASK_HOST", "127.0.0.1")
    port = cli.port if cli.port is not None else env_int("FLASK_PORT", 5001)
    debug = cli.debug if cli.debug is not None else env_bool("FLASK_DEBUG", False)
    print("Starting Hunyuan3D Flask Web App", flush=True)
    print(f"Open: http://{host}:{port}", flush=True)
    print("Press Ctrl+C to stop the server.", flush=True)
    app.run(
        host=host,
        port=port,
        debug=debug,
        use_reloader=False,
    )
