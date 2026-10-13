#!/usr/bin/env python3
import os,subprocess,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[1]
cache=root/'.cache'
cache.mkdir(exist_ok=True)
bundled_go=Path('/workspace/.go/bin/go')
go_binary=str(bundled_go) if bundled_go.is_file() else shutil.which('go')
if not go_binary:raise SystemExit('Go is required to build the health probe.')
for name in ('Api','Ai'):
 out=cache/name.lower()
 if out.exists():shutil.rmtree(out)
 out.mkdir()
 subprocess.run(['dotnet','publish',f'src/ModerateAI.{name}/ModerateAI.{name}.csproj','-c','Release','-r','linux-x64','--self-contained','true','--no-restore','-p:InvariantGlobalization=false','-o',str(out)],cwd=root,check=True)
 subprocess.run([go_binary,'build','-o',str(out/'healthprobe'),str(root/'deploy/healthprobe.go')],env={**os.environ,'CGO_ENABLED':'0','GOCACHE':str(cache/'go-build')},check=True)
 (out/'Dockerfile').write_text(f'FROM debian:bookworm-slim\nRUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libicu72 libssl3 libstdc++6 zlib1g libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*\nWORKDIR /app\nCOPY --chown=1654:1654 . .\nUSER 1654\nENV ASPNETCORE_HTTP_PORTS=8080\nENTRYPOINT ["/app/ModerateAI.{name}"]\n')
 # Keep probe at the same path used by standard images.
 (out/'Dockerfile').write_text((out/'Dockerfile').read_text().replace('USER 1654','COPY healthprobe /usr/local/bin/healthprobe\nUSER 1654'))
 args=['docker','build','-t',f'moderateai-{name.lower()}']
 import socket,urllib.parse
 for variable in ('HTTP_PROXY','HTTPS_PROXY'):
  if os.environ.get(variable):
   args+=['--build-arg',variable]
   hostname=urllib.parse.urlparse(os.environ[variable]).hostname
   if hostname:args+=['--add-host',hostname+':'+socket.gethostbyname(hostname)]
 subprocess.run(args+[str(out)],check=True)
out=cache/'web';out.mkdir(exist_ok=True)
shutil.copytree(root/'src/moderateai-web/dist',out/'dist',dirs_exist_ok=True);shutil.copy2(root/'deploy/nginx.conf',out/'nginx.conf')
(out/'Dockerfile').write_text('FROM nginx:1.28-alpine\nCOPY --chown=nginx:nginx dist /usr/share/nginx/html\nCOPY nginx.conf /etc/nginx/conf.d/default.conf\n')
subprocess.run(['docker','build','-t','moderateai-web',str(out)],check=True)
