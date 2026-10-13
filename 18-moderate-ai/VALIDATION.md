# Validation record

Validated on 2026-10-13 in the cloud development environment. The default
application mode was `Demo`; no Azure credentials or live moderation service
were used.

| Check | Result |
| --- | --- |
| `dotnet build ModerateAI.slnx -c Release --no-restore -m:2` | Passed, 0 warnings and 0 errors |
| `dotnet test ModerateAI.slnx -c Release --no-build --filter 'Category!=Integration'` | Passed, 60 tests |
| `python3 scripts/test-integration.py` with PostgreSQL and S3 started using `compose.test.yaml` | Passed, 9 tests |
| `npm run build` | Passed |
| `npm test` | Passed, 3 tests |
| `npm run format:check` | Passed |
| `PLAYWRIGHT_CHROMIUM_EXECUTABLE=/usr/bin/chromium npm run test:e2e` | Passed, 4 browser tests |
| `dotnet csharpier check src tests` | Passed, 62 files |
| `python3 scripts/smoke.py` against the Docker Demo stack | Passed, 8 functional checks |
| `bash scripts/start-cloud.sh` and `python3 scripts/readiness.py` | Passed; web, API, PostgreSQL, S3 and AI gateway ready |

The integration test command needs the local test port bindings. Start them with
`docker compose -f compose.yaml -f compose.test.yaml up -d --wait`; the normal
cloud startup keeps database, S3 and AI ports internal.
