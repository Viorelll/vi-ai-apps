#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose up -d --build --wait --wait-timeout 180
curl --fail --silent --show-error http://127.0.0.1:8080/health/ready
