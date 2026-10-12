# Acoperirea specificației

| Cerință | Implementare / verificare |
| --- | --- |
| Ecran unic brief/scenariu/player/remix | React, Playwright pe Chromium real |
| API, servicii, DB în proiecte separate | șapte proiecte .NET, un API modular și un gateway AI fără DB |
| Foundry text + Sora 2 preview | adaptor REST v1, contracte fake HTTP; live necesită resurse reale |
| Confirmare înainte de submit | control UI + regulă backend, 409 fără confirmare |
| Submit/poll/download durabile | SQL claim atomic, checkpoint provider, lease/reclaim, teste PostgreSQL reale |
| Submit incert | checkpoint Submitting, Uncertain, fără resubmit automat; test de recuperare după crash |
| MP4 și proveniență | FFprobe dimensiuni/durată, SHA-256, S3, deployment/prompt/snapshot și link remix |
| Redare / download | player Chromium, HTTP Range 206, content-type verificat |
| Migrare one-shot | migrări EF versionate, mod --migrate, aplicare repetată testată |
| Surse și documente | upload deduplicat, UTF-8/magic bytes, PDF text, extracție durabilă, chat scoped, ștergere |
| Izolare | browser doar /api; gateway fără proiect Database; cheile interne/server-only |
| Teste reproductibile | lockfiles, .NET + frontend + API smoke + browser; baseline sintetic |
| RAG alternativ / familii AI-103 | extensii opționale, neactivate; nu sunt prezentate drept funcționale |

Modul Demo verifică pipeline-ul local, nu modelul generativ. Smoke test-ul Foundry real nu se execută implicit și nu este considerat trecut fără un raport dintr-un run autorizat.
