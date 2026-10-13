# ModerateAI

Platformă locală de moderare text/imagini după [specificația PDF](docs/18_ModerateAI.pdf). Un singur ecran React, API modular .NET 10, gateway AI fără DB proprie, PostgreSQL 17 și storage S3. Nu există login, roluri, sancțiuni sau scrieri pe platforme externe.

## Pornire

Necesită Docker Compose, SDK .NET **10.0.401**, Node **22.12+**, Python 3. Nucleul funcționează local în **Demo** cu placeholders Azure.

```sh
cd 18-moderate-ai
python3 scripts/create-env.py
python3 scripts/prepare-s3.py
docker compose up -d --build --wait
python3 scripts/readiness.py
```

Web folosește portul **8088** numai pe loopback. Cinci servicii permanente: `web`, `api`, `ai`, `db`, `minio`. `migrate` aplică migrațiile EF și se oprește. Storage-ul din serviciul `minio` este **SeaweedFS S3**, fixat prin digest: distribuția MinIO din ghid nu mai este accesibilă în mediul curent. Backendul folosește `IObjectStore` / AWS SDK S3; poate fi configurat cu alt serviciu S3 suportat. Nu este emulare Azure.

`create-env.py` păstrează fișierul `.env` existent și generează parole locale aleatorii numai la prima pornire. Fișierul `.dev/s3.json` este generat, ignorat și protejat cu permisiuni 0600. Nu comite aceste fișiere. `docker compose down` păstrează datele; `down -v` le șterge explicit.

## Flux

1. Introdu text și/sau încarcă PNG/JPEG. Validare prin decodare, extensie, 4 MiB și dimensiuni 50–7200 px; hash SHA-256 și deduplicare.
2. Creează item și lansează analiza explicit. API răspunde 202, UI face polling pe item; joburile persistă în PostgreSQL.
3. Consultă categoriile **Hate, SelfHarm, Sexual, Violence**, versiunea API, scala și politica .NET. Preview-ul inputului este ascuns până la o acțiune explicită.
4. Cazurile `Review` intră în coada locală paginată. Confirmă `allow`/`block` cu revizia curentă; un conflict produce 409. Override-ul nu rescrie scorurile sau politica inițială.
5. Exportă JSON sau rezultatul brut. Ștergerea sursei o ascunde imediat, marchează rezultatele stale și programează cleanup S3 durabil.

Politica seed `policy-four-v1`: scala **FourSeverityLevels** 0/2/4/6, maxim 0 → allow, 2 → review, 4/6 → block. Categoriile lipsă, duplicate, scorurile nevalide și alte scale produc review. Nu sunt probabilități și nici praguri universale Microsoft.

**Demo nu clasifică conținut real.** Numai `[demo:allow]`, `[demo:review]`, `[demo:block]` fără imagine produc fixtures artificiale 0/2/4. Orice alt text sau imagine produce analiză incompletă și review. UI, exportul și raw JSON indică Demo. Nicio eroare Azure nu activează Demo automat.

## Azure real

Modifică `.env` numai pe server:

```dotenv
AI_MODE=Foundry
CONTENT_SAFETY_ENDPOINT=https://YOUR-RESOURCE.cognitiveservices.azure.com/
CONTENT_SAFETY_API_KEY=replace-with-safety-key
FOUNDRY_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/openai/v1/
FOUNDRY_API_KEY=replace-with-foundry-key
CHAT_DEPLOYMENT=moderateai-chat
PROMPT_SHIELDS_ENABLED=false
```

Înlocuiește placeholders și deployează un model care suportă Responses și structured JSON output. `CHAT_DEPLOYMENT` este **numele deploymentului**, nu numele generic al modelului. Configurația incompletă oprește gateway-ul la startup. Aplicare:

```sh
docker compose up -d --no-build --force-recreate ai api
python3 scripts/readiness.py
python3 scripts/smoke.py --live
```

Testul live face apeluri facturabile pentru un text și o imagine sintetice. Nu a fost executat fără credențiale. Content Safety REST **2024-09-01** primește `outputType=FourSeverityLevels` explicit atât pentru text cât și pentru imagine. Inputul combinat se analizează în două cereri distincte; se păstrează ambele rezultate brute și se combină maximul fiecărei categorii numai pentru aceeași scală, cu completeness pentru ambele modalități.

