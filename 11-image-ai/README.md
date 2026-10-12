# ImageAI

Studio pentru imagini publicitare, implementat după `11_ImageAI.pdf`: brief + fotografie opțională → generare → mască PNG la rezoluția originală → versiune derivată → export PNG cu text exact. Un singur ecran, fără conturi, campanii sau billing.

## Pornire rapidă

Necesită Docker Engine + Compose. Valorile inițiale sunt **placeholder-e pentru dezvoltare**. Copierea `.env` este opțională pentru demo; fișierul nu se comite.

```bash
cd 11-image-ai
cp .env.example .env # numai dacă nu există deja un .env personalizat
# Mod Demo explicit, fără Azure; cinci containere permanente + migrate one-shot.
docker compose up -d --build --wait
```

Web este publicat numai pe loopback, portul 8080. Browserul folosește exclusiv `/api`; API, AI, PostgreSQL și MinIO rămân în rețeaua internă. `docker compose down` păstrează volumele; `down -v` șterge datele.

În cloud-ul Codex, proxy-ul necesită trust roots și rezolvarea hostului `proxy` în build. Folosește:

```bash
bash scripts/install-cloud.sh
bash scripts/start-cloud.sh
```

Scriptul cloud instalează .NET 10.0.401 din arhiva Microsoft cu SHA-512 verificat, restaurează lockfile-urile și compilează Release. Build-ul Docker folosește aceeași arhivă verificată deoarece blob-urile MCR nu sunt accesibile în acest runner. `compose.cloud.yaml` adaugă numai configurația pentru build; nu dezactivează TLS sau verificarea semnăturilor APT. Build-urile se fac secvențial pentru a limita utilizarea discului. SDK-ul și cache-urile se păstrează sub `/workspace`; procesele trebuie pornite din nou în fiecare task. Folosește checkout-ul existent, fără worktree nou.

## Foundry real

Editează `.env` local, fără a comite chei:

```dotenv
IMAGE_PROVIDER=Foundry
IMAGE_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
IMAGE_API_KEY=YOUR-RESOURCE-KEY
IMAGE_DEPLOYMENT=YOUR-GPT-IMAGE-DEPLOYMENT
IMAGE_API_VERSION=2025-04-01-preview
CHAT_DEPLOYMENT=YOUR-GPT-4-1-MINI-DEPLOYMENT
```

Deployment-urile sunt numele configurate în Azure, nu identificatorii de catalog. Endpoint-ul este rădăcina resursei HTTPS. Modelul de referință este familia GPT-image-1; alte modele necesită verificarea contractului/versiunii API. Responses pentru brief/vision este separat de Image API. Dacă `CHAT_DEPLOYMENT` este gol, se folosește un prompt determinist din brief, fără etapă text/vision. Dacă este configurat, Responses trebuie să producă `completed`; refuzurile sau răspunsurile incomplete nu ajung la generare. Snapshot-ul păstrează promptul efectiv și usage-ul disponibil pe etape. Usage necunoscut rămâne `null`.

Înlocuiește și `DB_PASSWORD`, `MINIO_ROOT_PASSWORD`, `INTERNAL_API_KEY`. Aplică noua configurație cu `docker compose up -d --force-recreate --wait`. În environment settings adaugă exact hostname-ul resursei Azure la accesul de rețea, dacă politica este restrictivă. Cheile sunt exclusiv server-side. Înainte de expunere publică, adaugă autentificare la infrastructură și folosește identități/priviliegii minime; distribuția de față este un laborator privat.

Demo este un provider separat și vizibil în UI; nu este fallback pentru Azure. Creează imagini geometrice deterministe pentru testarea fluxului și nu oferă evaluare de calitate/safety AI.

## Structură

| Proiect | Responsabilitate |
| --- | --- |
| `ImageAI.Api` | Controllere, ProblemDetails, rate limiting, health și migrare one-shot |
| `ImageAI.Contracts/Dto` | Contracte HTTP în fișiere separate |
| `ImageAI.Domain/Entities`, `Models` | Proiecte, runs, assets, documente, măști, joburi și snapshots |
| `ImageAI.Services` | Reguli, idempotency, revisions, job worker, lease și client gateway |
| `ImageAI.Database` | EF Core/Npgsql, schema `app`, configurare relații și migrații |
| `ImageAI.Infrastructure` | S3 și decodare/validare imagini cu SkiaSharp |
| `ImageAI.Ai` | REST Foundry Responses + Image API, vision, provider demo; fără acces DB |
| `imageai-web` | React/TypeScript/Vite, Zod, canvas, galerie, comparație și export |
| `tests`, `scripts`, `docs`, `deploy`, `ci` | Teste, verificări, documentație și deployment |

Clasele, serviciile, entitățile și DTO-urile au fișiere separate. Backend-ul rămâne un monolit modular. .NET 10 / EF Core 10 / PostgreSQL 17 / Node 24, versiuni fixate și lockfile-uri comise. MinIO este fixat la tag și digest Bitnami Legacy, fiind disponibil în registry-ul runner-ului; verifică o distribuție MinIO întreținută și compatibilă înainte de un deploy de producție.

## API

- `POST /api/images/projects` — `{brief}`, 201.
- `GET /api/images/projects` — ultimele 50 de proiecte, pentru restaurare în UI.
- `GET /api/images/projects/{id}` — stare, revision, assets și runs.
- `POST .../{id}/generate` — `{requestId, revision, size, quality, inputDocumentId?}`, 202.
- `POST .../{id}/edits` — `{requestId, revision, inputAssetId, maskDocumentId, prompt, quality}`, 202.
- `POST .../{id}/variants` — `{requestId, revision, assetId, brief, quality}`, 202.
- `GET /api/images/assets/{id}/content` — PNG immutable, ETag, Range.
- `POST /api/documents` — multipart `file`; imagine validată disponibilă imediat, 200; deduplicare SHA-256.
- `GET /api/documents/{id}` / `.../content`, `DELETE .../{id}` — metadate/bytes/ștergere document nefolosit.
- `GET /api/config` — modul providerului; fără secrete.
- `/health/live`, `/health/ready` — readiness locală fără inferență facturabilă.

