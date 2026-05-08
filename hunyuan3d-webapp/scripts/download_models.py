"""Pre-download all Hunyuan3D 2.1 assets into the repo's local_models folder.

Targets (relative to HUNYUAN3D_REPO, defaults to E:/AI/3d_modeling/Hunyuan3D-2.1):
  local_models/tencent/Hunyuan3D-2.1/hunyuan3d-dit-v2-1/      (shape, 3.3B)
  local_models/Hunyuan3D-2.1/hunyuan3d-paintpbr-v2-1/         (paint, 2B)
  local_models/facebook--dinov2-giant/                        (image encoder)
  hy3dpaint/ckpt/RealESRGAN_x4plus.pth                        (texture SR)

Run once after cloning the Hunyuan3D-2.1 repo. Safe to re-run; already-downloaded
files are skipped by huggingface_hub.

Usage:
    python scripts/download_models.py
"""

from __future__ import annotations

import os
import sys
import urllib.request
from pathlib import Path

from dotenv import load_dotenv


BASE_DIR = Path(__file__).resolve().parent.parent
load_dotenv(BASE_DIR / ".env", override=True)


REAL_ESRGAN_URL = (
    "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth"
)


def resolve_repo_path() -> Path:
    repo_env = os.getenv("HUNYUAN3D_REPO")
    if not repo_env:
        raise SystemExit(
            "HUNYUAN3D_REPO is not set. Populate .env first (see .env.example)."
        )
    repo_path = Path(repo_env).expanduser().resolve()
    if not repo_path.exists():
        raise SystemExit(f"HUNYUAN3D_REPO does not exist: {repo_path}")
    return repo_path


def download_shape_model(repo_path: Path) -> Path:
    """Place shape ckpts at local_models/tencent/Hunyuan3D-2.1/hunyuan3d-dit-v2-1/."""
    import huggingface_hub

    shape_root = repo_path / "local_models" / "tencent" / "Hunyuan3D-2.1"
    shape_root.mkdir(parents=True, exist_ok=True)
    print(f"[shape] -> {shape_root}")
    huggingface_hub.snapshot_download(
        repo_id="tencent/Hunyuan3D-2.1",
        allow_patterns=["hunyuan3d-dit-v2-1/*"],
        local_dir=str(shape_root),
        local_dir_use_symlinks=False,
    )
    return shape_root / "hunyuan3d-dit-v2-1"


def download_paint_model(repo_path: Path) -> Path:
    """Place paint ckpts at local_models/Hunyuan3D-2.1/hunyuan3d-paintpbr-v2-1/."""
    import huggingface_hub

    paint_root = repo_path / "local_models" / "Hunyuan3D-2.1"
    paint_root.mkdir(parents=True, exist_ok=True)
    print(f"[paint] -> {paint_root}")
    huggingface_hub.snapshot_download(
        repo_id="tencent/Hunyuan3D-2.1",
        allow_patterns=["hunyuan3d-paintpbr-v2-1/*"],
        local_dir=str(paint_root),
        local_dir_use_symlinks=False,
    )
    return paint_root / "hunyuan3d-paintpbr-v2-1"


def download_dinov2(repo_path: Path) -> Path:
    """Place DINOv2-giant at local_models/facebook--dinov2-giant/."""
    import huggingface_hub

    dino_root = repo_path / "local_models" / "facebook--dinov2-giant"
    if (dino_root / "preprocessor_config.json").exists() and any(
        dino_root.glob("*.safetensors")
    ):
        print(f"[dinov2] already present -> {dino_root}")
        return dino_root

    dino_root.mkdir(parents=True, exist_ok=True)
    print(f"[dinov2] -> {dino_root}")
    huggingface_hub.snapshot_download(
        repo_id="facebook/dinov2-giant",
        local_dir=str(dino_root),
        local_dir_use_symlinks=False,
    )
    return dino_root


def download_real_esrgan(repo_path: Path) -> Path:
    """Place RealESRGAN_x4plus.pth under hy3dpaint/ckpt/."""
    ckpt_dir = repo_path / "hy3dpaint" / "ckpt"
    ckpt_dir.mkdir(parents=True, exist_ok=True)
    ckpt_path = ckpt_dir / "RealESRGAN_x4plus.pth"
    if ckpt_path.exists() and ckpt_path.stat().st_size > 50 * 1024 * 1024:
        print(f"[realesrgan] already present -> {ckpt_path}")
        return ckpt_path

    print(f"[realesrgan] downloading -> {ckpt_path}")
    with urllib.request.urlopen(REAL_ESRGAN_URL) as response, ckpt_path.open("wb") as out:
        total = int(response.headers.get("Content-Length", 0))
        downloaded = 0
        chunk = 1024 * 1024
        while True:
            buf = response.read(chunk)
            if not buf:
                break
            out.write(buf)
            downloaded += len(buf)
            if total:
                percent = downloaded * 100 / total
                sys.stdout.write(
                    f"\r  {downloaded / 1e6:7.1f} / {total / 1e6:7.1f} MB ({percent:5.1f}%)"
                )
                sys.stdout.flush()
    print()
    return ckpt_path


def main() -> int:
    repo_path = resolve_repo_path()
    print(f"HUNYUAN3D_REPO = {repo_path}")

    os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")

    shape_dir = download_shape_model(repo_path)
    paint_dir = download_paint_model(repo_path)
    dino_dir = download_dinov2(repo_path)
    esrgan_path = download_real_esrgan(repo_path)

    print()
    print("All assets ready:")
    print(f"  shape:   {shape_dir}")
    print(f"  paint:   {paint_dir}")
    print(f"  dinov2:  {dino_dir}")
    print(f"  realesr: {esrgan_path}")
    print()
    print("Make sure .env contains HY3DGEN_MODELS pointing at the local_models folder")
    print("so the shape pipeline reads from disk instead of re-downloading.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
