# InspectAI

Asistent pentru inspecția echipamentelor, implementat după `15_InspectAI.pdf`: fotografii + manual confirmat → observații separate `visible` / `inferred` / `unknown` → verificări cu dovezi foto și citate exacte din manual. Un singur ecran React, backend modular .NET 10, gateway Foundry separat, PostgreSQL și storage S3. Nu există conturi, control IoT, ordine de lucru sau certificare a siguranței echipamentului.

## Pornire în mediul cloud

```bash
cd /workspace/vi-ai-apps/15-inspect-ai
# .env este local și ignorat; nu suprascrie un fișier existent.
[ -f .env ] || cp .env.example .env
bash scripts/install-cloud.sh
bash scripts/start-cloud.sh
```

Instalarea verifică SHA-512 al SDK-ului Microsoft 10.0.401, restaurează lockfile-urile NuGet/npm, compilează/testează și publică API/AI self-contained Linux x64. Sunt necesare Node 22.12+, Python 3, Docker și Compose 2.40+. Artefactele sunt ignorate de Git; imaginile runtime se construiesc din ele. După modificări backend/frontend, rulează din nou instalarea și pornirea pentru a reconstrui artefactele.

Interfața se află pe portul local 8080, legat numai la loopback. Docker Compose pornește cinci servicii permanente (`web`, `api`, `ai`, `db`, `minio`) și migrarea one-shot înaintea API-ului. Serviciul `minio` folosește SeaweedFS, compatibil S3, fixat prin digest; nu emulează servicii Azure. PostgreSQL și S3 păstrează datele în volume. Pentru oprire folosește `docker compose down`, fără `-v`, care ar șterge datele.

Smoke-ul de startup păstrează sursele existente și evită inferența facturabilă când există o cheie reală; aceasta se verifică explicit prin scriptul live.

`compose.cloud.yaml` adaugă bundle-ul CA public al mediului prin BuildKit; TLS rămâne verificat. Proxy-ul cloud este transmis gateway-ului, iar traficul intern este exclus din proxy. Containerele API și AI rulează ca utilizatorul `node`, fără root. Pentru o mașină Linux x64 cu .NET SDK deja instalat poți executa comenzile de restore/build/publish din script cu propriile directoare de cache și apoi `docker compose up -d --build` fără overlay-ul cloud.

## Configurare Foundry

Valorile din `.env.example` sunt placeholders, conform cerinței. Înlocuiește în `.env`:

| Variabilă                                                   | Utilizare                                                                                   |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `FOUNDRY_OPENAI_ENDPOINT`                                   | Resursa Azure OpenAI, cu `/openai/v1/` și slash final; nu endpoint-ul de proiect            |
| `FOUNDRY_API_KEY`                                           | Cheia server-side pentru inferență                                                          |
| `CHAT_DEPLOYMENT`                                           | Deployment compatibil Responses, `input_image` și function calling, de exemplu GPT-4.1-mini |
| `CU_ENDPOINT`, `CU_API_KEY`, `CU_ANALYZER`                  | Content Understanding pentru manualele PDF; exemplu `prebuilt-documentSearch`               |
| `POSTGRES_PASSWORD`, `S3_ROOT_PASSWORD`, `INTERNAL_API_KEY` | Credențiale locale; înlocuiește valorile dummy înainte de utilizare menținută               |

Cheile nu ajung în browser. Adaugă în setările de rețea ale mediului hosturile exacte ale resurselor Azure alese. După configurare, rulează din nou `bash scripts/start-cloud.sh` pentru a recrea containerele cu noile valori.

Placeholder-ele permit pornirea infrastructurii și procesarea locală TXT/DOCX/foto. Analiza necesită Foundry real; PDF necesită Content Understanding. Lipsa configurării produce coduri explicite `foundry_not_configured` / `extractor_not_configured`, fără rezultate AI fictive. `/health/live` verifică procesul; `/health/ready` semnalează configurația cloud incompletă cu 503. Readiness nu dovedește accesul efectiv la deployment: acesta se verifică prin smoke-ul live.

## Flux și reguli

1. Creează o inspecție cu etichetă, model confirmat și versiunea manualului. Atașarea manualului cere același model și aceeași versiune; mismatch produce 409.
2. Adaugă maximum opt fotografii PNG/JPEG și un manual PDF/DOCX/TXT/Markdown. Maximum 50 MiB/fișier, text 2 MiB, fotografie 16 milioane de pixeli, imagini normalizate maximum 20 MiB în total la inferență.
3. Upload-ul verifică semnătura/MIME, UTF-8 și structura DOCX, folosește spool temporar, SHA-256 și deduplicare PostgreSQL. Originalele S3 sunt imuabile. Fotografiile sunt decodate și orientate EXIF în PNG separat; preview-ul și modelul folosesc aceeași imagine pentru geometria regiunilor.
4. Extracția persistă jobul și URL-ul operației CU între polling-uri, apoi raw/normalized JSON în S3 și chunk-uri cu generație activă în PostgreSQL. Nu se inventează pagina 1 când extractorul nu oferă poziție.
5. Pornește explicit analiza. API-ul răspunde 202, iar UI urmărește jobul. Gateway-ul trimite imaginile către Responses API și oferă tool-ul read-only `search_manual`, limitat la chunk-urile injectate de backend.
6. Validatorul respinge surse străine, citate inventate, ID-uri duplicate, regiuni invalide, ipoteze fără motiv și checklist fără dovezi din manual și fotografie. Observațiile nu certifică defecte interne. Raportul rămâne pentru revizuire umană.
7. Notează local verificările, pune întrebări strict despre inspecția curentă și exportă JSON. Exportul păstrează hashurile surselor, versiunea manualului/promptului, deploymentul și usage-ul răspunsului validat.
8. Ștergerea unei surse o ascunde imediat, revocă joburile și marchează rapoartele `Stale`; cleanup elimină obiectele S3. Exportul și întrebările pentru raportul învechit sunt respinse.

