# AccessReadAI

Cititor accesibil pentru documente. Încarcă un PDF, PNG, JPEG, TXT sau Markdown și oferă originalul alături de textul simplificat, traducere și narare audio. Fiecare fragment păstrează pagina sursă. Fragmentele OCR cu încredere scăzută sunt marcate pentru verificare, iar audio nu se generează înaintea acesteia.

Aplicația folosește React, TypeScript și Vite pentru o singură interfață cu navigare la tastatură, etichete semantice și audio fără redare automată. API-ul ASP.NET Core 10 deține PostgreSQL, schema EF Core, validarea deterministă și joburile cu lease recuperabile după restart. Gateway-ul AI este un proiect ASP.NET Core separat, fără acces la PostgreSQL. Originalele, imaginile extrase și audio sunt păstrate într-un serviciu S3 compatibil. În Compose pornesc cinci containere persistente: `web`, `api`, `ai`, `db` și `minio`; `migrate` rulează o singură dată.

## Pornește local

Cerințe: Docker Compose v2, Python 3, Node.js 22.12+ și acces HTTPS la proxy-ul Git/npm/debian configurat de mediul gazdă.

```bash
scripts/install-cloud.sh
scripts/start-cloud.sh
```

`install-cloud.sh` verifică semnătura SHA-512 a SDK-ului .NET 10.0.401 de pe metadatele oficiale Microsoft și restaurează dependențele NuGet și npm din lockfile-uri. `start-cloud.sh` pregătește configurația S3, publică API-ul și gateway-ul pentru Linux x64, preia pachetele eSpeak din depozitul Debian semnat, compilează interfața, validează Compose, pornește stack-ul și rulează verificările end-to-end cu PostgreSQL și S3 reale.

Interfața locală este disponibilă la `http://127.0.0.1:8080`. Nici PostgreSQL, S3, API-ul, nici gateway-ul AI nu publică porturi către host. Copia locală `.env` și datele persistente rămân în volumele Docker. `docker compose stop` oprește containerele fără să șteargă date. `docker compose down -v` șterge definitiv volumele.

## Mod demo și serviciile Microsoft

`.env.example` pornește explicit `DEMO_MODE=true`. Mod demo suportă TXT, Markdown și fluxul artificial din `fixtures/reading.txt`, folosește eSpeak real pentru audio și un set explicit de traduceri artificiale. Documentele sunt găsite numai în textul extras local și citările indică fragmentele persistate. Mod demo nu pretinde OCR pentru PDF/imagine și nu face apeluri facturabile.

Pentru documente scanate și procesare live, setează `DEMO_MODE=false` și completează în `.env` valorile server-side Azure: `LAYOUT_ENDPOINT`, `LAYOUT_API_KEY`, `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_API_KEY`, `CHAT_DEPLOYMENT`, `TRANSLATOR_ENDPOINT`, `TRANSLATOR_API_KEY`, `TRANSLATOR_REGION`, `SPEECH_TTS_ENDPOINT` și `SPEECH_API_KEY`. Deployment-ul Foundry trebuie să accepte Responses API; Translator și Speech au endpoint-uri și credențiale proprii. Cheile nu intră în bundle-ul browserului. După configurare rulează `docker compose up -d --build`, apoi verifică o pagină reprezentativă din regiunea ta. Cheile placeholder nu activează Azure. Scanul, Azure Live, Document Intelligence, Translator și Speech necesită resurse autorizate; aceste operații nu au putut fi confirmate fără credențiale reale.

