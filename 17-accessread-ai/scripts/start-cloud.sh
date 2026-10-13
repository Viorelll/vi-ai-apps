#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH=/workspace/.tools/dotnet:$PATH DOTNET_CLI_HOME=/workspace/.cache/dotnet NUGET_PACKAGES=/workspace/.cache/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1 DOCKER_CONFIG=/workspace/.cache/docker
mkdir -p "$DOCKER_CONFIG"
if [ ! -f .env ]; then cp .env.example .env; chmod 600 .env; fi
python3 scripts/prepare-s3.py
dotnet restore AccessReadAI.slnx --locked-mode -r linux-x64
for service in Api Ai; do
  dotnet publish "src/AccessReadAI.$service/AccessReadAI.$service.csproj" -c Release --no-restore -r linux-x64 --self-contained true -p:InvariantGlobalization=true -o "artifacts/${service,,}"
done
scripts/prepare-native-libs.sh
scripts/prepare-demo-speech.sh
npm --prefix src/accessreadai-web run build
chmod -R a+rX artifacts src/accessreadai-web/dist
docker compose config --quiet
docker compose up -d --build
for attempt in $(seq 1 90); do
 if curl -fsS http://127.0.0.1:8080/api/health >/dev/null 2>&1; then
   python3 scripts/smoke.py
   python3 scripts/integration.py
   exit 0
 fi
 sleep 2
done
docker compose logs --tail=100 api ai migrate >&2
exit 1
