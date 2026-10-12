#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .dev/images/api .dev/images/ai .dev/images/web
# The normal Dockerfiles build from source. This alternative avoids network access
# inside a builder when the cloud runner only supplies a host-level HTTPS proxy.
dotnet restore ProcureAI.slnx --locked-mode
dotnet publish src/ProcureAI.Api -c Release --no-restore -o .dev/images/api/app
dotnet publish src/ProcureAI.Ai -c Release --no-restore -o .dev/images/ai/app
npm --prefix src/procureai-web ci --cache "${NPM_CACHE:-/tmp/procureai-npm-cache}" --no-audit --no-fund
npm --prefix src/procureai-web run build
for service in api ai; do
  printf 'FROM mcr.microsoft.com/dotnet/aspnet:10.0\nWORKDIR /app\nCOPY --chown=$APP_UID:$APP_UID app/ ./\nENV ASPNETCORE_HTTP_PORTS=8080\nUSER $APP_UID\nENTRYPOINT ["dotnet", "ProcureAI.%s.dll"]\n' "$(test "$service" = api && echo Api || echo Ai)" > ".dev/images/$service/Dockerfile"
  docker build -t "procureai-$service:latest" ".dev/images/$service"
done
mkdir -p .dev/images/web/html
cp -R src/procureai-web/dist/. .dev/images/web/html/
cp deploy/nginx.conf .dev/images/web/nginx.conf
cat > .dev/images/web/Dockerfile <<'WEB'
FROM nginx:stable-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=nginx:nginx html/ /usr/share/nginx/html/
WEB
docker build -t procureai-web:latest .dev/images/web
