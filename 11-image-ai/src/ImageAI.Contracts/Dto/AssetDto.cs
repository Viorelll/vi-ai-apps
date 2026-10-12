namespace ImageAI.Contracts.Dto;

public sealed record AssetDto(
    Guid Id,
    Guid RunId,
    Guid? ParentAssetId,
    int Width,
    int Height,
    string Sha256,
    DateTimeOffset CreatedAtUtc
);
