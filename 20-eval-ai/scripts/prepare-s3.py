#!/usr/bin/env python3
"""Create ignored local S3 identity config; never print credentials."""
import json, os
from pathlib import Path
root=Path(__file__).resolve().parents[1]
values={}
if (root/".env").exists():
 for line in (root/".env").read_text().splitlines():
  if line and not line.lstrip().startswith("#") and "=" in line:
   k,v=line.split("=",1);values[k]=v.strip().strip("\"").strip("'")
values.update(os.environ)
user=values.get("MINIO_ROOT_USER") or "evalai-storage"
secret=values.get("MINIO_ROOT_PASSWORD") or "replace-local-storage-password"
path=root/".dev"/"s3.json";path.parent.mkdir(exist_ok=True)
path.write_text(json.dumps({"identities":[{"name":"evalai","credentials":[{"accessKey":user,"secretKey":secret}],"actions":["Admin","Read","Write","List","Tagging"]}]}));path.chmod(0o600)
print("Ignored S3 identity configuration is ready.")
