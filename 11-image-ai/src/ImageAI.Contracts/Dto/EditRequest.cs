namespace ImageAI.Contracts.Dto;

public sealed record EditRequest(
    Guid RequestId,
    int Revision,
    Guid InputAssetId,
    Guid MaskDocumentId,
    string Prompt,
    string Quality = "medium"
);