Foundry primește numai scorurile și decizia calculată, nici textul original, nici imaginea. Explicația are JSON strict, `store=false`, maximum 600 tokens output; tool-urile și modificarea deciziei nu sunt oferite modelului. Rezultate incomplete, refusal și JSON invalid produc eroare explicită. Prompt Shields este opțional; o eroare sau un atac detectat oprește analiza. Requests au timeout, redirect-urile sunt dezactivate și TLS rămâne verificat.

## Proiecte

| Proiect | Responsabilitate |
| --- | --- |
| `ModerateAI.Domain` | Entities, DTO, models, interfaces și evaluatorul politicii |
| `ModerateAI.Database` | DbContext, constrângeri, concurrency tokens și migrații EF |
| `ModerateAI.Services` | Submit, review, upload, cleanup și worker durabil |
| `ModerateAI.Infrastructure` | S3, typed HttpClient și integrarea infrastructurii |
| `ModerateAI.Api` | Controllere publice, ProblemDetails, health și migrare one-shot |
| `ModerateAI.Ai` | Content Safety, Prompt Shields și Responses Foundry; fără DB |
| `moderateai-web` | React/TypeScript, validare runtime Zod, polling, export și review |

Fiecare clasă/record/service are propriul fișier. Backendul rămâne un monolit modular, fără MediatR/cozi externe. Nu este necesar RAG pentru decizie. PDF/media extraction, RAG, chat generic, MCP, agenți și generare media din capitolele comune sunt laboratoare opționale **neimplementate**, nu funcții declarate disponibile. Nucleul text/imagine din capitolele 32–35 este implementat.

## Persistență și cost

Submit folosește `requestId` cu hash input; reutilizarea cu alt input produce 409. Claim SQL atomic `FOR UPDATE SKIP LOCKED`, lease token și lease de 180 s; operația are deadline 110 s, fiecare apel provider maximum 30 s. Salvarea rezultatului verifică tokenul și lease-ul, într-o tranzacție scurtă. Maximum 3 încercări pentru 429/5xx/timeout, backoff+jitter, Retry-After limitat. Erorile permanente nu sunt retried. Usage și raw artifact sunt persistate, buget 15.000 tokens. Un crash după un apel cloud poate repeta costul; nu se promite exactly-once.

Unique constraints protejează deduplicarea și un singur rezultat/review per revizie. Concurența pe item invalidează rezultatele/review-urile în curs dacă sursa este ștearsă. Uploadul compensează S3 când salvarea DB eșuează. Un crash între S3 și commit DB poate lăsa un obiect orfan; reconcilierea automată a orfanilor nu este implementată.

OpenTelemetry instrumentează ASP.NET și HttpClient în ambele procese; export OTLP numai când setezi `OTEL_EXPORTER_OTLP_ENDPOINT` în container. Nu se loghează input, prompturi sau chei. Health probează serviciile locale și configurația gatewayului; conectivitatea Azure și deploymentul se verifică prin smoke live, nu prin simpla stare health.

## Testare

```sh
dotnet restore ModerateAI.slnx --locked-mode
dotnet tool restore
dotnet build ModerateAI.slnx -c Release --no-restore
dotnet test ModerateAI.slnx -c Release --no-build --filter 'Category!=Integration'
npm --prefix src/moderateai-web ci
npm --prefix src/moderateai-web run build
npm --prefix src/moderateai-web test
# Publică numai porturi loopback suplimentare pentru test:
docker compose -f compose.yaml -f compose.test.yaml up -d --build --wait
python3 scripts/test-integration.py
python3 scripts/smoke.py
# Browser standard:
cd src/moderateai-web
npx playwright install --with-deps chromium
npm run test:e2e
```

Testele de integrare creează și șterg numai DB și bucketul cu nume aleatorii proprii. Folosesc PostgreSQL și S3 reale, nu EF InMemory; lipsa serviciilor produce eșec explicit. Testele gateway folosesc HTTP determinist și nu dovedesc funcționarea Azure. Baseline-ul `eval/cases.json` are 25 de cazuri de politică, nu o evaluare a calității modelului. CI rulează Demo fără cost Azure.

Formatare: `dotnet csharpier format --no-cache src tests` și `npm --prefix src/moderateai-web run format`. Instrucțiuni specifice Codex: [docs/cloud-environment.md](docs/cloud-environment.md). Rezultate: [VALIDATION.md](VALIDATION.md). Surse Microsoft: [docs/microsoft-references.md](docs/microsoft-references.md).
