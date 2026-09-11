#!/usr/bin/env bash
# Idempotent Cloud Agent setup for the 3d_modeling repository.
# Prepares a Python virtual environment with the web-layer dependencies
# shared by the Flask/stdlib browser apps. The heavy GPU pipeline
# (torch+cu124, Hunyuan3D weights) is intentionally NOT installed here:
# it requires an NVIDIA GPU and ~30 GB of model weights that are absent
# in the Cloud Agent VM.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Ensure Python venv support exists (Ubuntu ships python3 without ensurepip).
if ! python3 -c "import ensurepip" >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo apt-get install -y python3-venv
fi

# Create the virtual environment on first run only.
if [ ! -x ".venv/bin/python" ]; then
  python3 -m venv .venv
fi

# shellcheck disable=SC1091
. .venv/bin/activate

python -m pip install --upgrade pip
pip install -r hunyuan3d-webapp/requirements-web.txt

echo "install.sh: done. Activate with: source .venv/bin/activate"
