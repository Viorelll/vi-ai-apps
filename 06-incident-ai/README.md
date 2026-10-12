# IncidentAI

Incident response copilot built from the `06_IncidentAI.pdf` specification. The first release is a read-only incident workflow: select a service and UTC time window, import JSONL/text logs, redact sensitive values before analysis, correlate errors by trace, record observed releases, review source-linked hypotheses, and export a JSON report. No infrastructure-changing commands are exposed.

## Projects

- `src/IncidentAI.Domain`: incident, event, release, log-source, and diagnosis entities.
- `src/IncidentAI.Application`: DTOs, redaction, offline provider, and diagnosis contracts.
- `src/IncidentAI.Infrastructure`: EF Core/PostgreSQL migrations and S3 object storage.
- `src/IncidentAI.Api`: modular monolith API and persistence owner.
- `src/IncidentAI.Ai`: isolated internal AI gateway; uses Microsoft Foundry Responses API when configured and a deterministic local provider for placeholders/demo.
- `src/incidentai-web`: React + TypeScript incident console.
- `tests/IncidentAI.Tests` and `src/incidentai-web/src/App.test.tsx`: deterministic backend and UI tests.

## Run locally

Requirements: Docker Compose v2, Python 3, Node 22+, and .NET 10 SDK. The cloud setup script installs a pinned .NET SDK after checking its official SHA-512 checksum.

```bash
cp .env.example .env
scripts/install-cloud.sh
scripts/start-cloud.sh
```

Open `http://127.0.0.1:8080`. The demo mode needs no Azure credentials. It uses artificial sample data, local PostgreSQL, and S3-compatible storage. The `minio` Compose service name is retained for API compatibility; its image is SeaweedFS S3 pinned by digest because the supplied PDF documents that MinIO Community is archived. Only the web UI is published to loopback.

Stop without deleting persistent database and object-store data:

```bash
docker compose stop
docker compose start
```

`docker compose down -v` deletes persistent state.

## Foundry

Set `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_API_KEY`, and `CHAT_DEPLOYMENT` in `.env` to enable Responses API. The endpoint must be the Azure OpenAI v1 root, for example `https://RESOURCE.openai.azure.com/openai/v1/`. The API key stays server-side and never enters the browser bundle. If the key or endpoint is a placeholder, the gateway uses the offline provider. This does not validate an Azure resource or deployment; no live Foundry calls are made by the default smoke test.

## API

- `POST /api/incidents` — create an incident with service and a valid interval up to 24 hours.
- `POST /api/incidents/demo` — create the artificial acceptance scenario.
- `POST /api/incidents/{id}/logs?fileName=incident.jsonl` — import up to 5 MiB / 10,000 text events; originals go to S3 and secrets/emails/IPs are redacted before DB/AI use.
- `POST /api/incidents/{id}/releases` — attach a release observation.
- `POST /api/incidents/{id}/diagnose` — run diagnosis and persist a result.
- `GET /api/incidents/{id}` — timeline, releases, sources, and saved diagnoses.
- `GET /api/documents/{id}/content` — stream the private original through the API.
- `GET /api/incidents/{id}/report` — download a JSON report.
- `GET /api/health` — readiness for the local web/API route.

Log import accepts JSONL records with optional `timestampUtc` (or `timestamp`), `severity`, `traceId`, and `message`. Plain text lines are imported as informational events. The database stores redacted excerpts; original log files are private S3 objects.

## Verify

```bash
dotnet tool restore
dotnet restore IncidentAI.slnx --locked-mode -r linux-x64
# Runtime image artifacts are published for Linux x64 by start-cloud.sh.
dotnet test IncidentAI.slnx --no-restore
dotnet format IncidentAI.slnx --verify-no-changes --no-restore
npm --prefix src/incidentai-web ci
npm --prefix src/incidentai-web run format:check
npm --prefix src/incidentai-web test
npm --prefix src/incidentai-web run build
python3 scripts/smoke.py
```

The smoke test requires the Compose stack and verifies health, seeded diagnosis, source IDs, the no-causality check, and JSON report serialization.

## Scope

The included core is local seeded diagnosis, text/JSONL ingestion, S3 originals, PostgreSQL persistence, read-only Foundry diagnosis, and evidence validation. Azure Monitor, PDF/image/audio/video extraction, managed Azure AI Search/pgvector RAG, Content Understanding, Speech/Language adapters, application telemetry export, durable background ingestion jobs, and live Azure resource validation are optional PDF extensions not enabled in this initial runnable workflow. Placeholder cloud settings are examples only.
