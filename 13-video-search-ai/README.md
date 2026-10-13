# VideoSearchAI

Bibliotecă video care găsește transcriptul și scenele relevante, răspunde cu surse verificate și deschide videoclipul original la momentul citat. Aplicația nu are conturi și rulează ca monolit API, gateway AI, PostgreSQL, MinIO și interfață React.

## Arhitectură

- `VideoSearchAI.Api` deține API-ul, PostgreSQL, regulile produsului și workerul durabil.
- `VideoSearchAI.Services` implementează upload, căutare, citări și joburi reluabile.
- `VideoSearchAI.Domain` conține entități și reguli temporale.
- `VideoSearchAI.Database` conține contextul EF Core și migrațiile PostgreSQL.
- `VideoSearchAI.Contracts` găzduiește DTO-urile independente de transport.
- `VideoSearchAI.Infrastructure` implementează stocarea S3 și clientul HTTP intern.
- `VideoSearchAI.Ai` procesează fișiere, indexează evidence, caută și răspunde fără acces la baza de date.
- `src/videosearchai-web` oferă interfața React/TypeScript.

În modul `Demo`, o căutare deterministă folosește numai fixture-ul artificial din `seeds/postgresql-course.mp4`; încărcarea altor clipuri nu inventează transcripturi. În modul `Foundry`, Content Understanding (`prebuilt-videoSearch`) extrage transcriptul temporizat și scenele, deployment-ul Azure OpenAI generează embeddings `text-embedding-3-small` și răspunsuri cu `gpt-4.1-mini`, iar Azure AI Search indexează și filtrează generația activă. Apelurile la serviciile cloud se fac doar în modul Foundry.

Timestamps rămân pe axa videoclipului original. Clipurile se încarcă în S3 cu deduplicare SHA-256; starea, checkpoint-urile și lease-urile joburilor se păstrează în PostgreSQL. Reindexarea construiește o generație nouă înainte de publicare. Browserul primește doar API-ul; endpoint-ul de conținut transmite fluxuri și răspunde la cereri HTTP Range. Răspunsul AI poate cita numai evidence și identificatori autorizați de API.

## Pornire locală

Necesită Docker Compose, .NET 10 SDK, Node 24, FFmpeg pentru generarea fixture-ului și `curl`. Clonează repository-ul o singură dată; taskurile cloud folosesc checkout-ul existent, fără Git worktree nou.

```sh
cp .env.example .env
# Generate the deterministic 3-minute artificial video and SHA-256.
bash scripts/generate-seed.sh
docker compose --env-file .env build
docker compose --env-file .env up --wait
```

Deschide `http://127.0.0.1:8080`. API-ul publicat de Compose este legat numai la loopback. PostgreSQL, gateway-ul AI și MinIO nu publică porturi.

În Demo, încarcă `seeds/postgresql-course.mp4`, apasă „Procesează”, selectează clipul și caută „index pentru query”. Rezultatul fixture-ului este citat la **01:10–01:35**; apăsarea citării face seek la 70 de secunde în original. Fișierul demo este artificial și conține imagine, fără transcript audio.

Datele PostgreSQL și S3 persistă în volume. `docker compose down` păstrează volumele. `docker compose down -v` le șterge complet.

## Configurarea Microsoft Foundry

Copiază `.env.example` în `.env` și schimbă `AI_MODE=Foundry`. Completează pe server deployment-ul Azure OpenAI pentru Responses și embeddings, endpointul/key Content Understanding, analyzer ID-ul disponibil în resursa ta și endpointul/key Azure AI Search. Valorile `DUMMY` sunt exclusiv placeholders. Nu introduce chei în `VITE_*`, commit sau browser. Repornește stack-ul după configurare:

```sh
docker compose --env-file .env up --build --wait
```

Content Understanding acceptă formate video și durate conform limitei resursei. Limita aplicației este 50 MiB și 30 minute; un tier Azure poate impune limite mai mici. Cheile trebuie să aibă acces la resursele selectate. Endpointurile trebuie să fie HTTPS. La prima indexare, Search creează indexul dacă lipsește și refuză o dimensiune vectorială incompatibilă. Ștergerea unui document ascunde imediat fișierul și invalidează citările; jobul de cleanup elimină ulterior obiectele și indexul.

## Verificare

```sh
bash scripts/test.sh
INTEGRATION=1 bash scripts/test.sh
PLAYWRIGHT_BROWSERS_PATH="$PWD/.cache/playwright" npx --prefix src/videosearchai-web playwright install chromium
PLAYWRIGHT_BROWSERS_PATH="$PWD/.cache/playwright" npm --prefix src/videosearchai-web run test:e2e
```

Testele xUnit verifică regulile de timp, maparea oficială CU, citările și filtrele Foundry. Testele PostgreSQL și S3 folosesc containere reale Compose, bucket separat și volum PostgreSQL izolat (`testpgdata`). Testele browserului verifică upload → indexare → căutare → seek și aspectul pe mobil. `scripts/smoke.py --restart` verifică fluxul HTTP complet, deduplicarea, Range prefix/suffix/416, scope-ul surselor, reindexarea, invalidarea citărilor și persistența la restart.

Smoke test live pentru Foundry este separat, poate genera costuri și necesită chei și video autorizat:

```sh
AI_MODE=Foundry docker compose --env-file .env up --build --wait
bash scripts/live-smoke.sh path/to/authorized-video.mp4
```

## Structura proiectului

```text
src/
├── VideoSearchAI.Api/             # API HTTP, streaming, readiness, migrare
├── VideoSearchAI.Ai/              # gateway Foundry, CU, Search, tools limitate
├── VideoSearchAI.Contracts/       # DTO-uri
├── VideoSearchAI.Database/        # DbContext, configurare EF, migrații
├── VideoSearchAI.Domain/          # entity, modele și reguli temporale
├── VideoSearchAI.Infrastructure/  # S3, client HTTP tipizat
├── VideoSearchAI.Services/        # reguli de aplicație și worker PostgreSQL
└── videosearchai-web/             # React + Vite + TypeScript
```
