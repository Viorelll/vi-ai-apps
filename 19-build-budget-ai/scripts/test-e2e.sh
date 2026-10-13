#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
if [[ ! -f .env ]]; then cp .env.example .env; fi
python3 scripts/configure-cloud-build.py
COMPOSE_FILES=(-f compose.yaml)
if [[ -f .dev/compose.cloud.json ]]; then COMPOSE_FILES+=(-f .dev/compose.cloud.json); fi
docker compose "${COMPOSE_FILES[@]}" up --no-build -d
for attempt in $(seq 1 60); do
  if curl --fail --silent http://127.0.0.1:8080/api/health >/dev/null; then break; fi
  if [[ "$attempt" == 60 ]]; then docker compose "${COMPOSE_FILES[@]}" logs --tail=80 api ai web; exit 1; fi
  sleep 2
done
export CHROMIUM_PATH="${CHROMIUM_PATH:-/usr/bin/chromium}"
npm --prefix src/buildbudgetai-web run test:e2e
