# SupportAI

Asistent de suport pentru clienți, implementat după ghidul `02_SupportAI_Ghid_Dezvoltare_NET_Foundry.pdf`. Un singur ecran React, un API modular .NET 10, un gateway AI separat, PostgreSQL și MinIO. Fără conturi, CRM, plăți sau administrarea comenzilor.

## Pornire

Necesită Docker Compose. Pentru dezvoltare nativă: SDK .NET 10.0.401, Node 22.12+ și Python 3 pentru scripturile de verificare.

```sh
cd 02-support-ai
python3 scripts/create-env.py --demo
docker compose --env-file .env up -d --build
python3 scripts/readiness.py
```

Deschide aplicația pe portul local 8080. Compose publică numai frontendul pe loopback. PostgreSQL și obiectele S3 au volume persistente; cele cinci servicii permanente sunt `web`, `api`, `ai`, `db`, `minio`. `migrate` aplică migrațiile EF și se oprește.

`create-env.py` generează parole locale aleatorii și păstrează un `.env` existent. Cheile Azure și numele deploymenturilor rămân placeholders; `.env` este ignorat de Git. Modul **Demo** este determinist și indicat explicit în UI și răspunsuri; nu face inferență, OCR sau apeluri Azure. Textul și Markdown funcționează local. Pentru imagini/PDF/audio/video este necesar Content Understanding real.

## Microsoft Foundry

În `.env`, setează `AI_MODE=Foundry` și înlocuiește:

- `FOUNDRY_OPENAI_ENDPOINT`: baza resursei cu `/openai/v1/` la final.
- `FOUNDRY_API_KEY`: cheia resursei, numai pe server.
- `CHAT_DEPLOYMENT`: numele exact al deploymentului, de exemplu `supportai-chat`.
- `CU_ENDPOINT`, `CU_API_KEY`: endpointul și cheia Content Understanding.

Aplică modificările cu `docker compose --env-file .env up -d --force-recreate ai api`. API-ul v1 Responses folosește `api-key`, `store=false`, tools allowlistate, maximum cinci apeluri/rounds și un buget cumulativ de 15.000 tokeni. Rezultatele incomplete, JSON-ul invalid, citările inventate și retururile neeligibile sunt respinse. Formatul invalid se repară cel mult o dată.

Content Understanding primește bytes din S3 prin `analyzeBinary` API `2025-11-01`; URL-ul operației se salvează în PostgreSQL, iar pollingul validează hostul și reatașează cheia. Analyzerul este `prebuilt-documentSearch`, `prebuilt-audioSearch` sau `prebuilt-videoSearch`, în funcție de modalitate. Verifică disponibilitatea analyzerului și model mappings în resursa Azure.

Profilul implementat este **NoRag**, descris în ghid: gatewayul primește un snapshot limitat la opt surse active și zece mesaje de istoric. Nu pretinde că face vector search. Azure AI Search, pgvector, File Search, agentic retrieval, MCP și generarea media sunt laboratoarele opționale ale ghidului și nu sunt activate/implementate de acest profil. Configurarea unui provider nesuportat oprește startupul.

Prompt Shields poate fi activat prin `PROMPT_SHIELDS_ENABLED=true`, `SAFETY_ENDPOINT` și `SAFETY_API_KEY`; erorile de safety blochează inferența. Filtrele de conținut ale deploymentului se configurează în Azure. Nu este promisă detecția completă a prompt injection sau PII. OpenTelemetry instrumentează ASP.NET și HttpClient; exportul OTLP este activ numai când procesul primește `OTEL_EXPORTER_OTLP_ENDPOINT`. Nu se loghează prompturi, fișiere sau chei.

## Fluxul aplicației

