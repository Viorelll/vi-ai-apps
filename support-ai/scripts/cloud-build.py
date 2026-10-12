#!/usr/bin/env python3
"""Build from verified native outputs in Codex cloud; ordinary development uses Compose Dockerfiles."""
import os
from pathlib import Path
import subprocess

root=Path(__file__).resolve().parents[1]
cache=root/'.cache'
cache.mkdir(exist_ok=True)
runtime=os.environ.copy()
runtime.setdefault('DOCKER_CONFIG','/workspace/.docker')
runtime.setdefault('DOTNET_ROOT','/workspace/.dotnet')
runtime.setdefault('DOTNET_CLI_HOME','/workspace/.dotnet-home')
runtime.setdefault('NUGET_PACKAGES','/workspace/.cache/nuget')
dotnet=Path(runtime['DOTNET_ROOT'])/'dotnet'
for name in ('Api','Ai'):
 output=cache/name.lower()
 subprocess.run([str(dotnet),'publish',f'src/SupportAI.{name}/SupportAI.{name}.csproj','-c','Release','--no-restore','-o',str(output)],cwd=root,env=runtime,check=True)
 dockerfile=output/'Dockerfile'
 dockerfile.write_text(f'FROM mcr.microsoft.com/dotnet/aspnet:10.0.12\nWORKDIR /app\nCOPY --chown=1654:1654 . .\nENV ASPNETCORE_HTTP_PORTS=8080\nUSER $APP_UID\nENTRYPOINT ["dotnet","SupportAI.{name}.dll"]\n')
 subprocess.run(['docker','build','-t','supportai-'+name.lower(),str(output)],env=runtime,check=True)
 if name=='Api':
  subprocess.run(['docker','tag','supportai-api','supportai-migrate'],env=runtime,check=True)
minio=Path('/workspace/.setup/minio')
if not minio.exists():
 raise SystemExit('Build the pinned MinIO source first; see docs/cloud-environment.md. Never bypass Go checksum verification.')
output=cache/'minio'
output.mkdir(exist_ok=True)
import shutil
shutil.copy2(minio,output/'minio')
# Pure-Go health probe and MinIO are built with CGO_ENABLED=0.
subprocess.run(['/workspace/.go/bin/go','build','-o',str(output/'healthprobe'),str(root/'deploy/healthprobe.go')],env={**runtime,'CGO_ENABLED':'0','GOCACHE':'/workspace/.cache/go-build'},check=True)
shutil.copy2(os.environ.get('SSL_CERT_FILE','/etc/ssl/certs/ca-certificates.crt'),output/'ca-certificates.crt')
(output/'Dockerfile').write_text('FROM debian:bookworm-slim\nCOPY --chown=10001:10001 minio healthprobe /usr/local/bin/\nCOPY ca-certificates.crt /etc/ssl/certs/ca-certificates.crt\nRUN mkdir /data && chown 10001:10001 /data\nUSER 10001:10001\nENTRYPOINT ["minio"]\nCMD ["server","/data","--console-address",":9001"]\n')
subprocess.run(['docker','build','-t','supportai-minio',str(output)],env=runtime,check=True)
# Build the actual frontend image through its tested repository Dockerfile.
args=['docker','compose','--env-file','.env','-f','compose.yaml']
proxy=os.environ.get('HTTPS_PROXY')
if proxy:
 import socket,urllib.parse
 host=urllib.parse.urlparse(proxy).hostname
 if host:
  config=cache/'cloud-compose.yaml'
  config.write_text('services:\n  web:\n    build:\n      extra_hosts: ["'+host+':'+socket.gethostbyname(host)+'"]\n')
  args+=['-f',str(config)]
subprocess.run(args+['build','web'],cwd=root,env=runtime,check=True)