Revision este token de concurență; mutațiile vechi produc 409. Request IDs deduplică analiza și întrebările. Joburile folosesc claim atomic `FOR UPDATE SKIP LOCKED`, lease de 90 secunde și commit condiționat de lease; worker-ul vechi nu poate publica după revocare. Apelurile sunt limitate la 60 secunde, cinci tool calls/rounds și 15.000 tokeni, cu maximum trei încercări pentru erori tranzitorii și respectarea `Retry-After`. Nu se promite exactly-once pentru costul cloud după un crash. Usage-ul persistat descrie răspunsurile finalizate; facturarea încercărilor eșuate se verifică în Azure.

## Proiecte

| Proiect                    | Responsabilitate                                          |
| -------------------------- | --------------------------------------------------------- |
| `InspectAI.Domain`         | Entități, DTO-uri, modele și validatori deterministici    |
| `InspectAI.Services`       | Documente, inspecții, întrebări și worker durabil         |
| `InspectAI.Database`       | EF Core, migrații, constraints și lease PostgreSQL        |
| `InspectAI.Infrastructure` | S3, HTTP intern, Foundry, extracție și ProblemDetails     |
| `InspectAI.Api`            | Controllere publice și migrarea one-shot                  |
| `InspectAI.Ai`             | Gateway intern fără acces la DB                           |
| `inspectai-web`            | React/TypeScript, componente, servicii și modele separate |

Clasele, serviciile, DTO-urile și entitățile sunt în fișiere separate. Format: CSharpier și Prettier. Dependențele și tool-urile sunt fixate și au lockfile-uri versionate.

## Validare

```bash
export DOTNET_ROOT=/workspace/.tools/dotnet
export DOTNET_CLI_HOME=/workspace/.cache/dotnet
export NUGET_PACKAGES=/workspace/.cache/nuget
export XDG_DATA_HOME=/workspace/.cache/xdg
export PATH="$DOTNET_ROOT:$PATH"
dotnet tool restore
dotnet csharpier check .
INTEGRATION=1 bash scripts/test.sh
CHROMIUM_PATH=/usr/bin/chromium npm --prefix src/inspectai-web run test:e2e
python3 scripts/smoke-placeholder.py
```

Integrarea folosește PostgreSQL și S3 reale într-un proiect Compose separat, cu baze izolate aleatoriu. Doar testele injectează adaptorul AI determinist. Testele browser folosesc mock API și verifică UI-ul; nu sunt o probă Azure. Vezi `VALIDATION.md` pentru rezultate efectiv executate.

După configurarea Azure, testul separat și facturabil este `python3 scripts/smoke-live.py`. Acesta verifică fotografia și manualul artificiale, raportul cu dovezi, exportul și întrebările. Pentru CU încarcă suplimentar un PDF artificial și verifică extracția; testul TXT nu dovedește OCR/PDF live. `eval/baseline.json` documentează criteriile de evaluare. Testele implicite nu cheamă Azure.

## Profilul implementat

`RAG_PROVIDER=NoRag` este varianta permisă în ghid pentru manuale mici: maximum 200 chunk-uri / 100.000 caractere, context direct și căutare lexicală scoped prin tool. Nu pretinde hybrid/vector RAG. Alte valori sunt respinse la startup. Azure Search, pgvector, File Search, agentic retrieval, MCP, Speech, generare media și multi-agent review rămân laboratoarele opționale; nu sunt simulate sau activate. Nu sunt provisionate resurse Azure. Prompturile tratează documentele ca date neîncrezute; filtrele serviciului, Prompt Shields și exporter-ul Azure Monitor necesită configurare separată. Logurile publică code/traceId/job ID, fără dump de secrete sau răspunsuri integrale ale providerului.

## Documentație oficială consultată

- [Microsoft: Responses API](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses)
- [Sursa oficială Responses: input_image, REST v1, function calling](https://github.com/MicrosoftDocs/azure-ai-docs/blob/main/articles/foundry/openai/includes/how-to-responses-content.md)
- [Microsoft: Content Understanding REST](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/quickstart/use-rest-api)
- [Sursa oficială CU: API 2025-11-01 și Operation-Location](https://github.com/MicrosoftDocs/azure-ai-docs/blob/main/articles/ai-services/content-understanding/quickstart/includes/rest-quickstart.md)
