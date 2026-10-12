# Validare executată — 12 octombrie 2026

| Verificare                     | Rezultat                                                                                                                                    |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| SDK Microsoft 10.0.401         | Instalat; SHA-512 verificat din metadata oficială                                                                                           |
| Restore NuGet/npm              | Lockfile-uri fixe, restore/ci executate                                                                                                     |
| Build .NET Release             | Reușit, fără avertismente sau erori                                                                                                         |
| Teste .NET cu `INTEGRATION=1`  | 32 trecute, 0 eșuate, 0 omise                                                                                                               |
| PostgreSQL/S3 reale            | Migrații, hash unic, upload/download, cleanup, concurrency, claim/reclaim și revocare lease verificate                                      |
| Flux API complet cu AI de test | Fotografii + manual → raport cu două tipuri de dovezi → checklist local → întrebare deduplicată → export → restart → ștergere și invalidare |
| Foundry fake HTTP              | Payload multimodal, tool scope/limit, JSON repair, răspuns incomplet, 429/503/401 și Retry-After verificate                                 |
| EXIF                           | Orientare 6 convertită în PNG cu dimensiuni corecte și fără EXIF; imagine coruptă respinsă                                                  |
| Frontend build                 | TypeScript + Vite production reușite                                                                                                        |
| Vitest                         | 2 teste trecute                                                                                                                             |
| Chromium / Playwright          | 2 scenarii trecute: creare context și raport cu dovezi/manual/checklist local                                                               |
| Formatare                      | CSharpier, Prettier și Black executate; C#/frontend verificate                                                                              |
| Compose                        | Cinci servicii permanente healthy; migrare one-shot încheiată cu succes                                                                     |
| Smoke infrastructură           | Upload real, extracție TXT, preview PNG normalizat, inspecție persistentă și eroare explicabilă pentru placeholder Foundry                  |
| Restart API/AI                 | Smoke infrastructură repetat după restart, cu datele existente păstrate                                                                     |
| Repetabilitate                 | Scriptul de instalare și cel de pornire rulate repetat; sursele și lockfile-urile păstrate                                                  |

Nu s-au apelat modele Azure reale. Testele deterministe sunt injectate numai de proiectul de teste; producția nu conține provider AI fictiv. Foundry live, Content Understanding PDF live, identificarea semantică a modelului fizic și calitatea interpretării fotografiilor rămân de verificat după înlocuirea placeholder-elor și configurarea accesului la resursele Azure. Criteriile baseline-ului sunt în `eval/baseline.json`, iar smoke-ul live este explicit și separat.

Readiness cloud întoarce 503 cu placeholder-ele actuale. Aceasta este o limitare cunoscută de configurare, nu un test trecut al inferenței. Profilul `NoRag` nu implementează laboratoarele opționale din ghid. Template-ul CI din `ci/github-actions.yml` nu este activat automat în GitHub.
