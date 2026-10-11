"""Exercise authentication, CSRF, private assets and real streamed proxy I/O."""
from __future__ import annotations
import hashlib
import json
import re
import secrets
import tempfile
import threading
import requests
from pathlib import Path

from flask import Flask, Response, jsonify, redirect, request
from werkzeug.security import generate_password_hash
from werkzeug.serving import make_server
from private_gateway import create_gateway, safe_next

BASE = Path(__file__).resolve().parent
cases = []


def check(name, condition):
    assert condition, name
    cases.append(name)


fixture = Flask("gateway-fixture")
binary = bytes(range(256)) * 16384


@fixture.route("/asset", methods=["GET", "HEAD"])
def asset():
    range_header = request.headers.get("Range")
    if range_header:
        return Response(binary[100:200], 206, headers={"Content-Range": f"bytes 100-199/{len(binary)}", "Accept-Ranges": "bytes", "Content-Length": "100"}, content_type="model/gltf-binary")
    return Response(binary, headers={"Content-Length": str(len(binary)), "Accept-Ranges": "bytes", "Content-Disposition": 'attachment; filename="private.glb"'}, content_type="model/gltf-binary")


@fixture.route("/echo", methods=["GET", "POST"])
def echo():
    return jsonify(body=request.get_data().decode(), auth=request.headers.get("Authorization"), cookie=request.headers.get("Cookie"), host=request.host, forwarded=request.headers.get("X-Forwarded-Host"))


@fixture.route("/redirect", methods=["GET", "POST"])
def redirected():
    return redirect(request.host_url + "asset")


