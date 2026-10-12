namespace ImageAI.Contracts.Dto;

public sealed record GenerateRequest(
    Guid RequestId,
    int Revision,
    string Size = "1024x1024",
    string Quality = "medium",
    Guid? InputDocumentId = null
);
