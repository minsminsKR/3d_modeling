#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p logs
export CUDA_HOME=/usr/local/cuda
export PATH="$PWD/.venv/bin:$CUDA_HOME/bin:$PATH"
exec .venv/bin/gunicorn --bind 0.0.0.0:5001 --workers 1 --threads 4 --timeout 300 --access-logfile logs/access.log --error-logfile logs/webapp.log app:app
