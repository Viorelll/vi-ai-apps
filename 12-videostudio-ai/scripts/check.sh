#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build --no-restore -c Release
dotnet csharpier check .
dotnet test --no-build -c Release
npm --prefix src/videostudioai-web run format:check
npm --prefix src/videostudioai-web run build
npm --prefix src/videostudioai-web test
