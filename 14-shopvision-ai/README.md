# ShopVisionAI

Asistent vizual pentru un catalog demonstrativ, conform nucleului din `14_ShopVisionAI.pdf`. Nu are conturi, coș, comenzi, plăți sau administrare de produse.

## Pornire

```bash
cp .env.example .env
# Completează configurația Foundry în .env, apoi:
docker compose up --build -d
# Sau, fără resurse Azure, pentru fixture-ul USB artificial:
AI_MODE=Demo docker compose up --build -d
```

Interfața este publicată numai pe `127.0.0.1:8080`. Celelalte servicii nu publică porturi. Sunt cinci containere permanente (`web`, `api`, `ai`, `db`, `minio`) și o migrare one-shot. În medii cloud cu proxy și CA propriu:

```bash
./scripts/install.sh
./scripts/build-cloud.sh
AI_MODE=Demo docker compose up --no-build -d
python3 scripts/smoke.py
```

`Demo` este **explicit determinist**: returnează observațiile fixture-ului adaptor USB negru pentru orice imagine acceptată și este etichetat în UI/JSON. Nu este inferență vizuală și nu se activează automat la eșecul Foundry. Pentru analiza reală setează `AI_MODE=Foundry`, `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_API_KEY` și `CHAT_DEPLOYMENT` pentru un model vision care suportă Responses API și JSON schema strict. Placeholder-urile nu sunt chei funcționale. Cheile rămân în gateway, nu în browser. `.env` este ignorat de Git.

## Proiecte

| Proiect | Responsabilitate |
| --- | --- |
| `ShopVisionAI.Domain` | Entități și reguli deterministe |
| `ShopVisionAI.Contracts` | DTO-uri, geometrie și validarea provenienței |
| `ShopVisionAI.Database` | EF Core / PostgreSQL, migrare, catalog seed |
| `ShopVisionAI.Infrastructure` | S3, autentificare internă, HTTP și Retry-After |
| `ShopVisionAI.Services` | Upload, matching, căutări și worker durabil |
| `ShopVisionAI.Api` | Controllere publice și ProblemDetails |
| `ShopVisionAI.Ai` | Responses vision, generare grounded și tool-uri read-only; fără DB |
| `shopvisionai-web` | React / TypeScript / Vite, un singur ecran |
| `ShopVisionAI.Tests` | Reguli, HTTP Foundry fake, PostgreSQL și S3 reale |

Clasele, DTO-urile, entitățile și serviciile au fișiere proprii. API-ul și gateway-ul sunt două deploy-uri; proiectele backend nu reprezintă microservicii separate. .NET SDK 10.0.401, runtime 10.0.12; Node 22.12+; PostgreSQL 17. Lockfile-urile sunt comise. MinIO este binarul community distribuit de Bitnami, cu digest fixat în Dockerfile. SDK-ul din build se descarcă din sursa Microsoft și este verificat SHA-512.

## Flux și limite

1. Uploadul verifică dimensiunea (50 MiB), extensia și magic bytes pentru PNG/JPEG/WebP; face spool limitat pe disc, SHA-256, deduplicare și stocare S3, cu compensare la eșecul DB.
2. API-ul creează căutarea și apoi jobul atomic cu schimbarea stării/reviziei. `requestId` deduplică analiza.
3. Worker-ul revendică atomic un lease PostgreSQL (`SKIP LOCKED`), fără tranzacție DB în apelurile AI. Lease-ul expirat se recuperează, inclusiv marcarea terminală după a treia încercare. 429/5xx/timeouts au backoff limitat și Retry-After plafonat la 120s.
4. Foundry vision extrage observații vizibile/deduse/necunoscute. Backend-ul validează source IDs, geometria și atributele neobservabile. Gateway-ul impune maximum 20 MiB pentru apelul vision, distinct de limita uploadului.
5. Retrieval EF pe catalogul seed filtrează categoria și calculează un scor separat de explicația AI. Prețurile și stocurile provin din PostgreSQL, sunt copiate în rezultatul analizei și nu sunt inferate din fotografie.
6. Foundry explică numai candidații și apelează `get_catalog_snapshot`, tool read-only limitat la snapshot-urile din scope. Maximum cinci runde, maximum cinci candidați, 15.000 tokeni validați per analiză și deadline 110s pentru worker. Usage-ul este persistat; o operație cloud reluată poate fi facturată din nou.
7. UI face polling, arată proveniența, incertitudinea, stocul timestamped și exportă JSON. Întrebările suplimentare folosesc snapshot-ul salvat, nu întregul catalog. Ștergerea fotografiei marchează analizele `Stale` și elimină obiectul.

