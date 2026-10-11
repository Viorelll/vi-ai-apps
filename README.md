# DocuAI

DocuAI is a private, single-library document assistant based on the supplied development guide. It provides a React UI, a .NET API, a separate .NET AI gateway, PostgreSQL and S3-compatible object storage.

## Run locally

1. Copy `.env.example` to `.env` and replace the local passwords and Azure placeholders.
2. Run `docker compose up --build`.
3. Open <http://localhost:8080> (the web port binds to loopback only).

The first startup builds the .NET services and frontend, creates the database schema and starts the five long-running services. Text and Markdown ingestion works without Azure. PDF, DOCX and media extraction and generated answers require a configured Microsoft Foundry Content Understanding resource and a Responses API deployment. Configure Azure AI Search only if you enable that provider; the default RAG provider uses the local PostgreSQL chunk store.

## Supported file types

TXT, Markdown, PDF, DOCX, JPEG/PNG, WAV/MP3 and MP4 are accepted up to 50 MiB. Text and Markdown are extracted locally. Other formats are sent to the configured Content Understanding analyzer. A document remains failed with a safe setup hint when that service is not configured.

## Validate

```sh
docker compose config --quiet
npm --prefix src/docuai-web ci
npm --prefix src/docuai-web run build
dotnet build src/DocuAI.Api/DocuAI.Api.csproj -c Release
dotnet build src/DocuAI.Ai/DocuAI.Ai.csproj -c Release
```

The app has no user accounts. Keep it on localhost or a private network. Azure, database and storage credentials belong in `.env`, which is ignored by git. See [`.env.example`](.env.example) for placeholders.
