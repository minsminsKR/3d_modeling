"""Opt-in TRELLIS.2 1536/8K postprocessing with preserved checkpoints.

The job remains review_required until a person accepts the visual result.
All GPU transfers between stages use CPU files; P2P is never assumed.
"""
from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = Path(__file__).resolve().parent


def emit(message: str) -> None:
    print("A100_EVENT " + json.dumps({"message": message, "timestamp": time.time()}, ensure_ascii=False), flush=True)


def atomic_json(path: Path, value: dict) -> None:
    partial = path.with_suffix(path.suffix + ".partial")
    partial.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(partial, path)


def gpu_uuids() -> dict[str, int]:
    result = subprocess.run(
        ["nvidia-smi", "--query-gpu=index,uuid", "--format=csv,noheader"],
        check=True, capture_output=True, text=True, timeout=10,
    )
    return {row.split(",", 1)[1].strip(): int(row.split(",", 1)[0])
            for row in result.stdout.splitlines()}


def is_child(pid: int, root: int) -> bool:
    for _ in range(8):
        if pid == root:
            return True
        try:
            status = Path(f"/proc/{pid}/status").read_text()
            pid = int(next(line.split()[1] for line in status.splitlines() if line.startswith("PPid:")))
        except (OSError, StopIteration, ValueError):
            return False
        if pid <= 1:
            return False
    return False


def sample_native_memory(root_pid: int, uuid_map: dict[str, int]) -> dict[str, int]:
    query = subprocess.run(
        ["nvidia-smi", "--query-compute-apps=pid,gpu_uuid,used_gpu_memory",
         "--format=csv,noheader,nounits"],
        capture_output=True, text=True, timeout=10,
    )
    values = {"0": 0, "1": 0}
    if query.returncode:
        return values
    for row in query.stdout.splitlines():
        parts = [part.strip() for part in row.split(",")]
        if len(parts) != 3:
            continue
        try:
            pid, uuid, mib = int(parts[0]), parts[1], int(parts[2])
        except ValueError:
            continue
        if uuid in uuid_map and is_child(pid, root_pid):
            key = str(uuid_map[uuid])
            values[key] = values.get(key, 0) + mib
    return values


