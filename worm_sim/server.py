# -*- coding: utf-8 -*-
"""꼬마선충 커넥톰 시뮬레이션 정적 웹 서버.

라우팅:
  /assets/... -> game/assets/...            (캐릭터 텍스처)
  /model/...  -> model_test/model/...       (사이클롭스 FBX)
  그 외       -> worm_sim/web/...           (시뮬레이션 앱)

외부 패키지 없이 파이썬 표준 라이브러리만 사용한다.
"""
import argparse
import posixpath
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit

BASE_DIR = Path(__file__).resolve().parent
WORKSPACE = BASE_DIR.parent

ROUTES = [
    ("/assets/", WORKSPACE / "game" / "assets"),
    ("/model/", WORKSPACE / "model_test" / "model"),
    ("/", BASE_DIR / "web"),
]


class SimHandler(SimpleHTTPRequestHandler):
    def translate_path(self, path):
        path = posixpath.normpath(unquote(urlsplit(path).path))
        if not path.startswith("/"):
            path = "/" + path
        for prefix, root in ROUTES:
            if path.startswith(prefix) or path + "/" == prefix:
                rel = path[len(prefix):]
                target = (root / rel).resolve()
                if not str(target).startswith(str(root.resolve())):
                    return str(root / "__forbidden__")
                if target.is_dir():
                    target = target / "index.html"
                return str(target)
        return str(BASE_DIR / "web" / "index.html")

    def end_headers(self):
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def log_message(self, fmt, *args):
        print(fmt % args)


def main():
    parser = argparse.ArgumentParser(description="Worm connectome simulation server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8090)
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), SimHandler)
    print(f"C. elegans connectome sim: http://{args.host}:{args.port}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        server.shutdown()


if __name__ == "__main__":
    main()
