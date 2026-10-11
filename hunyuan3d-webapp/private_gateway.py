"""Password-protected, streaming public entry point for the existing A100 app.

The GPU application stays on its existing LAN address. Only this independent
gateway, bound to loopback, is exposed by cloudflared.
"""
from __future__ import annotations

import json
import secrets
import threading
import time
from collections import OrderedDict
from datetime import timedelta
from pathlib import Path
from urllib.parse import urlsplit, urlunsplit

import requests
from flask import Flask, Response, abort, redirect, render_template_string, request, session, stream_with_context
from werkzeug.security import check_password_hash

BASE = Path(__file__).resolve().parent
UPSTREAM = "http://127.0.0.1:5001"
HOP_HEADERS = {"connection", "keep-alive", "proxy-authenticate", "proxy-authorization",
               "te", "trailer", "transfer-encoding", "upgrade", "host", "cookie", "authorization"}
SAFE_METHODS = {"GET", "HEAD", "OPTIONS"}
LOGIN_HTML = """<!doctype html><html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex,nofollow">
<title>A100 Studio · 비공개 접속</title><style>
*{box-sizing:border-box}body{margin:0;background:#f7f3e5;color:#24382a;font-family:system-ui,sans-serif;display:grid;min-height:100vh;place-items:center;padding:24px}
main{width:min(100%,420px);background:#fffdf7;border:1px solid #dedfcf;border-radius:16px;padding:32px;box-shadow:0 12px 40px #24382a0a}
.tag{font-size:12px;font-weight:700;letter-spacing:2px;color:#376447}h1{font-size:28px;margin:12px 0}p{line-height:1.65;color:#647066;font-size:14px}label{display:block;font-size:14px;font-weight:600;margin:24px 0 8px}
input,button{width:100%;min-height:46px;font:inherit;border-radius:8px}input{padding:10px 12px;border:1px solid #bdc9bb;background:white}input:focus-visible,button:focus-visible{outline:3px solid #a5c7a0;outline-offset:3px}
button{margin-top:16px;background:#315a3d;color:white;border:0;font-weight:700;cursor:pointer}.error{color:#a2342b}small{display:block;color:#747b70;font-size:12px;line-height:1.6;margin-top:24px}
</style></head><body><main><div class="tag">A100 · PRIVATE STUDIO</div><h1>비밀번호로 접속하세요</h1>
<p>3D 생성과 리깅은 A100 서버에서 실행됩니다.<br>초대받은 사람만 이 작업 공간을 사용할 수 있습니다.</p>
{% if error %}<p class="error" role="alert">{{ error }}</p>{% endif %}
<form method="post"><input type="hidden" name="csrf" value="{{ csrf }}"><input type="hidden" name="next" value="{{ next_path }}">
<label for="password">접속 비밀번호</label><input id="password" name="password" type="password" autocomplete="current-password" required autofocus maxlength="256">
<button type="submit">작업 공간 열기</button></form><small>접속 권한이 있으면 생성 결과와 저장된 모델을 함께 볼 수 있습니다.<br>주소와 비밀번호는 허가한 사람에게만 전달하세요.</small></main></body></html>"""


def safe_next(value: str | None) -> str:
    if not value or not value.startswith("/") or value.startswith("//") or "\\" in value or any(ord(c) < 32 for c in value):
        return "/"
    if value.startswith("/access/"):
        return "/"
    return value