def run_stage(name: str, command: list[str], output: Path, env: dict,
              manifest: dict, expected: list[Path], uuid_map: dict[str, int]) -> dict:
    previous = manifest["stages"].get(name)
    if previous and previous.get("completed") and all(path.is_file() for path in expected):
        emit(f"{name}: 보존된 산출물 재사용")
        return previous
    emit(f"{name}: 시작")
    log_path = output / f"{name}.log"
    peaks = {"0": 0, "1": 0}
    started = time.monotonic()
    with log_path.open("w", encoding="utf-8") as stream:
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=stream, stderr=subprocess.STDOUT)
        while process.poll() is None:
            try:
                sample = sample_native_memory(process.pid, uuid_map)
                for gpu, mib in sample.items():
                    peaks[gpu] = max(peaks.get(gpu, 0), mib)
            except (OSError, subprocess.SubprocessError):
                pass
            time.sleep(0.5)
        code = process.wait()
    stage = {"completed": code == 0 and all(path.is_file() for path in expected),
             "command": command, "log": str(log_path),
             "elapsed_seconds": round(time.monotonic() - started, 2),
             "native_process_peak_mib_by_gpu": peaks}
    manifest["stages"][name] = stage
    atomic_json(output / "dual_postprocess_manifest.json", manifest)
    if not stage["completed"]:
        tail = log_path.read_text(encoding="utf-8", errors="replace")[-2500:]
        raise RuntimeError(f"{name} failed (exit {code}); {log_path.name}: {tail}")
    emit(f"{name}: 완료")
    return stage


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: run_dual_postprocess.py OUTPUT_DIR")
    output = Path(sys.argv[1]).resolve()
    raw = output / "raw_postprocess.pt"
    result_path = output / "generation_result.json"
    if not raw.is_file() or not result_path.is_file():
        raise RuntimeError("Preserved raw checkpoint or generation result is missing")
    generation = json.loads(result_path.read_text(encoding="utf-8"))
    if generation.get("actual_resolution") != 1536 or generation.get("gpu_mode") != "split":
        raise RuntimeError("Opt-in postprocess requires actual 1536 resolution and two-GPU inference")
    if shutil.disk_usage(output).free < 30 * 1024**3:
        raise RuntimeError("Less than 30 GiB free; raw checkpoint preserved for retry")
    patch_root = Path(os.environ["TRELLIS_CUMESH_PATCH_ROOT"]).resolve()
    if not (patch_root / "cumesh").is_dir():
        raise RuntimeError("The isolated CuMesh boundary patch is unavailable")
    env = os.environ.copy()
    env["PYTHONPATH"] = str(patch_root) + os.pathsep + env.get("PYTHONPATH", "")
    env["CUMESH_PATCH_ROOT"] = str(patch_root)
    env["CUMESH_BOUNDARY_PATCH"] = "1"
    python = sys.executable
    uuids = gpu_uuids()
    manifest_path = output / "dual_postprocess_manifest.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {
        "mode": "experimental_dual_1536_8192", "quality_status": "review_required",
        "raw_checkpoint": str(raw), "stages": {}, "completed": False}
    atomic_json(manifest_path, manifest)

    remesh = output / "dual_remesh"
    remesh.mkdir(exist_ok=True)
    env0 = {**env, "CUDA_VISIBLE_DEVICES": "0"}
    run_stage("remesh", [python, str(SCRIPT / "prepare_remesh.py"), str(raw), str(remesh)],
              output, env0, manifest, [remesh / "cleaned_surface.pt", remesh / "remeshed_geometry.pt"], uuids)

    mesh = output / "dual_mesh"
    mesh.mkdir(exist_ok=True)
    mesh_command = [python, str(SCRIPT / "run_dual_mesh.py"),
                    str(remesh / "remeshed_geometry.pt"), str(mesh),
                    "--cumesh-root", str(patch_root), "--target-faces", "3000000",
                    "--intermediate-nonmanifold"]
    if (mesh / "partitions" / "manifest.json").is_file():
        mesh_command.append("--reuse-partitions")
    run_stage("mesh_shards", mesh_command, output, env, manifest,
              [mesh / "simplified_mesh.pt", mesh / "summary.json"], uuids)

    repair = output / "dual_repair"
    repair.mkdir(exist_ok=True)
    run_stage("repair", [python, str(SCRIPT / "clean_merged_mesh.py"),
              str(mesh / "simplified_mesh.pt"), str(repair)],
              output, env0, manifest, [repair / "orient.pt", repair / "cleanup_report.json"], uuids)
    closed = repair / "closed_mesh.pt"
    run_stage("close", [python, str(SCRIPT / "fill_remaining_holes.py"),
              str(repair / "orient.pt"), str(closed), "--max-perimeter", "0.3"],
              output, env0, manifest, [closed, closed.with_suffix(".json")], uuids)

    run_stage("topology", [python, str(SCRIPT / "validate_mesh_topology.py"), str(closed)],
              output, env, manifest, [closed], uuids)
    topology = json.loads((output / "topology.log").read_text(encoding="utf-8"))
    if not topology.get("closed_oriented"):
        raise RuntimeError("Merged mesh is not closed and consistently oriented")
    manifest["topology"] = topology
    atomic_json(manifest_path, manifest)

    exported = output / "dual_export"
    exported.mkdir(exist_ok=True)
    run_stage("export_8k", [python, str(SCRIPT / "run_dual_export.py"),
              str(raw), str(remesh / "cleaned_surface.pt"), str(closed),
              str(exported), "--size", "8192"],
              output, env, manifest, [exported / "model.glb", exported / "export_manifest.json"], uuids)
    export_report = json.loads((exported / "export_manifest.json").read_text())
    if not export_report.get("completed"):
        raise RuntimeError("8K PBR export manifest is incomplete")
    manifest["export"] = export_report
    manifest["completed"] = True
    atomic_json(manifest_path, manifest)

    warnings = list(generation.get("warnings", []))
    warnings.append("두 GPU 후처리는 실험 경로입니다. 형상·처마·이음새의 시각 품질을 확인하기 전까지 검토 필요 상태입니다.")
    metadata = output / "metadata.json"
    atomic_json(metadata, {
        "model_path": str(exported / "model.glb"), "used_texture": True,
        "actual_resolution": 1536, "texture_size": 8192,
        "quality_status": "review_required", "warnings": warnings,
        "postprocess_checkpoint": str(raw),
        "dual_postprocess_manifest": str(manifest_path),
        "gpu_memory_measurements": {name: stage["native_process_peak_mib_by_gpu"]
                                    for name, stage in manifest["stages"].items()},
        "gpu_work": {"mesh_faces": json.loads((mesh / "summary.json").read_text())["gpu_face_totals"],
                     "tile_pixels": {str(gpu): export_report["stages"][f"tile_gpu_{gpu}"]["valid_pixels"]
                                     for gpu in (0, 1)}},
    })
    atomic_json(result_path, {
        "model_path": str(exported / "model.glb"), "metadata_path": str(metadata),
        "used_texture": True, "warnings": warnings, "quality_status": "review_required",
    })
    emit("두 GPU 후처리 완료 · 시각 품질 검토 필요")


if __name__ == "__main__":
    main()
