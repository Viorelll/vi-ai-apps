#!/usr/bin/env python3
"""Check real API readiness through the frontend proxy without calling an AI model."""
import argparse
import json
import time
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--base-url', default='http://127.0.0.1:8080')
args = parser.parse_args()
# Nginx proxies /api only; document list demonstrates API routing and policy ingestion.
deadline = time.monotonic() + 90
while time.monotonic() < deadline:
    try:
        with urllib.request.urlopen(args.base_url.rstrip('/') + '/api/documents', timeout=5) as response:
            documents = json.load(response)
            if any(document['isPolicy'] and document['status'] == 'Ready' for document in documents):
                print('Frontend proxy, API, database, S3 and policy ingestion are ready.')
                break
    except (OSError, ValueError):
        pass
    time.sleep(1)
else:
    raise SystemExit('Readiness failed: inspect compose logs for migrate/api/ai/minio.')
