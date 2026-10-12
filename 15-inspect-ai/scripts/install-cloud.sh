#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
export DOTNET_ROOT=/workspace/.tools/dotnet
export DOTNET_CLI_HOME=/workspace/.cache/dotnet
export NUGET_PACKAGES=/workspace/.cache/nuget
export npm_config_cache=/workspace/.cache/npm
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"
[[ "$(uname -m)" == "x86_64" ]] || { echo "This profile targets Linux x64." >&2; exit 1; }
mkdir -p "$DOTNET_ROOT" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$npm_config_cache"

if ! command -v dotnet >/dev/null || [[ "$(dotnet --version)" != "10.0.401" ]]; then
  metadata=/workspace/.cache/inspectai-dotnet-releases.json
  archive=/workspace/.cache/inspectai-dotnet-sdk.tar.gz
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o "$metadata"
  python3 - "$metadata" "$archive" "$DOTNET_ROOT" <<'PY'
import hashlib, json, subprocess, sys
from pathlib import Path
metadata, archive, destination = map(Path, sys.argv[1:])
releases = json.loads(metadata.read_text())
sdk = next(s for r in releases['releases'] for s in r.get('sdks', [r['sdk']]) if s['version'] == '10.0.401')
asset = next(f for f in sdk['files'] if f['rid'] == 'linux-x64' and f['name'].endswith('.tar.gz'))
subprocess.run(['curl', '-fsSL', asset['url'], '-o', str(archive)], check=True)
with archive.open('rb') as stream:
    actual = hashlib.file_digest(stream, 'sha512').hexdigest()
if actual.lower() != asset['hash'].lower():
    raise SystemExit('Official .NET SDK SHA-512 verification failed')
subprocess.run(['tar', '-xzf', str(archive), '-C', str(destination)], check=True)
PY
fi

if ! node -e 'const [major,minor]=process.versions.node.split(".").map(Number); process.exit(major > 22 || (major === 22 && minor >= 12) ? 0 : 1)'; then
  echo 'Node.js 22.12 or newer is required.' >&2
  exit 1
fi

case "$(uname -m)" in
  x86_64) DOTNET_RID=linux-x64 ;;

  *) echo 'Only Linux x64 and arm64 are supported.' >&2; exit 1 ;;
esac

dotnet restore InspectAI.slnx --locked-mode
dotnet tool restore
dotnet build InspectAI.slnx -c Release --no-restore -m:2
dotnet test InspectAI.slnx -c Release --no-build
dotnet publish src/InspectAI.Api/InspectAI.Api.csproj -c Release -r "$DOTNET_RID" --self-contained true --no-restore -p:PublishSingleFile=true -p:InvariantGlobalization=true -o artifacts/publish/api
dotnet publish src/InspectAI.Ai/InspectAI.Ai.csproj -c Release -r "$DOTNET_RID" --self-contained true --no-restore -p:PublishSingleFile=true -p:InvariantGlobalization=true -o artifacts/publish/ai
npm --prefix src/inspectai-web ci --cache /workspace/.cache/npm
npm --prefix src/inspectai-web run build
npm --prefix src/inspectai-web test
npm --prefix src/inspectai-web run format:check
if [[ ! -f .env ]]; then cp .env.example .env; chmod 600 .env; fi
python3 scripts/prepare-s3.py
docker compose config --quiet
