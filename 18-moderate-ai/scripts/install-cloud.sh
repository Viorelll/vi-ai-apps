#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/activate.sh
mkdir -p "$DOTNET_ROOT" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$DOCKER_CONFIG" /workspace/.setup
if [[ ! -x "$DOTNET_ROOT/dotnet" ]] || [[ "$(dotnet --version)" != 10.0.401 ]]; then
 curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o /workspace/.cache/dotnet-releases.json
 python3 - <<'INSTALL'
import hashlib,json,subprocess
from pathlib import Path
metadata=json.loads(Path('/workspace/.cache/dotnet-releases.json').read_text())
sdk=next(s for r in metadata['releases'] for s in r.get('sdks',[r['sdk']]) if s['version']=='10.0.401')
asset=next(f for f in sdk['files'] if f['rid']=='linux-x64' and f['name'].endswith('.tar.gz'))
archive=Path('/workspace/.cache/dotnet-sdk.tar.gz')
subprocess.run(['curl','-fsSL',asset['url'],'-o',str(archive)],check=True)
with archive.open('rb') as stream:
 if hashlib.file_digest(stream,'sha512').hexdigest().lower()!=asset['hash'].lower():raise SystemExit('SDK integrity verification failed')
subprocess.run(['tar','-xzf',str(archive),'-C','/workspace/.tools/dotnet'],check=True)
INSTALL
fi
node -e 'const [major,minor]=process.versions.node.split(".").map(Number);process.exit(major>22||(major===22&&minor>=12)?0:1)'
if [[ ! -x /workspace/.go/bin/go ]]; then
 container="moderateai-go-setup-$$"
 docker create --name "$container" golang:1.25-bookworm >/dev/null
 mkdir -p /workspace/.go
 docker cp "$container":/usr/local/go/. /workspace/.go/
 docker rm "$container" >/dev/null
fi
dotnet restore ModerateAI.slnx --locked-mode
dotnet tool restore
dotnet build ModerateAI.slnx -c Release --no-restore -m:2
npm --prefix src/moderateai-web ci
npm --prefix src/moderateai-web run build
python3 scripts/create-env.py
python3 scripts/prepare-s3.py
python3 scripts/cloud-build.py
