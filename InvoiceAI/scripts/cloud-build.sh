#!/usr/bin/env bash
# Build verified host artifacts into images when the cloud proxy blocks MCR layers.
set -euo pipefail
cd "$(dirname "$0")/.."
: "${DOTNET_ROOT:?Set DOTNET_ROOT to the installed .NET 10 SDK directory}"
: "${MINIO_BINARY:?Set MINIO_BINARY to MinIO built from the pinned official source tag}"
: "${GO_BINARY:?Set GO_BINARY to a real Go compiler}"
mkdir -p .local/runtime/host .local/runtime/shared .local/api .local/ai .local/web
cp "$DOTNET_ROOT/dotnet" "$DOTNET_ROOT/LICENSE.txt" "$DOTNET_ROOT/ThirdPartyNotices.txt" .local/runtime/
cp -a "$DOTNET_ROOT/host/fxr" .local/runtime/host/
cp -a "$DOTNET_ROOT/shared/Microsoft.NETCore.App" "$DOTNET_ROOT/shared/Microsoft.AspNetCore.App" .local/runtime/shared/
dotnet restore --locked-mode
dotnet publish src/InvoiceAI.Api -c Release --no-restore -o .local/api
dotnet publish src/InvoiceAI.Ai -c Release --no-restore -o .local/ai
(cd src/invoiceai-web && npm ci && npm run build)
cp -a src/invoiceai-web/dist/. .local/web/
chmod -R a+rX .local/runtime .local/api .local/ai .local/web
cp "$MINIO_BINARY" .local/minio
cat > .local/probe.go <<'EOF'
package main
import ("net/http";"os";"time")
func main(){c:=http.Client{Timeout:3*time.Second};r,e:=c.Get("http://127.0.0.1:9000/minio/health/live");if e!=nil{os.Exit(1)};defer r.Body.Close();if r.StatusCode!=200{os.Exit(1)}}
EOF
CGO_ENABLED=0 "$GO_BINARY" build -o .local/minio-probe .local/probe.go
chmod a+rx .local/minio .local/minio-probe
for project in Api Ai; do
 lower=$(printf '%s' "$project" | tr '[:upper:]' '[:lower:]')
 cat > ".local/$project.Dockerfile" <<EOF
FROM postgres:17.6
WORKDIR /app
COPY runtime/ /usr/share/dotnet/
COPY $lower/ /app/
ENV DOTNET_ROOT=/usr/share/dotnet ASPNETCORE_HTTP_PORTS=8080
USER 10001:10001
ENTRYPOINT ["/usr/share/dotnet/dotnet","InvoiceAI.$project.dll"]
EOF
 docker build -f ".local/$project.Dockerfile" -t "invoiceai-$lower:cloud" .local
 done
cat > .local/Minio.Dockerfile <<'EOF'
FROM postgres:17.6
COPY minio minio-probe /usr/local/bin/
RUN mkdir /data && chown 10001:10001 /data
USER 10001:10001
ENTRYPOINT ["minio"]
CMD ["server","/data","--console-address",":9001"]
EOF
docker build -f .local/Minio.Dockerfile -t invoiceai-minio:cloud .local
# Nginx image comes from the standard registry and verifies layer digests.
cat > .local/Web.Dockerfile <<'EOF'
FROM nginx:1.28.0-alpine
COPY web/ /usr/share/nginx/html/
COPY nginx.conf /etc/nginx/conf.d/default.conf
EOF
cp deploy/nginx.conf .local/nginx.conf
docker build -f .local/Web.Dockerfile -t invoiceai-web:cloud .local
cat > .local/compose.cloud.yaml <<'EOF'
services:
 api:
  image: invoiceai-api:cloud
  healthcheck:
   test: [CMD, /usr/share/dotnet/dotnet, InvoiceAI.Api.dll, --health-check]
 ai:
  image: invoiceai-ai:cloud
  healthcheck:
   test: [CMD, /usr/share/dotnet/dotnet, InvoiceAI.Ai.dll, --health-check]
 migrate:
  image: invoiceai-api:cloud
 minio:
  image: invoiceai-minio:cloud
  healthcheck:
   test: [CMD, minio-probe]
 web:
  image: invoiceai-web:cloud
EOF
