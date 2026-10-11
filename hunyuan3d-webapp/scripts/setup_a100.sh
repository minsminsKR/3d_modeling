#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
python3 scripts/fetch_upstream.py --only Hunyuan3D-2.1 Hunyuan3D-Omni
if [ ! -x .venv/bin/python ]; then python3 -m venv .venv; fi
export CUDA_HOME=/usr/local/cuda
export PATH="$PWD/.venv/bin:$CUDA_HOME/bin:$PATH"
export TORCH_CUDA_ARCH_LIST=8.0
export MAX_JOBS=4
if ! python -c 'import torch' >/dev/null 2>&1; then
    python -m pip install torch==2.5.1 torchvision==0.20.1 torchaudio==2.5.1 --index-url https://download.pytorch.org/whl/cu124
fi
python -m pip install numpy==1.24.4 cython pybind11==2.13.4
python -m pip install --no-build-isolation -r requirements-a100.txt
DS_BUILD_OPS=0 python -m pip install --no-build-isolation deepspeed==0.17.6
python -m pip install --no-build-isolation -e vendor/Hunyuan3D-2.1/hy3dpaint/custom_rasterizer
cd vendor/Hunyuan3D-2.1/hy3dpaint/DifferentiableRenderer
bash compile_mesh_painter.sh
cd "$ROOT"
python scripts/repair_wheel_metadata.py
python -m pip check
python -m pip freeze > requirements-a100.lock.txt
echo SETUP_COMPLETE
