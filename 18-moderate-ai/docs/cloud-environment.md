# Codex cloud

Task-urile sunt deja izolate. Refolosește checkout-ul `/workspace/vi-ai-apps`; nu crea worktree decât dacă utilizatorul cere explicit.

```sh
cd /workspace/vi-ai-apps/18-moderate-ai
bash scripts/install-cloud.sh
bash scripts/start-cloud.sh
bash scripts/test.sh
```

SDK-ul verificat SHA-512 este în `/workspace/.tools/dotnet`. Activează fiecare shell cu `source scripts/activate.sh`. Setup-ul verifică Node 22.12+ și restaurează lockfiles fără modificări. Go este extras din imaginea oficială numai pentru health probe; nu construiește MinIO.

`cloud-build.py` publică binare .NET self-contained **linux-x64**, cu globalization invariant, și construiește imagini locale mici ca diferență față de imaginea Go/Debian deja verificată și reținută. Baza Go este mai mare decât un runtime optimizat, dar evită redirect-ul MCR blocat în mediul curent. Standard Dockerfiles folosesc runtime-ul Microsoft. Imaginile cloud sunt native x64; alte platforme trebuie să folosească Dockerfiles standard sau un publish corespunzător.

Storage-ul compatibil S3 este SeaweedFS, digest pinned; MinIO din PDF nu mai poate fi descărcat, iar build-ul upstream necesită destinații Go suplimentare blocate. Publicarea nu depinde de acele artefacte.

`compose.cloud.yaml` montează numai CA bundle-ul public al mediului în gateway pentru proxy HTTPS, fără a dezactiva TLS. Nu monta credential files. Serverele locale se testează prin cereri interne; nu crea linkuri preview pentru loopback.

Startup normal folosește numai web port 8088. `compose.test.yaml` publică DB 5488/S3 9088/AI 5188 numai pe loopback pentru teste. Repornește serviciile reținute cu `bash scripts/start-cloud.sh`. Procesele active nu sunt presupuse păstrate după restaurarea snapshot-ului.

Chromium de sistem este disponibil: `PLAYWRIGHT_CHROMIUM_EXECUTABLE=/usr/bin/chromium npm --prefix src/moderateai-web run test:e2e`. Browserul Playwright bundled este opțional; download-ul lui din `cdn.playwright.dev` este blocat de politica curentă. Testele browser folosesc Chromium local cu protocolul Playwright, fără a modifica verificările.

Pentru modul Foundry, configurează server-side placeholders din `.env` și permite hosturile exacte ale resurselor Content Safety și OpenAI în setările de rețea. Nu sunt necesare chei Azure pentru Demo. Nu afișa valorile `.env`, process environment, S3 identity config sau `docker compose config` complet. Salvează numai numele/statusul variabilelor când diagnostichezi secrete.
