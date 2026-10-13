#!/usr/bin/env bash
set -euo pipefail
export BUILDX_CONFIG=/workspace/.cache/buildx
mkdir -p "$BUILDX_CONFIG"
cd "$(dirname "$0")/.."
export DOTNET_ROOT=/workspace/.tools/dotnet DOTNET_CLI_HOME=/workspace/.cache/dotnet NUGET_PACKAGES=/workspace/.cache/nuget npm_config_cache=/workspace/.cache/npm DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"
mkdir -p "$DOTNET_ROOT" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$npm_config_cache"
if ! command -v dotnet >/dev/null || [[ "$(dotnet --version)" != "10.0.401" ]]; then
 curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o /workspace/.cache/tutorai-dotnet-releases.json
 python3 - <<'SDK'
import hashlib,json,subprocess
from pathlib import Path
metadata=json.loads(Path('/workspace/.cache/tutorai-dotnet-releases.json').read_text())
sdk=next(s for r in metadata['releases'] for s in r.get('sdks',[r['sdk']]) if s['version']=='10.0.401')
asset=next(f for f in sdk['files'] if f['rid']=='linux-x64' and f['name'].endswith('.tar.gz'))
archive=Path('/workspace/.cache/tutorai-dotnet-sdk.tar.gz')
subprocess.run(['curl','-fsSL',asset['url'],'-o',str(archive)],check=True)
with archive.open('rb') as f:
 if hashlib.file_digest(f,'sha512').hexdigest().lower()!=asset['hash'].lower():raise SystemExit('SDK checksum mismatch')
subprocess.run(['tar','-xzf',str(archive),'-C','/workspace/.tools/dotnet'],check=True)
SDK
fi
node -e 'const [a,b]=process.versions.node.split(".").map(Number);if(a<22||(a===22&&b<12))process.exit(1)'
# NuGet's vulnerability service may be unreachable through the environment proxy.
# Dependencies stay pinned in lock files; skip only the online audit lookup.
dotnet restore TutorAI.slnx --locked-mode -p:NuGetAudit=false
dotnet tool restore
dotnet build TutorAI.slnx -c Release --no-restore -m:2
dotnet publish src/TutorAI.Api/TutorAI.Api.csproj -c Release -r linux-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:InvariantGlobalization=true -o artifacts/publish/api
dotnet publish src/TutorAI.Ai/TutorAI.Ai.csproj -c Release -r linux-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:InvariantGlobalization=true -o artifacts/publish/ai
npm --prefix src/tutorai-web ci --no-audit --no-fund
npm --prefix src/tutorai-web run build
if [[ ! -f .env ]]; then cp .env.example .env; chmod 600 .env; fi
python3 scripts/prepare-s3.py
docker compose -f compose.yaml -f compose.cloud.yaml config --quiet
