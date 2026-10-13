# BuildBudgetAI

A small construction budget workspace for comparing offers against a manually confirmed material plan. .NET owns decimal arithmetic and PostgreSQL; the AI service proposes extraction fields and explains source-backed differences. React renders one working screen. There are no accounts, project permissions, or inferred structural quantities.

## Run locally

Install Docker Engine with Compose and Node 22.12+ / npm 10+ for frontend work. Clone/open this existing repository checkout; do not create a Git worktree for cloud tasks. Copy `.env.example` to `.env`, replace the local dummy database, storage, and internal service keys, then run:

```sh
# Generate the private S3 identity file with the same credentials as .env.
set -a
. ./.env
set +a
cat > .dev/s3.json <<EOF_S3
{"identities":[{"name":"buildbudgetai-local","credentials":[{"accessKey":"${MINIO_ROOT_USER}","secretKey":"${MINIO_ROOT_PASSWORD}"}],"actions":["Admin","Read","List","Tagging","Write"]}]}
EOF_S3
docker compose config --quiet
docker compose up --build -d
docker compose ps
```

Open `http://localhost:8080`. Compose starts a React/Nginx web app, .NET API, AI gateway, PostgreSQL, and SeaweedFS's S3-compatible gateway. The storage container is named `minio` in Compose to keep the endpoint configurable; it implements S3 without the MinIO server image. PostgreSQL and object data use persistent volumes. A one-shot migration runs before the API. Only the web port is exposed, bound to loopback.

For a lightweight local run of the frontend, run `npm ci && npm run dev` in `src/buildbudgetai-web`; Vite forwards `/api` to `http://localhost:5080`. Run the API and AI projects on ports 5080 and 5081 with a local PostgreSQL and an S3 endpoint. The Compose defaults are safer for the full workflow.

## Demo and Azure modes

`AI_MODE=Demo` performs no model calls. It accepts plain-text documents written in the sample's explicit `Key: value` format and labels proposals as unconfirmed. It does not claim Foundry inference. PDF/image extraction and model explanations require `AI_MODE=Foundry`, an Azure OpenAI v1 endpoint and key, a deployment name, and a Content Understanding endpoint, key, and analyzer. Set these values only in the local `.env` or deployment secret store; never commit real keys. See `samples/offer-demo.txt` for the synthetic example. Its budget uses an explicitly entered 100 m² need, 10% waste, a confirmed EUR 5/m² price, and EUR 20 transport: 110 × 5 + 20 = EUR 570.

Upload accepts PDF, PNG, JPEG, and UTF-8 text up to 50 MiB. The application streams to a temporary file, checks magic bytes, hashes and deduplicates the file, stores the original in S3, and creates a durable PostgreSQL ingestion job. Job leases and retries survive API restarts. PDF/image extraction calls Azure Content Understanding; recognized fields are suggestions and require human confirmation. Photos are context only. There is no structural measurement, construction recommendation, or support for confidential public uploads.

The comparison only computes a line total if its price is confirmed and its currency, unit, and tax basis match the plan. Unknown transport stays unknown. Package pricing requires an explicit pieces-per-package value. Excluded tax remains an unknown on a known subtotal; no currency conversion is inferred. Money is serialized as decimal strings. Exports include assumptions.

## Validate

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
cd src/buildbudgetai-web
npm ci
npm run build
npm test
npm audit --audit-level=moderate
```

The default test suite needs no Azure resources. PostgreSQL concurrency, unique hashes, and migrations are exercised with a dedicated real database when `BUILDBUDGET_TEST_DB` is set. The Playwright workflow (`npm run test:e2e`) needs Compose running and Chromium installed; it checks the EUR 570 example, persisted plan, upload, source review, chat behavior, and responsive layout. It does not call or validate a live Azure deployment.

## Azure setup

Create a Microsoft Foundry resource and supported project. Deploy the chat model under the name configured by `CHAT_DEPLOYMENT`. Configure Content Understanding with the selected analyzer. The application uses the Azure OpenAI Responses v1 path and API key header, and Content Understanding's asynchronous `Operation-Location` contract. Check current region, analyzer, file-size, quota, API-version, and service availability requirements before using real customer documents.

`AI_MODE=Foundry` validates that endpoints and keys are no longer placeholders at startup. The AI container has no PostgreSQL connection. Its internal routes require the shared `INTERNAL_API_KEY`; the browser never receives infrastructure credentials.
