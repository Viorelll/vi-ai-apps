# ContractAI

Asistent pentru analiza contractelor, implementat după ghidul „05 / ContractAI”. Un ecran React, un backend modular .NET 10, un gateway AI separat, PostgreSQL și MinIO. Nu există conturi de aplicație, dashboard, notificări sau verdict juridic.

## Pornire

```bash
cp .env.example .env
# Înlocuiește placeholder-ele pentru Foundry, deployment și Content Understanding.
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build
```

Numai interfața este publicată, pe portul local 8080. API, AI, PostgreSQL și MinIO rămân în rețeaua Compose. Sunt cinci servicii permanente; `migrate` aplică migrațiile EF Core și se încheie înainte de pornirea API-ului. `docker compose down` păstrează volumele; nu utiliza `down -v` pentru o oprire obișnuită.

Pentru acest mediu cloud:

```bash
bash scripts/install-cloud.sh
export BUILDX_CONFIG=/workspace/.buildx
docker compose --env-file .env -f compose.yaml -f compose.cloud.yaml up -d --build
```

Varianta cloud folosește SDK-ul Microsoft oficial, verificat cu SHA-512, când hostul regional MCR nu este disponibil. Traficul către servicii Docker este exclus din proxy în ambele variante `NO_PROXY`/`no_proxy`; Nginx rezolvă dinamic adresa API după restart. Certificatul CA public al mediului este furnizat prin BuildKit; verificarea TLS și checksum-urile rămân active. Nu copia chei sau certificate private în repository.

## Configurare Azure

Completează în `.env` (fișier ignorat de Git):

- `FOUNDRY_OPENAI_ENDPOINT`: baza resursei, cu `/openai/v1/` și slash final; nu endpoint-ul proiectului Foundry.
- `FOUNDRY_API_KEY`: cheia resursei de inferență.
- `CHAT_DEPLOYMENT`: numele deployment-ului Azure, de exemplu `contractai-chat`, pentru un model compatibil cu Responses și function calling.
- `CU_ENDPOINT`, `CU_API_KEY`, `CU_ANALYZER`: resursa Content Understanding și un analyzer configurat pentru documente.
- Parolele locale DB/S3 și `INTERNAL_API_KEY`: valorile demonstrative trebuie înlocuite pentru o instalare menținută.

Placeholder-ele permit pornirea infrastructurii. Textul UTF-8 și DOCX sunt extrase local. PDF și scanuri necesită Content Understanding; analiza și întrebările necesită Foundry. Fără configurare, operația întoarce un cod explicit, fără răspuns AI fictiv. `/health/live` verifică procesul; `/health/ready` întoarce 503 când configurația AI/extracție este incompletă. Health check-urile nu apelează modele facturabile.

În setările de rețea ale mediului adaugă hosturile exacte ale resurselor Azure folosite. Cheile rămân numai pe server. Aplicația este pentru localhost/rețea privată; nu o expune anonim pentru documente confidențiale.

## Fluxul aplicației

1. Încarcă PDF, DOCX, PNG, JPEG, TXT sau Markdown (maximum 50 MiB; text maximum 2 MiB). Semnătura fișierului și structura DOCX sunt verificate, iar upload-ul este spooled în fișier temporar cu SHA-256.
2. Creează o versiune cu `contractKey`, etichetă și opțional versiune părinte din același contract. Conținutul este imuabil. Upload-urile active identice reutilizează documentul.
3. Așteaptă extragerea, apoi pornește explicit analiza. Jobul și rezultatul sunt persistente; UI afișează starea și erorile.
4. Verifică obligațiile, termenele relative, necunoscutele și observațiile `needs_review`. Datele absolute sunt acceptate numai când apar explicit în dovezi în format ISO; zilele lucrătoare și calendarele jurisdicționale nu sunt calculate.
5. Selectează două versiuni analizate din același contract. Backend-ul aliniază clauzele și păstrează textul înainte/după; Foundry explică schimbările, cu citări către versiunile corecte.
6. Pune întrebări strict despre versiunea selectată. Conversația este anonimă, cu istoric limitat la patru mesaje. Citările sunt validate ca ID și citat exact; fără dovezi răspunsul indică lipsa informației.
7. Exportă raportul JSON sau șterge sursa. Ștergerea ascunde imediat documentul, invalidează rezultatele și programează ștergerea obiectelor. Toate versiunile care reutilizează acel document sunt afectate. Un reupload după ștergere creează un document nou.

