"""Silent local gallery for actual new-creature game GLB exports.

Run with Python 3.10+: python preview_server.py --port 8062
Then open http://127.0.0.1:8062/. No pip/npm install or public tunnel is used.
Three.js r164 comes from this repository's game/happy_toy/vendor/three (MIT).
Only {key}/previews/{key}_Game.glb exports are exposed, never placeholder meshes.
These optional derivatives must be reproduced from the preserved .blend/FBX.
"""
from __future__ import annotations

import argparse
import json
import mimetypes
import struct
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit


TOOL_ROOT = Path(__file__).resolve().parent
ART_ROOT = TOOL_ROOT.parent
REPO_ROOT = Path(__file__).resolve().parents[4]
THREE_ROOT = REPO_ROOT / "game" / "happy_toy" / "vendor" / "three"
CREATURES = (("Uncat", "운냥이"), ("Cyclopse", "사이클롭스"), ("Baby", "베이비"))
MODEL_PATHS = {key: ART_ROOT / key / "previews" / f"{key}_Game.glb" for key, _ in CREATURES}
mimetypes.add_type("application/javascript", ".js")
mimetypes.add_type("model/gltf-binary", ".glb")


def describe_model(key, label):
    path = MODEL_PATHS[key]
    entry = {"key": key, "label": label, "url": f"/models/{key}.glb", "ready": False, "bytes": 0, "stamp": 0}
    if not path.is_file():
        return entry
    stat = path.stat()
    try:
        with path.open("rb") as stream:
            header = stream.read(12)
        magic, version, size = struct.unpack("<III", header)
    except (OSError, struct.error):
        return entry
    # Do not announce an export while Blender is still writing it.
    if magic == 0x46546C67 and version == 2 and size == stat.st_size:
        entry.update(ready=True, bytes=stat.st_size, stamp=stat.st_mtime_ns)
    return entry


class GalleryHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(TOOL_ROOT), **kwargs)

    def translate_path(self, path):
        route = unquote(urlsplit(path).path)
        if route.startswith("/vendor/three/"):
            candidate = (THREE_ROOT / route.removeprefix("/vendor/three/")).resolve()
            if candidate.is_relative_to(THREE_ROOT.resolve()):
                return str(candidate)
        elif route.startswith("/models/"):
            key = route.removeprefix("/models/").removesuffix(".glb")
            if key in MODEL_PATHS and route == f"/models/{key}.glb":
                return str(MODEL_PATHS[key])
        elif route in ("/", "/viewer.html"):
            return str(TOOL_ROOT / "viewer.html")
        return str(TOOL_ROOT / "__not_found__")

    def do_GET(self):
        if urlsplit(self.path).path == "/api/models":
            payload = json.dumps({"models": [describe_model(key, label) for key, label in CREATURES]}, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(payload)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(payload)
            return
        super().do_GET()

    def end_headers(self):
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        super().end_headers()

    def log_message(self, format, *args):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8062)
    options = parser.parse_args()
    if not 1 <= options.port <= 65535:
        parser.error("port must be 1..65535")
    for relative in ("build/three.module.js", "examples/jsm/loaders/GLTFLoader.js", "examples/jsm/utils/BufferGeometryUtils.js"):
        if not (THREE_ROOT / relative).is_file():
            parser.error("Missing pinned repository dependency: game/happy_toy/vendor/three/" + relative)
    server = ThreadingHTTPServer(("127.0.0.1", options.port), GalleryHandler)
    print(f"Silent creature gallery: http://127.0.0.1:{options.port}/", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
