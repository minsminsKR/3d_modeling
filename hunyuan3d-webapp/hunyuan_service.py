from __future__ import annotations

import contextlib
import gc
import importlib.metadata
import importlib.resources
import json
import os
import random
import shutil
import subprocess
import sys
import types
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable, Iterable


class Hunyuan3DError(RuntimeError):
    """Raised when Hunyuan3D generation cannot be completed."""


@dataclass(frozen=True)
class GenerationOptions:
    model: str = "hunyuan3d-2.1"
    gpu_id: int = 0
    texture: bool = False
    remove_background: bool = True
    seed: int = 1234
    num_inference_steps: int = 50
    guidance_scale: float = 7.5
    octree_resolution: int = 512
    num_chunks: int = 200000
    output_format: str = "glb"


@dataclass(frozen=True)
class GeneratedAsset:
    model_path: Path
    metadata_path: Path
    used_texture: bool
    warnings: tuple[str, ...] = ()


def is_windows_absolute_path(value: str) -> bool:
    return len(value) >= 3 and value[1] == ":" and value[2] in {"\\", "/"}


def resolve_config_path(value: str | None) -> Path | None:
    if not value:
        return None
    path = Path(value).expanduser()
    if path.is_absolute():
        return path.resolve()
    if is_windows_absolute_path(value):
        return path
    return path.resolve()


def parse_bbox(value: str | None) -> list[float]:
    if not value:
        return [1.8, 1.8, 1.8]
    parts = [part.strip() for part in value.split(",")]
    values = [float(part) for part in parts]
    if len(values) == 3:
        return values
    if len(values) == 6:
        x_min, y_min, z_min, x_max, y_max, z_max = values
        return [abs(x_max - x_min), abs(y_max - y_min), abs(z_max - z_min)]
    raise Hunyuan3DError("HUNYUAN_OMNI_BBOX must contain either 3 size values or 6 min/max values.")


def custom_rasterizer_search_paths(repo_path: Path) -> list[Path]:
    build_root = repo_path / "hy3dpaint" / "custom_rasterizer" / "build"
    search_paths: list[Path] = [repo_path / "hy3dpaint" / "custom_rasterizer"]
    if not build_root.exists():
        return search_paths
    search_paths.extend(path for path in build_root.iterdir() if path.is_dir())
    return search_paths


@contextlib.contextmanager
def pushd(path: Path):
    old_cwd = Path.cwd()
    os.chdir(path)
    try:
        yield
    finally:
        os.chdir(old_cwd)


