#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Start Compose in Foundry mode and provide real credentials securely in .env.
python3 scripts/smoke.py --live
