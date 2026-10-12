#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOCKER_CONFIG=/workspace/.cache/docker
mkdir -p "$DOCKER_CONFIG"
if [ ! -f .env ]; then cp .env.example .env; chmod 600 .env; fi
export PATH=/workspace/.tools/dotnet:$PATH
export DOTNET_CLI_HOME=/workspace/.cache/dotnet
export NUGET_PACKAGES=/workspace/.cache/nuget
export npm_config_cache=/workspace/.cache/npm
export DOTNET_CLI_TELEMETRY_OPTOUT=1
python3 scripts/prepare-s3.py
dotnet restore IncidentAI.slnx --locked-mode -r linux-x64
dotnet publish src/IncidentAI.Api/IncidentAI.Api.csproj -c Release --no-restore -r linux-x64 --self-contained true -p:InvariantGlobalization=true -o artifacts/api
dotnet publish src/IncidentAI.Ai/IncidentAI.Ai.csproj -c Release --no-restore -r linux-x64 --self-contained true -p:InvariantGlobalization=true -o artifacts/ai
chmod -R a+rX artifacts
scripts/prepare-native-libs.sh
npm --prefix src/incidentai-web ci
npm --prefix src/incidentai-web run build
chmod -R a+rX src/incidentai-web/dist
docker compose config --quiet
docker compose up -d --build
for attempt in $(seq 1 90); do
  if curl -fsS http://127.0.0.1:8080/api/health >/dev/null 2>&1; then
    python3 scripts/smoke.py
    exit 0
  fi
  sleep 2
done
docker compose logs --tail=100 api ai migrate >&2
exit 1
