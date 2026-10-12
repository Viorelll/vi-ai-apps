#!/usr/bin/env python3
"""Restart the local API/gateway and verify retained case state from a prior smoke report."""
import argparse
import json
from pathlib import Path
import subprocess
import time
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--base-url', default='http://127.0.0.1:8080')
parser.add_argument('--report', default='output/smoke.json')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
case_id = json.loads((root / args.report).read_text())['caseId']
url = args.base_url.rstrip('/') + '/api/support/cases/' + case_id

def load():
    with urllib.request.urlopen(url, timeout=10) as response:
        return json.load(response)

def fingerprint(case):
    return {
        'id': case['id'], 'state': case['state'], 'revision': case['revision'],
        'messages': sorted((message['id'], message['text'], message['stale']) for message in case['messages']),
        'citations': sorted((message['id'], source['label'], source['excerpt'], source['documentId']) for message in case['messages'] for source in message['citations']),
        'executions': sorted((execution['id'], execution['resultJson']) for execution in case['executions']),
        'jobs': sorted((job['id'], job['status']) for job in case['jobs']),
    }

before = fingerprint(load())
subprocess.run(['docker', 'compose', '--env-file', '.env', '-f', 'compose.yaml', '-f', 'compose.dev.yaml', 'restart', 'api', 'ai'], cwd=root, check=True)
deadline = time.monotonic() + 60
while time.monotonic() < deadline:
    try:
        after = fingerprint(load())
        assert after == before, 'Persisted case changed across restart.'
        print('PASS API/AI restart retains messages, citations, jobs and the single local execution.')
        break
    except (OSError, ValueError):
        time.sleep(1)
else:
    raise SystemExit('API did not become available after restart.')
