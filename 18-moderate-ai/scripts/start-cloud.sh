#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/activate.sh
python3 scripts/create-env.py
python3 scripts/prepare-s3.py
docker compose -f compose.yaml -f compose.cloud.yaml up -d --no-build
docker compose -f compose.yaml -f compose.cloud.yaml restart web
python3 scripts/readiness.py
