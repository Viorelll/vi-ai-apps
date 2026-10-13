#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-/workspace/.dotnet}"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"
mkdir -p "$DOTNET_ROOT" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"
if [[ ! -f "$ROOT/.env" ]]; then cp "$ROOT/.env.example" "$ROOT/.env"; fi
if ! "$DOTNET_ROOT/dotnet" --version >/dev/null 2>&1; then
  archive="$(mktemp /tmp/buildbudget-dotnet.XXXXXX.tar.gz)"
  trap 'rm -f "$archive"' EXIT
  curl --fail --location --proto '=https' --tlsv1.2 \
    https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz \
    --output "$archive"
  printf '%s  %s\n' \
    51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b "$archive" \
    | sha512sum --check
  tar -xzf "$archive" -C "$DOTNET_ROOT"
fi
cd "$ROOT"
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore

COMPOSE_FILES=(-f compose.yaml -f compose.test.yaml)
python3 scripts/configure-cloud-build.py
if [[ -f .dev/compose.cloud.json ]]; then COMPOSE_FILES+=(-f .dev/compose.cloud.json); fi
docker compose "${COMPOSE_FILES[@]}" config --quiet
docker compose "${COMPOSE_FILES[@]}" up -d db minio
source .env
if ! docker compose "${COMPOSE_FILES[@]}" exec -T db psql -U buildbudgetai -tAc \
  'SELECT 1 FROM pg_database WHERE datname=$$buildbudgetai_test$$' | grep -q 1; then
  docker compose "${COMPOSE_FILES[@]}" exec -T db createdb -U buildbudgetai buildbudgetai_test
fi
export BUILDBUDGET_TEST_DB="Host=localhost;Database=buildbudgetai_test;Username=buildbudgetai;Password=${DB_PASSWORD}"
dotnet test --no-build
npm --prefix src/buildbudgetai-web ci --cache /workspace/.npm
npm --prefix src/buildbudgetai-web run build
npm --prefix src/buildbudgetai-web test
npm --prefix src/buildbudgetai-web audit --audit-level=moderate
if [[ -f .dev/compose.cloud.json ]]; then
  docker compose "${COMPOSE_FILES[@]}" build
else
  docker compose "${COMPOSE_FILES[@]}" build
fi
