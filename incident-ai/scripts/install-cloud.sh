#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME=/workspace/.cache/dotnet
export NUGET_PACKAGES=/workspace/.cache/nuget
export npm_config_cache=/workspace/.cache/npm
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH=/workspace/.tools/dotnet:$PATH
mkdir -p /workspace/.cache /workspace/.tools/dotnet
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q '^10\.'; then
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o /workspace/.cache/incidentai-dotnet-releases.json
  python3 - <<'PY'
import hashlib, json, subprocess
from pathlib import Path
metadata = json.loads(Path('/workspace/.cache/incidentai-dotnet-releases.json').read_text())
sdk = next(s for r in metadata['releases'] for s in r.get('sdks', [r.get('sdk', {})]) if s and s.get('version') == '10.0.401')
artifact = next(f for f in sdk['files'] if f['rid'] == 'linux-x64' and f['name'].endswith('.tar.gz'))
archive = '/workspace/.cache/incidentai-dotnet-sdk.tar.gz'
subprocess.run(['curl', '-fsSL', artifact['url'], '-o', archive], check=True)
with open(archive, 'rb') as stream:
    digest = hashlib.file_digest(stream, 'sha512').hexdigest()
if digest.lower() != artifact['hash'].lower():
    raise SystemExit('Official .NET SDK SHA-512 verification failed')
subprocess.run(['tar', '-xzf', archive, '-C', '/workspace/.tools/dotnet'], check=True)
PY
fi
node -e 'const [major, minor] = process.versions.node.split(".").map(Number); if (major < 22 || (major === 22 && minor < 12)) process.exit(1)' || { printf 'Node.js 22.12 or newer is required.\n' >&2; exit 1; }
if [ ! -f .env ]; then cp .env.example .env; chmod 600 .env; fi
dotnet tool restore
npm --prefix src/incidentai-web ci
dotnet restore IncidentAI.slnx --locked-mode -r linux-x64
