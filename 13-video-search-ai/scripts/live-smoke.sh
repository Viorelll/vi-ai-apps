#!/usr/bin/env bash
# Run explicitly after replacing placeholders and deploying with AI_MODE=Foundry.
set -euo pipefail
cd "$(dirname "$0")/.."
base="${VIDEOSEARCH_BASE_URL:-http://127.0.0.1:8080}"
file="${1:?usage: scripts/live-smoke.sh path/to/test-video.mp4}"
mode=$(curl -fsS "$base/api/config" | python3 -c 'import json,sys; print(json.load(sys.stdin)["mode"])')
[ "$mode" = Foundry ] || { echo 'Live smoke requires AI_MODE=Foundry.' >&2; exit 1; }
result=$(curl -fsS -F "file=@$file" "$base/api/documents")
document_id=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["documentId"])' "$result")
video=$(curl -fsS -H 'Content-Type: application/json' -d "{\"documentId\":\"$document_id\",\"title\":\"Live smoke fixture\"}" "$base/api/video/library")
video_id=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["id"])' "$video")
revision=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["revision"])' "$video")
curl -fsS -H 'Content-Type: application/json' -d "{\"revision\":$revision}" "$base/api/video/library/$video_id/index"
echo "Live extraction queued. Poll /api/video/library/$video_id until Ready; then search using that ID. This may incur Azure charges."
