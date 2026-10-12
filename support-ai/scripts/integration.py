#!/usr/bin/env python3
"""Run integration tests using the loopback-only compose.dev.yaml services."""
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
values = {}
for line in (root / '.env').read_text().splitlines():
    if '=' in line and not line.startswith('#'):
        name, value = line.split('=', 1)
        values[name] = value
runtime = os.environ.copy()
runtime.update({
    'TEST_DATABASE_CONNECTION': f"Host=127.0.0.1;Port=55432;Database=postgres;Username=supportai;Password={values['DB_PASSWORD']}",
    'TEST_S3_ENDPOINT': 'http://127.0.0.1:59000',
    'TEST_S3_ACCESS_KEY': values['MINIO_ROOT_USER'],
    'TEST_S3_SECRET_KEY': values['MINIO_ROOT_PASSWORD'],
})
raise SystemExit(subprocess.run(['dotnet', 'test', 'tests/SupportAI.IntegrationTests', '-c', 'Release', '--logger', 'trx;LogFileName=integration.trx'], cwd=root, env=runtime).returncode)
