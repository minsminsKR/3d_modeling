#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
python3 scripts/fetch_upstream.py --only TRELLIS.2 trellis-extensions
python3 -m venv .venv-trellis
source .venv-trellis/bin/activate
export CUDA_HOME=/usr/local/cuda
export TORCH_CUDA_ARCH_LIST=8.0
export MAX_JOBS=4
export PIP_NO_INPUT=1
python -m pip install pip==25.2 setuptools==75.8.0 wheel
python -m pip install torch==2.6.0 torchvision==0.21.0 --index-url https://download.pytorch.org/whl/cu124
python -m pip install numpy==1.26.4 opencv-python-headless==4.11.0.86 transformers==4.57.6 huggingface_hub==0.36.0 pillow==11.3.0 imageio imageio-ffmpeg tqdm easydict ninja trimesh tensorboard pandas lpips zstandard kornia timm accelerate
python -m pip install git+https://github.com/EasternJournalist/utils3d.git@9a4eb15e4021b67b12c460c7057d642626897ec8
python -m pip install flash-attn==2.7.3 --no-build-isolation
EXT="$ROOT/vendor/trellis-extensions"
mkdir -p "$EXT"
python -m pip install "$EXT/nvdiffrast" --no-build-isolation
python -m pip install "$EXT/nvdiffrec" --no-build-isolation
python -m pip install "$EXT/CuMesh" --no-build-isolation
python -m pip install "$EXT/FlexGEMM" --no-build-isolation
python -m pip install "$ROOT/vendor/TRELLIS.2/o-voxel" --no-build-isolation
python -m pip check
python -m pip freeze > requirements-trellis-a100.lock.txt
printf 'TRELLIS_SETUP_COMPLETE\n'
