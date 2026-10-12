#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Run against the demo stack. Live Azure smoke is a separate explicit command.
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}" NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}" npm_config_cache="${npm_config_cache:-/workspace/.npm}"
export PATH="/workspace/.dotnet:$PATH"
dotnet restore ImageAI.slnx --locked-mode
dotnet build ImageAI.slnx -c Release --no-restore
dotnet test ImageAI.slnx -c Release --no-build --filter 'Category!=Integration'
if [ -n "${IMAGEAI_TEST_DB:-}" ]; then dotnet test ImageAI.slnx -c Release --no-build --filter 'Category=Integration'; fi
dotnet format ImageAI.slnx --no-restore --verify-no-changes
(cd src/imageai-web && npm ci --no-audit --no-fund && npm run build && npm test && npm run format:check && npm audit --audit-level=moderate)
python3 scripts/smoke.py --base "${IMAGEAI_API_URL:-http://127.0.0.1:8080}" --report /tmp/imageai-smoke.json
(cd src/imageai-web && npx playwright test)
