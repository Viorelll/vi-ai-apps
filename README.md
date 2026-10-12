# DocuAI

Codul aplicației și configurațiile ei se află în directorul [`docu-ai/`](docu-ai/README.md).

Pentru pornire locală, copiază `docu-ai/.env.example` în `docu-ai/.env`, completează valorile necesare, apoi rulează din rădăcina repo-ului:

```sh
docker compose --env-file docu-ai/.env -f docu-ai/compose.yaml up --build
```

## ContractAI

Noua aplicație pentru analiza și compararea contractelor se află în [`contract-ai/`](contract-ai/README.md), cu proiecte separate .NET 10, React, PostgreSQL și MinIO. Instrucțiunile de configurare, testare și limitele profilului sunt în acel director.
