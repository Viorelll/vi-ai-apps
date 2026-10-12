# Verificare ContractAI — 12 octombrie 2026

Profil verificat: .NET 10.0.401, React/Vite, PostgreSQL 17 și MinIO construit din commit-ul oficial `9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a`. `RAG_PROVIDER=NoRag`; credențialele Azure sunt placeholders.

| Verificare executată | Rezultat |
| --- | --- |
| Restore NuGet cu lockfile și `npm ci` | Trecut |
| Build soluție Release | Trecut, 0 warnings/0 errors |
| xUnit, inclusiv DB/S3 reale | 36 trecute, 0 eșuate, 0 omise |
| Vitest | 4 trecute |
| Playwright cu Chromium, API mock | 2 trecute |
| CSharpier/Prettier și `git diff --check` | Trecut |
| Build Web, API, AI și MinIO, profil cloud | Trecut; TLS/checksum verificate |
| Compose și migrare one-shot | Cinci servicii pornite, migrate exit 0 |
| Cereri reale prin Nginx/API/gateway/S3 | Upload TXT/DOCX, extragere, original stream și ștergere trecute |
| Restart API/AI | Procesul revine; documentul Ready este păstrat; proxy DNS dinamic verificat |
| Configurație Foundry absentă | Job Failed cu `foundry_not_configured`, readiness 503; fără AI fictiv |

Testul de integrare folosește PostgreSQL și MinIO reale; numai AI este înlocuit prin DI în proiectul de teste. Acoperă migrații repetabile și snapshot corect, label unic, revision incompatibilă, hash deduplication, termen 30 → 15, date relative fără dată inventată, citări strict pe versiune, requestId/conversation scope, lipsa dovezilor, restart, invalidare după ștergere și reupload. Testul de lease folosește o bază izolată și verifică revendicarea atomică, reclaim și respingerea token-ului vechi.

Testele de gateway folosesc HTTP simulat pentru Responses, status incomplete, tool call output, usage acumulat, maximum cinci tool calls și o singură reparare JSON. Validarea respinge ID-uri/citate, sume, părți și date inventate. Pagina necunoscută rămâne necunoscută.

Defecțiunile de mediu întâlnite au fost rezolvate: trust CA pentru proxy, acces la sursele MinIO, folosirea archivei SDK Microsoft când MCR regional este blocat, spațiu ocupat de cache-uri, permisiuni non-root, `no_proxy`/`NO_PROXY` pentru servicii interne și re-rezolvarea DNS de către Nginx după restart. Codul celorlalte aplicații nu a fost schimbat.

## Probe care rămân neexecutate

- Azure Foundry și Content Understanding reale: nu există endpoint/deployment/chei furnizate pentru ContractAI. `scripts/smoke-live.sh` este livrat, dar nu este raportat ca trecut. OCR/PDF/scans și interpretarea modelului trebuie verificate după configurarea resurselor.
- Publicarea snapshot-ului mediului și restaurarea într-o sarcină nouă aparțin platformei și nu sunt confirmate prin aceste teste.
- Profilul standard MCR nu a putut fi construit în acest cloud din cauza accesului regional; profilul `compose.cloud.yaml` a fost construit și rulat.
- Extensiile de laborator RAG alternativ/MCP/media/multi-agent/Prompt Shields/Application Insights nu sunt implementate în profilul NoRag. Detaliile de scope sunt în README.

Nu pretindem că un răspuns model este adevărat numai fiindcă schema și citările sunt valide. Evaluarea live și revizuirea umană rămân necesare pentru analiza contractelor.
