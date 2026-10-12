# Validation — 12 octombrie 2026

SupportAI a fost construit și rulat în cloud cu cele cinci servicii Docker și migrarea EF one-shot. Profilul verificat funcțional este **Demo + NoRag**, cu PostgreSQL 17 și MinIO compilat din tagul upstream fixat. Instalarea și pornirea salvate au fost executate și repetate.

| Verificare | Rezultat |
| --- | --- |
| Build soluție .NET 10 | Pass, 0 warnings / 0 errors |
| Teste unitare .NET | 28 passed, 0 failed, 0 skipped |
| Teste integrare PostgreSQL/S3 reale | 9 passed, 0 failed, 0 skipped |
| Build React/TypeScript/Vite | Pass |
| Teste frontend Vitest | 11 passed |
| Test browser Chromium/Playwright | 1 passed, inclusiv viewport mobil și reload |
| Smoke API/S3 | 13 verificări funcționale passed |
| Restart API și gateway | Mesaje, citări, joburi și o singură execuție locală păstrate |
| Dataset demonstrativ | 25 scenarii deterministe executate în testul de evaluare |
| Audit npm | 0 vulnerabilități raportate pentru lockfile-ul final |

Testele verifică semnături de fișiere, deduplicarea SHA-256, roundtrip S3, range requests, normalizare și stocarea artefactelor raw/normalized, claim atomic/reclaim al lease-ului, idempotency concurent, conflicte/expirări, scope de tools, citări invalide, JSON/incomplete Responses, buget de tokeni și propagarea Retry-After. Schimbarea dovezilor și ștergerea sursei invalidează răspunsurile/propunerile vechi. Nginx rezolvă dinamic numele serviciului API pentru a rezista la schimbarea IP-urilor Docker.

Rularea live Microsoft Foundry / Content Understanding / Prompt Shields nu a fost executată: resursele, deploymenturile și cheile au placeholders. Testele providerului folosesc HTTP determinist, iar testul de imagine din integrare folosește un gateway fake explicit. Ele nu demonstrează OCR, inferență sau autentificare Azure reale. `scripts/smoke-foundry.py` este testul separat pentru configurația reală.

Profilurile alternative Azure AI Search/pgvector/File Search/agentic retrieval și laboratoarele MCP/generare media sunt opționale în ghid și nu sunt implementate în acest profil. NoRag furnizează un snapshot limitat; nu este un index vectorial pentru corpusuri mari. Nu există scrieri în CRM/ERP sau rambursări.

Scripturile cloud folosesc SDK-ul nativ verificat și imagini Docker de runtime pentru a evita straturile SDK costisitoare ale driverului cloud `vfs`. Dockerfile-urile standard sunt incluse pentru dezvoltarea obișnuită. Publicarea mediului și restaurarea lui într-un task nou sunt operații separate ale platformei; nu au fost revendicate ca validate.
