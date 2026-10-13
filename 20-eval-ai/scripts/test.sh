#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
export npm_config_cache="${npm_config_cache:-/workspace/.npm}"
export PATH="/workspace/.dotnet:$PATH"
dotnet restore EvalAI.slnx --locked-mode
dotnet build EvalAI.slnx -c Release --no-restore
dotnet format whitespace EvalAI.slnx --verify-no-changes --no-restore --verbosity quiet
dotnet test EvalAI.slnx -c Release --no-build
npm --prefix src/evalai-web ci --ignore-scripts
npm --prefix src/evalai-web test
npm --prefix src/evalai-web run build
npm --prefix src/evalai-web run format:check
