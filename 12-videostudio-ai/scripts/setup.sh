#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
SDK_DIR=/workspace/.dotnet
SDK_VERSION=10.0.401
SDK_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/${SDK_VERSION}/dotnet-sdk-${SDK_VERSION}-linux-x64.tar.gz"
SDK_SHA512=51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b
if ! command -v dotnet >/dev/null 2>&1; then
  if [[ ! -x "$SDK_DIR/dotnet" ]]; then
    temp_dir=$(mktemp -d)
    trap 'rm -rf "$temp_dir"' EXIT
    curl --fail --location --retry 3 "$SDK_URL" --output "$temp_dir/dotnet-sdk.tar.gz"
    printf '%s  %s\n' "$SDK_SHA512" "$temp_dir/dotnet-sdk.tar.gz" | sha512sum --check --status
    mkdir -p "$SDK_DIR"
    tar -xzf "$temp_dir/dotnet-sdk.tar.gz" -C "$SDK_DIR"
  fi
  export DOTNET_ROOT="$SDK_DIR"
  export PATH="$DOTNET_ROOT:$PATH"
fi
case "$(node --version)" in v22.*|v24.*) ;; *) echo 'Node.js 22.12+ is required.' >&2; exit 1;; esac
dotnet restore --locked-mode
dotnet tool restore
npm --prefix src/videostudioai-web ci --no-audit --no-fund
