from __future__ import annotations

import argparse
import json
import os
import re
import sys
import threading
import time
import traceback
import uuid
import zipfile
from concurrent.futures import ThreadPoolExecutor
from importlib.util import find_spec
from pathlib import Path

from dotenv import load_dotenv
from flask import Flask, abort, jsonify, redirect, render_template, request, send_file, url_for
from werkzeug.utils import secure_filename

from hunyuan_service import GenerationOptions, Hunyuan3DGenerator


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

    for path in [INSTANCE_DIR, UPLOAD_DIR, OUTPUT_DIR]:
        path.mkdir(parents=True, exist_ok=True)

    jobs_lock = threading.Lock()
    jobs = load_jobs()
    executor = ThreadPoolExecutor(max_workers=env_int("GENERATION_WORKERS", 1))
    generator = Hunyuan3DGenerator(
        repo_path=os.getenv("HUNYUAN3D_REPO") or None,
        model_path=os.getenv("HUNYUAN_MODEL_PATH", "tencent/Hunyuan3D-2.1"),
        subfolder=os.getenv("HUNYUAN_SUBFOLDER", "hunyuan3d-dit-v2-1"),
        device=os.getenv("HUNYUAN_DEVICE", "cuda"),
        low_vram=env_bool("HUNYUAN_LOW_VRAM", False),
        enable_flashvdm=env_bool("HUNYUAN_ENABLE_FLASHVDM", False),
        compile_model=env_bool("HUNYUAN_COMPILE", False),
        texture_views=env_int("HUNYUAN_TEXTURE_VIEWS", 8),
        texture_resolution=env_int("HUNYUAN_TEXTURE_RESOLUTION", 512),
        texture_render_size=env_int("HUNYUAN_TEXTURE_RENDER_SIZE", 4096),
        texture_texture_size=env_int("HUNYUAN_TEXTURE_MAP_SIZE", 8192),
        texture_view_selection_resolution=env_int("HUNYUAN_TEXTURE_VIEW_SELECTION_RESOLUTION", 512),
        texture_render_device=os.getenv("HUNYUAN_TEXTURE_RENDER_DEVICE", "cuda"),
        hy3dgen_models_dir=os.getenv("HY3DGEN_MODELS") or None,
        local_model_dir=os.getenv("HUNYUAN_LOCAL_MODEL_DIR") or None,
        omni_repo_path=os.getenv("HUNYUAN_OMNI_REPO") or None,
        omni_model_path=os.getenv("HUNYUAN_OMNI_MODEL_PATH", "tencent/Hunyuan3D-Omni"),
        omni_control_type=os.getenv("HUNYUAN_OMNI_CONTROL_TYPE", "bbox"),
        omni_bbox=os.getenv("HUNYUAN_OMNI_BBOX") or None,
        omni_enable_flashvdm=env_bool("HUNYUAN_OMNI_FLASHVDM", False),
    )

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
            asset = generator.generate(
                [Path(path) for path in image_paths],
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
        return render_template(
            "index.html",
            recent_jobs=[public_job(job) for job in visible_recent_jobs],
            max_files=env_int("MAX_UPLOAD_FILES", 15),
            gpu_ids=gpu_ids,
            default_gpu_id=default_gpu_id(gpu_ids),
            texture_ready=not texture_issues,
            texture_issues=texture_issues,
            omni_ready=not omni_issues,
            omni_issues=omni_issues,
            multiview_ready=multiview_shape_ready(),
        )

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
            filename = secure_filename(file.filename)
            extension = filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
            if extension not in ALLOWED_EXTENSIONS:
                abort(400, f"Unsupported image type: {filename}")
            validated_files.append((file, filename))

        model = request.form.get("model", "hunyuan3d-2.1")
        if model not in {"hunyuan3d-2.1", "hunyuan3d-omni"}:
            abort(400, "Unsupported model selection.")
        if model == "hunyuan3d-omni" and omni_dependency_issues():
            abort(400, f"Hunyuan3D-Omni is not ready: {', '.join(omni_dependency_issues())}")

        texture_requested = request.form.get("texture") == "on"
        options = GenerationOptions(
            model=model,
            gpu_id=form_gpu_id(available_gpu_ids()),
            texture=texture_requested and texture_dependencies_ready(),
            remove_background=request.form.get("remove_background") == "on",
            seed=form_int("seed", 1234, 0, 2**32 - 1),
            num_inference_steps=form_int("num_inference_steps", 50, 1, 100),
            guidance_scale=form_float("guidance_scale", 7.5, 0.1, 20.0),
            octree_resolution=form_int("octree_resolution", 512, 64, 512),
            num_chunks=form_int("num_chunks", 200000, 1000, 300000),
            output_format="glb",
        )

        now = time.time()
        batch_id = str(uuid.uuid4()) if len(uploaded_files) > 1 else None
        created_jobs = []
        with jobs_lock:
            for index, (file, filename) in enumerate(validated_files, start=1):
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
                    "source_filename": filename,
                    "batch_index": index,
                    "batch_total": len(uploaded_files),
                    "options": options.__dict__,
                }
                if batch_id is not None:
                    job["batch_id"] = batch_id
                jobs[job_id] = job
                created_jobs.append(job)
            save_jobs(jobs)

        for job in created_jobs:
            executor.submit(run_generation, job["id"], job["image_paths"], options)

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

    @app.get("/jobs/<job_id>")
    def job_detail(job_id: str):
        job = jobs.get(job_id)
        if job is None:
            abort(404)
        return render_template("job.html", job=public_job(job))

    @app.get("/api/jobs/<job_id>")
    def job_status(job_id: str):
        job = jobs.get(job_id)
        if job is None:
            abort(404)
        return jsonify(public_job(job))

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

    @app.get("/health")
    def health():
        return jsonify({"status": "ok", "jobs": len(jobs), **runtime_diagnostics()})

    def public_job(job: dict) -> dict:
        result = dict(job)
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
        return result

    return app


def jobs_for_batch(batch_id: str, all_jobs: dict) -> list[dict]:
    return sorted(
        [job for job in all_jobs.values() if job.get("batch_id") == batch_id],
        key=lambda item: (item.get("batch_index", 0), item.get("created_at", 0)),
    )


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
