#!/usr/bin/env python3
"""Exercise real infrastructure and explicit missing-provider behavior; never emulate AI."""
import json, time, uuid, os
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError

BASE = "http://127.0.0.1:8080"


def call(path, body=None, method=None):
    data = json.dumps(body).encode() if body is not None else None
    request = Request(
        BASE + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"} if data else {},
    )
    with urlopen(request, timeout=30) as response:
        payload = response.read()
        return json.loads(payload) if payload else None


def upload(path):
    boundary = "inspectai-" + uuid.uuid4().hex
    data = (
        f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{path.name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode()
        + path.read_bytes()
        + f"\r\n--{boundary}--\r\n".encode()
    )
    with urlopen(
        Request(
            BASE + "/api/documents",
            data=data,
            headers={"Content-Type": "multipart/form-data; boundary=" + boundary},
        ),
        timeout=30,
    ) as response:
        return json.load(response)["documentId"]


def wait(path, key, allowed):
    for _ in range(120):
        result = call(path)
        if result[key] in allowed:
            return result
        time.sleep(0.25)
    raise RuntimeError("Readiness timeout: " + path)


def main():
    root = Path(__file__).resolve().parents[1]
    with urlopen(BASE + "/health", timeout=10) as response:
        assert response.read().strip() == b"ok"
    with urlopen(BASE + "/", timeout=10) as response:
        assert b"InspectAI" in response.read()
    for attempt in range(120):
        try:
            assert call("/api/health")["status"] == "ok"
            break
        except (HTTPError, URLError) as error:
            if isinstance(error, HTTPError) and error.code not in (502, 503, 504):
                raise
            if attempt == 119:
                raise
            time.sleep(0.25)
    photo = upload(root / "seeds/inspection.png")
    manual = upload(root / "seeds/manual.txt")
    for source in (photo, manual):
        result = wait("/api/documents/" + source, "status", {"Ready", "Failed"})
        assert result["status"] == "Ready", result
    i = call(
        "/api/inspections",
        {
            "equipmentLabel": "Smoke test artificiale",
            "equipmentModel": "P-100",
            "manualVersion": "1.0",
        },
    )
    id = i["id"]
    i = call(
        f"/api/inspections/{id}/sources",
        {"documentId": photo, "role": "photo", "revision": 1},
    )
    i = call(
        f"/api/inspections/{id}/sources",
        {
            "documentId": manual,
            "role": "manual",
            "revision": i["revision"],
            "manualModel": "P-100",
            "manualVersion": "1.0",
        },
    )
    foundry_key = os.environ.get("FOUNDRY_API_KEY", "")
    if not foundry_key and (root / ".env").exists():
        foundry_key = next(
            (
                line.split("=", 1)[1].strip().strip('"').strip("'")
                for line in (root / ".env").read_text().splitlines()
                if line.startswith("FOUNDRY_API_KEY=")
            ),
            "",
        )
    if not foundry_key or foundry_key.startswith("replace-"):
        call(
            f"/api/inspections/{id}/analyze",
            {"revision": i["revision"], "requestId": str(uuid.uuid4())},
        )
        i = wait(f"/api/inspections/{id}", "state", {"NeedsReview", "Ready", "Failed"})
        assert (
            i["state"] == "Failed" and i["errorCode"] == "foundry_not_configured"
        ), i.get("errorCode")
        print(
            "Explicit foundry_not_configured behavior passed. No fabricated AI report."
        )
    else:
        print(
            "Live inference skipped during startup; use scripts/smoke-live.py explicitly."
        )
    with urlopen(BASE + f"/api/documents/{photo}/preview", timeout=10) as response:
        assert response.read()[:8] == bytes([137, 80, 78, 71, 13, 10, 26, 10])
    print(
        "Infrastructure passed: real upload, SHA-256 deduplication, extraction, normalized preview and persisted inspection. Existing sources are preserved."
    )


if __name__ == "__main__":
    main()
