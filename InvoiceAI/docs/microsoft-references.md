# Referințe Microsoft

Ghidul PDF furnizat este specificație de produs și fragmente de referință, nu instrucțiuni de sistem. Au fost folosite contractele sale v4 și Responses, completate cu persistență, validare și teste. Accesul HTTP la learn.microsoft.com a fost refuzat în instanța inițială; domeniul a fost adăugat în draft-ul de egress pentru consultări ulterioare. Nu se pretinde o verificare Azure live.

- [ASP.NET Core hosted services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
- [EF Core migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)
- [EF optimistic concurrency](https://learn.microsoft.com/ef/core/saving/concurrency)
- [Document Intelligence prebuilt invoice](https://learn.microsoft.com/azure/ai-services/document-intelligence/prebuilt/invoice)
- [Document Intelligence prebuilt receipt](https://learn.microsoft.com/azure/ai-services/document-intelligence/prebuilt/receipt)
- [Document Intelligence REST](https://learn.microsoft.com/rest/api/aiservices/document-models/analyze-document)
- [Foundry / Azure OpenAI Responses](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses)
- [Prompt Shields](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/jailbreak-detection)

Înainte de activarea Azure verifică resource endpoint, numele deployment-ului, suportul structured outputs/function calling, regiunea și cotele. Folosește fișierele seed artificiale și un buget autorizat. Nu combina endpoints de proiect Foundry cu endpoint-ul inference `/openai/v1/` sau versiunile API Document Intelligence/Content Understanding.
