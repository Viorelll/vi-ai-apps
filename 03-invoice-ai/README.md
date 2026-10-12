# InvoiceAI

Aplicație .NET 10 + React/TypeScript pentru facturi și bonuri, implementată după ghidul furnizat. Toate fișierele aplicației sunt în acest director; proiectul existent `01-docu-ai` rămâne separat.

## Pornire

```bash
cd 03-invoice-ai
cp .env.example .env   # numai dacă .env nu există
# Mod demo, fără resurse Azure:
docker compose up -d --build
```

Interfața este servită pe portul local 8080. Încarcă `samples/invoice.pdf`, apasă **Procesează documentul**, verifică `TOTAL_MISMATCH`, corectează **Total** din 119 în 120 și descarcă JSON. `samples/receipt.pdf` este un bon valid. Demo este un adaptor deterministic pentru exact aceste două fișiere, verificat prin SHA-256; nu pretinde să facă OCR pe documente arbitrare.

Pentru documente proprii setează `AI_MODE=Azure` în `.env` și completează `DOCINT_ENDPOINT`, `DOCINT_KEY`, `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_API_KEY` și `CHAT_DEPLOYMENT`. `CHAT_DEPLOYMENT` este **numele deployment-ului** Azure, nu numele modelului din catalog. Endpoint-ul Responses trebuie să se termine în `/openai/v1/`. Cheile rămân exclusiv în containerele server. Repornește configurația cu `docker compose up -d --force-recreate`. Dummy values sunt intenționate și trebuie înlocuite; integrarea Azure live nu poate fi validată cu ele.

## Structură

| Proiect | Responsabilitate |
| --- | --- |
| `InvoiceAI.Domain` | Entități, fiecare în fișier separat |
| `InvoiceAI.Contracts` | DTO-uri publice și interne, fiecare separat |
| `InvoiceAI.Services` | Upload, reguli decimal, corecții, export și chat; interfețe de persistență |
| `InvoiceAI.Database` | EF Core/Npgsql, migrații, repositories și lease atomic PostgreSQL |
| `InvoiceAI.Infrastructure` | S3, HTTP gateway, configurare și health probe |
| `InvoiceAI.Api` | Controllers și worker durabil; un singur backend de business |
| `InvoiceAI.Ai` | Document Intelligence v4, Foundry Responses, tools read-only, Prompt Shields opțional; fără DB |
| `invoiceai-web` | Un singur ecran React, polling, surse, corecții și export |

Docker Compose păstrează cinci servicii permanente: `web`, `api`, `ai`, `db`, `minio`. `migrate` rulează o singură dată și trebuie să termine cu cod 0 înainte de API. Doar web are port publicat, legat la interfața loopback. Volumele păstrează DB și fișierele. `docker compose down` le păstrează; **nu utiliza `down -v` pe date de păstrat**.

## Comportament

- Upload PDF/PNG/JPEG/TIFF: maximum 50 MiB, verificare magic bytes + MIME + extensie, spool limitat pe disc, SHA-256 și deduplicare. S3 upload este compensat dacă salvarea DB eșuează.
- Analiza este explicită, asincronă și idempotentă pentru versiunea pipeline-ului. URL-ul operației providerului este persistat înainte de polling. Lease de 90 secunde, apeluri limitate la 65 secunde, reclaim după crash, maximum trei încercări pentru erori tranzitorii; polling-ul nu consumă încercări.
- Document Intelligence API `2024-11-30`: `prebuilt-invoice` / `prebuilt-receipt`. Fișierul este transmis ca bytes, fără URL MinIO accesibil din Azure. URL-urile de polling acceptă numai HTTPS pe host-ul configurat; redirect-urile sunt dezactivate.
- Sunt păstrate raw text, valori tipizate, confidence, pagina, polygon și spans. Null înseamnă necunoscut. Nu se ghicește moneda sau un număr OCR ambiguu.
- Validarea se face în .NET cu decimal: obligatorii, monedă, subtotal + taxă, suma liniilor, cantitate × preț, confidence, proveniență și posibile duplicate business. Profilul inițial presupune linii nete, subtotal net și o taxă totală, fără discount/freight separat. Toleranță 0.01 pentru monede fracționare, 1 pentru JPY/KRW.
- Corecțiile păstrează valoarea originală și timestamp-ul, incrementează `revision` și revalidează. O revizie veche întoarce 409. Explicația veche este invalidată după corecție.
- Export schema v1 este blocat la orice issue `error`; include valori, linii, proveniență și corecții. Snapshot unic pentru `(invoiceId, revision, schemaVersion)`, cu SHA-256. Repetarea downloadului nu produce operații ERP.
- Foundry Responses folosește structured JSON și tools executate local: `read_extracted_fields`, `calculate_totals`, `find_possible_duplicate`, `explain_validation_issue`, `prepare_export`. Scope-ul este injectat de backend; tools nu au scrieri externe. Maximum 5 rounds, 5 calls, 15000 tokens și deadline. Citările inventate și răspunsurile incomplete sunt respinse. Usage disponibil, inclusiv cel al apelurilor eșuate, este acumulat pentru analiză; timeouts pot avea cost provider necunoscut.
- Chat anonim este legat de factura selectată, cu istoric scurt și citări validate; sursele șterse/vechi sunt refuzate. Ștergerea ascunde imediat originalul și invalidează analiza; cleanup S3/chunks este durabil. `GET /api/jobs/{id}` expune progresul fără URL/chei interne; un DELETE repetat replanifică un cleanup eșuat după repararea dependenței. După cleanup se poate încărca din nou același fișier.
- Autentificare internă prin `X-Internal-Key`, comparație constant-time, fără expunerea cheilor în browser. ProblemDetails conține code/traceId sigur; raw documents și cheile nu sunt logate. OpenTelemetry instrumentează API/gateway și HTTP. Prompt Shields se activează separat cu `PROMPT_SHIELDS_ENABLED=true`, endpoint și key proprii; content filters se configurează în resursa Azure.

