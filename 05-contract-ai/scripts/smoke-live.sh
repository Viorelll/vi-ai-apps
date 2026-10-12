#!/usr/bin/env bash
# Opt-in: this calls your configured Azure resources and may incur charges.
set -euo pipefail
cd "$(dirname "$0")/.."
: "${CONTRACTAI_URL:=http://127.0.0.1:8080}"
command -v python3 >/dev/null
python3 scripts/smoke_live.py "$CONTRACTAI_URL"
