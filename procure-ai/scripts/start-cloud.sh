#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOCKER_CONFIG=/workspace/.cache/docker
python3 scripts/prepare-s3.py
docker compose -f compose.yaml -f compose.s3.yaml up -d --no-build
for attempt in $(seq 1 60); do
  if curl -fsS http://127.0.0.1:8080/api/health >/dev/null; then
    printf 'ProcureAI API, database, object storage and AI gateway are ready.\n'
    exit 0
  fi
  sleep 1
done
printf 'ProcureAI readiness failed; inspect docker compose logs for api, ai and migrate.\n' >&2
exit 1
