#!/usr/bin/env python3
"""Write an ignored S3 credential file without logging credential values."""
import json
import os
from pathlib import Path

root = Path(__file__).resolve().parent.parent
values = dict(os.environ)
if (root / '.env').exists():
    for line in (root / '.env').read_text().splitlines():
        if line and not line.lstrip().startswith('#') and '=' in line:
            name, value = line.split('=', 1)
            values.setdefault(name, value.strip().strip('"').strip("'"))
user = values.get('MINIO_ROOT_USER')
password = values.get('MINIO_ROOT_PASSWORD')
if not user or not password:
    raise SystemExit('MINIO_ROOT_USER and MINIO_ROOT_PASSWORD are required')
folder = root / '.dev'
folder.mkdir(exist_ok=True)
path = folder / 's3.json'
path.write_text(json.dumps({'identities': [{'name': 'procureai', 'credentials': [
    {'accessKey': user, 'secretKey': password}], 'actions': ['Admin', 'Read', 'Write', 'List', 'Tagging']}]}))
path.chmod(0o600)
