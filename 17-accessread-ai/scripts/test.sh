#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH=/workspace/.tools/dotnet:$PATH DOTNET_CLI_HOME=/workspace/.cache/dotnet NUGET_PACKAGES=/workspace/.cache/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1 XDG_DATA_HOME=/workspace/.cache/xdg
dotnet test AccessReadAI.slnx --no-restore -m:2
npm --prefix src/accessreadai-web run lint
npm --prefix src/accessreadai-web test
npm --prefix src/accessreadai-web run build
dotnet csharpier check .
npm --prefix src/accessreadai-web run format:check