def create_gateway(config_path: Path | None = None, upstream: str = UPSTREAM) -> Flask:
    config_path = config_path or BASE / "private-access" / "credentials.json"
    config = json.loads(config_path.read_text())
    if not config.get("secret_key") or not config.get("password_hash") or not config.get("auth_epoch"):
        raise RuntimeError("Private gateway credentials must be provisioned before startup")
    app = Flask(__name__, static_folder=None)
    app.config.update(SECRET_KEY=config["secret_key"], MAX_CONTENT_LENGTH=90 * 1024 * 1024,
                      SESSION_COOKIE_NAME="a100_private", SESSION_COOKIE_SECURE=True,
                      SESSION_COOKIE_HTTPONLY=True, SESSION_COOKIE_SAMESITE="Strict",
                      PERMANENT_SESSION_LIFETIME=timedelta(hours=12), SESSION_REFRESH_EACH_REQUEST=False)
    attempts: OrderedDict[str, list[float]] = OrderedDict()
    lock = threading.Lock()

    def origins_match() -> bool:
        # Browser POSTs (including fetch and multipart forms) carry Origin.
        # Requiring it also keeps ambient login cookies from authorizing CSRF.
        origin = request.headers.get("Origin", "")
        parsed = urlsplit(origin)
        return parsed.scheme == "https" and parsed.netloc.lower() == request.host.lower() and not parsed.path

    def allowed_host() -> bool:
        path = config_path.parent / "public-address.json"
        expected = set(config.get("local_hosts", ["127.0.0.1:5002", "localhost:5002"]))
        if path.exists():
            expected.add(urlsplit(json.loads(path.read_text()).get("url", "")).netloc.lower())
        return request.host.lower() in expected

    def authenticated() -> bool:
        return secrets.compare_digest(str(session.get("auth_epoch", "")), config["auth_epoch"])

    def ip_key() -> str:
        # This process binds loopback; cloudflared supplies the client address.
        return request.headers.get("CF-Connecting-IP", request.remote_addr or "unknown")[:80]

    def is_limited(add: bool = False) -> bool:
        now = time.monotonic()
        key = ip_key()
        with lock:
            recent = [stamp for stamp in attempts.pop(key, []) if now - stamp < 900]
            limited = len(recent) >= 8
            if add and not limited:
                recent.append(now)
            attempts[key] = recent
            while len(attempts) > 2048:
                attempts.popitem(last=False)
            return limited

    @app.before_request
    def guard():
        if not allowed_host():
            abort(421)
        if request.host.lower() not in set(config.get("local_hosts", ["127.0.0.1:5002", "localhost:5002"])) and request.headers.get("X-Forwarded-Proto", "").lower() != "https":
            return redirect("https://" + request.host + request.full_path.rstrip("?"), code=308)
        if request.endpoint in {"login", "logout", "account"}:
            return None
        if not authenticated():
            if request.path.startswith("/api/") or request.method not in SAFE_METHODS:
                return {"error": "authentication_required", "login_url": "/access/login"}, 401
            return redirect("/access/login?next=" + requests.utils.quote(safe_next(request.full_path.rstrip("?")), safe=""))
        if request.method not in SAFE_METHODS and not origins_match():
            return {"error": "cross_origin_request_rejected"}, 403

    @app.after_request
    def security_headers(response):
        response.headers["X-Robots-Tag"] = "noindex, nofollow, noarchive"
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["Referrer-Policy"] = "same-origin"
        response.headers["Cache-Control"] = "private, no-store"
        response.headers["Vary"] = "Cookie"
        response.headers["Strict-Transport-Security"] = "max-age=31536000"
        if request.path.startswith("/access/"):
            response.headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'"
        return response

    @app.route("/access/login", methods=["GET", "POST"])
    def login():
        if request.method == "GET":
            if authenticated():
                return redirect(safe_next(request.args.get("next")))
            session.setdefault("csrf", secrets.token_urlsafe(32))
            return render_template_string(LOGIN_HTML, error=None, csrf=session["csrf"], next_path=safe_next(request.args.get("next")))
        if not origins_match() or not secrets.compare_digest(str(session.get("csrf", "")), str(request.form.get("csrf", ""))) or not session.get("csrf"):
            abort(403)
        if is_limited():
            return render_template_string(LOGIN_HTML, error="로그인 시도가 많습니다. 15분 후 다시 시도하세요.", csrf=session["csrf"], next_path=safe_next(request.form.get("next"))), 429
        password = request.form.get("password", "")
        if len(password) > 256 or not check_password_hash(config["password_hash"], password):
            is_limited(add=True)
            return render_template_string(LOGIN_HTML, error="비밀번호를 확인하세요.", csrf=session["csrf"], next_path=safe_next(request.form.get("next"))), 401
        with lock:
            attempts.pop(ip_key(), None)
        next_path = safe_next(request.form.get("next"))
        session.clear()
        session.permanent = True
        session["auth_epoch"] = config["auth_epoch"]
        session["csrf"] = secrets.token_urlsafe(32)
        return redirect(next_path, code=303)

    @app.get("/access")
    def account():
        if not authenticated():
            return redirect("/access/login")
        return render_template_string("<!doctype html><html lang=ko><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>접속 관리</title><body><p>이 브라우저는 A100 작업 공간에 로그인되어 있습니다.</p><p><a href='/'>작업 공간으로 돌아가기</a></p><form method=post action='/access/logout'><input type=hidden name=csrf value='{{ csrf }}'><button>로그아웃</button></form></body></html>", csrf=session["csrf"])

    @app.post("/access/logout")
    def logout():
        if not origins_match() or not session.get("csrf") or not secrets.compare_digest(str(session["csrf"]), str(request.form.get("csrf", ""))):
            abort(403)
        session.clear()
        return redirect("/access/login", code=303)

    @app.route("/", defaults={"path": ""}, methods=["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"])
    @app.route("/<path:path>", methods=["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"])
    def proxy(path: str):
        target = upstream + "/" + requests.utils.quote(path, safe="/@:+")
        if request.query_string:
            target += "?" + request.query_string.decode("ascii", errors="strict")
        headers = {key: value for key, value in request.headers.items() if key.lower() not in HOP_HEADERS and not key.lower().startswith(("cf-", "x-forwarded-"))}
        headers["Accept-Encoding"] = "identity"
        # Prevent client-supplied Host / forwarding headers from influencing the
        # GPU app. Its links are relative, and upstream redirects are rewritten.
        headers["Host"] = urlsplit(upstream).netloc
        transport = requests.Session()
        transport.trust_env = False
        try:
            upstream_response = transport.request(request.method, target, headers=headers,
                                                 data=request.get_data(cache=False) if request.method not in SAFE_METHODS else None,
                                                 stream=True, allow_redirects=False, timeout=(5, 300))
        except requests.RequestException:
            transport.close()
            return "A100 앱에 연결할 수 없습니다. 잠시 후 다시 시도하세요.", 502
        out_headers = {key: value for key, value in upstream_response.headers.items()
                       if key.lower() not in HOP_HEADERS | {"set-cookie"}}
        if "Location" in out_headers:
            location = urlsplit(out_headers["Location"])
            if location.netloc == urlsplit(upstream).netloc:
                out_headers["Location"] = urlunsplit(("", "", location.path, location.query, location.fragment))
        if request.method == "HEAD":
            upstream_response.close()
            transport.close()
            return Response(status=upstream_response.status_code, headers=out_headers)
        if upstream_response.is_redirect:
            # Requests consumes redirect bodies while preparing Response.next,
            # even with allow_redirects=False. Streaming raw would emit zero
            # bytes under the original Content-Length and trigger tunnel EOF.
            body = upstream_response.content
            out_headers = {key: value for key, value in out_headers.items()
                           if key.lower() not in {"content-length", "content-encoding"}}
            status = upstream_response.status_code
            upstream_response.close()
            transport.close()
            return Response(body, status=status, headers=out_headers)

        @stream_with_context
        def content():
            try:
                yield from upstream_response.raw.stream(128 * 1024, decode_content=False)
            finally:
                upstream_response.close()
                transport.close()

        return Response(content(), status=upstream_response.status_code, headers=out_headers, direct_passthrough=True)

    return app


# Tests import the factory without provisioning a production secret.
if (BASE / "private-access" / "credentials.json").exists():
    app = create_gateway()
