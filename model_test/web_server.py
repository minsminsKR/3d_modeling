from __future__ import annotations

import argparse
import json
import mimetypes
import zipfile
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import quote, unquote, urlparse


APP_DIR = Path(__file__).resolve().parent
WEB_DIR = APP_DIR / "web"
MODEL_DIR = APP_DIR / "model"
EXTRACTED_DIR = MODEL_DIR / "_extracted"

SUPPORTED_MODEL_EXTENSIONS = {
    ".fbx": "Autodesk FBX",
    ".glb": "Binary glTF",
    ".gltf": "glTF",
    ".obj": "Wavefront OBJ",
    ".mtl": "Wavefront Material",
    ".jpg": "Texture Image",
    ".jpeg": "Texture Image",
    ".png": "Texture Image",
    ".webp": "Texture Image",
}

PRIMARY_MODEL_PRIORITY = {
    ".fbx": 0,
    ".glb": 1,
    ".gltf": 2,
    ".obj": 3,
}

CATEGORY_PRIORITY = {
    "mixamo": 0,
    "exports": 1,
    "source": 2,
    "_extracted": 3,
}


class ModelTestRequestHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs) -> None:
        super().__init__(*args, directory=str(WEB_DIR), **kwargs)

    def do_GET(self) -> None:
        parsed_url = urlparse(self.path)

        if parsed_url.path == "/api/models":
            self.send_models()
            return

        if parsed_url.path.startswith("/model/"):
            self.send_model_file(parsed_url.path.removeprefix("/model/"))
            return

        if parsed_url.path == "/":
            self.path = "/index.html"

        super().do_GET()

    def send_models(self) -> None:
        MODEL_DIR.mkdir(exist_ok=True)
        extract_zip_files()
        characters = group_character_assets(discover_model_assets())
        self.send_json({"models": characters})

    def send_model_file(self, relative_path: str) -> None:
        requested_path = Path(unquote(relative_path))
        model_path = (MODEL_DIR / requested_path).resolve()
        model_root = MODEL_DIR.resolve()

        if model_root not in model_path.parents and model_path != model_root:
            self.send_error(403, "Model path is not allowed")
            return

        if not model_path.is_file():
            self.send_error(404, "Model file not found")
            return

        guessed_type = mimetypes.guess_type(model_path.name)[0] or "application/octet-stream"
        self.send_response(200)
        self.send_header("Content-Type", guessed_type)
        self.send_header("Content-Length", str(model_path.stat().st_size))
        self.end_headers()

        with model_path.open("rb") as model_file:
            self.copyfile(model_file, self.wfile)

    def send_json(self, data: dict) -> None:
        payload = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the browser-based model test map.")
    parser.add_argument("--host", default="0.0.0.0", help="Host to bind. Default: 0.0.0.0")
    parser.add_argument("--port", type=int, default=8000, help="Port to bind. Default: 8000")
    return parser.parse_args()


def extract_zip_files() -> None:
    EXTRACTED_DIR.mkdir(parents=True, exist_ok=True)

    for zip_path in sorted(MODEL_DIR.rglob("*.zip")):
        if EXTRACTED_DIR in zip_path.parents:
            continue
        if "packages" in zip_path.relative_to(MODEL_DIR).parts:
            continue

        zip_relative_parent = zip_path.parent.relative_to(MODEL_DIR)
        target_dir = EXTRACTED_DIR / zip_relative_parent / zip_path.stem
        marker_file = target_dir / ".extracted_from_zip"

        if marker_file.is_file() and marker_file.read_text(encoding="utf-8") == str(zip_path.stat().st_mtime_ns):
            continue

        target_dir.mkdir(parents=True, exist_ok=True)

        with zipfile.ZipFile(zip_path) as archive:
            for member in archive.infolist():
                member_path = Path(member.filename)

                if member.is_dir() or member_path.is_absolute() or ".." in member_path.parts:
                    continue

                destination = target_dir / member_path
                destination.parent.mkdir(parents=True, exist_ok=True)

                with archive.open(member) as source, destination.open("wb") as output:
                    output.write(source.read())

        marker_file.write_text(str(zip_path.stat().st_mtime_ns), encoding="utf-8")


def discover_model_assets() -> list[dict]:
    assets = []

    for path in sorted(MODEL_DIR.rglob("*")):
        if not path.is_file():
            continue

        extension = path.suffix.lower()
        if extension not in SUPPORTED_MODEL_EXTENSIONS:
            continue

        relative_path = path.relative_to(MODEL_DIR).as_posix()
        relative_parts = path.relative_to(MODEL_DIR).parts
        assets.append(
            {
                "name": relative_path,
                "url": f"/model/{quote(relative_path, safe='/')}",
                "extension": extension,
                "type": SUPPORTED_MODEL_EXTENSIONS[extension],
                "category": get_asset_category(relative_parts),
                "sizeBytes": path.stat().st_size,
            }
        )

    return assets


def group_character_assets(assets: list[dict]) -> list[dict]:
    grouped_assets: dict[str, list[dict]] = {}

    for asset in assets:
        grouped_assets.setdefault(get_character_name(asset["name"]), []).append(asset)

    characters = []
    for character_name, character_assets in sorted(grouped_assets.items()):
        sorted_assets = sorted(
            character_assets,
            key=lambda asset: (
                CATEGORY_PRIORITY.get(asset["category"], 99),
                PRIMARY_MODEL_PRIORITY.get(asset["extension"], 99),
                "walking" not in asset["name"].lower(),
                asset["name"],
            ),
        )
        primary_asset = sorted_assets[0]

        characters.append(
            {
                "name": character_name,
                "url": primary_asset["url"],
                "extension": primary_asset["extension"],
                "type": primary_asset["type"],
                "sizeBytes": primary_asset["sizeBytes"],
                "primaryAsset": primary_asset,
                "assets": sorted_assets,
            }
        )

    return characters


def get_character_name(relative_path: str) -> str:
    parts = Path(relative_path).parts

    if len(parts) >= 2 and parts[0] == "_extracted":
        return parts[1]

    if len(parts) >= 2:
        return parts[0]

    return Path(relative_path).stem


def get_asset_category(relative_parts: tuple[str, ...]) -> str:
    if len(relative_parts) >= 3 and relative_parts[0] == "_extracted":
        return "_extracted"
    if len(relative_parts) >= 2 and relative_parts[1] in {"source", "mixamo", "exports"}:
        return relative_parts[1]
    return "root"


def main() -> None:
    args = parse_args()
    MODEL_DIR.mkdir(exist_ok=True)
    WEB_DIR.mkdir(exist_ok=True)

    server = ThreadingHTTPServer((args.host, args.port), ModelTestRequestHandler)
    print(f"Model test web server: http://{args.host}:{args.port}")
    print("브라우저에서 서버 IP와 포트로 접속하세요. 종료: Ctrl+C")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nServer stopped.")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
