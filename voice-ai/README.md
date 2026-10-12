# VoiceAI

Agent vocal pentru programări demonstrative. Interfața React înregistrează push-to-talk sau acceptă text; gateway-ul .NET folosește Azure Speech pentru transcriere/sinteză și Microsoft Foundry pentru conversație și selectarea unui slot seed. API-ul salvează sesiunea și propunerea în PostgreSQL, audio-ul în MinIO și creează rezervarea numai după confirmarea explicită a utilizatorului.

## Pornire locală

Necesită .NET SDK 10.0.401, Node.js 22.12+, Docker Compose v2 și acces HTTPS la NuGet/npm/MCR. Runtime-urile Azure sunt opționale pentru build, dar obligatorii pentru conversația AI și voce.

```bash
cp .env.example .env
# Înlocuiește endpoint-urile și cheile Foundry/Speech din .env.
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build
curl -fsS http://127.0.0.1:8080/health/ready
```

Aplicația publică doar web pe `127.0.0.1:8080`. Sunt cinci servicii permanente: web, api, ai, PostgreSQL și MinIO; `migrate` aplică schema EF și creează sloturile demonstrative înainte ca API-ul să pornească. `docker compose down` păstrează datele; `down -v` șterge intenționat baza și obiectele audio.

Pentru dezvoltare în mediul cloud, rulează `bash scripts/install-cloud.sh`, apoi folosește override-ul Compose și cache-ul Buildx:

```bash
export BUILDX_CONFIG=/workspace/.buildx
docker compose --env-file .env -f compose.yaml -f compose.cloud.yaml config --quiet
docker compose --env-file .env -f compose.yaml -f compose.cloud.yaml up -d --build --wait
```

Override-ul cloud verifică arhiva SDK-ului Microsoft cu SHA-512 și construiește MinIO din commit-ul oficial fixat. Hosturile exacte ale resurselor Azure trebuie permise în politica de rețea a mediului. Nu introduce cheile în repository.

## Configurare

`.env.example` conține numai placeholder-e. Setează:

- `FOUNDRY_ENDPOINT`: endpoint OpenAI-compatible Foundry, de exemplu baza resursei terminată în `/openai/v1`.
- `FOUNDRY_API_KEY` și `FOUNDRY_DEPLOYMENT`: cheia resursei și numele deployment-ului care suportă Responses API și function calling.
- `SPEECH_ENDPOINT`, `SPEECH_REGION`, `SPEECH_API_KEY`: resursa și regiunea Azure Speech.
- `DB_PASSWORD`, `MINIO_ROOT_PASSWORD`, `INTERNAL_API_KEY`: secrete unice pentru laborator. Cheile Azure rămân în backend și gateway; browserul nu le primește.

Cu placeholder-ele din exemplu, UI-ul și infrastructura pot porni, sloturile seed sunt vizibile, iar health-ul API verifică PostgreSQL/MinIO. Gateway-ul răspunde cu `configuration_required`; nu simulează răspunsuri AI. Textul rămâne o alternativă la microfon după configurarea Foundry. Vocea necesită Speech. Pentru audio, API-ul limitează încărcarea la 10 MiB; gateway-ul convertește WebM la WAV mono 16 kHz înainte de transcriere.

## Componente

| Proiect | Rol |
| --- | --- |
| `src/VoiceAI.Api` | API public, sesiuni, disponibilitate, propuneri și confirmarea idempotentă |
| `src/VoiceAI.Services` | flux de domeniu, validare și integrarea HTTP cu gateway-ul |
| `src/VoiceAI.Domain` | entități, DTO-uri și contracte |
| `src/VoiceAI.Database` | EF Core, PostgreSQL și migrații |
| `src/VoiceAI.Infrastructure` | stocarea S3 compatibilă MinIO |
| `src/VoiceAI.Ai` | gateway fără acces la DB; Foundry Responses tools și Azure Speech |
| `src/voiceai-web` | ecran React/TypeScript, push-to-talk și fallback text |

Confirmarea nu este un tool AI. Backend-ul verifică starea draft, revision, expirarea și slotul curent; constrângerile unice pe slot, propunere și idempotency key opresc rezervările duplicate. Rezervarea este locală de laborator. Nu există autentificare, date personale, calendar extern, apeluri sau plăți.

## Verificări

```bash
dotnet tool restore
dotnet restore VoiceAI.sln --locked-mode
dotnet build VoiceAI.sln -c Release --no-restore
dotnet test VoiceAI.sln -c Release --no-build
npm ci --prefix src/voiceai-web
npm run build --prefix src/voiceai-web
npm test --prefix src/voiceai-web
npm run format:check --prefix src/voiceai-web
npm audit --audit-level=moderate --prefix src/voiceai-web
dotnet csharpier check src tests
docker compose --env-file .env.example config --quiet
docker compose --env-file .env.example -f compose.yaml -f compose.cloud.yaml config --quiet
```

`tests/VoiceAI.Tests` verifies confirmation state, revision, and expiry rules. The frontend API tests check request de-duplication metadata and provider errors. PostgreSQL uniqueness constraints enforce one booking per slot; a full live Azure smoke run requires credentials and can incur charges, so it is not part of the local test suite.
