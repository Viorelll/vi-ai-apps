#!/usr/bin/env bash
set -euo pipefail
export BUILDX_CONFIG=/workspace/.cache/buildx
mkdir -p "$BUILDX_CONFIG"
cd "$(dirname "$0")/.."
python3 scripts/prepare-s3.py
docker compose up -d --build
python3 scripts/smoke.py
