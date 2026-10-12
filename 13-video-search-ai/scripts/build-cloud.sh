#!/usr/bin/env bash
# Optional onboarding helper: standard deployments can use docker compose build.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .local
export DOCKER_CONFIG="${DOCKER_CONFIG:-/workspace/docker-config}"
args=()
if [ -n "${HTTPS_PROXY:-}" ]; then
 proxy_host=$(python3 -c 'import os,urllib.parse; print(urllib.parse.urlparse(os.environ["HTTPS_PROXY"]).hostname)')
 proxy_ip=$(getent ahostsv4 "$proxy_host" | awk 'NR==1 {print $1}')
 args+=(--add-host "$proxy_host:$proxy_ip" --build-arg HTTPS_PROXY --build-arg HTTP_PROXY --build-arg NO_PROXY)
 trust_bundle="${SSL_CERT_FILE:-/etc/ssl/certs/ca-certificates.crt}"
 if [ -f "$trust_bundle" ]; then args+=(--secret "id=egress_ca,src=$trust_bundle"); fi
fi
for service in Api Ai Web Minio; do
 lower=$(echo "$service" | tr '[:upper:]' '[:lower:]')
 image="videosearchai-$lower"
 if [ "$service" = Minio ]; then image="videosearchai-minio:local"; fi
 docker build "${args[@]}" -f "deploy/$service.Dockerfile" -t "$image" .
done
