# Scope and design

The supplied [development guide](04_ProcureAI_Ghid_Dezvoltare_NET_Foundry.pdf) is the product specification. This application is separate from DocuAI. The backend remains one modular deployment and one database, with projects separating API controllers, application services, domain entities/DTOs, PostgreSQL persistence and infrastructure adapters. All four agent roles execute inside the single AI gateway, which has no database connection.

## Core implemented

- Brief and immutable criteria: currency, quantity, unit, maximum delivery days, fixed 0.7 cost / 0.3 delivery weights.
- Original upload to private S3 storage with bounded spooling, UTF-8 and magic-byte checks, SHA-256 deduplication, database transaction and upload compensation.
- TXT/Markdown/JSON extraction locally, DOCX through bounded ZIP/XML parsing, PDF/images through Content Understanding 2025-11-01. Provider operation URLs are persisted between polling jobs, restricted to the configured HTTPS host/port/path, and redirects are disabled.
- PostgreSQL migrations, run/document relations, offers, durable jobs, four agent checkpoints, recommendations, anonymous conversations/messages and citation snapshots.
- Atomic `UPDATE ... FOR UPDATE SKIP LOCKED` claims, 180-second leases and concurrency checks on completion; processing has a 120-second deadline. Transient failures retry at most twice after the first attempt with backoff. A validated stage survives process loss; a cloud call completed before a crash can still be repeated and billed.
- Distinct needs/suppliers/compare/recommend prompts, deployments and function-tool allowlists in the gateway. Tools only read the immutable, server-injected snapshot. Maximum five tool calls/rounds; incomplete Responses outputs and unapproved tools are rejected. Token usage from successful provider results is retained, and the run budget stops dependent steps. Provider failures before a usable response may not report usage; use Azure spending controls as well.
- Exact decimal scoring, source scope/generation checks, deterministic tie breaking, and rejection of a changed winner, changed criteria, changed scores or invented citation IDs.
- Single React screen with persisted analysis selection, polling, explicit processing, document retry/delete, visible agent roles, comparison, source links, scoped questions and JSON export.
- Deletion immediately hides the source and marks related analyses stale; a durable cleanup job removes the original and CU artifact. A filtered unique hash index permits uploading a new document after deletion, without reviving old recommendations.
- Five permanent containers, plus one-shot migration. Internal keys are checked in constant time and never delivered to the browser. Safe ProblemDetails carry code/traceId; raw documents and provider credentials are not logged.

## Explicit provider modes

`AI_MODE=Demo` is a deterministic fixture processor, not a simulated Foundry service. It accepts structured JSON offers and labels its output as demonstration; it makes zero model calls. Missing offer values remain null and block scoring.

`AI_MODE=Foundry` selects actual Azure OpenAI v1 Responses requests in the gateway. Fill the endpoint/key and deployment names. Content Understanding has its own endpoint/key/analyzer. A placeholder never counts as live provider validation. Real Azure authentication/model/extractor acceptance needs provisioned resources and is not included in the offline test results.

`RAG_PROVIDER=NoRag` is the implemented direct-evidence path with a bounded context. Unsupported provider values fail startup rather than silently substituting another provider. Azure Search, pgvector, File Search, agentic retrieval, MCP/OpenAPI hosting, Speech/Translator/Language, media generation, Entra project agents and fine-tuning are the guide's optional laboratory extensions; adapters for those are not claimed by this delivery. Multi-agent roles and local Responses function calling are implemented without mixing classic SDK contracts.

## Storage and cloud build alternatives

The standard `compose.yaml` retains the guide's pinned MinIO source build. In this cloud instance, its console dependency download redirects to `storage.googleapis.com`, which the active proxy denied; direct fallback also could not access the archived console repository. `compose.s3.yaml` explicitly substitutes a digest-pinned SeaweedFS S3 container under the same storage service. It preserves the five-container layout and authenticates using an ignored credential file generated from `.env`. That alternative is the storage provider exercised by integration and functional tests.

Normal Dockerfiles build from source with frozen NuGet/npm locks. Cloud Docker builders could not resolve the runner's proxy, so `scripts/build-local-images.sh` uses the installed native SDK/npm to publish, then packages those verified artifacts in normal .NET/Nginx runtime images. COPY assigns ownership to the application/NGINX users, including when the runner uses restrictive file permissions. No TLS, NuGet package signature or Go checksum checks are disabled.

## Known boundaries

No ERP, supplier portal, email, purchase orders, user accounts or financial actions. Currency conversion is deliberately absent. Missing evidence or mismatched units require a new analysis with corrected sources. Retrieval and conversations are scoped to a selected run; conversation history is stored, but the current answer is generated from the current question and allowed sources, not a persistent model conversation. Texts are bounded to 200,000 characters and model context to 45,000 JSON characters; large libraries require an optional retrieval provider. Page metadata is retained when available, but the initial CU adapter combines extracted text and does not reconstruct detailed page regions or timestamps. Media upload/generation is not enabled.
