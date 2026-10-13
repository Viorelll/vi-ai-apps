#!/usr/bin/env python3
"""Create local S3-compatible storage identity configuration without logging credentials."""
import json
import os
from pathlib import Path

root = Path(__file__).resolve().parents[1]
values = {}
if (root / ".env").exists():
    for line in (root / ".env").read_text().splitlines():
        if line and not line.lstrip().startswith("#") and "=" in line:
            name, value = line.split("=", 1)
            values[name] = value.strip().strip('"').strip("'")
values.update(os.environ)
access_key = values.get("MINIO_ROOT_USER")
secret_key = values.get("MINIO_ROOT_PASSWORD")
if not access_key or not secret_key:
    raise SystemExit("MINIO_ROOT_USER and MINIO_ROOT_PASSWORD must be configured in .env")
directory = root / ".dev"
directory.mkdir(exist_ok=True)
config = directory / "s3.json"
config.write_text(
    json.dumps(
        {
            "identities": [
                {
                    "name": "moderateai",
                    "credentials": [{"accessKey": access_key, "secretKey": secret_key}],
                    "actions": ["Admin", "Read", "Write", "List", "Tagging"],
                }
            ]
        }
    )
)
config.chmod(0o600)
print("Local S3 identity configuration is ready.")
