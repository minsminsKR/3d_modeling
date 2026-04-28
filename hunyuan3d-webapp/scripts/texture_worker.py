from __future__ import annotations

import importlib.metadata
import importlib.resources
import json
import os
import shutil
import sys
import types
from pathlib import Path

import huggingface_hub


def apply_torchvision_compat() -> None:
    if "torchvision.transforms.functional_tensor" in sys.modules:
        return
    import torchvision.transforms.functional as functional

    shim = types.ModuleType("torchvision.transforms.functional_tensor")
    for name in dir(functional):
        if not name.startswith("__"):
            setattr(shim, name, getattr(functional, name))
    sys.modules["torchvision.transforms.functional_tensor"] = shim


def apply_pkg_resources_compat() -> None:
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
        package = package_or_requirement.name if hasattr(package_or_requirement, "name") else str(package_or_requirement)
        return str(importlib.resources.files(package).joinpath(resource_name))

    shim.declare_namespace = declare_namespace
    shim.get_distribution = get_distribution
    shim.parse_requirements = parse_requirements
    shim.require = require
    shim.resource_filename = resource_filename
    sys.modules["pkg_resources"] = shim


def configure_repo(repo_path: Path) -> None:
    build_root = repo_path / "hy3dpaint" / "custom_rasterizer" / "build"
    custom_rasterizer_paths = [repo_path / "hy3dpaint" / "custom_rasterizer"]
    if build_root.exists():
        custom_rasterizer_paths.extend(path for path in build_root.iterdir() if path.is_dir())
    for path in [repo_path, repo_path / "hy3dshape", repo_path / "hy3dpaint", *custom_rasterizer_paths]:
        if path.exists():
            sys.path.insert(0, str(path))


def configure_huggingface_cache(local_model_dir: Path) -> None:
    cache_root = local_model_dir.parent / "hf_cache"
    modules_cache = local_model_dir.parent / "hf_modules"
    for path in [cache_root, cache_root / "hub", cache_root / "transformers", modules_cache]:
        path.mkdir(parents=True, exist_ok=True)

    os.environ["HF_HOME"] = str(cache_root)
    os.environ["HF_HUB_CACHE"] = str(cache_root / "hub")
    os.environ["HUGGINGFACE_HUB_CACHE"] = str(cache_root / "hub")
    os.environ["TRANSFORMERS_CACHE"] = str(cache_root / "transformers")
    os.environ["HF_MODULES_CACHE"] = str(modules_cache)
    os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")


def ensure_local_dino_model(local_model_dir: Path) -> Path:
    dino_local_dir = local_model_dir.parent / "facebook--dinov2-giant"
    if (dino_local_dir / "preprocessor_config.json").exists():
        return dino_local_dir

    user_hf_cache = Path.home() / ".cache" / "huggingface" / "hub" / "models--facebook--dinov2-giant" / "snapshots"
    if user_hf_cache.exists():
        snapshots = sorted((path for path in user_hf_cache.iterdir() if path.is_dir()), key=lambda path: path.name)
        if snapshots:
            shutil.copytree(snapshots[-1], dino_local_dir, dirs_exist_ok=True)

    if not (dino_local_dir / "preprocessor_config.json").exists():
        huggingface_hub.snapshot_download(
            repo_id="facebook/dinov2-giant",
            local_dir=str(dino_local_dir),
            local_dir_use_symlinks=False,
        )
    return dino_local_dir


def main() -> int:
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python scripts/texture_worker.py <request.json>")

    request_path = Path(sys.argv[1]).resolve()
    payload = json.loads(request_path.read_text(encoding="utf-8"))
    repo_path = Path(payload["repo_path"]).resolve()
    local_model_dir = Path(payload["local_model_dir"]).resolve()
    output_mesh_path = Path(payload["output_mesh_path"]).resolve()
    output_glb_path = output_mesh_path.with_suffix(".glb")

    configure_repo(repo_path)
    configure_huggingface_cache(local_model_dir)
    dino_model_dir = ensure_local_dino_model(local_model_dir)

    apply_torchvision_compat()
    apply_pkg_resources_compat()

    import torch
    from PIL import Image
    from textureGenPipeline import Hunyuan3DPaintConfig, Hunyuan3DPaintPipeline

    image_paths = [Path(path) for path in payload["image_paths"]]
    images = [Image.open(path).convert("RGBA") for path in image_paths]

    config = Hunyuan3DPaintConfig(payload["texture_views"], payload["texture_resolution"])
    config.realesrgan_ckpt_path = str(repo_path / "hy3dpaint" / "ckpt" / "RealESRGAN_x4plus.pth")
    config.multiview_cfg_path = str(repo_path / "hy3dpaint" / "cfgs" / "hunyuan-paint-pbr.yaml")
    config.custom_pipeline = str(repo_path / "hy3dpaint" / "hunyuanpaintpbr")
    config.multiview_local_dir = str(local_model_dir)
    config.dino_ckpt_path = str(dino_model_dir)
    config.max_selected_view_num = payload["texture_views"]
    config.render_size = payload["texture_render_size"]
    config.texture_size = payload["texture_texture_size"]
    config.view_selection_resolution = payload["texture_view_selection_resolution"]
    config.render_device = payload["texture_render_device"]

    pipeline = Hunyuan3DPaintPipeline(config)
    pipeline(
        mesh_path=payload["mesh_path"],
        image_path=images,
        output_mesh_path=str(output_mesh_path),
        save_glb=True,
    )

    if not output_mesh_path.exists():
        raise RuntimeError(f"Texture worker did not create OBJ: {output_mesh_path}")
    if not output_glb_path.exists():
        raise RuntimeError(f"Texture worker did not create GLB: {output_glb_path}")

    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
