#!/usr/bin/env bash
set -euo pipefail
# Keep tracked application files and lockfiles unchanged. Use this task's existing checkout.
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.cache/nuget
export DOCKER_CONFIG=/workspace/.docker
export PATH="$DOTNET_ROOT:$PATH"
mkdir -p /workspace/.setup "$DOTNET_ROOT" "$DOCKER_CONFIG"
if [ ! -x "$DOTNET_ROOT/dotnet" ]; then
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o /workspace/.setup/dotnet-sdk.tar.gz
  printf '%s  %s\n' '51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b' '/workspace/.setup/dotnet-sdk.tar.gz' | sha512sum -c -
  tar -xzf /workspace/.setup/dotnet-sdk.tar.gz -C "$DOTNET_ROOT"
fi
test "$(dotnet --version)" = 10.0.401
if [ ! -x /workspace/.setup/minio ]; then
  if [ ! -x /workspace/.go/bin/go ]; then
    task_go_container="supportai-go-setup-$$"
    docker create --name "$task_go_container" golang:1.25-bookworm >/dev/null
    mkdir -p /workspace/.go
    docker cp "$task_go_container":/usr/local/go/. /workspace/.go/
    docker rm "$task_go_container" >/dev/null
  fi
  if [ ! -d /workspace/.setup/minio-source ]; then
    git clone --depth 1 --branch RELEASE.2025-10-15T17-29-55Z https://github.com/minio/minio.git /workspace/.setup/minio-source
  fi
  test "$(git -C /workspace/.setup/minio-source rev-parse HEAD)" = 9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a
  test -z "$(git -C /workspace/.setup/minio-source status --porcelain)"
  (
    cd /workspace/.setup/minio-source
    CGO_ENABLED=0 GOPATH=/workspace/.cache/go GOCACHE=/workspace/.cache/go-build \
      GOPROXY='https://proxy.golang.org|direct' GOMAXPROCS=2 \
      /workspace/.go/bin/go build -p 2 -o /workspace/.setup/minio .
  )
fi
cd /workspace/vi-ai-apps/support-ai
dotnet restore SupportAI.slnx --locked-mode
dotnet build SupportAI.slnx -c Release --no-restore -m:2
npm --prefix src/supportai-web ci --cache /workspace/.cache/npm
npm --prefix src/supportai-web run build
python3 scripts/create-env.py --demo
python3 scripts/cloud-build.py
