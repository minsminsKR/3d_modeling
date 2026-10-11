"""Serve the optional, silent Baeksugwi animation preview on loopback only.

Uses the repository's pinned Three.js r164 files; no CDN or npm installation.
Run from any directory: python preview_server.py --port 8061
"""
from __future__ import annotations

import argparse
import json
import mimetypes
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit


ART_ROOT = Path(__file__).resolve().parent
REPO_ROOT = Path(__file__).resolve().parents[3]
THREE_ROOT = REPO_ROOT / "game" / "happy_toy" / "vendor" / "three"
MODEL_NAMES = (
    ("previews/Baeksugwi_Run_Preview.glb", "미리보기 · 2K 텍스처"),
    ("Baeksugwi_Run.glb", "원본 품질 · 전체 텍스처"),
)
mimetypes.add_type("application/javascript", ".js")
mimetypes.add_type("model/gltf-binary", ".glb")


class PreviewHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ART_ROOT), **kwargs)

    def translate_path(self, path):
        route = unquote(urlsplit(path).path)
        if route.startswith("/vendor/three/"):
            candidate = (THREE_ROOT / route.removeprefix("/vendor/three/")).resolve()
            if candidate.is_relative_to(THREE_ROOT.resolve()):
                return str(candidate)
            return str(ART_ROOT / "__not_found__")
        return super().translate_path(path)

    def do_GET(self):
        route = urlsplit(self.path).path
        if route == "/api/models":
            plan_path = ART_ROOT / "skeleton-plan.json"
            ground_y = None
            if plan_path.is_file():
                with plan_path.open(encoding="utf-8") as stream:
                    ground_y = json.load(stream).get("floor")
            models = [
                {"file": name, "label": label, "bytes": (ART_ROOT / name).stat().st_size}
                for name, label in MODEL_NAMES if (ART_ROOT / name).is_file()
            ]
            payload = json.dumps({"models": models, "groundY": ground_y}, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(payload)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(payload)
            return
        if route == "/":
            self.path = "/preview.html"
        super().do_GET()

    def end_headers(self):
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        super().end_headers()

    def log_message(self, format, *args):
        # A large GLB can generate several requests. Keep the background server quiet.
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8061)
    options = parser.parse_args()
    required = (
        THREE_ROOT / "build" / "three.module.js",
        THREE_ROOT / "examples" / "jsm" / "loaders" / "GLTFLoader.js",
        THREE_ROOT / "examples" / "jsm" / "utils" / "BufferGeometryUtils.js",
    )
    missing = [str(path.relative_to(REPO_ROOT)) for path in required if not path.is_file()]
    if missing:
        parser.error("Missing repository dependencies: " + ", ".join(missing))
    if not 1 <= options.port <= 65535:
        parser.error("port must be 1..65535")
    server = ThreadingHTTPServer(("127.0.0.1", options.port), PreviewHandler)
    print(f"Baeksugwi silent preview: http://127.0.0.1:{options.port}/", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
