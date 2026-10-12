#!/usr/bin/env python3
"""Exercise the real API, PostgreSQL worker and S3; uses no third-party Python dependencies."""
import argparse
import json
import time
import uuid
import urllib.request
import urllib.error
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--base-url", default="http://127.0.0.1:8080")
parser.add_argument("--restart", action="store_true")
args = parser.parse_args()
checks = []

def request(method, path, body=None, headers=None, expected=200):
    headers = dict(headers or {})
    if isinstance(body, dict):
        body = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(args.base_url + path, data=body, headers=headers, method=method)
    try:
        response = urllib.request.urlopen(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        payload = response.read()
        assert response.status == expected, (method, path, response.status, payload[:400])
        result = json.loads(payload) if response.headers.get("Content-Type", "").startswith("application/") and payload else payload
        return result, response.headers

def poll(path, field, desired, timeout=60):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        value, _ = request("GET", path)
        if value[field] == desired:
            return value
        assert value[field] != "Failed", value
        time.sleep(0.3)
    raise AssertionError(f"Timed out: {path}")

def upload():
    video = (ROOT / "seeds/postgresql-course.mp4").read_bytes()
    boundary = "video-" + uuid.uuid4().hex
    data = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"postgresql-course.mp4\"\r\nContent-Type: video/mp4\r\n\r\n".encode() + video + f"\r\n--{boundary}--\r\n".encode())
    req = urllib.request.Request(args.base_url + "/api/documents", data=data, headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req, timeout=30) as response:
        assert response.status in (200, 202)
        return json.loads(response.read())

def search(video_id, question):
    body = {"question": question, "videoIds": [video_id], "requestId": str(uuid.uuid4())}
    run, _ = request("POST", "/api/video/library/search", body, expected=202)
    duplicate, _ = request("POST", "/api/video/library/search", body, expected=202)
    assert run == duplicate
    return poll("/api/video/library/search/" + run["searchRunId"], "state", "Answered")

request("GET", "/api/config")
# Make repeated runs safe by clearing only this exact committed artificial fixture.
for existing in request("GET", "/api/video/library")[0]:
    if existing["title"] == "postgresql-course":
        request("DELETE", "/api/documents/" + existing["documentId"], expected=202)
doc = upload()
duplicate = upload()
assert duplicate["documentId"] == doc["documentId"] and duplicate["duplicate"]
checks.append("upload_hash_deduplication")
v, _ = request("POST", "/api/video/library", {"documentId": doc["documentId"], "title": "postgresql-course"}, expected=201)
id = v["id"]
request("POST", f"/api/video/library/{id}/index", {"revision": v["revision"]}, expected=202)
request("POST", f"/api/video/library/{id}/index", {"revision": v["revision"]}, expected=409)
v = poll(f"/api/video/library/{id}", "state", "Ready")
checks.append("durable_index_and_revision_guard")
run = search(id, "index pentru query")
answer = run["result"]
assert answer["citations"][0]["startMs"] == 70000 and answer["citations"][0]["endMs"] == 95000, answer
assert "CREATE INDEX" in answer["citations"][0]["excerpt"]
checks.append("seed_temporal_rag_70_95_seconds")
content = answer["citations"][0]["contentUrl"]
first, headers = request("GET", content, headers={"Range": "bytes=0-15"}, expected=206)
seed = (ROOT / "seeds/postgresql-course.mp4").read_bytes()
assert first == seed[:16] and headers["Content-Range"] == f"bytes 0-15/{len(seed)}"
last, _ = request("GET", content, headers={"Range": "bytes=-16"}, expected=206)
assert last == seed[-16:]
request("GET", content, headers={"Range": "bytes=999999999-"}, expected=416)
checks.append("http_range_prefix_suffix_and_416")
missing = search(id, "astronomie telescop galaxie")
assert missing["result"]["citations"] == []
checks.append("no_evidence_no_fabricated_citations")
request("POST", "/api/video/library/search", {"question": "index", "videoIds": [str(uuid.uuid4())], "requestId": str(uuid.uuid4())}, expected=409)
checks.append("source_scope_guard")
request("POST", f"/api/video/library/{id}/index", {"revision": v["revision"]}, expected=202)
v = poll(f"/api/video/library/{id}", "state", "Ready")
stale, _ = request("GET", "/api/video/library/search/" + run["id"])
assert stale["state"] == "Stale" and stale["result"] is None
fresh = search(id, "index pentru query")
assert fresh["result"]["citations"][0]["generation"] > answer["citations"][0]["generation"]
checks.append("reindex_atomic_generation_and_stale_citations")
if args.restart:
    subprocess.run(["docker", "compose", "--env-file", ".env.example", "-f", "compose.yaml", "-f", "compose.test.yaml", "restart", "api", "ai"], cwd=ROOT, check=True)
    deadline = time.monotonic() + 60
    while True:
        try:
            restored, _ = request("GET", "/api/video/library/search/" + fresh["id"])
            assert restored["state"] == "Answered" and restored["result"]["citations"][0]["startMs"] == 70000
            break
        except (urllib.error.URLError, ConnectionError):
            assert time.monotonic() < deadline
            time.sleep(0.5)
        except AssertionError as error:
            issue = error.args[0]
            status = issue[2] if isinstance(issue, tuple) and len(issue) > 2 else None
            if status not in (502, 503, 504):
                raise
            assert time.monotonic() < deadline
            time.sleep(0.5)
    checks.append("api_ai_restart_preserves_result_and_sources")
request("DELETE", "/api/documents/" + doc["documentId"], expected=202)
request("GET", content, expected=404)
stale, _ = request("GET", "/api/video/library/search/" + fresh["id"])
assert stale["state"] == "Stale" and stale["result"] is None
checks.append("deletion_hides_content_and_invalidates_answer")
reuploaded = upload()
assert reuploaded["documentId"] != doc["documentId"]
checks.append("deleted_file_can_be_reuploaded")
report = {"passed": checks, "failed": [], "liveFoundry": "not_run_requires_credentials"}
(ROOT / "artifacts").mkdir(exist_ok=True)
(ROOT / "artifacts/smoke.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))
