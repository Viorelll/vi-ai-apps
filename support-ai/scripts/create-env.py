#!/usr/bin/env python3
"""Create ignored local credentials without ever printing or overwriting them."""
import argparse
from pathlib import Path
import secrets

parser = argparse.ArgumentParser()
parser.add_argument('--demo', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
target = root / '.env'
if target.exists():
    print('Existing .env preserved.')
else:
    content = (root / '.env.example').read_text()
    content = content.replace('DB_PASSWORD=replace-with-a-long-local-password', 'DB_PASSWORD=' + secrets.token_hex(24))
    content = content.replace('MINIO_ROOT_PASSWORD=replace-with-a-long-storage-password', 'MINIO_ROOT_PASSWORD=' + secrets.token_hex(24))
    content = content.replace('INTERNAL_API_KEY=replace-with-a-random-internal-secret', 'INTERNAL_API_KEY=' + secrets.token_hex(32))
    if args.demo:
        content = content.replace('AI_MODE=Foundry', 'AI_MODE=Demo')
    with target.open('x') as output:
        output.write(content)
    target.chmod(0o600)
    print('Created ignored .env. Azure fields remain placeholders.')
