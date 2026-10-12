#!/usr/bin/env bash
set -euo pipefail
# Tasks are already isolated; reuse this checkout. Do not create a worktree unless explicitly requested.
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.cache/nuget
export DOCKER_CONFIG=/workspace/.docker
export PATH="$DOTNET_ROOT:$PATH"
cd /workspace/vi-ai-apps/02-support-ai
python3 scripts/create-env.py --demo
docker compose --env-file .env -f compose.yaml -f compose.dev.yaml up -d --no-build
supportai_web_port="$(python3 - <<'PY'
from pathlib import Path
for line in Path('.env').read_text().splitlines():
    if line.startswith('WEB_PORT='):
        print(int(line.split('=', 1)[1]))
        break
else:
    print(8080)
PY
)"
python3 scripts/readiness.py --base-url "http://127.0.0.1:$supportai_web_port"
