from __future__ import annotations

"""Happy Toy 정적 개발 서버.

이 파일은 브라우저가 FBX, JPG, JS 모듈을 올바른 MIME 타입으로
받을 수 있게 해주는 작은 HTTP 서버입니다. 게임 로직은 모두
`src/` 아래 JS 모듈에 있고, 이 서버는 파일 제공만 담당합니다.
"""

import argparse
import json
import mimetypes
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse


APP_DIR = Path(__file__).resolve().parent
GAME_DIR = APP_DIR.parent
ASSET_DIR = GAME_DIR / "assets"
MAP_OVERRIDE_PATH = APP_DIR / "src" / "config" / "mapConfigOverride.js"
MAX_EDITOR_PAYLOAD_BYTES = 8 * 1024 * 1024

mimetypes.add_type("text/javascript", ".js")
mimetypes.add_type("text/css", ".css")
mimetypes.add_type("application/octet-stream", ".fbx")
mimetypes.add_type("model/gltf-binary", ".glb")
mimetypes.add_type("model/gltf+json", ".gltf")


class HappyToyRequestHandler(SimpleHTTPRequestHandler):
    """루트 경로를 `happy_toy` 폴더로 고정하는 요청 핸들러."""

    def __init__(self, *args, **kwargs) -> None:
        super().__init__(*args, directory=str(APP_DIR), **kwargs)

    def translate_path(self, path: str) -> str:
        parsed_path = unquote(urlparse(path).path)
        if parsed_path == "/assets" or parsed_path.startswith("/assets/"):
            relative = parsed_path.removeprefix("/assets").lstrip("/")
            requested = (ASSET_DIR / relative).resolve()
            asset_root = ASSET_DIR.resolve()
            if requested != asset_root and asset_root not in requested.parents:
                return str(asset_root / "__forbidden__")
            return str(requested)
        return super().translate_path(path)

    def do_GET(self) -> None:
        parsed_url = urlparse(self.path)
        if parsed_url.path == "/favicon.ico":
            self.send_response(204)
            self.end_headers()
            return
        if parsed_url.path == "/":
            self.path = "/index.html"
        else:
            self.path = unquote(parsed_url.path)
        super().do_GET()

    def do_POST(self) -> None:
        parsed_url = urlparse(self.path)
        if parsed_url.path != "/api/editor/map-override":
            self.send_error(404, "Unknown editor API endpoint.")
            return

        content_length = int(self.headers.get("Content-Length", "0"))
        if content_length <= 0 or content_length > MAX_EDITOR_PAYLOAD_BYTES:
            self.send_json({"error": "Invalid or too large editor payload."}, status=413)
            return

        try:
            body = self.rfile.read(content_length).decode("utf-8")
            payload = json.loads(body)
            map_config = payload["mapConfig"]
            if not isinstance(map_config, dict):
                raise ValueError("mapConfig must be an object.")
            write_map_override(map_config)
        except Exception as error:
            self.send_json({"error": str(error)}, status=400)
            return

        self.send_json({
            "ok": True,
            "path": str(MAP_OVERRIDE_PATH.relative_to(APP_DIR)),
        })

    def do_DELETE(self) -> None:
        parsed_url = urlparse(self.path)
        if parsed_url.path != "/api/editor/map-override":
            self.send_error(404, "Unknown editor API endpoint.")
            return

        clear_map_override()
        self.send_json({
            "ok": True,
            "path": str(MAP_OVERRIDE_PATH.relative_to(APP_DIR)),
        })

    def send_json(self, payload: dict, status: int = 200) -> None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def end_headers(self) -> None:
        self.send_header("Cache-Control", "no-store, max-age=0")
        self.send_header("Pragma", "no-cache")
        self.send_header("Expires", "0")
        super().end_headers()


def write_map_override(map_config: dict) -> None:
    serialized = json.dumps(map_config, ensure_ascii=False, indent=2)
    MAP_OVERRIDE_PATH.write_text(
        "// Map Editor가 저장한 맵 override입니다.\n"
        "// null이면 gameConfig.js의 DEFAULT_MAP_CONFIG를 그대로 사용합니다.\n\n"
        f"export const MAP_CONFIG_OVERRIDE = {serialized};\n",
        encoding="utf-8",
    )


def clear_map_override() -> None:
    MAP_OVERRIDE_PATH.write_text(
        "// Map Editor가 저장한 맵 override입니다.\n"
        "// null이면 gameConfig.js의 DEFAULT_MAP_CONFIG를 그대로 사용합니다.\n\n"
        "export const MAP_CONFIG_OVERRIDE = null;\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run Happy Toy local web server.")
    parser.add_argument("--host", default="127.0.0.1", help="Host to bind.")
    parser.add_argument("--port", type=int, default=8010, help="Port to bind.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    server = ThreadingHTTPServer((args.host, args.port), HappyToyRequestHandler)
    print(f"Happy Toy: http://{args.host}:{args.port}", flush=True)
    print("Press Ctrl+C to stop.", flush=True)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nServer stopped.", flush=True)
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