Seed-ul are două adaptoare USB negre și un mouse alb. Stocurile au un timestamp fix și devin `stale` după 24h; nu sunt actualizate artificial la fiecare pornire. Numerele financiare sunt serializate ca șiruri zecimale. Modelul nu confirmă identitatea exactă a unui SKU.

## API

- `POST /api/documents` — multipart `file`, 202 sau 200 pentru hash existent.
- `GET /api/documents/{id}` și `/content`; `DELETE /api/documents/{id}`.
- `POST /api/visual/searches` — `imageDocumentId`, `question?`, `catalogVersion=seed-v1`.
- `POST /api/visual/searches/{id}/analyze` — `revision`, `requestId` UUID; 202 și job ID.
- `GET /api/visual/searches/{id}` — stare, revizie, observații, candidați și usage.
- `POST /api/visual/searches/{id}/questions` — `question`, `requestId` UUID.
- `GET /api/visual/catalog`, `/{sku}`, `/{sku}/stock` — read-only.
- `GET /api/health/ready` — verifică PostgreSQL și S3.
- Gateway: `POST /internal/vision`, `/internal/grounded`, protejate cu `X-Internal-Key`.

Erorile publice au `code` și `traceId`, fără răspunsul brut al providerului. O revizie veche primește 409. Nu există endpoints de scriere pentru catalog/stoc.

## Teste

```bash
# Expune infrastructura numai pentru teste locale:
AI_MODE=Demo docker compose -f compose.yaml -f compose.test.yaml up --no-build -d
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget/packages
export PATH="$DOTNET_ROOT:$PATH"
./scripts/verify.sh
python3 scripts/smoke.py
cd src/shopvisionai-web
PLAYWRIGHT_BROWSERS_PATH=/workspace/.cache/ms-playwright npm exec playwright install chromium
SHOPVISION_WEB_URL=http://127.0.0.1:8080 npm run test:e2e
# În cloud poți folosi Chromium deja instalat:
PLAYWRIGHT_CHROMIUM_EXECUTABLE=/usr/bin/chromium SHOPVISION_WEB_URL=http://127.0.0.1:8080 npm run test:e2e
```

Testele backend folosesc un DB separat `shopvisionai_tests` pe port 15432 și bucket-ul `shopvision-tests` pe port 19000; DB-ul de test este recreat. Nu folosi o bază de date de business în `SHOPVISION_TEST_DB`. Conexiunile sunt suprascriptibile prin `SHOPVISION_TEST_DB` și `SHOPVISION_TEST_S3`. Testele SQL nu folosesc EF InMemory. Fixture: `fixtures/black-usb-adapter.png`.

`./scripts/live-smoke.sh` rulează separat cu Foundry real și refuză rezultatele Demo. Nu este rulat implicit în CI și necesită resurse Azure autorizate. Vezi [validarea](docs/validation.md) și [sursele oficiale](docs/official-sources.md).

## Acoperirea documentului

Este implementat nucleul de căutare vizuală și catalog/stoc read-only. Retrieval-ul implicit este EF configurat pentru seed; RAG Azure AI Search/pgvector/File Search/agentic retrieval este opțional în PDF și nu este activat. Adaptoarele de laborator AI-103 (Content Understanding pentru PDF/video, Speech, Language, Translator, generare media, MCP, multi-agent, fine-tuning) nu sunt implementate în acest livrabil. API-ul acceptă intenționat numai fotografii; nu anunță suport PDF/DOCX/video fără extractor. Nu există chat general pe documente, pipeline de chunk-uri sau index extern. Aceste extensii cer implementare și validare separată, nu doar schimbarea unui feature flag.

Mediul cloud este deja izolat. Folosește checkout-ul existent; nu crea Git worktree decât dacă este cerut explicit. Procesele nu supraviețuiesc publicării/snapshot-ului și trebuie repornite prin instrucțiunile salvate.
