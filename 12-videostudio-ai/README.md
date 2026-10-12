# VideoStudioAI

Studio video cu React/TypeScript, .NET 10, EF Core/PostgreSQL și MinIO. Fluxul principal: **brief → storyboard → confirmare locală → submit → poll → MP4 validat → remix legat de clipul original**. Nu există login, conturi sau servicii de billing.

## Pornire

Prerechizite: Docker + Compose. Pentru dezvoltare nativă: .NET SDK 10.0.401, Node 22.12+ (testat cu 24.19.0), FFmpeg/ffprobe, `pdftotext` și Python 3 pentru smoke tests.

```bash
cd 12-videostudio-ai
cp .env.example .env
bash scripts/start.sh
```

Web este singurul serviciu publicat în configurația normală, pe `127.0.0.1:8080`. Sunt exact cinci servicii permanente (`web`, `api`, `ai`, `db`, `minio`); `migrate` este un proces one-shot. Volumele persistă la restart; `docker compose down -v` șterge datele și nu este necesar pentru dezvoltare.

**Implicit rulează Demo**, afișat explicit în UI. În acest mod nu există inferență AI: storyboardul este un șablon, iar FFmpeg generează un MP4 cu watermark DEMO. Submit/poll, verificarea metadatelor, S3, PostgreSQL, playerul și remixul parcurg pipeline-ul real al aplicației. ID-urile demo sunt independente de memoria procesului și pot fi reluate după restart.

## Foundry real

Editează `.env` numai local:

- `AI_MODE=Foundry`.
- `FOUNDRY_OPENAI_ENDPOINT=https://RESOURCE.openai.azure.com/openai/v1/`.
- `FOUNDRY_API_KEY`: cheia resursei autorizate.
- `CHAT_DEPLOYMENT`: numele deploymentului text/vision, de exemplu deploymentul tău GPT-4.1-mini.
- `VIDEO_DEPLOYMENT`: deployment Sora 2 compatibil, cu acces preview în regiunea aleasă.
- Duratele și rezoluțiile din `Foundry:Seconds` / `Foundry:Sizes` sunt allowlist-ul deploymentului, nu o promisiune universală de capabilități. Vezi `.env.example` pentru override-uri individuale.
- Înlocuiește și parolele locale DB/MinIO/cheia HTTP internă înainte de o instalare menținută.

Aplică modificările cu `docker compose up -d --build`. Configurația Foundry cu placeholder-uri e respinsă la startup. Nu există fallback automat la Demo. Cheile rămân pe server, nu în `VITE_*`, browser sau Git. API-ul preview este implementat prin `/openai/v1/videos`, `/videos/{id}`, `/videos/{id}/content` și `/videos/{id}/remix`; nu utilizează lifecycle-ul legacy `video/generations/jobs`.

Smoke test-ul real este separat, poate genera cost și necesită configurarea unei resurse autorizate:

```bash
API_URL=http://127.0.0.1:8080 python3 scripts/live-smoke.py --allow-cloud-cost
```

Nu s-a executat inferență Foundry reală în mediul cu placeholder-uri. Testele HTTP deterministe verifică payloadurile, parsarea și erorile adaptorului; nu demonstrează accesul tenantului sau calitatea modelului.

## Proiecte și responsabilități

```text
src/VideoStudioAI.Domain/          entities și reguli deterministe
src/VideoStudioAI.Contracts/       DTO-uri publice/interne, un tip per fișier
src/VideoStudioAI.Database/        DbContext, relații, migrări PostgreSQL
src/VideoStudioAI.Services/        servicii de aplicație, orchestrare, claim/reclaim
src/VideoStudioAI.Infrastructure/  S3, HTTP gateway, FFprobe, worker, DI
src/VideoStudioAI.Api/             controllere, ProblemDetails, startup/migrate
src/VideoStudioAI.Ai/              Foundry, Demo, extracție, autentificare internă
src/videostudioai-web/             React, scheme Zod, servicii API și componente
tests/VideoStudioAI.Tests/         reguli, contracte Foundry, PostgreSQL, S3/media
scripts/                          setup, startup și smoke tests
evals/                            brief sintetic și criterii de evaluare
deploy/                           Dockerfile-uri și Nginx
```

API-ul deține singura bază de date; gateway-ul AI nu referențiază proiectul Database. Nu sunt introduse Redis, RabbitMQ, microservicii de business sau worktree-uri.

## Dezvoltare și teste

