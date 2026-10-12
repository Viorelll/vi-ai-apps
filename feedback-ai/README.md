# FeedbackAI

Aplicație pentru importul și clasificarea feedbackului în română și engleză. Salvează mesajul original separat de textul redactat, maschează emailurile/telefoanele/adresele simple înainte de clasificare, validează topicul și urgența față de taxonomia fixă și permite corectarea locală a etichetelor. Exportul include exclusiv text redactat.

## Pornire cu Docker Compose

Necesită Docker Compose v2 și Node 22+ pentru dezvoltare locală. Compose pornește `web`, `api`, `ai`, `db` și `minio`; serviciul one-shot `migrate` aplică migrațiile EF Core înainte de API. Demo-ul este determinist și funcționează fără credențiale Azure.

```sh
cp .env.example .env
docker compose config --quiet
docker compose up -d --build
docker compose ps
curl -fsS http://127.0.0.1:8080/health/ready
```

Deschide `http://127.0.0.1:8080`. Oprește cu `docker compose down`; volumele bazei de date și MinIO rămân păstrate. `docker compose down -v` șterge datele.

## Microsoft Foundry

În `.env`, setează `AI_MODE=Foundry` și completează `FOUNDRY_OPENAI_ENDPOINT` (baza `/openai/v1`), `FOUNDRY_API_KEY` și `CHAT_DEPLOYMENT` (numele deploymentului). Cheia este folosită doar de gatewayul AI și nu intră în browser. Endpointul folosește formatul Azure OpenAI Chat Completions cu `response_format=json_object`; asigură-te că deploymentul îl acceptă. Actualizează serviciul cu `docker compose up -d --build ai`.

Demo mode nu face inferență AI, OCR sau analiză media. PDF/imagine/audio/video și adaptoarele Content Understanding, Speech, Language, Translator, Azure AI Search, pgvector și agenți Foundry sunt extensii dezactivate; v1 acceptă text introdus/lipit direct. Nu încărca date sensibile în instanța demo.

## Structură

| Proiect | Responsabilitate |
| --- | --- |
| `FeedbackAI.Domain` | Entități și DTO-uri |
| `FeedbackAI.Application` | Taxonomie și redactarea PII |
| `FeedbackAI.Database` | DbContext și configurarea EF Core/PostgreSQL |
| `FeedbackAI.Infrastructure` | Stocarea originalelor prin S3/MinIO |
| `FeedbackAI.Api` | REST API, servicii de aplicație și migrații |
| `FeedbackAI.Ai` | Gateway Foundry și provider demonstrativ |
| `feedbackai-web` | Interfața React, TypeScript și Vite |

## Build și verificare locală

SDK .NET 10.0.401, Node 22.12+ și Docker Compose:

```sh
dotnet restore FeedbackAI.slnx
dotnet build FeedbackAI.slnx -c Release --no-restore
dotnet test FeedbackAI.slnx -c Release --no-build
npm --prefix src/feedbackai-web ci
npm --prefix src/feedbackai-web run build
docker compose config --quiet
```

Migrațiile se aplică numai prin serviciul `migrate`; nu porni schema cu `EnsureCreated`. API-ul expune `/health/live` și `/health/ready`. MinIO se construiește din modulul upstream la versiunea fixată în Dockerfile; Go verifică modulele prin `go.sum`.
