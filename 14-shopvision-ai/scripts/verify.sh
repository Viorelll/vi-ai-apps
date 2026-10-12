#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build --logger "trx;LogFileName=shopvision.trx"
dotnet format --verify-no-changes --no-restore
cd src/shopvisionai-web
npm ci --cache /workspace/.npm-cache
npm run build
npm test
npm run format:check