class Hunyuan3DGenerator:
    """Lazy wrapper around the official Hunyuan3D 2.1 Python API.

    Quality-first defaults tuned for a single RTX 3090 Ti (24 GB). Paint uses
    eight views, 512 multiview resolution (2.1 README), render_size 4096 and
    texture_size 8192 for sharper PBR maps (slower than official 2048/4096).
    """

    def __init__(
        self,
        repo_path: str | None = None,
        model_path: str = "tencent/Hunyuan3D-2.1",
        subfolder: str = "hunyuan3d-dit-v2-1",
        device: str = "cuda",
        low_vram: bool = False,
        enable_flashvdm: bool = False,
        compile_model: bool = False,
        texture_views: int = 8,
        texture_resolution: int = 512,
        texture_render_size: int = 4096,
        texture_texture_size: int = 8192,
        texture_view_selection_resolution: int = 512,
        texture_render_device: str = "cuda",
        hy3dgen_models_dir: str | None = None,
        local_model_dir: str | None = None,
        omni_repo_path: str | None = None,
        omni_model_path: str = "tencent/Hunyuan3D-Omni",
        omni_control_type: str = "bbox",
        omni_bbox: str | None = None,
        omni_enable_flashvdm: bool = False,
    ) -> None:
        self.repo_path = resolve_config_path(repo_path)
        self.omni_repo_path = resolve_config_path(omni_repo_path)
        self.model_path = model_path
        self.subfolder = subfolder
        self.omni_model_path = omni_model_path
        self.omni_control_type = omni_control_type
        self.omni_bbox = parse_bbox(omni_bbox)
        self.omni_enable_flashvdm = omni_enable_flashvdm
        self.device = device
        self.low_vram = low_vram
        self.enable_flashvdm = enable_flashvdm
        self.compile_model = compile_model
        self.texture_views = texture_views
        self.texture_resolution = texture_resolution
        self.texture_render_size = texture_render_size
        self.texture_texture_size = texture_texture_size
        self.texture_view_selection_resolution = texture_view_selection_resolution
        self.texture_render_device = texture_render_device
        self.hy3dgen_models_dir = (
            resolve_config_path(hy3dgen_models_dir) if hy3dgen_models_dir else None
        )
        self.local_model_dir = (
            resolve_config_path(local_model_dir) if local_model_dir else None
        )
        self._shape_pipeline = None
        self._shape_pipeline_device = None
        self._paint_pipeline = None
        self._background_remover = None

    def generate(
        self,
        image_paths: Iterable[Path],
        output_dir: Path,
        options: GenerationOptions,
        progress: Callable[[str], None] | None = None,
    ) -> GeneratedAsset:
        image_paths = [Path(path) for path in image_paths]
        if not image_paths:
            raise Hunyuan3DError("At least one image is required.")

        output_dir.mkdir(parents=True, exist_ok=True)
        self._progress(progress, "Preparing Hunyuan3D repository...")
        self._configure_repo()
        self._seed_everything(options.seed)

        with pushd(self.repo_path):
            warnings = []
            self._progress(progress, "Loading and preprocessing input image(s)...")
            images = self._load_images(image_paths, options.remove_background)
            self._save_processed_inputs(images, output_dir)
            self._progress(progress, "Generating shape mesh with Hunyuan3D...")
            mesh = self._generate_shape(images, output_dir, options, warnings, progress)
            final_path = mesh
            used_texture = False

            if options.texture:
                try:
                    self._release_shape_pipeline()
                    self._progress(progress, "Generating texture maps...")
                    final_path = self._generate_texture(mesh, images, output_dir, options.gpu_id)
                    used_texture = True
                except Exception as exc:
                    # Texture generation is much more fragile on Windows/CUDA setups.
                    # Keep the untextured mesh instead of discarding the whole job.
                    warning = f"Texture generation failed: {exc}"
                    warnings.append(warning)
                    self._write_warning(output_dir, warning)

            normalized_path = output_dir / f"model.{options.output_format}"
            self._progress(progress, "Exporting final model...")
            if final_path.resolve() != normalized_path.resolve():
                shutil.copyfile(final_path, normalized_path)

            metadata_path = output_dir / "metadata.json"
            metadata = {
                "source_images": [str(path) for path in image_paths],
                "model_path": str(normalized_path),
                "used_texture": used_texture,
                "warnings": warnings,
                "options": asdict(options),
                "note": self._metadata_note(images, options),
            }
            metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
            self._empty_cuda_cache()
            return GeneratedAsset(normalized_path, metadata_path, used_texture, tuple(warnings))

    def _progress(self, progress: Callable[[str], None] | None, message: str) -> None:
        if progress is not None:
            progress(message)

    def _configure_repo(self) -> None:
        if self.repo_path is None:
            raise Hunyuan3DError(
                "HUNYUAN3D_REPO is not set. Clone Tencent-Hunyuan/Hunyuan3D-2.1 "
                "and point HUNYUAN3D_REPO to that folder."
            )

        if not self.repo_path.exists():
            raise Hunyuan3DError(f"HUNYUAN3D_REPO does not exist: {self.repo_path}")

        required = [self.repo_path / "hy3dshape", self.repo_path / "hy3dpaint"]
        missing = [str(path) for path in required if not path.exists()]
        if missing:
            raise Hunyuan3DError(
                "HUNYUAN3D_REPO does not look like the official repository. "
                f"Missing: {', '.join(missing)}"
            )

        hy3dgen_root = self.hy3dgen_models_dir or (self.repo_path / "local_models")
        hy3dgen_root.mkdir(parents=True, exist_ok=True)
        os.environ["HY3DGEN_MODELS"] = str(hy3dgen_root)
        os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")

        for path in [
            self.repo_path,
            self.repo_path / "hy3dshape",
            self.repo_path / "hy3dpaint",
            *custom_rasterizer_search_paths(self.repo_path),
        ]:
            if not path.exists():
                continue
            path_str = str(path)
            if path_str not in sys.path:
                sys.path.insert(0, path_str)

    def _resolve_paint_local_dir(self) -> Path:
        if self.local_model_dir is not None:
            return self.local_model_dir
        if self.repo_path is None:
            raise Hunyuan3DError("HUNYUAN3D_REPO is not set; cannot resolve texture cache.")
        return self.repo_path / "local_models" / "Hunyuan3D-2.1"

    def _seed_everything(self, seed: int) -> None:
        random.seed(seed)
        try:
            import numpy as np

            np.random.seed(seed)
        except Exception:
            pass

    def _device_for_gpu(self, gpu_id: int) -> str:
        if not self.device.startswith("cuda"):
            return self.device
        return f"cuda:{max(0, int(gpu_id))}"
        try:
            import torch

            torch.manual_seed(seed)
            if torch.cuda.is_available():
                torch.cuda.manual_seed_all(seed)
        except Exception:
            pass

    def _load_images(self, image_paths: list[Path], remove_background: bool):
        from PIL import Image

        images = []
        for path in image_paths:
            image = Image.open(path)
            if remove_background:
                if self._has_useful_alpha(image):
                    image = image.convert("RGBA")
                else:
                    image = self._remove_background(image.convert("RGB"))
            else:
                image = image.convert("RGBA")
            images.append(image)
        return images

    def _has_useful_alpha(self, image) -> bool:
        if image.mode != "RGBA":
            return False
        alpha = image.getchannel("A")
        min_alpha, max_alpha = alpha.getextrema()
        return max_alpha > 0 and min_alpha < 250

    def _save_processed_inputs(self, images, output_dir: Path) -> None:
        preview_dir = output_dir / "processed_inputs"
        preview_dir.mkdir(parents=True, exist_ok=True)
        for index, image in enumerate(images, start=1):
            image.save(preview_dir / f"{index:02d}.png")

    def _remove_background(self, image):
        if self._background_remover is None:
            from hy3dshape.rembg import BackgroundRemover

            self._background_remover = BackgroundRemover()
        return self._background_remover(image).convert("RGBA")

    def _load_shape_pipeline(self, gpu_id: int):
        device = self._device_for_gpu(gpu_id)
        if self._shape_pipeline is not None and self._shape_pipeline_device == device:
            return self._shape_pipeline
        if self._shape_pipeline is not None:
            self._release_shape_pipeline()

        import torch
        from hy3dshape.pipelines import Hunyuan3DDiTFlowMatchingPipeline

        model_root = Path(os.environ.get("HY3DGEN_MODELS", "")).expanduser()
        model_dir = model_root / self.model_path / self.subfolder if model_root else Path()
        has_safetensors = (model_dir / "model.fp16.safetensors").exists()
        has_ckpt = (model_dir / "model.fp16.ckpt").exists()
        use_safetensors = has_safetensors
        if not has_safetensors and not has_ckpt:
            # Let upstream downloader try remote; error message will include the missing path.
            use_safetensors = False

        dtype = torch.float16 if device.startswith("cuda") else torch.float32
        pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
            self.model_path,
            subfolder=self.subfolder,
            device=device,
            dtype=dtype,
            use_safetensors=use_safetensors,
            variant="fp16",
        )

        if self.low_vram and hasattr(pipeline, "enable_model_cpu_offload"):
            pipeline.enable_model_cpu_offload(device=device)
        if self.enable_flashvdm:
            pipeline.enable_flashvdm(mc_algo="mc")
        if self.compile_model:
            pipeline.compile()

        self._shape_pipeline = pipeline
        self._shape_pipeline_device = device
        return pipeline

    def _release_shape_pipeline(self) -> None:
        self._shape_pipeline = None
        self._shape_pipeline_device = None
        gc.collect()
        self._empty_cuda_cache()

    def _generate_shape(
        self,
        images,
        output_dir: Path,
        options: GenerationOptions,
        warnings: list[str],
        progress: Callable[[str], None] | None = None,
    ) -> Path:
        if options.model == "hunyuan3d-omni":
            if len(images) > 1:
                warnings.append(
                    "Hunyuan3D-Omni web mode uses the first image for bbox-controlled shape generation; "
                    "remaining images are still forwarded to the texture stage."
                )
            return self._generate_omni_shape(output_dir / "processed_inputs" / "01.png", output_dir, options, progress)

        pipeline = self._load_shape_pipeline(options.gpu_id)
        image = self._shape_input(images, pipeline, warnings)
        mesh = pipeline(
            image=image,
            num_inference_steps=options.num_inference_steps,
            guidance_scale=options.guidance_scale,
            octree_resolution=options.octree_resolution,
            num_chunks=options.num_chunks,
            output_type="trimesh",
        )[0]

        mesh_path = output_dir / "model_initial.glb"
        mesh.export(mesh_path)
        return mesh_path

    def _generate_omni_shape(
        self,
        image_path: Path,
        output_dir: Path,
        options: GenerationOptions,
        progress: Callable[[str], None] | None = None,
    ) -> Path:
        if self.omni_repo_path is None:
            raise Hunyuan3DError(
                "HUNYUAN_OMNI_REPO is not set. Clone Tencent-Hunyuan/Hunyuan3D-Omni "
                "and point HUNYUAN_OMNI_REPO to that folder."
            )
        if not self.omni_repo_path.exists():
            raise Hunyuan3DError(f"HUNYUAN_OMNI_REPO does not exist: {self.omni_repo_path}")
        if self.omni_control_type != "bbox":
            raise Hunyuan3DError("The web app currently supports Hunyuan3D-Omni with bbox control only.")

        output_glb = output_dir / "model_initial.glb"
        worker_input = output_dir / "omni_request.json"
        worker_log = output_dir / "omni_worker.log"
        worker_payload = {
            "repo_path": str(self.omni_repo_path),
            "model_path": self.omni_model_path,
            "image_path": str(image_path),
            "output_glb_path": str(output_glb),
            "control_type": self.omni_control_type,
            "bbox": self.omni_bbox,
            "num_inference_steps": options.num_inference_steps,
            "guidance_scale": options.guidance_scale,
            "octree_resolution": options.octree_resolution,
            "seed": options.seed,
            "flashvdm": self.omni_enable_flashvdm,
            "gpu_id": options.gpu_id,
        }
        worker_input.write_text(json.dumps(worker_payload, indent=2), encoding="utf-8")

        worker_script = Path(__file__).resolve().parent / "scripts" / "omni_worker.py"
        command = [sys.executable, str(worker_script), str(worker_input)]
        env = os.environ.copy()
        if self.device.startswith("cuda"):
            env["CUDA_VISIBLE_DEVICES"] = str(max(0, int(options.gpu_id)))
        self._progress(progress, f"Running Hunyuan3D-Omni worker on GPU {options.gpu_id}...")
        with worker_log.open("w", encoding="utf-8") as log:
            log.write(f"command: {' '.join(command)}\n")
            log.write(f"CUDA_VISIBLE_DEVICES: {env.get('CUDA_VISIBLE_DEVICES', '')}\n\n")
            log.flush()
            completed = subprocess.run(
                command,
                cwd=str(Path(__file__).resolve().parent),
                stdout=log,
                stderr=subprocess.STDOUT,
                text=True,
                env=env,
            )
            log.write(f"\nreturncode: {completed.returncode}\n")
        if completed.returncode != 0:
            details = worker_log.read_text(encoding="utf-8")[-4000:]
            raise Hunyuan3DError(f"Hunyuan3D-Omni worker failed: {details}")
        if not output_glb.exists():
            raise Hunyuan3DError(f"Hunyuan3D-Omni did not produce a GLB file. See {worker_log}.")
        return output_glb

    def _shape_input(self, images, pipeline, warnings: list[str]):
        if len(images) == 1:
            return images[0]

        image_processor = getattr(pipeline, "image_processor", None)
        supports_multiview = getattr(image_processor, "return_view_idx", False)
        model_is_multiview = "mv" in self.model_path.lower() or "mv" in self.subfolder.lower()
        if not supports_multiview or not model_is_multiview:
            warning = (
                "Hunyuan3D 2.1 does not provide a multiview shape model. "
                "The first uploaded image was used for shape generation; "
                "remaining images are still forwarded to the texture stage."
            )
            warnings.append(warning)
            return images[0]

        view_order = ["front", "back", "left", "right"]
        return {view: image for view, image in zip(view_order, images)}

    def _metadata_note(self, images, options: GenerationOptions) -> str:
        if options.model == "hunyuan3d-omni":
            return "Hunyuan3D-Omni was used for bbox-controlled shape generation."
        if len(images) == 1:
            return "A single image was used for shape generation."
        if "mv" in self.model_path.lower() or "mv" in self.subfolder.lower():
            return (
                "Multiple images were mapped to Hunyuan3D multiview shape inputs in this order: "
                "1 front, 2 back, 3 left, 4 right. Extra images are ignored for shape but may be "
                "used by the texture stage."
            )
        return (
            "Hunyuan3D 2.1 has no multiview shape variant, so the first image drove shape "
            "generation. All uploaded images are still passed to the texture stage when enabled."
        )

    def _load_paint_pipeline(self):
        if self._paint_pipeline is not None:
            return self._paint_pipeline

        import torch  # Ensures torch CUDA DLL directories are registered before custom extensions import.

        self._apply_torchvision_compat()
        self._apply_pkg_resources_compat()
        from textureGenPipeline import Hunyuan3DPaintConfig, Hunyuan3DPaintPipeline

        config = Hunyuan3DPaintConfig(self.texture_views, self.texture_resolution)
        config.realesrgan_ckpt_path = str(self.repo_path / "hy3dpaint" / "ckpt" / "RealESRGAN_x4plus.pth")
        config.multiview_cfg_path = str(self.repo_path / "hy3dpaint" / "cfgs" / "hunyuan-paint-pbr.yaml")
        config.custom_pipeline = str(self.repo_path / "hy3dpaint" / "hunyuanpaintpbr")
        config.multiview_local_dir = str(self._resolve_paint_local_dir())
        config.max_selected_view_num = self.texture_views
        config.render_size = self.texture_render_size
        config.texture_size = self.texture_texture_size
        config.view_selection_resolution = self.texture_view_selection_resolution
        config.render_device = self.texture_render_device
        self._paint_pipeline = Hunyuan3DPaintPipeline(config)
        return self._paint_pipeline

    def _apply_torchvision_compat(self) -> None:
        if "torchvision.transforms.functional_tensor" in sys.modules:
            return

        import torchvision.transforms.functional as functional

        shim = types.ModuleType("torchvision.transforms.functional_tensor")
        for name in dir(functional):
            if not name.startswith("__"):
                setattr(shim, name, getattr(functional, name))
        sys.modules["torchvision.transforms.functional_tensor"] = shim

    def _apply_pkg_resources_compat(self) -> None:
        if "pkg_resources" in sys.modules:
            return
        try:
            import pkg_resources  # type: ignore  # noqa: F401

            return
        except Exception:
            pass

        from packaging.requirements import Requirement
        from packaging.version import parse as parse_version

        class DistributionNotFound(Exception):
            pass

        class _Distribution:
            def __init__(self, name: str):
                self.project_name = name
                self.version = importlib.metadata.version(name)
                self.parsed_version = parse_version(self.version)

        shim = types.ModuleType("pkg_resources")
        shim.DistributionNotFound = DistributionNotFound
        shim.parse_version = parse_version

        def declare_namespace(_name: str) -> None:
            return None

        def get_distribution(name: str):
            try:
                return _Distribution(name)
            except importlib.metadata.PackageNotFoundError as exc:
                raise DistributionNotFound(name) from exc

        def parse_requirements(requirements):
            if isinstance(requirements, str):
                requirements = requirements.splitlines()
            return [Requirement(line.strip()) for line in requirements if str(line).strip()]

        def require(*requirements):
            resolved = []
            for requirement in requirements:
                parsed = Requirement(requirement) if isinstance(requirement, str) else requirement
                resolved.append(get_distribution(parsed.name))
            return resolved

        def resource_filename(package_or_requirement, resource_name: str) -> str:
            package = (
                package_or_requirement.name
                if hasattr(package_or_requirement, "name")
                else str(package_or_requirement)
            )
            return str(importlib.resources.files(package).joinpath(resource_name))

        shim.declare_namespace = declare_namespace
        shim.get_distribution = get_distribution
        shim.parse_requirements = parse_requirements
        shim.require = require
        shim.resource_filename = resource_filename
        sys.modules["pkg_resources"] = shim

    def _generate_texture(self, mesh_path: Path, images, output_dir: Path, gpu_id: int) -> Path:
        worker_input = output_dir / "texture_request.json"
        output_obj = output_dir / "model_textured.obj"
        output_glb = output_obj.with_suffix(".glb")
        worker_payload = {
            "repo_path": str(self.repo_path),
            "mesh_path": str(mesh_path),
            "image_paths": [str(path) for path in self._materialize_images(images, output_dir)],
            "output_mesh_path": str(output_obj),
            "texture_views": self.texture_views,
            "texture_resolution": self.texture_resolution,
            "texture_render_size": self.texture_render_size,
            "texture_texture_size": self.texture_texture_size,
            "texture_view_selection_resolution": self.texture_view_selection_resolution,
            "texture_render_device": self.texture_render_device,
            "local_model_dir": str(self._resolve_paint_local_dir()),
            "gpu_id": gpu_id,
        }
        worker_input.write_text(json.dumps(worker_payload, indent=2), encoding="utf-8")

        worker_script = Path(__file__).resolve().parent / "scripts" / "texture_worker.py"
        worker_log = output_dir / "texture_worker.log"
        command = [sys.executable, str(worker_script), str(worker_input)]
        env = os.environ.copy()
        if self.device.startswith("cuda"):
            env["CUDA_VISIBLE_DEVICES"] = str(max(0, int(gpu_id)))
        timeout_minutes = max(1, int(os.getenv("HUNYUAN_TEXTURE_TIMEOUT_MINUTES", "12")))
        try:
            completed = subprocess.run(
                command,
                cwd=str(Path(__file__).resolve().parent),
                capture_output=True,
                text=True,
                env=env,
                timeout=timeout_minutes * 60,
            )
        except subprocess.TimeoutExpired as exc:
            stdout = (exc.stdout or "").strip()
            stderr = (exc.stderr or "").strip()
            worker_log.write_text(
                "\n".join(
                    [
                        f"command: {' '.join(command)}",
                        f"timeout_minutes: {timeout_minutes}",
                        "",
                        "stdout:",
                        stdout,
                        "",
                        "stderr:",
                        stderr,
                    ]
                ),
                encoding="utf-8",
            )
            raise Hunyuan3DError(f"Texture worker timed out after {timeout_minutes} minutes.") from exc
        worker_log.write_text(
            "\n".join(
                [
                    f"command: {' '.join(command)}",
                    f"timeout_minutes: {timeout_minutes}",
                    f"returncode: {completed.returncode}",
                    "",
                    "stdout:",
                    completed.stdout or "",
                    "",
                    "stderr:",
                    completed.stderr or "",
                ]
            ),
            encoding="utf-8",
        )
        if completed.returncode != 0:
            stderr = (completed.stderr or "").strip()
            stdout = (completed.stdout or "").strip()
            details = stderr or stdout or f"texture worker exited with code {completed.returncode}"
            raise Hunyuan3DError(f"Texture worker failed: {details}")

        if output_glb.exists():
            return output_glb
        raise Hunyuan3DError(f"Texture generation did not produce a GLB file. See {worker_log}.")

    def _materialize_images(self, images, output_dir: Path) -> list[Path]:
        image_dir = output_dir / "texture_inputs"
        image_dir.mkdir(parents=True, exist_ok=True)
        saved_paths = []
        for index, image in enumerate(images, start=1):
            path = image_dir / f"{index:02d}.png"
            image.save(path)
            saved_paths.append(path)
        return saved_paths

    def _write_warning(self, output_dir: Path, message: str) -> None:
        warning_path = output_dir / "warnings.log"
        with warning_path.open("a", encoding="utf-8") as handle:
            handle.write(message + "\n")

    def _empty_cuda_cache(self) -> None:
        try:
            import torch

            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except Exception:
            pass
