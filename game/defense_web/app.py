from __future__ import annotations

import mimetypes
import argparse
from pathlib import Path

from flask import Flask, Response, render_template, send_from_directory


APP_DIR = Path(__file__).resolve().parent
GAME_DIR = APP_DIR.parent
HAPPY_TOY_DIR = GAME_DIR / "happy_toy"

mimetypes.add_type("text/javascript", ".js")
mimetypes.add_type("text/css", ".css")
mimetypes.add_type("application/octet-stream", ".fbx")
mimetypes.add_type("model/gltf-binary", ".glb")
mimetypes.add_type("model/gltf+json", ".gltf")

app = Flask(__name__, static_folder="static", template_folder="templates")


@app.get("/")
def index():
    return render_template("index.html")


@app.get("/health")
def health():
    return {"ok": True}


@app.get("/favicon.ico")
def favicon():
    return Response(status=204)


@app.get("/vendor/<path:path>")
def vendor(path: str):
    return send_from_directory(HAPPY_TOY_DIR / "vendor", path)


@app.get("/assets/<path:path>")
def assets(path: str):
    return send_from_directory(HAPPY_TOY_DIR / "assets", path)


@app.after_request
def add_headers(response):
    response.headers["Cache-Control"] = "no-store, max-age=0"
    response.headers["Pragma"] = "no-cache"
    return response


def parse_args():
    parser = argparse.ArgumentParser(description="Run Defense Web.")
    parser.add_argument("--host", default="127.0.0.1", help="Use 0.0.0.0 for LAN access.")
    parser.add_argument("--port", type=int, default=8020)
    parser.add_argument("--debug", action="store_true")
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    app.run(host=args.host, port=args.port, debug=args.debug)
