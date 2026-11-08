"""Offline optional 8K export from validated sharded mesh and raw checkpoint."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time


def _run(args: list[str], gpu: int | None, log: Path) -> dict:
    env = os.environ.copy()
    if gpu is not None:
        env["CUDA_VISIBLE_DEVICES"] = str(gpu)
    started = time.monotonic()
    with log.open("w") as stream:
        result = subprocess.run(args, env=env, stdout=stream, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError(f"Stage failed with code {result.returncode}: {log}")
    result_text = log.read_text()
    marker = result_text.rfind("{\n")
    data = json.loads(result_text[marker:]) if marker >= 0 else {}
    data["elapsed_wall_seconds"] = round(time.monotonic() - started, 2)
    return data


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("raw", type=Path)
    parser.add_argument("surface", type=Path)
    parser.add_argument("simplified", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--size", type=int, default=8192)
    parser.add_argument("--uv-checkpoint", type=Path,
                        help="Reuse a verified UV checkpoint in a new export attempt")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if shutil.disk_usage(args.output).free < 30 * 1024**3:
        raise RuntimeError("Less than 30 GiB free; refusing 8K export")
    script = str(Path(__file__).with_name("dual_bake.py"))
    python = sys.executable
    manifest = {"requested_texture_size": args.size, "completed": False, "stages": {}}
    manifest_path = args.output / "export_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2))
    uv = args.output / "uv_mesh.pt"
    if args.uv_checkpoint:
        if not args.uv_checkpoint.is_file():
            raise FileNotFoundError(args.uv_checkpoint)
        shutil.copy2(args.uv_checkpoint, uv)
        manifest["stages"]["uv"] = {"reused_from": str(args.uv_checkpoint)}
    else:
        manifest["stages"]["uv"] = _run([python, script, "uv", str(args.simplified), str(uv)],
                                       1, args.output / "uv.log")
    manifest_path.write_text(json.dumps(manifest, indent=2))
    ordered_uv = args.output / "uv_mesh_ordered.pt"
    manifest["stages"]["uv_face_audit"] = _run(
        [python, str(Path(__file__).with_name("audit_uv_face_map.py")),
         str(args.simplified), str(uv), str(args.output), str(ordered_uv)],
        None, args.output / "uv_face_audit.log")
    uv = ordered_uv
    manifest_path.write_text(json.dumps(manifest, indent=2))
    half = args.size // 2
    running = []
    for gpu, start, end in ((0, 0, half), (1, half, args.size)):
        tile = args.output / f"tile_{gpu}.pt"
        log = (args.output / f"tile_{gpu}.log").open("w")
        env = os.environ.copy()
        env["CUDA_VISIBLE_DEVICES"] = str(gpu)
        process = subprocess.Popen([python, script, "tile", str(args.raw), str(args.surface), str(uv),
                                    str(args.size), str(start), str(end), str(tile)],
                                   env=env, stdout=log, stderr=subprocess.STDOUT)
        running.append((gpu, process, log, tile))
    failures = []
    for gpu, process, log, tile in running:
        code = process.wait()
        log.close()
        if code:
            failures.append((gpu, code, str(log.name)))
        else:
            result_text = (args.output / f"tile_{gpu}.log").read_text()
            marker = result_text.rfind("{\n")
            manifest["stages"][f"tile_gpu_{gpu}"] = json.loads(result_text[marker:])
    manifest_path.write_text(json.dumps(manifest, indent=2))
    if failures:
        raise RuntimeError(f"Tile bake failures: {failures}")
    glb = args.output / "model.glb"
    manifest["stages"]["assemble"] = _run([python, script, "assemble", str(args.raw), str(uv),
                                             str(glb), str(args.output / "tile_0.pt"),
                                             str(args.output / "tile_1.pt")],
                                            None, args.output / "assemble.log")
    manifest["completed"] = True
    manifest_path.write_text(json.dumps(manifest, indent=2))
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
