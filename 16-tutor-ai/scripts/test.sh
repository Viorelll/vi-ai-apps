#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH=/workspace/.tools/dotnet:$PATH DOTNET_CLI_HOME=/workspace/.cache/dotnet NUGET_PACKAGES=/workspace/.cache/nuget
python3 scripts/prepare-s3.py
docker compose -f compose.test.yaml up -d --wait
dotnet test TutorAI.slnx -c Release
npm --prefix src/tutorai-web test
npm --prefix src/tutorai-web run build
