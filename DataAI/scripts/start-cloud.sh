#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
export DOCKER_CONFIG=/workspace/.cache/docker
mkdir -p "$DOCKER_CONFIG"
if [[ ! -x artifacts/publish/api/DataAI.Api || ! -x artifacts/publish/ai/DataAI.Ai || ! -s src/dataai-web/dist/index.html ]]; then
  echo 'Run bash scripts/install-cloud.sh before starting DataAI.' >&2
  exit 1
fi
if [[ ! -f .env ]]; then
  cp .env.example .env
  chmod 600 .env
fi
python3 scripts/prepare-s3.py
docker compose up -d --build --wait
curl --fail --silent --show-error http://127.0.0.1:8080/health
curl --fail --silent --show-error http://127.0.0.1:8080/api/datasets
python3 scripts/smoke.py
printf '\nDataAI services are ready at http://127.0.0.1:8080.\n'
