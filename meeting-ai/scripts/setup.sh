#!/usr/bin/env bash
set -euo pipefail

app_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$app_root"

export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.cache/meetingai-nuget}"
export npm_config_cache="${npm_config_cache:-/workspace/.cache/meetingai-npm}"
mkdir -p "$NUGET_PACKAGES" "$npm_config_cache" .build

dotnet restore MeetingAI.slnx --locked-mode
dotnet test MeetingAI.slnx --no-restore --verbosity minimal
npm ci --prefix src/meetingai-web
npm run build --prefix src/meetingai-web
./scripts/build-minio.sh
dotnet publish src/MeetingAI.Api/MeetingAI.Api.csproj -c Release --no-restore -o .build/api
dotnet publish src/MeetingAI.Ai/MeetingAI.Ai.csproj -c Release --no-restore -o .build/ai
docker compose build api ai web minio
