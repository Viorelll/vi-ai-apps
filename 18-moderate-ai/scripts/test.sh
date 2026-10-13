#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/activate.sh
dotnet restore ModerateAI.slnx --locked-mode
dotnet build ModerateAI.slnx -c Release --no-restore -m:2
dotnet test ModerateAI.slnx -c Release --no-build --filter 'Category!=Integration' --logger 'trx;LogFileName=unit.trx' --results-directory output/unit
npm --prefix src/moderateai-web run build
npm --prefix src/moderateai-web test
npm --prefix src/moderateai-web run format:check
docker compose -f compose.yaml -f compose.cloud.yaml -f compose.test.yaml up -d --no-build --wait
python3 scripts/readiness.py
python3 scripts/test-integration.py
python3 scripts/smoke.py --restart
PLAYWRIGHT_CHROMIUM_EXECUTABLE="${PLAYWRIGHT_CHROMIUM_EXECUTABLE:-/usr/bin/chromium}" npm --prefix src/moderateai-web run test:e2e
