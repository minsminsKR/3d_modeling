from __future__ import annotations

import importlib
import os
import sys
from pathlib import Path


def check_module(name: str) -> str:
    try:
        importlib.import_module(name)
        return "ok"
    except Exception as exc:
        return f"missing ({exc})"


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    env_path = root / ".env"
    if env_path.exists():
        for line in env_path.read_text(encoding="utf-8").splitlines():
            if not line or line.strip().startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            os.environ.setdefault(key.strip(), value.strip())

    repo = os.getenv("HUNYUAN3D_REPO")
    print(f"Python: {sys.version.split()[0]}")
    print(f"Flask: {check_module('flask')}")
    print(f"Pillow: {check_module('PIL')}")
    print(f"pkg_resources: {check_module('pkg_resources')}")
    print(f"HUNYUAN3D_REPO: {repo or 'not set'}")

    if repo:
        repo_path = Path(repo)
        print(f"Repo exists: {repo_path.exists()}")
        for rel in ["hy3dshape", "hy3dpaint"]:
            print(f"{rel}: {(repo_path / rel).exists()}")
        for path in [repo_path, repo_path / "hy3dshape", repo_path / "hy3dpaint"]:
            if str(path) not in sys.path:
                sys.path.insert(0, str(path))
        print(f"hy3dshape import: {check_module('hy3dshape')}")

    try:
        import torch

        print(f"torch: {torch.__version__}")
        print(f"cuda available: {torch.cuda.is_available()}")
        if torch.cuda.is_available():
            print(f"cuda device: {torch.cuda.get_device_name(0)}")
    except Exception as exc:
        print(f"torch: missing ({exc})")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
