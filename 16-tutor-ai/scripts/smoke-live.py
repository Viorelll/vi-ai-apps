#!/usr/bin/env python3
import json,os,subprocess,sys,urllib.request
base=os.environ.get('TUTORAI_URL','http://127.0.0.1:8080')
with urllib.request.urlopen(base+'/api/status') as r:status=json.load(r)
if status['demoMode']:sys.exit('Live smoke refuses demo mode. Set AI_DEMO_MODE=false and configure real Azure credentials.')
subprocess.run([sys.executable,'scripts/smoke.py'],check=True)
