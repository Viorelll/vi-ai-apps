#!/usr/bin/env python3
"""Functional API check against real PostgreSQL/S3 services, with no Azure cost in Demo mode."""
import argparse
import concurrent.futures
import json
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--base-url', default='http://127.0.0.1:8080')
parser.add_argument('--report', default='output/smoke.json')
args = parser.parse_args()
base = args.base_url.rstrip('/')
checks = []


def call(path, body=None, method=None, headers=None, expected=200):
    payload = json.dumps(body).encode() if isinstance(body, dict) else body
    request_headers = {'Content-Type': 'application/json'} if isinstance(body, dict) else {}
    request_headers.update(headers or {})
    request = urllib.request.Request(base + path, payload, request_headers, method=method)
    try:
        response = urllib.request.urlopen(request, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        data = response.read()
        assert response.status == expected, (path, response.status, data.decode(errors='replace'))
        return json.loads(data) if response.headers.get_content_type() == 'application/json' else data


def wait(path, predicate, seconds=60):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = call(path)
        if predicate(value):
            return value
        time.sleep(.25)
    raise AssertionError('Timed out waiting for ' + path)


def passed(name):
    checks.append(name)
    print('PASS ' + name, flush=True)


orders = call('/api/support/orders')
assert any(order['reference'] == 'LAB-1001' and order['status'] == 'Delivered' for order in orders)
passed('seed_order_status')
call('/api/support/orders/UNKNOWN', expected=404)
passed('unknown_order_rejected')
case = call('/api/support/cases', {'orderReference': 'LAB-1001'}, expected=201)
case_path = '/api/support/cases/' + case['id']
content = ('Synthetic support evidence ' + str(uuid.uuid4())).encode()
boundary = 'supportai-' + uuid.uuid4().hex
multipart = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="evidence.txt"\r\nContent-Type: text/plain\r\n\r\n'.encode() + content + f'\r\n--{boundary}--\r\n'.encode())
upload = call('/api/documents', multipart, 'POST', {'Content-Type': 'multipart/form-data; boundary=' + boundary}, 202)
doc_path = '/api/documents/' + upload['documentId']
wait(doc_path, lambda doc: doc['status'] == 'Ready')
passed('real_s3_upload_and_ingestion')
duplicate = call('/api/documents', multipart, 'POST', {'Content-Type': 'multipart/form-data; boundary=' + boundary})
assert duplicate['deduplicated'] and duplicate['documentId'] == upload['documentId']
passed('upload_hash_deduplication')
assert call(doc_path + '/content') == content
passed('original_content_roundtrip')
assert call(doc_path + '/content', headers={'Range': 'bytes=0-9'}, expected=206) == content[:10]
passed('s3_range_download')
call(case_path + '/attachments', {'documentId': upload['documentId']}, expected=204)
request = {'text': 'Ecranul este fisurat. Pregătește un tichet.', 'requestId': str(uuid.uuid4())}
job = call(case_path + '/messages', request, expected=202)
assert call(case_path + '/messages', request, expected=202)['jobId'] == job['jobId']
passed('message_request_id_deduplication')
result = wait(case_path, lambda item: any(message['role'] == 'assistant' for message in item['messages']))
answer = next(message for message in result['messages'] if message['role'] == 'assistant')
assert answer['citations'] and any(source['documentId'] == upload['documentId'] for source in answer['citations'])
for source in answer['citations']:
    assert source['contentUrl'].startswith('/api/documents/')
    call(source['contentUrl'])
passed('grounded_answer_with_working_citations')
proposal = result['proposals'][0]
assert proposal['state'] == 'Draft' and not result['executions']
passed('draft_does_not_execute_automatically')
confirm = {'revision': proposal['revision'], 'payloadHash': proposal['payloadHash'], 'idempotencyKey': str(uuid.uuid4())}
call('/api/support/actions/' + proposal['id'] + '/confirm', {**confirm, 'payloadHash': 'tampered'}, expected=409)
passed('tampered_confirmation_rejected')
with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
    executions = list(pool.map(lambda _: call('/api/support/actions/' + proposal['id'] + '/confirm', confirm), range(5)))
assert len({execution['id'] for execution in executions}) == 1
assert json.loads(executions[0]['resultJson'])['scope'] == 'local-demo'
assert len(call(case_path)['executions']) == 1
passed('concurrent_confirmation_executes_once')
call(doc_path, method='DELETE', expected=202)
assert next(message for message in call(case_path)['messages'] if message['id'] == answer['id'])['stale']
passed('deletion_marks_existing_answer_stale')
deadline = time.monotonic() + 30
while time.monotonic() < deadline:
    documents = call('/api/documents')
    if all(document['id'] != upload['documentId'] for document in documents):
        break
    time.sleep(.25)
call(doc_path, expected=404)
passed('deleted_source_is_hidden')
from pathlib import Path
report = Path(args.report)
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({'passed': len(checks), 'checks': checks, 'caseId': case['id'], 'azureLiveTested': False}, indent=2) + '\n')
print(f'{len(checks)} functional checks passed')
