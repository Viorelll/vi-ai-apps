# Codex cloud environment

Each task already has an isolated environment. Use `/workspace/vi-ai-apps`; do not create a Git worktree unless explicitly requested.

The SDK is retained at `/workspace/.dotnet` (10.0.401). Activate it for every shell:

```sh
export DOTNET_ROOT=/workspace/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.cache/nuget
export DOCKER_CONFIG=/workspace/.docker
cd /workspace/vi-ai-apps/02-support-ai
dotnet restore SupportAI.slnx --locked-mode
npm --prefix src/supportai-web ci --cache /workspace/.cache/npm
```

Docker's cloud `vfs` driver is costly for large SDK build stages. `python3 scripts/cloud-build.py` builds small runtime images from native `dotnet publish` outputs and the verified MinIO binary. It writes only ignored `.cache/` outputs. Standard machines use `docker compose build`.

The cloud proxy hostname may need a build-only extra host; the cloud helper resolves it automatically. TLS, Go module checksum verification, and NuGet signatures stay enabled. Do not dump `.env`, `docker compose config`, the process environment, or credential files.

MinIO is built from the official pinned tag, with Go module checksums intact. Initial native preparation (only if the retained binary is absent): obtain Go 1.25 from its verified official image, clone `https://github.com/minio/minio.git` at `RELEASE.2025-10-15T17-29-55Z` outside the project checkout, and run:

```sh
cd /workspace/.setup/minio-source
CGO_ENABLED=0 GOPATH=/workspace/.cache/go GOCACHE=/workspace/.cache/go-build \
  GOPROXY='https://proxy.golang.org|direct' GOMAXPROCS=2 \
  /workspace/.go/bin/go build -p 2 -o /workspace/.setup/minio .
```

The `|direct` fallback uses normal Git authentication and still verifies modules against their checksums; it does not turn off `GOSUMDB`. Keep the source tag and `go.sum` unchanged.

Restart retained services after a new task starts:

```sh
cd /workspace/vi-ai-apps/02-support-ai
python3 scripts/create-env.py --demo  # preserves any existing .env
DOCKER_CONFIG=/workspace/.docker docker compose --env-file .env -f compose.yaml -f compose.dev.yaml up -d --no-build
python3 scripts/readiness.py --base-url http://127.0.0.1:8082
```

The current cloud instance uses `WEB_PORT=8082` to avoid conflicts with DocuAI. Read only the port and AI mode names/status when checking local configuration. `.env` retains random local credentials; Azure remains unconfigured. The UI clearly labels Demo mode. Do not create a public preview link for a loopback address.

Validation: unit tests, frontend build/tests, `python3 scripts/integration.py`, and `python3 scripts/smoke.py --base-url http://127.0.0.1:8082`. Integration tests require the optional loopback ports in `compose.dev.yaml`, create their own PostgreSQL database and S3 bucket, and delete only those test resources.

Publication and filesystem restoration are platform operations. Prepared images/files can be retained; running processes must restart. Saved environment instructions do not publish the environment or prove restoration in a fresh task.
