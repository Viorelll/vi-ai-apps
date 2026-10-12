#!/usr/bin/env python3
"""Explicit live Azure smoke test. Uses a synthetic PDF/image/media file and incurs provider cost."""
import argparse
import json
from pathlib import Path
import time
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--base-url', default='http://127.0.0.1:8080')
parser.add_argument('--file', required=True, help='Small synthetic PDF/image/audio/video fixture, never private customer data.')
args = parser.parse_args()
base = args.base_url.rstrip('/')

def call(path, body=None, headers=None):
    if isinstance(body, dict):
        body = json.dumps(body).encode()
        headers = {'Content-Type': 'application/json'}
    with urllib.request.urlopen(urllib.request.Request(base + path, body, headers or {}), timeout=30) as response:
        return json.load(response)

assert call('/api/config')['aiMode'] == 'Foundry', 'Live smoke requires AI_MODE=Foundry; Demo does not prove Azure connectivity.'
case = call('/api/support/cases', {'orderReference': 'LAB-1001'})
file = Path(args.file)
assert file.stat().st_size <= 5 * 1024 * 1024, 'Use a small synthetic fixture to limit cost.'
boundary = uuid.uuid4().hex
body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{file.name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + file.read_bytes() + f'\r\n--{boundary}--\r\n'.encode())
upload = call('/api/documents', body, {'Content-Type': 'multipart/form-data; boundary=' + boundary})
try:
    deadline = time.monotonic() + 180
    while time.monotonic() < deadline:
        document = call('/api/documents/' + upload['documentId'])
        assert document['status'] != 'Failed', document.get('errorCode')
        if document['status'] == 'Ready':
            break
        time.sleep(2)
    else:
        raise AssertionError('Content Understanding did not finish before the live-test deadline.')
    # Attachment endpoint returns 204, so consume it without JSON parsing.
    payload = json.dumps({'documentId': upload['documentId']}).encode()
    with urllib.request.urlopen(urllib.request.Request(base + '/api/support/cases/' + case['id'] + '/attachments', payload, {'Content-Type': 'application/json'})) as response:
        assert response.status == 204
    call('/api/support/cases/' + case['id'] + '/messages', {'text': 'Descrie doar dovezile atașate și starea comenzii; citează sursele, fără a executa acțiuni.', 'requestId': str(uuid.uuid4())})
    deadline = time.monotonic() + 180
    while time.monotonic() < deadline:
        result = call('/api/support/cases/' + case['id'])
        assert all(job['status'] != 'Failed' for job in result['jobs']), [(job['id'], job['errorCode']) for job in result['jobs']]
        answers = [message for message in result['messages'] if message['role'] == 'assistant']
        if answers:
            assert answers[-1]['citations'] and '[Mod demo' not in answers[-1]['text']
            assert answers[-1]['inputTokens'] + answers[-1]['outputTokens'] > 0
            print('Live Foundry response, Content Understanding extraction and citation checks passed. No action confirmed.')
            break
        time.sleep(2)
    else:
        raise AssertionError('Foundry answer did not finish before the live-test deadline.')
finally:
    if not upload['deduplicated']:
        with urllib.request.urlopen(urllib.request.Request(base + '/api/documents/' + upload['documentId'], method='DELETE')):
            pass
