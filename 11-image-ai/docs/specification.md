# Acoperirea documentului ImageAI

PDF-ul este o specificație cu fragmente de referință și laboratoare AI-103, nu un repository compilabil. Implementarea prioritizează Definition of Done de la paginile 39–44.

| Cerință | Implementare |
| --- | --- |
| Brief + fotografie opțională | React + API; fotografie validată și stocată în S3 |
| Foundry generation / edits | REST Image API; n=1; adaptor separat, request correlation |
| Prompt și vision | Responses opțional, deployment text separat, output limitat, refuz/incomplete respinse |
| Mască PNG alpha | Canvas la rezoluția originală + validare decoder, dimensiuni, selecție și asset ownership |
| Asset B cu parent A | Relații EF, chei S3 immutable, SHA-256; originale păstrate |
| Text exact | Compoziție canvas la export, fără a delega textul modelului |
| Durabilitate | PostgreSQL jobs, claim atomic, revisions și requestId; SQL testat real |
| S3 real | Bucket bootstrap, upload/hash/dedup, compensare la DB failure, content prin API |
| Cinci containere | web/api/ai/db/minio; migrate one-shot; un singur DB |
| Safety / errors | Provider content filters, starea Blocked, ProblemDetails și protejarea cheilor |
| Cost / retry | n=1, limită variante, usage nullable, retry doar pentru respingere 429; apel ambiguu Uncertain |
| Validare | Unit, PostgreSQL integration, S3/API smoke, browser E2E și smoke live opt-in |
| Opționale | RAG/pgvector/Search, document extraction, agent/tool orchestration, MCP, Speech/Language/Translator, video, Azure cloud evaluation și App Insights exporter rămân extensii separate |

Diferențe explicite față de șablonul comun: documentele nucleului sunt numai imagini, validate sincron (200); nu se creează job de extracție fictiv. Documentele referite de runs imutabile nu pot fi șterse. Gateway-ul primește bytes validate prin backend, fără acces la DB și fără URL public MinIO. Lease-urile expirate de media nu sunt retrimise automat. Maximum 12 rezultate/proiect permite comparații succesive, cu un singur apel AI concurent per worker.

Nu există interfață activă pentru laboratoarele neimplementate. Activarea lor necesită o implementare, resurse cloud și teste proprii; simpla adăugare a unei chei `.env` nu le activează. Referințele generice PDF pentru conversații/citări nu sunt necesare pentru studioul de imagini fără RAG.
