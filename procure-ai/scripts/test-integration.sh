#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# TEST_* values are explicitly supplied by the caller; these tests create and drop
# temporary databases and use their own S3 bucket, without touching application data.
: "${TEST_DB_CONNECTION:?set TEST_DB_CONNECTION}"
: "${TEST_S3_ENDPOINT:?set TEST_S3_ENDPOINT}"
: "${TEST_S3_ACCESS_KEY:?set TEST_S3_ACCESS_KEY}"
: "${TEST_S3_SECRET_KEY:?set TEST_S3_SECRET_KEY}"
dotnet test ProcureAI.slnx -c Release
npm --prefix src/procureai-web test
npm --prefix src/procureai-web run build
