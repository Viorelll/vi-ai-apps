namespace ImageAI.Contracts.Dto;

public sealed record VariantRequest(
    Guid RequestId,
    int Revision,
    Guid AssetId,
    string Brief,
    string Quality = "medium"
);
