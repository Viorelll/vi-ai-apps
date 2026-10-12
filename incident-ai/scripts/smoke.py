#!/usr/bin/env python3
"""Exercise seeded diagnosis, S3 upload, privacy and optional restart persistence."""
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request

base = 'http://127.0.0.1:8080'
def request(path, method='GET', body=None, headers=None):
    req = urllib.request.Request(base + path, data=body, method=method, headers=headers or {})
    with urllib.request.urlopen(req, timeout=120) as response:
        return response.status, response.read()

_, payload = request('/api/health')
assert json.loads(payload)['status'] == 'ok', payload
_, payload = request('/api/incidents/demo', 'POST', b'')
incident_id = json.loads(payload)['id']
_, payload = request(f'/api/incidents/{incident_id}/diagnose', 'POST', b'')
result = json.loads(payload)
assert result['state'] == 'completed' and result['hypotheses'], result
_, payload = request(f'/api/incidents/{incident_id}')
incident_data = json.loads(payload)
incident = incident_data['incident']
source_ids = {event['sourceId'] for event in incident['events']}
for hypothesis in result['hypotheses']:
    assert set(hypothesis['evidenceIds']) <= source_ids, hypothesis
assert any('nu dovedește' in check for check in result['nextChecks']), result
_, payload = request(f'/api/incidents/{incident_id}/report')
assert json.loads(payload)['service'] == 'checkout-api', payload

original = b'{"timestampUtc":"2026-10-11T10:02:10Z","severity":"Error","traceId":"T9","message":"api_key=smoke-secret-value; email=smoke@example.com"}\n'
_, payload = request(f'/api/incidents/{incident_id}/logs?fileName=smoke.jsonl', 'POST', original,
                     {'Content-Type': 'application/x-ndjson'})
upload = json.loads(payload)
assert upload['imported'] == 1
_, restored = request(f"/api/documents/{upload['documentId']}/content")
assert restored == original, 'S3 original file mismatch'
_, payload = request(f'/api/incidents/{incident_id}')
incident_data = json.loads(payload)
serialized = payload.decode()
assert 'smoke-secret-value' not in serialized and 'smoke@example.com' not in serialized, 'sensitive data leaked into incident response'
assert '[REDACTED]' in serialized and '[EMAIL]' in serialized, 'sensitive values were not redacted'
assert any(source['id'] == upload['documentId'] for source in incident_data['logSources'])

try:
    request('/api/incidents', 'POST', json.dumps({'service': 'bad', 'fromUtc': '2026-10-12T10:00:00Z', 'toUtc': '2026-10-12T09:00:00Z'}).encode(), {'Content-Type': 'application/json'})
except urllib.error.HTTPError as error:
    assert error.code == 400, error.code
else:
    raise AssertionError('invalid incident time window accepted')

if '--restart' in sys.argv:
    subprocess.run(['docker', 'compose', 'restart', 'api', 'ai'], check=True)
    for _ in range(60):
        try:
            request('/api/health')
            break
        except (OSError, urllib.error.URLError):
            time.sleep(1)
    else:
        raise RuntimeError('API did not recover after restart')
    _, payload = request(f'/api/incidents/{incident_id}')
    restored_incident = json.loads(payload)['incident']
    assert len(restored_incident['events']) == len(incident['events']) + 1
    _, restored = request(f"/api/documents/{upload['documentId']}/content")
    assert restored == original

print(f"Smoke passed: incident {incident_id}, {len(incident['events'])} events, {len(result['hypotheses'])} cited hypotheses, redaction, S3 original retrieval, and HTTP 400 validation.")
if '--restart' in sys.argv:
    print('Restart persistence passed: PostgreSQL event data and S3 content survived API/AI restart.')
