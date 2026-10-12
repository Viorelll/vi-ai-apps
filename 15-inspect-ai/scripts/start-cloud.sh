#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOCKER_CONFIG=/workspace/.cache/docker
mkdir -p "$DOCKER_CONFIG"
if [[ ! -x artifacts/publish/api/InspectAI.Api || ! -x artifacts/publish/ai/InspectAI.Ai || ! -s src/inspectai-web/dist/index.html ]]; then echo 'Run bash scripts/install-cloud.sh first.' >&2; exit 1; fi
if [[ ! -f .env ]]; then cp .env.example .env; chmod 600 .env; fi
python3 scripts/prepare-s3.py
docker compose -f compose.yaml -f compose.cloud.yaml up -d --build --wait
python3 scripts/smoke-placeholder.py
