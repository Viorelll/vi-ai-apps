namespace ImageAI.Contracts.Dto;

public sealed record RunDto(
    Guid Id,
    string Kind,
    string State,
    string? ErrorCode,
    string Prompt,
    Guid? InputAssetId,
    Guid? InputDocumentId,
    Guid? MaskDocumentId,
    string Size,
    string Quality,
    string? Deployment,
    string? UsageJson
);
