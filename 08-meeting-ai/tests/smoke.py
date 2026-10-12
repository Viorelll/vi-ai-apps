#!/usr/bin/env python3
"""Run the demo API acceptance flow against a started MeetingAI instance."""
import json
import os
import urllib.error
import urllib.request
import uuid

BASE_URL = os.environ.get("MEETINGAI_URL", "http://127.0.0.1:8080").rstrip("/")


def request(path: str, method: str = "GET", body: dict | None = None):
    payload = None if body is None else json.dumps(body).encode()
    headers = {"Content-Type": "application/json"} if payload is not None else {}
    req = urllib.request.Request(BASE_URL + path, data=payload, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


boundary = "MeetingAI-" + uuid.uuid4().hex
multipart = (
    f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"fake.wav\"\r\n"
    "Content-Type: audio/wav\r\n\r\nnot-wave\r\n"
    f"--{boundary}--\r\n"
).encode()
invalid_audio = urllib.request.Request(
    BASE_URL + "/api/meetings",
    data=multipart,
    method="POST",
    headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
)
try:
    urllib.request.urlopen(invalid_audio)
    raise AssertionError("invalid audio signature should be rejected")
except urllib.error.HTTPError as error:
    assert error.code == 400 and b"invalid_audio_content" in error.read()

status, payload = request("/api/meetings/demo", "POST")
assert status == 201, (status, payload)
meeting = json.loads(payload)
assert len(meeting["segments"]) == 2
assert len(meeting["decisions"]) == len(meeting["actionItems"]) == 1
segment_ids = {segment["id"] for segment in meeting["segments"]}
assert all(set(item["segmentIds"]) <= segment_ids for item in meeting["decisions"] + meeting["actionItems"])

status, payload = request(f"/api/meetings/{meeting['id']}")
assert status == 200 and json.loads(payload)["segments"] == meeting["segments"]

edited = meeting["segments"][0]
status, payload = request(f"/api/meetings/{meeting['id']}/transcript", "PATCH", {
    "revision": meeting["revision"],
    "segments": [{"id": edited["id"], "text": "Corectat local.", "speakerLabel": "Ana"}],
})
assert status == 200, (status, payload)
corrected = json.loads(payload)
assert corrected["segments"][0]["text"] == "Corectat local."
assert corrected["segments"][0]["speakerId"] == edited["speakerId"]
assert corrected["segments"][0]["speakerLabel"] == "Ana"
assert not corrected["decisions"] and not corrected["actionItems"]

status, _ = request(f"/api/meetings/{meeting['id']}/transcript", "PATCH", {
    "revision": meeting["revision"], "segments": []
})
assert status == 409, status
status, payload = request(f"/api/meetings/{meeting['id']}/export")
assert status == 200 and json.loads(payload)["id"] == meeting["id"]
print("MeetingAI demo API acceptance flow passed.")
