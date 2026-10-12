"""Real-provider acceptance probe; never substitutes deterministic AI."""
import json
import sys
import time
import urllib.request
from pathlib import Path
from uuid import uuid4
base = sys.argv[1].rstrip("/")
def call(path, data=None, content_type="application/json"):
    body = json.dumps(data).encode() if data is not None and content_type == "application/json" else data
    request = urllib.request.Request(base + path, body, {"Content-Type": content_type})
    with urllib.request.urlopen(request, timeout=75) as response:
        return json.load(response)
def wait(path, field):
    for _ in range(180):
        value = call(path)
        state = value[field]
        if state in ("Ready", "NeedsReview", "Succeeded"):
            return value
        if state in ("Failed", "Stale", "Deleting", "Canceled"):
            raise RuntimeError(f"{path}: {state}")
        time.sleep(1)
    raise TimeoutError(path)
key = "live-seed-" + str(uuid4())
versions = []
for label in ("v1", "v2"):
    boundary = "contractai-" + str(uuid4())
    text = Path(f"seeds/contract-{label}.txt").read_bytes() + f"\n\nIdentificator test: {key}".encode()
    body = f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{label}.txt"\r\nContent-Type: text/plain\r\n\r\n'.encode() + text + f'\r\n--{boundary}--\r\n'.encode()
    document = call("/api/documents", body, "multipart/form-data; boundary=" + boundary)
    wait("/api/documents/" + document["documentId"], "status")
    version = call("/api/contracts/versions", {"documentId": document["documentId"], "contractKey": key, "versionLabel": label, "parentVersionId": None})
    job = call(f'/api/contracts/versions/{version["id"]}/analyze', {"revision": version["revision"]})
    wait("/api/jobs/" + job["jobId"], "status")
    versions.append(version)
comparison = call("/api/contracts/compare", {"leftVersionId": versions[0]["id"], "rightVersionId": versions[1]["id"]})
result = wait("/api/contracts/comparisons/" + comparison["comparisonId"], "state")
pairs = [pair for pair in result["result"]["pairs"] if pair["kind"] == "modified"]
assert any("30 zile" in p["beforeText"] and "15 zile" in p["afterText"] for p in pairs)
version = call("/api/contracts/versions/" + versions[1]["id"])
assert version["obligations"] and all(o["dueDate"] is None for o in version["obligations"])
answer = call(f'/api/contracts/versions/{versions[1]["id"]}/questions', {"requestId": str(uuid4()), "question": "Care este termenul de plată?"})
assert answer["citations"] and all(c["versionId"] == versions[1]["id"] for c in answer["citations"])
report = {"key": key, "deployment": answer["deployment"], "comparisonId": comparison["comparisonId"], "citations": len(answer["citations"]), "status": "passed"}
print(json.dumps(report, ensure_ascii=False, indent=2))