```bash
bash scripts/setup.sh
# Porturi locale pentru teste; nu folosi această suprascriere la publicare.
docker compose -f compose.yaml -f compose.test.yaml up -d db minio
# Terminal 1: schema și workerul
dotnet run --project src/VideoStudioAI.Api -- --migrate
ASPNETCORE_URLS=http://127.0.0.1:5080 dotnet run --project src/VideoStudioAI.Api
# Terminal 2
ASPNETCORE_URLS=http://127.0.0.1:5081 dotnet run --project src/VideoStudioAI.Ai
# Terminal 3
npm --prefix src/videostudioai-web run dev
```

```bash
bash scripts/check.sh
API_URL=http://127.0.0.1:8080 python3 scripts/smoke.py
WEB_URL=http://127.0.0.1:8080 CHROMIUM_PATH=/usr/bin/chromium npm --prefix src/videostudioai-web run test:e2e
```

`dotnet test` folosește PostgreSQL și S3 **reale**; fixture-ul creează și elimină o bază de date separată per run, fără EF InMemory. `TEST_POSTGRES` și `TEST_S3` suprascriu conexiunile de test. Smoke test-ul offline cere Demo și creează proiecte/documente sintetice. Pentru browser, `CHROMIUM_PATH` poate indica Chromium instalat; alternativ instalează browserul Playwright cu `npx playwright install chromium`.

Restore folosește lockfiles; NuGet/npm sunt fixate. C# este formatat cu CSharpier, frontendul cu Prettier. Raportul smoke curent este în `test-results/smoke.json` (ignorat în Git). Nu modifica lockfiles în onboarding; actualizările de dependențe sunt o acțiune de dezvoltare separată.

## Integritate și comportamente importante

- `Revision` detectează schimbări concurente; conflictele au HTTP 409. `RequestId` unic pe proiect deduplică cererile repetate.
- Jobul și snapshot-ul sunt comise înainte de procesarea externă. SQL `FOR UPDATE SKIP LOCKED` revendică un job atomic, cu lease de 90 s; apelurile sunt limitate la 55 s pe iterație. LeaseToken împiedică finalizarea de către workerul vechi.
- `Submitting` este persistat înainte de scrierea externă. Un timeout/crash fără provider ID devine `Uncertain`; workerul nu retrimite automat. UI cere recunoașterea costului posibil pentru o relansare explicită, cu Operation/Request ID nou.
- Provider ID este comis înainte de poll; la restart se reia polling-ul. Nu există tranzacție DB ținută deschisă în timpul HTTP/S3.
- `Ready` necesită MP4 real, dimensiuni și durată verificate prin FFprobe, SHA-256 și obiect S3 persistat. Downloadul are limită 50 MiB și suportă Range în browser.
- Clipurile și relațiile remix sunt immutable. Obiectul original nu este rescris. Snapshot-ul păstrează promptul, deploymentul, parametrii și versiunile pipeline/promptului; usage-ul text e în DB.
- Retry limitat: maximum trei încercări la operațiile reluabile; polling cu backoff 2–60 s; deadline job 30 min; buget text 15.000 tokeni și output limitat. Erorile 401/403 sau input invalid nu sunt reluate automat.
- Uploadurile verifică magic bytes/UTF-8, plafon 50 MiB și hash unic. S3 este compensat dacă inserarea DB eșuează. Fișierele temporare sunt șterse după utilizare.
- Ștergerea documentului îl ascunde imediat; cleanup S3/chunks rulează durabil. Citările păstrează excerpt-ul, iar FK-urile permit ștergerea surselor. Chatul acceptă numai documentele `Ready` selectate și respinge surse inventate.
- Erorile expun `code` și `traceId`, fără chei sau răspunsuri brute ale providerului. Gateway-ul verifică cheia internă cu comparație constantă. Nginx setează CSP și `nosniff`.

## Limite și extensii

Nucleul obligatoriu de studio video este implementat. Extracția locală acoperă text și PDF cu text; imaginile folosesc vision numai în Foundry. Un PDF scanat fără text primește o eroare explicabilă; OCR/Content Understanding, analiza video, Speech, traducere, MCP, multi-agent, concatenare FFmpeg și RAG Azure Search/pgvector/File Search sunt laboratoare opționale **neactivate și neimplementate**. Nu există fallback care să pretindă analiză OCR/video. Retrieval-ul documentelor implementat este lexical, cu scope explicit, nu vectorial.

MinIO Community este construit din commitul upstream `07c3a429bfed433e49018cb0f78a52145d4bedeb` (release 2025-09-07), arhiva verificată SHA-256 și module Go cu verificare normală a sumelor. Imaginea are licență AGPLv3; vezi sursa oficială. Resursele Azure, permisiunile și costurile nu sunt provisionate de Compose.

Surse Microsoft consultate: [Sora 2](https://learn.microsoft.com/en-us/azure/foundry/openai/concepts/video-generation), [Hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0), [DbContext lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/), [Aplicarea migrărilor](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).
