"""Keep the currently active Quick Tunnel address in an atomic local record."""
from __future__ import annotations
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

BASE = Path(__file__).resolve().parent.parent
ACCESS = BASE / "private-access"
address = ACCESS / "public-address.json"
address.unlink(missing_ok=True)
command = [str(ACCESS / "cloudflared"), "tunnel", "--no-autoupdate", "--protocol", "http2",
           "--url", "http://127.0.0.1:5002", "--metrics", "127.0.0.1:5004"]
process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
assert process.stdout is not None
for line in process.stdout:
    print(line.rstrip(), flush=True)
    match = re.search(r"https://[a-z0-9-]+\.trycloudflare\.com", line)
    if match:
        payload = {"url": match.group(0), "status": "starting", "temporary_hostname": True,
                   "authentication": "password_required", "upstream": "http://127.0.0.1:5002",
                   "started_at": datetime.now(timezone.utc).isoformat(), "change_id": "CHG-20261011-A100-006"}
        temporary = address.with_suffix(".tmp")
        temporary.write_text(json.dumps(payload, indent=2))
        temporary.chmod(0o600)
        temporary.replace(address)
    if "Registered tunnel connection" in line and address.exists():
        payload = json.loads(address.read_text())
        payload["status"] = "connected"
        temporary = address.with_suffix(".tmp")
        temporary.write_text(json.dumps(payload, indent=2))
        temporary.chmod(0o600)
        temporary.replace(address)
return_code = process.wait()
address.unlink(missing_ok=True)
sys.exit(return_code or 1)
