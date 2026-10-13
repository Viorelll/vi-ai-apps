#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ ! -f .env ]]; then cp .env.example .env; fi
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
python3 scripts/prepare-s3.py
python3 - <<'PY'
import os,socket
from urllib.parse import urlparse
from pathlib import Path
p=urlparse(os.environ.get('HTTPS_PROXY') or os.environ.get('HTTP_PROXY') or '')
host=p.hostname or 'proxy'
ip=socket.gethostbyname(host)
path=Path('.dev/cloud-proxy.env')
path.write_text(f'EVALAI_PROXY_HOST={host}\nEVALAI_PROXY_IP={ip}\n')
path.chmod(0o600)
PY
while IFS='=' read -r name value; do export "$name=$value"; done < .dev/cloud-proxy.env
compose=(docker compose -f compose.yaml -f compose.cloud.yaml -f compose.dev.yaml)
"${compose[@]}" up -d --build
"${compose[@]}" ps
