#!/usr/bin/env python3
"""Functional acceptance against the actual API, PostgreSQL, S3 and AI gateway."""
import argparse
import json
import subprocess
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://127.0.0.1:8080')
parser.add_argument('--restart', action='store_true')
args = parser.parse_args()
checks = []


def request(path, method='GET', body=None, status=200, data=None, headers=None):
    headers = headers or {}
    if body is not None:
        data = json.dumps(body).encode()
        headers['Content-Type'] = 'application/json'
    req = urllib.request.Request(args.url + path, data=data, method=method, headers=headers)
    try:
        response = urllib.request.urlopen(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        raw = response.read()
        assert response.status == status, (path, response.status, raw[:400])
        return json.loads(raw) if raw and response.headers.get('Content-Type', '').startswith('application/') and 'json' in response.headers.get('Content-Type', '') else raw


def wait_for(path, predicate, seconds=240):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        try:
            value = request(path)
            if predicate(value):
                return value
        except (urllib.error.URLError, TimeoutError, AssertionError):
            pass
        time.sleep(.2)
    raise AssertionError('Readiness timeout: ' + path)


def upload(name, payload):
    boundary = 'procureai-' + uuid.uuid4().hex
    data = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: application/json\r\n\r\n'.encode()
            + json.dumps(payload).encode() + f'\r\n--{boundary}--\r\n'.encode())
    # First upload may have been deduplicated from a previous acceptance run.
    req = urllib.request.Request(args.url + '/api/documents', data=data, method='POST', headers={'Content-Type': f'multipart/form-data; boundary={boundary}'})
    with urllib.request.urlopen(req, timeout=15) as response:
        assert response.status in (200, 202)
        result = json.loads(response.read())
    wait_for('/api/documents/' + result['documentId'], lambda d: d['state'] == 'Ready', 60)
    return result['documentId']


def create_run(offers):
    run = request('/api/procurement/runs', 'POST', {'brief': '10 unități, EUR, maximum 10 zile',
                  'criteria': {'currency': 'EUR', 'quantity': 10, 'unit': 'unități', 'maxDays': 10}}, 201)
    for offer in offers:
        document = upload('offer.json', offer)
        run = request(f"/api/procurement/runs/{run['id']}/documents", 'POST',
                      {'documentId': document, 'role': 'offer', 'revision': run['revision']})
    return run


wait_for('/api/health', lambda h: h['status'] == 'ready', 60)
a = json.loads((ROOT / 'fixtures/offer-a.json').read_text())
b = json.loads((ROOT / 'fixtures/offer-b.json').read_text())
# Distinct artificial sources permit cleanup without affecting earlier manual demo analyses.
a['supplierLabel'] += '-' + uuid.uuid4().hex[:8]
b['supplierLabel'] += '-' + uuid.uuid4().hex[:8]
run = create_run([a, b])
path = '/api/procurement/runs/' + run['id']
request(path + '/start', 'POST', {'revision': run['revision']}, 202)
if args.restart:
    wait_for(path, lambda r: len(r['steps']) >= 1)
    subprocess.run(['docker', 'compose', '-f', str(ROOT / 'compose.yaml'), '-f', str(ROOT / 'compose.s3.yaml'), 'restart', 'api', 'ai'], cwd=ROOT, check=True)
run = wait_for(path, lambda r: r['state'] in ('Completed', 'Failed', 'NeedsInput'))
assert run['state'] == 'Completed', run
assert len(run['steps']) == 4
assert [s['role'] for s in run['steps']] == ['needs', 'suppliers', 'compare', 'recommend']
assert abs(run['scores'][0]['score'] - .79) < 1e-12
assert run['scores'][0]['cost'] == 1050
assert run['recommendation']['recommendedOfferId'] == run['scores'][0]['id']
checks.append('reference_scores_and_four_checkpoints')
request(path + '/start', 'POST', {'revision': run['revision']}, 409)
checks.append('duplicate_start_rejected')
request(path + '/report')
source = run['sources'][0]
request('/api/documents/' + source['documentId'] + '/content')
checks.append('report_and_source_download')
answer = request('/api/chat', 'POST', {'runId': run['id'], 'question': 'EUR shipping', 'conversationId': None})
assert answer['citations'] and answer['answer']
assert all(c['documentId'] in [d['id'] for d in run['documents']] for c in answer['citations'])
no_evidence = request('/api/chat', 'POST', {'runId': run['id'], 'question': 'xylophone undisclosed', 'conversationId': answer['conversationId']})
assert not no_evidence['citations']
checks.append('chat_scoped_evidence_and_abstention')
request('/api/documents/' + source['documentId'], 'DELETE', status=202)
request('/api/documents/' + source['documentId'] + '/content', status=404)
run = request(path)
assert run['stale']
assert all(s['documentId'] != source['documentId'] for s in run['sources'])
request(path + '/report', status=409)
checks.append('deletion_hides_source_and_marks_report_stale')
for scenario in ['currency', 'shipping']:
    offer = dict(a)
    offer['supplierLabel'] = scenario + '-' + uuid.uuid4().hex[:8]
    if scenario == 'currency':
        offer['currency'] = 'USD'
    else:
        del offer['shipping']
    run = create_run([offer])
    path = '/api/procurement/runs/' + run['id']
    request(path + '/start', 'POST', {'revision': run['revision']}, 202)
    run = wait_for(path, lambda r: r['state'] in ('Completed', 'Failed', 'NeedsInput'))
    assert run['state'] == 'NeedsInput'
    assert run['recommendation'] is None
    checks.append('needs_input_' + scenario)
    for document in run['documents']:
        request('/api/documents/' + document['id'], 'DELETE', status=202)
report = {'status': 'passed', 'checks': checks, 'restart': args.restart, 'provider': 'Demo', 'liveFoundry': 'not_run'}
output = ROOT / 'artifacts'
output.mkdir(exist_ok=True)
(output / 'smoke-results.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
