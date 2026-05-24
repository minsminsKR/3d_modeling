from __future__ import annotations

import ctypes
import json
import os
import shutil
import sys
from pathlib import Path


def configure_repo(repo_path: Path) -> None:
    for path in [repo_path, repo_path / "hy3dshape"]:
        if path.exists():
            sys.path.insert(0, str(path))


def save_ply_points(filename: Path, points) -> None:
    with filename.open("w", encoding="utf-8") as handle:
        handle.write("ply\n")
        handle.write("format ascii 1.0\n")
        handle.write(f"element vertex {len(points)}\n")
        handle.write("property float x\n")
        handle.write("property float y\n")
        handle.write("property float z\n")
        handle.write("end_header\n")
        for point in points:
            handle.write(f"{point[0]} {point[1]} {point[2]}\n")


def main() -> int:
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python scripts/omni_worker.py <request.json>")

    request_path = Path(sys.argv[1]).resolve()
    payload = json.loads(request_path.read_text(encoding="utf-8"))
    repo_path = Path(payload["repo_path"]).resolve()
    image_path = Path(payload["image_path"]).resolve()
    output_glb_path = Path(payload["output_glb_path"]).resolve()
    output_glb_path.parent.mkdir(parents=True, exist_ok=True)

    ctypes.CDLL("libgcc_s.so.1")
    configure_repo(repo_path)

    import torch
    from hy3dshape.pipelines import Hunyuan3DOmniSiTFlowMatchingPipeline
    from hy3dshape.postprocessors import DegenerateFaceRemover, FloaterRemover

    if payload["control_type"] != "bbox":
        raise RuntimeError("Only bbox control is supported by this web worker.")

    pipeline = Hunyuan3DOmniSiTFlowMatchingPipeline.from_pretrained(
        payload["model_path"],
        fast_decode=bool(payload.get("flashvdm", False)),
    )
    bbox = torch.FloatTensor(payload["bbox"]).unsqueeze(0).unsqueeze(0).to(pipeline.device).to(pipeline.dtype)
    generator_device = "cuda" if str(pipeline.device).startswith("cuda") else "cpu"
    result = pipeline(
        image=str(image_path),
        bbox=bbox,
        num_inference_steps=int(payload["num_inference_steps"]),
        octree_resolution=int(payload["octree_resolution"]),
        mc_level=0,
        guidance_scale=float(payload["guidance_scale"]),
        generator=torch.Generator(generator_device).manual_seed(int(payload["seed"])),
    )

    mesh = result["shapes"][0][0]
    sampled_point = result.get("sampled_point", [None])[0]
    mesh = FloaterRemover()(mesh)
    mesh = DegenerateFaceRemover()(mesh)
    mesh.export(output_glb_path)
    if sampled_point is not None:
        save_ply_points(output_glb_path.with_suffix(".ply"), sampled_point.cpu().numpy())
    shutil.copy(image_path, output_glb_path.with_suffix(".png"))
    if not output_glb_path.exists():
        raise RuntimeError(f"Hunyuan3D-Omni did not create {output_glb_path}")
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
