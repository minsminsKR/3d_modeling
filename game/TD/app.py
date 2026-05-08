from __future__ import annotations

import mimetypes
from pathlib import Path
from urllib.parse import quote

from flask import Flask, abort, jsonify, render_template, send_file


APP_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = APP_DIR.parents[1]
MODEL_ROOT = PROJECT_ROOT / "model_test" / "model"
CYCLOPSE_ROOT = MODEL_ROOT / "cyclopse"

mimetypes.add_type("model/vnd.autodesk.fbx", ".fbx")
mimetypes.add_type("image/jpeg", ".jpg")
mimetypes.add_type("image/jpeg", ".jpeg")
mimetypes.add_type("text/javascript", ".js")
mimetypes.add_type("text/css", ".css")

app = Flask(__name__)


def model_url(relative_path: str) -> str:
    return f"/model/{quote(relative_path, safe='/')}"


def asset_entry(label: str, relative_path: str, asset_type: str) -> dict:
    path = MODEL_ROOT / relative_path
    entry = {
        "label": label,
        "path": relative_path,
        "url": model_url(relative_path),
        "type": asset_type,
        "exists": path.is_file(),
    }

    if path.is_file():
        entry["sizeBytes"] = path.stat().st_size
        entry["contentType"] = mimetypes.guess_type(path.name)[0] or "application/octet-stream"

    return entry


@app.get("/")
def index():
    return render_template("index.html")


@app.get("/favicon.ico")
def favicon():
    return ("", 204)


@app.get("/api/assets")
def assets():
    walk = asset_entry("walking", "cyclopse/mixamo/Walking.fbx", "animation")
    jump = asset_entry("jump", "cyclopse/mixamo/Jump.fbx", "animation")
    cyclopse_assets = [walk, jump]

    texture_path = CYCLOPSE_ROOT / "source" / "model_textured.jpg"
    texture = None
    if texture_path.is_file():
        texture = asset_entry("texture", "cyclopse/source/model_textured.jpg", "texture")
        cyclopse_assets.append(texture)

    return jsonify(
        {
            "character": "cyclopse",
            "modelRoot": "/model/",
            "monster": {
                "name": "cyclopse",
                "walk": walk["url"] if walk["exists"] else None,
                "jump": jump["url"] if jump["exists"] else None,
                "texture": texture["url"] if texture and texture["exists"] else None,
            },
            "assets": cyclopse_assets,
        }
    )


@app.get("/model/<path:relative_path>")
def model_file(relative_path: str):
    requested_path = (MODEL_ROOT / relative_path).resolve()
    model_root = MODEL_ROOT.resolve()

    if requested_path != model_root and model_root not in requested_path.parents:
        abort(403)

    if not requested_path.is_file():
        abort(404)

    guessed_type = mimetypes.guess_type(requested_path.name)[0] or "application/octet-stream"
    return send_file(requested_path, mimetype=guessed_type, conditional=True)


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8123, debug=False)
