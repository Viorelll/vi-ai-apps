namespace ShopVisionAI.Contracts.Dto;

public sealed record CandidateExplanation(
    string Sku,
    string Reason,
    IReadOnlyList<string> SourceIds
);
