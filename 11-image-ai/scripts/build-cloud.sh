#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOCKER_CONFIG=/workspace/.docker
if [ -n "${HTTPS_PROXY:-}" ]; then
 export IMAGEAI_PROXY_IP="$(getent ahostsv4 proxy | awk 'NR==1 {print $1}')"
 test -n "$IMAGEAI_PROXY_IP"
 for service in api ai web; do
  docker compose -f compose.yaml -f compose.cloud.yaml build --build-arg HTTP_PROXY --build-arg HTTPS_PROXY --build-arg http_proxy --build-arg https_proxy --build-arg NO_PROXY --build-arg no_proxy "$service"
 done
else
 for service in api ai web; do docker compose build "$service"; done
fi
