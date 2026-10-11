from __future__ import annotations

import json
import os
import sys
import time
from dataclasses import asdict
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE_DIR))

from dotenv import load_dotenv
from gpu_runtime import TEXTURE_PROFILES
from hunyuan_service import GenerationOptions, Hunyuan3DGenerator


def main():
    load_dotenv(BASE_DIR / ".env", override=True)
    payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    options = GenerationOptions(**payload["options"])
    import torch
    if torch.cuda.is_available():
        torch.cuda.set_device(options.gpu_id)
    profile = TEXTURE_PROFILES[options.texture_profile]
    def flag(key, default="0"):
        return os.getenv(key, default).lower() in {"1", "true", "yes", "on"}
    generator = Hunyuan3DGenerator(
        repo_path=os.getenv("HUNYUAN3D_REPO"),
        model_path=os.getenv("HUNYUAN_MODEL_PATH", "tencent/Hunyuan3D-2.1"),
        subfolder=os.getenv("HUNYUAN_SUBFOLDER", "hunyuan3d-dit-v2-1"),
        device=os.getenv("HUNYUAN_DEVICE", "cuda"),
        low_vram=flag("HUNYUAN_LOW_VRAM"),
        enable_flashvdm=flag("HUNYUAN_ENABLE_FLASHVDM"),
        compile_model=flag("HUNYUAN_COMPILE"),
        texture_views=profile["views"], texture_resolution=profile["resolution"],
        texture_render_size=profile["render"], texture_texture_size=profile["map_size"],
        texture_view_selection_resolution=512, texture_render_device="cuda",
        hy3dgen_models_dir=os.getenv("HY3DGEN_MODELS"),
        local_model_dir=os.getenv("HUNYUAN_LOCAL_MODEL_DIR"),
        omni_repo_path=os.getenv("HUNYUAN_OMNI_REPO"),
        omni_model_path=os.getenv("HUNYUAN_OMNI_MODEL_PATH", "tencent/Hunyuan3D-Omni"),
        omni_control_type=os.getenv("HUNYUAN_OMNI_CONTROL_TYPE", "bbox"),
        omni_bbox=os.getenv("HUNYUAN_OMNI_BBOX"),
        omni_enable_flashvdm=flag("HUNYUAN_OMNI_FLASHVDM"),
    )
    def progress(message):
        print("A100_EVENT " + json.dumps({"message": message, "timestamp": time.time()}, ensure_ascii=False), flush=True)
    asset = generator.generate([Path(path) for path in payload["image_paths"]], Path(payload["output_dir"]), options, progress)
    result = asdict(asset)
    result["model_path"] = str(asset.model_path)
    result["metadata_path"] = str(asset.metadata_path)
    (Path(payload["output_dir"]) / "generation_result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
