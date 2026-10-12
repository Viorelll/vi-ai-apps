# MeetingAI

Asistent local pentru ședințe: încărcare audio, transcriere diarizată, minute cu decizii și acțiuni ancorate în momente audio, corectare locală și export JSON. UI-ul este React/Vite; API-ul modular ASP.NET Core 10 folosește PostgreSQL, iar gateway-ul .NET izolează integrarea Azure Speech și Microsoft Foundry. MinIO păstrează fișierul original; API-ul creează bucket-ul la start după health check. Imaginea locală se compilează din tag-ul de sursă cu fixul de securitate din PDF. Repository-ul MinIO Community este arhivat; această configurare este pentru laborator local, nu o recomandare de deployment menținut.

## Pornire cu Docker Compose

Cerințe: Docker Engine cu Compose v2. Copiază `.env.example` în `.env`, apoi înlocuiește valorile Speech și Foundry cu endpoint-uri și chei valide pentru transcriere reală. Fără acestea, butonul de demonstrație folosește date artificiale; analiza audio reală returnează o eroare clară. Nu comite `.env`.

```sh
cp .env.example .env
./scripts/setup.sh
docker compose up -d
```

Deschide <http://localhost:8080>. API-ul expune numai web-ul pe `127.0.0.1:8080`; baza de date și MinIO nu au porturi publicate. Un serviciu one-shot `migrate` aplică migrațiile EF Core din PostgreSQL înaintea API-ului. Starea PostgreSQL și obiectele MinIO folosesc volume persistente.

## Dezvoltare și verificări

Cerințe locale: .NET 10 SDK, Node.js 22.12+ și Docker Compose v2. `scripts/setup.sh` restorează lockfile-urile, rulează testele, construiește web-ul și API-urile, compilează tag-ul MinIO Community din surse cu checksum Go verificat și împachetează imaginile Docker. Reexecută scriptul după schimbări de cod.

```sh
./scripts/setup.sh
docker compose up -d
curl --fail http://localhost:8080/health/ready
python3 tests/smoke.py
```

`dotnet test` rulează testele validatorului. `tests/smoke.py` verifică MIME-ul upload-ului, API-ul demo, citările temporale, corectarea transcriptului și etichetei, invalidarea rezultatelor vechi, conflictul de revision și exportul JSON. Demo-ul nu cere chei Azure.

API: `POST /api/meetings` (multipart `file`), `POST /api/meetings/{id}/analyze`, `GET /api/meetings/{id}`, `GET /api/meetings/{id}/audio` (Range inclusiv), `PATCH /api/meetings/{id}/transcript` cu `revision` așteptată, `GET /api/meetings/{id}/export`. `POST /api/meetings/demo` creează un scenariu determinist, fără Azure.

Speech folosește fast transcription cu diarization, iar Foundry extrage sumarul, deciziile și acțiunile. Gateway-ul validează referințele la segmente cunoscute; responsabilii și termenele sunt `null` dacă modelul nu îi găsește expliciți. Editarea transcriptului invalidează deciziile/acțiunile vechi. Exportul nu efectuează scrieri externe.

## Limitări curente

Foundry/Speech necesită resurse Azure și chei proprii. API-ul nu are conturi. Search/RAG, agentic retrieval, ingestia documentelor și laboratoarele AI-103 sunt extensii ale specificației, nu parte din fluxul audio V1. Transcrierea și rezumarea rulează sincron prin request; joburile durabile cu lease/retry din specificație rămân de implementat. Durata media nu este încă decodată local; adaptorul Speech aplică limitele serviciului Azure.
