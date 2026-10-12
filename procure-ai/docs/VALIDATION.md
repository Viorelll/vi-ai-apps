# Validation report

Validated on 12 October 2026 in the cloud workspace. Source code and lockfiles were unchanged between the final passing checks and this report.

| Check | Result |
| --- | --- |
| .NET 10 solution compilation | Passed, no compiler warnings/errors |
| .NET automated tests | 33 passed, 0 failed, 0 skipped |
| PostgreSQL integration | Real migrations, unique hash index, optimistic revision conflict, atomic claim/reclaim, stale-owner rejection, four durable checkpoints, source deletion |
| S3 integration | Real authenticated upload/download, deduplication, invalid-signature rejection, deletion and new upload after deletion |
| React/TypeScript tests | 12 passed; API contracts, revision handling, source attachment, processing guard, safe errors and matrix rendering |
| Frontend production build | Passed |
| .NET / Prettier formatting | Passed |
| Compose configuration | Passed for base configuration and S3 alternative |
| Installation helper repeatability | Passed; frozen restore, native publish and runtime image build |
| Startup helper | Passed; actual `/api/health` checks DB, object storage and AI gateway |
| Functional acceptance with API/AI restart | All 7 scenarios passed |

Functional acceptance validates the guide's reference prices/scores and four stages, duplicate-start rejection, JSON report/source download, scoped chat and abstention, deletion/stale behavior, mixed currencies, and missing shipping. The test restarts the actual API and AI containers and verifies the retained result/checkpoints.

The running default is explicitly **Demo + NoRag + SeaweedFS S3**. The application has five long-running containers and a completed one-shot migration. Foundry REST parsing/tool limits are exercised with deterministic HTTP responses; **real Azure model, authentication and Content Understanding calls were not run with dummy credentials**.

The standard MinIO source build and full networked source builds inside Docker could not complete in this cloud runner because of proxy/DNS and archived dependency access. The tested native artifact build and digest-pinned S3 alternative are included; see [implementation boundaries](IMPLEMENTATION.md). No verification checks were disabled to bypass those failures.

Reusable `install_script` and `start_skill` were saved to the cloud configuration draft. Network additions were saved for `storage.googleapis.com`, `*.data.mcr.microsoft.com` and `learn.microsoft.com`, preserving the selected package-manager preset. Draft persistence is confirmed; environment publication and restoration into a new task remain user/product actions and were not claimed as tested.
