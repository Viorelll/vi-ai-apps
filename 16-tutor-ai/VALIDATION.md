# Validation

Checked on 2026-10-13 from `feat/tutor-ai` with Docker Compose, PostgreSQL 17.6,
SeaweedFS S3, .NET SDK 10.0.401, Node 24 and system Chromium.

## Passed

- `dotnet test TutorAI.slnx -c Release --no-restore -m:2`: 40 passed.
- `dotnet csharpier check .`: all 84 C# files formatted.
- `npm --prefix src/tutorai-web test`: 4 passed.
- `npm --prefix src/tutorai-web run build`: production bundle built.
- `npm --prefix src/tutorai-web run format:check`: passed.
- `npm --prefix src/tutorai-web run test:e2e`: anonymous session, cited explanation,
  two graded exercises, level adaptation and session reload passed in Chromium.
- `./scripts/start-cloud.sh`: all local services started; HTTP smoke passed for
  explanation, server-only answer key, grading, request idempotency and adaptation
  from level 1 to 2.
- `git diff --check`: passed.

The environment proxy could not reach NuGet's online vulnerability service. The
install script keeps dependency lock files enforced and skips only that network
audit lookup with `NuGetAudit=false`.

## Requires user Azure resources

`smoke-live.py` was not run. The `.env.example` endpoint, key and deployment names
are placeholders, so the Foundry Responses API and Content Understanding live
flows need real resource values before they can be verified. The demo mode is
explicit and visibly marked in the UI; it is not an Azure fallback.

The implementation covers the core tutor workflow, cited source snapshots,
bounded document upload and provider adapters. Optional vector/hybrid retrieval,
hosted agent/MCP, Speech, Language, Translator and media labs described in the
source brief remain out of scope; see `docs/architecture.md`.