server = make_server("127.0.0.1", 0, fixture, threaded=True)
thread = threading.Thread(target=server.serve_forever, daemon=True)
thread.start()
host = "https://private.example.invalid"
origin = {"Origin": host}
try:
    with tempfile.TemporaryDirectory(prefix="a100-private-gateway-test-") as folder:
        temp = Path(folder)
        password = secrets.token_urlsafe(24)
        credentials = temp / "credentials.json"
        credentials.write_text(json.dumps({"secret_key": secrets.token_hex(32), "password_hash": generate_password_hash(password), "auth_epoch": secrets.token_hex(32), "local_hosts": ["private.example.invalid"]}))
        app = create_gateway(credentials, f"http://127.0.0.1:{server.server_port}")
        app.testing = True
        client = app.test_client()
        for path in ("/", "/asset", "/static/private.js", "/health", "/jobs/private/rigging/model", "/access/unknown", "/access/%2e%2e/asset"):
            response = client.get(path, base_url=host)
            check("anonymous-protected:" + path, response.status_code == 302 and response.headers["Location"].startswith("/access/login"))
        check("anonymous-api", client.get("/api/jobs/private", base_url=host).status_code == 401)
        check("anonymous-gpu-submit", client.post("/jobs", base_url=host).status_code == 401)
        check("untrusted-host", client.get("/access/login", base_url="https://attacker.invalid").status_code == 421)
        response = client.get("/access/login", base_url=host)
        csrf = re.search(r'name="csrf" value="([^"]+)"', response.text).group(1)
        check("login-cookie-flags", all(x in response.headers["Set-Cookie"] for x in ("Secure", "HttpOnly", "SameSite=Strict")))
        check("login-csp", "frame-ancestors 'none'" in response.headers["Content-Security-Policy"])
        check("login-csrf", client.post("/access/login", base_url=host, headers=origin, data={"password": password, "csrf": "bad"}).status_code == 403)
        check("login-origin", client.post("/access/login", base_url=host, data={"password": password, "csrf": csrf}).status_code == 403)
        check("wrong-password", client.post("/access/login", base_url=host, headers=origin, data={"password": "wrong", "csrf": csrf}).status_code == 401)
        response = client.post("/access/login", base_url=host, headers=origin, data={"password": password, "csrf": csrf, "next": "//attacker.invalid"})
        check("valid-login-no-open-redirect", response.status_code == 303 and response.headers["Location"] == "/")
        response = client.get("/asset", base_url=host)
        check("streamed-binary-sha", response.status_code == 200 and hashlib.sha256(response.data).digest() == hashlib.sha256(binary).digest())
        check("download-headers", response.headers["Content-Disposition"] == 'attachment; filename="private.glb"' and int(response.headers["Content-Length"]) == len(binary))
        response = client.head("/asset", base_url=host)
        check("head-stream", response.status_code == 200 and not response.data and int(response.headers["Content-Length"]) == len(binary))
        response = client.get("/asset", base_url=host, headers={"Range": "bytes=100-199"})
        check("partial-glb", response.status_code == 206 and response.data == binary[100:200] and response.headers["Content-Range"].startswith("bytes 100-199/"))
        response = client.get("/redirect", base_url=host)
        check("redirect-upstream-hidden", response.headers["Location"] == "/asset")
        check("redirect-body-length", len(response.data) > 0 and int(response.headers["Content-Length"]) == len(response.data))
        response = client.post("/redirect", base_url=host, headers=origin, data="uploaded-image-placeholder")
        check("post-redirect-body-length", response.status_code == 302 and response.headers["Location"] == "/asset" and len(response.data) > 0 and int(response.headers["Content-Length"]) == len(response.data))
        live_gateway = make_server("127.0.0.1", 0, app, threaded=True)
        live_thread = threading.Thread(target=live_gateway.serve_forever, daemon=True)
        live_thread.start()
        try:
            transport = requests.Session()
            transport.trust_env = False
            cookie = client.get_cookie("a100_private", domain="private.example.invalid").value
            headers = {"Host": "private.example.invalid", "Cookie": "a100_private=" + cookie, **origin}
            for method in ("GET", "POST"):
                live = transport.request(method, f"http://127.0.0.1:{live_gateway.server_port}/redirect", headers=headers, data="placeholder" if method == "POST" else None, allow_redirects=False, timeout=5)
                check("actual-http-redirect:" + method, live.status_code == 302 and live.headers["Location"] == "/asset" and len(live.content) > 0 and int(live.headers["Content-Length"]) == len(live.content))
            transport.close()
        finally:
            live_gateway.shutdown()
            live_gateway.server_close()
        check("no-origin-mutation", client.post("/echo", base_url=host, data="change").status_code == 403)
        check("foreign-origin-mutation", client.post("/echo", base_url=host, headers={"Origin": "https://attacker.invalid"}, data="change").status_code == 403)
        response = client.post("/echo", base_url=host, headers={**origin, "Authorization": "sensitive", "X-Forwarded-Host": "attacker.invalid"}, data="preserve-body")
        payload = response.json
        check("authenticated-post-preserved", response.status_code == 200 and payload["body"] == "preserve-body")
        check("credentials-not-forwarded", not payload["auth"] and not payload["cookie"] and not payload["forwarded"] and payload["host"] == f"127.0.0.1:{server.server_port}")
        check("cache-private", response.headers["Cache-Control"] == "private, no-store")
        response = client.get("/access", base_url=host)
        logout_csrf = re.search(r"name=csrf value='([^']+)'", response.text).group(1)
        check("logout-csrf", client.post("/access/logout", base_url=host, headers=origin, data={"csrf": "bad"}).status_code == 403)
        check("logout", client.post("/access/logout", base_url=host, headers=origin, data={"csrf": logout_csrf}).status_code == 303)
        check("logout-assets-protected", client.get("/asset", base_url=host).status_code == 302)
        response = client.get("/access/login", base_url=host)
        csrf = re.search(r'name="csrf" value="([^"]+)"', response.text).group(1)
        for _ in range(8):
            client.post("/access/login", base_url=host, headers=origin, data={"password": "wrong", "csrf": csrf})
        check("brute-force-throttled", client.post("/access/login", base_url=host, headers=origin, data={"password": password, "csrf": csrf}).status_code == 429)
        check("next-path-guards", all(safe_next(x) == "/" for x in ("https://evil.invalid", "//evil.invalid", "/\\evil.invalid", "/access/login", "/\n")))
        check("oversize-upload-rejected", client.post("/access/login", base_url=host, headers=origin, environ_overrides={"CONTENT_LENGTH": str(91 * 1024 * 1024)}, data=b"x").status_code == 413)
finally:
    server.shutdown()
    server.server_close()

report = {"change_id": "CHG-20261011-A100-008", "data_id": "DATA-20261011-A100-008-REDIRECT", "passed": True, "case_count": len(cases), "cases": cases, "binary_sha256": hashlib.sha256(binary).hexdigest(), "gpu_inference": False}
(BASE / "logs" / "private-gateway-redirect-validation.json").write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
