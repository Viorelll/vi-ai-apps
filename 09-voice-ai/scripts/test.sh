#!/usr/bin/env bash
set -euo pipefail
APP_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget
export PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 npm_config_cache=/workspace/.npm
export XDG_DATA_HOME=/workspace/.local/share
cd "$APP_ROOT"
dotnet restore VoiceAI.sln --locked-mode
dotnet test VoiceAI.sln -c Release
npm ci --prefix src/voiceai-web
npm run build --prefix src/voiceai-web
npm test --prefix src/voiceai-web
npm run format:check --prefix src/voiceai-web
npm audit --audit-level=moderate --prefix src/voiceai-web
dotnet csharpier check src tests
docker compose --env-file .env.example config --quiet
docker compose --env-file .env.example -f compose.yaml -f compose.cloud.yaml config --quiet