Format: 1024×1024, 1536×1024 sau 1024×1536; quality low/medium/high; n=1; maximum 12 rezultate reușite/proiect. Upload PNG/JPEG ≤50 MiB, ≤20 milioane pixeli, decodare completă și respingerea animațiilor. Masca este PNG alpha cu aceleași dimensiuni ca asset-ul, selecție transparentă și zonă protejată opacă. Validează proprietatea asset-ului în proiect; nu acceptă URL-uri de provider furnizate de browser.

`requestId` se reutilizează pentru retry-ul **aceleiași cereri**. Payload diferit → 409. Revision învechită / proiect ocupat → 409. Metadata + job se salvează atomic. Worker-ul revendică prin `FOR UPDATE SKIP LOCKED`, în afara apelului AI; persistă obiectul S3 înainte de `Ready`. Rezultatul/snapshot-ul au unicitate per run. Originalul și măștile folosite rămân immutable.

429 produce backoff limitat, maximum trei încercări. Timeout/5xx/crash în timpul unei operații media → `Uncertain`, fără replay automat care ar putea dubla factura. Lease expirat → `Uncertain`. Verifică operația la provider înainte de o cerere nouă. Nu se promite exactly-once pentru Azure. Safety rejection → `Blocked`. ProblemDetails include code/traceId sigur; raw provider bodies și cheile nu sunt logate.

## Dezvoltare și teste

```bash
# .NET 10.0.401 și Node 24
export DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget/packages
export PATH=/workspace/.dotnet:$PATH
dotnet tool restore
dotnet restore ImageAI.slnx --locked-mode
dotnet build ImageAI.slnx -c Release --no-restore
dotnet test ImageAI.slnx -c Release --no-build --filter 'Category!=Integration'
cd src/imageai-web
npm ci
npm run build
npm test
npm run format:check
```

Teste PostgreSQL reale, în baze temporare separate, create/șterse numai de fixture:

```bash
# Portul de test este opțional și numai loopback.
docker compose -f compose.yaml -f compose.test.yaml up -d db
export IMAGEAI_TEST_DB='Host=127.0.0.1;Port=5433;Database=imageai;Username=imageai;Password=replace-with-database-password'
dotnet test ImageAI.slnx -c Release --filter 'Category=Integration'
```

Utilizează parola locală curentă pentru test; nu o salva în git. Testele verifică migrațiile repetabile, schema, unique constraints, optimistic concurrency, claim concurent și lease expirat. Nu folosesc EF InMemory ca substitut pentru SQL.

```bash
python3 scripts/smoke.py --base http://127.0.0.1:8080
# Generare + editare Foundry reală, explicit facturabilă, numai după configurare:
python3 scripts/smoke.py --base http://127.0.0.1:8080 --live
# După restart, folosind projectId din raportul anterior:
python3 scripts/smoke.py --base http://127.0.0.1:8080 --verify-project PROJECT_ID
# Browser E2E (folosește Chromium instalat dacă există):
cd src/imageai-web
CHROMIUM_PATH=/usr/bin/chromium IMAGEAI_WEB_URL=http://127.0.0.1:8080 npx playwright test
```

Pe un calculator fără Chromium instalează browserul Playwright cu `npx playwright install chromium`. În runner a fost folosit Chromium de sistem deoarece CDN-ul Playwright este blocat. Pentru frontend dev: `npm run dev`; Vite proxyează API-ul local pe 5080. Pentru API/AI native setează connection/storage/gateway și pornește proiectele pe 5080/5081; în Compose se folosesc 8080 intern.

Format C#: `dotnet format` și `DOTNET_ROLL_FORWARD=Major dotnet tool run csharpier format .`. Testele de UI verifică generare, desenarea măștii, editare, comparație, export PNG, restaurare după reload și layout mobil. `scripts/check.sh` combină verificările; `ci/imageai.yml` este un workflow gata de copiat în `.github/workflows/` când se activează CI la nivelul repository-ului. Toate fișierele acestei aplicații sunt păstrate în directorul ei.

## Domeniu și limite

Implementat nucleul obligatoriu din capitolele 1–11, 24–29 și 32–35: studio image generation/editing, versiuni, S3/PostgreSQL, prompt/vision opțional, safety al providerului, cost necunoscut explicit, corelație și testare. RAG, extracție PDF/audio/video, chat cu citări, agenți/tool calling, MCP, Speech/Language/Translator și evaluare cloud sunt laboratoare opționale descrise în PDF; nu sunt expuse ca funcționalități active și nu există adaptoare fictive care pretind că le execută. Vezi [acoperirea specificației](docs/specification.md) și [validarea](docs/validation.md).

Testul live Azure nu poate fi considerat trecut cu placeholder-e. Readiness AI verifică modul/configurarea locală; nu garantează auth, disponibilitatea regională sau cota deployment-ului. Masca ghidează modelul, fără garanție de identitate a pixelilor protejați; păstrează/comparează originalul.

Surse oficiale: [Microsoft Image generation](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/dall-e), [Responses](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses), [EF migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying), [hosted services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services), [Npgsql](https://www.npgsql.org/efcore/), [SkiaSharp](https://github.com/mono/SkiaSharp). Documentația Microsoft Image API a fost consultată din sursa oficială MicrosoftDocs deoarece accesul direct la Learn a fost refuzat de proxy.
