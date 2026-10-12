#!/usr/bin/env bash
# Reusable setup; no repository source, lockfile or user configuration changes.
set -euo pipefail
umask 022
cd "$(dirname "$0")/.."
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export npm_config_cache=/workspace/.npm DOCKER_CONFIG=/workspace/.docker
export GOPATH=/workspace/.go/packages GOCACHE=/workspace/.go/cache GOBIN=/workspace/.tools GOMAXPROCS=4
export MINIO_BINARY=/workspace/.tools/minio GO_BINARY=/workspace/.go/go/bin/go
export PATH="$DOTNET_ROOT:/workspace/.tools:$PATH"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" /workspace/.go /workspace/.tools
if ! test -x "$DOTNET_ROOT/dotnet"; then
 curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o /tmp/invoiceai-dotnet-releases.json
 python3 - <<'PY'
import json
j=json.load(open('/tmp/invoiceai-dotnet-releases.json'))
f=next(f for r in j['releases'] for s in r.get('sdks',[]) if s['version']=='10.0.401' for f in s['files'] if f['rid']=='linux-x64' and f['name'].endswith('.tar.gz'))
open('/tmp/invoiceai-dotnet-url','w').write(f['url'])
open('/tmp/invoiceai-dotnet.sha512','w').write(f['hash']+'  /tmp/invoiceai-dotnet.tar.gz\n')
PY
 curl -fsSL "$(cat /tmp/invoiceai-dotnet-url)" -o /tmp/invoiceai-dotnet.tar.gz
 sha512sum -c /tmp/invoiceai-dotnet.sha512
 mkdir -p "$DOTNET_ROOT"
 tar -xzf /tmp/invoiceai-dotnet.tar.gz -C "$DOTNET_ROOT"
fi
test "$(dotnet --version)" = 10.0.401
if ! test -x "$MINIO_BINARY"; then
 if ! test -x "$GO_BINARY"; then
  curl -fsSL 'https://go.dev/dl/?mode=json&include=all' -o /tmp/invoiceai-go-releases.json
  python3 - <<'PY'
import json
f=next(f for r in json.load(open('/tmp/invoiceai-go-releases.json')) if r['version']=='go1.25.1' for f in r['files'] if f['filename']=='go1.25.1.linux-amd64.tar.gz')
open('/tmp/invoiceai-go.sha256','w').write(f['sha256']+'  /tmp/invoiceai-go.tar.gz\n')
PY
  curl -fsSL https://go.dev/dl/go1.25.1.linux-amd64.tar.gz -o /tmp/invoiceai-go.tar.gz
  sha256sum -c /tmp/invoiceai-go.sha256
  tar -xzf /tmp/invoiceai-go.tar.gz -C /workspace/.go
 fi
 # Use official proxy/sumdb verification. A private proxy does not change expected checksums.
 "$GO_BINARY" install github.com/minio/minio@RELEASE.2025-10-15T17-29-55Z
fi
"$GO_BINARY" version -m "$MINIO_BINARY" | rg 'v0.0.0-20251015172955-9e49d5e7a648' > /dev/null
if ! test -f /workspace/invoiceai-env.sh; then
cat > /workspace/invoiceai-env.sh <<'PROFILE'
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export npm_config_cache=/workspace/.npm DOCKER_CONFIG=/workspace/.docker
export GOPATH=/workspace/.go/packages GOCACHE=/workspace/.go/cache GOBIN=/workspace/.tools GOMAXPROCS=4
export GO_BINARY=/workspace/.go/go/bin/go MINIO_BINARY=/workspace/.tools/minio
export PATH="/workspace/.dotnet:/workspace/.tools:$PATH"
PROFILE
fi
dotnet tool restore
dotnet restore --locked-mode
dotnet build -c Release --no-restore
(cd src/invoiceai-web && npm ci && npm run build)
