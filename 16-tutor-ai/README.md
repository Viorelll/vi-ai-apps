# TutorAI — tutor AI-103 / .NET

Un ecran React pentru topic, explicație citată, exercițiu, răspuns și feedback. Sesiuni anonime, rubrică ascunsă, evaluare deterministă .NET și adaptare reproductibilă. Proiectele `Api`, `Services`, `Database`, `Domain`, `Infrastructure` și `Ai` sunt separate, cu câte un tip în fiecare fișier.

## Pornire

Necesită .NET SDK **10.0.401**, Node **22.12+**, Docker Compose. Pe Linux x64 în cloud:

```bash
./scripts/install-cloud.sh
cp .env.example .env # doar dacă nu există deja
# Editează .env cu valorile tale. Pentru demonstrație locală: AI_DEMO_MODE=true
./scripts/start-local.sh
```

Browser: portul local **8080**. Numai web publică un port, legat la loopback. PostgreSQL și S3 păstrează datele în volume. `docker compose down` oprește fără ștergerea datelor; `down -v` șterge datele.

Serviciul `minio` este rolul de stocare S3 și rulează **SeaweedFS** fixat prin digest. MinIO a trecut la distribuție din surse, iar dependența oficială console nu este accesibilă în acest mediu; alternativa S3 permite testarea completă a aplicației. Nu este o emulare Foundry.

Profilul cloud folosește artefacte self-contained compilate pe host și `compose.cloud.yaml` pentru a evita restricțiile registrelor Microsoft. `./scripts/start-cloud.sh` configurează exclusiv demonstrația locală. Profilul standard `compose.yaml` construiește imaginile .NET folosind SDK-ul oficial.

## Configurare reală

`.env.example` conține numai placeholder-e. Nu există secrete în frontend. Setează `FOUNDRY_OPENAI_ENDPOINT` la endpointul resursei care se termină cu `/openai/v1/`, `FOUNDRY_API_KEY`, `CHAT_DEPLOYMENT`, și `JUDGE_DEPLOYMENT` separat pentru răspunsurile deschise; apoi `AI_DEMO_MODE=false`. Pentru PDF/imagine: `CU_ENDPOINT`, `CU_API_KEY` și `CU_ANALYZER`. Numele deployment-urilor nu sunt automat numele modelelor din catalog. Alege deployment-uri compatibile cu Responses/function calling. Modurile demo și live nu se substituie automat.

Cheile locale DB/S3/internal sunt dummy pentru dezvoltare. Înlocuiește-le înainte de alt tip de deployment. Nu expune această aplicație anonimă public pentru date confidențiale. Nu pune chei în `VITE_*` sau Git.

## Teste

```bash
./scripts/test.sh
python3 scripts/smoke.py
# Separat, doar cu resurse Azure configurate și buget:
python3 scripts/smoke-live.py
```

Testele xUnit folosesc PostgreSQL și S3 reale, nu EF InMemory. Porturi izolate de test: 54336 și 59016. `TutorAI.Api --migrate` aplică migrațiile și seed-ul; este idempotent. CI nu apelează Azure. Frontend: `npm --prefix src/tutorai-web run format:check`. Backend: `dotnet format TutorAI.slnx --verify-no-changes --no-restore`.

Endpoint-uri: `/api/tutor/sessions`, `/{id}/explain`, `/{id}/exercises`, `/api/tutor/exercises/{id}/attempts`, `/api/lessons/sources`, `/api/documents`, `/api/jobs/{id}`, `/api/chat`. Operațiile lungi întorc 202 și sunt urmărite prin sesiune/job. `revision` previne mutații concurente și `requestId` face submit-ul idempotent. Endpointul exercițiului întoarce numai promptul și opțiunile.

Nucleul Foundry REST, tool search în surse permise și extracția Content Understanding sunt implementate. RAG vectorial/hibrid și laboratoarele opționale Speech, Language, Translator, generare media, hosted agents/MCP sunt extensii neactivate, nu servicii emulate. Vezi [arhitectura](docs/architecture.md), [sursele oficiale](docs/official-sources.md) și [raportul de validare](VALIDATION.md) pentru limitele exacte. Verificarea live Azure necesită credențiale reale; placeholder-ele nu permit certificarea ei.
