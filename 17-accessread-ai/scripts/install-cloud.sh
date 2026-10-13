#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH=/workspace/.tools/dotnet:$PATH
export DOTNET_CLI_HOME=/workspace/.cache/dotnet NUGET_PACKAGES=/workspace/.cache/nuget npm_config_cache=/workspace/.cache/npm DOTNET_CLI_TELEMETRY_OPTOUT=1
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q '^10.0.401 '; then
  mkdir -p /workspace/.cache /workspace/.tools/dotnet
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o /workspace/.cache/accessread-dotnet.json
  python3 - <<'VERIFY'
import hashlib,json,subprocess
from pathlib import Path
metadata=json.loads(Path('/workspace/.cache/accessread-dotnet.json').read_text())
sdk=next(s for r in metadata['releases'] for s in r.get('sdks',[r.get('sdk',{})]) if s.get('version')=='10.0.401')
artifact=next(f for f in sdk['files'] if f['rid']=='linux-x64' and f['name'].endswith('.tar.gz'))
archive='/workspace/.cache/accessread-sdk.tar.gz'
subprocess.run(['curl','-fsSL',artifact['url'],'-o',archive],check=True)
with open(archive,'rb') as f:
    if hashlib.file_digest(f,'sha512').hexdigest().lower()!=artifact['hash'].lower(): raise SystemExit('SDK checksum failed')
subprocess.run(['tar','-xzf',archive,'-C','/workspace/.tools/dotnet'],check=True)
VERIFY
fi
node -e 'if(Number(process.versions.node.split(".")[0])<22)process.exit(1)'
if [ ! -f .env ]; then cp .env.example .env; chmod 600 .env; fi
dotnet tool restore
dotnet restore AccessReadAI.slnx --locked-mode -r linux-x64
npm --prefix src/accessreadai-web ci
