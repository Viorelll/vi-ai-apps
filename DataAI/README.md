# DataAI

Analist AI pentru datele companiei, conform nucleului funcțional din `07_DataAI.pdf`. Interogările în limbaj natural se transformă într-o specificație strictă, allowlistată. API-ul filtrează datele din PostgreSQL, calculează cu `decimal`, persistă rezultatul și servește aceleași valori pentru tabel și graficul SVG. Microsoft Foundry poate planifica agregarea și redacta explicația; browserul nu primește chei.

Aplicația este izolată în acest director și folosește cinci servicii permanente (`web`, `api`, `ai`, `db`, `minio`) plus serviciul one-shot `migrate`. Datele sunt sintetice. Aplicația nu oferă conturi, SQL arbitrar sau administrare de business.

## Pornire cu Docker Compose

Necesită Docker Compose v2.40+ și acces la registry-urile Docker/npm/NuGet. În acest mediu:

```sh
cd /workspace/vi-ai-apps/DataAI
cp .env.example .env
bash scripts/install-cloud.sh
bash scripts/start-cloud.sh
python3 scripts/smoke.py
```

Web-ul este expus numai pe `127.0.0.1:8080`. PostgreSQL, object storage S3, API-ul și gateway-ul AI nu publică porturi pe host. Compose folosește SeaweedFS la endpointul local S3, compatibil cu interfața MinIO din aplicație, cu imagine fixată prin digest. `scripts/prepare-s3.py` citește credentialele din `.env` și generează configurația locală ignorată de Git. Runtime-ul self-contained folosește globalization invariant ca să nu depindă de biblioteci de sistem care nu se pot descărca din build; API-ul parsează numere/date în invariant, iar UI formatează folosind locale-ul browserului. Compose rulează migrarea înaintea API-ului și păstrează datele în volume. Oprirea normală este `docker compose down`; `docker compose down -v` șterge datele.

Modifică `.env` cu endpointul resursei Foundry, cheia server-side și numele deploymentului Responses API (`CHAT_DEPLOYMENT`). Placeholder-ele activează modul demo local, astfel încât seed-ul, calculele, tabelul și graficul funcționează fără credențiale. Pentru o instalare conectată la Azure, folosește o resursă compatibilă cu Responses API și deployment-ul indicat. Cheile Azure AI Search/Content Understanding nu sunt necesare pentru fluxul de agregare a CSV-ului.

## Dataseturi

La prima pornire se creează setul sintetic EUR din PDF: Hardware `2 × 10.00 + 1 × 5.00 = 25.00`, Software `3 × 7.00 = 21.00`, total `46.00`.

Importul acceptă CSV UTF-8 de maximum 50 MiB, maximum 100.000 rânduri, cu antetul exact:

```csv
sold_on,category,quantity,unit_price,currency
2026-01-05,Hardware,2,10.00,EUR
2026-01-06,Hardware,1,5.00,EUR
2026-01-07,Software,3,7.00,EUR
```

Datele sunt validate și importate tranzacțional; originalul CSV se păstrează în MinIO. Pentru integritate, un set poate folosi o singură monedă. Agregările suportate sunt `revenue` și `quantity`, grupate după `category` sau `day`. Orice câmp în afara allowlist-ului este respins înainte de calcul.

## Proiecte

- `src/DataAI.Domain`: entități și contracte DTO.
- `src/DataAI.Application`: cazuri de utilizare, interfețe și calcule deterministe.
- `src/DataAI.Database`: `AppDbContext`, maparea PostgreSQL și migrații EF Core.
- `src/DataAI.Infrastructure`: S3/MinIO și clientul HTTP către AI gateway.
- `src/DataAI.Api`: controllere, API monolitic și migrare one-shot.
- `src/DataAI.Ai`: gateway fără bază de date, integrare Responses API, planning și explicații.
- `src/dataai-web`: React, Vite, TypeScript și SVG fără executarea codului returnat de model.
- `tests/DataAI.Tests`: teste .NET pentru precizia calculului, allowlist și intervale.

## Dezvoltare și verificare

Necesită .NET SDK 10.0.401, Node 22.12+ și Python 3. `scripts/install-cloud.sh` fixează runtime-ul în `/workspace/.tools/dotnet`, folosește `DOTNET_CLI_HOME=/workspace/.cache/dotnet`, `NUGET_PACKAGES=/workspace/.cache/nuget` și cache-ul npm `/workspace/.cache/npm`, restaurează, testează și publică API-ul/gateway-ul self-contained pentru Linux. Artefactele locale din `artifacts/` permit imagini runtime fără SDK și reduc numărul registrelor accesate la pornire.

```sh
dotnet restore DataAI.slnx --runtime linux-x64 --locked-mode
dotnet tool restore
dotnet build DataAI.slnx -c Release --no-restore
dotnet test DataAI.slnx -c Release --no-build
npm --prefix src/dataai-web ci --cache /workspace/.cache/npm
npm --prefix src/dataai-web run build
npm --prefix src/dataai-web test
docker compose config --quiet
```

Migrațiile sunt incluse în API. Modifică schema prin migrații EF; nu utiliza `EnsureCreated`. Testele Foundry live ar trebui executate separat după configurarea endpointului și deploymentului. Demo-ul nu pretinde că oferă grounding/RAG.

Pentru o modificare de schemă, creează migrația cu `dotnet ef migrations add DescriptiveName --project src/DataAI.Database --startup-project src/DataAI.Api --output-dir Persistence/Migrations` din rădăcina DataAI.

## Limitări asumate în prima versiune

Acest nucleu implementează analiza structurată sales-v1, importul CSV, Foundry Responses, PostgreSQL, MinIO, health checks, UI și teste deterministe. Adaptoarele opționale din ghid pentru Azure AI Search, pgvector, File Search, Content Understanding, Speech, Language, Translator, media generation, MCP, agentic retrieval și cozi durabile nu sunt incluse în această versiune. API-ul agregă sincron seturi de maximum 100.000 de rânduri. Pentru funcții acestea, extinde infrastructura în task separat.
