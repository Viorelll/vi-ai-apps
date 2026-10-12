#!/usr/bin/env bash
set -euo pipefail

APP_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.nuget
export npm_config_cache=/workspace/.npm
export XDG_DATA_HOME=/workspace/.local/share
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"

if ! command -v dotnet >/dev/null || [[ "$(dotnet --version)" != "10.0.401" ]]; then
  mkdir -p "$DOTNET_ROOT"
  archive="$(mktemp /tmp/voiceai-sdk.XXXXXX.tar.gz)"
  trap 'rm -f "$archive"' EXIT
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o "$archive"
  echo "51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  $archive" | sha512sum -c -
  tar -xzf "$archive" -C "$DOTNET_ROOT"
fi

cd "$APP_ROOT"
dotnet tool restore
dotnet restore VoiceAI.sln --locked-mode
dotnet build VoiceAI.sln -c Release --no-restore
npm ci --prefix src/voiceai-web
npm run build --prefix src/voiceai-web
