#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOCKER_CONFIG="${DOCKER_CONFIG:-/workspace/.docker-config}"
export BUILDX_CONFIG="${BUILDX_CONFIG:-$DOCKER_CONFIG/buildx}"
args=(--build-arg HTTP_PROXY --build-arg HTTPS_PROXY --build-arg NO_PROXY)
# Resolve the provided proxy host for Docker's isolated build network; never print credentials.
if [[ -n "${HTTPS_PROXY:-}" ]]; then
  mapping=$(python3 - <<'PYC'
import os,socket,urllib.parse
host=urllib.parse.urlparse(os.environ['HTTPS_PROXY']).hostname
if host: print(host+':'+socket.gethostbyname(host))
PYC
)
  if [[ -n "$mapping" ]]; then args+=(--add-host "$mapping"); fi
fi
# Optional public trust certificate supplied by the host, mounted through BuildKit.
if [[ -n "${SSL_CERT_FILE:-}" && -f "$SSL_CERT_FILE" ]]; then args+=(--secret "id=proxy_ca,src=$SSL_CERT_FILE"); fi
docker build "${args[@]}" -f deploy/Api.Dockerfile -t shopvisionai-api -t shopvisionai-migrate .
docker build "${args[@]}" -f deploy/Ai.Dockerfile -t shopvisionai-ai .
docker build "${args[@]}" -f deploy/Web.Dockerfile -t shopvisionai-web .
docker build -f deploy/Minio.Dockerfile -t shopvisionai-minio .
