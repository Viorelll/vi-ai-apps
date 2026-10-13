#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
export npm_config_cache="${npm_config_cache:-/workspace/.npm}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if ! command -v dotnet >/dev/null 2>&1; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/evalai-dotnet-install.sh
  bash /tmp/evalai-dotnet-install.sh --version 10.0.401 --install-dir /workspace/.dotnet
  export PATH="/workspace/.dotnet:$PATH"
fi
dotnet restore EvalAI.slnx --locked-mode
dotnet tool restore
npm --prefix src/evalai-web ci --ignore-scripts
