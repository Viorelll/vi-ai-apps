#!/usr/bin/env python3
from pathlib import Path
import secrets
root=Path(__file__).resolve().parents[1]
target=root/'.env'
if not target.exists():
 content=(root/'.env.example').read_text()
 for name,placeholder in [('DB_PASSWORD','replace-with-a-long-local-password'),('MINIO_ROOT_PASSWORD','replace-with-a-long-storage-password'),('INTERNAL_API_KEY','replace-with-a-random-internal-secret')]:
  content=content.replace(name+'='+placeholder,name+'='+secrets.token_hex(32))
 with target.open('x') as out: out.write(content)
 target.chmod(0o600)
 print('Created ignored local configuration; Azure values remain placeholders.')
else: print('Existing .env preserved.')
