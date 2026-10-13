# TutorAI architecture

The browser calls only `/api`. `TutorAI.Api` owns application orchestration and one PostgreSQL database. Its controllers call `TutorAI.Services`; entities, DTOs and deterministic rules are in `TutorAI.Domain`, EF mappings/migrations and atomic leases in `TutorAI.Database`, and S3/HTTP/provider adapters in `TutorAI.Infrastructure`. `TutorAI.Ai` is a separate technical gateway with no database dependency or connection string in Compose.

The `minio` storage role runs a pinned SeaweedFS S3-compatible server because the MinIO source-only release cannot be built with the available upstream artifact access. No storage behavior is mocked.

Five persistent containers: web, api, ai, db, minio. `migrate` is a one-shot task. No external queue, login, billing or global learner profiles. Sessions are anonymous resources and their UUIDs are not authentication: the deployment binds only to loopback and is for local learning, not a public multi-user service.

A request saves the state mutation and job atomically. The worker claims a PostgreSQL lease with `FOR UPDATE SKIP LOCKED`, commits, calls the gateway under a 70 second deadline, then locks and checks the lease before committing the result. Expired leases are reclaimed. Maximum three attempts for 429/5xx/transport failures, honoring Retry-After. A provider response lost before persistence may incur a second model charge. There is no exactly-once claim for Azure.

Single-choice assessment is exact, case-insensitive .NET matching against a server-only answer key. Open answers go to a separately configured judge deployment with the stored rubric. The learner answer is untrusted data. The last two validated scores determine the next level (1–3). One assessment per exercise prevents repeatedly submitting one question to inflate adaptation. Duplicate request IDs return the same attempt; changed payloads conflict.

Source UUIDs and exact quote membership are checked before persistence; URLs come only from backend source metadata and are restricted to HTTPS Microsoft Learn. Seed excerpts are dated snapshots verified during implementation. Uploaded documents are untrusted; attaching a Learn URL records the local provenance declaration and does not certify that the file came from that URL. No source download is triggered by arbitrary user URLs.

Original files are streamed to bounded temporary files, MIME signatures checked, hashed and uploaded to S3. A DB failure compensates S3 writes. Hash uniqueness handles concurrent duplicates. UTF-8 and DOCX are extracted locally; PDF/images use Content Understanding with persisted operation URLs and same-host polling validation. Deletion hides sources immediately, marks dependent exercises stale, and queues object cleanup. Citation excerpts survive deletion.

The core supplies bounded official source snapshots directly and lets Foundry search that scope using a function tool. This is the configurable NoRag/source-context baseline, not a claim of vector or hybrid search. Azure AI Search, pgvector, File Search, agentic retrieval, embeddings, hosted agents, MCP and optional Speech/Language/Translator/image/video labs are not enabled in this delivery; they require separately implemented/configured adapters and Azure resources. They are not simulated by Docker.

Live provider keys remain server-side. ProblemDetails expose safe codes and trace IDs; logs omit request bodies and credentials. Responses API has a five-tool-call/six-round and 15,000-token budget, one JSON repair, and a 60-second deadline. The demo provider is explicit, visibly labeled and records zero model usage; it is not a fallback after a live error.