Citările indică pagina numai când extractorul oferă poziție. TXT/DOCX și un rezultat fără layout păstrează pagina necunoscută; pagina 1 nu este inventată. Necunoscutele și citările exacte nu certifică adevărul interpretării: observațiile se revizuiesc de un om.

## Structură

| Proiect/director | Responsabilitate |
| --- | --- |
| `src/ContractAI.Api` | Controllere publice, health și migrare one-shot |
| `src/ContractAI.Services` | Servicii de documente/domeniu și worker durabil |
| `src/ContractAI.Database` | EF Core, migrații, constraints și claim atomic cu lease |
| `src/ContractAI.Domain` | Entități, DTO-uri, modele și reguli deterministe |
| `src/ContractAI.Infrastructure` | S3, HTTP, Foundry Responses, extracție și middleware |
| `src/ContractAI.Ai` | Gateway intern fără acces la PostgreSQL |
| `src/contractai-web` | React/TypeScript, modele, servicii și componente separate |
| `tests/ContractAI.Tests` | Teste unitare și integrare reală DB/S3 |
| `seeds` | Două contracte artificiale în TXT, DOCX și PDF, cu 30/15 zile |

Fiecare clasă, serviciu, entitate și DTO este într-un fișier separat. Dependințele NuGet/npm și tool-urile sunt fixate; lockfile-urile sunt versionate. CSharpier și Prettier formatează codul.

## Testare

```bash
dotnet tool restore
dotnet csharpier check src tests
bash scripts/test.sh
INTEGRATION=1 bash scripts/test.sh
# Browser: Chromium instalat de Playwright sau CHROMIUM_PATH=/usr/bin/chromium.
cd src/contractai-web
npx playwright install chromium
npm run test:e2e
```

Integrarea pornește PostgreSQL și MinIO reale pe porturile locale 54329/59000 într-un proiect Compose separat. Testele AI folosesc un adaptor determinist exclusiv în proiectul de teste; acesta nu există în înregistrările DI de producție. PostgreSQL verifică migrațiile, unique constraints, revision și lease; S3 verifică upload/content/cleanup. Testul de workflow acoperă 30 → 15 zile, citări pe versiune, idempotency, conversație, restart și ștergere. Testele browser folosesc API mock pentru UI; nu reprezintă o probă Azure.

Testul live este separat și implică apeluri facturabile:

```bash
bash scripts/smoke-live.sh
```

Acesta verifică două versiuni artificiale, obligațiile, comparația și întrebările prin serviciul configurat. PDF-urile seed se folosesc suplimentar pentru proba Content Understanding. CI nu apelează Azure implicit. Pentru containere pornite cu placeholder-ele din `.env.example`, rulează `python3 scripts/smoke-placeholder.py`. Vezi `VALIDATION.md` pentru verificările efectiv executate în mediul curent.

## Limitele profilului livrat

Profilul implementat este `RAG_PROVIDER=NoRag`, permis de ghid pentru documente mici. Maximum 200 clauze/100.000 caractere; depășirea este o eroare explicită. Alegerea altui provider este respinsă la startup. Azure Search, pgvector, File Search, MCP, OpenAPI găzduit, Speech/Translator/Language, generare media și orchestrare multi-agent sunt extensiile de laborator opționale din ghid și nu sunt activate/implementate în acest profil. Nu există fallback care să le simuleze.

Foundry folosește REST v1, prompt versionat, maximum 5 tool calls/rounds, 15.000 tokeni și deadline 60 secunde. Tool-ul `search_clauses` citește numai sursele injectate de backend. Worker-ul are lease 90 secunde și maximum 3 încercări pentru erori tranzitorii, backoff și `Retry-After`. Content Understanding persistă URL-ul operației între polling-uri și validează hostul înainte de reatașarea cheii. Rezultatele raw/normalizate sunt păstrate în S3. Nu se promite exactly-once pentru facturarea apelurilor cloud.

Logurile conțin coduri, job IDs și trace IDs, nu documente integrale, răspunsuri provider sau secrete. Filtrele de conținut și politicile de safety se configurează în deployment-ul Foundry. Prompt Shields și exporter-ul Application Insights sunt integrări suplimentare; nu pretindem că sunt validate de testele locale.

## Documentație Microsoft consultată

- [.NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Azure OpenAI Responses](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/responses?view=foundry-classic)
- [Content Understanding analyzeBinary 2025-11-01](https://learn.microsoft.com/en-us/rest/api/contentunderstanding/content-analyzers/analyze-binary?view=rest-contentunderstanding-2025-11-01)
- [Content Understanding REST quickstart](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/quickstart/use-rest-api)
