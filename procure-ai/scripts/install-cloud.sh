#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME=/workspace/.cache/dotnet
export NUGET_PACKAGES=/workspace/.cache/nuget
export DOCKER_CONFIG=/workspace/.cache/docker
export NPM_CACHE=/workspace/.cache/npm
export PATH=/workspace/.tools/dotnet:$PATH
mkdir -p /workspace/.cache /workspace/.tools/dotnet
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | rg -q '^10\.'; then
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o /workspace/.cache/procureai-dotnet-releases.json
  python3 - <<'PYTHON'
import hashlib
import json
import subprocess
from pathlib import Path
releases = json.loads(Path('/workspace/.cache/procureai-dotnet-releases.json').read_text())
sdk = next(s for r in releases['releases'] for s in r.get('sdks', [r['sdk']]) if s['version'] == '10.0.401')
file = next(f for f in sdk['files'] if f['rid'] == 'linux-x64' and f['name'].endswith('.tar.gz'))
archive = '/workspace/.cache/procureai-dotnet-sdk.tar.gz'
subprocess.run(['curl', '-fsSL', file['url'], '-o', archive], check=True)
with open(archive, 'rb') as stream:
    actual = hashlib.file_digest(stream, 'sha512').hexdigest()
if actual.lower() != file['hash'].lower():
    raise SystemExit('Official SDK checksum verification failed')
subprocess.run(['tar', '-xzf', archive, '-C', '/workspace/.tools/dotnet'], check=True)
PYTHON
fi
if [ ! -f .env ]; then
  cp .env.example .env
  chmod 600 .env
fi
python3 scripts/prepare-s3.py
scripts/build-local-images.sh
docker compose -f compose.yaml -f compose.s3.yaml config --quiet
