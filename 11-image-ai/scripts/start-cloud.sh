#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOCKER_CONFIG=/workspace/.docker
scripts/build-cloud.sh
docker compose up -d --wait --wait-timeout 180
curl -fsS http://127.0.0.1:8080/health/ready
curl -fsS http://127.0.0.1:8080/api/config
