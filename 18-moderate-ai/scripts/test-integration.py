#!/usr/bin/env python3
import os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1]
values=dict(line.split('=',1) for line in (root/'.env').read_text().splitlines() if line and not line.startswith('#'))
environment=os.environ.copy()
environment['MODERATEAI_TEST_DB']='Host=127.0.0.1;Port=5488;Database=moderateai;Username=moderateai;Password='+values['DB_PASSWORD']
environment['MODERATEAI_TEST_S3_KEY']=values['MINIO_ROOT_USER']
environment['MODERATEAI_TEST_S3_SECRET']=values['MINIO_ROOT_PASSWORD']
subprocess.run(['dotnet','test','ModerateAI.slnx','-c','Release','--no-build','--filter','Category=Integration','--logger','trx;LogFileName=integration.trx','--results-directory','output/integration'],cwd=root,env=environment,check=True)
