"""One physical GPU per shape process; exit releases upstream checkpoint VRAM."""
import json
import os
import sys
from dataclasses import replace
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from PIL import Image
from hunyuan_service import GenerationOptions, Hunyuan3DGenerator, pushd


def main():
    payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    # CUDA_VISIBLE_DEVICES maps the physical card selected by the parent to 0.
    options = replace(GenerationOptions(**payload["options"]), gpu_id=0)
    generator = Hunyuan3DGenerator(
        repo_path=payload["repo_path"], model_path=payload["model_path"],
        subfolder=payload["subfolder"], device=os.getenv("HUNYUAN_DEVICE", "cuda"),
        hy3dgen_models_dir=payload["hy3dgen_models_dir"], local_model_dir=payload["local_model_dir"],
        low_vram=payload["low_vram"], enable_flashvdm=payload["enable_flashvdm"], compile_model=payload["compile_model"],
    )
    generator._configure_repo()
    generator._seed_everything(options.seed)
    output = Path(payload["output_dir"])
    warnings = []
    images = [Image.open(path).convert("RGBA") for path in payload["image_paths"]]
    with pushd(generator.repo_path):
        generator._generate_shape_in_process(images, output, options, warnings)
    (output / "shape_result.json").write_text(json.dumps({"warnings": warnings}), encoding="utf-8")


if __name__ == "__main__":
    main()
