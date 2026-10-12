# VI AI Apps

Aplicațiile sunt independente. Catalogul urmează numerotarea din lista de proiecte; directoarele care nu sunt încă în repository sunt marcate explicit.

| Nr. | Proiect | Director | Model / serviciu AI configurat |
| --- | --- | --- | --- |
| 01 | DocuAI | [docu-ai/](docu-ai/README.md) | Foundry Responses (`CHAT_DEPLOYMENT=docuai-chat`); Content Understanding pentru extragere; deployment-ul de embeddings este opțional (`docuai-embed`) |
| 02 | SupportAI | [support-ai/](support-ai/README.md) | Foundry Responses (`supportai-chat`); Content Understanding analyzers `prebuilt-documentSearch`, `prebuilt-audioSearch` sau `prebuilt-videoSearch` |
| 03 | InvoiceAI | [03-InvoiceAI/](03-InvoiceAI/README.md) | Demo determinist; în Azure: Document Intelligence `prebuilt-invoice` / `prebuilt-receipt` și Foundry Responses (`invoiceai-chat`) |
| 04 | ProcureAI | [procure-ai/](procure-ai/README.md) | Demo; Foundry Responses (`procureai-chat`) și Content Understanding `prebuilt-documentSearch` |
| 05 | ContractAI | [contract-ai/](contract-ai/README.md) | Foundry Responses (`contractai-chat`) și Content Understanding `prebuilt-documentSearch` |
| 06 | IncidentAI | Nu este încă în repository | Neconfigurat |
| 07 | DataAI | Nu este încă în repository | Neconfigurat |
| 08 | MeetingAI | Nu este încă în repository | Neconfigurat |
| 09 | VoiceAI | Nu este încă în repository | Neconfigurat |
| 10 | FeedbackAI | [feedback-ai/](feedback-ai/README.md) | Demo; Foundry Chat Completions (`feedbackai-chat`) |

Valorile între paranteze sunt nume de deployment exemplificative din configurațiile aplicațiilor, nu nume garantate ale modelelor din catalogul Azure. Modelul efectiv se stabilește în deployment-ul Microsoft Foundry configurat pentru fiecare aplicație. Cheile și endpoint-urile reale se păstrează în `.env`, ignorat de Git; consultă README-ul fiecărui proiect înainte de pornire.
