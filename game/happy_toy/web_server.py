from __future__ import annotations

"""Happy Toy 정적 개발 서버.

이 파일은 브라우저가 FBX, JPG, JS 모듈을 올바른 MIME 타입으로
받을 수 있게 해주는 작은 HTTP 서버입니다. 게임 로직은 모두
`src/` 아래 JS 모듈에 있고, 이 서버는 파일 제공만 담당합니다.
"""

import argparse
import mimetypes
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse


APP_DIR = Path(__file__).resolve().parent

mimetypes.add_type("text/javascript", ".js")
mimetypes.add_type("text/css", ".css")
mimetypes.add_type("application/octet-stream", ".fbx")
mimetypes.add_type("model/gltf-binary", ".glb")
mimetypes.add_type("model/gltf+json", ".gltf")


class HappyToyRequestHandler(SimpleHTTPRequestHandler):
    """루트 경로를 `happy_toy` 폴더로 고정하는 요청 핸들러."""

    def __init__(self, *args, **kwargs) -> None:
        super().__init__(*args, directory=str(APP_DIR), **kwargs)

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

    def end_headers(self) -> None:
        self.send_header("Cache-Control", "no-store, max-age=0")
        self.send_header("Pragma", "no-cache")
        self.send_header("Expires", "0")
        super().end_headers()


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