1. Alege `LAB-1001`, `LAB-1002` sau `LAB-1003` și creează un caz anonim. Comenzile sunt fixtures read-only.
2. Încarcă dovezi. Se verifică extensia, semnătura reală, limita de 50 MiB și SHA-256. Un fișier existent este deduplicat. Dovezile se atașează numai după starea `Ready`.
3. Trimite mesajul. API-ul salvează mesajul și jobul atomic și întoarce `202`; UI face polling.
4. Verifică răspunsul, politica seed și sursele. Originalele se deschid prin API, inclusiv range requests pentru media.
5. Inspectează propunerea și confirmă explicit. Tichetul/returul se execută doar local. Confirmarea repetată/concurentă întoarce aceeași execuție. O propunere modificată, expirată sau o cheie reutilizată produce `409`.

Joburile folosesc lease atomic PostgreSQL, `SKIP LOCKED`, token de lease și reclaim după expirare. Gatewayul nu are DB. Pentru erori tranzitorii sunt cel mult trei încercări cu backoff; un crash după un apel Foundry poate repeta costul, fără promisiunea exactly-once în cloud. Ștergerea ascunde imediat sursa, marchează răspunsurile vechi și invalidează propunerile neexecutate; cleanupul S3 se execută durabil.

Politica și comanda sunt read-only în model. Modelul nu primește secrete, adrese arbitrare sau tools de scriere externă. Snapshoturile comenzilor sunt artificiale și raportate la momentul pornirii laboratorului; nu reprezintă un ERP real.

## Structură

| Proiect | Responsabilitate |
| --- | --- |
| `SupportAI.Domain` | Entități, DTO-uri și modele; fiecare tip în fișier separat |
| `SupportAI.Application` | Cazuri, documente, acțiuni, validare, worker și abstracții |
| `SupportAI.Database` | DbContext, relații, constrângeri și migrații EF Core |
| `SupportAI.Infrastructure` | S3 și client HTTP pentru gateway |
| `SupportAI.Api` | Controllere, health și ProblemDetails |
| `SupportAI.Ai` | Responses, tools, Content Understanding, Prompt Shields și autentificare internă |
| `supportai-web` | Interfață React/TypeScript într-un singur ecran |

## Build și teste

```sh
dotnet restore SupportAI.slnx --locked-mode
dotnet build SupportAI.slnx -c Release --no-restore
dotnet test tests/SupportAI.UnitTests -c Release --no-build
npm --prefix src/supportai-web ci
npm --prefix src/supportai-web run build
npm --prefix src/supportai-web test
# Testele de integrare folosesc PostgreSQL și S3 reale, cu porturi numai pe loopback:
docker compose --env-file .env -f compose.yaml -f compose.dev.yaml up -d --build
python3 scripts/integration.py
python3 scripts/smoke.py
python3 scripts/restart-smoke.py
```

Testele de integrare creează și șterg numai baza de date și bucketul propriu de test. Lipsa serviciilor produce eroare; testele nu sunt sărite implicit. Testele HTTP ale Foundry folosesc răspunsuri deterministe și nu probează deploymentul Azure. Datasetul `evaluation/cases.json` conține 25 de scenarii, inclusiv ocolirea confirmării. Acesta este un baseline determinist de contracte, nu o evaluare a calității modelului real.

`python3 scripts/smoke-foundry.py --file /path/to/synthetic.pdf` verifică separat un provider real, extragerea și citările. Necesită resurse configurate și face apeluri facturabile. CI folosește Demo și nu cheamă Azure.

Formatare: `dotnet format SupportAI.slnx` și `npx --prefix src/supportai-web prettier --write 'src/supportai-web/src/**/*.{ts,tsx,css}'`. Lockfiles sunt incluse. MinIO este compilat din tagul upstream `RELEASE.2025-10-15T17-29-55Z`, cu verificarea checksumurilor Go; este o distribuție de laborator, iar pentru utilizare menținută trebuie ales un storage S3 suportat.

Oprire: `docker compose down` păstrează volumele. `down -v` este un reset explicit care șterge datele. Instrucțiunile cloud sunt în [docs/cloud-environment.md](docs/cloud-environment.md).

Surse oficiale consultate: [Responses API](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses), [Content Understanding REST](https://learn.microsoft.com/azure/ai-services/content-understanding/quickstart/use-rest-api), [migrații EF Core](https://learn.microsoft.com/ef/core/managing-schemas/migrations/), [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/).
