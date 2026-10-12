#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget/packages
export PATH="$DOTNET_ROOT:$PATH"
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | rg -q '^10\.0\.401 '; then
  mkdir -p "$DOTNET_ROOT"
  sdk_archive=$(mktemp)
  trap 'rm -f "$sdk_archive"' EXIT
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o "$sdk_archive"
  echo "51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  $sdk_archive" | sha512sum -c -
  tar -xzf "$sdk_archive" -C "$DOTNET_ROOT"
fi
dotnet restore --locked-mode
dotnet build --no-restore
(cd src/shopvisionai-web && npm ci --cache /workspace/.npm-cache && npm run build)
