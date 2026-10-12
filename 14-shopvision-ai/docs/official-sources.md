# Referințe Microsoft
- [EF Core: lifetime, factory și siguranța DbContext](https://learn.microsoft.com/ef/core/dbcontext-configuration/). Sursa oficială consultată: `dotnet/EntityFramework.Docs`, `entity-framework/core/dbcontext-configuration/index.md`. Worker-ul folosește un context separat pe operație; nu păstrează tranzacții în apelurile AI.
- [Azure OpenAI Responses API](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses): endpoint OpenAI v1, `input_image`, răspunsuri structurate și usage. Deploymentul trebuie să suporte vision și JSON schema strict.
- [Migrații EF Core](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying): migrare one-shot înainte de API, nu `EnsureCreated` în aplicație.
- [Hosted services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services): anulare cooperativă și servicii scoped prin factory.

PDF-ul este specificație și exemple de referință; codul fragmentelor nu este copiat ca instrucțiune de shell și nu presupune existența API-urilor SDK prezentate ca pseudocod.
