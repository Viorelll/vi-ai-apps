# EvalAI

EvalAI compara modele și versiuni de prompt pe aceleași cazuri sintetice sau golden cases. Un monolit .NET 10 deține API-ul, datele și joburile; un gateway .NET separat face apelurile Foundry; React/Vite prezintă un singur panou. PostgreSQL păstrează dataset-uri imuabile, baseline-uri, usage și checkpoint-uri. Stocarea locală folosește API S3 prin SeaweedFS, fixat la o imagine cu digest verificat.

## Pornește laboratorul demonstrativ

Ai nevoie de Docker Compose v2.24+, Git și Python 3.11+. Mediul cloud folosește .NET 10.0.401 și Node 22.21.1. Din rădăcina `20-eval-ai`:

```bash
cp .env.example .env
./scripts/start-cloud.sh
```

Scriptul creează un fișier local `.dev/s3.json` ignorat de Git, pornește PostgreSQL, storage-ul S3, gateway-ul AI, migrarea EF o singură dată, API-ul și web-ul. Aplicația este publicată numai pe `127.0.0.1:8080`. Datele seed sunt artificiale. Demo acceptă operații `sum` cu array numeric și produce usage sintetic clar marcat; judge-ul din Demo nu măsoară calitatea unui model.

Deschide `http://localhost:8080`. Seed-ul compară A 3/3 cu B 2/3 și permite repetarea rulării ca baseline. Rulările păstrează checks deterministe, judge, p50/p95, failures, tokens, costul per deployment/rate version și export JSON/JSONL/CSV. Fără tarife salvate, costul rămâne null. Un seed mic nu demonstrează semnificație statistică.

## Activează Foundry Live

În `.env`, schimbă `FOUNDRY_MODE=Live` și completează `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_API_KEY`, `CANDIDATE_A`, `CANDIDATE_B` și `JUDGE_DEPLOYMENT` cu deployment-urile permise de proiectul Foundry. Păstrează endpoint-ul Azure cu `/openai/v1/`. Schimbă `CANDIDATE_A_PROMPT_VERSION`/`CANDIDATE_B_PROMPT_VERSION` doar între `candidate-v1` și `candidate-v2`. Cheile sunt consumate doar de gateway și nu intră în bundle-ul web sau în request-urile browserului. Live nu se declară pregătit până când `/health/ready` și un apel smoke cu modelul real nu trec.

PDF-urile și imaginile necesită un analyzer Content Understanding configurat prin `CU_ENDPOINT`, `CU_API_KEY` și `CU_ANALYZER`. Textul TXT/MD și DOCX se extrage local; imaginile și PDF-urile nu primesc text inventat când Azure lipsește. Upload-ul este limitat la 50 MiB și verifică extensia/semnătura. Chunk-urile țin generația activă; ștergerea documentului marchează rulările afectate ca stale și blochează citările ulterioare.

În pagina Tarife introdu prețul verificat pentru fiecare deployment și versiune. API-ul îl fixează la crearea rulării. Raportul calculează doar tokenii cu tarif complet, currency și rateVersion; taxele de media/tool/cache nu sunt incluse. Erorile după un request extern marchează posibilitatea unui cost repetat când usage-ul nu poate fi confirmat.

## Dezvoltare și verificări

```bash
./scripts/install-cloud.sh
./scripts/test.sh
npm --prefix src/evalai-web run test:e2e
```

Testele de integrare au nevoie de serviciile PostgreSQL și S3 locale din Compose. Acoperă migrațiile PostgreSQL, unique/concurrency constraints, lease reclaim, checkpoint după eșec de judge, retry, hash/deduplicare upload, read/write S3, ștergere, baseline și tarife. E2E-ul browserului folosește Chromium-ul instalat (`PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` poate indica alt binar).

Pentru dezvoltare pe host, Compose expune PostgreSQL/S3 doar pe loopback (`55420`, `59020`); API-ul ascultă `5100`, gateway-ul `5101`, iar Vite proxy `/api` la API. În containere, bazele/API/storage nu publică porturi. PostgreSQL și obiecte persistă în volume Compose; `docker compose down` le păstrează.

## Scope și limite

Nucleul implementează Responses pentru candidat/judge, variante de model/prompt allowlistate, checks .NET, context/citări limitate la sursele Ready selectate, joburi EF durabile, upload S3, chat anonim și costuri versionate. Cheile și expected answers nu intră în promptul candidatului. Judge-ul primește separat expected/rubric și nu poate modifica checks. Nicio rulare nu schimbă automat vreun deployment.

Retrieval RAG hibrid, embeddings/Search, MCP/OpenAPI tools, provisioning și runtime pentru agenți Foundry, managed identity Entra, evaluări Foundry cloud, speech, Translator, Language, Vision, Safety, imagine/video generation și Application Insights/OTel rămân integrări opționale care cer resurse Azure, deployment-uri și credențiale. Varianta de bază nu se prezintă ca adaptor funcțional pentru aceste servicii. Conversațiile sunt anonime, fără login și fără autorizare între utilizatori; ține instanța pe localhost/rețea privată. Pentru restore reia joburile prin lease/DB; costul unui request cloud reexecutat după crash poate fi facturat din nou.

SeaweedFS oferă endpoint S3 local și este fixat la digest pentru testare reproductibilă. PDF-ul menționează că distribuția MinIO Community este arhivată; înlocuiește providerul prin interfața S3 înainte de a alege o distribuție suportată pentru producție. Nu șterge volumele cu `docker compose down -v` decât când dorești resetarea datelor.
