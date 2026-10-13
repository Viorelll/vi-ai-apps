#!/usr/bin/env python3
"""Create an ignored Compose override for Docker builds behind a hosted proxy."""
import json
import os
import socket
from pathlib import Path
from urllib.parse import urlsplit

root = Path(__file__).resolve().parents[1]
config = root / ".dev" / "compose.cloud.json"
config.parent.mkdir(exist_ok=True)
proxy = os.environ.get("HTTPS_PROXY") or os.environ.get("https_proxy") or ""
proxy_host = urlsplit(proxy).hostname if proxy else None
services = {name: {"build": {"secrets": [{"source": "ca", "target": "ca", "required": False}]}} for name in ("api", "ai", "migrate", "web")}
for service in services.values():
    if proxy_host:
        address = socket.gethostbyname(proxy_host)
        service["build"]["extra_hosts"] = [f"{proxy_host}:{address}"]
    service["build"]["args"] = {
        name: os.environ.get(name, "")
        for name in ("HTTPS_PROXY", "HTTP_PROXY", "NO_PROXY")
    }
ca_path = os.environ.get("SSL_CERT_FILE")
if ca_path and Path(ca_path).is_file():
    config.write_text(json.dumps({"services": services, "secrets": {"ca": {"file": ca_path}}}, indent=2) + "\n")
else:
    config.write_text(json.dumps({"services": services}, indent=2) + "\n")
config.chmod(0o600)
