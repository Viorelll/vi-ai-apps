#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore --locked-mode
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --filter 'Category!=Integration'
dotnet format whitespace --verify-no-changes --no-restore
(cd src/invoiceai-web && npm ci && npm run format:check && npm run build && npm test)
docker compose config --quiet
