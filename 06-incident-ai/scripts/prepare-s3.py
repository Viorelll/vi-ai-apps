#!/usr/bin/env python3
"""Create local SeaweedFS S3 auth configuration without printing credentials."""
import json
import os
from pathlib import Path
root = Path(__file__).resolve().parent.parent
if (root / '.env').exists():
    for line in (root / '.env').read_text().splitlines():
        if line and not line.lstrip().startswith('#') and '=' in line:
            name, value = line.split('=', 1)
            os.environ.setdefault(name, value.strip().strip('"').strip("'"))
user = os.environ.get('MINIO_ROOT_USER')
password = os.environ.get('MINIO_ROOT_PASSWORD')
if not user or not password:
    raise SystemExit('MINIO_ROOT_USER and MINIO_ROOT_PASSWORD must be set in .env')
config = root / '.dev' / 's3.json'
config.parent.mkdir(parents=True, exist_ok=True)
config.write_text(json.dumps({'identities': [{'name': 'incidentai-local', 'credentials': [{'accessKey': user, 'secretKey': password}], 'actions': ['Admin', 'Read', 'Write', 'List', 'Tagging']}]}))
config.chmod(0o600)