## Dezvoltare și teste

.NET SDK este fixat în `global.json`; pachetele NuGet/npm au versiuni și lockfiles. Node 24.19+ este folosit pentru frontend. Nu folosi EF InMemory pentru testele SQL.

```bash
dotnet tool restore
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --filter 'Category!=Integration'
cd src/invoiceai-web
npm ci
npm run build
npm test
```

Testele `Category=Integration` creează o **bază de date separată cu nume aleator** și o șterg după rulare; utilizează PostgreSQL real și MinIO real. Utilizatorul de test trebuie să poată crea baze de date. Configurează `INVOICEAI_TEST_DB` (connection string) și, opțional, `INVOICEAI_TEST_S3` (endpoint). Credențialele artificiale implicite coincid cu `.env.example`; pentru alte chei configurează `INVOICEAI_TEST_S3_ACCESS_KEY` și `INVOICEAI_TEST_S3_SECRET_KEY`, fără a le comite.

```bash
# Cu PostgreSQL și MinIO accesibile local:
dotnet test --filter 'Category=Integration'
# Cu aplicația pornită în Demo:
python scripts/smoke.py --base-url http://127.0.0.1:8080
python scripts/restart-smoke.py   # repornește doar API/AI din Compose
# Din src/invoiceai-web, cu Chromium instalat:
CHROMIUM_PATH=/usr/bin/chromium npx playwright test
```

Testul browser presupune seed nou (nu o factură demo deja corectată), verifică și layout-ul mobil și își elimină documentul la final. Smoke-ul nu trebuie rulat pe o configurație Azure/producție. `scripts/validate.sh` rulează build, unit tests, formatare și validare Compose. `ci/github-actions.yml` este un template de pipeline, păstrat aici pentru a respecta cerința ca toate fișierele noi să fie în directorul aplicației; pentru activare GitHub trebuie mutat în `.github/workflows/` la rădăcina repository-ului.

## Mediul cloud pregătit

În această instanță CDN-ul pentru imaginile MCR a fost blocat de politica de egress. Au fost construite și validate imagini din **aceleași outputs .NET publicate pe host**, runtime-ul SDK oficial verificabil și MinIO compilat din tag-ul oficial. `scripts/cloud-build.sh` produce `.local/compose.cloud.yaml` și imaginile locale; `.local` nu intră în Git. Baza acestor imagini de test este PostgreSQL/Debian și conține pachete suplimentare; pentru deploy folosește Dockerfile-urile standard cu runtime ASP.NET.

```bash
# Mediul onboarding salvează activarea toolchain-ului în acest fișier extern:
source /workspace/invoiceai-env.sh
bash scripts/cloud-build.sh
docker compose -f compose.yaml -f .local/compose.cloud.yaml up -d --no-build
```

Outputs pe disc și dependențele pot fi păstrate în snapshot; procesele trebuie repornite. Folosește checkout-ul existent; fiecare task cloud este deja izolat și nu necesită un Git worktree nou. Instrucțiunile de instalare/start și domeniile de rețea au fost salvate separat în draft-ul mediului. Salvarea draft-ului nu publică mediul.

## Limite și extensii

Nucleul rulează în profilul **NoRag**, permis de specificație. Search/pgvector/File Search/agentic retrieval, MCP, Speech/Translator/Language, Content Understanding alternativ, image/video generation, multi-agent și fine-tuning sunt extensii de laborator **neimplementate** în această versiune; nu sunt prezentate drept capabilități active. Nu există ERP, login, billing sau dashboard. Pentru deployment menținut înlocuiește MinIO Community arhivat cu o distribuție S3 suportată, verificând licența; tag-ul din Compose este pentru laborator. Nu expune aplicația anonim pe Internet pentru date confidențiale.

Raportul verificărilor curente este în `docs/validation.json`; integrarea Azure live rămâne de executat după înlocuirea placeholderelor. Pe o bază de test curată, `python scripts/live-smoke.py --accept-cloud-costs` verifică endpoint/auth/extracție/Foundry cu seed artificial și lasă rezultatul pentru revizuire. Documentația oficială pentru configurația serviciilor este listată în `docs/microsoft-references.md`.
