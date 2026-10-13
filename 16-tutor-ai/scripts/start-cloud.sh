#!/usr/bin/env bash
set -euo pipefail
export BUILDX_CONFIG=/workspace/.cache/buildx
mkdir -p "$BUILDX_CONFIG"
cd "$(dirname "$0")/.."
# Explicit demo mode is a local validation profile, never an automatic Azure fallback.
export AI_DEMO_MODE=true
export NO_PROXY=localhost,127.0.0.1,db,minio,api,ai
export no_proxy="$NO_PROXY"
python3 scripts/prepare-s3.py
docker compose -f compose.yaml -f compose.cloud.yaml up -d --build
python3 scripts/smoke.py
