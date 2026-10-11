"""Provision loopback-only gateway and checksum-verified official tunnel binary.

Does not start an internet tunnel. The service is created but left disabled
until all gateway checks pass.
"""
from __future__ import annotations
import hashlib
import json
import os
import secrets
import stat
import subprocess
import urllib.request
from pathlib import Path
from werkzeug.security import generate_password_hash

BASE = Path(__file__).resolve().parent
ACCESS = BASE / "private-access"
CHANGE = "CHG-20261011-A100-006"
VERSION = "2026.10.0"
DIGEST = "d33ff2d14475178d2012c2c56beba87389ac5ded27649519f198a7d3134a99db"
ACCESS.mkdir(mode=0o700, exist_ok=True)
ACCESS.chmod(0o700)

credentials = ACCESS / "credentials.json"
if not credentials.exists():
    password = secrets.token_urlsafe(24)
    payload = {"secret_key": secrets.token_hex(32), "password_hash": generate_password_hash(password),
               "auth_epoch": secrets.token_hex(32), "local_hosts": ["127.0.0.1:5002", "localhost:5002"],
               "change_id": CHANGE}
    credentials.write_text(json.dumps(payload, indent=2))
    credentials.chmod(0o600)
    (ACCESS / "owner-password.txt").write_text(password + "\n")
    (ACCESS / "owner-password.txt").chmod(0o600)
    print("Access password provisioned in owner-only file; plaintext not printed")

binary = ACCESS / "cloudflared"
if not binary.exists() or hashlib.sha256(binary.read_bytes()).hexdigest() != DIGEST:
    source = f"https://github.com/cloudflare/cloudflared/releases/download/{VERSION}/cloudflared-linux-amd64"
    with urllib.request.urlopen(source, timeout=60) as response:
        data = response.read()
    if hashlib.sha256(data).hexdigest() != DIGEST:
        raise RuntimeError("Official cloudflared release checksum does not match")
    temp = binary.with_suffix(".tmp")
    temp.write_bytes(data)
    temp.chmod(0o700)
    temp.replace(binary)
print(subprocess.check_output([str(binary), "--version"], text=True).strip())

unit_dir = Path.home() / ".config/systemd/user"
unit_dir.mkdir(parents=True, exist_ok=True)
for name in ("hunyuan3d-private-gateway.service", "hunyuan3d-private-tunnel.service"):
    existing = unit_dir / name
    if existing.exists() and not (ACCESS / (name + ".before")).exists():
        (ACCESS / (name + ".before")).write_bytes(existing.read_bytes())

gateway = f"""[Unit]
Description=Password-protected entry point for A100 3D Studio
After=hunyuan3d-webapp.service

[Service]
Type=simple
WorkingDirectory={BASE}
ExecStart={BASE}/.venv/bin/gunicorn --bind 127.0.0.1:5002 --workers 1 --threads 8 --timeout 330 --access-logfile {BASE}/logs/private-gateway-access.log --error-logfile {BASE}/logs/private-gateway-error.log private_gateway:app
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
UMask=0077

[Install]
WantedBy=default.target
"""
tunnel = f"""[Unit]
Description=HTTPS tunnel to password-protected A100 gateway only
After=network-online.target hunyuan3d-private-gateway.service
Wants=network-online.target hunyuan3d-private-gateway.service

[Service]
Type=simple
WorkingDirectory={BASE}
ExecStart={BASE}/.venv/bin/python {BASE}/scripts/run_private_tunnel.py
Restart=on-failure
RestartSec=10
NoNewPrivileges=true
UMask=0077
KillMode=control-group
TimeoutStopSec=20

[Install]
WantedBy=default.target
"""
(unit_dir / "hunyuan3d-private-gateway.service").write_text(gateway)
(unit_dir / "hunyuan3d-private-tunnel.service").write_text(tunnel)
subprocess.run(["systemctl", "--user", "daemon-reload"], check=True)
print("Gateway and tunnel units prepared; tunnel not started")
