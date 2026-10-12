namespace ShopVisionAI.Contracts.Dto;

public sealed record CreateSearchRequest(
    Guid ImageDocumentId,
    string? Question,
    string CatalogVersion = "seed-v1"
);
