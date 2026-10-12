#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore ContractAI.slnx --locked-mode
dotnet test ContractAI.slnx -c Release --filter 'FullyQualifiedName!~Integration'
npm --prefix src/contractai-web ci
npm --prefix src/contractai-web run build
npm --prefix src/contractai-web test
if [[ "${INTEGRATION:-0}" == 1 ]]; then
  docker compose -p contractai-tests --env-file .env.example -f compose.yaml -f compose.test.yaml up -d --wait db minio
  export CONTRACTAI_TEST_DB='Host=127.0.0.1;Port=54329;Database=contractai_tests;Username=contractai;Password=contractai-test-password'
  export CONTRACTAI_TEST_S3=http://127.0.0.1:59000
  dotnet test ContractAI.slnx -c Release --no-build --filter 'FullyQualifiedName~Integration'
fi
