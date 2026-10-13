#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p seeds
# Artificial fixture with a visible index command precisely between 70 and 95 seconds.
ffmpeg -hide_banner -loglevel error -f lavfi -i color=c=0x18223b:s=640x360:r=2:d=180 \
 -vf "drawtext=text='VideoSearchAI - Artificial PostgreSQL course':fontcolor=white:fontsize=22:x=(w-tw)/2:y=70,drawtext=text='CREATE INDEX idx_orders_customer ON orders(customer_id)':fontcolor=0xa99cff:fontsize=17:x=(w-tw)/2:y=180:enable='between(t,70,95)'" \
 -c:v libx264 -pix_fmt yuv420p -movflags +faststart -y seeds/postgresql-course.mp4
sha256sum seeds/postgresql-course.mp4 > seeds/postgresql-course.sha256
seed_hash=$(cut -d ' ' -f1 seeds/postgresql-course.sha256)
python3 - "$seed_hash" compose.yaml <<'PY'
from pathlib import Path
import re
import sys

path = Path(sys.argv[2])
content = path.read_text()
updated, count = re.subn(
    r"(Ai__DemoSeedHash: )[0-9a-f]{64}",
    rf"\g<1>{sys.argv[1]}",
    content,
    count=1,
)
if count != 1:
    raise SystemExit("Could not find exactly one demo seed hash in compose.yaml")
path.write_text(updated)
PY
