#!/usr/bin/env python3
import json,time,urllib.request,os
from pathlib import Path
root=Path(__file__).resolve().parents[1]
values=dict(line.split('=',1) for line in (root/'.env').read_text().splitlines() if line and not line.startswith('#'))
base=os.environ.get('MODERATEAI_BASE_URL',f"http://127.0.0.1:{values.get('WEB_PORT','8088')}")
for attempt in range(60):
 try:
  with urllib.request.urlopen(base+'/api/health',timeout=4) as response:data=json.load(response)
  assert data['status']=='ok' and data['mode'] in ('Demo','Foundry')
  with urllib.request.urlopen(base+'/',timeout=4) as response:assert b'ModerateAI' in response.read()
  print('Web, API, PostgreSQL, S3 and AI gateway are ready. Mode: '+data['mode']);break
 except Exception:
  if attempt==59:raise
  time.sleep(1)
