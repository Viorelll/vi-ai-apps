#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget/packages npm_config_cache=/workspace/.npm DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10.0.401 '; then
 archive="$(mktemp /tmp/imageai-sdk.XXXXXX.tar.gz)"
 trap 'rm -f "$archive"' EXIT
 curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o "$archive"
 echo "51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b  $archive" | sha512sum -c -
 mkdir -p "$DOTNET_ROOT"
 tar -xzf "$archive" -C "$DOTNET_ROOT"
fi
cd "$repo_dir"
dotnet tool restore
dotnet restore ImageAI.slnx --locked-mode
dotnet build ImageAI.slnx -c Release --no-restore
(cd src/imageai-web && npm ci --no-audit --no-fund && npm run build)
