# Validation record

Run from `14-shopvision-ai/` on the cloud environment, 12 October 2026. The PDF itself describes pseudocode/reference fragments and explicitly leaves implementation, .NET builds and tests to this step.

- `dotnet restore --locked-mode`: passed.
- `dotnet build --no-restore`: passed with 0 warnings and 0 errors on .NET SDK 10.0.401.
- PostgreSQL migrations: all five `app` tables created in PostgreSQL 17, including seed catalog and unique/foreign key/concurrency constraints.
- `dotnet test`: 34 passed, 0 failed, 0 skipped. Includes PostgreSQL 17 + EF migrations, MinIO S3 upload/download/delete, deduplication, lease reclaim and concurrent claim, retry exhaustion, concurrency token, stock freshness, image type/size validation, strict vision source/geometry validation, Azure Responses parsing, grounded tool scope and five-round limit.
- `npm ci`: passed from the committed lockfile; `npm audit --audit-level=high`: 0 vulnerabilities.
- `npm run build`: passed with Vite 8.3.4 and strict TypeScript.
- `npm test`: 3 passed, 0 failed, 0 skipped.
- Playwright desktop and mobile E2E: 2 passed. Exercises the image upload through the API and S3, durable analysis, polling, candidate details, sources, grounded Q&A, JSON export and invalid image rejection. Chromium came from the environment.
- `python3 scripts/smoke.py --base http://127.0.0.1:15100`: passed with USB-BLK-01 and USB-BLK-02, source validation, catalog/stock and Q&A.
- Compose initially exposed an image permission problem: ASP.NET configuration JSON files inherited source mode 0600, so the non-root runtime could not read them. The API and AI Dockerfiles now make published, non-secret application files readable by the non-root user. Full Compose startup now succeeds: EF migration exits 0, all five services remain healthy, `/api/health/ready` returns ready, and the Docker-backed smoke test returns USB-BLK-01 and USB-BLK-02 with valid sources.
- Real Azure Foundry vision and grounding are unrun because the endpoint, API key and deployment are placeholders. The explicit Demo mode is fully exercised and visibly labeled. No real Azure service capability is inferred from the demo test. The optional Content Understanding, Search/pgvector, Speech, Language, Translator, media generation, MCP and multi-agent labs are documented as out of the core milestone.

The browser suite was also repeated after updating to current Vite and Vitest; it still passes. Verify formatting with `dotnet format --verify-no-changes --no-restore` and `npm run format:check`.
