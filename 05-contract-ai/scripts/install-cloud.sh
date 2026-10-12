#!/usr/bin/env bash
set -euo pipefail
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.nuget
export XDG_DATA_HOME=/workspace/.local/share
export npm_config_cache=/workspace/.npm
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"
if ! command -v dotnet >/dev/null || [[ "$(dotnet --version)" != "10.0.401" ]]; then
  mkdir -p "$DOTNET_ROOT"
  task_archive=$(mktemp /tmp/contractai-sdk.XXXXXX.tar.gz)
  trap 'rm -f "$task_archive"' EXIT
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o "$task_archive"
  echo "51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  $task_archive" | sha512sum -c -
  tar -xzf "$task_archive" -C "$DOTNET_ROOT"
fi
cd /workspace/vi-ai-apps/05-contract-ai
dotnet tool restore
dotnet restore ContractAI.slnx --locked-mode
dotnet build ContractAI.slnx -c Release --no-restore
npm --prefix src/contractai-web ci
npm --prefix src/contractai-web run build
