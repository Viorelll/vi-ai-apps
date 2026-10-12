#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_ROOT=/workspace/.tools/dotnet DOTNET_CLI_HOME=/workspace/.cache/dotnet NUGET_PACKAGES=/workspace/.cache/nuget
export PATH="$DOTNET_ROOT:$PATH"
if [[ "${INTEGRATION:-0}" == 1 ]]; then
 mkdir -p .dev
 python3 - <<'CONFIG'
import json
from pathlib import Path
p=Path('.dev/s3-test.json')
p.write_text(json.dumps({'identities':[{'name':'inspectai-tests','credentials':[{'accessKey':'inspectai-test','secretKey':'inspectai-test-password'}],'actions':['Admin','Read','Write','List','Tagging']}]}));p.chmod(0o600)
CONFIG
 export DOCKER_CONFIG=/workspace/.cache/docker
 mkdir -p "$DOCKER_CONFIG"
 docker compose -f compose.test.yaml up -d --wait
 export INSPECTAI_TEST_DB='Host=127.0.0.1;Port=54335;Database=inspectai_tests;Username=inspectai;Password=inspectai-test-password'
 export INSPECTAI_TEST_S3=http://127.0.0.1:59005
fi
dotnet test InspectAI.slnx -c Release
npm --prefix src/inspectai-web test