Documentația Microsoft folosită pentru adaptorul Layout verifică schema PDF și câmpurile `paragraphs`, `pages`, `words`, `figures`: [Document Intelligence layout](https://learn.microsoft.com/azure/ai-services/document-intelligence/prebuilt/layout). Contractul de inferență folosește [Responses API](https://learn.microsoft.com/azure/ai-services/openai/how-to/responses), iar traducerea și SSML urmează documentația pentru [Translator v3](https://learn.microsoft.com/azure/ai-services/translator/text-translation/reference/v3/translate) și [Speech TTS](https://learn.microsoft.com/azure/ai-services/speech-service/rest-text-to-speech). Pentru producție în Azure înlocuiește cheia de infrastructură internă, folosește identități gestionate/roluri minime unde suportate și confirmă modelele/tier-urile disponibile în regiunea aleasă.

## Soluția

- `src/AccessReadAI.Domain` păstrează entitățile și regulile de domeniu, câte o clasă per fișier.
- `src/AccessReadAI.Contracts` păstrează DTO-urile publice și contractele gateway-ului.
- `src/AccessReadAI.Database` deține DbContext-ul, configurarea PostgreSQL, fabrica EF și migrațiile.
- `src/AccessReadAI.Infrastructure` conține clientul S3 și limita `IObjectStore`.
- `src/AccessReadAI.Services` orchestrează upload-ul, lectura, chat-ul citat și worker-ul durabil.
- `src/AccessReadAI.Api` oferă endpoint-uri publice, migrarea one-shot, health check, rate limit și middleware erorilor.
- `src/AccessReadAI.Ai` implementează adaptoarele Layout, Responses, Translator și TTS; nu cunoaște schema bazei de date.
- `src/accessreadai-web` conține DTO-urile TypeScript validate la runtime și ecranul accesibil.

## Contracte utile

- `POST /api/documents` — upload multipart; validează semnătura fișierului, limita de 50 MiB, calculează SHA-256 și deduplică obiectele identice.
- `GET /api/documents` și `GET /api/documents/{id}` — listă și stare de ingestie.
- `GET /api/documents/{id}/content` — descarcă originalul privat prin API.
- `POST /api/documents/{id}/retry`, `DELETE /api/documents/{id}` — replanifică extracția sau ascunde sursa și curăță obiectele printr-un job.
- `POST /api/readings` — atașează limba originalului și limba țintă unui document.
- `POST /api/readings/{id}/generate` — trimite munca în coada PostgreSQL și răspunde `202`.
- `GET /api/readings/{id}` — polling pentru blocuri, pagini, variante, marcaje de verificare și segmente audio.
- `PATCH /api/readings/{id}/variants` — acceptă o corectare cu revizia optimistă și verifică cifre, unități și negații.
- `GET /api/readings/{id}/audio/{segmentId}` și `GET /api/readings/{id}/export` — redare audio și export JSON structurat.
- `POST /api/chat` — răspuns grounded doar din documentele selectate, cu citări reconstruite pe server.
- `GET /health/live` și `GET /api/health` — disponibilitate proces, PostgreSQL, bucket S3 și gateway.

Header-ul `X-Internal-Key` protejează gateway-ul AI; endpoint-ul și cheia nu sunt publicate către browser. Documentele sunt date neîncredere, conținutul modelului nu este randat ca HTML, callback-urile tool-urilor au scope limitat la fragmentele primite, iar răspunsurile și operation URLs ale providerului sunt validate înainte de persistare. Baza de date este gardianul final pentru documente și generația sursei. Joburile au heartbeat neexternalizat: o singură extracție sau transformare poate dura până la timeout-ul fix; lease-ul expiră și operația reia din checkpoint. Un apel Foundry acceptat înainte de un restart poate fi facturat de două ori dacă rezultatul nu s-a salvat încă.

## Verifică

```bash
scripts/test.sh
```

Suită unit: `dotnet test AccessReadAI.slnx --no-restore -m:2`; controale UI: `npm --prefix src/accessreadai-web run lint`, `test`, `build`, `format:check`. `npm --prefix src/accessreadai-web run test:e2e` rulează Chromium headless disponibil în mediul acesta, axe-core pentru accesibilitate, navigare prin tastatură, redarea metadatelor WAV, ecran responsiv și persistență după refresh. `scripts/smoke.py` folosește PostgreSQL, storage S3 și serviciile Compose reale; verifică upload-uri concurente, deduplicare, conținut original, joburi, trei variante aliniate, audio WAV, refuzul corectărilor care modifică sensul, conflictul de revizie, citări, lipsa dovezilor și ștergere. `scripts/integration.py` oprește/pornește worker-ul pentru a verifica reluarea jobului după expirarea lease-ului, fencing cu token vechi, lipsa chunk-urilor duplicate, persistența datelor după restart și `NeedsReview`.

Testele live Microsoft necesită chei și deployment-uri valide și nu se execută implicit în CI. Demo-ul nu este un substitut pentru evaluarea OCR/Translator/Speech în regiunea reală.
