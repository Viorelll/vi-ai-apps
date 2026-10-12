# ProcureAI

Aplicație de achiziții după [ghidul furnizat](docs/04_ProcureAI_Ghid_Dezvoltare_NET_Foundry.pdf): definești necesarul, încarci oferte, rulezi patru roluri distincte și primești scoruri .NET și o recomandare cu surse. Tot codul aplicației este în acest director.

## Pornire

Necesită Docker Compose 2.24+; pentru build nativ: .NET 10, Node 22.12+ și Python 3.

```bash
cd 04-procure-ai
cp .env.example .env
docker compose config --quiet
docker compose up -d --build
```

Acces local: portul web 8080 este legat numai la loopback. API, gateway AI, PostgreSQL și S3 rămân în rețeaua internă. `migrate` aplică migrațiile înaintea API-ului. Volumele păstrează datele; nu utiliza `down -v` dacă vrei să le păstrezi.

Configurația implicită este **Demo**, fără apeluri cloud. În ecran, apasă „Încarcă exemplul A / B”, așteaptă documentele pregătite, apoi „Rulează agenții”. Oferta A: cost 1050 EUR, scor 0.7900; oferta B: cost 1100 EUR, scor aproximativ 0.6982. Demo normalizează fișiere JSON; fișierele text comerciale libere cer Foundry.

## Cloud / alternativă S3 verificată

Dacă build-ul MinIO arhivat sau rețeaua builder-ului nu sunt disponibile:

```bash
python3 scripts/prepare-s3.py
scripts/build-local-images.sh
docker compose -f compose.yaml -f compose.s3.yaml up -d --no-build
```

Alternativa S3 folosește SeaweedFS cu imagine fixată prin digest, în locul containerului MinIO. Sunt tot cinci containere permanente. Pentru mediul cloud curent, `scripts/install-cloud.sh` pregătește SDK-ul/dependențele/imaginile; `scripts/start-cloud.sh` pornește serviciile și verifică API-ul.

## Microsoft Foundry real

În `.env`, setează `AI_MODE=Foundry` și înlocuiește `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_API_KEY`, `CHAT_DEPLOYMENT` sau deployment-urile individuale. Endpoint-ul Responses are forma `https://RESOURCE.openai.azure.com/openai/v1/`. Pentru PDF/imagine completează separat `CU_ENDPOINT`, `CU_API_KEY` și `CU_ANALYZER`. Apoi reaplică `docker compose up -d`. Cheile nu se pun în `VITE_*`, git sau browser.

`RAG_PROVIDER=NoRag` este traseul implementat. [Acoperirea și limitele](docs/IMPLEMENTATION.md) disting nucleul de extensiile opționale din ghid. Testele cu chei dummy validează demonstrația și contractele HTTP; nu certifică resursele Azure reale.

## Structură

- `src/ProcureAI.Api`: controllere, middleware și compunerea monolitului.
- `src/ProcureAI.Application`: servicii de documente, analize, chat, worker, scoring și validare; interfețe de infrastructură.
- `src/ProcureAI.Domain`: câte un fișier pentru fiecare entity, DTO și model comun.
- `src/ProcureAI.Database`: EF Core, migrații, repository și job store PostgreSQL.
- `src/ProcureAI.Infrastructure`: S3, clientul HTTP intern și probe de sănătate.
- `src/ProcureAI.Ai`: gateway fără DB, providers Foundry/demo/extraction, prompts și tools.
- `src/procureai-web`: React/Vite/TypeScript, componente și modele separate.
- `src/ProcureAI.Tests`, `tests`, `fixtures`: teste automate și probe funcționale.

## Verificare

```bash
dotnet restore ProcureAI.slnx --locked-mode
dotnet build ProcureAI.slnx -c Release --no-restore
dotnet test ProcureAI.slnx -c Release --filter 'Category!=Integration'
npm --prefix src/procureai-web ci
npm --prefix src/procureai-web test
npm --prefix src/procureai-web run build
dotnet format ProcureAI.slnx --verify-no-changes --no-restore
npm --prefix src/procureai-web run format:check
```

Pentru testele reale PostgreSQL/S3, pornește suplimentar `compose.test.yaml` (publică numai porturi loopback 5433/9000). Configurează `TEST_DB_CONNECTION`, `TEST_S3_ENDPOINT`, `TEST_S3_ACCESS_KEY`, `TEST_S3_SECRET_KEY`, apoi rulează `scripts/test-integration.sh`. Testele creează baze de date și bucket-uri temporare proprii; nu folosesc EF InMemory. Contul de test PostgreSQL trebuie să poată crea baze de date.

```bash
docker compose -f compose.yaml -f compose.s3.yaml -f compose.test.yaml up -d --no-build
python3 tests/smoke.py --restart
```

Proba funcțională folosește documente artificiale, repornește API/AI și scrie `artifacts/smoke-results.json`. Raportul include explicit faptul că Foundry live nu a fost apelat. Nu rula proba de restart într-o instanță folosită de alte persoane.
