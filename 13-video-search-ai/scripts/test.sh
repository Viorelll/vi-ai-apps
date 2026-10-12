#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet tool restore
dotnet restore --locked-mode
dotnet csharpier check --no-cache src tests
dotnet test --no-restore
npm --prefix src/videosearchai-web ci
npm --prefix src/videosearchai-web run build
npm --prefix src/videosearchai-web test
npm --prefix src/videosearchai-web run format:check
